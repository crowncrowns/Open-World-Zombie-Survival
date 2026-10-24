import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];A=ROOT/'work/ModularRoofKit/UnityProject/Assets/Environment/ModularRoofKit'
bpy.ops.wm.open_mainfile(filepath=str(A/'Source~/ModularRoofKit_Master.blend'));bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='DESELECT');saved=[]
for lod in range(3):
    ob=bpy.data.objects['Gutter_Bracket_LOD'+str(lod)]
    if not ob.get('mount_lowered',False):
        for v in ob.data.vertices:
            if v.co.z>.015:v.co.z=.015+(v.co.z-.015)*.15
        ob['mount_lowered']=True
    saved.append((ob,ob.location.copy(),ob.hide_get(),ob.hide_render));ob.hide_set(False);ob.hide_render=False;ob.location=(0,0,0);ob.select_set(True)
bpy.context.view_layer.objects.active=saved[0][0]
bpy.ops.export_scene.fbx(filepath=str(A/'Models/Gutters/Gutter_Bracket.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True,mesh_smooth_type='EDGE',path_mode='STRIP')
for ob,loc,hide,render in saved:ob.location=loc;ob.select_set(False);ob.hide_set(hide);ob.hide_render=render
bpy.ops.wm.save_as_mainfile(filepath=str(A/'Source~/ModularRoofKit_Master.blend'))
print('GUTTER_BRACKET_MOUNT_BELOW_SHEET')
