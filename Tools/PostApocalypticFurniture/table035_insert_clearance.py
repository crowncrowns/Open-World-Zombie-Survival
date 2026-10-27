"""Full-volume OBB and actual bracket contact proof for table 035's insert."""
import sys,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from mathutils import Vector,Euler
from mathutils.bvhtree import BVHTree
from models_01_10 import build

g=build(dict(id=35,variant=4,category='Tables',name='Table',slug='F035'))

def box(c,delta=(0,0,0)):
    rotation=Euler(tuple(math.radians(v)for v in c.get('rotation',(0,0,0))),'XYZ').to_matrix()
    return Vector(c['center'])+Vector(delta),[rotation.col[i].normalized()for i in range(3)],[v*.5 for v in c['size']]

def obb_overlap(a,b):
    ac,aa,ae=a;bc,ba,be=b;difference=bc-ac
    axes=aa+ba+[a.cross(b)for a in aa for b in ba]
    for axis in axes:
        if axis.length<1e-9:continue
        axis=axis.normalized()
        distance=abs(difference.dot(axis))
        ar=sum(ae[i]*abs(axis.dot(aa[i]))for i in range(3))
        br=sum(be[i]*abs(axis.dot(ba[i]))for i in range(3))
        if distance>ar+br+1e-7:return False
    return True

leaf=next(c for c in g.colliders if c['part']=='ExtensionLeaf')
body=[c for c in g.colliders if c['part']=='Body'and 'size'in c]
tops=[c for c in g.colliders if c['part'].startswith('SlidingTop_')]
obstacles=[('Body',box(c))for c in body]
for c in tops:
    p=g.parts[c['part']];delta=Vector(p['axis'])*p['amount']
    obstacles.append((c['part'],box(c,delta)))
samples=[]
for step in range(21):
    t=step/20;delta=Vector(g.parts['ExtensionLeaf']['axis'])*(g.parts['ExtensionLeaf']['amount']*t)
    moving=box(leaf,delta)
    hits=[name for name,obstacle in obstacles if obb_overlap(moving,obstacle)]
    samples.append(dict(fraction=t,overlap_count=len(hits),overlap_parts=hits))

def tree(mesh,delta=(0,0,0)):
    faces=[(f[0],f[j],f[j+1])for f in mesh['f']for j in range(1,len(f)-1)]
    return BVHTree.FromPolygons([Vector(v)+Vector(delta)for v in mesh['v']],faces,all_triangles=True,epsilon=.0001)

rails=[];brackets=[]
for i,m in enumerate(g.meshes):
    if m['part']!='Body' or m['mat']!='Steel':continue
    lo=[min(v[k]for v in m['v'])for k in range(3)];hi=[max(v[k]for v in m['v'])for k in range(3)]
    if hi[0]-lo[0]>1:rails.append((i,m))
    else:brackets.append((i,m))
joins=[]
for idx,m in brackets:
    bvh=tree(m)
    rail_contacts=[i for i,rail in rails if bvh.overlap(tree(rail))]
    top_contacts={}
    for state in (0,1):
        hits=[]
        for top in [m for m in g.meshes if m['part'].startswith('SlidingTop_')]:
            p=g.parts[top['part']];delta=Vector(p['axis'])*(p['amount']*state)
            if bvh.overlap(tree(top,delta)):hits.append(top['part'])
        top_contacts[str(state)]=hits
    joins.append(dict(bracket_mesh=idx,rail_contacts=rail_contacts,top_contacts=top_contacts))

leaf_top=leaf['center'][1]+leaf['size'][1]*.5+g.parts['ExtensionLeaf']['amount']
main_top=tops[0]['center'][1]+tops[0]['size'][1]*.5
report=dict(id=35,kind='source_full_volume_insert_clearance',samples=samples,
            bracket_joins=joins,insert_top_y=leaf_top,main_top_y=main_top,
            top_height_difference=leaf_top-main_top,
            minimum_leaf_rail_z_clearance=.378-.022*.5-.72*.5,
            passed=not any(s['overlap_count']for s in samples)and abs(leaf_top-main_top)<1e-6 and
                   len(joins)==4 and all(j['rail_contacts']and all(j['top_contacts'].values())for j in joins))
dest=ROOT/'Verification/PostApocalypticFurniture/table035_insert_clearance.json'
dest.write_text(json.dumps(report,indent=2),encoding='utf-8')
print('TABLE035_INSERT_PROOF',json.dumps(report),flush=True)
