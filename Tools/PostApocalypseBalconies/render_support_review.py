"""Render elevated close views of every corrected balcony and its floor props."""
from pathlib import Path
import bpy, sys
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'work/PostApocalypseBalconies/UnityProject/Assets/Environment/PostApocalypse/Balconies'
OUT=ROOT/'Verification/PostApocalypseBalconies/SupportReview';OUT.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Models/Source~/Balconies_Master.blend'))
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=1800;scene.render.resolution_y=680;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
if scene.world is None:scene.world=bpy.data.worlds.new('Support review world')
scene.world.color=(.22,.22,.22)
for ob in scene.objects:ob.hide_set(True);ob.hide_render=True
def aim(ob,point):ob.rotation_euler=(Vector(point)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.2,11,7.2));camera=bpy.context.object
aim(camera,(0,.5,.45));camera.data.type='ORTHO';camera.data.ortho_scale=13.3;scene.camera=camera
for pos,power,size in [((0,5,9),3500,9),((-7,1,5),2200,7),((6,-2,7),3000,6)]:
    data=bpy.data.lights.new('Inspection light','AREA');data.energy=power;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(data.name,data);scene.collection.objects.link(ob);ob.location=pos;aim(ob,(0,.6,.5))
types=sorted({ob.name.rsplit('_',2)[0] for ob in scene.objects if ob.name.startswith('Balcony_') and ob.name.endswith('_LOD0')})
if '--only-type' in sys.argv:types=[sys.argv[sys.argv.index('--only-type')+1]]
for kind in types:
    visible=[];labels=[]
    for i,state in enumerate('ABCD'):
        ob=bpy.data.objects[kind+'_'+state+'_LOD0'];ob.hide_set(False);ob.hide_render=False
        ob.location=(i*3.4-5.1,0,0);visible.append(ob)
        data=bpy.data.curves.new('State label','FONT');data.body=kind+' / '+state;data.size=.19;data.align_x='CENTER'
        label=bpy.data.objects.new('State label',data);scene.collection.objects.link(label)
        label.location=(i*3.4-5.1,.1,-.34);label.rotation_euler=camera.rotation_euler;labels.append(label)
    scene.render.filepath=str(OUT/(kind+'.png'));bpy.ops.render.render(write_still=True)
    for ob in visible:ob.hide_render=True;ob.hide_set(True)
    for label in labels:bpy.data.objects.remove(label,do_unlink=True)
    print('SUPPORT_REVIEW_RENDER',kind,flush=True)
