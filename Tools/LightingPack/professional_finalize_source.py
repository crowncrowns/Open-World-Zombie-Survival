import bpy,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
linked=0
for m in bpy.data.materials:
    if not m.use_nodes:continue
    parts=m.name.rsplit('_',1)
    if len(parts)!=2 or parts[1] not in ['Used','Worn','Damaged']:continue
    name,wear=parts;bs=m.node_tree.nodes.get('Principled BSDF')
    if name=='GhostPaint':
        path=ASSET/'Textures/GhostPaint/GhostPaint_BaseColor.png'
        tex=m.node_tree.nodes.get('Audit_BaseColor') or m.node_tree.nodes.new('ShaderNodeTexImage');tex.name='Audit_BaseColor';tex.image=bpy.data.images.load(str(path),check_existing=True)
        m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);m.node_tree.links.new(tex.outputs['Alpha'],bs.inputs['Alpha']);bs.inputs['Roughness'].default_value=.98;continue
    if name not in MATERIALS:continue
    folder=ASSET/'Textures'/wear/name
    tex=m.node_tree.nodes.get('Audit_NormalTexture') or m.node_tree.nodes.new('ShaderNodeTexImage');tex.name='Audit_NormalTexture';tex.image=bpy.data.images.load(str(folder/'Normal.png'),check_existing=True);tex.image.colorspace_settings.name='Non-Color'
    norm=m.node_tree.nodes.get('Audit_PhysicalNormal') or m.node_tree.nodes.new('ShaderNodeNormalMap');norm.name='Audit_PhysicalNormal';norm.inputs['Strength'].default_value=.85
    m.node_tree.links.new(tex.outputs['Color'],norm.inputs['Color']);m.node_tree.links.new(norm.outputs['Normal'],bs.inputs['Normal'])
    h=m.node_tree.nodes.get('Audit_Height_Source') or m.node_tree.nodes.new('ShaderNodeTexImage');h.name='Audit_Height_Source';h.image=bpy.data.images.load(str(folder/'Height.png'),check_existing=True);h.image.colorspace_settings.name='Non-Color';h.label='Height encoded: -20 to +120 micrometres; engine uses Normal map.'
    if name=='ClearGlass':
        texbase=next((n for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.name.startswith('BaseColor')),None)
        if texbase:m.node_tree.links.new(texbase.outputs['Alpha'],bs.inputs['Alpha'])
    linked+=1
for image in bpy.data.images:
    if image.filepath and not image.filepath.startswith('//'):
        try:image.filepath='//../'+str(Path(image.filepath).relative_to(ASSET)).replace('\\','/')
        except ValueError:pass
bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
print('SOURCE_PBR_LINKED_PASS',linked,flush=True)
