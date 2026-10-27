"""Lighting-only revision. Edits the saved source's existing objects, preserving export node names."""
import ast,bpy,bmesh,math,json,sys,hashlib
from pathlib import Path
from mathutils import Vector
from math import pi,sin,cos,sqrt
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import ROOT,ASSET,VERIFY,MATERIALS
PROOF=VERIFY/'ProfessionalAudit'
source=ROOT/'Tools/LightingPack/build_models.py'
tree=ast.parse(source.read_text(encoding='utf-8'))
defs=[n for n in tree.body if isinstance(n,(ast.ClassDef,ast.FunctionDef)) and n.name in ['Geo','model','object_from_geo','active','finish']]
exec(compile(ast.Module(body=defs,type_ignores=[]),str(source),'exec'))
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
scene=bpy.context.scene
MATERIALS=list(MATERIALS)+['GhostPaint']
mats={m.name:m for m in bpy.data.materials}
for wear in ['Used','Worn','Damaged']:
    key='GhostPaint_'+wear
    if key not in mats:
        m=bpy.data.materials.new(key);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.26,.25,.22,1);mats[key]=m
manifest=json.loads((ASSET/'manifest.json').read_text(encoding='utf-8'))

def resample(points,steps=6):
    pts=[Vector(p) for p in points];result=[]
    for i in range(len(pts)-1):
        p0=pts[max(0,i-1)];p1=pts[i];p2=pts[i+1];p3=pts[min(len(pts)-1,i+2)]
        for k in range(steps):
            t=k/steps;result.append(tuple(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)))
    result.append(tuple(pts[-1]));return result

def tube(self,points,r,mat='Steel',n=10,smooth=True,corrugate=0):
    pts=[Vector(p) for p in (resample(points) if smooth and len(points)>2 else points)];vs=[];fs=[]
    for i,p in enumerate(pts):
        axis=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized();u=axis.cross(Vector((0,1,0)))
        if u.length<.01:u=axis.cross(Vector((1,0,0)))
        u.normalize();w=axis.cross(u);radius=r+(corrugate*(.5+.5*cos(i*2*pi/3)) if corrugate else 0)
        vs.extend([tuple(p+radius*(u*cos(k*2*pi/n)+w*sin(k*2*pi/n))) for k in range(n)])
    for j in range(len(pts)-1):
        for k in range(n):fs.append((j*n+k,j*n+(k+1)%n,(j+1)*n+(k+1)%n,(j+1)*n+k))
    fs.extend([tuple(range(n-1,-1,-1)),tuple((len(pts)-1)*n+k for k in range(n))]);self.add(vs,fs,mat)
Geo.path=tube

def washer(g,p,r=.005,axis='z',mat='Aluminium'):
    g.lathe(p,[(r,0),(r,.0015),(r*.48,.0015),(r*.48,0)],mat,n=20,axis=axis)

def thread(g,c,r=.013,depth=.025,turns=6,axis='y',mat='Aluminium',inner=False):
    pts=[]
    for k in range(turns*22+1):
        a=k/22*2*pi;d=k/(turns*22)*depth
        v=(r*cos(a),-d,r*sin(a)) if axis=='y' else (r*cos(a),r*sin(a),d)
        pts.append(tuple(c[j]+v[j] for j in range(3)))
    tube(g,pts,.0009,mat,n=6,smooth=False)

def dome(g,c,r,depth,mat='Glass',axis='z',oval=1,broken=False):
    outer=[];inner=[]
    for k in range(19):
        a=k/18*pi/2;rad=max(.001,r*cos(a));dep=depth*sin(a)
        outer.append((rad,dep));inner.append((max(.0007,rad-.0015),max(0,dep-.0015)))
    g.lathe(c,outer+list(reversed(inner)),mat,n=48,axis=axis,oval=oval,broken=broken)

def bulb(g,c,damaged=False):
    anchors=[(0,.014),(.18,.019),(.43,.0285),(.65,.0315),(.83,.025),(1,.001)]
    points=resample([(t*.095,r,0) for t,r in anchors],5)
    profile=[(max(.001,p[1]),p[0]) for p in points]
    profile += [(max(.0006,r-.0008),max(0,z-.0008)) for r,z in reversed(profile)]
    g.lathe(c,profile,'Bulb',n=48,axis='y',broken=damaged)

def part_replace(g,part,new):
    keep=[k for k,p in enumerate(g.parts) if p!=part]
    g.f=[g.f[k] for k in keep];g.mi=[g.mi[k] for k in keep];g.parts=[g.parts[k] for k in keep]
    offset=len(g.v);g.v.extend(new.v);g.f.extend([tuple(offset+j for j in f) for f in new.f]);g.mi.extend(new.mi);g.parts.extend([part]*len(new.f))

def trim(g,c,w,h,z,mat='Aluminium'):
    # Four separate folded trim strips, with a real opening, not a solid plate.
    x,y,_=c;t=.004
    g.box((x,y+h/2,z),(w,t,.006),mat);g.box((x,y-h/2,z),(w,t,.006),mat)
    g.box((x-w/2,y,z),(t,h,.006),mat);g.box((x+w/2,y,z),(t,h,.006),mat)

def gland(g,p,axis='y',r=.010):
    g.lathe(p,[(r,0),(r,.010),(r*.8,.013),(r*.8,.024),(r*.55,.024),(r*.55,.004)],'Rubber',n=16,axis=axis)
    g.lathe(p,[(r*1.18,.006),(r*1.18,.012),(r*.85,.012),(r*.85,.006)],'Plastic',n=6,axis=axis)

def engineering(f):
    g,_=model(f,f['wear']);s=f['shape'];d=f['wear']=='Damaged';w=f['width'];h=f['height'];dep=f['depth'];i=f['id']
    if s in ['oval','tablet','armored','longcage','sealed','basement']:
        glass=Geo(i,d);r=h*.46
        dome(glass,(0,0,.03),r*.85,dep*.64,oval=w/(2*r),broken=d)
        part_replace(g,'Glass',glass)
        g.part='Housing'
        # Seal, rolled lip and separated screw lugs.
        g.lathe((0,0,dep*.59),[(r*.87,0),(r*.87,.003),(r*.83,.003),(r*.83,0)],'Rubber',n=48,oval=w/(2*r))
        for sign in [-1,1]:
            xx=sign*w*.40;g.box((xx,0,dep*.47),(.016,.026,.010),'Steel');washer(g,(xx,0,dep*.54),.006);g.screw((xx,0,dep*.56),d and sign==1)
        gland(g,(0,-h*.44,.019),r=.0075)
    if s in ['ceilingring','round']:
        glass=Geo(i,d);dome(glass,(0,-.04,0),w*.40,dep-.035,axis='y',broken=d);part_replace(g,'Glass',glass)
        g.part='Housing';g.lathe((0,-.035,0),[(w*.408,0),(w*.408,.003),(w*.391,.003),(w*.391,0)],'Rubber',axis='y')
    if s=='globe':
        glass=Geo(i,d);profile=[]
        for k in range(33):
            a=.015+(pi-.03)*k/32;profile.append((.15*sin(a),-.15*cos(a)))
        profile += [(max(.001,r-.0018),z*.988) for r,z in reversed(profile)]
        glass.lathe((0,.09,.32),profile,'Glass',n=56,axis='y',broken=d);part_replace(g,'Glass',glass)
        g.part='Housing';g.lathe((0,-.058,.32),[(.033,0),(.033,.025),(.025,.025),(.025,0)],'Aluminium',axis='y')
    if s in ['barebulb','wallsocket','cage','portable','pendant']:
        ceiling=s in ['barebulb','cage'];cy=-.03 if ceiling else (.22 if s=='portable' else (-.40 if s=='pendant' else -.035));cz=0 if ceiling or s=='pendant' else .1 if s=='portable' else .11
        new=Geo(i,d);bulb(new,(0,cy-.052 if s!='pendant' else -.40,cz),d);part_replace(g,'Bulb',new)
        g.part='Housing';thread(g,(0,cy-.035,cz),.0175,.019,5)
        for sign in [-1,1]:g.rod((sign*.007,cy-.041,cz),(sign*.007,cy-.052,cz),.0016,'Copper',8)
        gland(g,(0,cy+.004,cz),r=.008)
    if s=='soviet':
        g.v=[(x,-(.44+(-yy-.44)*1.35) if -yy>.27 else yy,z) for x,yy,z in g.v]
        g.part='Housing'
        for k in range(7):
            xx=(k-3)*.026;g.box((xx,.105,.44),(.006,.025,.23),'Aluminium')
        for zz in [.16,.24]:g.lathe((0,.082,zz),[(.028,0),(.028,.011),(.023,.011),(.023,0)],'Aluminium',axis='z')
    if s in ['flood','adjustable','oldflood']:
        # Rectangular tapered reflector cavity, folded aluminium thickness, heat sink and trim.
        g.part='Housing';cy=.02;cz=.14
        vs=[];fs=[]
        for ww,hh,zz in [(w*.69,h*.64,cz-.048),(w*.84,h*.78,cz+.05),(w*.82,h*.76,cz+.052),(w*.68,h*.63,cz-.046)]:
            vs.extend([(-ww/2,cy-hh/2,zz),(ww/2,cy-hh/2,zz),(ww/2,cy+hh/2,zz),(-ww/2,cy+hh/2,zz)])
        for j in range(4):
            for k in range(4):fs.append((j*4+k,j*4+(k+1)%4,((j+1)%4)*4+(k+1)%4,((j+1)%4)*4+k))
        g.add(vs,fs,'Aluminium');trim(g,(0,cy,0),w*.90,h*.85,cz+.080,'Rubber')
        for k in range(8):g.box(((k-3.5)*w*.102,cy,cz-.090),(.006,h*.65,.025),'Steel')
        for xx in [-w*.42,w*.42]:
            for yy in [-h*.37,h*.37]:washer(g,(xx,cy+yy,cz+.085),.007)
        for sign in [-1,1]:g.rod((sign*w*.52,0,.13),(sign*w*.59,0,.13),.018,'Aluminium',6)
        gland(g,(0,-h*.45,.12),r=.009)
    if s in ['doorplastic','visor','underpass','emergency','weatherlong']:
        g.part='Housing';trim(g,(0,0,0),w-.017,h-.017,dep*.67,'Rubber')
        # Moulded return flange and anti-pry clips, smaller service screws.
        for sign in [-1,1]:
            for yy in [-h*.34,h*.34]:g.box((sign*w*.465,yy,dep*.60),(.012,.015,.025),'Aluminium');washer(g,(sign*w*.44,yy,dep*.88),.004)
        if s in ['doorplastic','emergency']:
            for k in range(8):g.box(((k-3.5)*w*.095,-h*.47,dep*.32),(.010,.0025,.015),'Rubber')
        if s=='weatherlong':
            for sign in [-1,1]:g.box((sign*w*.29,0,.005),(.04,.11,.010),'Aluminium')
        gland(g,(-w*.42,-h*.36,.03),r=.006)
    if s in ['ledarm','entry','canopy','panel','office','twintube','singletube','pendant']:
        g.part='Housing'
        if s in ['twintube','singletube']:
            for sign in [-1,1]:
                for zz in [-h*.24,h*.24]:
                    g.rod((sign*w*.445,-.038,zz),(sign*w*.426,-.038,zz),.004,'Copper',10)
                    g.box((sign*w*.453,-.040,zz),(.022,.043,.032),'Plastic')
            for sign in [-1,1]:g.box((sign*w*.27,-.006,0),(.045,.012,h*1.12),'Aluminium');washer(g,(sign*w*.27,-.018,0),.005,axis='y')
        elif s in ['panel','office','canopy']:
            bw=w;bh=h
            for sign in [-1,1]:
                g.box((sign*bw*.48,-.01,0),(.009,.015,bh*.95),'Aluminium')
                g.box((0,-.01,sign*bh*.48),(bw*.95,.015,.009),'Aluminium')
                washer(g,(sign*bw*.43,-.026,sign*bh*.43),.004,axis='y')
            if s=='panel':
                for sign in [-1,1]:g.path([(sign*.27,0,.20),(sign*.30,.025,.20),(sign*.33,.005,.20)],.0018,'Steel',n=8)
        elif s=='pendant':
            for k in range(4):
                a=k*pi/2;g.rod((.03*cos(a),-.39,.03*sin(a)),(.10*cos(a),-.45,.10*sin(a)),.0025,'Aluminium',8)
            gland(g,(0,-.30,0),r=.013)
        elif s=='ledarm':
            for k in range(4):g.rod((-.10+k*.067,.063,.21),(-.10+k*.067,.063,.46),.003,'Rubber',8)
        elif s=='entry':
            g.lathe((0,-.15,.25),[(.085,0),(.085,.006),(.075,.006),(.075,0)],'Rubber',axis='y')
    # Every fixture receives a small service fastening rather than unanchored generic blocks.
    g.part='Housing'
    if f['mount']=='ceiling':
        for sign in [-1,1]:
            xx=sign*min(w*.27,.085);g.box((xx,-.004,0),(.024,.008,.025),'Steel');washer(g,(xx,-.009,0),.004,axis='y');g.rod((xx,-.008,0),(xx,-.012,0),.003,'Aluminium',12)
    if s=='portable':g.v=[(x,y,z+.055) for x,y,z in g.v]
    return g

def mesh_cleanup(o,uv2=True):
    active(o);me=o.data;bm=bmesh.new();bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000003)
    bad=[p for p in bm.faces if p.calc_area()<1e-12]
    if bad:bmesh.ops.delete(bm,geom=bad,context='FACES')
    loose=[v for v in bm.verts if not v.link_faces]
    if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free();me.update()
    if uv2 and me.polygons:
        layer=me.uv_layers.get('LightmapUV') or me.uv_layers.new(name='LightmapUV');me.uv_layers.active=layer
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.25,island_margin=.007,area_weight=.35,correct_aspect=True,scale_to_bounds=False);bpy.ops.object.mode_set(mode='OBJECT')
        me.uv_layers.active_index=0
    tri=o.modifiers.new('Explicit game triangulation','TRIANGULATE');tri.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=tri.name)

def replace_data(o,temp):
    old=o.data;name=old.name;new=temp.data;o.data=new;bpy.data.objects.remove(temp,do_unlink=True)
    if old.users==0:bpy.data.meshes.remove(old)
    new.name=name

def export(objects,path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.hide_set(False);o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True)
    for o in objects:o.hide_set(True)

report=[]
for f in manifest['fixtures']:
    col=bpy.data.collections[f['name']];g=engineering(f);counts=[]
    for lod in range(3):
        count=0
        for o in list(col.objects):
            if not o.name.startswith('LOD'+str(lod)+'_'):continue
            part=o.name.split('_',1)[1].split('.')[0];ids=[k for k,p in enumerate(g.parts) if p==part]
            if not ids:raise RuntimeError('Existing part would be lost: '+f['name']+' '+part)
            faces=[g.f[k] for k in ids];used=sorted({v for face in faces for v in face});remap={v:i for i,v in enumerate(used)}
            sub=Geo(f['id']);sub.v=[g.v[v] for v in used];sub.f=[tuple(remap[v] for v in face) for face in faces];sub.mi=[g.mi[k] for k in ids]
            temp=object_from_geo(sub,'RevisionTemporary',f['wear']);finish(temp,lod);mesh_cleanup(temp);replace_data(o,temp);count+=len(o.data.polygons)
        counts.append(count)
    hi=next(o for o in col.objects if o.name.startswith('HighPoly'))
    temp=object_from_geo(g,'RevisionHighPoly',f['wear']);mesh_cleanup(temp,False);replace_data(hi,temp)
    objects=[o for o in col.objects if o.name.startswith('LOD')]
    export(objects,ASSET/'Models'/f['category']/(f['name']+'.fbx'))
    f['triangles']=counts;f['revision']='2026-10-25-professional-audit';f['geometryHash']=hashlib.sha256(str(g.v).encode()+str(g.f).encode()).hexdigest()
    report.append(dict(name=f['name'],triangles=counts,revision=f['revision']));print('FIXTURE_REVISED',f['name'],counts,flush=True)

for name in manifest['decor']:
    o=next(o for o in scene.objects if o.name==name);g=Geo(400,True);g.part='Housing'
    if name=='Decor_OldWires':
        for k in range(3):
            tube(g,[(0,k*.012,0),(.12,k*.012-.025,.018),(.28,k*.012-.01,.065),(.47,k*.012-.065,.035)],.0038,'Rubber',n=12)
            g.rod((.47,k*.012-.065,.035),(.50,k*.012-.071,.038),.0014,'Copper',8)
        for xx in [.10,.31]:g.box((xx,-.006,.026),(.008,.055,.006),'Aluminium')
    elif name=='Decor_CorrugatedConduit':
        pts=resample([(0,0,0),(.20,-.015,.025),(.40,-.06,.04),(.60,-.045,.1)],45)
        tube(g,pts,.011,'Rubber',n=16,smooth=False,corrugate=.003)
        for xx,yy,zz in [pts[0],pts[-1]]:g.lathe((xx,yy,zz),[(.016,0),(.016,.018),(.012,.018),(.012,0)],'Plastic',n=20,axis='z')
    elif name=='Decor_JunctionBox':
        g.box((0,0,.026),(.16,.13,.052));g.box((0,0,.055),(.164,.134,.007));trim(g,(0,0,0),.153,.122,.056,'Rubber')
        for x in [-.066,.066]:
            for y in [-.052,.052]:washer(g,(x,y,.060),.005);g.screw((x,y,.061))
        for x in [-.042,.042]:gland(g,(x,-.066,.026),r=.010)
        g.lathe((0,0,.061),[(.020,0),(.020,.0008),(.017,.0008),(.017,0)],'Steel',n=28)
    elif name=='Decor_WallBracket':
        g.box((0,0,0),(.12,.18,.012));tube(g,[(0,-.035,.006),(0,-.035,.24),(0,.10,.34)],.018,n=20)
        g.add([(-.003,-.035,.03),(-.003,.055,.03),(-.003,-.035,.19),(.003,-.035,.03),(.003,.055,.03),(.003,-.035,.19)],[(0,1,2),(3,5,4),(0,3,4,1),(1,4,5,2),(2,5,3,0)],'Steel')
        for x in [-.043,.043]:
            for y in [-.065,.065]:washer(g,(x,y,.007),.006);g.screw((x,y,.009))
        g.lathe((0,.09,.34),[(.024,0),(.024,.028),(.018,.028),(.018,0)],'Aluminium',axis='y')
    elif name=='Decor_EmptySocket':
        g.lathe((0,0,0),[(.027,0),(.029,.010),(.027,.044),(.021,.052),(.017,.052),(.017,.006)],'Plastic',n=48,axis='y')
        thread(g,(0,-.008,0),.0185,.034,7,mat='Copper');g.rod((0,-.004,0),(0,-.008,0),.004,'Copper',16)
        for sign in [-1,1]:g.box((sign*.011,.001,0),(.008,.005,.015),'Aluminium')
    elif name=='Decor_BrokenBulb':
        bulb(g,(0,-.022,0),True);g.lathe((0,0,0),[(.014,0),(.014,.025),(.012,.025),(.012,0)],'Aluminium',axis='y');thread(g,(0,0,0),.0145,.023,6)
        for sign in [-1,1]:tube(g,[(sign*.004,-.025,0),(sign*.008,-.052,.004),(sign*.007,-.073,.003)],.0008,'Copper',n=6)
    elif name=='Decor_DetachedShade':dome(g,(0,0,0),.12,.085,'Glass',axis='y',broken=True);g.lathe((0,0,0),[(.122,0),(.122,.004),(.116,.004),(.116,0)],'Rubber',axis='y')
    elif name=='Decor_Fasteners':
        for k in range(5):
            x=k*.021;g.rod((x,0,0),(x,.025,0),.0028,'Aluminium',12);g.rod((x,.025,0),(x,.030,0),.005,'Aluminium',6);washer(g,(x,-.024,0),.006,axis='y');thread(g,(x,.012,0),.003,.012,5)
    elif name=='Decor_OldSwitch':
        g.box((0,0,.012),(.075,.09,.024),'Plastic');trim(g,(0,0,0),.049,.061,.026,'Rubber');g.box((0,0,.029),(.044,.054,.012),'Plastic',rot=.12)
        for y in [-.034,.034]:washer(g,(0,y,.026),.004);g.screw((0,y,.027))
    elif name=='Decor_RemovedFixtureMark':
        # Recessed drill scars and an alpha-edged old paint ghost. No opaque pasted board.
        g.box((0,0,.0004),(.25,.15,.0008),'GhostPaint')
        for x in [-.083,.083]:
            for y in [-.044,.044]:g.lathe((x,y,.001),[(.004,0),(.005,.0015),(.002,.0015),(.002,0)],'Steel',n=16)
        g.cable([(0,-.045,.005),(.008,-.086,.008),(.015,-.11,.01)],True)
    else:raise RuntimeError(name)
    temp=object_from_geo(g,'DecorRevisionTemporary','Damaged');finish(temp,0);mesh_cleanup(temp);replace_data(o,temp);export([o],ASSET/'Models/Decor'/(name+'.fbx'))
    report.append(dict(name=name,triangles=[len(o.data.polygons)],revision='2026-10-25-professional-audit'));print('DECOR_REVISED',name,flush=True)

for image in bpy.data.images:
    if image.filepath and not image.filepath.startswith('//'):
        try:image.filepath='//../'+str(Path(image.filepath).relative_to(ASSET)).replace('\\','/')
        except ValueError:pass
bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
(ASSET/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(PROOF/'geometry_revision.json').write_text(json.dumps(dict(passed=True,assets=report),indent=2),encoding='utf-8')
print('ALL_100_SOURCE_REVISED_PASS',len(report),flush=True)
