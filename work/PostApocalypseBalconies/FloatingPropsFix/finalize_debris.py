from pathlib import Path
import bpy,os
r=Path.cwd();asset=r/'work/PostApocalypseBalconies/UnityProject/Assets/Environment/PostApocalypse/Balconies';blend=asset/'Models/Source~/Balconies_Master.blend'
bpy.ops.wm.open_mainfile(filepath=str(blend))
objects=list(bpy.data.collections['ConcreteDebris'].objects);saved=[]
for ob in objects:
 saved.append((ob,ob.name,ob.location.copy(),ob.hide_render,ob.hide_get()))
 if '_LOD' in ob.name:
  for modifier in list(ob.modifiers):ob.modifiers.remove(modifier)
bpy.ops.object.select_all(action='DESELECT')
for ob,name,loc,render,hide in saved:
 ob.name='COLLISION' if name.endswith('_COLLISION') else name.rsplit('_',1)[-1]
 ob.location=(0,0,0);ob.hide_set(False);ob.hide_render=False;ob.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.fbx(filepath=str(asset/'Models/FBX/Props/ConcreteDebris.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True,use_triangles=True,mesh_smooth_type='FACE',path_mode='STRIP',use_custom_props=False)
for ob,name,loc,render,hide in saved:
 ob.name=name;ob.location=loc;ob.hide_render=render;ob.hide_set(hide);ob.select_set(False)
temp=blend.with_name('Balconies_Master_final.blend');bpy.ops.wm.save_as_mainfile(filepath=str(temp));os.replace(temp,blend)
print('DEBRIS_LOD_FIXED',flush=True)
