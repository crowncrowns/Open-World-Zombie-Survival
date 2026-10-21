"""Prepare a fresh revision-8 archive receiver, preserving its cached Unity Library.

Never launches Unity or touches the game checkout's Assets/ProjectSettings.
First run backs up ONLY the old receiver's Kurs asset directory and probe.
--package binds the actual exported archive, with dynamic counts and hashes.
"""
from pathlib import Path, PurePosixPath
import argparse
import hashlib
import json
import re
import shutil
import tarfile
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'work/KursHub/audit8'
PROJECT = ROOT / 'work/KursHub/audit7/PackageImportProject'
FOLDER = 'Assets/Environment/KursHub'
GUID = re.compile(r'^[a-f0-9]{32}$')


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf8')


def guard_receiver():
    resolved = PROJECT.resolve(strict=True)
    expected = (ROOT / 'work/KursHub/audit7/PackageImportProject').absolute()
    if resolved != expected or not resolved.is_relative_to((ROOT / 'work/KursHub/audit7').resolve()):
        raise RuntimeError('Receiver is outside its explicit owned directory')
    if (resolved / 'Temp/UnityLockfile').exists():
        raise RuntimeError('The owned receiver appears open in Unity; do not alter it')
    version = (resolved / 'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf8')
    if 'm_EditorVersion: 6000.6.3f1\n' not in version:
        raise RuntimeError('Wrong receiver Unity version')
    return resolved


def prepare():
    receiver = guard_receiver()
    marker = WORK / 'package_receiver_prepared8.json'
    if marker.exists():
        state = json.loads(marker.read_text(encoding='utf8'))
        if (receiver / FOLDER).exists():
            raise RuntimeError('Prepared receiver contains Kurs assets; never silently reset an import proof')
        if sha(receiver / 'Assets/Editor/KursHubPackageImportCheck.cs') != sha(ROOT / 'Tools/KursHub/audit8_package_import.cs.txt'):
            raise RuntimeError('Receiver probe changed since preparation')
        print('PACKAGE_RECEIVER_ALREADY_PREPARED', state['backup'])
        return
    backup = WORK / 'package_receiver_backup' / datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    backup.mkdir(parents=True, exist_ok=False)
    old_proof = ROOT / 'work/KursHub/audit7/package_import_validation.json'
    proof_sha = sha(old_proof)
    preserved = []
    for relative in ('ValidationConfig.json', 'ExpectedPackage.json',
                     'Assets/Editor/KursHubPackageImportCheck.cs', 'Assets/Editor/KursHubPackageImportCheck.cs.meta'):
        source = receiver / relative
        if source.is_file():
            destination = backup / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            preserved.append(dict(path=relative, sha256=sha(source)))
    shutil.copy2(old_proof, backup / 'revision7_package_import_validation.json')
    moved = []
    for relative in (FOLDER, FOLDER + '.meta'):
        source = receiver / relative
        if not source.exists():
            continue
        target = source.resolve(strict=True)
        if target != receiver / relative or not target.is_relative_to(receiver / 'Assets/Environment'):
            raise RuntimeError('Unsafe receiver asset target: ' + str(target))
        destination = backup / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(str(target), str(destination))
        moved.append(relative)
    template = ROOT / 'Tools/KursHub/audit8_package_import.cs.txt'
    shutil.copy2(template, receiver / 'Assets/Editor/KursHubPackageImportCheck.cs')
    write_json(receiver / 'ValidationConfig8.json', dict(
        receiverProjectPath=str(receiver),
        packagePath=str(WORK / 'delivery/KursHub_Final.unitypackage'),
        reportPath=str(WORK / 'package_import_validation8.json'),
        previewFolder=str(WORK / 'package_import_previews8'),
        expectedPath=str(receiver / 'ExpectedPackage8.json'),
        expectedPrefabCount=0))
    assert not (receiver / FOLDER).exists() and sha(old_proof) == proof_sha
    write_json(marker, dict(status='PREPARED_NOT_LAUNCHED', receiver=str(receiver), backup=str(backup),
                           preserved=preserved, moved=moved, previousProofSha256=proof_sha,
                           cachedLibraryPreserved=(receiver / 'Library').is_dir(),
                           mainAndTestProjectUntouched=True, probeSha256=sha(template),
                           limitation='Assets are absent; their import must use the real final unitypackage. Cached shader/package Library is reused.'))
    print('PACKAGE_RECEIVER_PREPARED', receiver)
    print('BACKUP', backup)


def read_archive(package):
    """Hash a streaming tar without extracting assets or retaining large PNG/FBX data."""
    records = {}
    with tarfile.open(package, 'r|gz') as archive:
        for member in archive:
            if member.isdir():
                continue
            parts = PurePosixPath(member.name).parts
            if len(parts) != 2 or not GUID.fullmatch(parts[0]):
                raise RuntimeError('Unexpected archive member: ' + member.name)
            guid, kind = parts
            if kind not in ('asset', 'asset.meta', 'pathname', 'preview.png') or not member.isfile():
                raise RuntimeError('Unsupported archive member: ' + member.name)
            row = records.setdefault(guid, {})
            if kind in row:
                raise RuntimeError('Duplicate archive member: ' + member.name)
            stream = archive.extractfile(member)
            digest = hashlib.sha256()
            small = bytearray() if kind in ('pathname', 'asset.meta') or (kind == 'asset' and member.size < 8_000_000) else None
            while block := stream.read(1024 * 1024):
                digest.update(block)
                if small is not None:
                    small.extend(block)
            row[kind] = dict(sha256=digest.hexdigest(), bytes=member.size)
            if small is not None:
                if kind == 'asset' and small.lstrip().startswith(b'{'):
                    try:
                        row['json'] = json.loads(small)
                    except (ValueError, UnicodeDecodeError):
                        pass
                elif kind in ('pathname', 'asset.meta'):
                    row[kind]['text'] = small.decode('utf-8-sig')
    entries = {}
    for guid, row in records.items():
        if 'pathname' not in row or 'asset.meta' not in row:
            raise RuntimeError('Missing path / meta for GUID ' + guid)
        path = row['pathname']['text'].strip().replace('\\', '/')
        if PurePosixPath(path).is_absolute() or '..' in PurePosixPath(path).parts or ':' in path or path in entries:
            raise RuntimeError('Unsafe / duplicate asset path: ' + path)
        allowed = path in ('Assets', 'Assets/Environment', FOLDER) or path.startswith((FOLDER + '/', 'Packages/com.unity.render-pipelines.', 'Packages/com.unity.shadergraph/'))
        if not allowed:
            raise RuntimeError('Unexpected archived dependency: ' + path)
        match = re.search(r'^guid:\s*([a-f0-9]{32})\s*$', row['asset.meta']['text'], re.M)
        if not match or match[1] != guid:
            raise RuntimeError('Metadata GUID mismatch: ' + path)
        if path.endswith(('.blend', '.blend1')) or (path.startswith(FOLDER + '/') and path.endswith(('.cs', '.py'))):
            raise RuntimeError('Authoring or gameplay code in archive: ' + path)
        entries[path] = dict(guid=guid, row=row)
    return entries


def bind(package):
    receiver = guard_receiver()
    if not (WORK / 'package_receiver_prepared8.json').is_file() or (receiver / FOLDER).exists():
        raise RuntimeError('Prepare an empty owned receiver before binding')
    package = package.resolve(strict=True)
    if not package.is_relative_to(WORK / 'delivery') or package.suffix != '.unitypackage':
        raise RuntimeError('Expected the revision-8 final archive in delivery')
    before = sha(package)
    entries = read_archive(package)
    def manifest(relative):
        return entries[FOLDER + '/' + relative]['row']['json']
    props = manifest('Props/KursHubPropsManifest.json')['props']
    hub = manifest('KursHubManifest.json')
    families = manifest('Textures/Audit7PBRManifest.json')['families']
    prefabs = {p for p in entries if p.endswith('.prefab')}
    wanted = {FOLDER + '/KursHub.prefab'} | {FOLDER + '/Props/Prefabs/' + p['id'] + '.prefab' for p in props}
    if prefabs != wanted:
        raise RuntimeError('Prefab archive does not exactly match the source manifest')
    if entries[FOLDER + '/KursHub.prefab']['guid'] != 'ff5cdff5a433ef342ad7bf24aeafd8b8':
        raise RuntimeError('Building prefab GUID changed')
    # Shell chunk records describe renderer pieces; the three actual shell FBX
    # paths are the stable import contract in KursHubSetup/QualitySetup.
    models = {lod['model'] for p in props for lod in p['lods']} | {
        FOLDER + '/Models/KursHubShell.fbx',
        FOLDER + '/Models/LOD/KursHubShell_LOD1.fbx',
        FOLDER + '/Models/LOD/KursHubShell_LOD2.fbx'}
    actual_models = {p for p in entries if p.lower().endswith('.fbx')}
    if models != actual_models:
        raise RuntimeError('FBX archive does not exactly match the source manifests: ' + str(models ^ actual_models))
    for family in families:
        for suffix in ('BaseColor', 'Normal', 'Roughness', 'Metallic', 'Occlusion', 'Height', 'MetallicSmoothness'):
            if FOLDER + '/Textures/' + family['name'] + '_' + suffix + '.png' not in entries:
                raise RuntimeError('Missing archived family map: ' + family['name'] + ' ' + suffix)
    if hub['width'] != 93 or hub['depth'] != 130 or hub['stage'] != 8:
        raise RuntimeError('Unexpected building dimensions/revision')
    count = sum(len(hub[k]) for k in ('propInstances', 'interiorInstances', 'settlementInstances'))
    png_count = sum(path.lower().endswith('.png') for path in entries)
    expected = dict(packageSha256=before, prefabCount=len(prefabs), propCount=len(props),
                    modelCount=len(actual_models), familyCount=len(families), pngCount=png_count, nestedPropInstances=count,
                    entries=[dict(path=path, guid=row['guid'], contentSha256=row['row'].get('asset', {}).get('sha256', '')) for path, row in sorted(entries.items())])
    if sha(package) != before:
        raise RuntimeError('Archive changed during binding')
    write_json(receiver / 'ExpectedPackage8.json', expected)
    config_path = receiver / 'ValidationConfig8.json'
    config = json.loads(config_path.read_text(encoding='utf8'))
    config.update(packagePath=str(package), expectedPrefabCount=len(prefabs))
    write_json(config_path, config)
    write_json(WORK / 'package_archive_binding8.json', dict(status='BOUND_NOT_IMPORTED', **expected))
    print('PACKAGE_BOUND', before, 'GUIDs', len(entries), 'prefabs', len(prefabs), 'props', len(props),
          'FBX', len(actual_models), 'families', len(families), 'PNG', png_count, 'nested instances', count)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path)
    args = parser.parse_args()
    bind(args.package) if args.package else prepare()
