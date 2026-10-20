"""Prepare a fresh settings-only Unity project; never launches Unity.

Run once before import to seed the isolated project. Re-run with --package after
the final archive exists to bind expected GUIDs and its SHA-256. No gameplay or
Kurs assets are copied: they must arrive through AssetDatabase.ImportPackage.
"""
from pathlib import Path
import argparse, hashlib, json, re, shutil, subprocess, tarfile

ROOT=Path(__file__).resolve().parents[3]
WORK=ROOT/'work/KursHub/audit7'
PROJECT=WORK/'PackageImportProject'
GUID=re.compile(r'guid:\s*([a-f0-9]{32})')


def write_json(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf8')


def dependencies():
    paths=list((ROOT/'Assets/Settings').glob('*.asset'))+list((ROOT/'ProjectSettings').glob('*.asset'))
    refs={}
    for path in paths:
        text=path.read_text(encoding='utf8')
        for guid in GUID.findall(text):
            if guid.startswith('0000000000000000'):continue
            refs.setdefault(guid,[]).append(path.relative_to(ROOT).as_posix())
    expression=r'^guid: ('+'|'.join(refs)+r')\r?$'
    result=subprocess.run(['rg','-l','--no-ignore','-g','*.meta',expression,'Assets','Packages','Library/PackageCache'],cwd=ROOT,encoding='utf8',stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    if result.returncode not in (0,1):raise RuntimeError(result.stderr)
    resolved={}
    for filename in result.stdout.splitlines():
        path=ROOT/filename
        match=re.search(r'^guid: ([a-f0-9]{32})',path.read_text(encoding='utf8'),re.M)
        if match and match[1] in refs:resolved.setdefault(match[1],[]).append(path.relative_to(ROOT).as_posix()[:-5])
    rows=[]
    for guid,sources in sorted(refs.items()):
        candidates=resolved.get(guid,[])
        rows.append(dict(guid=guid,referencedBy=sources,resolvedPaths=candidates,
                         category='settings' if any(p.startswith('Assets/Settings/') for p in candidates) else 'package' if any(p.startswith(('Library/PackageCache/','Packages/')) for p in candidates) else 'external project asset' if candidates else 'unresolved'))
    return dict(scope='All YAML GUID references in copied Settings assets and ProjectSettings; not the final unitypackage dependency audit.',references=rows,
                externalProjectAssets=[r for r in rows if r['category']=='external project asset'],unresolved=[r for r in rows if r['category']=='unresolved'])


def seed():
    PROJECT.mkdir(parents=True,exist_ok=True)
    if (PROJECT/'Assets/Environment/KursHub').exists():raise RuntimeError('Fresh import project already contains Kurs assets; do not silently reuse it.')
    for relative in ('Packages','ProjectSettings','Assets/Settings'):
        destination=PROJECT/relative
        if destination.exists():continue
        shutil.copytree(ROOT/relative,destination)
    if (ROOT/'Assets/Settings.meta').exists():shutil.copy2(ROOT/'Assets/Settings.meta',PROJECT/'Assets/Settings.meta')
    (PROJECT/'Assets/Editor').mkdir(parents=True,exist_ok=True)
    report=dependencies()
    write_json(WORK/'package_seed_dependencies.json',report)
    write_json(PROJECT/'ValidationConfig.json',dict(packagePath=str((ROOT/'FINAL_ASSETS/KursHub_Final.unitypackage').resolve()),reportPath=str((WORK/'package_import_validation.json').resolve()),previewFolder=str((WORK/'package_import_previews').resolve()),expectedPath=str((PROJECT/'ExpectedPackage.json').resolve()),expectedPrefabCount=55))
    print('PACKAGE_PROJECT_PREPARED',PROJECT)
    print('SETTINGS_EXTERNAL',len(report['externalProjectAssets']),'UNRESOLVED',len(report['unresolved']))


def bind(package):
    if not package.is_file():raise FileNotFoundError(package)
    entries={}
    with tarfile.open(package,'r:gz') as archive:
        for member in archive:
            if member.name.endswith('/pathname'):
                value=archive.extractfile(member).read().decode('utf8').strip().replace('\\','/')
                guid=member.name.split('/')[0]
                if not re.fullmatch('[a-f0-9]{32}',guid):raise RuntimeError('Invalid archive GUID '+guid)
                if value in entries:raise RuntimeError('Duplicate package path '+value)
                entries[value]=guid
    prefabs=[p for p in entries if p.endswith('.prefab') and p.startswith('Assets/Environment/KursHub/')]
    if len(prefabs)<55:raise RuntimeError('At least the existing 55 prefab archive entries are required, found '+str(len(prefabs)))
    unexpected=[p for p in entries if not p.startswith(('Assets/Environment/KursHub','Packages/com.unity.render-pipelines.','Packages/com.unity.shadergraph/')) and p not in ('Assets','Assets/Environment')]
    if unexpected:raise RuntimeError('Unexpected archived assets: '+str(unexpected))
    sha=hashlib.sha256(package.read_bytes()).hexdigest()
    write_json(PROJECT/'ExpectedPackage.json',dict(packageSha256=sha,entries=[dict(path=path,guid=guid) for path,guid in sorted(entries.items())],prefabCount=len(prefabs)))
    config=json.loads((PROJECT/'ValidationConfig.json').read_text(encoding='utf8'));config['packagePath']=str(package.resolve());config['expectedPrefabCount']=len(prefabs);write_json(PROJECT/'ValidationConfig.json',config)
    print('PACKAGE_BOUND',sha,'entries',len(entries),'prefabs',len(prefabs))


def sanitize():
    """Approved test-only reset of inherited unrelated / obsolete references."""
    if (PROJECT/'Library').exists():raise RuntimeError('Sanitize the seed before its first Unity launch.')
    backup=PROJECT/'OriginalSeedSettings';backup.mkdir(exist_ok=True)
    changes=[]
    def replace(relative,text,reason):
        path=PROJECT/relative
        destination=backup/relative;destination.parent.mkdir(parents=True,exist_ok=True)
        if not destination.exists():shutil.copy2(path,destination)
        before=hashlib.sha256(path.read_bytes()).hexdigest()
        path.write_text(text,encoding='utf8')
        changes.append(dict(path=relative,reason=reason,beforeSha256=before,afterSha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    profile=PROJECT/'Assets/Settings/DefaultVolumeProfile.asset'
    text=profile.read_text(encoding='utf8')
    documents=re.split(r'(?=^--- !u!)',text,flags=re.M)
    root=next(d for d in documents if '  m_Name: DefaultVolumeProfile\n' in d)
    root=root.split('  components:')[0]+'  components: []\n'
    replace('Assets/Settings/DefaultVolumeProfile.asset','%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+root,
            'Empty seed-only default volume; old profile included missing scripts / editor-test components. Original project unchanged.')
    renderer=PROJECT/'Assets/Settings/PC_Renderer.asset'
    text=renderer.read_text(encoding='utf8')
    for guid in ('03cfc4915c15d504a9ed85ecc404e607','24ec0e140fb444a44ab96ee80844e18e','53a11f4ebaebf4049b3638ef78dc9664','57d7c4c16e2765b47a4d2069b311bffe','8f96cd657dc40064aa21efcc7e50a2e7','b9a23f869c4fd45f19c5ada54dd82176','e5c6678ed2aaa91408dd3df699057aae'):
        text=re.sub(r'\{fileID: -?\d+, guid: '+guid+r', type: \d+\}','{fileID: 0}',text)
    replace('Assets/Settings/PC_Renderer.asset',text,'Clear seven obsolete unused probe-debug resources in seed only; checker creates an independent fresh Universal renderer.')
    settings=PROJECT/'ProjectSettings/EditorBuildSettings.asset'
    text=settings.read_text(encoding='utf8').split('  m_Scenes:')[0]+'  m_Scenes: []\n  m_configObjects: {}\n'
    replace('ProjectSettings/EditorBuildSettings.asset',text,'Remove unrelated SampleScene build entry and InputSystem_Actions config object from this gameplay-free import test.')
    write_json(WORK/'package_seed_sanitization.json',dict(scope='Only PackageImportProject; authorized by parent; main Assets/Settings and ProjectSettings remain unchanged.',changes=changes,
                runtimeSetup='KursHubPackageImportCheck creates UniversalRendererData and UniversalRenderPipelineAsset.Create(renderer), sets all quality levels to the fresh URP asset.'))
    print('PACKAGE_SEED_SANITIZED',len(changes))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--package',type=Path);parser.add_argument('--sanitize',action='store_true');args=parser.parse_args()
    if args.package:bind(args.package.resolve())
    elif args.sanitize:sanitize()
    else:seed()
