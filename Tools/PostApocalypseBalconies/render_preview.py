"""Render a Blender beauty-check gallery from the editable source library."""
from pathlib import Path
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'work/PostApocalypseBalconies/UnityProject/Assets/Environment/PostApocalypse/Balconies'
OUT=ROOT/'Verification/PostApocalypseBalconies'/'BlenderGallery.png'
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Models/Source~/Balconies_Master.blend'))
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=1920;scene.render.resolution_y=1080;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.view_settings.view_transform='AgX'
if scene.world is None:scene.world=bpy.data.worlds.new('BalconyGalleryWorld')
scene.world.color=(.07,.08,.085)

for ob in scene.objects:
    visible=ob.name.startswith('Balcony_') and ob.name.endswith('_LOD0')
    ob.hide_set(not visible);ob.hide_render=not visible

# A vertical neutral backing card reveals silhouettes and open construction.
mesh=bpy.data.meshes.new('Preview_Backdrop_Mesh');mesh.from_pydata([(-19,-.42,-2),(19,-.42,-2),(19,-.42,19),(-19,-.42,19)],[],[(0,1,2,3)]);mesh.update()
back=bpy.data.objects.new('Preview_Backdrop',mesh);scene.collection.objects.link(back)
mat=bpy.data.materials.new('Preview_Backdrop_Material');mat.diffuse_color=(.10,.12,.13,1);mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.10,.12,.13,1);mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.9;mesh.materials.append(mat)

def aim(obj,point):obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
camera_data=bpy.data.cameras.new('BalconyGalleryCamera');camera=bpy.data.objects.new('BalconyGalleryCamera',camera_data);scene.collection.objects.link(camera)
camera.location=(0,42,20);aim(camera,(0,0,8));camera_data.type='ORTHO';camera_data.ortho_scale=36;scene.camera=camera

def area(name,location,power,color,size,target=(0,0,8)):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
    obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=location;aim(obj,target)
area('Warm Key',(0,20,25),5200,(1.0,.83,.68),20)
area('Cool Fill',(-17,14,9),3400,(.62,.75,1.0),14)
area('Top Rim',(16,8,22),4600,(1.0,.93,.78),12)
scene.render.filepath=str(OUT)
bpy.ops.render.render(write_still=True)
print('BALCONY_PREVIEW_RENDERED',OUT,flush=True)
