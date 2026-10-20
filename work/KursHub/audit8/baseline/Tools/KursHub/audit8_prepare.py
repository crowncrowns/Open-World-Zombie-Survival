"""Freeze the complete accepted revision before a new spatial/art pass."""
from pathlib import Path
import hashlib,json,shutil,subprocess
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit8';B=W/'baseline';A=W/'authoring';T=R/'work/KursHub/UnityProject'
if (B/'hashes.json').exists():raise SystemExit('AUDIT8 baseline already exists; never overwrite it.')
paths=[]
for directory in ('Assets/Environment/KursHub','Sources/KursHub','Tools/KursHub'):
 paths.extend(p for p in (R/directory).rglob('*') if p.is_file() and '__pycache__' not in p.parts)
paths.extend((R/'Assets/Editor').glob('KursHub*.cs*'))
paths.extend(R/p for p in ('AGENTS.md','Docs/KursHub.md','Docs/KursHubQualityAudit.md','Assets/CharacterTesting/CharacterTest.unity','ProjectSettings/ProjectVersion.txt'))
hashes={}
for p in paths:
 rel=p.relative_to(R);dest=B/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest);hashes[rel.as_posix()]=hashlib.sha256(p.read_bytes()).hexdigest()
for directory in ('Assets/Environment/KursHub','Sources/KursHub'):
 shutil.copytree(B/directory,A/directory,dirs_exist_ok=True)
shutil.copytree(B/'Assets/Environment/KursHub',T/'Assets/Environment/KursHub',dirs_exist_ok=True)
for p in (B/'Assets/Editor').glob('KursHub*.cs*'):shutil.copy2(p,T/'Assets/Editor'/p.name)
shutil.copy2(R/'Assets/CharacterTesting/CharacterTest.unity',T/'Assets/CharacterTesting/CharacterTest.unity')
shutil.copytree(R/'FINAL_ASSETS/REPORTS',B/'Previous_Reports')
shutil.copy2(R/'FINAL_ASSETS/KursHub_Final.unitypackage',B/'KursHub_Revision7.unitypackage')
(B/'hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf8')
for cmd,name in [(['git','status','--short'],'git_status.txt'),(['git','log','-3','--oneline'],'git_head.txt')]:
 (B/name).write_text(subprocess.check_output(cmd,cwd=R,text=True,encoding='utf8'),encoding='utf8')
print('AUDIT8_BASELINE_OK',len(hashes),'files; scene SHA256',hashes['Assets/CharacterTesting/CharacterTest.unity'])
