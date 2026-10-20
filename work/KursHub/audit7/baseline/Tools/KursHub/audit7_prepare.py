"""Snapshot the complete authored hub before the quality revision; never reset Git."""
from pathlib import Path
import hashlib,json,shutil,subprocess
R=Path(__file__).resolve().parents[2]
W=R/'work/KursHub/audit7';B=W/'baseline';A=W/'authoring';T=R/'work/KursHub/UnityProject'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
if (B/'hashes.json').exists():
    raise SystemExit('Baseline exists; do not overwrite it')
paths=[]
for directory in ['Assets/Environment/KursHub','Sources/KursHub','Tools/KursHub']:
    paths += [p for p in (R/directory).rglob('*') if p.is_file() and '__pycache__' not in p.parts]
paths += list((R/'Assets/Editor').glob('KursHub*.cs*'))
paths += [R/p for p in ['AGENTS.md','Docs/KursHub.md','Docs/KursHubInteriorReferenceReview.md','Assets/CharacterTesting/CharacterTest.unity','ProjectSettings/ProjectVersion.txt']]
hashes={}
for p in paths:
    rel=p.relative_to(R);q=B/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q);hashes[rel.as_posix()]=sha(p)
for directory in ['Assets/Environment/KursHub','Sources/KursHub']:
    shutil.copytree(B/directory,A/directory,dirs_exist_ok=True)
shutil.copytree(B/'Assets/Environment/KursHub',T/'Assets/Environment/KursHub',dirs_exist_ok=True)
for p in (B/'Assets/Editor').glob('KursHub*.cs*'):shutil.copy2(p,T/'Assets/Editor'/p.name)
(B/'hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf8')
(B/'git_status.txt').write_text(subprocess.check_output(['git','status','--short'],cwd=R,text=True,encoding='utf8'),encoding='utf8')
(B/'git_head.txt').write_text(subprocess.check_output(['git','log','-3','--oneline'],cwd=R,text=True,encoding='utf8'),encoding='utf8')
print('AUDIT7_BASELINE_OK',len(paths),'files; CharacterTest',hashes['Assets/CharacterTesting/CharacterTest.unity'])
