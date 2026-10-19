from pathlib import Path
import hashlib,json,shutil
R=Path(__file__).resolve().parents[2]
W=R/'work/KursHub/stage6';B=W/'baseline';A=W/'authoring';T=R/'work/KursHub/UnityProject'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert not B.exists(),'Do not replace baseline'
paths=['Assets/Environment/KursHub','Sources/KursHub','Tools/KursHub','Assets/Editor/KursHubSetup.cs','Assets/Editor/KursHubPropSetup.cs','Assets/Editor/KursHubSurfaceSetup.cs','Assets/CharacterTesting/CharacterTest.unity','AGENTS.md','Docs/KursHub.md']
hashes={}
for relative in paths:
 p=R/relative
 for source in ([p] if p.is_file() else p.rglob('*')):
  if not source.is_file() or '__pycache__' in source.parts:continue
  rel=source.relative_to(R);dest=B/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,dest);hashes[rel.as_posix()]=sha(source)
(B/'hashes.json').write_text(json.dumps(hashes,indent=2))
for rel in ['Assets/Environment/KursHub','Sources/KursHub']:
 shutil.copytree(R/rel,A/rel,dirs_exist_ok=True)
# Reuse cached vendor imports, but refresh current project-owned code and scene.
for rel in ['Assets/Scripts','Assets/Editor','Assets/CharacterTesting','Packages','ProjectSettings']:
 for source in (R/rel).rglob('*'):
  if not source.is_file():continue
  dest=T/source.relative_to(R)
  if not dest.exists() or sha(source)!=sha(dest):
   dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,dest)
print('STAGE6_BASELINE_READY',len(hashes),'files; scene',sha(R/'Assets/CharacterTesting/CharacterTest.unity'))

