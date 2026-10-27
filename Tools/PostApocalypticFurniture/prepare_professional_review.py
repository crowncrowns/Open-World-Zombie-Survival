"""Fork baseline art into the isolated review project with unique GUIDs.

Existing game scripts/audio remain shared. No duplicate C# classes or source
asset GUIDs are imported. This folder is never installed or exported.
"""
from pathlib import Path
import re,uuid,shutil,json
from catalog import ROOT,WORK,ASSET,VERIFY

before=WORK/'ProfessionalAudit/Before/Assets/PostApocalypticFurniture'
dest=ASSET.parent/'FurnitureAuditBefore'
assert '/work/PostApocalypticFurniture/UnityProject/Assets/' in str(dest).replace('\\','/')
files=[]
for folder in ['Models','Prefabs','Materials','Textures']:
    files.extend(p for p in (before/folder).rglob('*') if p.is_file())
shader=before/'Scripts/Runtime/FurnitureWearLit.shader'
files.extend([shader,Path(str(shader)+'.meta')])
mapping={}
for p in files:
    if p.suffix=='.meta':
        m=re.search(r'^guid: ([0-9a-f]{32})$',p.read_text(encoding='utf-8-sig'),re.M)
        if m:mapping[m[1]]=uuid.uuid5(uuid.NAMESPACE_URL,'FurnitureProfessionalBefore:'+m[1]).hex
for p in files:
    target=dest/p.relative_to(before);target.parent.mkdir(parents=True,exist_ok=True)
    if p.suffix in ['.prefab','.mat','.meta','.shader']:
        text=p.read_text(encoding='utf-8-sig')
        text=re.sub(r'(?<![0-9a-f])[0-9a-f]{32}(?![0-9a-f])',lambda m:mapping.get(m[0],m[0]),text)
        if p==shader:text=text.replace('Shader "PostApocalypticFurniture/Wear Lit"','Shader "FurnitureAuditBefore/Wear Lit"')
        target.write_text(text,encoding='utf-8')
    else:shutil.copyfile(p,target)
shutil.copyfile(before/'Documentation/catalog.json',WORK/'ProfessionalAudit/before_catalog.json')
(WORK/'ProfessionalAudit/before_guid_map.json').write_text(json.dumps(mapping,indent=2))
print('BASELINE_REVIEW_FORK_READY',len(files),'files',len(mapping),'unique GUIDs')
