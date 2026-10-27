import bpy,json,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
manifest=json.loads((ASSET/'manifest.json').read_text(encoding='utf-8'));changes=[]
for f in manifest['fixtures']:
    col=bpy.data.collections[f['name']];changed=False
    for o in list(col.objects):
        if not o.name.startswith('LOD'):continue
        part=o.name.split('_',1)[1].split('.')[0];level=int(o.name[3]);me=o.data
        if len(me.polygons)<6 or sum(len(p.vertices)-2 for p in me.polygons)<6:
            candidates=[q for q in col.objects if q.name.startswith('LOD0_'+part)]
            if not candidates:continue
            src=candidates[0];o.data=src.data.copy();bpy.ops.object.select_all(action='DESELECT');o.hide_set(False);o.select_set(True);bpy.context.view_layer.objects.active=o
            mod=o.modifiers.new('Keep readable small component','DECIMATE');mod.ratio=.35 if level==1 else .22
            bpy.ops.object.modifier_apply(modifier=mod.name)
            if len(o.data.polygons)<6:o.data=src.data.copy()
            changes.append(f['name']+' '+o.name);changed=True
    if changed:
        objects=[o for o in col.objects if o.name.startswith('LOD')]
        bpy.ops.object.select_all(action='DESELECT')
        for o in objects:o.hide_set(False);o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(ASSET/'Models'/f['category']/(f['name']+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
        for o in objects:o.hide_set(True)
        f['triangles']=[sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects if o.name.startswith('LOD'+str(lod)+'_')) for lod in range(3)]
bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
(ASSET/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(VERIFY/'small_lod_repairs.json').write_text(json.dumps(changes,indent=2),encoding='utf-8')
print('SMALL_LOD_REPAIR_PASS',changes,flush=True)
