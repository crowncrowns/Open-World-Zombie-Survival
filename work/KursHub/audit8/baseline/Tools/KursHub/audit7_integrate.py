"""Assemble the audited geometry, shared PBR set and stable native instances."""
from pathlib import Path
import bpy,json,sys,math
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit7';A=W/'authoring'
sys.path.insert(0,str(R/'Tools/KursHub'))
from audit7_materials import apply_materials
propsfile=A/'Sources/KursHub/KursHubProps.blend'
if '--building-only' not in sys.argv:
    bpy.ops.wm.open_mainfile(filepath=str(propsfile))
    apply_materials(A,W/'props_material_hookup.json')
    bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(propsfile))

path=A/'Sources/KursHub/KursHub.blend';bpy.ops.wm.open_mainfile(filepath=str(path))
manifest=json.loads((A/'Assets/Environment/KursHub/KursHubManifest.json').read_text(encoding='utf8'))
props=json.loads((A/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json').read_text(encoding='utf8'))
ids={p['id'] for p in props['props']}
saved={o.name:o.instance_collection.name for o in bpy.data.objects if o.instance_collection and o.instance_collection.name in ids}
for name in saved:bpy.data.objects[name].instance_collection=None
for id in ids:
    old=bpy.data.collections.get(id)
    if not old:continue
    assert not any(o.instance_collection==old for o in bpy.data.objects),'Referenced source collection '+id
    for o in list(old.objects):bpy.data.objects.remove(o,do_unlink=True)
    bpy.data.collections.remove(old)
with bpy.data.libraries.load(str(propsfile),link=False) as (available,data):
    assert ids<=set(available.collections);data.collections=sorted(ids)
library={c.name:c for c in data.collections}
for c in library.values():
    for o in c.objects:
        for slot in o.material_slots:
            if slot.material:
                base=bpy.data.materials.get(slot.material.name.split('.')[0])
                if base:slot.material=base
for name,id in saved.items():bpy.data.objects[name].instance_collection=library[id]
counts={}
for key,groupname in [('propInstances','Reusable exterior props'),('interiorInstances','Reusable interior architecture'),('settlementInstances','Reusable interior settlement')]:
    group=bpy.data.collections[groupname];prior={o.name for o in group.objects}
    placements=manifest[key];assert prior<={p['id'] for p in placements}
    for p in placements:
        o=bpy.data.objects.get(p['id'])
        if o is None:o=bpy.data.objects.new(p['id'],None);group.objects.link(o)
        assert o.name in group.objects
        o.instance_type='COLLECTION';o.instance_collection=library[p['prop']]
        x,y,z=p['position'];o.location=(x,z,y);o.rotation_euler=(0,0,-math.radians(p['rotation'][1]));x,y,z=p['scale'];o.scale=(x,z,y)
        o['UnityPrefab']='Assets/Environment/KursHub/Props/Prefabs/'+p['prop']+'.prefab'
    counts[key]=len(placements)
for p in manifest['settlementLights']:
    o=bpy.data.objects.get(p['id'])
    if o is None:
        data=bpy.data.lights.new(p['id'],'POINT');o=bpy.data.objects.new(p['id'],data);bpy.data.collections['Settlement practical lights'].objects.link(o)
    x,y,z=p['position'];o.location=(x,z,y);o.data.color=p['color'];o.data.energy=p['intensity']*4
apply_materials(A,W/'building_material_hookup.json')
bpy.context.scene['KursHubQualityRevision']='7: native reused audited props, physical PBR, recessed glazing and manufacturing detail; no NPC'
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(path))
report=dict(status='AUDIT7_SOURCE_ASSEMBLY_OK',retainedInstances=len(saved),propCollections=len(library),**counts)
(W/'source_assembly.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(report,flush=True)
