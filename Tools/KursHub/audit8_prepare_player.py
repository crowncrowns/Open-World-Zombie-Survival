"""Seed current player test inputs into the owned copy; never edit main actors."""
from pathlib import Path
import hashlib,json,os,re,shutil,time,uuid

R=Path(__file__).resolve().parents[2]
W=R/'work/KursHub/audit8'
T=R/'work/KursHub/UnityProject'
assert T.resolve()==(R/'work/KursHub/UnityProject').resolve()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
paths=[Path('Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab'),
       Path('Assets/Scripts/Player/FirstPersonController.cs'),
       Path('Assets/InputSystem_Actions.inputactions')]
records={}
for relative in paths:
 for item in (relative,Path(str(relative)+'.meta')):
  source,target=R/item,T/item
  before=sha(source)
  if item.suffix=='.meta' and target.exists():
   guid=lambda p:re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M)[1]
   assert guid(source)==guid(target),'Current source GUID changed: '+str(item)
  backup=W/'player_seed_before'/item
  if target.exists() and not backup.exists():
   backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(target,backup)
  temporary=target.with_name(target.name+'.kurs8-'+uuid.uuid4().hex+'.tmp')
  shutil.copy2(source,temporary)
  try:
   for attempt in range(8):
    try:os.replace(temporary,target);break
    except OSError as error:
     if getattr(error,'winerror',0) not in (32,1224) or attempt==7:raise
     time.sleep(.5)
  finally:
   if temporary.exists():temporary.unlink()
  assert before==sha(source)==sha(target),'Concurrent source mutation: '+str(item)
  records[item.as_posix()]=before
for source,target in [('audit8_player_check.cs.txt','Assets/Editor/KursHubPlayerCheck8.cs'),
                      ('audit8_player_probe.cs.txt','Assets/KursHubPlayerProbe8.cs')]:
 shutil.copy2(R/'Tools/KursHub'/source,T/target)
report=dict(status='CURRENT_PLAYER_TEST_INPUTS_STAGED',mainFilesUnchanged=True,files=records,
            scope='Current motor implementation, prefab serialized capsule/motor settings and Input Actions. '
                  'The temporary probe creates only the motor/capsule and a private camera; '
                  'this does not validate UAL/avatar/pistol presentation or whole-player gameplay.')
(W/'player_source_sync8.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf8')
print(report['status'])
