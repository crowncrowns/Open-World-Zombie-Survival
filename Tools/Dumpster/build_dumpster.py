import bpy, bmesh, math, random, json
from mathutils import Vector
from pathlib import Path
from math import sin, cos, pi
random.seed(115)
ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'work/Dumpster/UnityProject/Assets/Environment/Props/Dumpster'
VERIFY=ROOT/'Verification/Dumpster';VERIFY.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
materials={}
for name,col,metal,rough in [('Body',(.075,.18,.125,1),0,.7),('Metal',(.43,.46,.46,1),.87,.39),('Wheels',(.03,.032,.034,1),0,.8),('Garbage',(.045,.05,.055,1),0,.38),('BagBlue',(.025,.045,.08,1),0,.36),('BagGrey',(.17,.18,.175,1),0,.48),('BagTranslucent',(.34,.38,.34,1),0,.3),('Stickers',(.7,.7,.6,1),0,.9)]:
    m=bpy.data.materials.new(name);m.diffuse_color=col;m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=col;bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
    folder=name if name in ['Body','Metal','Wheels'] else 'Garbage'
    if name=='Stickers':folder='Stickers';fn='Stickers_Atlas.png'
    else:fn=folder+'_BaseColor.png'
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ASSET/'Textures'/folder/fn));m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    if name=='Stickers':m.node_tree.links.new(tex.outputs['Alpha'],bs.inputs['Alpha'])
    if name in ['BagBlue','BagGrey','BagTranslucent']:
        # Tint neutral polyethylene without making a separate texture set.
        mix=m.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=tuple(v*8 for v in col[:3])+(1,)
        m.node_tree.links.new(tex.outputs['Color'],mix.inputs[1]);m.node_tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
    materials[name]=m
def active(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
def mesh(name,verts,faces,mat,bevel=0,segments=2):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new(name,me);scene.collection.objects.link(o);o.data.materials.append(materials[mat])
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free()
    active(o)
    if bevel:
        mod=o.modifiers.new('Manufacturing radii','BEVEL');mod.width=bevel;mod.segments=segments
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in me.polygons:p.use_smooth=True
    # Smooth broad panels, preserve sharp edges through weighted split normals.
    mod=o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50
    try:bpy.ops.object.modifier_apply(modifier=mod.name)
    except:pass
    if len(me.polygons):
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.018);bpy.ops.object.mode_set(mode='OBJECT')
    return o
def cube(name,loc,size,mat,bevel=.008,seg=3):
    x,y,z=loc;a,b,c=[v/2 for v in size]
    return mesh(name,[(x+i*a,y+j*b,z+k*c) for i,j,k in [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]],[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)],mat,bevel,seg)
def cyl(name,loc,r,depth,mat,n=24,axis='Z'):
    vs=[]
    for d in [-depth/2,depth/2]:
        for i in range(n):
            a=2*pi*i/n;v=[r*cos(a),r*sin(a),d]
            if axis=='X':v=[d,v[0],v[1]]
            if axis=='Y':v=[v[0],d,v[1]]
            vs.append(tuple(v[j]+loc[j] for j in range(3)))
    fs=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vs,fs,mat,.002,2)
def rr(w,d,r,n):
    pts=[]
    for cx,cy,start in [(w/2-r,d/2-r,0),(-w/2+r,d/2-r,90),(-w/2+r,-d/2+r,180),(w/2-r,-d/2+r,270)]:
        for j in range(n+1):
            a=(start+j*90/n)*pi/180;pts.append((cx+r*cos(a),cy+r*sin(a)))
    return pts
def rings(name,levels,mat,n=8,closed=True):
    vs=[]
    for w,d,r,z in levels:vs += [(x,y,z) for x,y in rr(w,d,r,n)]
    k=len(vs)//len(levels);fs=[]
    for j in range(len(levels)-1):
        for i in range(k):fs.append((j*k+i,j*k+(i+1)%k,(j+1)*k+(i+1)%k,(j+1)*k+i))
    if closed=='LOOP':
        j=len(levels)-1
        for i in range(k):fs.append((j*k+i,j*k+(i+1)%k,(i+1)%k,i))
    elif closed:fs.extend([tuple(range(k-1,-1,-1)),tuple((len(levels)-1)*k+i for i in range(k))])
    return mesh(name,vs,fs,mat)
def join(objs,name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();o=objs[0];o.name=name
    scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.008);bpy.ops.object.mode_set(mode='OBJECT')
    return o
def shell(n):
    # A continuous watertight solid: outside, rolled rim, inside, floor underside.
    return rings('Shell',[(1.14,.89,.095,.285),(1.18,.93,.10,.32),(1.29,1.04,.11,1.15),(1.30,1.05,.11,1.20),(1.274,1.024,.10,1.20),(1.264,1.014,.10,1.15),(1.154,.904,.085,.335)],'Body',n)
def make_container(lod):
    n=[10,6,3][lod];seg=[4,2,1][lod];body=[shell(n)]
    # Reinforced lower perimeter and rolled upper flange, each with a visible gap beneath.
    body.append(rings('Bottom reinforcement',[(1.17,.92,.09,.285),(1.20,.95,.10,.32),(1.18,.93,.10,.35),(1.155,.905,.09,.35)],'Body',n,'LOOP'))
    body.append(rings('Rolled rim',[(1.30,1.05,.11,1.155),(1.365,1.115,.105,1.17),(1.365,1.115,.105,1.205),(1.30,1.05,.105,1.22)],'Body',n,'LOOP'))
    # Integral moulded vertical ribs and feet, with broad, softened transitions.
    for side in [-1,1]:
        for x in [-.46,.46]:
            vs=[]
            for z in [.34,.38,1.10,1.16]:
                y=side*(.465+(z-.32)*.066)
                taper=.003 if z in [.34,1.16] else .015
                vs += [(x-.014,y,z),(x+.014,y,z),(x+.014,y+side*taper,z),(x-.014,y+side*taper,z)]
            fs=[(j*4+i,j*4+(i+1)%4,(j+1)*4+(i+1)%4,(j+1)*4+i) for j in range(3) for i in range(4)]+[(3,2,1,0),(12,13,14,15)]
            body.append(mesh('Moulded rib',vs,fs,'Body',.004,seg))
        for x in [-.50,-.33,-.16,0,.16,.33,.50]:
            body.append(mesh('Flange gusset',[(x-.008,side*.514,1.06),(x+.008,side*.514,1.06),(x-.008,side*.548,1.16),(x+.008,side*.548,1.16),(x-.008,side*.507,1.16),(x+.008,side*.507,1.16)],[(0,2,4),(1,5,3),(0,1,3,2),(2,3,5,4),(4,5,1,0)],'Body',.002,seg))
    for x in [-.565,.565]:
        for y in [-.43,.43]:body.append(cube('Corner shoe',(x,y,.33),(.09,.095,.15),'Body',.013,seg))
    # Side lifting trunnions and moulded handles.
    for side in [-1,1]:
        body += [cube('Trunnion shield',(side*.65,0,1.06),(.045,.25,.235),'Wheels',.035,seg),cyl('Lifting pin',(side*.695,0,1.06),.027,.09,'Metal',n*3,'X')]
        for y in [-.33,.33]:
            body.append(cube('Handle saddle',(side*.666,y,1.18),(.075,.12,.09),'Body',.012,seg))
            body.append(cube('Hand grip',(side*.695,y,1.22),(.06,.14,.027),'Body',.012,seg))
    body=join(body,'Dumpster_Body_LOD'+str(lod))
    # Profiled crown lid with two broad pressed plateaux and a recessed centre channel.
    lidparts=[rings('Lid shell',[(1.355,1.10,.105,1.218),(1.38,1.115,.10,1.24),(1.335,1.075,.12,1.267),(1.25,1.00,.16,1.29)],'Body',n)]
    for x in [-.32,.32]:
        o=rings('Raised lid panel',[(.59,.91,.10,1.278),(.56,.88,.13,1.335),(.48,.79,.16,1.35)],'Body',n)
        for v in o.data.vertices:v.co.x+=x
        lidparts.append(o)
    lidparts.append(cube('Front lid grip',(0,-.55,1.255),(.28,.065,.04),'Body',.013,seg))
    if lod<2:
        for x in [-.45,0,.45]:lidparts.append(cube('Under lid rib',(x,0,1.21),(.018,.92,.019),'Body',.006,seg))
    lid=join(lidparts,'Dumpster_Lid_LOD'+str(lod))
    # Store lid vertices in hinge-local coordinates, so Unity can put it on a true pivot.
    for v in lid.data.vertices:v.co-=Vector((0,.56,1.23))
    hinge=[]
    for x in [-.47,.47]:
        hinge += [cube('Hinge pad',(x,.52,1.16),(.15,.075,.11),'Body',.012,seg),cyl('Hinge barrel',(x,.56,1.23),.028,.17,'Body',n*3,'X'),cyl('Hinge pin',(x,.56,1.23),.012,.195,'Metal',n*2,'X')]
    hinge=join(hinge,'Dumpster_Hinges_LOD'+str(lod))
    wheels=[]
    for j,(x,y) in enumerate([(-.51,-.39),(.51,-.39),(-.51,.39),(.51,.39)]):
        parts=[];yaw=[-.22,.32,.15,-.4][j]
        # Caster mounting plate, ball race, bent steel side cheeks, axle and hub.
        parts += [cube('Mount plate',(0,0,.275),(.13,.11,.014),'Metal',.006,seg),cyl('Swivel race',(0,0,.248),.045,.037,'Metal',n*3)]
        for sy in [-1,1]:
            verts=[]
            poly=[(-.04,.244),(.043,.244),(.086,.13),(.075,.105),(.028,.105),(-.038,.213)]
            for yy in [sy*.037-.004,sy*.037+.004]:verts += [(xx,yy,zz) for xx,zz in poly]
            fs=[tuple(range(5,-1,-1)),tuple(range(6,12))]+[(k,(k+1)%6,(k+1)%6+6,k+6) for k in range(6)]
            parts.append(mesh('Pressed steel fork',verts,fs,'Metal',.004,seg))
        parts += [cyl('Wheel axle',(.048,0,.108),.014,.105,'Metal',n*2,'Y'),cyl('Wheel hub',(.048,0,.108),.058,.057,'Wheels',n*3,'Y')]
        # Rounded tyre profile, not a flat cylinder.
        vs=[];profile=[(-.033,.071),(-.034,.086),(-.023,.102),(0,.108),(.023,.102),(.034,.086),(.033,.071)]
        for yy,rad in profile:
            for k in range(n*5):
                a=2*pi*k/(n*5);vs.append((.048+rad*cos(a),yy,.108+rad*sin(a)))
        nk=n*5;fs=[(r*nk+k,r*nk+(k+1)%nk,(r+1)*nk+(k+1)%nk,(r+1)*nk+k) for r in range(len(profile)-1) for k in range(nk)]
        fs += [(k,(k+1)%nk,(len(profile)-1)*nk+(k+1)%nk,(len(profile)-1)*nk+k) for k in range(nk)]
        parts.append(mesh('Rubber tyre',vs,fs,'Wheels'))
        for sy in [-1,1]:
            parts.append(cyl('Axle cap',(.048,sy*.052,.108),.018,.008,'Metal',n*2,'Y'))
            if lod==0:
                for angle in range(0,360,60):
                    a=angle*pi/180;parts.append(cyl('Hub recess',(.048+.041*cos(a),sy*.031,.108+.041*sin(a)),.006,.003,'Metal',8,'Y'))
        if j<2:parts.append(cube('Brake pedal',(-.055,0,.20),(.10,.07,.012),'Metal',.006,seg))
        for p in parts:
            for v in p.data.vertices:
                xx,yy=v.co.x,v.co.y;v.co.x=x+xx*cos(yaw)-yy*sin(yaw);v.co.y=y+xx*sin(yaw)+yy*cos(yaw)
        wheels.append(join(parts,['Wheel_FL','Wheel_FR','Wheel_RL','Wheel_RR'][j]+'_LOD'+str(lod)))
    return [body,lid,hinge]+wheels

lods=[make_container(i) for i in range(3)]
# High poly source uses real rounded mould edges with additional subdivisions.
high=[]
for ob in lods[0]:
    cp=ob.copy();cp.data=ob.data.copy();scene.collection.objects.link(cp);cp.name='HP_'+ob.name;high.append(cp)
    active(cp);be=cp.modifiers.new('Fine mould bevels','BEVEL');be.width=.002;be.segments=4
    bpy.ops.object.modifier_apply(modifier=be.name)

bags=[]
for lod in range(3):
    for b in range(12):
        random.seed(600+b);nu=[56,32,16][lod];nv=[30,18,10][lod]
        width=[.22,.23,.18,.23,.22,.19,.23,.20,.21,.21,.22,.20][b];height=[.38,.42,.35,.42,.36,.38,.35,.34,.33,.34,.33,.35][b]
        vs=[];faces=[]
        for j in range(nv+1):
            t=j/nv;z=t*height
            # Cinched neck and broad asymmetrical filled lower body.
            radius=(sin(pi*(.04+.92*t))**.54)*width*(1-.68*max(0,(t-.76)/.24))
            for i in range(nu):
                a=2*pi*i/nu
                folds=(sin(a*13+b+t*6)*.028+sin(a*23-t*9)*.012)*(0.3+2.4*t**5)
                bulge=1+.08*sin(a*3+t*5+b)+.05*cos(a*5-t*3)
                r=radius*bulge+folds*width
                vs.append((r*cos(a)+.028*sin(t*4+b),r*sin(a)*(.82+.12*sin(b)),z+.008*sin(a*4)*sin(pi*t)))
        for j in range(nv):
            for i in range(nu):faces.append((j*nu+i,j*nu+(i+1)%nu,(j+1)*nu+(i+1)%nu,(j+1)*nu+i))
        faces += [tuple(range(nu-1,-1,-1)),tuple(nv*nu+i for i in range(nu))]
        mat=['Garbage','BagGrey','BagBlue','Garbage','BagTranslucent','BagBlue','BagGrey','Garbage','BagTranslucent','Garbage','BagBlue','BagGrey'][b]
        ob=mesh('Bag',vs,faces,mat)
        knot=cube('Tied neck',(.028*sin(4+b),0,height),(.055,.04,.035),mat,.014,3)
        tails=[]
        for sign in [-1,1]:
            tail=mesh('Crumpled tie tail',[(sign*.012,-.016,height),(sign*.015,.018,height),(sign*.074,.028,height+.024),(sign*.065,-.021,height+.04),(sign*.04,-.012,height+.008)],[(0,1,2,3),(0,3,4)],mat)
            sol=tail.modifiers.new('Film thickness','SOLIDIFY');sol.thickness=.001;active(tail);bpy.ops.object.modifier_apply(modifier=sol.name);tails.append(tail)
        ob=join([ob,knot]+tails,'Garbage_Bag_%02d_LOD%d'%(b+1,lod))
        # Six lower supporting bags and six independent, nested upper bags.
        positions=[(-.35,-.22,.36),(0,-.22,.36),(.34,-.19,.36),(-.33,.19,.36),(.03,.20,.36),(.34,.21,.36),(-.32,-.20,.75),(0,.20,.76),(.32,-.20,.77),(-.32,.20,.76),(0,-.20,.76),(.32,.20,.75)]
        ob.location=positions[b];ob.rotation_euler=(.04*sin(b),.06*cos(b),b*.81)
        active(ob);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        bags.append(ob)

# Curled, thin mesh decals; independent UV tiles and transforms are rebuilt in Unity.
stickers=[]
for idx in range(8):
    vs=[];fs=[];nx,ny=8,12
    for j in range(ny+1):
        for i in range(nx+1):
            u=i/nx;v=j/ny
            curl=(max(0,u-.78)**2+max(0,.12-v)**2)*.13
            vs.append(((u-.5)*.24,-curl,(v-.5)*.35))
    for j in range(ny):
        for i in range(nx):
            k=j*(nx+1)+i;fs.append((k,k+1,k+nx+2,k+nx+1))
    o=mesh('Sticker_%02d'%idx,vs,fs,'Stickers')
    uv=o.data.uv_layers.active.data
    for poly in o.data.polygons:
        for li in poly.loop_indices:
            vi=o.data.loops[li].vertex_index;u=(vi%(nx+1))/nx;v=(vi//(nx+1))/ny
            uv[li].uv=((idx%4+u)/4,(1-idx//4+v)/2)
    stickers.append(o)

def export(objects,path,anim=False):
    path.parent.mkdir(parents=True,exist_ok=True);bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,add_leaf_bones=False,bake_anim=anim,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='STRIP')
for ob in [lo[1] for lo in lods]+[o for o in high if 'Dumpster_Lid' in o.name]:ob.location=(0,.56,1.23)
export([o for lo in lods for o in lo]+stickers,ASSET/'Models/GameReady/Dumpster_GameReady.fbx')
export(high,ASSET/'Models/HighPoly/Dumpster_HighPoly.fbx')
export(bags,ASSET/'Models/GameReady/Garbage_Bags.fbx')

# Separate FBX object animation around the rear hinge; no centre-of-mesh rotation.
pivot=bpy.data.objects.new('Lid_Pivot',None);scene.collection.objects.link(pivot);pivot.location=(0,.56,1.23)
al=lods[0][1].copy();al.data=al.data.copy();scene.collection.objects.link(al);al.name='Dumpster_Lid';al.parent=pivot;al.location=(0,0,0)
scene.render.fps=30
for name,keys,end in [('Dumpster_Lid_Open',[(1,0),(9,-18),(23,-91),(31,-105)],31),('Dumpster_Lid_Close',[(1,-105),(10,-84),(21,-12),(25,0),(27,-1.2),(28,0)],28)]:
    pivot.animation_data_clear()
    for f,degrees in keys:pivot.rotation_euler.x=math.radians(degrees);pivot.keyframe_insert(data_path='rotation_euler',frame=f)
    pivot.animation_data.action.name=name;scene.frame_start=1;scene.frame_end=end
    export([pivot,al],ASSET/('Animations/'+name+'.fbx'),True)
pivot.animation_data_clear();bpy.data.objects.remove(al,do_unlink=True);bpy.data.objects.remove(pivot,do_unlink=True)

# Bake true high-to-low tangent normals and occlusion on the body UV atlas.
low=lods[0][0];hi=high[0]
for ob in scene.objects:ob.hide_render=ob not in [low,hi]
scene.render.engine='CYCLES';scene.cycles.samples=8
for typ,suffix in [('NORMAL','BakedNormal'),('AO','BakedAO')]:
    img=bpy.data.images.new('Body_'+suffix,4096,4096,alpha=False);img.colorspace_settings.name='Non-Color'
    original=list(low.data.materials)
    # Temporary bake targets prevent changing shared material image nodes.
    for slot in low.material_slots:
        slot.material=slot.material.copy();nodes=slot.material.node_tree.nodes;node=nodes.new('ShaderNodeTexImage');node.image=img;nodes.active=node
    active(low);hi.select_set(True)
    scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.025;scene.render.bake.max_ray_distance=.07;scene.render.bake.margin=12
    bpy.ops.object.bake(type=typ)
    img.filepath_raw=str(ASSET/'Textures/Body'/('Body_'+suffix+'.png'));img.file_format='PNG';img.save()
    for slot,mat in zip(low.material_slots,original):slot.material=mat
    print('BAKED',typ,flush=True)

report={'dimensionsMetres':[1.48,1.148,1.35],'hingeBlender':[0,.56,1.23],'filledMaxZ':max((o.matrix_world@v.co).z for o in bags if 'LOD0' in o.name for v in o.data.vertices),'meshes':[]}
for o in [o for lo in lods for o in lo]+bags+high:
    o.data.calc_loop_triangles();bm=bmesh.new();bm.from_mesh(o.data)
    report['meshes'].append({'name':o.name,'triangles':len(o.data.loop_triangles),'vertices':len(o.data.vertices),'uv':len(o.data.uv_layers),'boundaryEdges':sum(1 for e in bm.edges if e.is_boundary),'nonManifoldEdges':sum(1 for e in bm.edges if not e.is_manifold),'signedVolume':bm.calc_volume(signed=True)})
    bm.free()
(VERIFY/'blender_geometry.json').write_text(json.dumps(report,indent=2))

# Editable source and studio contact sheet.
for o in scene.objects:o.hide_render=True
visible=lods[0]+[o for o in bags if 'LOD0' in o.name]
for o in visible:o.hide_render=False
# Put hinge-local lid back in closed position for preview/source only.
lods[0][1].location=(0,.56,1.23)
for i,o in enumerate(stickers[:6]):
    o.location=([-.29,-.08,.28,-.36,.18,.39][i],-.511,.77+[.12,-.08,.03,-.17,.17,-.18][i]);o.rotation_euler=(math.radians(-4),0,math.radians([8,-5,4,-12,3,-8][i]));o.hide_render=False
ground=cube('Studio floor',(0,0,-.045),(200,200,.08),'Wheels',0);ground.hide_render=False
ground.data.materials.clear();gm=bpy.data.materials.new('Studio');gm.diffuse_color=(.19,.21,.22,1);ground.data.materials.append(gm)
world=scene.world or bpy.data.worlds.new('Studio');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.28,.31,.35,1);world.node_tree.nodes['Background'].inputs[1].default_value=.6
def area(name,loc,power,size):
    bpy.ops.object.light_add(type='AREA',location=loc);l=bpy.context.object;l.name=name;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(Vector((0,0,.7))-l.location).to_track_quat('-Z','Y').to_euler()
area('Key',(-3,-4,5),650,4);area('Rim',(3,2,4),850,3);area('Fill',(4,-2,2),250,3)
bpy.ops.object.camera_add(location=(2.6,-3.6,2.15));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.69))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.35;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.render.resolution_x=1200;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
src=ASSET/'Models/Source~';src.mkdir(parents=True,exist_ok=True)
for ob in scene.objects:
    ob.hide_set(ob.hide_render)
for img in bpy.data.images:
    if img.source=='FILE' and img.filepath:
        try:img.filepath='//../../'+str(Path(img.filepath).relative_to(ASSET)).replace('\\','/')
        except ValueError:pass
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(src/'Dumpster_Master.blend'))
for mode in ['Closed','Open','Rear','Side','Interior']:
    if mode=='Open':lods[0][1].rotation_euler.x=math.radians(-105)
    if mode=='Rear':cam.location=(-2.6,3.6,2.1)
    if mode=='Side':cam.location=(3.9,-.7,1.8)
    if mode=='Interior':
        cam.location=(1.7,-2.5,3.4)
        for o in bags:o.hide_render=True
    cam.rotation_euler=(Vector((0,0,.7))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(VERIFY/(mode+'.png'));bpy.ops.render.render(write_still=True)
print('DUMPSTER_BUILD_COMPLETE',flush=True)
