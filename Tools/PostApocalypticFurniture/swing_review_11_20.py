"""Read-only source sweep of moving primitive proxies against fixed boards.

Closed-pose contact pairs are excluded from the sweep and enumerated separately;
this avoids claiming hinge/barrel and gasket contact as a motion regression.
This audit is supplementary to, not a replacement for, imported Unity tests.
"""
import sys,json,math
from pathlib import Path
from mathutils import Vector,Quaternion,Euler
sys.path.insert(0,str(Path(__file__).resolve().parent))
from models_11_20 import build
from preflight_11_20 import box_overlap,capsule_overlap


def to_box(c):
    if c.get('type')!='capsule':return c
    a,b=Vector(c['a']),Vector(c['b']);v=b-a
    rot=Vector((0,1,0)).rotation_difference(v.normalized()).to_euler('XYZ')
    return dict(part=c['part'],center=list((a+b)/2),size=[c['radius']*2,v.length+c['radius']*2,c['radius']*2],rotation=[math.degrees(x) for x in rot])


def moved(c,p,f):
    if c.get('type')=='capsule':
        out=dict(c);pivot=Vector(p['pivot']);axis=Vector(p['axis'])
        if p['kind'] in ['hinge','rotate']:
            q=Quaternion(axis,math.radians(p['amount']*f))
            for key in ['a','b']:out[key]=list(pivot+q@(Vector(c[key])-pivot))
        else:
            for key in ['a','b']:out[key]=list(Vector(c[key])+axis*(p['amount']*f))
        return out
    c=to_box(c);out=dict(c);pivot=Vector(p['pivot']);axis=Vector(p['axis'])
    center=Vector(c['center'])
    if p['kind'] in ['hinge','rotate']:
        q=Quaternion(axis,math.radians(p['amount']*f));center=pivot+q@(center-pivot)
        e=Euler(tuple(math.radians(v) for v in c['rotation']),'XYZ').to_quaternion()
        out['rotation']=[math.degrees(v) for v in (q@e).to_euler('XYZ')]
    else:center+=axis*(p['amount']*f)
    out['center']=list(center)
    return out


def primitives_overlap(a,b,epsilon=.005):
    ac=a.get('type')=='capsule';bc=b.get('type')=='capsule'
    if ac and not bc:return primitives_overlap(b,a,epsilon)
    if ac and bc:
        # Closest points between two line segments; clamp both finite ends.
        p,q=Vector(a['a']),Vector(b['a']);u=Vector(a['b'])-p;v=Vector(b['b'])-q;w=p-q
        aa,bb,cc,dd,ee=u.dot(u),u.dot(v),v.dot(v),u.dot(w),v.dot(w)
        den=aa*cc-bb*bb
        s=max(0,min(1,(bb*ee-cc*dd)/den)) if den>1e-12 else 0
        t=(bb*s+ee)/cc if cc>1e-12 else 0
        if t<0:t=0;s=max(0,min(1,-dd/aa)) if aa>1e-12 else 0
        elif t>1:t=1;s=max(0,min(1,(bb-dd)/aa)) if aa>1e-12 else 0
        return (w+u*s-v*t).length<a['radius']+b['radius']-epsilon
    q=Euler(tuple(math.radians(v) for v in a.get('rotation',[0,0,0])),'XYZ').to_quaternion()
    inv=q.conjugated();local=dict(b);center=Vector(a['center'])
    zone=dict(center=[0,0,0],size=a['size'])
    if bc:
        for key in ['a','b']:local[key]=list(inv@(Vector(b[key])-center))
        return capsule_overlap(zone,local,epsilon)
    local['center']=list(inv@(Vector(b['center'])-center))
    bq=Euler(tuple(math.radians(v) for v in b.get('rotation',[0,0,0])),'XYZ').to_quaternion()
    local['rotation']=[math.degrees(v) for v in (inv@bq).to_euler('XYZ')]
    return box_overlap(zone,local,epsilon)


report=[]
for i in range(101,201):
    g=build(dict(id=i,variant=(i-1)%10,category='',name='',slug=''))
    fixed=[(j,c) for j,c in enumerate(g.colliders) if c['part']=='Body']
    problems=[];initial=[];below=[];geometric_below=[];missing=[];tested=0
    for name,p in g.parts.items():
        if p['kind'] not in ['hinge','slide','lift','rotate']:continue
        vertices=[Vector(v) for m in g.meshes if m['part']==name for v in m['v']]
        # Exact rendered vertices use a strict floor threshold; unlike the
        # conservative capsule proxy audit, this has no 25 mm tolerance.
        pivot=Vector(p['pivot']);axis=Vector(p['axis'])
        for step in range(21):
            f=step/20
            if p['kind'] in ['hinge','rotate']:
                q=Quaternion(axis,math.radians(p['amount']*f))
                low=min((pivot+q@(v-pivot)).y for v in vertices)
            else:low=min(v.y+axis.y*p['amount']*f for v in vertices)
            if low<-.00001:geometric_below.append(dict(part=name,fraction=f,bottomY=round(low,7)))
        cols=[(j,c) for j,c in enumerate(g.colliders) if c['part']==name]
        if not cols:missing.append(name)
        for ci,c in cols:
            baseline={j for j,b in fixed if primitives_overlap(c,b,epsilon=.005)}
            if baseline:initial.append(dict(part=name,collider=ci,bodyColliders=sorted(baseline)))
            found=set()
            for step in range(1,21):
                f=step/20;mb=moved(c,p,f);tested+=1
                if mb.get('type')=='capsule':low=min(mb['a'][1],mb['b'][1])-mb['radius']
                else:
                    mat=Euler(tuple(math.radians(v) for v in mb['rotation']),'XYZ').to_matrix()
                    ext=sum(abs(mat[1][k])*mb['size'][k]*.5 for k in range(3))
                    low=mb['center'][1]-ext
                if low<-.025:below.append(dict(part=name,fraction=f,bottomY=round(low,5)))
                for j,b in fixed:
                    if j in baseline or j in found:continue
                    if primitives_overlap(mb,b,epsilon=.005):
                        found.add(j);problems.append(dict(part=name,collider=ci,bodyCollider=j,firstFraction=f))
    # Keep one below-floor extreme per part, not twenty repetitive entries.
    extremes={}
    for b in below:
        if b['part'] not in extremes or b['bottomY']<extremes[b['part']]['bottomY']:extremes[b['part']]=b
    geometric_extremes={}
    for b in geometric_below:
        if b['part'] not in geometric_extremes or b['bottomY']<geometric_extremes[b['part']]['bottomY']:geometric_extremes[b['part']]=b
    report.append(dict(id=i,primitivePosesTested=tested,newBodyOverlapPairs=problems,initialBodyContactPairs=initial,belowLocalFloor=list(extremes.values()),belowExactMeshFloor=list(geometric_extremes.values()),mechanismsWithoutPrimitiveCollider=missing))
out=Path(__file__).resolve().parents[2]/'Verification'/'PostApocalypticFurniture'/'swing_review_11_20.json'
out.write_text(json.dumps(report,indent=2),encoding='utf-8')
print('FURNITURE_101_200_SWEEP',sum(r['primitivePosesTested'] for r in report),'POSES')
print('NEW_PAIRS',[(r['id'],len(r['newBodyOverlapPairs'])) for r in report if r['newBodyOverlapPairs']])
print('MISSING_COLLIDERS',[(r['id'],r['mechanismsWithoutPrimitiveCollider']) for r in report if r['mechanismsWithoutPrimitiveCollider']])
print('BELOW_LOCAL_FLOOR',[(r['id'],r['belowLocalFloor']) for r in report if r['belowLocalFloor']])
print('BELOW_EXACT_MESH_FLOOR',[(r['id'],r['belowExactMeshFloor']) for r in report if r['belowExactMeshFloor']])
