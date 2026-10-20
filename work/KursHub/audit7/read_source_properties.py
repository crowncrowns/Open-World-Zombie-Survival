import bpy, json, pathlib
root=pathlib.Path(r'E:/git/Open-World-Zombie-Survival/work/KursHub/audit7')
path=root/'geometry/output/Sources/KursHub/KursHubProps.blend'
bpy.ops.wm.open_mainfile(filepath=str(path))
records=[]
for c in bpy.data.collections:
    if not c.name.startswith('KH_'):continue
    objects=[o for o in c.objects if o.type=='MESH']
    records.append(dict(id=c.name,woodUVObjects=[o.name for o in objects if o.get('Audit7WoodUV')],
        beveledObjects=[o.name for o in objects if o.get('Audit7BevelMetres')],
        bevelRadii={o.name:o['Audit7BevelMetres'] for o in objects if o.get('Audit7BevelMetres')}))
(root/'geometry/prop_source_properties.json').write_text(json.dumps(dict(source=str(path),props=records),indent=2),encoding='utf8')
print('SOURCE_PROPERTIES_OK',len(records),sum(len(p['woodUVObjects']) for p in records))
