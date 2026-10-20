"""Publish the verified stage-4 asset update without rewriting CharacterTest.

Run only after the stage-4 isolated Unity checks; baseline hashes reject
concurrent authoring changes, and prefab/source IDs keep scene overrides valid.
"""
from pathlib import Path
import hashlib
import json
import os
import re
import shutil

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'work/KursHub/stage4'
TEST=ROOT/'work/KursHub/UnityProject'
BACKUP=WORK/'resume_backup'
AUTHORING=WORK/'authoring'
ASSETS=Path('Assets/Environment/KursHub')
SCENE=ROOT/'Assets/CharacterTesting/CharacterTest.unity'
PREFAB=ASSETS/'KursHub.prefab'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def guid(path):return re.search(r'^guid: ([a-f0-9]+)',path.read_text(encoding='utf8'),re.M)[1]
def ids(path):return set(re.findall(r'^--- !u!\d+ &(-?\d+)',path.read_text(encoding='utf8'),re.M))

report=(WORK/'play_validation.txt').read_text(encoding='utf8')
assert 'KURS_PLAY_OK:' in report and '\nFAIL ' not in report,'A complete passing Play run is required'
assert 'KURS_STAGE4_SCENE_CHECK_OK' in (WORK/'scene_validation.txt').read_text(encoding='utf8')
scene_report=(WORK/'scene_validation.txt').read_text(encoding='utf8')
library_paths=sorted(p for p in (TEST/ASSETS).rglob('*') if p.is_file() and p.suffix!='.meta' and p.relative_to(TEST/ASSETS).as_posix() not in ('Models/KursHubShell.fbx','KursHub.prefab','KursHubManifest.json'))
library_text='\n'.join(p.relative_to(TEST).as_posix()+':'+sha(p) for p in library_paths)
library_hash=hashlib.sha256(library_text.encode('utf8')).hexdigest()
assert 'VERIFIED_LIBRARY_SHA256='+library_hash in report and 'VERIFIED_LIBRARY_SHA256='+library_hash in scene_report,'Prop/material library needs fresh checks'
for label,relative in [('FBX','Models/KursHubShell.fbx'),('PREFAB','KursHub.prefab'),('MANIFEST','KursHubManifest.json')]:
    proof='VERIFIED_'+label+'_SHA256='+sha(TEST/ASSETS/relative)
    assert proof in report and proof in scene_report,'Final geometry needs fresh Play and scene checks: '+label
assert json.loads((WORK/'geometry.json').read_text())['sourceSolidsValidated']
baseline=json.loads((BACKUP/'hashes.json').read_text())
for path,expected in baseline.items():
    if path.startswith('Assets/Environment/KursHub/') or path in ('Sources/KursHub/KursHub.blend','Assets/Editor/KursHubSetup.cs'):
        candidate=(AUTHORING if path.startswith('Sources/') else TEST)/path
        assert sha(ROOT/path)==expected or (candidate.exists() and sha(ROOT/path)==sha(candidate)),'Authoring changed during verification: '+path
for relative in ('Models/KursHubShell.fbx','KursHubManifest.json'):
    assert sha(TEST/ASSETS/relative)==sha(AUTHORING/ASSETS/relative),'Stale tested geometry'
manifest=json.loads((TEST/ASSETS/'KursHubManifest.json').read_text())
assert manifest['stage']==4
for name in ('KursHubSetup.cs','KursHubPropSetup.cs','KursHubSurfaceSetup.cs'):
    assert sha(ROOT/'Assets/Editor'/name)==sha(TEST/'Assets/Editor'/name),'Editor authoring changed since verification: '+name
assert 'Facade_BlueCladding_001' in {c['name'] for c in manifest['chunks']},'Scene-overridden facade chunk lost'
assert guid(ROOT/(str(PREFAB)+'.meta'))==guid(TEST/(str(PREFAB)+'.meta'))
assert {'7181941858700996431','4352749818230737907'}<=ids(TEST/PREFAB),'Prefab root IDs changed'
prefab_guid=guid(ROOT/(str(PREFAB)+'.meta'))
scene_text=SCENE.read_text(encoding='utf8')
scene_refs=set(re.findall(r'\{fileID: (-?\d+), guid: '+prefab_guid+r', type: 3\}',scene_text))
scene_refs.discard('100100000')
assert scene_refs<=ids(TEST/PREFAB),'Scene has unknown prefab child targets: '+str(scene_refs-ids(TEST/PREFAB))
scene_hash=sha(SCENE)

pending=[]
for source in (TEST/ASSETS).rglob('*'):
    if not source.is_file():continue
    target=ROOT/source.relative_to(TEST)
    if source.suffix=='.meta' and target.exists():assert guid(source)==guid(target),'GUID changed: '+str(target)
    if not target.exists() or sha(source)!=sha(target):pending.append((source,target))
pending.extend((AUTHORING/'Sources/KursHub'/name,ROOT/'Sources/KursHub'/name) for name in ('KursHub.blend','KursHubProps.blend'))
for name in ('KursHubSetup.cs','KursHubPropSetup.cs','KursHubSurfaceSetup.cs'):
    for source in (TEST/'Assets/Editor').glob(name+'*'):
        target=ROOT/source.relative_to(TEST)
        if source.suffix=='.meta' and target.exists():assert guid(source)==guid(target),'Editor GUID changed'
        pending.append((source,target))
staging=WORK/'atomic_deploy'
staging.mkdir(exist_ok=True)
for index,(source,target) in enumerate(pending):
    prepared=staging/(str(index)+source.suffix)
    shutil.copy2(source,prepared)
    target.parent.mkdir(parents=True,exist_ok=True)
    os.replace(prepared,target)
assert sha(SCENE)==scene_hash,'The asset update must not rewrite the scene'
result=dict(stage=4,scene_sha256=scene_hash,scene_rewritten=False,
            scene_prefab_targets_preserved=len(scene_refs),prefab_ids_preserved=len(ids(BACKUP/PREFAB)&ids(TEST/PREFAB)),retired_unreferenced_prefab_ids=len(ids(BACKUP/PREFAB)-ids(TEST/PREFAB)),
            prefab_guid=prefab_guid,fbx_sha256=sha(ROOT/ASSETS/'Models/KursHubShell.fbx'),
            prefab_sha256=sha(ROOT/PREFAB),blend_sha256=sha(ROOT/'Sources/KursHub/KursHub.blend'),
            copied_files=len(pending),library_sha256=library_hash,props_blend_sha256=sha(ROOT/'Sources/KursHub/KursHubProps.blend'))
(WORK/'deployment.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print('KURS_STAGE4_DEPLOY_OK: assets updated; CharacterTest retained byte-for-byte;',len(scene_refs),'scene targets preserved')
