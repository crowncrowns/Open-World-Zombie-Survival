import bpy,bmesh,math,ast,sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
o=bpy.data.objects['Decor_BrokenBulb'];bm=bmesh.new();bm.from_mesh(o.data)
indices={i for i,m in enumerate(o.data.materials) if m.name=='Bulb_Damaged'}
bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.material_index in indices],context='FACES');loose=[v for v in bm.verts if not v.link_faces]
if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
bm.to_mesh(o.data);bm.free()
# A large open front sector with real thickness at both broken edges. Back glass,
# the original metal screw base and exposed wire supports remain visible.
anchors=[(0,.014),(.18,.019),(.43,.0285),(.65,.0315),(.83,.025),(1,.001)]
profile=[]
for j in range(len(anchors)-1):
    for k in range(6):
        t=k/6;a,r=anchors[j];b,s=anchors[j+1];q=t*t*(3-2*t);profile.append((r+(s-r)*q,(a+(b-a)*t)*.095))
profile.append((.001,.095));profile += [(max(.0006,r-.0008),max(0,d-.0008)) for r,d in reversed(profile)]
n=48;verts=[];faces=[];missing=set(range(6,19))
for r,d in profile:
    for k in range(n):
        a=k*2*math.pi/n;verts.append((r*math.cos(a),-r*math.sin(a),-.022-d))
for j in range(len(profile)):
    z=(j+1)%len(profile)
    for k in range(n):
        if k not in missing:faces.append((j*n+k,j*n+(k+1)%n,z*n+(k+1)%n,z*n+k))
for k in [6,19]:faces.append(tuple(j*n+k for j in range(len(profile))))
me=bpy.data.meshes.new('BrokenGlassSector');me.from_pydata(verts,[],faces);me.update();me.materials.append(bpy.data.materials['ClearGlass_Damaged'])
uv=me.uv_layers.new(name='UV0')
for p in me.polygons:
    for li in p.loop_indices:
        v=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=(v.x*2+.5,v.z*2+.5)
    p.use_smooth=True
temp=bpy.data.objects.new('BrokenGlassSector',me);bpy.context.scene.collection.objects.link(temp)
bpy.ops.object.select_all(action='DESELECT');o.hide_set(False);o.select_set(True);temp.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.join()
bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000003);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
layer=o.data.uv_layers.get('LightmapUV') or o.data.uv_layers.new(name='LightmapUV');o.data.uv_layers.active=layer
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.25,island_margin=.007,area_weight=.35);bpy.ops.object.mode_set(mode='OBJECT');o.data.uv_layers.active_index=0
tri=o.modifiers.new('Broken sector triangulation','TRIANGULATE');tri.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=tri.name)
bpy.ops.object.select_all(action='DESELECT');o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(ASSET/'Models/Decor/Decor_BrokenBulb.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
o.hide_set(True);bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
print('BROKEN_BULB_OPEN_GLASS_SECTOR_PASS',flush=True)
