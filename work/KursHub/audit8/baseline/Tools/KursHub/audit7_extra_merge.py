"""Append the reference evergreen as a separate reusable native prop."""
from pathlib import Path
import bpy,json
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit7';A=W/'authoring'
record=json.loads((W/'extras/fir_manifest.json').read_text(encoding='utf8'))
path=A/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json';manifest=json.loads(path.read_text(encoding='utf8'))
assert not any(p['id']==record['id'] for p in manifest['props']),'Already appended'
source=A/'Sources/KursHub/KursHubProps.blend';bpy.ops.wm.open_mainfile(filepath=str(source))
with bpy.data.libraries.load(str(W/'extras/KursHubAtriumFir.blend'),link=False) as (_,data):data.collections=['KH_AtriumFir']
collection=data.collections[0];bpy.context.scene.collection.children.link(collection)
for o in collection.objects:
    for slot in o.material_slots:
        if slot.material:
            base=bpy.data.materials.get(slot.material.name.split('.')[0])
            if base:slot.material=base
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(source))
manifest['props'].append(record);path.write_text(json.dumps(manifest,indent=2),encoding='utf8')
p=A/'Assets/Environment/KursHub/KursHubManifest.json';hub=json.loads(p.read_text(encoding='utf8'))
hub['settlementInstances'].append(dict(id='Atrium gallery evergreen',prop='KH_AtriumFir',position=[3.8,5.5,20.4],rotation=[0,17,0],scale=[1,1,1]))
hub['settlementLights'].append(dict(id='Gallery evergreen glow',position=[3.8,7.4,20.4],color=[1,.72,.46],intensity=6,range=5.5))
hub['qualityLandmark']='Separate modeled evergreen on rear upper gallery, from all interior references'
p.write_text(json.dumps(hub,indent=2),encoding='utf8');print('REFERENCE_EVERGREEN_MERGED',len(manifest['props']))
