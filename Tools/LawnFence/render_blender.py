import bpy,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
for kind in ['Intact','Broken_Rods','Bent','Heavy_Rust','Destroyed']:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Sources/LawnFence'/('Fence_'+kind+'.blend')))
    for im in bpy.data.images:
        p=im.filepath.replace('\\','/')
        if '/Textures/' in p:im.filepath=str(ROOT/'work/LawnFence/UnityProject/Assets/Environment/Fences/Textures'/p.split('/Textures/')[-1]);im.reload()
    # Add the independently authored support for the visual review.
    with bpy.data.libraries.load(str(ROOT/'Sources/LawnFence/Fence_Post.blend')) as (src,dst):
        dst.objects=[n for n in src.objects if n=='Fence_Post_LOD0']
    for o in dst.objects:
        if o:bpy.context.collection.objects.link(o)
    for im in bpy.data.images:
        p=im.filepath.replace('\\','/')
        if '/Textures/' in p:im.filepath=str(ROOT/'work/LawnFence/UnityProject/Assets/Environment/Fences/Textures'/p.split('/Textures/')[-1]);im.reload()
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=20
    scene.cycles.use_denoising=True
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.005))
    ground=bpy.context.object;m=bpy.data.materials.new('Preview soil');m.diffuse_color=(.15,.17,.12,1);ground.data.materials.append(m)
    bpy.ops.object.light_add(type='AREA',location=(.3,-1.0,3.5));bpy.context.object.data.energy=220;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=3
    scene.world.color=(.28,.28,.28)
    bpy.ops.object.camera_add(location=(.88,-2.3,1.0));cam=bpy.context.object;cam.rotation_euler=(Vector((.9,.05,.25))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=43;scene.camera=cam
    scene.render.resolution_x=1400;scene.render.resolution_y=650;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX'
    scene.render.filepath=str(ROOT/'Verification/LawnFence'/('Blender_'+kind+'.png'));bpy.ops.render.render(write_still=True)
print('BLENDER_PREVIEWS_READY')
