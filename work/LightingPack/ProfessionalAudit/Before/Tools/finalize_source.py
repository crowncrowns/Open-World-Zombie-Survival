import bpy,sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
manifest=json.loads((ASSET/'manifest.json').read_text(encoding='utf-8'))
for f in manifest['fixtures']:
    if f['shape']!='portable' or f.get('portablePivotFinalized'):continue
    collection=bpy.data.collections[f['name']]
    for o in collection.objects:
        if o.type=='MESH':
            for v in o.data.vertices:v.co.z += .055
    for light in f['lights']:light['position'][1]+=.055
    f['portablePivotFinalized']=True
    bpy.ops.object.select_all(action='DESELECT')
    objects=[o for o in collection.objects if o.name.startswith('LOD')]
    for o in objects:o.hide_set(False);o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(ASSET/'Models'/f['category']/(f['name']+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
    for o in objects:o.hide_set(True)
for image in bpy.data.images:
    if image.filepath and not image.filepath.startswith('//'):
        try:image.filepath='//../'+str(Path(image.filepath).relative_to(ASSET)).replace('\\','/')
        except ValueError:pass
bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
(ASSET/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Source~'/'Manufacturing_NormalBake.blend'))
for image in bpy.data.images:
    if image.name.startswith('Manufacturing_BakedNormal'):image.filepath='//../Textures/Shared/Manufacturing_BakedNormal.png'
bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'Source~'/'Manufacturing_NormalBake.blend'))
print('SOURCE_FINALIZED_PASS',flush=True)
