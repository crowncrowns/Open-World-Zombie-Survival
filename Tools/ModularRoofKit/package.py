"""Install only the validated kit, preserving GUIDs, and verify every ZIP entry."""
from pathlib import Path
import shutil,json,hashlib,zipfile
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'work/ModularRoofKit/UnityProject/Assets/Environment/ModularRoofKit'
DST=ROOT/'Assets/Environment/ModularRoofKit'
V=ROOT/'Verification/ModularRoofKit'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for report in ['blender_reimport.json','unity_validation.json','play_validation.json']:
    assert json.loads((V/report).read_text())['passed'],report
assert len(list((SRC/'Prefabs').rglob('*.prefab')))==97
assert len(list((SRC/'Models').rglob('*.fbx')))==97
assert len(list((SRC/'Textures').rglob('*.png')))==53
assert len(list((SRC/'Materials').glob('*.mat')))==11
if not DST.exists():
    shutil.copytree(SRC,DST)
    shutil.copy2(SRC.with_suffix('.meta'),DST.with_suffix('.meta'))
else:
    for p in SRC.rglob('*'):
        if p.is_file():assert (DST/p.relative_to(SRC)).is_file() and digest(p)==digest(DST/p.relative_to(SRC)),f'Refuse to overwrite changed kit file: {p}'
delivery=ROOT/'outputs/ModularRoofKit_Unity.zip';delivery.parent.mkdir(exist_ok=True)
files=sorted(p for p in DST.rglob('*') if p.is_file())+[DST.with_suffix('.meta')]
extras=[ROOT/'Docs/ModularRoofKit.md']+sorted(p for p in (ROOT/'Tools/ModularRoofKit').iterdir() if p.is_file())+sorted(p for p in V.iterdir() if p.is_file() and p.suffix in ['.json','.md','.png'] and p.name!='package_integrity.json')
records=[]
with zipfile.ZipFile(delivery,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for p in files+extras:
        arc=str(p.relative_to(ROOT)).replace('\\','/');archive.write(p,arc)
        records.append({'file':arc,'bytes':p.stat().st_size,'sha256':digest(p)})
with zipfile.ZipFile(delivery) as archive:
    assert archive.testzip() is None
    for item in records:assert hashlib.sha256(archive.read(item['file'])).hexdigest()==item['sha256']
report={'passed':True,'files':len(records),'archive':str(delivery.relative_to(ROOT)),'bytes':delivery.stat().st_size,'sha256':digest(delivery),'models':97,'prefabs':97,'textures4K':53,'materials':11,'sourceBlend':True,'records':records}
(V/'package_integrity.json').write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='records'},indent=2))
