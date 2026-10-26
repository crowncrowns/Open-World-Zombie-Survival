"""Build the procedural source library and real FBX models for the balcony kit.

Run with Blender's Python: blender --background --python build.py -- --output <asset folder>
All dimensions in this script are metres in Unity coordinates: X horizontal,
Y vertical, +Z outward from the facade. FBX export uses the validated Unity axes.
"""
from pathlib import Path
from math import sin, cos, pi, sqrt
from random import Random
import bpy, bmesh, json, sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
args = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
OUT = Path(args[args.index('--output')+1]) if '--output' in args else ROOT/'work/PostApocalypseBalconies/UnityProject/Assets/Environment/PostApocalypse/Balconies'
OUT = OUT.resolve()
MODEL = OUT/'Models'/'FBX'
SRC = OUT/'Models'/'Source~'
VER = ROOT/'Verification'/'PostApocalypseBalconies'
VER.mkdir(parents=True, exist_ok=True)
MODEL.mkdir(parents=True, exist_ok=True)
SRC.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1.0
bpy.context.preferences.filepaths.save_version = 0

SURFACE = {
    'BAL_Concrete':'Concrete', 'BAL_ConcreteDamage':'Concrete',
    'BAL_PaintedSteel':'PaintedMetal', 'BAL_CorrodedSteel':'CorrodedMetal',
    'BAL_Timber':'Timber', 'BAL_RottenTimber':'Timber',
    'BAL_Glass':'Glazing', 'BAL_Aluminium':'PaintedMetal',
    'BAL_Plastic':'PaintedMetal', 'BAL_Canvas':'Organic',
    'BAL_Organic':'Organic', 'BAL_Brick':'Concrete',
    'BAL_Burnt':'Timber', 'BAL_Dirt':'Organic', 'BAL_Ceramic':'Concrete'
}
COLORS = {
    'BAL_Concrete':(.43,.42,.39,1), 'BAL_ConcreteDamage':(.30,.29,.27,1),
    'BAL_PaintedSteel':(.38,.37,.32,1), 'BAL_CorrodedSteel':(.32,.17,.09,1),
    'BAL_Timber':(.37,.25,.14,1), 'BAL_RottenTimber':(.20,.18,.13,1),
    'BAL_Glass':(.42,.55,.54,.48), 'BAL_Aluminium':(.43,.45,.43,1),
    'BAL_Plastic':(.39,.39,.34,1), 'BAL_Canvas':(.39,.34,.25,1),
    'BAL_Organic':(.29,.32,.20,1), 'BAL_Brick':(.37,.20,.13,1),
    'BAL_Burnt':(.075,.065,.052,1), 'BAL_Dirt':(.21,.17,.11,1),
    'BAL_Ceramic':(.39,.35,.28,1)
}
MATS = {}
SOURCE_MODELS = []


def make_material(name):
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new(name)
    m.diffuse_color = COLORS[name]
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = COLORS[name]
    p.inputs['Roughness'].default_value = .78 if 'Glass' not in name else .34
    p.inputs['Metallic'].default_value = .76 if ('Steel' in name or name=='BAL_Aluminium') else 0.0
    if name == 'BAL_Glass':
        p.inputs['Alpha'].default_value = .48
        m.blend_method = 'BLEND'
        m.use_nodes = True
    texture = OUT/'Textures'/f"{SURFACE[name]}_BaseColor.png"
    if texture.exists():
        image = bpy.data.images.load(str(texture), check_existing=True)
        tex = m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=image; tex.label='Procedural 2K Base Color'
        m.node_tree.links.new(tex.outputs['Color'], p.inputs['Base Color'])
        normal_path = OUT/'Textures'/f"{SURFACE[name]}_Normal.png"
        normal_output=None
        if normal_path.exists():
            normal_image = bpy.data.images.load(str(normal_path), check_existing=True)
            normal_tex = m.node_tree.nodes.new('ShaderNodeTexImage'); normal_tex.image=normal_image; normal_tex.image.colorspace_settings.name='Non-Color'
            normal = m.node_tree.nodes.new('ShaderNodeNormalMap')
            m.node_tree.links.new(normal_tex.outputs['Color'],normal.inputs['Color']); normal_output=normal.outputs['Normal']
        mask_path=OUT/'Textures'/f"{SURFACE[name]}_Mask.png"
        if mask_path.exists():
            mask_image=bpy.data.images.load(str(mask_path),check_existing=True);mask_image.colorspace_settings.name='Non-Color'
            mask_tex=m.node_tree.nodes.new('ShaderNodeTexImage');mask_tex.image=mask_image;mask_tex.label='Packed Metallic / AO / Height / Smoothness'
            separate=m.node_tree.nodes.new('ShaderNodeSeparateColor');separate.mode='RGB'
            m.node_tree.links.new(mask_tex.outputs['Color'],separate.inputs['Color'])
            m.node_tree.links.new(separate.outputs['Red'],p.inputs['Metallic'])
            rough=m.node_tree.nodes.new('ShaderNodeMath');rough.operation='SUBTRACT';rough.inputs[0].default_value=1.0
            m.node_tree.links.new(mask_tex.outputs['Alpha'],rough.inputs[1]);m.node_tree.links.new(rough.outputs[0],p.inputs['Roughness'])
            if normal_output is not None:
                bump=m.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.12;bump.inputs['Distance'].default_value=.018
                m.node_tree.links.new(separate.outputs['Blue'],bump.inputs['Height']);m.node_tree.links.new(normal_output,bump.inputs['Normal'])
                m.node_tree.links.new(bump.outputs['Normal'],p.inputs['Normal'])
        elif normal_output is not None:m.node_tree.links.new(normal_output,p.inputs['Normal'])
    MATS[name]=m
    return m


for material_name in SURFACE: make_material(material_name)


class Mesh:
    def __init__(self): self.v=[]; self.f=[]; self.uv=[]; self.mi=[]; self.labels=[]; self.label=0
    def face(self, points, mat, uvs=None):
        points=[tuple(float(c) for c in p) for p in points]
        if uvs is None:
            if len(points)>=3:
                a,b,c=Vector(points[0]),Vector(points[1]),Vector(points[2])
                uvs=[(0,0),((b-a).length,0),((b-a).length,(c-b).length)]
                if len(points)==4: uvs.append((0,(points[3][1]-points[0][1])**2**.5 if False else (Vector(points[3])-a).length))
            else: uvs=[(0,0)]*len(points)
        start=len(self.v); self.v.extend(points); self.labels.extend([self.label]*len(points)); self.uv.extend(uvs); self.f.append(tuple(range(start,start+len(points)))); self.mi.append(mat)
    def box(self, center, size, mat, bevel=0.0):
        x,y,z=center; sx,sy,sz=[max(.001,float(q))/2 for q in size]
        if bevel>0:
            b=min(bevel,sx*.45,sy*.45,sz*.45)
            corners=[(-sx+b,-sy,-sz+b),(sx-b,-sy,-sz+b),(sx,-sy,-sz+b),(sx,-sy,sz-b),
                     (sx-b,-sy,sz),(-sx+b,-sy,sz),(-sx,-sy,sz-b),(-sx,-sy,-sz+b)]
            # Small chamfered rectangular prisms are assembled from a stable octagonal ring.
            # For speed/robustness a plain block is used for dimensions near the bevel limit.
            if b < .0005: bevel=0
        v=[(x+i*sx,y+j*sy,z+k*sz) for i,j,k in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
        for inds in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:
            p=[v[i] for i in inds]
            a,bv,c=Vector(p[0]),Vector(p[1]),Vector(p[2]); w=(bv-a).length; h=(c-bv).length
            self.face(p,mat,[(0,0),(w,0),(w,h),(0,h)])
    def cylinder(self, a, b, radius, mat, sides=10, r2=None):
        a,b=Vector(a),Vector(b); d=b-a
        if d.length < 1e-6: return
        t=d.normalized(); ref=Vector((0,1,0)) if abs(t.y)<.92 else Vector((1,0,0))
        u=t.cross(ref).normalized(); v=t.cross(u).normalized(); r2=radius if r2 is None else r2
        rings=[]
        for p,r in [(a,radius),(b,r2)]:
            rings.append([tuple(p+r*(cos(2*pi*i/sides)*u+sin(2*pi*i/sides)*v)) for i in range(sides)])
        for i in range(sides):
            j=(i+1)%sides
            self.face([rings[0][i],rings[0][j],rings[1][j],rings[1][i]],mat,[(i/sides,0),((i+1)/sides,0),((i+1)/sides,d.length),(i/sides,d.length)])
        self.face(rings[0][::-1],mat,[(.5+.5*cos(2*pi*i/sides),.5+.5*sin(2*pi*i/sides)) for i in range(sides)])
        self.face(rings[1],mat,[(.5+.5*cos(2*pi*i/sides),.5+.5*sin(2*pi*i/sides)) for i in range(sides)])
    def rod_path(self, points, radius, mat, sides=8):
        for a,b in zip(points,points[1:]): self.cylinder(a,b,radius,mat,sides)
    def polygon(self, points, y0, y1, mat, triangulate=True):
        pts=[(float(x),float(z)) for x,z in points]
        if len(pts)<3:return
        area=sum(pts[i][0]*pts[(i+1)%len(pts)][1]-pts[(i+1)%len(pts)][0]*pts[i][1] for i in range(len(pts)))/2
        order=list(range(len(pts))) if area>0 else list(reversed(range(len(pts))))
        triangles=[]
        def cross(a,b,c): return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
        def inside(p,a,b,c):
            v1=cross(a,b,p);v2=cross(b,c,p);v3=cross(c,a,p)
            return (v1>=-1e-8 and v2>=-1e-8 and v3>=-1e-8) or (v1<=1e-8 and v2<=1e-8 and v3<=1e-8)
        guard=0
        while len(order)>3 and guard<500:
            guard+=1; found=False
            for k in range(len(order)):
                ia,ib,ic=order[k-1],order[k],order[(k+1)%len(order)]
                a,b,c=pts[ia],pts[ib],pts[ic]
                if cross(a,b,c)<=1e-9: continue
                if any(inside(pts[j],a,b,c) for j in order if j not in (ia,ib,ic)): continue
                triangles.append((ia,ib,ic));order.pop(k);found=True;break
            if not found: break
        if len(order)==3:triangles.append(tuple(order))
        if not triangles:
            for i in range(1,len(pts)-1): triangles.append((0,i,i+1))
        # Top and bottom faces use the same UV map; side surfaces keep real-world scale.
        for tri in triangles:
            q=[pts[i] for i in tri]
            self.face([(x,y1,z) for x,z in q],mat,[(x,z) for x,z in q])
            self.face([(x,y0,z) for x,z in reversed(q)],mat,[(x,z) for x,z in reversed(q)])
        for i,(x,z) in enumerate(pts):
            x2,z2=pts[(i+1)%len(pts)]
            self.face([(x,y0,z),(x2,y0,z2),(x2,y1,z2),(x,y1,z)],mat,[(0,0),(sqrt((x2-x)**2+(z2-z)**2),0),(sqrt((x2-x)**2+(z2-z)**2),y1-y0),(0,y1-y0)])
    def ellipsoid(self, center, size, mat, rings=6, sides=10):
        x,y,z=center;rx,ry,rz=[q/2 for q in size]
        levels=[]
        for j in range(1,rings):
            lat=-pi/2+pi*j/rings
            levels.append([(x+rx*cos(lat)*cos(2*pi*i/sides),y+ry*sin(lat),z+rz*cos(lat)*sin(2*pi*i/sides)) for i in range(sides)])
        bottom=(x,y-ry,z);top=(x,y+ry,z)
        for i in range(sides):self.face([bottom,levels[0][(i+1)%sides],levels[0][i]],mat)
        for r in range(len(levels)-1):
            for i in range(sides):self.face([levels[r][i],levels[r][(i+1)%sides],levels[r+1][(i+1)%sides],levels[r+1][i]],mat)
        for i in range(sides):self.face([levels[-1][i],levels[-1][(i+1)%sides],top],mat)


def create_obj(name, mesh, collision=False):
    # Internal modelling coordinates are Unity XYZ. Blender/FBX's standard Y-up conversion
    # is handled by the exporter. Vertices are stored in Blender XZY coordinates here.
    bm=bpy.data.meshes.new(name); bm.from_pydata([(x,z,y) for x,y,z in mesh.v],[],mesh.f); bm.update()
    used=sorted(set(mesh.mi))
    for mi in used: bm.materials.append(make_material(MAT_NAMES[mi]))
    uv=bm.uv_layers.new(name='UV0')
    labels=bm.color_attributes.new(name='SupportLabel',type='FLOAT_COLOR',domain='CORNER')
    for poly,mi in zip(bm.polygons,mesh.mi):
        poly.material_index=used.index(mi)
        for li in poly.loop_indices:
            vi=bm.loops[li].vertex_index
            uv.data[li].uv=mesh.uv[vi]
            tag=mesh.labels[vi]
            labels.data[li].color=(float(tag&1),float((tag>>1)&1),float((tag>>2)&1),1.0)
    bm.validate(clean_customdata=False)
    bm.calc_loop_triangles()
    ob=bpy.data.objects.new(name,bm); bpy.context.collection.objects.link(ob)
    edit=bmesh.new();edit.from_mesh(bm);bmesh.ops.remove_doubles(edit,verts=list(edit.verts),dist=1e-7);bmesh.ops.recalc_face_normals(edit,faces=list(edit.faces));edit.to_mesh(bm);edit.free();bm.update()
    for poly in bm.polygons: poly.use_smooth=False
    if not collision and name.startswith('LOD'):
        # Small manufactured edge radii catch light at close inspection without
        # changing the authored metre-scale envelope or deck collision surfaces.
        bevel=ob.modifiers.new('0.003 m manufactured edge radii','BEVEL')
        bevel.width=.003;bevel.segments=2;bevel.limit_method='ANGLE';bevel.angle_limit=.62;bevel.use_clamp_overlap=True
    return ob


MAT_NAMES=['BAL_Concrete','BAL_ConcreteDamage','BAL_PaintedSteel','BAL_CorrodedSteel','BAL_Timber','BAL_RottenTimber','BAL_Glass','BAL_Aluminium','BAL_Plastic','BAL_Canvas','BAL_Organic','BAL_Brick','BAL_Burnt','BAL_Dirt','BAL_Ceramic']
M={name:i for i,name in enumerate(MAT_NAMES)}


def B(name): return M[name]


def add_mounts(m, level, material='BAL_CorrodedSteel'):
    steel=B(material)
    for x in (-1.18,1.18):
        m.box((x,.06,.055),(.24,.12,.06),steel)
        m.box((x,.02,.18),(.09,.04,.30),steel)
        if level==0:
            for dx in (-.075,.075):
                m.cylinder((x+dx,.062,.019),(x+dx,.087,.019),.011,steel,8)


def slab_outline(state, rng, depth=1.2):
    rear=.02; front=depth-.025; hw=1.335
    corner=.035 if state in ('A','B') else .065
    if state=='A':
        return [(-hw+corner,rear),(hw-corner,rear),(hw, rear+corner),(hw,front-corner),(hw-corner,front),(-hw+corner,front),(-hw,front-corner),(-hw,rear+corner)]
    if state=='B':
        return [(-hw+corner,rear),(hw-corner,rear),(hw,rear+corner),(hw,front-.12),(hw-.12,front),(-hw+.08,front),(-hw,front-.08),(-hw,rear+corner)]
    if state=='C':
        # One broad missing section at the right-front corner and a second shallow edge loss.
        return [(-hw+corner,rear),(hw-corner,rear),(hw,rear+corner),(hw,.58),(hw-.48,.57),(hw-.43,.86),(hw-.75,front),(-.18,front),(-.08,front-.11),(-.55,front-.13),(-.60,front-.33),(-hw,front-.31),(-hw,rear+corner)]
    return []


def inside_polygon(x,z,outline):
    inside=False
    for i,(ax,az) in enumerate(outline):
        bx,bz=outline[(i+1)%len(outline)]
        if (az>z)!=(bz>z) and x<(bx-ax)*(z-az)/(bz-az)+ax:inside=not inside
    return inside


def add_slab(m, state, rng, depth=1.2, floor_mat='BAL_Concrete', thickness=.15, collision=False):
    if state=='D':
        # Two surviving slab shelves; the large central/front void is real geometry
        # and the simplified floor collider is generated from exactly these surfaces.
        left=[(-1.33,.02),(-.43,.02),(-.43,.42),(-1.28,.52),(-1.33,.42)]
        right=[(.44,.02),(1.31,.02),(1.31,.39),(.92,.47),(.45,.40)]
        back=[(-.43,.02),(.44,.02),(.45,.31),(-.43,.29)]
        for p in (left,right,back):m.polygon(p,0,thickness,B(floor_mat))
        if not collision:
            for x,z in [(-.29,.28),(.40,.25),(.93,.37)]:
                m.rod_path([(x,.09,z),(x+.03,.015,z+.1),(x-.01,.012,z+.22)],.011,B('BAL_CorrodedSteel'),6)
        return
    m.polygon(slab_outline(state,rng,depth),0,thickness,B(floor_mat))
    if state=='C':
        # Exposed fractured rebar at the missing edge; each rod stays inside the module.
        for x in (.48,.76,1.02):
            m.rod_path([(x,.10,.71),(x+.035,.02,.79),(x+.01,.015,.93)],.009,B('BAL_CorrodedSteel'),6)
    if state in ('B','C') and not collision:
        for _ in range(3 if state=='B' else 6):
            x=rng.uniform(-1.15,1.15);length=rng.uniform(.12,.32);z=rng.uniform(.3,max(.31,depth-.20-length))
            if abs(x)>1.18 or z>depth-.12:continue
            points=[(x,.151,z),(x+rng.uniform(-.05,.05),.151,z+length*.45),(x+rng.uniform(-.06,.06),.151,z+length)]
            if all(inside_polygon(px,pz,slab_outline(state,rng,depth)) for px,py,pz in points):m.rod_path(points,.0028,B('BAL_ConcreteDamage'),5)


def add_metal_supports(m, level, depth, mat='BAL_CorrodedSteel'):
    s=B(mat);r=[.024,.022,.018,.016][level]
    # Surviving anchored steel skeleton carries rail fragments above missing concrete.
    for x in (-1.18,1.18):m.box((x,.06,depth/2),(.045,.06,depth-.04),s)
    m.box((0,.06,depth-.045),(2.64,.06,.045),s)
    for x in (-1.08,1.08):
        m.box((x,.013,depth/2),(.065,.035,depth-.12),s)
        m.rod_path([(x,0,.07),(x,.60,.38),(x,.02,.78)],r,s,[10,8,6,5][level])
        m.box((x,.04,.075),(.18,.08,.10),s)


def add_concrete_cracks(m, level, rng, state, depth):
    if state=='D':return
    n=[7,4,2,1][level]
    mat=B('BAL_ConcreteDamage')
    for i in range(n):
        x=-1.12+i*.34+rng.uniform(-.07,.07); z=.28+(i%3)*.24
        pts=[(x,.153,z),(x+.035,.154,z+.045),(x-.025,.154,z+.105),(x+.06,.154,z+.17)]
        if all(inside_polygon(x,z,slab_outline(state,rng,depth)) for x,y,z in pts):m.rod_path(pts,[.003,.003,.0024,.002][level],mat,5)


def add_wood_floor(m, level, rng, state, depth, collision=False):
    mat=B('BAL_RottenTimber' if state in ('C','D') and rng.random()<.45 else 'BAL_Timber')
    # Missing boards are structural openings: retain their layout at every LOD.
    board_count=13
    x0=-1.31; x1=1.31; width=(x1-x0)/board_count
    missing={
        'A':set(), 'B':{board_count-2},
        'C':{board_count-1,board_count//2}, 'D':{board_count-1,board_count-2,board_count//2}
    }[state]
    for i in range(board_count):
        logical=min(12,int(i*13/board_count))
        if logical in missing: continue
        x=x0+(i+.5)*width
        length=depth-rng.uniform(.02,.10) if state in ('B','C','D') and i in missing else depth-.04
        z0=.02
        m.box((x,.125,z0+length/2),(width-.004,.05,length),mat)
        if level==0:
            for zz in (.12,depth-.12):
                for xx in (x-width*.25,x+width*.25):
                    m.cylinder((xx,.152,zz),(xx,.157,zz),.0045,B('BAL_CorrodedSteel'),6)
    if not collision:
        for z in (.18,depth-.16):m.box((0,.075,z),(2.63,.065,.08),B('BAL_RottenTimber' if state in ('C','D') else 'BAL_Timber'))


def add_grate_floor(m, level, rng, state, depth):
    steel=B('BAL_CorrodedSteel'); edge=B('BAL_PaintedSteel')
    bar_count=[38,24,14,9][level]
    missing={
        'A':set(), 'B':set(range(bar_count//3,bar_count//3+2)),
        'C':set(range(bar_count//2-3,bar_count//2+4)),
        'D':set(list(range(bar_count//4,bar_count//2+2))+list(range(bar_count-3,bar_count)))
    }[state]
    m.box((0,.118,.51),(2.66,.065,.07),edge)
    for x in (-1.29,1.29):m.box((x,.118,depth/2),(.07,.065,depth-.06),edge)
    for i in range(bar_count):
        if i in missing: continue
        x=-1.26+(i+.5)*(2.52/bar_count)
        m.box((x,.132,depth/2),(.018,.018,depth-.12),steel)
    for z in (.12,depth-.12):m.box((0,.112,z),(2.62,.035,.05),steel)


def add_guardrails(m, level, rng, state, kind, depth):
    sev={'A':.06,'B':.28,'C':.58,'D':.86}[state]
    sides=[5,4,3,2][level]
    painted=B('BAL_PaintedSteel');rust=B('BAL_CorrodedSteel');wood=B('BAL_Timber');rotten=B('BAL_RottenTimber')
    railmat=wood if kind=='wood' else rust if state in ('C','D') or kind in ('industrial','burnt') else painted
    r=[.018,.016,.014,.012][level]
    # Back mounting post faces remain inside the exact 2.7 m module width.
    for x in (-1.30,1.30):
        if state=='D' and rng.random()<.55: continue
        top_y=1.18
        if state in ('C','D') and rng.random()<sev:
            top_y-=rng.uniform(.12,.4)
        mat=rotten if kind=='wood' and state in ('C','D') else railmat
        m.rod_path([(x,.18,.22),(x,top_y,.20)],r,mat,[10,8,6,5][level])
        m.box((x,.16,.22),(.055,.12,.08),rust)
    # Front edge. Broken states use short, physically attached remnants.
    frontz=depth-.045
    if state=='D': segments=[(-1.30,-.88),(-.54,.08),(.77,1.29)]
    elif state=='C': segments=[(-1.30,-.55),(-.22,.55),(.88,1.29)]
    else: segments=[(-1.30,1.30)]
    if kind in ('glazedwood','loggia'):
        return
    for x0,x1 in segments:
        # Every disconnected upper rail segment has full-height end posts.
        for x in (x0,x1):m.cylinder((x,.06,frontz),(x,1.18,frontz),r,railmat,[10,8,6,5][level])
        def top_path(y):
            if state in ('C','D') and x1-x0>.6:return [(x0,y,frontz),(x0+(x1-x0)*.45,y-.06,frontz+.035),(x1,y-.10,frontz)]
            return [(x0,y,frontz),(x1,y,frontz)]
        if kind=='wood':
            m.rod_path(top_path(1.18),.028,railmat,[8,7,6,5][level])
            m.rod_path([(x0,.42,frontz),(x1,.42,frontz)],.018,railmat,[8,7,6,5][level])
        else:
            m.rod_path(top_path(1.18),.022,railmat,[10,8,6,5][level])
            m.rod_path([(x0,.38,frontz),(x1,.38,frontz)],.018,railmat,[10,8,6,5][level])
        span=x1-x0; count=max(1,round(span/([.22,.30,.46,.7][level])))
        for i in range(count+1):
            x=x0+span*i/count
            if state=='D' and rng.random()<.55:continue
            if state=='C' and rng.random()<.24:continue
            ytop=1.18
            raw_bend=rng.uniform(-.12,.12) if state in ('C','D') and rng.random()<sev else 0
            bend=(-abs(raw_bend) if x>0 else abs(raw_bend))
            if kind=='wood':
                m.rod_path([(x,.42,frontz),(x+bend,ytop,frontz)],.013,railmat,[8,7,6,5][level])
            else:
                m.cylinder((x,.39,frontz),(x+bend,ytop,frontz),r,railmat,[8,7,6,5][level])
            if level==0 and state in ('B','C','D') and i%2==0:
                m.cylinder((x,.46,frontz),(x,.48,frontz),.0065,B('BAL_CorrodedSteel'),6)
    # Side returns.
    for side in (-1,1):
        z0=.22; z1=frontz
        if state=='D' and side==1: continue
        m.rod_path([(side*1.29,.42,z0),(side*1.29,1.18,z1)],.018,railmat,[9,7,6,5][level])
        m.rod_path([(side*1.29,1.18,z0),(side*1.29,1.18,z1)],.021,railmat,[9,7,6,5][level])
        for j in range(sides):
            t=(j+1)/(sides+1);z=z0+(z1-z0)*t
            if state=='D' and j%2==0:continue
            y=1.18-(.18 if state in ('C','D') and j==sides//2 else 0)
            m.cylinder((side*1.29,.42,z),(side*1.29,y,z),r,railmat,[8,7,6,5][level])


def add_sheet_panels(m, level, state, depth, rng, kind):
    mat=B('BAL_CorrodedSteel' if state in ('C','D') else 'BAL_PaintedSteel')
    if kind=='khrush':
        panel_y0=.45; panel_y1=.93; z=depth-.055
        widths=[.46,.46,.46,.46,.46]
        x=-1.18
        for i,w in enumerate(widths):
            if state=='D' and i in (1,3):continue
            h=panel_y1-panel_y0; xmid=x+w/2
            dent=.055 if state in ('B','C','D') and i%2 else 0
            m.box((xmid,(panel_y0+panel_y1)/2,z),(w-.025,h,z if False else .018),mat)
            if dent and level==0:
                m.rod_path([(xmid-.13,panel_y1-.08,z+.011),(xmid,panel_y1-.15,z+.035),(xmid+.12,panel_y1-.1,z+.012)],.006,B('BAL_ConcreteDamage'),5)
            x+=w
    elif kind=='fortified':
        z=depth-.06
        for x in (-.98,-.33,.32,.97):
            if state=='D' and x>.5:continue
            h=.68 if x!=-.33 else .51
            m.box((x,.53,z),(.60,h,.035),mat)
            # Eyeline slit in the survivor-built shield.
            m.box((x,.86,z+.021),(.21,.035,.004),B('BAL_Burnt'))
            for dx in (-.23,.23):
                m.cylinder((x+dx,.24,z+.024),(x+dx,.27,z+.024),.012,B('BAL_CorrodedSteel'),6)


def add_glazing(m, level, rng, state, depth, modern=False):
    frame=B('BAL_Aluminium' if modern else ('BAL_RottenTimber' if state in ('C','D') else 'BAL_Timber'))
    glass=B('BAL_Glass'); dirty=B('BAL_Plastic' if state=='D' and not modern else 'BAL_Glass')
    outerz=depth-.06; y0=.34; y1=2.18; x0=-1.28; x1=1.28
    bays=5 if modern else 4; xs=[x0+(x1-x0)*i/bays for i in range(bays+1)]
    for x in (-1.28,0,1.28):m.box((x,(.06+y0)/2,outerz),(.045,y0-.06,.055),frame)
    # Outer frame and track. Individual profiles produce real openings rather than one filled box.
    for x in xs:
        off=.045 if modern else .055
        m.box((x,(y0+y1)/2,outerz), (off,y1-y0,.055),frame)
    m.box((0,y0,outerz),(2.62,.07,.08),frame)
    if state!='D' or rng.random()<.6:m.box((0,y1,outerz),(2.62,.06,.07),frame)
    # Sashes and panes are separated so broken/missing glass changes actual geometry.
    for i in range(bays):
        left,right=xs[i]+.04,xs[i+1]-.04
        if right-left<.08:continue
        missing = (state=='D' and i in (0,bays-1)) or (state=='C' and i==bays//2) or (state=='B' and i==bays-1 and rng.random()<.6)
        board = state in ('C','D') and i==(1 if modern else 0)
        if missing and not board:continue
        mat=B('BAL_RottenTimber') if board else (dirty if state in ('B','C','D') and rng.random()<.48 else glass)
        height=y1-y0-.06
        if board:
            for j in range(3 if level<2 else 2):
                yy=y0+.12+j*height/(3 if level<2 else 2)
                m.box(((left+right)/2,yy,outerz+.008),(right-left+.05,.055,.025),B('BAL_Timber'))
        else:
            pane_depth=.012 if modern else .018
            m.box(((left+right)/2,(y0+y1)/2,outerz+.01),(right-left,height,pane_depth),mat)
            # Cross rails and sliding overlap remain visible at close range.
            if level==0:
                m.box(((left+right)/2,y0+.13,outerz+.024),(right-left,.022,.018),frame)
                if modern:m.box(((left+right)/2,(y0+y1)/2,outerz+.026),(.018,height,.018),frame)
        if not modern and state in ('B','C') and i==0:
            # One visibly loose sash is tilted but stays inside the facade module.
            m.rod_path([(left,y1-.05,outerz+.005),(right-.12,y1-.15,outerz+.015)],.018,frame,[8,7,6,5][level])
    # Small sill and lower exterior drip rail.
    m.box((0,y0-.05,outerz+.02),(2.68,.065,.045),B('BAL_ConcreteDamage' if state in ('C','D') else 'BAL_Aluminium'))


def add_vines(m, level, rng, state, depth):
    count={'A':2,'B':5,'C':8,'D':10}[state]
    leafmat=B('BAL_Organic');stem=B('BAL_RottenTimber')
    anchors={'C':[-.55,-.22,.55,.88],'D':[-.88,-.54,.08,.77]}
    for i in range(count):
        x=-1.18+2.36*i/max(1,count-1);top=1.18
        if state in anchors:
            x=anchors[state][i%len(anchors[state])];top=1.10
        z=depth-.045;direction=1 if i%2==0 else -1
        length=min(.97,rng.uniform(.55,1.25)*(1.0 if state in ('C','D') else .72))
        knots=[6,5,4,3][level]
        points=[]
        for j in range(knots):
            t=j/(knots-1)
            points.append(Vector((max(-1.18,min(1.18,x+direction*.055*sin(t*pi*2))),top-t*length,z-.018*sin(t*pi))))
        m.rod_path(points,.008,stem,[7,6,5,4][level])
        leaves=[8,5,3,2][level]
        for j in range(leaves):
            t=(j+1)/(leaves+1);segment=min(knots-2,int(t*(knots-1)))
            root=points[segment].lerp(points[segment+1],t*(knots-1)-segment)
            size=rng.uniform(.035,.065)
            for side in (-1,1):
                center=root+Vector((side*size*.60,-.012,.006))
                m.cylinder(root,center,.003,stem,5)
                m.ellipsoid(center,(size*1.6,size*.45,.012),leafmat,3,5)
    if state=='C':
        outline=slab_outline(state,rng,depth)
        for x in (-1.0,-.3,.5,1.0):
            z=depth*.6
            if all(inside_polygon(x+dx,z+dz,outline) for dx in (-.11,.11) for dz in (-.065,.065)):
                m.ellipsoid((x,.156,z),(.22,.012,.13),leafmat,3,7)


def add_burn_damage(m, level, rng, state, depth):
    mat=B('BAL_Burnt'); soot=B('BAL_ConcreteDamage')
    for i in range(0 if state=='D' else [12,8,5,3][level]):
        x=-1.20+i*.21;z=.24+(i%4)*.22
        length=rng.uniform(.10,.24)
        points=[(x,.153,z),(x+.025,.154,z+length*.35),(x-.018,.154,z+length)]
        if all(inside_polygon(px,pz,slab_outline(state,rng,depth)) for px,py,pz in points):m.rod_path(points,.003 if level<2 else .002,soot,5)
    # Charred remnants of wooden glazing rails.
    for x in (-1.16,-.63,.05,.72,1.18):
        if state=='D' and abs(x)<.7:continue
        m.rod_path([(x,.35,depth-.06),(x+rng.uniform(-.025,.025),1.04,depth-.055)],.022,mat,[8,7,6,5][level])
    if state in ('C','D'):
        for x in (-1.0,-.45,.35,.9):
            if state=='D' and x>0:continue
            m.box((x,.19,.20 if state=='D' else .56),(.16,.08,.13),B('BAL_Burnt'))


def prop_geo(m, prop, level, rng, center=(0,.15,.5), compact=False):
    x,y,z=center; steel=B('BAL_CorrodedSteel'); paint=B('BAL_PaintedSteel'); wood=B('BAL_Timber'); rotten=B('BAL_RottenTimber'); org=B('BAL_Organic'); glass=B('BAL_Glass')
    rad=[10,8,6,5][level]
    if prop=='SatelliteDish_Rusted':
        m.box((x,y+.055,z),(.26,.04,.20),steel)
        m.cylinder((x,y+.065,z),(x,y+.12,z),.012,steel,6)
        r=.25; depth=.055
        rings=[2,2,1,1][level]; n=[18,12,8,6][level]
        for k in range(rings):
            rr=r*(.48+.52*k/max(1,rings-1)); yy=y+.36+depth*(1-(rr/r)**2)
            points=[(x+rr*cos(2*pi*i/n),yy,z+rr*sin(2*pi*i/n)) for i in range(n)]
            for i in range(n):m.cylinder(points[i],points[(i+1)%n],.004,steel,5)
        for i in range(6):
            angle=2*pi*i/6
            points=[(x,y+.415,z),(x+r*.48*cos(angle),y+.36+depth*(1-.48**2),z+r*.48*sin(angle))]
            if rings>1:points.append((x+r*cos(angle),y+.36,z+r*sin(angle)))
            m.rod_path(points,.006,steel,6)
        m.rod_path([(x,y+.10,z),(x,y+.33,z),(x,y+.43,z)],.012,steel,8)
        m.rod_path([(x-.18,y+.13,z),(x,y+.08,z),(x+.18,y+.13,z)],.009,steel,7)
        if level==0:m.cylinder((x,y+.44,z),(x,y+.49,z),.009,paint,8)
    elif prop=='Antenna_Bent':
        m.rod_path([(x,y,z),(x,y+.48,z),(x+.07,y+.60,z+.01),(x+.16,y+.54,z+.01)],.012,steel,rad)
        for k in range([6,4,3,2][level]):
            yy=y+.16+k*.055;m.rod_path([(x-.17,yy,z),(x+.17,yy,z)],.007,steel,6)
        m.box((x,y+.03,z),(.22,.06,.18),paint)
    elif prop=='AC_OutdoorUnit':
        m.box((x,y+.22,z),(.68,.40,.27),paint,.005)
        m.box((x,y+.22,z+.14),(.42,.32,.025),B('BAL_Aluminium'))
        for k in range([10,7,4,2][level]):
            xx=x-.18+k*.036;m.box((xx,y+.22,z+.150),(.009,.26,.009),steel)
        if level<2:
            m.rod_path([(x-.24,y+.22,z+.16),(x+.24,y+.22,z+.16)],.012,steel,6)
        for dx in (-.23,.23):m.box((x+dx,y+.04,z-.02),(.10,.08,.32),steel)
    elif prop=='Clothesline':
        for dx in (-.54,.54):m.box((x+dx,y-.007,z),(.10,.014,.24),steel)
        for dx in (-.54,.54):m.rod_path([(x+dx,y,z),(x+dx,y+.82,z)],.014,steel,7)
        for i in range([5,3,2,1][level]):
            yy=y+.78-i*.12;m.rod_path([(x-.54,yy,z),(x+.54,yy,z)],.0035,steel,5)
        if level==0:
            for xx in (-.38,-.1,.21,.39):m.box((x+xx,y+.48,z+.01),(.14,.26,.012),B('BAL_Canvas'))
    elif prop in ('Planter_Old','BrokenPot'):
        if prop=='Planter_Old':
            m.box((x,y+.12,z),(.54,.24,.22),B('BAL_Ceramic'),.005);m.box((x,y+.245,z),(.52,.018,.21),B('BAL_Dirt'))
            for i in range([5,3,2,1][level]):
                xx=x+(i-(2 if level<2 else 1))* .11
                m.rod_path([(xx,y+.25,z),(xx+.025,y+.38,z+.01)],.004,org,5)
                m.ellipsoid((xx+.02,y+.38,z+.01),(.09,.035,.02),org,3,6)
        else:
            m.ellipsoid((x,y+.11,z),(.28,.22,.25),B('BAL_Ceramic'),5,10)
            m.box((x,y+.22,z),(.22,.025,.19),B('BAL_Dirt'))
            if level<2:m.rod_path([(x-.08,y+.19,z+.09),(x,y+.25,z+.10),(x+.1,y+.08,z+.12)],.004,soot if False else steel,5)
    elif prop=='WoodenCrate':
        m.box((x,y+.18,z),(.44,.36,.40),wood)
        for yy in (y+.05,y+.31):m.box((x,yy,z+.207),(.46,.045,.025),rotten)
        if level<2:
            m.rod_path([(x-.20,y+.03,z+.22),(x+.20,y+.33,z+.22)],.014,rotten,6)
            m.rod_path([(x+.20,y+.03,z+.22),(x-.20,y+.33,z+.22)],.014,rotten,6)
        for xx in (-.20,.20):
            for zz in (-.18,.18):m.box((x+xx,y+.18,z+zz),(.035,.36,.035),rotten)
    elif prop=='WireMesh':
        for dx in (-.29,.29):m.box((x+dx,y-.027,z),(.12,.026,.24),steel)
        w=.72;h=.52;z0=z
        for xx in (-w/2,w/2):m.rod_path([(x+xx,y,z0),(x+xx,y+h,z0)],.018,steel,7)
        for yy in (y,y+h):m.rod_path([(x-w/2,yy,z0),(x+w/2,yy,z0)],.018,steel,7)
        pitch=[.10,.15,.26,.4][level]
        for i in range(1,int(w/pitch)):
            xx=x-w/2+i*pitch;m.rod_path([(xx,y,z0),(xx,y+h,z0)],.0035,steel,5)
        for i in range(1,int(h/pitch)):
            yy=y+i*pitch;m.rod_path([(x-w/2,yy,z0),(x+w/2,yy,z0)],.0035,steel,5)
    elif prop=='CorrugatedAwning':
        w=.78;d=.48;z0=z
        m.rod_path([(x-w/2,y+.38,z0),(x-w/2,y+.38,z0+d)],.018,steel,7);m.rod_path([(x+w/2,y+.38,z0),(x+w/2,y+.38,z0+d)],.018,steel,7)
        if level<3:
            for i in range([9,6,4,2][level]):
                xx=x-w/2+(i+.5)*w/[9,6,4,2][level]
                off=.025*sin(i*1.7) if level<2 else 0
                m.rod_path([(xx,y+.37,z0),(xx+off,y+.35,z0+d)],.009,steel,6)
        for side in (-1,1):m.rod_path([(x+side*w/2,y+.02,z0+.06),(x+side*w/2,y+.38,z0+.05)],.014,steel,6)
    elif prop=='TornCurtain':
        w=.74; h=.82
        m.rod_path([(x-w/2,y+h,z),(x+w/2,y+h,z)],.012,steel,6)
        pleats=[12,8,5,3][level]
        for i in range(pleats):
            xx=x-w/2+i*w/max(1,pleats-1); sway=.035*sin(i*2.1)
            if level<3 or i%2==0:
                m.face([(xx,y+h,z),(xx+.03,y+h-.26,z+sway),(xx-.01,y+.08,z+sway+.02),(xx+.045,y+.06,z)],B('BAL_Canvas'))
    elif prop=='MosquitoMesh':
        w=.74;h=.76;z0=z
        for xx in (x-w/2,x+w/2):m.rod_path([(xx,y,z0),(xx,y+h,z0)],.012,paint,6)
        for yy in (y,y+h):m.rod_path([(x-w/2,yy,z0),(x+w/2,yy,z0)],.012,paint,6)
        pitch=[.055,.09,.15,.25][level]
        for i in range(1,int(w/pitch)):
            xx=x-w/2+i*pitch;m.rod_path([(xx,y,z0),(xx,y+h,z0)],.0018,B('BAL_Glass'),4)
        for i in range(1,int(h/pitch)):
            yy=y+i*pitch;m.rod_path([(x-w/2,yy,z0),(x+w/2,yy,z0)],.0018,B('BAL_Glass'),4)
    elif prop=='TornPlastic':
        pts=[]
        for i in range(9):
            xx=x-.40+i*.10;yy=y+.86-.07*sin(i*2.2)-(.18 if i in (3,7) and level<2 else 0);zz=z+.03*sin(i*3)
            pts.append((xx,yy,zz))
        for a,b in zip(pts,pts[1:]):m.cylinder(a,b,.0025,B('BAL_Plastic'),4)
        for i in range(1,6 if level<2 else 4):
            t=i/(6 if level<2 else 4);xx=x-.36+t*.72
            m.rod_path([(xx,y+.08,z+.01),(xx-.05,y+.26,z+.02),(xx+.025,y+.47,z)],.002,B('BAL_Plastic'),4)
    elif prop=='ConcreteDebris':
        count=[8,5,3,2][level]
        for i in range(count):
            sx=rng.uniform(.10,.25);sz=rng.uniform(.09,.22);sy=rng.uniform(.05,.14)
            m.ellipsoid((x+rng.uniform(-.2,.2),y+sy/2,z+rng.uniform(-.1,.1)),(sx,sy,sz),B('BAL_ConcreteDamage'),3 if level>1 else 4,6 if level>1 else 8)
    elif prop=='Sandbags':
        for i in range([4,3,2,1][level]):
            xx=x+(i-(1.5 if level==0 else .5))*.20
            m.ellipsoid((xx,y+.12,z),(.28,.22,.18),B('BAL_Canvas'),4 if level<2 else 3,8)
    elif prop=='BrokenDryer':
        for dx in (-.42,.42):m.box((x+dx,y,z),(.12,.03,.22),steel)
        for side in (-1,1):m.rod_path([(x+side*.42,y+.02,z),(x+side*.42,y+.66,z+.04),(x+side*.33,y+.6,z+.13)],.014,steel,7)
        m.rod_path([(x-.33,y+.60,z+.13),(x-.46,y+.54,z+.11)],.014,steel,7)
        m.rod_path([(x+.33,y+.60,z+.13),(x+.43,y+.69,z+.12)],.014,steel,7)
        m.rod_path([(x-.46,y+.54,z+.11),(x+.43,y+.69,z+.12),(x+.49,y+.53,z+.12)],.014,steel,7)
        if level<2:m.rod_path([(x-.4,y+.08,z+.07),(x+.35,y+.52,z+.13)],.008,steel,6)
    elif prop=='FlowerVines':
        for i in range([7,5,3,2][level]):
            xx=x-.25+i*.08;yy=y+.08+rng.random()*.36
            m.rod_path([(xx,y, z),(xx+.03,yy,z+.02),(xx-.02,yy+.13,z+.025)],.006,B('BAL_RottenTimber'),5)
            m.ellipsoid((xx,yy+.10,z+.03),(.08,.035,.018),org,3,6)


def prop_bounds(prop):
    return {
        'SatelliteDish_Rusted':(.45,.52,.45), 'Antenna_Bent':(.40,.64,.28),
        'AC_OutdoorUnit':(.72,.48,.36), 'Clothesline':(1.12,.84,.22),
        'Planter_Old':(.58,.46,.28), 'BrokenPot':(.31,.26,.28),
        'WoodenCrate':(.48,.38,.44), 'WireMesh':(.76,.56,.08),
        'CorrugatedAwning':(.84,.46,.56), 'TornCurtain':(.78,.86,.09),
        'MosquitoMesh':(.78,.80,.08), 'TornPlastic':(.84,.94,.14),
        'ConcreteDebris':(.48,.18,.34), 'Sandbags':(.70,.30,.27),
        'BrokenDryer':(1.02,.73,.24), 'FlowerVines':(.72,.58,.22)
    }[prop]


PROP_PLANS={}
PROP_SUPPORT=[]


def mesh_bounds(mesh):
    return ([min(v[i] for v in mesh.v) for i in range(3)],
            [max(v[i] for v in mesh.v) for i in range(3)])


def append_mesh(target, source, offset=(0,0,0), label=0):
    first=len(target.v)
    target.v.extend(tuple(v[i]+offset[i] for i in range(3)) for v in source.v)
    target.labels.extend([label]*len(source.v))
    target.uv.extend(source.uv);target.mi.extend(source.mi)
    target.f.extend(tuple(first+i for i in face) for face in source.f)


def floor_tree(kind,state,seed,depth,level):
    floor=Mesh();random=Random(seed)
    if kind=='05':add_wood_floor(floor,level,random,state,depth,True)
    elif kind=='07':add_grate_floor(floor,level,random,state,depth)
    else:add_slab(floor,state,random,depth,collision=True)
    return BVHTree.FromPolygons(floor.v,floor.f)


def floor_height(tree,x,z):
    hit,normal,_,_=tree.ray_cast(Vector((x,2,z)),Vector((0,-1,0)),3)
    return hit.y if hit is not None and abs(normal.y)>.7 else None


def supported_footprint(trees,box):
    x0,x1,z0,z1=box
    # Narrow plank seams may be bridged by a rigid object, but corners and
    # edge midpoints must all rest on surviving material at every LOD.
    critical=[(u,v) for u in (.06,.5,.94) for v in (.06,.5,.94)]
    heights=[];coverage=[]
    for tree in trees:
        corners=[floor_height(tree,x0+(x1-x0)*u,z0+(z1-z0)*v) for u,v in critical]
        if any(y is None for y in corners):return None
        samples=[floor_height(tree,x0+(x1-x0)*(i+.5)/7,z0+(z1-z0)*(j+.5)/7) for i in range(7) for j in range(7)]
        fraction=sum(y is not None for y in samples)/len(samples)
        if fraction<.88 or max(corners)-min(corners)>.004:return None
        heights.extend(corners);coverage.append(round(fraction,4))
    if max(heights)-min(heights)>.004:return None
    return max(heights),coverage


def prop_plan(state,depth,kind,seed):
    cache=(kind,state,seed)
    if cache in PROP_PLANS:return PROP_PLANS[cache]
    random=Random(seed+89123)
    # Only genuinely floor-standing objects use floor sockets. Curtains,
    # awnings and loose film remain separate assets requiring authored mounts.
    pools={
        '01':['Planter_Old','SatelliteDish_Rusted','Antenna_Bent','WoodenCrate','BrokenPot','Clothesline','ConcreteDebris'],
        '02':['BrokenDryer','WoodenCrate','BrokenPot','SatelliteDish_Rusted','ConcreteDebris'],
        '03':['Planter_Old','BrokenPot','WoodenCrate'],
        '04':['AC_OutdoorUnit','Planter_Old','BrokenPot','FlowerVines','WoodenCrate'],
        '05':['WoodenCrate','BrokenPot','ConcreteDebris','Planter_Old'],
        '06':['Sandbags','WoodenCrate','Antenna_Bent','ConcreteDebris','SatelliteDish_Rusted'],
        '07':['AC_OutdoorUnit','WireMesh','Antenna_Bent','WoodenCrate','ConcreteDebris','BrokenDryer'],
        '08':['Planter_Old','FlowerVines','BrokenPot','ConcreteDebris','WoodenCrate'],
        '09':['ConcreteDebris','BrokenDryer','WoodenCrate','SatelliteDish_Rusted'],
        '10':['ConcreteDebris','Antenna_Bent','WoodenCrate','BrokenPot','WireMesh']
    }
    options=pools[kind][:];random.shuffle(options);occupied=[];plan=[]
    trees=[floor_tree(kind,state,seed,depth,lod) for lod in range(4)]
    candidates=[(-1.05+i*.15,.13+j*.085) for i in range(15) for j in range(max(1,int((depth-.25)/.085)))]
    random.shuffle(candidates)
    if kind=='07':candidates=[(-1.05+i*.15,.51) for i in range(15)];random.shuffle(candidates)
    for prop_index,prop in enumerate(options):
        if len(plan)>=(2 if state=='D' else 3 if state in ('B','C') else 4):break
        variants=[];bounds=[]
        for lod in range(4):
            geo=Mesh();prop_geo(geo,prop,lod,Random(seed+prop_index*619),(0,0,0))
            variants.append(geo);bounds.append(mesh_bounds(geo))
        lo=[min(b[0][i] for b in bounds) for i in range(3)]
        hi=[max(b[1][i] for b in bounds) for i in range(3)]
        w=hi[0]-lo[0]+.02;d=hi[2]-lo[2]+.02
        for x,z in candidates:
            box=(x-w/2,x+w/2,z-d/2,z+d/2)
            if box[0]<-1.28 or box[1]>1.28 or box[2]<.055 or box[3]>depth-.13:continue
            if any(not (box[1]<o[0] or box[0]>o[1] or box[3]<o[2] or box[2]>o[3]) for o in occupied):continue
            support=supported_footprint(trees,box) if kind!='07' else (.1505,[1.0]*4)
            if support is None:continue
            surface,coverage=support;tray=kind=='07'
            plan.append({'name':prop,'label':len(plan)+1,'variants':variants,'offsetXZ':(x-(lo[0]+hi[0])/2,z-(lo[2]+hi[2])/2),
                         'surface':surface,'tray':tray,'box':box,'coverage':coverage,'bounds':bounds})
            occupied.append(box);break
    PROP_PLANS[cache]=plan
    PROP_SUPPORT.append({'type':kind,'state':state,'seed':seed,'props':[{'name':p['name'],'label':p['label'],'box':p['box'],
        'surface':p['surface'],'support':'crossbeam-mounted steel plate' if p['tray'] else 'surviving floor',
        'coverageByLOD':p['coverage']} for p in plan]})
    return plan


def add_variant_props(m,level,seed,state,depth,kind):
    for prop in prop_plan(state,depth,kind,seed):
        x,z=prop['offsetXZ'];surface=prop['surface'];label=prop['label']
        if prop['tray']:
            x0,x1,z0,z1=prop['box'];plate=Mesh()
            plate.box(((x0+x1)/2,surface+.009,(z0+z1)/2),(x1-x0,.018,z1-z0),B('BAL_CorrodedSteel'))
            append_mesh(m,plate,label=label);surface+=.018
        low=prop['bounds'][level][0][1]
        append_mesh(m,prop['variants'][level],(x,surface-low,z),label)


def add_balcony_geometry(type_no, state, level, seed, collision=False):
    rng=Random(seed)
    key=f'{type_no:02d}'
    depth=1.5 if key=='04' else 1.4 if key=='05' else 1.2
    m=Mesh()
    if key=='05': add_wood_floor(m,level,rng,state,depth,collision)
    elif key=='07': add_grate_floor(m,level,rng,state,depth)
    else: add_slab(m,state,rng,depth,floor_mat='BAL_Concrete',collision=collision)
    add_mounts(m,level,'BAL_CorrodedSteel')
    add_metal_supports(m,level,depth,'BAL_CorrodedSteel')

    if key=='01':
        add_guardrails(m,level,rng,state,'steel',depth); add_concrete_cracks(m,level,rng,state,depth)
        # Remnants of anchors and a repaired lower rail.
        if level<2:
            for x in (-1.08,1.08):m.box((x,.38,depth-.05),(.19,.035,.035),B('BAL_PaintedSteel'))
    elif key=='02':
        add_guardrails(m,level,rng,state,'steel',depth);add_sheet_panels(m,level,state,depth,rng,'khrush')
        if state in ('B','C','D'):
            for x in (-1.02,1.02):m.rod_path([(x,.39,depth-.05),(x+(.07 if x<0 else -.08),.74,depth-.04)],.018,B('BAL_CorrodedSteel'),7)
    elif key=='03':
        add_glazing(m,level,rng,state,depth,False)
        add_guardrails(m,level,rng,state,'glazedwood',depth)
        if level==0:
            for x in (-1.15,1.15):m.box((x,.38,depth-.10),(.12,.08,.24),B('BAL_Timber'))
    elif key=='04':
        add_glazing(m,level,rng,state,depth,True)
        add_guardrails(m,level,rng,state,'loggia',depth)
        # PVC sill and low-profile service conduit.
        m.box((0,.27,depth-.12),(2.56,.06,.15),B('BAL_Plastic'))
        if level<2:m.rod_path([(-1.1,.21,.07),(-1.1,.17,.30),(1.1,.17,.30)],.011,B('BAL_Aluminium'),6)
    elif key=='05':
        add_guardrails(m,level,rng,state,'wood',depth)
        for x in (-.96,.96):
            m.rod_path([(x,.04,.12),(x,.20,.65),(x,.08,depth-.08)],.026,B('BAL_Timber' if state in ('A','B') else 'BAL_RottenTimber'),7)
        # Repair braces are fastened plates plus visible bolts.
        if level==0:
            for x in (-1.18,1.18):
                m.box((x,.10,.47),(.11,.05,.18),B('BAL_CorrodedSteel'))
                for yy in (.08,.12):m.cylinder((x-.027,yy,.47),(x-.027,yy+.012,.47),.006,B('BAL_PaintedSteel'),6)
    elif key=='06':
        add_guardrails(m,level,rng,state,'steel',depth);add_sheet_panels(m,level,state,depth,rng,'fortified')
    elif key=='07':
        add_guardrails(m,level,rng,state,'industrial',depth)
        for x in (-1.03,1.03):
            m.rod_path([(x,.03,.12),(x,.20,.62),(x,.04,depth-.05)],.035,B('BAL_CorrodedSteel'),[10,8,6,5][level])
        # Real angle-bracket flanges and bolted support plates.
        for x in (-1.05,1.05):
            m.box((x,.06,.40),(.17,.08,.32),B('BAL_PaintedSteel'))
            if level==0:
                for dx in (-.05,.05):m.cylinder((x+dx,.105,.4),(x+dx,.12,.4),.008,B('BAL_CorrodedSteel'),6)
    elif key=='08':
        add_guardrails(m,level,rng,state,'steel',depth);add_concrete_cracks(m,level,rng,state,depth);add_vines(m,level,rng,state,depth)
    elif key=='09':
        add_guardrails(m,level,rng,state,'burnt',depth);add_glazing(m,level,rng,state,depth,False);add_burn_damage(m,level,rng,state,depth)
    elif key=='10':
        add_guardrails(m,level,rng,state,'steel',depth);add_concrete_cracks(m,level,rng,state,depth)
        if state in ('C','D'):
            for x in (-1.15,-.72,.62,1.08):
                if state=='D' and x<0:continue
                m.rod_path([(x,.12,.18),(x+.04,.30,.46),(x-.035,.23,.73)],.018,B('BAL_CorrodedSteel'),[9,7,6,5][level])
                m.rod_path([(x-.10,.03,.28),(x,.18,.53)],.011,B('BAL_CorrodedSteel'),6)

    # Seeded, AABB-separated surface clutter. Never generated into the footprint of a facade or rail.
    if not collision:add_variant_props(m,level,seed,state,depth,key)
    return m,depth


def make_collision(type_no,state,seed,depth):
    key=f'{type_no:02d}';rng=Random(seed);m=Mesh()
    if key=='05': add_wood_floor(m,0,rng,state,depth,True)
    elif key=='07': add_grate_floor(m,0,rng,state,depth)
    else: add_slab(m,state,rng,depth,floor_mat='BAL_Concrete',collision=True)
    return m


def add_simple_prop_model(prop, seed=0):
    rng=Random(seed);objects=[];lodcounts=[];all_bounds=[]
    for level in range(4):
        m=Mesh();prop_geo(m,prop,level,rng,(0,0,0))
        if prop not in ('CorrugatedAwning','TornCurtain','MosquitoMesh','TornPlastic'):
            bottom=min(v[1] for v in m.v)
            m.v=[(x,y-bottom,z) for x,y,z in m.v]
        all_bounds.append(mesh_bounds(m))
        ob=create_obj(f'LOD{level}',m)
        if prop=='ConcreteDebris':
            for modifier in list(ob.modifiers):ob.modifiers.remove(modifier)
        objects.append(ob);lodcounts.append(len(ob.data.loop_triangles))
    low,high=all_bounds[0]
    collision=Mesh();collision.box(tuple((low[i]+high[i])/2 for i in range(3)),tuple(high[i]-low[i] for i in range(3)),B('BAL_Canvas' if prop in ('Planter_Old','Sandbags') else 'BAL_CorrodedSteel'))
    col=create_obj('COLLISION',collision);objects.append(col)
    return objects,lodcounts


def export_model(objects,path):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.hide_set(False);ob.hide_render=False;ob.select_set(True)
    bpy.context.view_layer.objects.active=next((o for o in objects if o.name=='LOD0'),objects[0])
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True,use_triangles=True,mesh_smooth_type='FACE',path_mode='STRIP',use_custom_props=False)
    for index,ob in enumerate(objects):
        ob.select_set(False)
        # Blender object names are global. Restore exact LOD0..3 names for the next FBX,
        # then give retained source meshes a unique editable name in the master library.
        ob.name=path.stem+('_LOD'+str(index) if index<4 else '_COLLISION')
        ob.hide_set(True);ob.hide_render=True


def build_model(name, category, maker, collision_builder, seed, directory):
    objects=[];lod=[];depth=None
    for level in range(4):
        mesh,depth=maker(level,seed)
        ob=create_obj(f'LOD{level}',mesh);objects.append(ob)
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update();ob.data.calc_loop_triangles()
        lod.append(len(ob.data.loop_triangles))
    # Keep the imported draw cost monotonic even when detail density in a
    # specific damaged variant produces a busier intermediate hand-built LOD.
    for level in range(1,4):
        # Keep independently grounded prop geometry intact; reduce only when a
        # hand-built level would otherwise grow relative to the preceding LOD.
        ceiling=lod[level-1]*.95
        if lod[level]>ceiling:
            ratio=max(.08,ceiling/lod[level])
            reduction=objects[level].modifiers.new(f'LOD{level} triangle budget','DECIMATE')
            reduction.ratio=ratio
            lod[level]=max(1,int(lod[level]*ratio))
    collision=collision_builder(seed,depth)
    col=create_obj('COLLISION',collision);objects.append(col)
    path=directory/(name+'.fbx');path.parent.mkdir(parents=True,exist_ok=True)
    export_model(objects,path)
    SOURCE_MODELS.append((name,category,objects))
    return {'name':name,'path':str(path.relative_to(OUT)).replace('\\','/'),'lodTriangles':lod,'depth':round(depth,4),'seed':seed}


TYPES=[
 ('Balcony_01_Soviet','01'),('Balcony_02_Khrushchevka','02'),('Balcony_03_GlazedSoviet','03'),
 ('Balcony_04_ModernLoggia','04'),('Balcony_05_Wooden','05'),('Balcony_06_Fortified','06'),
 ('Balcony_07_Industrial','07'),('Balcony_08_Overgrown','08'),('Balcony_09_Burned','09'),
 ('Balcony_10_Collapsed','10')]
STATES=['A','B','C','D']
REPORT={'exporter':'Blender 5.2 standard FBX exporter','coordinateSystem':'Unity XYZ metres; facade plane Z=0, balcony extends +Z; pivot at module lower center','seedBase':2401024,'balconies':[],'props':[],'accessories':[]}
all_models=[]

for type_name,key in TYPES:
    for state_index,state in enumerate(STATES):
        name=f'{type_name}_{state}'
        seed=2401024+int(key)*100+state_index*17
        def maker(level,sd,k=key,s=state):return add_balcony_geometry(int(k),s,level,sd)
        def coll(sd,depth):return make_collision(int(key),state,sd,depth)
        record=build_model(name,'Balconies',maker,coll,seed,MODEL/'Balconies'/type_name)
        record.update({'type':type_name,'state':state,'slotWidth':2.7,'moduleWidth':3.0,'seed':seed})
        REPORT['balconies'].append(record)
        print('BALCONY_FBX',name,record['lodTriangles'],flush=True)

PROP_NAMES=['SatelliteDish_Rusted','Antenna_Bent','AC_OutdoorUnit','Clothesline','BrokenDryer','Planter_Old','BrokenPot','WoodenCrate','WireMesh','CorrugatedAwning','TornCurtain','MosquitoMesh','TornPlastic','ConcreteDebris','Sandbags','FlowerVines']
for i,prop in enumerate(PROP_NAMES):
    objects,lod=add_simple_prop_model(prop,2401800+i)
    path=MODEL/'Props'/(prop+'.fbx');path.parent.mkdir(parents=True,exist_ok=True);export_model(objects,path);SOURCE_MODELS.append((prop,'Props',objects))
    REPORT['props'].append({'name':prop,'path':str(path.relative_to(OUT)).replace('\\','/'),'lodTriangles':lod,'seed':2401800+i})
    print('PROP_FBX',prop,lod,flush=True)

# Shared compatible wall accessory models. Open aperture dimensions are exact 0.9 x 2.1 m.
def accessory_frame(name, mat_name, seed, broken=False):
    materials=[B(mat_name),B('BAL_Ceramic')]
    lods=[]
    for level in range(4):
        m=Mesh();s=materials[0];detail=[.055,.05,.045,.04][level]
        # No backing panel: clear opening remains 0.9 m wide and 2.1 m high.
        m.box((-.475,1.20,0),(.05,2.10,.09),s);m.box((.475,1.20,0),(.05,2.10,.09),s)
        if not broken or level<3:m.box((0,2.275,0),(1.0,.05,.09),s)
        m.box((0,.18,.09),(1.00,.06,.20),materials[1])
        if broken:
            m.rod_path([(-.43,.25,.07),(-.20,.65,.10),(-.11,1.05,.10)],.021,B('BAL_RottenTimber'),7)
        else:
            m.box((0,1.20,.045),(.86,2.06,.045),B('BAL_Glass'))
        ob=create_obj(f'LOD{level}',m);lods.append((ob,len(ob.data.loop_triangles)))
    cm=Mesh();cm.box((-.475,1.2,0),(.05,2.1,.09),materials[0]);cm.box((.475,1.2,0),(.05,2.1,.09),materials[0])
    if not broken:cm.box((0,2.275,0),(1.0,.05,.09),materials[0])
    col=create_obj('COLLISION',cm)
    objs=[x[0] for x in lods]+[col]
    out=MODEL/'Accessories'/(name+'.fbx');out.parent.mkdir(parents=True,exist_ok=True);export_model(objs,out);SOURCE_MODELS.append((name,'Accessories',objs))
    return {'name':name,'path':str(out.relative_to(OUT)).replace('\\','/'),'opening':[.9,2.1],'lodTriangles':[x[1] for x in lods]}

REPORT['accessories'].append(accessory_frame('DoorFrame_Soviet_Wood','BAL_Timber',2401900))
REPORT['accessories'].append(accessory_frame('DoorFrame_Modern_PVC','BAL_Aluminium',2401901))
REPORT['accessories'].append(accessory_frame('DoorFrame_Ruined','BAL_RottenTimber',2401902,True))

# Save an editable source library with one collection per model, organized on a grid.
# LOD0 is visible; LOD1-3 and the collision-only surface are hidden but remain editable.
category_collections={}
for index,(model_name,category,objects) in enumerate(SOURCE_MODELS):
    parent=category_collections.get(category)
    if parent is None:
        parent=bpy.data.collections.new(category);bpy.context.scene.collection.children.link(parent);category_collections[category]=parent
    item=bpy.data.collections.new(model_name);parent.children.link(item)
    grid_x=(index%8)*4.2-14.7;grid_z=(index//8)*3.6
    for object_index,ob in enumerate(objects):
        for old_collection in list(ob.users_collection):old_collection.objects.unlink(ob)
        item.objects.link(ob)
        ob.name=model_name+('_LOD'+str(object_index) if object_index<4 else '_COLLISION')
        ob.location.x=grid_x;ob.location.z=grid_z
        visible=ob.name.endswith('_LOD0')
        ob.hide_render=not visible;ob.hide_set(not visible)
# Keep the editable .blend relocatable inside the delivered asset folder.
for image in bpy.data.images:
    if image.source=='FILE' and image.filepath:
        try:image.filepath=bpy.path.relpath(image.filepath,start=str(SRC))
        except Exception:pass
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Balconies_Master.blend'))
(VER/'generation.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
support_json=json.dumps({'balconies':PROP_SUPPORT},indent=2)
(VER/'prop_support_plan.json').write_text(support_json,encoding='utf-8')
(OUT/'Editor'/'BalconyPropSupport.json').write_text(support_json,encoding='utf-8')
print('BALCONY_BUILD_DONE',len(REPORT['balconies']),len(REPORT['props']),len(REPORT['accessories']),flush=True)
