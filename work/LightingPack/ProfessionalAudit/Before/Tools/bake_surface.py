import bpy,math,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1
scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.035;scene.render.bake.max_ray_distance=.06
scene.render.bake.margin=16
bpy.ops.mesh.primitive_plane_add(size=1);low=bpy.context.object;low.name='LowPoly_UV_Tile'
mat=bpy.data.materials.new('NormalBakeTarget');mat.use_nodes=True;low.data.materials.append(mat)
img=bpy.data.images.new('Manufacturing_BakedNormal',width=2048,height=2048);img.colorspace_settings.name='Non-Color'
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img;mat.node_tree.nodes.active=tex
n=160;v=[];f=[]
for j in range(n+1):
    for i in range(n+1):
        x=i/n;y=j/n
        # Pressed-metal tool marks, raised seams and shallow stamping; all tile periodically.
        z=.0012*math.sin(x*math.pi*12)*math.sin(y*math.pi*8)+.0005*math.sin(x*math.pi*64)+.00025*math.cos(y*math.pi*80)
        v.append((x-.5,y-.5,z))
for j in range(n):
    for i in range(n):
        q=j*(n+1)+i;f.append((q,q+1,q+n+2,q+n+1))
mesh=bpy.data.meshes.new('HighPoly_StampedSurface');mesh.from_pydata(v,[],f);mesh.update();high=bpy.data.objects.new('HighPoly_StampedSurface',mesh);scene.collection.objects.link(high)
for p in mesh.polygons:p.use_smooth=True
bpy.ops.object.select_all(action='DESELECT');high.select_set(True);low.select_set(True);bpy.context.view_layer.objects.active=low
bpy.ops.object.bake(type='NORMAL',normal_space='TANGENT')
folder=ASSET/'Textures'/'Shared';folder.mkdir(parents=True,exist_ok=True)
img.filepath_raw=str(folder/'Manufacturing_BakedNormal.png');img.file_format='PNG';img.save()
img.filepath='//../Textures/Shared/Manufacturing_BakedNormal.png'
bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'Source~'/'Manufacturing_NormalBake.blend'))
print('NORMAL_BAKE_PASS',flush=True)
