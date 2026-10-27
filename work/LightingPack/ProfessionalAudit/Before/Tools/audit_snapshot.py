from pathlib import Path
import json,hashlib,shutil,re,datetime
ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'Assets/PostApocalypse/LightingPack'
WORK=ROOT/'work/LightingPack/ProfessionalAudit'
BACKUP=WORK/'Before';PROOF=ROOT/'Verification/LightingPack/ProfessionalAudit'
assert not BACKUP.exists(),'Never overwrite the initial audit snapshot'
BACKUP.mkdir(parents=True);PROOF.mkdir(parents=True,exist_ok=True)
hashes={};prefabs=[]
for p in ASSET.rglob('*'):
    if not p.is_file():continue
    rel=p.relative_to(ASSET);dest=BACKUP/'LightingPack'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
    hashes[rel.as_posix()]=hashlib.sha256(p.read_bytes()).hexdigest()
shutil.copy2(ASSET.with_suffix('.meta'),BACKUP/'LightingPack.meta')
shutil.copytree(ROOT/'Tools/LightingPack',BACKUP/'Tools',ignore=shutil.ignore_patterns('__pycache__'))
shutil.copy2(ROOT/'Docs/LightingPack.md',BACKUP/'LightingPack_Report.md')
for p in ASSET.rglob('*.prefab'):
    text=p.read_text(encoding='utf-8');meta=p.with_suffix('.prefab.meta').read_text()
    prefabs.append(dict(path=p.relative_to(ASSET).as_posix(),guid=re.search(r'^guid: (\w+)',meta,re.M).group(1),fileIDs=re.findall(r'^--- !u!(\d+) &(-?\d+)',text,re.M),sha256=hashes[p.relative_to(ASSET).as_posix()]))
inventory=dict(created=datetime.datetime.now().isoformat(),head='7b3b259e',scope='100 LightingPack prefabs only',backup=str(BACKUP),prefabs=prefabs,files=hashes,referenceImagesFound=0,referenceStatus='No original lamp photographs provided or present in LightingPack directories; catalogue dimensions available. Existing renders are BEFORE evidence, not references.')
(PROOF/'initial_inventory.json').write_text(json.dumps(inventory,ensure_ascii=False,indent=2),encoding='utf-8')
stage=ROOT/'work/LightingPack/UnityProject/Assets/PostApocalypse/LightingPack'
shutil.copytree(ASSET,stage,dirs_exist_ok=True);shutil.copy2(ASSET.with_suffix('.meta'),stage.with_suffix('.meta'))
print('BACKUP_AND_CURRENT_STAGE_PASS',len(hashes),len(prefabs),str(BACKUP))
