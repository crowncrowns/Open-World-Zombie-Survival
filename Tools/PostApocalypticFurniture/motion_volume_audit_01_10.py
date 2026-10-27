"""Full source OBB motion volumes against each model's static Body.

Box/box tests use all 15 SAT axes. Capsule shapes use their enclosing oriented
boxes, so capsule findings are conservative candidates for native confirmation.
The closed contact depth plus 2 mm is allowed, matching the native diagnostic.
"""
import sys,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from mathutils import Vector,Matrix,Euler
from models_01_10 import build


def shape(c):
    if c.get('type')=='capsule':
        a,b=Vector(c['a']),Vector(c['b']);axis=(b-a).normalized()
        x=axis.cross(Vector((0,1,0)))
        if x.length<.01:x=axis.cross(Vector((1,0,0)))
        x.normalize();z=x.cross(axis).normalized()
        return ((a+b)*.5,[x,axis,z],[c['radius'],(b-a).length*.5,c['radius']],True)
    rotation=Euler(tuple(math.radians(v)for v in c.get('rotation',(0,0,0))),'XYZ').to_matrix()
    return (Vector(c['center']),[rotation.col[i].normalized()for i in range(3)],[v*.5 for v in c['size']],False)


def penetration(a,b):
    ac,aa,ae,_=a;bc,ba,be,_=b;difference=bc-ac
    for k in range(3):
        if abs(difference[k])>=sum(ae[i]*abs(aa[i][k])+be[i]*abs(ba[i][k])for i in range(3))-1e-8:return 0
    minimum=1e9
    for axis in aa+ba+[x.cross(y)for x in aa for y in ba]:
        length=axis.length
        if length<1e-8:continue
        axis=axis/length
        depth=sum(ae[i]*abs(axis.dot(aa[i]))+be[i]*abs(axis.dot(ba[i]))for i in range(3))-abs(difference.dot(axis))
        if depth<=1e-8:return 0
        minimum=min(minimum,depth)
    return minimum


def moved(s,p,t):
    center,axes,half,cap=s;axis=Vector(p['axis']);axis.normalize()
    if p['kind']in('hinge','rotate'):
        rotation=Matrix.Rotation(math.radians(p['amount']*t),3,axis);pivot=Vector(p['pivot'])
        return pivot+rotation@(center-pivot),[rotation@v for v in axes],half,cap
    return center+axis*(p['amount']*t),axes,half,cap


report={'kind':'full_source_OBB_body_baseline_diagnostic','samples_per_mechanism':21,
        'closed_baseline_extra_metres':.002,'models':100,'mechanisms':0,'findings':[]}
for aid in range(1,101):
    g=build(dict(id=aid,variant=(aid-1)%10,category='test',name=str(aid),slug=str(aid)))
    bodies=[(c,shape(c))for c in g.colliders if c['part']=='Body']
    for name,p in g.parts.items():
        if p['kind']not in('hinge','rotate','slide','lift'):continue
        report['mechanisms']+=1
        moves=[(c,shape(c))for c in g.colliders if c['part']==name]
        baseline={(a,b):penetration(s,bs)for a,(_,s)in enumerate(moves)for b,(_,bs)in enumerate(bodies)}
        worst={}
        for step in range(1,21):
            t=step/20
            for a,(mc,s)in enumerate(moves):
                transformed=moved(s,p,t)
                for b,(bc,bs)in enumerate(bodies):
                    depth=penetration(transformed,bs)
                    if depth<=baseline[a,b]+.002001:continue
                    old=worst.get((a,b))
                    if old and old['depth']>=depth:continue
                    worst[a,b]=dict(id=aid,part=name,moving_index=a,body_index=b,fraction=t,
                                    depth=depth,closed_depth=baseline[a,b],capsule_bounding_box=s[3]or bs[3],
                                    moving=mc,body=bc)
        report['findings'].extend(worst.values())
report['summary']={'mechanisms':report['mechanisms'],'findings':len(report['findings']),
                   'exact_box_box':sum(not f['capsule_bounding_box']for f in report['findings']),
                   'conservative_capsule_box':sum(f['capsule_bounding_box']for f in report['findings'])}
dest=ROOT/'Verification/PostApocalypticFurniture/source_motion_volumes_01_10.json'
dest.write_text(json.dumps(report,indent=2),encoding='utf-8')
print('SOURCE_FULL_VOLUME_SUMMARY',report['summary'],flush=True)
for f in report['findings']:print('SOURCE_VOLUME_CANDIDATE',f['id'],f['part'],f['moving_index'],f['body_index'],round(f['depth'],5),round(f['closed_depth'],5),'capsuleBound',f['capsule_bounding_box'],flush=True)
