
import bpy,json,math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2];V=R/'Verification/TrashBin';A=R/'work/TrashBin/UnityProject/Assets/Environment/Props/TrashBin'
bpy.ops.wm.open_mainfile(filepath=str(R/'Sources/TrashBin/TrashBin_Master.blend'))
sc=bpy.context.scene;bpy.context.view_layer.update()
def chain(o):
    while o:
        yield o;o=o.parent
def has(o,name):return any(p.name.startswith(name) for p in chain(o))
clipping=[]
for o in sc.objects:
    if o.type!='MESH' or not has(o,'TrashContents'):continue
    bad=0;inv=o.matrix_world.inverted()
    for v in o.data.vertices:
        p=o.matrix_world@v.co
        if p.z<.878:
            t=max(0,min(1,(p.z-.245)/.635));hx=.163+.017*t-.004;hy=.136+.014*t-.004
            if abs(p.x)>hx or abs(p.y)>hy:
                bad+=1;p.x=max(-hx,min(hx,p.x));p.y=max(-hy,min(hy,p.y));v.co=inv@p
    if bad:clipping.append([o.name,bad])
(V/'contents_wall_clearance.json').write_text(json.dumps({'correctedVertices':clipping,'clearanceMetres':.004,'method':'Vertices below lip clamped inside tapered cavity. This is not a full triangle intersection or contact/packing proof.'},indent=2))
for image in bpy.data.images:
    candidate=A/'Textures'/image.name
    if candidate.exists():
        image.filepath=str(candidate);image.reload();image.pack()
for o in sc.objects:
    if o.type=='MESH':o.hide_render=has(o,'HIGH_POLY') or any('LOD1' in p.name or 'LOD2' in p.name for p in chain(o))
# Update exports after vertex-level clearance corrections.
for name in ['SM_TrashBin_Empty','SM_TrashBin_Filled']:
    root=bpy.data.objects[name];bpy.ops.object.select_all(action='DESELECT')
    for o in sc.objects:
        if has(o,name):o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(A/'Models'/f'{name}.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
bpy.ops.wm.save_as_mainfile(filepath=str(R/'Sources/TrashBin/TrashBin_Master.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in sc.objects:
    if has(o,'HIGH_POLY'):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(R/'Sources/TrashBin/SM_TrashBin_HighPoly.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True)
sc.render.engine='CYCLES';sc.cycles.samples=24;sc.cycles.use_denoising=True;sc.render.resolution_x=900;sc.render.resolution_y=1100;sc.render.resolution_percentage=100
sc.world.color=(.18,.18,.18)
for name,loc,power,size in [('Key',(-2,-3,4),450,3),('Fill',(3,0,3),300,2),('Rim',(0,3,3),350,2)]:
    bpy.ops.object.light_add(type='AREA',location=loc);l=bpy.context.object;l.name=name;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(Vector((0,0,.5))-l.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=200);p=bpy.context.object;p.location.z=-.003;m=bpy.data.materials.new('StudioGround');m.diffuse_color=(.18,.18,.17,1);p.data.materials.append(m)
bpy.ops.object.camera_add(location=(1.2,-1.9,1.35));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.47))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.18;sc.camera=cam
for variant in ['Empty','Filled']:
    for o in sc.objects:
        if o.type=='MESH' and (has(o,'SM_TrashBin_Empty') or has(o,'SM_TrashBin_Filled')):
            o.hide_render=not has(o,'SM_TrashBin_'+variant) or any('LOD1' in p.name or 'LOD2' in p.name for p in chain(o))
    sc.render.filepath=str(V/f'Blender_{variant}.png');bpy.ops.render.render(write_still=True)
print('SOURCE_INSPECTED',clipping,flush=True)
checks=[]
for f in sorted((A/'Models').glob('*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(f))
    obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];pts=[o.matrix_world@v.co for o in obs for v in o.data.vertices]
    checks.append({'file':f.name,'meshes':len(obs),'boundsXYZ_m':[[min(p[i] for p in pts),max(p[i] for p in pts)] for i in range(3)],'allUV':all(bool(o.data.uv_layers) for o in obs),'allMaterialSlots':all(len(o.data.materials)>0 for o in obs)})
(V/'blender_reimport.json').write_text(json.dumps(checks,indent=2));print('FINAL_REIMPORT_OK',flush=True)
