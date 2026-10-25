import bpy, bmesh, math, json, random, sys, hashlib
from mathutils import Vector
from math import sin,cos,pi
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
for d in ['Models','Source~']: (ASSET/d).mkdir(parents=True,exist_ok=True)
mats={}
for wear in ['Used','Worn','Damaged']:
    for name in MATERIALS:
        mat=bpy.data.materials.new(name+'_'+wear);mat.use_nodes=True;mat.diffuse_color=(.4,.42,.38,1)
        bs=mat.node_tree.nodes.get('Principled BSDF')
        for fn,input_name in [('BaseColor','Base Color'),('Metallic','Metallic'),('Roughness','Roughness')]:
            tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ASSET/'Textures'/wear/name/(fn+'.png')),check_existing=True)
            if fn!='BaseColor':tex.image.colorspace_settings.name='Non-Color'
            mat.node_tree.links.new(tex.outputs['Color'],bs.inputs[input_name])
        mats[name+'_'+wear]=mat

class Geo:
    def __init__(self,seed,damaged=False):self.v=[];self.f=[];self.mi=[];self.parts=[];self.part='Housing';self.seed=seed;self.damaged=damaged
    def add(self,v,f,mat):
        start=len(self.v);self.v += [(p[0],-p[2],p[1]) for p in v];self.f += [tuple(start+i for i in face) for face in f];self.mi += [MATERIALS.index(mat)]*len(f)
        part='Glass' if mat in ['Glass','ClearGlass'] else ('Bulb' if mat=='Bulb' else self.part)
        self.parts += [part]*len(f)
    def box(self,p,s,mat='Steel',rot=0):
        a,b,c=[v/2 for v in s];vs=[]
        for x,y,z in [(-a,-b,-c),(-a,-b,c),(-a,b,-c),(-a,b,c),(a,-b,-c),(a,-b,c),(a,b,-c),(a,b,c)]:
            yy=y*cos(rot)-z*sin(rot);zz=y*sin(rot)+z*cos(rot);vs.append((x+p[0],yy+p[1],zz+p[2]))
        self.add(vs,[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)],mat)
    def rod(self,a,b,r,mat='Steel',n=12):
        av,bv=Vector(a),Vector(b);axis=(bv-av).normalized();u=axis.cross(Vector((0,1,0)))
        if u.length<.01:u=axis.cross(Vector((1,0,0)))
        u.normalize();w=axis.cross(u);v=[]
        for p in [av,bv]:v += [tuple(p+r*(u*cos(i*2*pi/n)+w*sin(i*2*pi/n))) for i in range(n)]
        self.add(v,[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat)
    def path(self,pts,r,mat='Steel',n=10):
        for a,b in zip(pts,pts[1:]):self.rod(a,b,r,mat,n)
    def lathe(self,center,profile,mat='Steel',n=40,axis='z',oval=1,broken=False):
        # Hollow/solid lathe defined by a closed cross-section, with real wall thickness.
        vs=[];fs=[];cx,cy,cz=center
        for radius,depth in profile:
            for i in range(n):
                a=i*2*pi/n;v=(radius*cos(a)*oval,radius*sin(a),depth)
                if axis=='y':v=(v[0],-v[2],v[1])
                vs.append((v[0]+cx,v[1]+cy,v[2]+cz))
        missing={i for i in range(n) if broken and ((i-self.seed*3)%n in [2,3,4] or (i+self.seed)%n==17)}
        for j in range(len(profile)):
            k=(j+1)%len(profile)
            for i in range(n):
                # Two missing angular sectors, varied for every design. Gaps are actual geometry.
                if i in missing:continue
                fs.append((j*n+i,j*n+(i+1)%n,k*n+(i+1)%n,k*n+i))
        if broken:
            for i in range(n):
                before=(i-1)%n in missing;after=i in missing
                if before!=after:
                    cap=tuple(j*n+i for j in range(len(profile)))
                    fs.append(cap if before else tuple(reversed(cap)))
        self.add(vs,fs,mat)
    def lens(self,c,w,h,d,mat='Glass',broken=False):
        if not broken:self.box(c,(w,h,d),mat);return
        # Four independent slabs around an irregular through fracture, with a missing corner.
        x,y,z=c;gap=.003+(self.seed%4)*.0008
        self.box((x-w*.26,y,z),(w*.47,h,d),mat)
        self.box((x+w*.26,y-h*.14,z),(w*.47,h*.70,d),mat,rot=.018)
        self.box((x+w*.18,y+h*.38,z+d*.25),(w*.32,h*.20,d),mat,rot=-.08)
    def ring(self,c,r,depth,mat='Steel',axis='z',oval=1,broken=False):
        self.lathe(c,[(r,0),(r,depth),(max(.001,r-.008),depth),(max(.001,r-.008),0)],mat,axis=axis,oval=oval,broken=broken)
    def screw(self,p,missing=False):
        if missing:return
        x,y,z=p;self.rod((x,y,z),(x,y,z+.003),.004,'Aluminium',12)
        self.box((x,y,z+.003),(.005,.0009,.0007),'Rubber')
    def cable(self,pts,exposed=False):
        self.path(pts,.006,'Rubber')
        if exposed:
            end=Vector(pts[-1]);self.rod(tuple(end),tuple(end+Vector((.016,-.03,.012))),.0017,'Copper',8)
            self.rod(tuple(end+Vector((.004,0,0))),tuple(end+Vector((.028,-.02,.016))),.0017,'Copper',8)

def model(row,wear):
    i=row['id'];d=wear=='Damaged';g=Geo(i,d);s=row['shape'];w=row['width'];h=row['height'];dep=row['depth'];mount=row['mount'];lights=[]
    # Most wall/arm fixtures use a backplate at Z=0, with mounting screws near its perimeter.
    if mount in ['wall','arm']:
        g.part='Mounting'
        bw=min(w,.24) if mount=='arm' else w*.93;bh=min(h,.24) if mount=='arm' else h*.88
        g.box((0,0,.008),(bw,bh,.016),'Steel')
        for k,(xx,yy) in enumerate([(-bw*.39,-bh*.35),(bw*.39,-bh*.35),(-bw*.39,bh*.35),(bw*.39,bh*.35)]):g.screw((xx,yy,.017),d and k==i%4)
        g.rod((0,-bh*.51,.021),(0,-bh*.66,.021),.011,'Rubber')
        g.cable([(0,-bh*.66,.022),(.008,-bh*.66-.06,.018),(.015,-bh*.66-.13,.02)],d)
        g.part='Housing'
    def light(p,kind='Spot',direction=(0,-.55,1),weight=1):lights.append(dict(position=p,type=kind,direction=direction,weight=weight))
    if s in ['oval','tablet','armored','longcage','ceilingring','round','sealed','basement']:
        ceiling=mount=='ceiling';r=h*.46 if not ceiling else w*.47;oval=w/(2*r);center=(0,0,.022)
        if ceiling:
            # Build horizontal lens beneath ceiling, axis Y. Geometry points downward.
            g.lathe((0,0,0),[(r,0),(r,.045),(r*.9,.065),(r*.82,.056),(r*.86,.012)],'Steel',axis='y')
            g.lathe((0,-.04,0),[(r*.86,0),(r*.84,.035),(r*.62,dep-.04),(.001,dep-.025),(.001,dep-.030),(r*.60,dep-.045),(r*.81,.03)],'Glass',axis='y',broken=d)
            for a in [0,pi*.5,pi,pi*1.5]:g.rod((r*.95*cos(a),-.03,r*.95*sin(a)),(r*1.01*cos(a),-.03,r*1.01*sin(a)),.004,'Aluminium')
            light((0,-dep-.018,0),direction=(0,-1,0))
        else:
            g.lathe(center,[(r,0),(r,dep*.46),(r*.88,dep*.64),(r*.81,dep*.56),(r*.84,.012)],'Steel',oval=oval)
            g.lathe((0,0,.03),[(r*.85,0),(r*.83,dep*.48),(r*.52,dep*.65),(.001,dep*.68),(.001,dep*.64),(r*.5,dep*.60),(r*.8,dep*.45)],'Glass',oval=oval,broken=d)
            # Fine moulded ribs on press-glass lenses.
            for k in range(7):
                yy=(k-3)*r*.19;xx=math.sqrt(max(.001,r*r-yy*yy))*oval*.79
                g.rod((-xx,yy,dep*.85),(xx,yy,dep*.85),.0011,'Glass',6)
            if s in ['oval','armored','longcage','sealed','basement']:
                g.ring((0,0,dep*.94),r*.91,.006,oval=oval,broken=d)
                for k in range(5 if s!='armored' else 7):
                    yy=(k-2 if s!='armored' else k-3)*r*.27;xx=math.sqrt(max(.0001,(r*.88)**2-yy*yy))*oval
                    if d and k==i%5:continue
                    g.path([(-xx,yy,dep*.95),(0,yy+(.022 if d else 0),dep*1.04+(.026 if d else 0)),(xx,yy,dep*.95)],.0033)
            if s=='sealed':
                for sign in [-1,1]:g.box((sign*w*.47,0,dep*.45),(.016,h*.64,.023),'Aluminium')
            if s=='armored':
                for a in [0,pi*.5,pi,pi*1.5]:g.screw((r*.87*cos(a),r*.87*sin(a),dep*.97))
            light((0,-h*.06,dep*1.20),direction=(0,-.55,1))
    elif s in ['wallsocket','barebulb','cage','portable']:
        ceiling=s in ['barebulb','cage'];cy=-.03 if ceiling else (.06 if s=='portable' else -.035);cz=0 if ceiling else .11
        if s=='wallsocket':g.box((0,.03,.08),(.07,.075,.13),'Plastic')
        if s=='portable':
            g.ring((0,-.04,.10),.085,.012,axis='y');g.box((0,-.02,.10),(.10,.035,.11),'Rubber')
            cy=.22;cz=.1;g.cable([(.045,-.04,.1),(.18,-.04,.14),(.24,-.04,.20),(.32,-.04,.19)],d)
        g.lathe((0,cy,cz),[(.025,0),(.025,.045),(.017,.054),(.017,.06),(.012,.06),(.012,.01)],'Plastic',axis='y')
        for k in range(5):g.ring((0,cy-.041-k*.0025,cz),.0175,.0015,'Aluminium',axis='y')
        g.lathe((0,cy-.052,cz),[(.015,0),(.020,.018),(.030,.04),(.032,.065),(.025,.084),(.001,.095),(.001,.092),(.023,.080),(.029,.063),(.027,.04),(.017,.018)],'Bulb',axis='y',broken=d)
        if s in ['cage','portable']:
            for k in range(8):
                a=k*pi/4
                if d and k==i%8:continue
                g.path([(.052*cos(a),cy-.018,cz+.052*sin(a)),(.062*cos(a),cy-.15,cz+.062*sin(a)),(.018*cos(a),cy-.18,cz+.018*sin(a))],.0028)
            for yy in [cy-.04,cy-.13]:g.ring((0,yy,cz),.064,.004,axis='y',broken=d)
        light((0,cy-.125,cz),'Point')
    elif s in ['doorplastic','visor','canopy','underpass','emergency','weatherlong']:
        ceiling=s=='canopy'
        if ceiling:
            g.box((0,-.017,0),(w,.034,h),'Steel');g.lens((0,-.052,0),(w-.025),.025,h-.025,'Glass',d)
            light((0,-.08,0),direction=(0,-1,0))
        else:
            g.box((0,0,.032),(w,h,.045),'Plastic' if s in ['doorplastic','emergency','weatherlong'] else 'Steel')
            g.lens((0,0,dep*.8),(w-.026),(h-.026),.022,'Glass',d)
            if s=='visor':g.box((0,h*.54,dep*.58),(w*1.1,.016,dep*1.2),'Steel',rot=-.22)
            if s=='underpass':
                for k in range(8):
                    if d and k==3:continue
                    xx=(k-3.5)*w/8;g.path([(xx,-h*.48,dep),(xx+.012*d,0,dep+.018*d),(xx,h*.48,dep)],.0045)
            if s=='emergency':
                g.box((0,-h*.41,dep*.96),(w*.92,.032,.018),'Plastic')
                g.rod((w*.34,-h*.41,dep),(w*.34,-h*.41,dep+.012),.004,'Bulb',10)
                g.box((-w*.29,-h*.41,dep),(.045,.012,.018),'Rubber')
            if s=='weatherlong':
                for sign in [-1,1]:g.box((sign*w*.485,0,dep*.54),(.024,h*1.04,dep),'Plastic')
                for k in range(8):g.box(((k-3.5)*w*.115,h*.5,dep*.5),(.014,.012,.035),'Aluminium')
            for sign in [-1,1]:g.screw((sign*w*.44,0,dep*.99),d and sign==1)
            for k in range(2 if w>.8 else 1):light(((k-.5)*w*.45 if w>.8 else 0,-h*.1,dep+.025),weight=.5 if w>.8 else 1)
    elif s in ['soviet','ledarm','globe','entry']:
        if s=='globe':
            g.path([(0,-.06,.02),(0,-.12,.22),(0,-.05,.32)],.018);g.lathe((0,.09,.32),[(.012,-.12),(.08,-.10),(.14,-.05),(.15,0),(.13,.08),(.07,.13),(.001,.145),(.001,.14),(.065,.124),(.124,.076),(.144,0),(.134,-.045),(.074,-.093),(.009,-.113)],'Glass',axis='y',broken=d)
            light((0,.07,.32),'Point')
        elif s=='entry':
            g.path([(0,.10,.02),(0,.15,.15),(0,.15,.25)],.014)
            g.lathe((0,.16,.25),[(.02,0),(.13,.07),(.13,.083),(.015,.014)],'Steel',axis='y')
            g.lens((0,-.01,.25),.15,.22,.13,'ClearGlass',d)
            for xx in [-.085,.085]:
                for zz in [.175,.325]:g.rod((xx,.07,zz),(xx,-.14,zz),.005)
            g.box((0,-.15,.25),(.20,.022,.19),'Steel');g.rod((0,.07,.25),(0,-.07,.25),.022,'Bulb',18)
            light((0,-.18,.25),direction=(0,-1,0))
        else:
            end=.44 if s=='soviet' else .33
            g.path([(0,0,.02),(0,.08,.17),(0,.10,end)],.023)
            if s=='soviet':
                # Deep spoon housing with rolled lip and a recessed refractor.
                g.lathe((0,.1,end),[(.03,0),(.13,.02),(.16,.08),(.155,.11),(.147,.11),(.15,.08),(.12,.03),(.026,.01)],'Aluminium',axis='y',oval=1.0)
                g.lathe((0,-.002,end),[(.145,0),(.11,.055),(.001,.08),(.001,.073),(.105,.048),(.14,0)],'Glass',axis='y',oval=1,broken=d)
                for k in range(7):g.rod((-.105+k*.035,-.025,end-.085),(-.105+k*.035,-.025,end+.085),.0017,'Glass',6)
                light((0,-.12,end),direction=(0,-1,.18))
            else:
                g.box((0,.09,end),(w,.045,.32),'Steel')
                for k in range(9):g.box(((k-4)*w*.09,.125,end),(.012,.034,.27),'Aluminium')
                g.lens((0,.056,end),w*.86,.012,.25,'Glass',d)
                for xx in [-.09,0,.09]:
                    for zz in [-.085,0,.085]:g.box((xx,.045,end+zz),(.035,.005,.035),'Bulb')
                light((0,.015,end),direction=(0,-1,.08))
    elif s in ['flood','adjustable','oldflood']:
        cy=.02;cz=.14
        for sign in [-1,1]:
            g.box((sign*w*.52,-h*.05,.11),(.022,h*.85,.16),'Steel')
            g.rod((sign*w*.46,0,.13),(sign*w*.59,0,.13),.018,'Aluminium',16)
        g.box((0,-h*.45,.10),(w*1.10,.022,.16),'Steel')
        # Open reflector; closed shell never lies across the light aperture.
        g.box((0,cy,cz-.07),(w*.88,h*.88,.024),'Steel')
        g.box((0,cy+h*.45,cz),(w,.025,.15),'Steel');g.box((0,cy-h*.45,cz),(w,.025,.15),'Steel')
        for sign in [-1,1]:g.box((sign*w*.46,cy,cz),(.024,h*.87,.15),'Steel')
        g.box((0,cy,cz-.045),(w*.76,h*.72,.005),'Aluminium')
        for sign in [-1,1]:
            g.box((sign*w*.36,cy,cz-.015),(.02,h*.72,.09),'Aluminium')
        g.rod((-w*.27,cy,cz-.015),(w*.27,cy,cz-.015),.013,'Bulb',20)
        g.lens((0,cy,cz+.075),w*.86,h*.81,.012 if s=='oldflood' else .006,'ClearGlass',d)
        for xx in [-w*.43,w*.43]:
            for yy in [-h*.40,h*.40]:g.screw((xx,yy+cy,cz+.08),d and xx>0 and yy>0)
        if s=='oldflood':
            for k in range(7):g.box(((k-3)*w*.11,cy,cz-.089),(.012,h*.70,.019),'Steel')
            g.box((0,cy-h*.57,cz-.04),(.11,.08,.09),'Steel')
        if s=='adjustable':g.ring((w*.59,0,.13),.038,.014,'Steel',axis='z')
        light((0,cy,cz+.10),direction=(0,-.35,1))
    elif s in ['twintube','singletube','office','panel']:
        length=w;breadth=h
        g.box((0,-.012,0),(length,.024,breadth),'Steel' if s!='panel' else 'Aluminium')
        if s in ['twintube','singletube']:
            num=2 if s=='twintube' else 1
            for k in range(num):
                zz=(k-(num-1)/2)*breadth*.62
                for sign in [-1,1]:g.box((sign*(length*.46),-.045,zz),(.035,.06,.048),'Plastic')
                if d:
                    g.rod((-length*.43,-.06,zz),(-length*.12,-.06,zz),.013,'Bulb',20)
                    g.rod((length*.06,-.075,zz),(length*.43,-.06,zz),.013,'Bulb',20)
                else:g.rod((-length*.43,-.06,zz),(length*.43,-.06,zz),.013,'Bulb',24)
                for sign in [-1,1]:g.rod((sign*length*.435,-.06,zz),(sign*length*.45,-.06,zz),.014,'Aluminium',16)
                for xx in [-length*.25,length*.25]:light((xx,-.085,zz),direction=(0,-1,0),weight=1/(num*2))
            for sign in [-1,1]:g.box((0,-.025,sign*breadth*.48),(length,.035,.008),'Aluminium')
        elif s=='office':
            for k in range(4):
                zz=(k-1.5)*.13;g.rod((-.255,-.04,zz),(.255,-.04,zz),.011,'Bulb',20)
            for k in range(8):
                if d and k==i%8:continue
                g.box(((k-3.5)*.07,-.065,0),(.006,.075,.57),'Aluminium',rot=.08 if d else 0)
            for sign in [-1,1]:g.box((0,-.05,sign*.292),(.60,.08,.012),'Steel')
            light((0,-.11,0),direction=(0,-1,0))
        else:
            g.lens((0,-.027,0),.565,.012,.565,'Glass',d)
            light((0,-.05,0),direction=(0,-1,0))
    elif s=='pendant':
        g.box((0,-.012,0),(.09,.024,.09),'Steel');g.rod((0,-.025,0),(0,-.34,0),.0035,'Steel',12)
        g.cable([(.012,-.02,0),(.017,-.15,.012),(0,-.34,0)],d)
        g.lathe((0,-.32,0),[(.033,0),(.038,.065),(.20,.20),(.20,.21),(.192,.21),(.031,.069),(.026,.01)],'Steel',axis='y',broken=d)
        g.lathe((0,-.39,0),[(.034,0),(.189,.13),(.184,.135),(.029,.006)],'Aluminium',axis='y',broken=d)
        g.lathe((0,-.40,0),[(.018,0),(.031,.035),(.03,.065),(.001,.085),(.001,.08),(.025,.06),(.026,.035),(.014,0)],'Bulb',axis='y',broken=d)
        light((0,-.55,0),direction=(0,-1,0))
    else:raise RuntimeError(s)
    if d:
        # Local dent on opaque housing: bounded deformation away from mounting plane.
        for k,p in enumerate(g.v):
            # Blender coordinates correspond to X, -Z, Y.
            xx,yy,zz=p
            dist=((xx-w*.29)**2+(zz-h*.19)**2)**.5
            if dist<w*.18 and yy<-.025:
                a=(1-dist/(w*.18))*.008;g.v[k]=(xx+a*.3,yy+a,zz-a*.4)
    rowdata=dict(row);rowdata['lights']=lights
    return g,rowdata

def object_from_geo(g,name,wear):
    me=bpy.data.meshes.new(name);me.from_pydata(g.v,[],g.f);me.update()
    for mat in MATERIALS:me.materials.append(mats[mat+'_'+wear])
    for p,mi in zip(me.polygons,g.mi):p.material_index=mi
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free()
    # UV per face in real-world units; common materials tile at 0.5m.
    uv=me.uv_layers.new(name='UV0')
    for p in me.polygons:
        axis=max(range(3),key=lambda a:abs(p.normal[a]));axes=[a for a in range(3) if a!=axis]
        for li in p.loop_indices:
            v=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=(v[axes[0]]*2+.5,v[axes[1]]*2+.5)
        p.use_smooth=True
    o=bpy.data.objects.new(name,me);scene.collection.objects.link(o);return o

def active(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o

def finish(o,lod):
    active(o)
    bevel=o.modifiers.new('Manufactured edge radii','BEVEL');bevel.width=.0012 if lod<2 else .0007;bevel.segments=[3,1,1][lod];bevel.affect='EDGES';bevel.angle_limit=.65
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    if lod:
        backup=o.data.copy()
        dec=o.modifiers.new('Game LOD reduction','DECIMATE');dec.ratio=[1,.43,.17][lod];dec.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=dec.name)
        if len(o.data.polygons)<6:o.data=backup
    nor=o.modifiers.new('Weighted manufacturing normals','WEIGHTED_NORMAL');nor.keep_sharp=True
    try:bpy.ops.object.modifier_apply(modifier=nor.name)
    except:pass

manifest=[]
limit=int(sys.argv[sys.argv.index('--limit')+1]) if '--limit' in sys.argv else 30
for row in catalog()[:limit]:
    for wear in ['Used','Worn','Damaged']:
        g,data=model(row,wear);name='Light_'+row['name']+'_'+wear
        col=bpy.data.collections.new(name);scene.collection.children.link(col);objs=[];tris=[]
        base=object_from_geo(g,name+'_Geometry',wear)
        for lod in range(3):
            tri=0
            for part in ['Housing','Glass','Bulb','Mounting']:
                faces=[f for f,p in zip(g.f,g.parts) if p==part]
                if not faces:continue
                used=sorted({vi for f in faces for vi in f});remap={v:k for k,v in enumerate(used)}
                sub=Geo(row['id']);sub.v=[g.v[v] for v in used];sub.f=[tuple(remap[v] for v in f) for f in faces];sub.mi=[mi for mi,p in zip(g.mi,g.parts) if p==part]
                o=object_from_geo(sub,'LOD'+str(lod)+'_'+part,wear);finish(o,lod)
                for c in list(o.users_collection):c.objects.unlink(o)
                col.objects.link(o);objs.append(o);tri+=sum(len(p.vertices)-2 for p in o.data.polygons)
            tris.append(tri)
        # Retain editable bevelled high-poly source independent of exported LODs.
        base.name='HighPoly';active(base);be=base.modifiers.new('HighPoly edge radii','BEVEL');be.width=.0015;be.segments=6
        for c in list(base.users_collection):c.objects.unlink(base)
        col.objects.link(base);base.hide_render=True;base.hide_set(True)
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs:o.select_set(True)
        dest=ASSET/'Models'/row['category'];dest.mkdir(parents=True,exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=str(dest/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,use_mesh_modifiers=True,add_leaf_bones=False,path_mode='AUTO')
        data.update(name=name,wear=wear,triangles=tris,geometryHash=hashlib.sha256(str(g.v).encode()+str(g.f).encode()).hexdigest());manifest.append(data)
        # Hide source collections to keep navigation usable in master file.
        col.hide_render=True
        for o in objs:o.hide_set(True)
        bpy.data.objects.remove(base,do_unlink=True) if limit<30 else None
        print('EXPORTED',name,tris,flush=True)

# Ten reusable decorative meshes with sensible local origin.
decor=[]
for k,n in enumerate(['OldWires','CorrugatedConduit','JunctionBox','WallBracket','EmptySocket','BrokenBulb','DetachedShade','Fasteners','OldSwitch','RemovedFixtureMark']):
    g=Geo(k,True)
    if k==0:
        for q in range(3):g.cable([(0,q*.014,0),(.15,q*.014-.03,.01),(.30,q*.014-.02,.06),(.45,q*.014-.06,.03)],True)
    elif k==1:
        g.rod((0,0,0),(.6,0,0),.013,'Rubber',20)
        for q in range(60):g.rod((q*.01,0,0),(q*.01+.004,0,0),.016,'Rubber',16)
    elif k==2:
        g.box((0,0,.03),(.16,.13,.06));g.box((0,0,.064),(.17,.14,.008))
        for x in [-.068,.068]:g.screw((x,0,.07))
        g.rod((0,-.08,.03),(0,-.065,.03),.012,'Rubber')
    elif k==3:g.path([(0,0,0),(0,0,.26),(0,.11,.34)],.022);g.box((0,0,0),(.12,.18,.012))
    elif k==4:g.lathe((0,0,0),[(.027,0),(.027,.05),(.017,.05),(.017,.004)],'Plastic',axis='y')
    elif k==5:g.lathe((0,0,0),[(.012,0),(.021,.03),(.032,.05),(.022,.075),(.019,.073),(.029,.05),(.018,.03)],'ClearGlass',axis='y',broken=True)
    elif k==6:g.lathe((0,0,0),[(.12,0),(.10,.06),(.001,.09),(.001,.085),(.095,.055),(.115,0)],'Glass',axis='y',broken=True)
    elif k==7:
        for q in range(5):g.rod((q*.018,0,0),(q*.018,.025,0),.003,'Aluminium');g.box((q*.018,.027,0),(.009,.003,.009),'Aluminium')
    elif k==8:g.box((0,0,.012),(.075,.09,.024),'Plastic');g.box((0,0,.029),(.038,.05,.015),'Plastic',rot=.13)
    else:
        # Thin solid wall applique with old plaster/paint, drill holes and cable ends.
        g.box((0,0,.001),(.25,.15,.002),'Plastic');g.screw((-.08,.04,.003));g.cable([(0,-.04,.005),(.01,-.09,.008)],True)
    obj=object_from_geo(g,'Decor_'+n,'Damaged');finish(obj,0);active(obj)
    dest=ASSET/'Models'/'Decor';dest.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(dest/('Decor_'+n+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
    obj.hide_set(True);decor.append('Decor_'+n)
(ASSET/'manifest.json').write_text(json.dumps(dict(fixtures=manifest,decor=decor),ensure_ascii=False,indent=2),encoding='utf-8')
for image in bpy.data.images:
    if image.filepath:
        try:image.filepath='//../'+str(Path(image.filepath).relative_to(ASSET)).replace('\\','/')
        except ValueError:pass
bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
print('MODELS_PASS',len(manifest),flush=True)
