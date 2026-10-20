"""Publish verified modular interiors, retaining the scene and accepted shell."""
from pathlib import Path
import hashlib
import json
import os
import re
import shutil

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'work/KursHub/stage6'
TEST=ROOT/'work/KursHub/UnityProject'
BACKUP=WORK/'baseline'
AUTHORING=WORK/'authoring'
ASSETS=Path('Assets/Environment/KursHub')
SCENE=Path('Assets/CharacterTesting/CharacterTest.unity')
PREFAB=ASSETS/'KursHub.prefab'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def guid(path):return re.search(r'^guid: ([a-f0-9]+)',path.read_text(encoding='utf8'),re.M)[1]
def ids(path):return set(re.findall(r'^--- !u!\d+ &(-?\d+)',path.read_text(encoding='utf8'),re.M))

play=(WORK/'play_validation.txt').read_text(encoding='utf8')
scene=(WORK/'scene_validation.txt').read_text(encoding='utf8')
assert 'KURS_PLAY_OK:' in play and '\nFAIL ' not in play
assert 'KURS_STAGE6_SCENE_CHECK_OK' in scene
paths=sorted(p for p in (TEST/ASSETS).rglob('*') if p.is_file() and p.suffix!='.meta' and p.relative_to(TEST/ASSETS).as_posix() not in ('Models/KursHubShell.fbx','KursHub.prefab','KursHubManifest.json'))
library_hash=hashlib.sha256('\n'.join(p.relative_to(TEST).as_posix()+':'+sha(p) for p in paths).encode()).hexdigest()
for report in (play,scene):
 assert 'VERIFIED_LIBRARY_SHA256='+library_hash in report,'Library changed since checks'
 for label,relative in [('FBX','Models/KursHubShell.fbx'),('PREFAB','KursHub.prefab'),('MANIFEST','KursHubManifest.json')]:
  assert 'VERIFIED_'+label+'_SHA256='+sha(TEST/ASSETS/relative) in report,'Stale '+label+' proof'
assert json.loads((AUTHORING/'work/KursHub/props_geometry.json').read_text())['sourceSolidsValidated']
assert json.loads((AUTHORING/'work/KursHub/props_export_validation.json').read_text())['status']=='KURS_PROP_EXPORTS_OK'
baseline=json.loads((BACKUP/'hashes.json').read_text())
assert sha(ROOT/SCENE)==baseline[SCENE.as_posix()]==sha(TEST/SCENE),'Current scene must match tested baseline byte for byte'
for path,expected in baseline.items():
 if path.startswith('Assets/Environment/KursHub/') or path.startswith('Sources/KursHub/'):
  candidate=(AUTHORING if path.startswith('Sources/') else TEST)/path
  assert sha(ROOT/path)==expected or (candidate.exists() and sha(ROOT/path)==sha(candidate)),'Concurrent authoring change: '+path
for name in ('KursHubSetup.cs','KursHubPropSetup.cs','KursHubSurfaceSetup.cs','KursHubInteriorFinishSetup.cs'):
 assert sha(ROOT/'Assets/Editor'/name)==sha(TEST/'Assets/Editor'/name),'Untested editor change: '+name
manifest=json.loads((TEST/ASSETS/'KursHubManifest.json').read_text())
assert manifest['stage']==6 and len(manifest['interiorZones'])==11 and len(manifest['interiorInstances'])==130
assert sha(TEST/ASSETS/'Models/KursHubShell.fbx')==sha(BACKUP/ASSETS/'Models/KursHubShell.fbx'),'Stage5 must retain accepted shell'
assert sha(TEST/ASSETS/'KursHubManifest.json')==sha(AUTHORING/ASSETS/'KursHubManifest.json')
assert ids(BACKUP/PREFAB)<=ids(TEST/PREFAB),'A previous prefab ID was lost'
prefab_guid=guid(ROOT/(str(PREFAB)+'.meta'))
assert prefab_guid==guid(TEST/(str(PREFAB)+'.meta'))
refs=set(re.findall(r'\{fileID: (-?\d+), guid: '+prefab_guid+r', type: 3\}',(ROOT/SCENE).read_text(encoding='utf8')))-{'100100000'}
assert refs<=ids(TEST/PREFAB),'Scene target lost'
assert len(manifest["settlementInstances"])==542 and len(manifest["settlementLights"])==54
assert json.loads((WORK/"source_validation.json").read_text())["shellGeometryUnchanged"]
pending=[]
for source in (TEST/ASSETS).rglob('*'):
 if not source.is_file():continue
 target=ROOT/source.relative_to(TEST)
 if source.suffix=='.meta' and target.exists():assert guid(source)==guid(target),'GUID changed: '+str(target)
 if not target.exists() or sha(source)!=sha(target):pending.append((source,target))
for name in ('KursHub.blend','KursHubProps.blend'):
 pending.append((AUTHORING/'Sources/KursHub'/name,ROOT/'Sources/KursHub'/name))
def order(pair):
 path=pair[1].relative_to(ROOT).as_posix()
 rank=4 if path=='Assets/Environment/KursHub/KursHub.prefab' else 3 if path.endswith('/KursHubManifest.json') else 2 if '/Props/Prefabs/' in path else 1 if '/Materials/' in path else 0
 return rank,pair[0].suffix!='.meta',path
pending.sort(key=order)
staging=WORK/'atomic_deploy';staging.mkdir(exist_ok=True)
for index,(source,target) in enumerate(pending):
 prepared=staging/(str(index)+source.suffix);shutil.copy2(source,prepared)
 target.parent.mkdir(parents=True,exist_ok=True);os.replace(prepared,target)
assert sha(ROOT/SCENE)==baseline[SCENE.as_posix()]
assert all(sha(s)==sha(t) for s,t in pending),'Deployment readback mismatch'
report=dict(stage=6,scene_sha256=sha(ROOT/SCENE),scene_rewritten=False,shell_unchanged=True,
 prefab_guid=prefab_guid,scene_prefab_targets_preserved=len(refs),prefab_ids_preserved=len(ids(BACKUP/PREFAB)),
 interior_zones=len(manifest['interiorZones']),interior_instances=len(manifest['interiorInstances']),
 library_sha256=library_hash,prefab_sha256=sha(ROOT/PREFAB),manifest_sha256=sha(ROOT/ASSETS/'KursHubManifest.json'),
 blend_sha256=sha(ROOT/'Sources/KursHub/KursHub.blend'),props_blend_sha256=sha(ROOT/'Sources/KursHub/KursHubProps.blend'),copied_files=len(pending),settlement_instances=len(manifest["settlementInstances"]),settlement_lights=len(manifest["settlementLights"]))
(WORK/'deployment.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('KURS_STAGE6_DEPLOY_OK:',len(pending),'files;',len(refs),'scene targets; scene and shell retained')
