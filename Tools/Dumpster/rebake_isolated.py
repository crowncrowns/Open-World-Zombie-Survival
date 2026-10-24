import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];ASSET=ROOT/'work/Dumpster/UnityProject/Assets/Environment/Props/Dumpster'
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Models/Source~/Dumpster_Master.blend'))
scene=bpy.context.scene;low=bpy.data.objects['Dumpster_Body_LOD0'];hi=bpy.data.objects['HP_Dumpster_Body_LOD0']
for ob in scene.objects:ob.hide_render=ob not in [low,hi];ob.hide_set(ob not in [low,hi])
scene.render.engine='CYCLES';scene.cycles.samples=16
for typ,suffix in [('NORMAL','BakedNormal'),('AO','BakedAO')]:
    img=bpy.data.images.new('Isolated_'+suffix,4096,4096,alpha=False);img.colorspace_settings.name='Non-Color'
    for slot in low.material_slots:
        slot.material=slot.material.copy();node=slot.material.node_tree.nodes.new('ShaderNodeTexImage');node.image=img;slot.material.node_tree.nodes.active=node
    bpy.ops.object.select_all(action='DESELECT');low.select_set(True);hi.select_set(True);bpy.context.view_layer.objects.active=low
    scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.025;scene.render.bake.max_ray_distance=.07;scene.render.bake.margin=12
    bpy.ops.object.bake(type=typ);img.filepath_raw=str(ASSET/'Textures/Body'/('Body_'+suffix+'.png'));img.file_format='PNG';img.save();print('ISOLATED_BAKE_PASS',typ,flush=True)
