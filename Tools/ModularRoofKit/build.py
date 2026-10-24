import bpy,bmesh,math,json,random,sys
from pathlib import Path
from mathutils import Vector
from math import sin,cos,pi

ROOT=Path(__file__).resolve().parents[2]
A=ROOT/'work/ModularRoofKit/UnityProject/Assets/Environment/ModularRoofKit'
TEX=A
if '--stage' in sys.argv:A=ROOT/'work/ModularRoofKit/Rebuilt'
P=ROOT/'Verification/ModularRoofKit';P.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system='METRIC'
bpy.context.preferences.filepaths.save_version=0
materials=['M_Roof_Metal_Old','M_Roof_Metal_Rusted','M_Roof_Metal_Damaged','M_Gutter_Galvanized','M_Gutter_Painted','M_Gutter_HeavyRust','M_Fence_PaintedWhite','M_Fence_Rust','M_Fence_PeelingPaint']
mats=[]
for idx,name in enumerate(materials):
    m=bpy.data.materials.new(name);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');folder=['Roof','Gutters','Fences'][idx//3];state=[0,2,1,0,1,2,0,2,1][idx]
    for suffix,socket in [('BaseColor','Base Color'),('Metallic','Metallic'),('Roughness','Roughness')]:
        n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=bpy.data.images.load(str(TEX/'Textures'/folder/(folder+f'_{state}_'+suffix+'.png')),check_existing=True)
        if suffix!='BaseColor':n.image.colorspace_settings.name='Non-Color'
        m.node_tree.links.new(n.outputs['Color'],bs.inputs[socket])
    n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=bpy.data.images.load(str(TEX/'Textures'/folder/(folder+'_Normal.png')),check_existing=True);n.image.colorspace_settings.name='Non-Color'
    normal=m.node_tree.nodes.new('ShaderNodeNormalMap');m.node_tree.links.new(n.outputs['Color'],normal.inputs['Color']);m.node_tree.links.new(normal.outputs[0],bs.inputs['Normal']);mats.append(m)

class Mesh:
    def __init__(self):self.v=[];self.f=[];self.uv=[];self.mi=[]
    def face(self,coords,uv,mat):
        start=len(self.v);self.v.extend(coords);self.uv.extend(uv);self.f.append(tuple(range(start,start+len(coords))));self.mi.append(mat)
    def box(self,c,s,mat=0):
        x,y,z=c;a,b,d=[q/2 for q in s]
        v=[(x+i*a,y+j*b,z+k*d) for i,j,k in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
        for face in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:
            pts=[v[i] for i in face];w=(Vector(pts[1])-Vector(pts[0])).length;h=(Vector(pts[3])-Vector(pts[0])).length
            self.face(pts,[(.02,.02),(.02+w,.02),(.02+w,.02+h),(.02,.02+h)],mat)
    def sweep(self,path,r=.05,n=20,mat=3,wall=0,start=0,end=2*pi,omit=None,cap=True):
        path=[Vector(p) for p in path];rings=[];dist=[0]
        for a,b in zip(path,path[1:]):dist.append(dist[-1]+(b-a).length)
        previous=None
        for k,p in enumerate(path):
            t=(path[min(k+1,len(path)-1)]-path[max(0,k-1)]).normalized()
            # Parallel transport prevents a half-round gutter from flipping at a 90-degree turn.
            if previous is not None:u=(previous-t*previous.dot(t)).normalized()
            elif abs(end-start-pi)<.01 and abs(t.y)<.5:u=Vector((0,-1,0))
            else:
                ref=Vector((0,0,1)) if abs(t.z)<.92 else Vector((1,0,0));u=t.cross(ref).normalized()
            v=t.cross(u).normalized();previous=u
            rings.append([tuple(p+r*(cos(start+(end-start)*j/n)*u+sin(start+(end-start)*j/n)*v)) for j in range(n+1)])
        for k in range(len(path)-1):
            for j in range(n):
                if omit and omit(k,j,len(path)-1,n):continue
                v0=dist[k]/max(dist[-1],1e-8);v1=dist[k+1]/max(dist[-1],1e-8)
                self.face([rings[k][j],rings[k][j+1],rings[k+1][j+1],rings[k+1][j]],[(j/n,v0),((j+1)/n,v0),((j+1)/n,v1),(j/n,v1)],mat)
        if wall:
            # Shell is closed around all boundaries, including deliberately omitted cells.
            sub=Mesh();sub.v=self.v;sub.f=self.f;sub.uv=self.uv;sub.mi=self.mi
        if cap and not wall and abs(end-start-2*pi)<.001:
            for ring in [rings[0][::-1],rings[-1]]:self.face(ring[:-1],[(.5+.45*cos(2*pi*j/n),.5+.45*sin(2*pi*j/n)) for j in range(n)],mat)
    def obj(self,name,shell=0,bevel=0):
        me=bpy.data.meshes.new(name);me.from_pydata([(x,z,y) for x,y,z in self.v],[],self.f);me.update();ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
        used=sorted(set(self.mi))
        for idx in used:me.materials.append(mats[idx])
        uv=me.uv_layers.new(name='UV0')
        for p,mat in zip(me.polygons,self.mi):
            p.material_index=used.index(mat)
            for li in p.loop_indices:uv.data[li].uv=self.uv[me.loops[li].vertex_index]
        bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
        bpy.context.view_layer.objects.active=ob;ob.select_set(True)
        if shell:
            mod=ob.modifiers.new('Real metal wall thickness','SOLIDIFY');mod.thickness=shell;mod.offset=-1;mod.use_even_offset=True;bpy.ops.object.modifier_apply(modifier=mod.name)
        if bevel:
            mod=ob.modifiers.new('Manufactured edge radii','BEVEL');mod.width=bevel;mod.segments=2;mod.affect='EDGES';mod.angle_limit=.5;bpy.ops.object.modifier_apply(modifier=mod.name)
        # Smooth by angle without rounding creases in the profiled sheet.
        bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        for f in bm.faces:f.smooth=True
        for e in bm.edges:
            if e.is_manifold:e.smooth=e.calc_face_angle(0)<.65
        bm.to_mesh(me);bm.free();me.update();ob.select_set(False);return ob

catalog=[];allobjs=[]
def snap(name,p,d,kind,size):return dict(name=name,position=p,direction=d,kind=kind,size=size)
def add(name,cat,maker,snaps,variant='Intact',family='',length=0):
    meshes=[];counts=[]
    for lod in range(3):
        ob=maker(lod);ob.name=name+f'_LOD{lod}';ob.data.name=ob.name
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7);bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7)
        bad=[f for f in bm.faces if f.calc_area()<1e-13]
        if bad:bmesh.ops.delete(bm,geom=bad,context='FACES')
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update();ob.data.calc_loop_triangles();counts.append(len(ob.data.loop_triangles));meshes.append(ob)
    path=A/'Models'/cat/(name+'.fbx');path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in meshes:ob.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True,mesh_smooth_type='EDGE',path_mode='STRIP')
    for ob in meshes:ob.select_set(False);ob.hide_render=True;ob.hide_set(True)
    catalog.append(dict(name=name,category=cat,variant=variant,family=family or name,length=length,snaps=snaps,triangles=counts));allobjs.extend(meshes)
    print('EXPORTED',name,counts,flush=True)

def rod(m,path,r,mat,lod=0,fracture=False):
    start=len(m.v);m.sweep(path,r,[12,8,6][lod],mat)
    if fracture:
        tip=Vector(path[-1]);tangent=(tip-Vector(path[-2])).normalized()
        for i in range(start,len(m.v)):
            v=Vector(m.v[i])
            if abs((v-tip).dot(tangent))<1e-6:
                v+=tangent*(.002*sin(i*2.4)+.0015*cos(i*1.7));m.v[i]=tuple(v)
def bolt(m,p,lod=0,mat=1):
    x,y,z=p;rod(m,[(x,y,z),(x,y+.004,z)],.008,mat,lod)
    if lod==0:rod(m,[(x,y+.004,z),(x,y+.009,z)],.0048,3,lod)
def roof(length=1,kind='Old',shape='Rectangle'):
    def make(lod):
        m=Mesh();metal=1 if kind=='Rusted' else 2 if kind in ['Damaged','Bent','Destroyed','Lifted'] else 0
        # Structural deck remains visible through removed cladding, with open secondary beams.
        for x in [.018,length/2,length-.018]:m.box((x,.025,.5),(.035,.05,1),3)
        if shape=='Inner':rects=[(0,.5,0,1),(.5,1,0,.5)]
        elif shape=='Outer':rects=[(0,1,0,1)]
        else:rects=[(0,length,0,1.25 if shape=='Overhang' else 1)]
        for a,b,c,d in rects:
            m.box(((a+b)/2,.058,(c+d)/2),(b-a,.012,d-c),4)
            panels=max(1,round((b-a)/.5))
            for pan in range(panels):
                nominal=a+(b-a)*pan/panels;xa=nominal-(.008 if pan>0 else 0);xb=a+(b-a)*(pan+1)/panels
                if kind=='MissingPanel' and pan==panels//2:continue
                if kind=='Destroyed' and pan in [0,panels-1]:continue
                # Folded trapezoidal metal, with exact periodic endpoints at the grid seam.
                xs=[xa+(xb-xa)*i/max(1,round((xb-xa)/.0125)) for i in range(max(1,round((xb-xa)/.0125))+1)]
                zs=[c+(d-c)*j/[14,7,3][lod] for j in range([14,7,3][lod]+1)]
                surface=Mesh()
                def point(x,z):
                    phase=(x/.25)%1;h=.028*max(0,min(1,(phase-.30)/.10,(.70-phase)/.10)) if .30<phase<.70 else 0
                    y=.105+h
                    if pan>0:y+=.0012*max(0,1-(x-xa)/.025)
                    env=sin(pi*(x-xa)/(xb-xa))*sin(pi*(z-c)/(d-c))
                    if kind in ['Damaged','Bent','Destroyed']:y+=env*(-.015 if kind=='Damaged' else .10 if kind=='Bent' else .18)*math.exp(-((x-length*.5)/.7)**2)
                    if kind=='Lifted' and pan==panels-1:y+=.22*((x-xa)/(xb-xa))**2*sin(pi*(z-c)/(d-c))
                    return (x,y,z)
                for i in range(len(xs)-1):
                    for j in range(len(zs)-1):
                        coords=[point(xs[i],zs[j]),point(xs[i+1],zs[j]),point(xs[i+1],zs[j+1]),point(xs[i],zs[j+1])]
                        surface.face(coords,[(xs[i]*.55,zs[j]),(xs[i+1]*.55,zs[j]),(xs[i+1]*.55,zs[j+1]),(xs[i]*.55,zs[j+1])],metal)
                ob=surface.obj('sheet',shell=.0008)
                # Integrate already thickened sheet in deck mesh; preserve per-loop UVs.
                base=len(m.v)
                for p in ob.data.polygons:
                    pts=[];uvs=[]
                    for li in p.loop_indices:
                        v=ob.data.vertices[ob.data.loops[li].vertex_index].co;pts.append((v.x,v.z,v.y));uvs.append(tuple(ob.data.uv_layers[0].data[li].uv))
                    m.face(pts,uvs,metal)
                bpy.data.objects.remove(ob,do_unlink=True)
                # Folded side lap deliberately raised 1.2 mm; no coplanar duplicate faces.
                rod(m,[(xa,.108,c),(xa,.108,d)],.0014,metal,lod)
                if lod<2:
                    for x in [xa+.04,xb-.04]:
                        for z in [c+.10,d-.10]:bolt(m,(x,.108,z),lod)
        if shape=='Outer':
            m.box((length/2,.065,1.007),(length,.075,.014),metal);m.box((1.007,.065,.5),(.014,.075,1),metal)
        return m.obj('roof',bevel=.0006 if lod==0 else 0)
    return make

for n,L in [('Roof_Straight_1m',1),('Roof_Straight_2m',2),('Roof_Straight_4m',4)]:
    add(n,'Roof',roof(L),[snap('West',[0,0,0],[-1,0,0],'Roof',1),snap('East',[L,0,0],[1,0,0],'Roof',1),snap('North',[0,0,1],[0,0,1],'RoofSide',L),snap('South',[0,0,0],[0,0,-1],'RoofSide',L)],length=L)
for name,kind in [('Damaged','Damaged'),('Rusted','Rusted'),('MissingPanel','MissingPanel'),('Bent','Bent'),('Lifted','Lifted'),('Destroyed','Destroyed')]:
    add('Roof_'+name,'Roof',roof(2,kind),[snap('West',[0,0,0],[-1,0,0],'Roof',1),snap('East',[2,0,0],[1,0,0],'Roof',1),snap('North',[0,0,1],[0,0,1],'RoofSide',2),snap('South',[0,0,0],[0,0,-1],'RoofSide',2)],name,'Roof_Straight_2m',2)
for name,shape in [('OuterCorner','Outer'),('InnerCorner','Inner'),('Overhang','Overhang')]:
    add('Roof_'+name,'Roof',roof(1,'Old',shape),[snap('West',[0,0,0],[-1,0,0],'Roof',1),snap('East',[1,0,0],[1,0,0],'Roof',1)],length=1)
def flashing(kind):
    def make(lod):
        m=Mesh();m.box((.5,.09,0),(1,.055,.0012),0);m.box((.5,.118,.035),(1,.0012,.07),0);m.box((.5,.063,-.007),(1,.005,.015),1)
        if kind=='EndCap':m.box((0,.09,.03),(.0012,.06,.06),0)
        return m.obj('flashing',bevel=.0005 if lod==0 else 0)
    return make
for n in ['Edge','EndCap']:add('Roof_'+n,'Roof',flashing(n),[snap('Start',[0,0,0],[-1,0,0],'Flashing',.07),snap('End',[1,0,0],[1,0,0],'Flashing',.07)],length=1)

def shell_path(path,r,lod,mat=4,half=False,kind='',hole=False):
    m=Mesh();n=[28,16,10][lod] if half else [24,14,8][lod]
    # For tangent +X, U points -Y. Angles -pi/2..pi/2 create lower half.
    omit=(lambda k,j,K,J: (K*.36<k<K*.64 and J*.3<j<J*.7)) if hole else None
    m.sweep(path,r,n,mat,wall=.0012,start=-pi/2 if half else 0,end=pi/2 if half else 2*pi,omit=omit,cap=False)
    ob=m.obj('shell',shell=.0012)
    return ob
def combine(objects,name,bevel=0):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();ob=bpy.context.object;ob.name=name;ob.select_set(False);return ob
def boolean(ob,operand,operation):
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    mod=ob.modifiers.new('Fabricated open joint','BOOLEAN');mod.operation=operation;mod.solver='EXACT';mod.object=operand
    bpy.ops.object.modifier_apply(modifier=mod.name);ob.select_set(False);bpy.data.objects.remove(operand,do_unlink=True);return ob
def cutter(center,direction,radius,depth,lod):
    p=Vector((center[0],center[2],center[1]));v=Vector((direction[0],direction[2],direction[1])).normalized()
    bpy.ops.mesh.primitive_cylinder_add(vertices=[48,32,24][lod],radius=radius,depth=depth,location=p)
    ob=bpy.context.object;ob.rotation_euler=v.to_track_quat('Z','Y').to_euler();bpy.ops.object.transform_apply(location=False,rotation=True,scale=True);ob.select_set(False);return ob
def gutter(length=1,kind='Old',corner=0):
    def make(lod):
        steps=max(8,int(length*[18,10,5][lod]));path=[]
        if corner:
            sign=1 if corner>0 else -1
            path=[(x,0,0) for x in [0,.15,.3]]+[(.3+.2*sin(t*pi/2),0,sign*(.2-.2*cos(t*pi/2))) for t in [i/12 for i in range(1,13)]]+[(.5,0,sign*.35),(.5,0,sign*.5)]
        else:
            for i in range(steps+1):
                x=length*i/steps;t=i/steps;drop=-.16*sin(pi*t)**2 if kind=='Sagging' else -.025*sin(pi*t)**4 if kind=='Bent' else 0
                path.append((x,drop,.035*sin(pi*t)**2 if kind=='Bent' else 0))
        mat=5 if kind in ['Rusted','Corroded','Sagging'] else 4
        ob=shell_path(path,.075,lod,mat,True,hole=kind=='Corroded');m=Mesh()
        if kind=='Outlet':boolean(ob,cutter((length/2,-.08,0),(0,1,0),.051,.5,lod),'DIFFERENCE')
        # Rolled lips follow the actual swept edges (not solid cylinders inside the bowl).
        for sign in [-1,1]:
            lip=[]
            for k,p in enumerate(path):
                t=(Vector(path[min(k+1,len(path)-1)])-Vector(path[max(0,k-1)])).normalized()
                lip.append(Vector(p)+t.cross(Vector((0,-1,0)))*(.075*sign))
            rod(m,lip,.0024,mat,lod)
        if kind=='Leaves':
            rr=random.Random(77)
            for i in range([65,30,10][lod]):
                x=rr.uniform(.03,length-.03);z=rr.uniform(-.048,.048);y=-.045+rr.uniform(0,.015)
                m.face([(x-.012,y,z),(x,y+.007,z+.022),(x+.014,y,z),(x,y+.002,z-.018)],[(0,0),(1,0),(1,1),(0,1)],5)
            m.box((length/2,-.058,0),(length-.05,.015,.07),5)
        parts=[ob,m.obj('rolled_lips')]
        if kind=='Outlet':
            # Open funnel from bowl to matching 100 mm pipe throat.
            fun=Mesh();K=8;J=[28,16,10][lod]
            for k in range(K):
                for j in range(J):
                    pts=[];uv=[]
                    for kk,jj in [(k,j),(k,j+1),(k+1,j+1),(k+1,j)]:
                        t=kk/K;r=.075*(1-t)+.05*t;ang=2*pi*jj/J;pts.append((length/2+r*cos(ang),-.055-t*.14,r*sin(ang)));uv.append((jj/J,t))
                    fun.face(pts,uv,mat)
            boolean(ob,fun.obj('funnel',shell=.0012),'UNION')
        return combine(parts,'gutter')
    return make
for L in [.5,1,2,3]:
    add(f'Gutter_Straight_{L:g}m','Gutters',gutter(L),[snap('Start',[0,0,0],[-1,0,0],'Gutter',.15),snap('End',[L,0,0],[1,0,0],'Gutter',.15)],length=L)
for n,sign in [('90_Inner',-1),('90_Outer',1)]:
    add('Gutter_Corner_'+n,'Gutters',gutter(corner=sign),[snap('Start',[0,0,0],[-1,0,0],'Gutter',.15),snap('End',[.5,0,sign*.5],[0,0,sign],'Gutter',.15)])
for kind in ['Rusted','Bent','Sagging','Leaves','Corroded','Outlet']:
    snaps=[snap('Start',[0,0,0],[-1,0,0],'Gutter',.15),snap('End',[1,0,0],[1,0,0],'Gutter',.15)]
    if kind=='Outlet':snaps.append(snap('Downpipe',[.5,-.195,0],[0,-1,0],'Pipe',.1))
    add('Gutter_'+kind,'Gutters',gutter(kind=kind),snaps,kind,'Gutter_Outlet' if kind=='Outlet' else 'Gutter_Straight_1m',1)

def pipe(length=1,kind='Old',angle=0):
    def make(lod):
        if angle:
            a=math.radians(angle);R=.18;steps=[18,10,6][lod]
            path=[(0,0,0),(0,-.06,0)]+[(0,-.06-R*sin(a*i/steps),R*(1-cos(a*i/steps))) for i in range(1,steps+1)]
            end=Vector(path[-1])+Vector((0,-cos(a),sin(a)))*.06;path.append(tuple(end))
        elif kind=='Offset':path=[(0,0,0),(0,-.06,0),(0,-.18,.04),(0,-.40,.26),(0,-.52,.3),(0,-.6,.3)]
        else:path=[(0,-length*i/12,.018*sin(pi*i/12)**4 if kind=='Dented' else 0) for i in range(13)]
        mat=5 if kind in ['Rusted','Broken','MissingSegment','FailedJoint'] else 4
        if kind in ['Broken','MissingSegment']:
            parts=[shell_path(path[:5],.05,lod,mat),shell_path(path[8:],.05,lod,mat)];return combine(parts,'broken pipe')
        ob=shell_path(path,.05,lod,mat)
        if kind=='FailedJoint':
            # Split socket lip and flare its free edge; intact ends retain nominal bore.
            for v in ob.data.vertices:
                if -.07<v.co.z<-.008:
                    w=sin(pi*(-v.co.z-.008)/.062)
                    if v.co.x>0:v.co.x+=.014*w;v.co.y+=.009*w
        if kind=='Dented':
            for v in ob.data.vertices:
                # Local dent across one face, preserving both circular end sockets.
                x,z,y=v.co;w=math.exp(-((y+.45)/.13)**2)*math.exp(-((x-.045)/.035)**2);v.co.x-=.022*w
        m=Mesh()
        if lod<2:
            for yy in [-.025,-length+.025] if not angle and kind!='Offset' else [-.025]:
                m.sweep([(0,yy,0),(0,yy-.014,0)],.0517,[24,14,8][lod],5,wall=.001,start=0,end=2*pi,cap=False)
        if m.f:return combine([ob,m.obj('rolled socket',shell=.001)],'pipe')
        return ob
    return make
for L in [.5,1,2,3]:add(f'Downpipe_Straight_{L:g}m','Downpipes',pipe(L),[snap('In',[0,0,0],[0,1,0],'Pipe',.1),snap('Out',[0,-L,0],[0,-1,0],'Pipe',.1)],length=L)
for angle in [45,72,90]:
    a=math.radians(angle);p=[0,-.06-.18*sin(a)-.06*cos(a),.18*(1-cos(a))+.06*sin(a)]
    add('Downpipe_Elbow_'+str(angle),'Downpipes',pipe(angle=angle),[snap('In',[0,0,0],[0,1,0],'Pipe',.1),snap('Out',p,[0,-cos(a),sin(a)],'Pipe',.1)])
for kind in ['Offset','Dented','FailedJoint','Broken','MissingSegment','Rusted']:
    add('Downpipe_'+kind,'Downpipes',pipe(kind=kind),[snap('In',[0,0,0],[0,1,0],'Pipe',.1),snap('Out',[0,-.6,.3] if kind=='Offset' else [0,-1,0],[0,-1,0],'Pipe',.1)],kind,'Downpipe_Offset' if kind=='Offset' else 'Downpipe_Straight_1m',1 if kind!='Offset' else 0)

def fitting(kind):
    def make(lod):
        m=Mesh();parts=[];n=[24,14,8][lod]
        if kind in ['Gutter_EndCap_Left','Gutter_EndCap_Right']:
            sign=1 if kind.endswith('Right') else -1;x=sign*.001
            pts=[(x,0,0)]+[(x,-.075*cos(-pi/2+pi*j/n),.075*sin(-pi/2+pi*j/n)) for j in range(n+1)]
            m.face(pts,[(.5,.5)]+[(.5+.45*sin(-pi/2+pi*j/n),.5-.45*cos(-pi/2+pi*j/n)) for j in range(n+1)],4)
            return m.obj(kind,shell=.0012)
        if kind in ['Gutter_Connector','Gutter_Transition','Gutter_Bracket']:
            if kind=='Gutter_Bracket':
                m.sweep([(x,0,0) for x in [-.014,.014]],.078,n,5,wall=.003,start=-pi/2,end=pi/2,cap=False)
                parts.append(m.obj(kind,shell=.003));m=Mesh();m.box((0,.07,.082),(.028,.14,.006),5);m.box((0,.13,.14),(.028,.006,.12),4);bolt(m,(0,.134,.17),lod,5)
            else:
                m.sweep([(-.045,0,0),(.045,0,0)],.077,n,5,wall=.0012,start=-pi/2,end=pi/2,cap=False);return m.obj(kind,shell=.0012)
        elif kind in ['Downpipe_Connector','Downpipe_Transition']:
            path=[(0,0,0),(0,-.10,0)];ob=shell_path(path,.052,lod,5)
            if kind.endswith('Transition'):
                for v in ob.data.vertices:
                    t=max(0,min(1,-v.co.z/.1));v.co.x*=1+t*.25;v.co.y*=1+t*.25
            return ob
        elif kind=='Downpipe_Clamp':
            m.sweep([(0,-.012,0),(0,.012,0)],.054,n,5,wall=.003,cap=False);parts.append(m.obj(kind,shell=.003));m=Mesh();m.box((0,0,.10),(.035,.035,.09),5);m.box((0,0,.152),(.065,.10,.008),5);bolt(m,(0,.04,.158),lod,5)
        elif kind=='Downpipe_Tee':
            # Branch intersects a true opening in the trunk wall, both shells remain open.
            main=Mesh();main.sweep([(0,-.4*i/16,0) for i in range(17)],.05,n,4,wall=.0012,cap=False);ob=main.obj(kind,shell=.0012)
            direction=Vector((0,.04,.06)).normalized();center=Vector((0,-.2,0))+direction*.20
            boolean(ob,cutter(center,direction,.0488,.4,lod),'DIFFERENCE')
            branch=shell_path([(0,-.2,.04),(0,-.16,.10),(0,-.08,.18)],.05,lod,4);boolean(ob,branch,'UNION');return ob
        elif kind in ['Downpipe_Outlet','Downpipe_BottomDrain']:
            return pipe(angle=72 if kind.endswith('Outlet') else 90)(lod)
        elif kind=='Gutter_LeafGuard':
            for x in [i*.025 for i in range(21)]:rod(m,[(x,-.008,-.065),(x,-.008,.065)],.001,3,lod)
            for z in [-.065,-.035,0,.035,.065]:rod(m,[(0,-.008,z),(.5,-.008,z)],.001,3,lod)
        elif kind=='Fastener_Bolt':bolt(m,(0,0,0),lod,3)
        else: m.box((0,0,0),(.05,.1,.005),5);bolt(m,(0,.052,0),lod,5)
        parts.append(m.obj(kind,bevel=.0007 if lod==0 else 0));return combine(parts,kind) if len(parts)>1 else parts[0]
    return make
for name in ['Gutter_EndCap_Left','Gutter_EndCap_Right','Gutter_Connector','Gutter_Transition','Gutter_Bracket','Gutter_LeafGuard','Downpipe_Connector','Downpipe_Transition','Downpipe_Clamp','Downpipe_Tee','Downpipe_Outlet','Downpipe_BottomDrain','Fastener_Bolt','Wall_BrickAnchor']:
    cat='Gutters' if name.startswith('Gutter') else 'Downpipes';sn=[]
    if 'EndCap' in name:sn=[snap('Join',[0,0,0],[1 if name.endswith('Left') else -1,0,0],'Gutter',.15)]
    elif name.startswith('Gutter'):sn=[snap('Start',[-.045,0,0],[-1,0,0],'Gutter',.15),snap('End',[.045,0,0],[1,0,0],'Gutter',.15)]
    elif name=='Downpipe_Tee':sn=[snap('In',[0,0,0],[0,1,0],'Pipe',.1),snap('Out',[0,-.4,0],[0,-1,0],'Pipe',.1),snap('Branch',[0,-.08,.18],[0,1,1],'Pipe',.1)]
    elif name in ['Downpipe_Outlet','Downpipe_BottomDrain']:
        a=math.radians(72 if name.endswith('Outlet') else 90);sn=[snap('In',[0,0,0],[0,1,0],'Pipe',.1),snap('Out',[0,-.06-.18*sin(a)-.06*cos(a),.18*(1-cos(a))+.06*sin(a)],[0,-cos(a),sin(a)],'Pipe',.1)]
    elif name in ['Downpipe_Connector','Downpipe_Transition']:sn=[snap('In',[0,0,0],[0,1,0],'Pipe',.1),snap('Out',[0,-.1,0],[0,-1,0],'Pipe',.125 if name.endswith('Transition') else .1)]
    add(name,cat,fitting(name),sn)

variants=['IntactOld','LightRust','HeavyRust','Bent','BrokenBars','Collapsed','MissingSegment','Abandoned']
def fence(L=1,kind='IntactOld',height=.75,corner=0):
    def make(lod):
        m=Mesh();mat=7 if kind=='HeavyRust' else 8 if kind in ['BrokenBars','Collapsed','MissingSegment','Abandoned'] else 6
        def deform(p):
            x,y,z=p;w=sin(pi*max(0,min(1,x/L)))**2
            if kind=='Bent':z+=.16*w*y/height;y-=.07*w
            if kind=='Collapsed':z+=(y-.08)*.90;y=.08+(y-.08)*.30
            if corner and x>L/2:x,z=L/2,(x-L/2)*(1 if corner>0 else -1)
            return (x,y,z)
        # One start post per section; next section owns the next post. EndCap terminates a run.
        rod(m,[deform((0,0,0)),deform((0,height+.025,0))],.014,mat,lod)
        m.box((0,.006,0),(.10,.012,.10),7)
        if lod<2:
            for x in [-.034,.034]:
                for z in [-.034,.034]:bolt(m,(x,.013,z),lod,7)
        for y in [.18,height]:
            ranges=[(0,L)]
            if kind=='MissingSegment':ranges=[(0,.1),(L-.08,L)]
            if kind=='Collapsed' and y==height:ranges=[(0,L*.52),(L*.57,L)]
            for a,b in ranges:rod(m,[deform((a+(b-a)*i/12,y,0)) for i in range(13)],.009,mat,lod)
        count=max(2,round(L/.25))
        for j in range(1,count):
            x=L*j/count;hi=height;low=.18
            if kind=='MissingSegment':continue
            if kind in ['BrokenBars','Abandoned'] and j%3==1:continue
            if kind=='BrokenBars' and j%3==2:hi=.38
            pts=[deform((x+.003*sin(pi*i/8)*sin(j),low+(hi-low)*i/8,.003*sin(pi*i/8))) for i in range(9)]
            rod(m,pts,.0055 if kind!='HeavyRust' else .0049,mat,lod,fracture=hi<height)
            # Rough welded beads at joints, anchored to the same deformed construction.
            if lod<2:
                for y in [low,hi]:
                    p=Vector(deform((x,y,0)));rod(m,[p+Vector((-.007,0,0)),p+Vector((0,.002,.003)),p+Vector((.007,0,0))],.0025,7,lod)
        # Repeated simple Soviet welded loops, no unsupported modern glass/steel balustrade.
        if kind!='MissingSegment':
            for j in range(max(1,round(L/.5))):
                if kind in ['BrokenBars','Collapsed','Abandoned'] and j%2==0:continue
                cx=(j+.5)*.5
                if cx>L-.04:continue
                path=[deform((cx+.082*cos(2*pi*i/[32,16,8][lod]),.40+.13*sin(2*pi*i/[32,16,8][lod]),0)) for i in range([32,16,8][lod]+1)]
                rod(m,path,.0045,mat,lod)
        if kind=='Abandoned':
            for j in range([9,5,2][lod]):
                x=L*.15+j*.06;rod(m,[deform((x,.03,0)),deform((x+.02,.2,.02)),deform((x-.01,.35,.01))],.0015,5,lod)
                m.box(deform((x+.02,.18,.02)),(.017,.027,.002),5)
        return m.obj('fence',bevel=.0007 if lod==0 else 0)
    return make
for L in [.5,1,2]:
    for kind in variants:
        name=f'RoofFence_Straight_{L:g}m'+('' if kind=='IntactOld' else '_'+kind)
        add(name,'RoofFences',fence(L,kind),[snap('Start',[0,0,0],[-1,0,0],'Fence',.75),snap('End',[L,0,0],[1,0,0],'Fence',.75)],kind,f'RoofFence_Straight_{L:g}m',L)
for name,sign in [('Corner_90',1),('InnerCorner',-1)]:
    add('RoofFence_'+name,'RoofFences',fence(1,corner=sign),[snap('Start',[0,0,0],[-1,0,0],'Fence',.75),snap('End',[.5,0,sign*.5],[0,0,sign],'Fence',.75)])
add('RoofFence_LowSection','RoofFences',fence(1,height=.60),[snap('Start',[0,0,0],[-1,0,0],'Fence',.60),snap('End',[1,0,0],[1,0,0],'Fence',.60)],length=1)
def detail(kind):
    def make(lod):
        m=Mesh()
        if kind in ['EndCap','SupportPost','BentPost']:
            rod(m,[(0,0,0),(.03 if kind=='BentPost' else 0,.4,.05 if kind=='BentPost' else 0),(.08 if kind=='BentPost' else 0,.775,.14 if kind=='BentPost' else 0)],.014,7,lod);m.box((0,.006,0),(.10,.012,.10),7)
        elif kind=='Mount':m.box((0,.006,0),(.1,.012,.1),7);rod(m,[(0,.01,0),(0,.08,0)],.019,7,lod)
        elif kind in ['BrokenBar','TornWeld']:rod(m,[(0,0,0),(.02,.18,.04),(.03,.34,.04)],.0055 if kind=='BrokenBar' else .009,7,lod)
        elif kind in ['DetachedSection','FallenElement']:return fence(1,'Collapsed')(lod)
        else:
            for i in range(8):m.box((i*.023,.002*sin(i),.02*sin(i*3)),(.016,.001,.025),7)
        if kind in ['EndCap','SupportPost','BentPost','Mount'] and lod<2:
            for x in [-.034,.034]:
                for z in [-.034,.034]:bolt(m,(x,.013,z),lod,7)
        return m.obj(kind,bevel=.0006 if lod==0 else 0)
    return make
for k in ['EndCap','SupportPost','Mount','BrokenBar','DetachedSection','BentPost','TornWeld','FallenElement','PaintMetalDebris']:
    add('RoofFence_'+k,'RoofFences',detail(k),[snap('Join',[0,0,0],[1,0,0],'Fence',.75)] if k=='EndCap' else [])

def prop(kind):
    def make(lod):
        m=Mesh();parts=[]
        if kind=='VentBox':
            m.box((0,.13,0),(.7,.26,.45),3);m.box((0,.27,0),(.76,.025,.51),1)
            for i in range(7):m.box((-.27+i*.09,.13,-.229),(.045,.13,.008),5)
        elif kind=='VentMushroom':
            parts.append(shell_path([(0,0,0),(0,.42,0)],.10,lod,3));m.box((0,.018,0),(.4,.025,.4),1)
            rod(m,[(0,.46,0),(0,.48,0)],.22,3,lod)
            for x in [-.12,.12]:rod(m,[(x,.30,0),(x,.47,0)],.008,3,lod)
        elif kind=='BrickChimney':
            for level in range(8):
                y=.035+level*.075
                for x in [-.22,0,.22]:
                    for z in [-.25,.25]:m.box((x+(.04 if level%2 else 0),y,z),(.21,.065,.11),5)
                for z in [-.11,.11]:
                    for x in [-.29,.29]:m.box((x,y,z),(.11,.065,.21),5)
            for z in [-.31,.31]:m.box((0,.62,z),(.76,.05,.12),3)
            for x in [-.34,.34]:m.box((x,.62,0),(.08,.05,.60),3)
        elif kind=='RoofHatch':
            m.box((0,.08,0),(.82,.16,.82),3);m.box((0,.168,0),(.87,.015,.87),0)
            rod(m,[(-.1,.185,-.28),(-.1,.22,-.28),(.1,.22,-.28),(.1,.185,-.28)],.008,3,lod)
            for x in [-.28,.28]:rod(m,[(x-.04,.177,.36),(x+.04,.177,.36)],.014,5,lod)
        elif kind=='AntennaMount':
            rod(m,[(0,0,0),(0,.9,0)],.018,5,lod)
            for x,z in [(.22,.15),(-.22,.15),(0,-.22)]:rod(m,[(x,.01,z),(0,.5,0)],.008,5,lod);m.box((x,.006,z),(.10,.012,.10),5)
        elif kind=='Flashing':return flashing('Edge')(lod)
        elif kind=='DamagedSheet':
            ob=roof(1,'Bent')(lod);bm=bmesh.new();bm.from_mesh(ob.data)
            discard=[f for f in bm.faces if not ob.data.materials[f.material_index].name.startswith('M_Roof')]
            bmesh.ops.delete(bm,geom=discard,context='FACES');bm.to_mesh(ob.data);bm.free();return ob
        else:return gutter(.5,'Leaves')(lod)
        parts.append(m.obj(kind,bevel=.002 if lod==0 else 0));return combine(parts,kind) if len(parts)>1 else parts[0]
    return make
for k in ['VentBox','VentMushroom','BrickChimney','RoofHatch','AntennaMount','Flashing','LeafPile','DamagedSheet']:add('RoofProp_'+k,'RoofProps',prop(k),[])

(A/'catalog.json').write_text(json.dumps({'schema':1,'units':'metres','pipeDiameter':.1,'gutterWidth':.15,'fenceHeight':.75,'items':catalog},indent=2))
# Editable library arranged by category. LOD1/2 stay in named collections, hidden.
for i,item in enumerate(catalog):
    for lod in range(3):
        ob=bpy.data.objects[item['name']+f'_LOD{lod}'];ob.location=(i%12*4,-(i//12)*4,0);ob.hide_render=lod!=0;ob.hide_set(lod!=0)
for image in bpy.data.images:
    if image.source=='FILE':image.filepath='//../'+str(Path(image.filepath).relative_to(TEX)).replace('\\','/')
source=A/'Source~';source.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(source/'ModularRoofKit_Master.blend'))
print('ROOF_KIT_EXPORT_COMPLETE',len(catalog),flush=True)
