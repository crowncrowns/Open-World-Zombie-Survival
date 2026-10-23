"""Install staged authored assets into the owned Unity copy, never main."""
from pathlib import Path
import hashlib,json,re,shutil,sys
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit8';A=W/'authoring';T=R/'work/KursHub/UnityProject';D=Path('Assets/Environment/KursHub')
assert T.resolve()==(R/'work/KursHub/UnityProject').resolve()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
guid=lambda p:re.search(r'^guid: ([a-f0-9]+)',p.read_text(encoding='utf-8-sig'),re.M)[1]
changes=[]
previous=json.loads((W/'engine_input8.json').read_text(encoding='utf8'))['inputFiles'] if '--incremental' in sys.argv else {}
for s in sorted((A/D).rglob('*'),key=lambda p:p.as_posix()):
 if not s.is_file():continue
 t=T/s.relative_to(A)
 if s.suffix=='.meta' and t.exists():assert guid(s)==guid(t),str(t)
 # An unchanged authored input must not reset the native materials/prefabs
 # or importer metadata that the previous engine build deliberately authored.
 if t.exists() and previous.get(s.relative_to(A).as_posix())==sha(s):continue
 if not t.exists() or sha(s)!=sha(t):
  t.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(s,t);changes.append(s.relative_to(A).as_posix())
for p in (R/'Assets/Editor').glob('KursHub*.cs'):shutil.copy2(p,T/'Assets/Editor'/p.name)
for src,dest in [('audit8_build.cs.txt','KursHubBuild8.cs'),('audit8_capture.cs.txt','KursHubCapture8.cs'),('audit8_navmesh_check.cs.txt','KursHubNavMeshAudit8.cs')]:
 shutil.copy2(R/'Tools/KursHub'/src,T/'Assets/Editor'/dest)
main={p.relative_to(R).as_posix():sha(p) for folder in (R/D,R/'Sources/KursHub') for p in folder.rglob('*') if p.is_file()}
before=W/'main_before8.json'
if before.exists():
 assert json.loads(before.read_text(encoding='utf8'))==main,'Main authored hub changed concurrently; inspect before staging'
else:before.write_text(json.dumps(main,indent=2),encoding='utf8')
scene=R/'Assets/CharacterTesting/CharacterTest.unity'
scene_guard=W/'main_scene_snapshot8.json'
record=dict(path=scene.relative_to(R).as_posix(),sha256=sha(scene),copied=False)
if scene_guard.exists():assert json.loads(scene_guard.read_text(encoding='utf8'))==record,'Main scene changed concurrently; inspect without replacing it'
else:scene_guard.write_text(json.dumps(record,indent=2),encoding='utf8')
inputs={p.relative_to(A).as_posix():sha(p) for p in (A/D).rglob('*') if p.is_file()}
inputs.update({p.relative_to(T).as_posix():sha(p) for p in (T/'Assets/Editor').glob('KursHub*.cs')
               if (R/p.relative_to(T)).exists()})
(W/'engine_input8.json').write_text(json.dumps(dict(copiedFiles=changes,sceneCopied=False,inputFiles=inputs),indent=2),encoding='utf8')
print('AUDIT8_ENGINE_STAGED',len(changes),'changed asset files; main scene not copied')
