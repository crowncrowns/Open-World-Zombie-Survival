import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];ASSET=ROOT/'work/Dumpster/UnityProject/Assets/Environment/Props/Dumpster'
path=ASSET/'Models/Source~/Dumpster_Master.blend';bpy.ops.wm.open_mainfile(filepath=str(path))
for mat in bpy.data.materials:
    if not mat.use_nodes or mat.name in ['Stickers','Studio']:continue
    bs=mat.node_tree.nodes.get('Principled BSDF')
    if not bs:continue
    folder='Body' if mat.name.startswith('Body') else 'Metal' if mat.name.startswith('Metal') else 'Wheels' if mat.name.startswith('Wheels') else 'Garbage' if mat.name.startswith(('Garbage','Bag')) else None
    if not folder:continue
    for suffix,input_name in [('Roughness','Roughness'),('Metallic','Metallic'),('Normal','Normal')]:
        img=bpy.data.images.load(str(ASSET/'Textures'/folder/(folder+'_'+suffix+'.png')),check_existing=True);img.colorspace_settings.name='Non-Color';img.filepath='//../../Textures/'+folder+'/'+folder+'_'+suffix+'.png'
        node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=img;node.label=suffix
        if suffix=='Normal':
            normal=mat.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.6;mat.node_tree.links.new(node.outputs['Color'],normal.inputs['Color']);mat.node_tree.links.new(normal.outputs['Normal'],bs.inputs['Normal'])
        else:mat.node_tree.links.new(node.outputs['Color'],bs.inputs[input_name])
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(path));print('SOURCE_PBR_MATERIALS_READY')
