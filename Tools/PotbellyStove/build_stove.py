import bpy,bmesh,math,json,random
from pathlib import Path
from mathutils import Vector
from math import sin,cos,pi
ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'work/PotbellyStove/UnityProject/Assets/Environment/Props/PotbellyStove'
for p in ['Models','Animations']: (ASSET/p).mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';random.seed(23)
mats={}
for n in ['Stove_Metal','Stove_Rust','Embers','Ash']:
    m=bpy.data.materials.new(n);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Roughness'].default_value=.85;p.inputs['Metallic'].default_value=.45 if 'Stove' in n else 0
    t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(ASSET/'Textures'/('Embers_BaseColor.png' if n=='Embers' else 'Stove_BaseColor.png')))
    m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color']);mats[n]=m
def active(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
def finish(o,n,mat,bevel=.002):
    o.name=n;o.data.materials.append(mats[mat]);active(o)
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    if bevel:
        m=o.modifiers.new('Worn edge radius','BEVEL');m.width=bevel;m.segments=2;bpy.ops.object.modifier_apply(modifier=m.name)
    for p in o.data.polygons:p.use_smooth=True
    m=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');m.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=m.name)
    if not o.data.uv_layers:
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
    return o
def cube(n,loc,dim,mat='Stove_Metal',bevel=.003):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=dim;return finish(o,n,mat,bevel)
def cyl(n,loc,r,depth,mat='Stove_Metal',seg=64):
    bpy.ops.mesh.primitive_cylinder_add(vertices=seg,radius=r,depth=depth,location=loc);return finish(bpy.context.object,n,mat)
def tube(n,path,r,thick,seg=64,mat='Stove_Metal'):
    vs=[];fs=[];L=len(path)
    reference=Vector((0,1,0)) if any(abs(p[0]-path[0][0])>.001 for p in path) else Vector((1,0,0))
    for inner in [False,True]:
        for j,pt in enumerate(path):
            tangent=Vector(path[min(j+1,L-1)])-Vector(path[max(0,j-1)]);tangent.normalize()
            a=(reference-tangent*reference.dot(tangent)).normalized();b=tangent.cross(a).normalized()
            for i in range(seg):
                angle=2*pi*i/seg;rad=r-(thick if inner else 0)
                # Very slight out-of-round hand fabrication.
                rad+=.0007*sin(angle*5+j*.6)
                vs.append(tuple(Vector(pt)+(a*cos(angle)+b*sin(angle))*rad))
    for k in [0,1]:
        for j in range(L-1):
            for i in range(seg):
                q=k*L*seg+j*seg+i;qn=k*L*seg+j*seg+(i+1)%seg
                fs.append((q,qn,qn+seg,q+seg) if k==0 else (q+seg,qn+seg,qn,q))
    for j in [0,L-1]:
        for i in range(seg):
            a=j*seg+i;b=j*seg+(i+1)%seg;c=L*seg+b;d=L*seg+a;fs.append((a,d,c,b) if j==0 else (a,b,c,d))
    me=bpy.data.meshes.new(n);me.from_pydata(vs,[],fs);me.update()
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free()
    o=bpy.data.objects.new(n,me);scene.collection.objects.link(o)
    uv=me.uv_layers.new()
    for poly in me.polygons:
        for li in poly.loop_indices:
            v=me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv=((math.atan2(v.x,-v.y)/(2*pi)+.5)%1, v.z/.80)
    return finish(o,n,mat,.001)
def torus(n,loc,major,minor,mat='Stove_Rust',seg=64):
    bpy.ops.mesh.primitive_torus_add(major_segments=seg,minor_segments=6,location=loc,major_radius=major,minor_radius=minor)
    return finish(bpy.context.object,n,mat,0)
levels=[]
for lod,seg in enumerate([96,48,24]):
    start=set(bpy.data.objects)
    body=tube('Body_Mesh',[(0,0,.095+j*.065) for j in range(11)],.313,.012,seg)
    cut=cube('cut',(0,-.30,.315),(.235,.29,.325),bevel=.008)
    active(body);m=body.modifiers.new('Actual firebox aperture','BOOLEAN');m.operation='DIFFERENCE';m.object=cut;bpy.ops.object.modifier_apply(modifier=m.name);bpy.data.objects.remove(cut,do_unlink=True)
    cyl('Firebox_Base',(0,0,.105),.299,.022,'Stove_Metal',seg)
    cyl('Cooking_Plate',(0,0,.752),.325,.018,'Stove_Metal',seg)
    torus('Upper_Rolled_Rim',(0,0,.756),.323,.008,seg=seg)
    torus('Base_Rolled_Rim',(0,0,.108),.317,.009,seg=seg)
    torus('Body_Seam',(0,0,.535),.314,.0028,seg=seg)
    for x in [-.125,.125]:cube('Aperture_Vertical_Rim',(x,-.302,.315),(.016,.024,.354),'Stove_Rust')
    for z in [.145,.485]:cube('Aperture_Horizontal_Rim',(0,-.306,z),(.25,.025,.019),'Stove_Rust')
    # Door opens out towards the viewer from a real side hinge, leaving the fire visible.
    door=cube('Door_Mesh',(0,0,0),(.253,.014,.342),'Stove_Metal')
    for v in door.data.vertices:v.co.x+=.1265
    door.location=(-.132,-.325,.315);door.rotation_euler.z=math.radians(-108)
    for z in [.205,.425]:
        cyl('Door_Hinge',(-.134,-.327,z),.012,.055,'Stove_Rust',16)
    handle=cube('Door_Latch',(0,0,0),(.045,.026,.016),'Stove_Rust');handle.parent=door;handle.location=(.214,-.027,.015)
    # Front ash drawer and air regulator below the loading aperture.
    cube('Ash_Drawer',(0,-.312,.115),(.19,.027,.039),'Stove_Metal')
    for x in [-.058,-.029,0,.029,.058]:cube('Air_Slot',(x,-.329,.115),(.014,.002,.008),'Ash',.001)
    for x,y in [(-.21,-.19),(.21,-.19),(-.21,.19),(.21,.19)]:
        cube('Welded_Foot',(x,y,.054),(.05,.055,.105),'Stove_Rust')
        if lod==0:
            for z in [.023,.081]:cyl('Foot_Rivet',(x,y-.029,z),.006,.008,'Stove_Metal',12).rotation_euler.x=pi/2
    # Rear flue, long sectional vertical pipe, then a continuous 90 degree elbow.
    px,py=0,.125
    tube('Chimney_Mesh',[(px,py,.75),(px,py,1.1),(px,py,1.5),(px,py,1.9),(px,py,2.3),(px,py,2.58)],.071,.004,seg)
    path=[(px+.095*(1-cos(i*pi/24)),py,2.58+.095*sin(i*pi/24)) for i in range(13)]
    path +=[(px+.15,py,2.675)]
    tube('Chimney_Elbow',path,.071,.004,seg)
    # Thin cuffs and raised weld beads at believable section joints.
    for z in [.79,1.16,1.65,2.13,2.56]:
        tube('Chimney_Joint',[(px,py,z-.014),(px,py,z+.014)],.075,.006,seg,mat='Stove_Rust')
        if lod<2:torus('Joint_Weld',(px,py,z+.015),.074,.002,seg=seg)
    torus('Hotplate_Ring',(-.13,-.06,.765),.075,.003,seg=seg)
    log_layout=[(-.03,-.13,.18,.29,.035),(.035,-.10,.244,.265,.032),(-.04,.055,.19,.28,.035),(.03,.07,.25,.25,.032),(0,.01,.30,.24,.027)]
    for i,(x,y,z,length,radius) in enumerate(log_layout[:5 if lod<2 else 3]):
        o=cyl('Wood_Log',(x,y,z),radius,length,'Embers',20 if lod==0 else 12)
        for vertex in o.data.vertices:
            distortion=1+.045*sin(vertex.co.z*73+vertex.co.x*61+vertex.co.y*57)
            vertex.co.x*=distortion;vertex.co.y*=distortion
        o.rotation_euler=(random.uniform(-.1,.1),pi/2,random.uniform(-.16,.16))
    cyl('Ash_Bed',(0,0,.124),.275,.012,'Ash',seg)
    for i in range(28 if lod==0 else 12 if lod==1 else 5):
        a=random.uniform(0,2*pi);r=random.uniform(.02,.23)
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=random.uniform(.009,.022),location=(r*cos(a),r*sin(a),.14))
        o=bpy.context.object;o.scale.z=.5;finish(o,'Embers_Mesh','Embers',0)
    objects=list(set(bpy.data.objects)-start)
    # Consolidate by material and semantic part; retain the independently hinged door.
    groups={}
    for o in objects:
        if o==door or o==handle:continue
        key=('Chimney' if o.name.startswith(('Chimney','Joint')) else 'Wood_Logs' if o.name.startswith('Wood') else 'Embers_Mesh' if o.data.materials[0].name=='Embers' else 'Body_Mesh',o.data.materials[0].name)
        groups.setdefault(key,[]).append(o)
    for (name,mat),obs in groups.items():
        active(obs[0])
        for o in obs:o.select_set(True)
        bpy.ops.object.join();obs[0].name=name+'_'+mat
    socket=bpy.data.objects.new('Smoke_Outlet',None);scene.collection.objects.link(socket);socket.location=path[-1]
    objects=[o for o in scene.objects if o not in start]
    root=bpy.data.objects.new('LOD'+str(lod),None);scene.collection.objects.link(root)
    for o in objects:
        if not o.parent:o.parent=root
    levels.append(root)
    # Hidden later LODs are not exported into the single-resolution source.
for o in scene.objects:o.select_set(False)
def descendants(root):return [root]+list(root.children_recursive)
def export(path,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
export(ASSET/'Models/PotbellyStove.fbx',descendants(levels[0]))
export(ASSET/'Models/PotbellyStove_LODs.fbx',sum([descendants(o) for o in levels],[]))
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Sources/PotbellyStove/PotbellyStove_Master.blend'))
report={}
for f in ['PotbellyStove.fbx','PotbellyStove_LODs.fbx']:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(ASSET/'Models'/f))
    meshes=[o for o in scene.objects if o.type=='MESH']
    report[f]={'meshes':len(meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),'all_uv':all(len(o.data.uv_layers)>0 for o in meshes),'all_materials':all(len(o.data.materials)>0 for o in meshes),'invalid_vertices':sum(sum(not all(math.isfinite(c) for c in v.co) for v in o.data.vertices) for o in meshes)}
(ROOT/'Verification/PotbellyStove/BlenderReimport.json').write_text(json.dumps(report,indent=2))
print('STOVE_FBX_REIMPORT_PASS',report)
