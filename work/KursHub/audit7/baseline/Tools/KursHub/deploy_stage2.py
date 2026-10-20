"""Publish the verified stage-2 asset update without rewriting CharacterTest.

Run only after the stage-2 isolated Unity checks; baseline hashes reject
concurrent authoring changes, and prefab/source IDs keep scene overrides valid.
"""
from pathlib import Path
import hashlib
import json
import os
import re
import shutil

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'work/KursHub/stage2'
TEST=ROOT/'work/KursHub/UnityProject'
BACKUP=WORK/'backup'
AUTHORING=WORK/'authoring'
ASSETS=Path('Assets/Environment/KursHub')
SCENE=ROOT/'Assets/CharacterTesting/CharacterTest.unity'
PREFAB=ASSETS/'KursHub.prefab'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def guid(path):return re.search(r'^guid: ([a-f0-9]+)',path.read_text(encoding='utf8'),re.M)[1]
def ids(path):return set(re.findall(r'^--- !u!\d+ &(-?\d+)',path.read_text(encoding='utf8'),re.M))

report=(WORK/'play_validation.txt').read_text(encoding='utf8')
assert 'KURS_PLAY_OK:' in report and '\nFAIL ' not in report,'A complete passing Play run is required'
assert 'KURS_STAGE2_SCENE_CHECK_OK' in (WORK/'scene_validation.txt').read_text(encoding='utf8')
assert json.loads((WORK/'geometry.json').read_text())['sourceSolidsValidated']
baseline=json.loads((BACKUP/'hashes.json').read_text())
for path,expected in baseline.items():
    if path.startswith('Assets/Environment/KursHub/') or path in ('Sources/KursHub/KursHub.blend','Assets/Editor/KursHubSetup.cs'):
        candidate=(AUTHORING if path.startswith('Sources/') else TEST)/path
        assert sha(ROOT/path)==expected or (candidate.exists() and sha(ROOT/path)==sha(candidate)),'Authoring changed during verification: '+path
for relative in ('Models/KursHubShell.fbx','KursHubManifest.json'):
    assert sha(TEST/ASSETS/relative)==sha(AUTHORING/ASSETS/relative),'Stale tested geometry'
manifest=json.loads((TEST/ASSETS/'KursHubManifest.json').read_text())
assert manifest['stage']==2
old_names={c['name'] for c in json.loads((BACKUP/ASSETS/'KursHubManifest.json').read_text())['chunks']}
assert old_names<={c['name'] for c in manifest['chunks']},'Legacy FBX names changed'
assert guid(ROOT/(str(PREFAB)+'.meta'))==guid(TEST/(str(PREFAB)+'.meta'))
assert ids(ROOT/PREFAB)<=ids(TEST/PREFAB),'Existing prefab local IDs changed'
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
pending.extend([(AUTHORING/'Sources/KursHub/KursHub.blend',ROOT/'Sources/KursHub/KursHub.blend'),
                (TEST/'Assets/Editor/KursHubSetup.cs',ROOT/'Assets/Editor/KursHubSetup.cs')])
staging=WORK/'atomic_deploy'
staging.mkdir(exist_ok=True)
for index,(source,target) in enumerate(pending):
    prepared=staging/(str(index)+source.suffix)
    shutil.copy2(source,prepared)
    target.parent.mkdir(parents=True,exist_ok=True)
    os.replace(prepared,target)
assert sha(SCENE)==scene_hash,'The asset update must not rewrite the scene'
result=dict(stage=2,scene_sha256=scene_hash,scene_rewritten=False,
            scene_prefab_targets_preserved=len(scene_refs),prefab_ids_preserved=len(ids(BACKUP/PREFAB)),
            prefab_guid=prefab_guid,fbx_sha256=sha(ROOT/ASSETS/'Models/KursHubShell.fbx'),
            prefab_sha256=sha(ROOT/PREFAB),blend_sha256=sha(ROOT/'Sources/KursHub/KursHub.blend'),
            copied_files=len(pending))
(WORK/'deployment.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print('KURS_STAGE2_DEPLOY_OK: assets updated; CharacterTest retained byte-for-byte;',len(scene_refs),'scene targets preserved')
