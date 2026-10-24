import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'work/Dumpster/UnityProject/Assets/Environment/Props/Dumpster'
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Models/Source~/Dumpster_Master.blend'))
scene=bpy.context.scene
for ob in scene.objects:
    if ob.type=='MESH' and 'Dumpster_Lid_LOD' in ob.name:ob.location=(0,.56,1.23)
def export(objects,path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
game=[o for o in scene.objects if o.type=='MESH' and (o.name.startswith(('Dumpster_','Wheel_','Sticker_'))) and not o.name.startswith('HP_')]
high=[o for o in scene.objects if o.name.startswith('HP_')]
export(game,ASSET/'Models/GameReady/Dumpster_GameReady.fbx');export(high,ASSET/'Models/HighPoly/Dumpster_HighPoly.fbx')
for o in list(scene.objects):
    if o.type=='MESH':
        if o.name.startswith('HP_'):name='HighPoly_Source'
        elif o.name.endswith('LOD1'):name='GameReady_LOD1'
        elif o.name.endswith('LOD2'):name='GameReady_LOD2'
        elif o.name.endswith('LOD0'):name='GameReady_LOD0'
        elif o.name.startswith('Sticker'):name='Decal_Meshes'
        else:name='Studio'
        col=bpy.data.collections.get(name)
        if not col:col=bpy.data.collections.new(name);scene.collection.children.link(col)
        if o.name not in col.objects:col.objects.link(o)
        for old in list(o.users_collection):
            if old!=col:old.objects.unlink(o)
    o.hide_set(o.hide_render)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'Models/Source~/Dumpster_Master.blend'))
print('EDITABLE_SOURCE_AND_FBX_ASSEMBLY_FINALIZED')
