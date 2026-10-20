"""Append the furnished settlement to the editable source without baking props."""
from pathlib import Path
import argparse,json,runpy,sys

def finish6(root,placements,lights):
    import bpy
    palette=runpy.run_path(str(Path(__file__).with_name('stage6_palette.py')))['PALETTE6']
    for name,color in palette.items():
        mat=bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.use_nodes=True;mat.diffuse_color=color
        bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=color
        bsdf.inputs['Roughness'].default_value=.72
        texroot=root/'Assets/Environment/KursHub/Textures'
        for suffix in ('BaseColor','Normal'):
            path=texroot/(name+'_'+suffix+'.png')
            if not path.exists():continue
            node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=bpy.data.images.load(str(path),check_existing=True)
            if suffix=='Normal':
                node.image.colorspace_settings.name='Non-Color';normal=mat.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.35
                mat.node_tree.links.new(node.outputs['Color'],normal.inputs['Color']);mat.node_tree.links.new(normal.outputs[0],bsdf.inputs['Normal'])
            else:
                if name=='CeramicTile':
                    uv=mat.node_tree.nodes.new('ShaderNodeTexCoord');mapping=mat.node_tree.nodes.new('ShaderNodeVectorMath');mapping.operation='SCALE';mapping.inputs[3].default_value=1
                    mat.node_tree.links.new(uv.outputs['UV'],mapping.inputs[0]);mat.node_tree.links.new(mapping.outputs[0],node.inputs['Vector'])
                mat.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])
        if name=='Ember':bsdf.inputs['Emission Color'].default_value=(1,.29,.04,1);bsdf.inputs['Emission Strength'].default_value=3
    attach=runpy.run_path(str(Path(__file__).with_name('build_stage5_interior.py')))['attach']
    attach(root,placements,'Reusable interior settlement')
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH' and (obj.name.startswith('GroundBody') or obj.name.startswith('GroundRoundBay') or obj.name.startswith('First_')):
            obj.data.materials.clear();obj.data.materials.append(bpy.data.materials['Floor']);obj.data.materials.append(bpy.data.materials['CeramicTile'])
            for polygon in obj.data.polygons:polygon.material_index=1 if polygon.normal.z>.5 else 0
        if obj.name in ('Ground Arrival Directions','Upper Rear Directions'):obj.hide_render=True;obj.hide_viewport=True
    old_shop=bpy.data.collections.get('KH_Shopfront6')
    if old_shop:
        for obj in old_shop.objects:
            if any(slot.material and slot.material.name.split('.')[0]=='Lettering' for slot in obj.material_slots):obj.hide_render=True
    group=bpy.data.collections.get('Settlement practical lights')
    if group:
        for obj in list(group.objects):bpy.data.objects.remove(obj,do_unlink=True)
    else:group=bpy.data.collections.new('Settlement practical lights');bpy.context.scene.collection.children.link(group)
    for item in lights:
        data=bpy.data.lights.new(item['id'],'POINT');data.color=item['color'];data.energy=item['intensity']*4;data.shadow_soft_size=.35
        obj=bpy.data.objects.new(item['id'],data);group.objects.link(obj);x,y,z=item['position'];obj.location=(x,z,y)
    bpy.context.scene['KursHubStage']='6: dense market, checkpoint, housing, ceramic tiles and shared hearth'

def main():
    import bpy
    p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);root=p.parse_args(sys.argv[sys.argv.index('--')+1:]).root.resolve()
    data=runpy.run_path(str(Path(__file__).with_name('stage6_interior.py')))['layout6']()
    bpy.ops.wm.open_mainfile(filepath=str(root/'Sources/KursHub/KursHub.blend'))
    finish6(root,data['settlementInstances'],data['settlementLights']);bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(root/'Sources/KursHub/KursHub.blend'))
    path=root/'Assets/Environment/KursHub/KursHubManifest.json';manifest=json.loads(path.read_text())
    manifest.update(stage=6,**data)
    palette=runpy.run_path(str(Path(__file__).with_name('stage6_palette.py')))['PALETTE6']
    known={e['name'] for e in manifest['palette']}
    manifest['palette'] += [dict(name=n,rgba=list(c)) for n,c in palette.items() if n not in known]
    path.write_text(json.dumps(manifest,indent=2),encoding='utf8');print('KURS_STAGE6_SOURCE_OK',len(data['settlementInstances']),'linked props')

if __name__=='__main__':main()
