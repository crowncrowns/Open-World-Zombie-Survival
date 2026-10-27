"""Geometry-level preflight; not a Unity or visual acceptance report."""
import sys, json, math
from pathlib import Path
from mathutils import Vector, Euler
from mathutils.bvhtree import BVHTree
sys.path.insert(0,str(Path(__file__).resolve().parent))
from models_11_20 import build

def box_overlap(zone,col,epsilon=.001):
    ac=Vector(zone['center']);bc=Vector(col['center'])
    ae=Vector(zone['size'])*.5;be=Vector(col['size'])*.5
    axes=[Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1))]
    matrix=Euler(tuple(math.radians(x) for x in col.get('rotation',[0,0,0])),'XYZ').to_matrix()
    baxes=[matrix.col[k].copy() for k in range(3)]
    test=axes+baxes+[a.cross(b) for a in axes for b in baxes]
    delta=bc-ac
    for n in test:
        if n.length<1e-8:continue
        n.normalize()
        ra=sum(ae[k]*abs(n.dot(axes[k])) for k in range(3))
        rb=sum(be[k]*abs(n.dot(baxes[k])) for k in range(3))
        if abs(delta.dot(n))>=ra+rb-epsilon:return False
    return True


def capsule_overlap(zone,col,epsilon=.001):
    a=Vector(col['a']);b=Vector(col['b']);v=b-a
    lo=Vector(zone['center'])-Vector(zone['size'])*.5
    hi=Vector(zone['center'])+Vector(zone['size'])*.5
    ts=[0.0,1.0]
    for k in range(3):
        if abs(v[k])>1e-10:
            for edge in [lo[k],hi[k]]:
                t=(edge-a[k])/v[k]
                if 0<t<1:ts.append(t)
    ts.sort();best=1e30
    def distance(t):
        p=a+v*t
        return sum(max(lo[k]-p[k],0,p[k]-hi[k])**2 for k in range(3))
    for x,y in zip(ts,ts[1:]):
        mid=(x+y)/2;den=0;num=0
        for k in range(3):
            val=a[k]+v[k]*mid
            if val<lo[k]:edge=lo[k]
            elif val>hi[k]:edge=hi[k]
            else:continue
            den+=v[k]**2;num+=v[k]*(a[k]-edge)
        t=max(x,min(y,-num/den)) if den else mid
        best=min(best,distance(x),distance(y),distance(t))
    return best<max(0,col['radius']-epsilon)**2


report=[]
def mesh_bvh(g,part):
    vertices=[];faces=[]
    for m in g.meshes:
        if m['part']!=part:continue
        offset=len(vertices);vertices+=m['v'];faces += [tuple(offset+j for j in f) for f in m['f']]
    return BVHTree.FromPolygons(vertices,faces,all_triangles=False,epsilon=.0001)


for i in range(101,201):
    row=dict(id=i,variant=(i-1)%10,category='',name='',slug='')
    g=build(row)
    verts=[v for m in g.meshes for v in m['v']]
    tris=sum(sum(len(f)-2 for f in m['f']) for m in g.meshes)
    issues=[]
    if not verts:issues.append('empty')
    if not all(math.isfinite(x) for v in verts for x in v):issues.append('nonfinite')
    if tris>18000:issues.append('triangle_budget')
    if not g.colliders:issues.append('no_primitive_colliders')
    for m in g.meshes:
        if any(any(q<0 or q>=len(m['v']) for q in f) for f in m['f']):issues.append('bad_face')
    mins=[min(v[k] for v in verts) for k in range(3)]
    maxs=[max(v[k] for v in verts) for k in range(3)]
    if min(maxs[k]-mins[k] for k in range(3))<.002:issues.append('zero_dimension')
    if any(c.get('size') and min(c['size'])<=0 for c in g.colliders):issues.append('invalid_collider')
    if any(min(z['size'])<=0 for z in g.zones):issues.append('invalid_zone')
    overlaps=[]
    for zi,z in enumerate(g.zones):
        for ci,c in enumerate(g.colliders):
            hit=capsule_overlap(z,c) if c.get('type')=='capsule' else box_overlap(z,c)
            if hit:overlaps.append(dict(zone=z['name'],collider=ci,part=c['part']))
    if overlaps:issues.append('zone_geometry_overlap')
    support_contacts=None
    if i in [121,158]:
        moving='HobLid' if i==121 else 'Receiver'
        support_contacts=len(mesh_bvh(g,'Body').overlap(mesh_bvh(g,moving)))
        if support_contacts==0:issues.append('no_mesh_support_contact')
    cavity_checks=[]
    cavity_probes={
        144:[('BatteryCover',(0,.105,-.058),(0,0,1),.012)],
        145:[('CassetteDoor',(.065,.153,.005),(0,-1,0),.020)],
        146:[('CassetteDoor%d'%k,(x,.125,.088),(0,0,-1),.020) for k,x in enumerate([-.0675,.0675])],
        147:[('CassetteDoor%d'%k,(x,.10,.168),(0,0,-1),.018) for k,x in enumerate([-.06,.06])]+[('CDTray',(0,.355,.168),(0,0,-1),.020)]
    }
    if i in cavity_probes:
        bvh=mesh_bvh(g,'Body')
        for name,origin,direction,minimum in cavity_probes[i]:
            hit,normal,index,distance=bvh.ray_cast(Vector(origin),Vector(direction),1.0)
            passed=hit is not None and distance>=minimum
            cavity_checks.append(dict(part=name,firstBodySurfaceDistance=distance,minimumDepth=minimum,passed=passed))
            if not passed:issues.append('blocked_or_empty_cavity_'+name)
    if i in range(131,141) and not any(p['kind']=='hinge' for p in g.parts.values()):issues.append('no_fridge_door')
    if i in range(141,151):
        if not any(d['kind']=='radio' for d in g.devices):issues.append('no_radio_device')
        for needed in ['power','channel','volume','tune']:
            if not any(p['kind']==needed for p in g.parts.values()):issues.append('missing_radio_'+needed)
    report.append(dict(id=i,triangles=tris,meshPieces=len(g.meshes),parts=len(g.parts),colliders=len(g.colliders),zones=len(g.zones),boundsMin=mins,boundsMax=maxs,issues=issues,zoneOverlaps=overlaps,supportMeshContactPairs=support_contacts,cavityRayChecks=cavity_checks))
out=Path(__file__).resolve().parents[2]/'Verification'/'PostApocalypticFurniture'/'preflight_11_20.json'
out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(report,indent=2),encoding='utf-8')
print('FURNITURE_101_200_PREFLIGHT',len(report),'MAX_TRIANGLES',max(r['triangles'] for r in report),'ISSUES',[(r['id'],r['issues']) for r in report if r['issues']])
print('TOP_TRIANGLES',sorted([(r['id'],r['triangles']) for r in report],key=lambda q:q[1],reverse=True)[:12])
