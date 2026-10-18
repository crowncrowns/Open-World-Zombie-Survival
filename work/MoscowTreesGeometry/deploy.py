from pathlib import Path
import hashlib,re,shutil,json
root=Path('E:/git/Open-World-Zombie-Survival')
work=root/'work/MoscowTreesGeometry'
test=Path('C:/Users/Alex/Documents/Codex/2026-10-18/referenced-chatgpt-conversation-this-is-an/work/unity603')
assets=root/'Assets/MoscowTrees'
def guid(p): return re.search(r'^guid: ([a-f0-9]+)',p.read_text(),re.M)[1]
report=json.loads((work/'geometry_report.json').read_text())
assert len(report)==96 and all(x['reimport']=='PASS' for x in report)
for folder,glob in [('Models_FBX','*.fbx'),('Prefabs','*.prefab')]:
    for p in (assets/folder).rglob(glob):
        rel=p.relative_to(assets)
        assert p.read_bytes()==(work/'backup'/rel).read_bytes(),'Edited during repair: '+str(rel)
        testfile=test/'Assets/MoscowTrees'/rel
        assert guid(p.with_suffix(p.suffix+'.meta'))==guid(testfile.with_suffix(testfile.suffix+'.meta')),'GUID changed: '+str(rel)
for record in report:
    p=next((work/'Models_FBX').rglob(record['model']))
    assert hashlib.sha256(p.read_bytes()).hexdigest()==record['sha256']
    shutil.copy2(p,assets/'Models_FBX'/p.relative_to(work/'Models_FBX'))
for p in (test/'Assets/MoscowTrees/Prefabs').glob('*.prefab'):
    shutil.copy2(p,assets/'Prefabs'/p.name)
for name in ['geometry_validation.txt','validation.txt','play_validation.txt']:
    shutil.copy2(test/name,work/name)
shutil.copytree(test/'geometry_previews',work/'previews',dirs_exist_ok=True)
print('Deployed 96 repaired FBX and 32 collider updates; all existing .meta/GUID preserved.')
