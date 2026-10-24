"""Assign ceramic/organic surfaces and re-export only the affected models.
Keeps the source library transforms intact and exports every FBX about its original pivot.
"""
import bpy,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];A=ROOT/'work/ModularRoofKit/UnityProject/Assets/Environment/ModularRoofKit'
bpy.ops.wm.open_mainfile(filepath=str(A/'Source~/ModularRoofKit_Master.blend'))
bpy.context.preferences.filepaths.save_version=0
new=[]
for i,name in enumerate(['M_RoofProps_Brick','M_RoofProps_Organic']):
    mat=bpy.data.materials.get(name) or bpy.data.materials.new(name);mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF')
    for suffix,socket in [('BaseColor','Base Color'),('Metallic','Metallic'),('Roughness','Roughness')]:
        node=mat.node_tree.nodes.new('ShaderNodeTexImage');stem=f'Props_{i}_BaseColor' if suffix=='BaseColor' else 'Props_'+suffix
        node.image=bpy.data.images.load(str(A/'Textures/Props'/(stem+'.png')),check_existing=True)
        if suffix!='BaseColor':node.image.colorspace_settings.name='Non-Color'
        mat.node_tree.links.new(node.outputs['Color'],bs.inputs[socket])
    node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=bpy.data.images.load(str(A/'Textures/Props/Props_Normal.png'),check_existing=True);node.image.colorspace_settings.name='Non-Color'
    normal=mat.node_tree.nodes.new('ShaderNodeNormalMap');mat.node_tree.links.new(node.outputs[0],normal.inputs['Color']);mat.node_tree.links.new(normal.outputs[0],bs.inputs['Normal']);new.append(mat)
catalog=json.loads((A/'catalog.json').read_text())
for item in catalog['items']:
    name=item['name'];brick=name=='RoofProp_BrickChimney';organic=name in ['Gutter_Leaves','RoofProp_LeafPile'] or name.endswith('_Abandoned')
    if not(brick or organic):continue
    meshes=[bpy.data.objects[name+f'_LOD{l}'] for l in range(3)]
    for ob in meshes:
        for i,mat in enumerate(ob.data.materials):
            if mat.name=='M_Gutter_HeavyRust':ob.data.materials[i]=new[0 if brick else 1]
    bpy.ops.object.select_all(action='DESELECT');saved=[]
    for ob in meshes:
        saved.append((ob,ob.location.copy(),ob.hide_get(),ob.hide_render));ob.hide_set(False);ob.hide_render=False;ob.location=(0,0,0);ob.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.export_scene.fbx(filepath=str(A/'Models'/item['category']/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True,mesh_smooth_type='EDGE',path_mode='STRIP')
    for ob,loc,hidden,render in saved:ob.location=loc;ob.select_set(False);ob.hide_set(hidden);ob.hide_render=render
for img in bpy.data.images:
    if img.source=='FILE' and not img.filepath.startswith('//'):img.filepath='//../'+str(Path(img.filepath).relative_to(A)).replace('\\','/')
bpy.ops.wm.save_as_mainfile(filepath=str(A/'Source~/ModularRoofKit_Master.blend'))
print('PROPS_SOURCE_AND_FBX_COMPLETE')
