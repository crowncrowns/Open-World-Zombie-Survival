"""Capture completed native core evidence after its final concurrent-scene guard failed.

Does not run Unity, refresh publication guards, or modify any main/T asset.
The stability claim is an explicit control-flow inference, not a new build run.
"""
from pathlib import Path
import difflib
import hashlib
import json
import re
import subprocess
from datetime import datetime, timezone

R = Path(__file__).resolve().parents[2]
W = R / 'work/KursHub/audit8'
T = R / 'work/KursHub/UnityProject'
D = Path('Assets/Environment/KursHub')
SC = 'Assets/CharacterTesting/CharacterTest.unity'

def sha(p):
    h = hashlib.sha256()
    with Path(p).open('rb') as f:
        for b in iter(lambda: f.read(1048576), b''): h.update(b)
    return h.hexdigest()

def hbytes(b): return hashlib.sha256(b).hexdigest()
def save(p, x):
    with p.open('x', encoding='utf8', newline='\n') as f: json.dump(x, f, indent=2); f.write('\n')

def prepare():
    target = W / 'native_core_receipt8.json'
    if target.exists(): raise RuntimeError('Core receipt is immutable; refusing to refresh')
    log = W / 'native_build_final8.log'
    template = R / 'Tools/KursHub/audit8_build.cs.txt'
    compiled = T / 'Assets/Editor/KursHubBuild8.cs'
    assert sha(template) == sha(compiled), 'Actual failed-run source differs from template'
    source = template.read_text(encoding='utf8')
    text = log.read_text(encoding='utf8', errors='replace')
    exception = 'Main scene changed during verification; inspect before publication'
    errors = re.findall(r'^(?:[\w.]*Exception): (.*)$', text, re.M)
    assert errors == [exception], errors
    markers = ['KURS_VALIDATION_OK:', 'KURS_QUALITY_AUDIT_OK: 69 prefabs, Unity 6000.6.3f1',
               'KURS_DEMO_SCENE_OK:', 'KURS_QUALITY_PACKAGE_OK:', 'Exception: ' + exception]
    positions = [text.index(m) for m in markers]
    assert positions == sorted(positions)
    assert re.search(r'KursHubBuild8.cs:56\s', text), 'Expected final guard location not reached'
    assert source.count('KursHubSetup.BuildPrefab();') == 2
    guard = 'if(first!=Identities())throw new Exception("Second build changed native prefab GUID/local IDs");'
    export = 'KursHubQualitySetup.AuditAndExport();'
    final_guard = 'if(Hash(scenePath)!=sceneSha)throw new Exception("' + exception + '");'
    assert source.index(guard) < source.index(export) < source.index(final_guard)
    package = W / 'delivery/KursHub_Final.unitypackage'
    audit = W / 'delivery/REPORTS/Unity_Asset_Audit.json'
    data = json.loads(audit.read_text(encoding='utf-8-sig'))
    assert data['unityVersion'] == '6000.6.3f1' and len(data['assets']) == 69
    assert all(not x.get('errors') for x in data['assets'])
    assets = {p.relative_to(T).as_posix(): sha(p) for p in (T/D).rglob('*') if p.is_file()}
    editors = {p.relative_to(R).as_posix(): sha(p) for p in (R/'Assets/Editor').glob('KursHub*.cs')}
    assert all(sha(T/p) == h for p,h in editors.items())
    excluded = {str(D/'Models/KursHubShell.fbx').replace('\\','/'), str(D/'KursHub.prefab').replace('\\','/'), str(D/'KursHubManifest.json').replace('\\','/')}
    library = {p:h for p,h in assets.items() if not p.endswith('.meta') and p not in excluded}
    digest_map = lambda m: hbytes('\n'.join(p+':'+m[p] for p in sorted(m)).encode())
    old_scene = subprocess.check_output(['git','show','HEAD:'+SC], cwd=R)
    current_scene = (R/SC).read_bytes()
    preserved_guard = W/'main_scene_snapshot8.before_registry_cleanup.json'
    old_guard = (preserved_guard if preserved_guard.exists() else W/'main_scene_snapshot8.json').read_bytes()
    assert json.loads(old_guard)['sha256'] == hbytes(old_scene), 'Old scene guard is not HEAD scene'
    before = old_scene.decode('utf8').splitlines(keepends=True)
    after = current_scene.decode('utf8').splitlines(keepends=True)
    delta = list(difflib.SequenceMatcher(a=before,b=after,autojunk=False).get_opcodes())
    changes = [(tag,i,j,k,l) for tag,i,j,k,l in delta if tag!='equal']
    assert changes and all(tag=='delete' and all(line.strip()=='- {fileID: 0}' for line in before[i:j]) for tag,i,j,k,l in changes)
    assert sum(j-i for _,i,j,_,_ in changes) == 5
    for _,i,j,_,_ in changes:
        prefix = ''.join(before[:i]); block = prefix[prefix.rfind('--- !u!'):]
        assert block.startswith('--- !u!114 &1692918278\n') and 'WorldSaveRegistry' in block and '  sceneObjects:\n' in block
    refs = lambda b: sorted(set(re.findall(rb'\{fileID: (-?\d+), guid: ff5cdff5a433ef342ad7bf24aeafd8b8, type: 3\}',b)))
    assert refs(old_scene)==refs(current_scene)
    folder = W / 'native_core_evidence8'; folder.mkdir(exist_ok=False)
    (folder/'main_scene_guard_before.json').write_bytes(old_guard)
    (folder/'CharacterTest_before_null_cleanup.unity').write_bytes(old_scene)
    (folder/'CharacterTest_after_null_cleanup.unity').write_bytes(current_scene)
    (folder/'CharacterTest_null_cleanup.diff').write_text(''.join(difflib.unified_diff(before,after,fromfile='HEAD',tofile='current')),encoding='utf8')
    (folder/'Unity_Asset_Audit.json').write_bytes(audit.read_bytes())
    difference = dict(status='INSPECTED_FIVE_NULL_REGISTRY_ENTRIES_ONLY', componentFileId=1692918278,
                      removedNullEntries=5, beforeSha256=hbytes(old_scene), afterSha256=hbytes(current_scene),
                      kursNativeReferencesUnchanged=True, sceneRestored=False, snapshotRefreshed=False,
                      diffSha256=sha(folder/'CharacterTest_null_cleanup.diff'))
    save(folder/'scene_delta_proof.json',difference)
    receipt = dict(status='CORE_BUILD_COMPLETED_FINAL_SCENE_GUARD_FAILED', generatedUtc=datetime.now(timezone.utc).isoformat(),
                   unityVersion='6000.6.3f1', buildLogPath='native_build_final8.log', buildLogSha256=sha(log),
                   sourceBuildPath='Tools/KursHub/audit8_build.cs.txt', sourceBuildSha256=sha(template),
                   compiledBuildSourcePath='Assets/Editor/KursHubBuild8.cs', compiledBuildSourceSha256=sha(compiled),
                   expectedFinalException=exception, markerPositions=dict(zip(markers,positions)),
                   twoBuildIdentityGuardPassedByControlFlow=True,
                   stabilityEvidence='Inference: reaching export and only line56 exception requires both BuildPrefab calls and first!=Identities guard to have completed. No new build was run.',
                   packageSha256=sha(package), packageBytes=package.stat().st_size,
                   auditPath='native_core_evidence8/Unity_Asset_Audit.json', auditSha256=sha(folder/'Unity_Asset_Audit.json'),
                   librarySha256=digest_map(library), assetInventorySha256=digest_map(assets), assets=assets, mainEditors=editors,
                   coreSceneGuardBeforeSha256=hbytes(old_scene), coreSceneGuardAfterSha256=hbytes(current_scene),
                   sceneDeltaProofSha256=sha(folder/'scene_delta_proof.json'),
                   scope='Retained completed core only. Fresh current scene reference and readonly native checks remain mandatory.')
    save(target,receipt)
    print('AUDIT8_CORE_RECEIPT_CAPTURED', len(assets), 'assets;', package.stat().st_size, 'package bytes')

if __name__=='__main__': prepare()
