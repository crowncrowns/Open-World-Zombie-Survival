"""Blender hookup for the audit7 PBR set, called by source integration.

Use apply_materials(staging_root) after loading either native .blend. This module
does not open/save files or modify geometry. Existing named material datablocks
and artist settings survive. Recognized generated image links are replaced;
custom image inputs refuse an automatic overwrite.
"""
from pathlib import Path
import json
import re

MATERIALS = {
    'BlueCladding':'BluePaint','MallBlue':'BluePaint','Concrete':'CreamConcrete',
    'CreamPanel':'CreamConcrete','Floor':'FloorConcrete','Frame':'RustedSteel',
    'SteelOxide':'RustedSteel','Roof':'RoofMembrane','Glass':'GlassGrime',
}


def apply_materials(root, report_path=None):
    import bpy
    root=Path(root).resolve();tex=root/'Assets/Environment/KursHub/Textures'
    manifest=json.loads((tex/'Audit7PBRManifest.json').read_text(encoding='utf8'))
    specs={r['name']:r for r in manifest['families']}
    image_cache={}
    def image(name,suffix):
        key=(name,suffix)
        if key not in image_cache:
            p=tex/(name+'_'+suffix+'.png')
            # A previous image may contain packed old pixels; do not reuse it
            # merely because it has the same filename.
            im=bpy.data.images.load(str(p),check_existing=False)
            im.name='KursPBR7_'+name+'_'+suffix
            im.colorspace_settings.name='sRGB' if suffix=='BaseColor' else 'Non-Color'
            im['KursPBRVersion']=7
            image_cache[key]=im
        return image_cache[key]
    report={'version':7,'materials':[],'skipped':[]}
    for mat in list(bpy.data.materials):
        base=re.sub(r'\.\d{3}$','',mat.name)
        family=MATERIALS.get(base,base)
        spec=specs.get(family)
        if spec is None:continue
        if not mat.use_nodes:mat.use_nodes=True
        nodes=mat.node_tree.nodes;links=mat.node_tree.links
        bsdf=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
        if bsdf is None:
            report['skipped'].append({'name':mat.name,'reason':'Custom shader graph without Principled BSDF'});continue
        custom=[]
        # Only the original generator's known maps are safe to replace.
        for socket_name in ('Base Color','Normal','Roughness','Metallic','Alpha'):
            sock=bsdf.inputs[socket_name]
            for link in sock.links:
                todo=[link.from_node];seen=set()
                while todo:
                    node=todo.pop()
                    if node in seen:continue
                    seen.add(node)
                    if node.type=='TEX_IMAGE' and node.image:
                        filename=Path(bpy.path.abspath(node.image.filepath)).name
                        recognized=filename.startswith(family+'_') or node.image.get('KursPBRVersion')==7
                        if not recognized:custom.append(filename)
                    for input in node.inputs:
                        todo.extend(l.from_node for l in input.links)
        if custom:
            report['skipped'].append({'name':mat.name,'reason':'Artist texture input: '+', '.join(sorted(set(custom)))});continue
        # Name only our own generated nodes; never erase another artist's nodes.
        for node in list(nodes):
            if node.get('KursPBRVersion')==7:nodes.remove(node)
        def make(kind,label,location):
            node=nodes.new(kind);node.name='KursPBR7_'+label;node.label=label;node['KursPBRVersion']=7
            node.location=location;node.width=210;return node
        uv=make('ShaderNodeTexCoord','UV in metres / 2',(-1180,120))
        mapping=make('ShaderNodeVectorMath','Physical texture period',(-920,120));mapping.operation='SCALE';mapping.inputs[3].default_value=spec['tiling']
        links.new(uv.outputs['UV'],mapping.inputs[0])
        def texture(suffix):
            positions={'BaseColor':360,'Roughness':60,'Metallic':-240,'Normal':-540,'Occlusion':-840,'Height':-1140}
            n=make('ShaderNodeTexImage',family+' '+suffix,(-650,positions[suffix]));n.image=image(family,suffix)
            n.interpolation='Linear';n.extension='REPEAT';links.new(mapping.outputs[0],n.inputs['Vector']);return n
        albedo=texture('BaseColor');rough=texture('Roughness');metal=texture('Metallic');normal=texture('Normal')
        if base=='Floor':
            tint=make('ShaderNodeMixRGB','Retained floor brightness',(-370,360));tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(1.15,1.15,1.15,1)
            links.new(albedo.outputs['Color'],tint.inputs[1]);links.new(tint.outputs[0],bsdf.inputs['Base Color'])
        else:links.new(albedo.outputs['Color'],bsdf.inputs['Base Color'])
        links.new(rough.outputs['Color'],bsdf.inputs['Roughness'])
        links.new(metal.outputs['Color'],bsdf.inputs['Metallic'])
        nm=make('ShaderNodeNormalMap','Physical +Y normal',(-370,-540));nm.inputs['Strength'].default_value=spec['normalStrength']
        links.new(normal.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bsdf.inputs['Normal'])
        if family=='GlassGrime':
            alpha=make('ShaderNodeMath','Glass opacity',(-370,70));alpha.operation='MULTIPLY';alpha.inputs[1].default_value=.5
            links.new(albedo.outputs['Alpha'],alpha.inputs[0]);links.new(alpha.outputs[0],bsdf.inputs['Alpha'])
        if family=='FirNeedles':
            alpha=make('ShaderNodeMath','Fir alpha clip .4',(-370,70));alpha.operation='GREATER_THAN';alpha.inputs[1].default_value=.4
            links.new(albedo.outputs['Alpha'],alpha.inputs[0]);links.new(alpha.outputs[0],bsdf.inputs['Alpha'])
            mat.surface_render_method='DITHERED';mat['KursPBRAlphaCutoff']=.4
        # Blender has no Principled AO input. Preserve exchange maps as named
        # image nodes, unconnected so the source does not bake shadows into color.
        ao=texture('Occlusion');ao.label='Local cavity AO for Unity indirect lighting'
        height=texture('Height');height.label=f"Height {spec['heightMinMetres']:.6f}..{spec['heightMaxMetres']:.6f} m (exchange only)"
        mat['KursSurfaceVersion']=7
        mat['KursPBRHeightMinMetres']=spec['heightMinMetres'];mat['KursPBRHeightMaxMetres']=spec['heightMaxMetres']
        mat['KursPBRPeriodMetres']=spec['periodMetres']
        report['materials'].append({'name':mat.name,'family':family,'tiling':spec['tiling'],'resolution':spec['resolution']})
    # Removing zero-user image datablocks is safe after replacing only known
    # links. Keep all unrelated images even when they are currently orphaned.
    for im in list(bpy.data.images):
        if im.users==0 and im.get('KursPBRVersion')==7:bpy.data.images.remove(im)
    report['imagesAdded']=len(image_cache)
    if report_path:Path(report_path).write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    return report
