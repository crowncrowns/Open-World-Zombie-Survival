import bpy
from pathlib import Path
R=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(R/'Sources/TrashBin/TrashBin_Master.blend'))
bpy.ops.object.select_all(action='DESELECT')
def high(o):
    while o:
        if o.name.startswith('HIGH_POLY'):return True
        o=o.parent
    return False
for o in bpy.context.scene.objects:
    if high(o):o.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['HIGH_POLY_SOURCE']
bpy.ops.export_scene.fbx(filepath=str(R/'Sources/TrashBin/SM_TrashBin_HighPoly.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True)
