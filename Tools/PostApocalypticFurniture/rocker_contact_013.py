"""Read-only actual-mesh runner/floor and frame/runner proof for model 013."""
import sys,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from models_01_10 import build

g=build(dict(id=13,variant=2,category='Armchairs',name='Rocking chair',slug='F013'))
p=g.parts['RockingFrame'];pivot=Vector(p['pivot']);vertices=[Vector(v)for m in g.meshes for v in m['v']]
samples=[]
for step in range(41):
    amount=10*step/40
    mat=Matrix.Rotation(math.radians(amount),3,Vector((1,0,0)))
    minimum=min((pivot+mat@(v-pivot)).y for v in vertices)
    samples.append(dict(degrees=amount,minimum_y=minimum))

def tree(mesh):
    faces=[]
    for f in mesh['f']:
        faces.extend((f[0],f[j],f[j+1])for j in range(1,len(f)-1))
    return BVHTree.FromPolygons([Vector(v)for v in mesh['v']],faces,all_triangles=True,epsilon=.0001)

runner_count=60
runners=[tree(m)for m in g.meshes[:runner_count]]
leg_joins=[]
for idx in range(runner_count,runner_count+4):
    leg=tree(g.meshes[idx]);contacts=[]
    for runner,bvh in enumerate(runners):
        if leg.overlap(bvh):contacts.append(runner)
    leg_joins.append(dict(mesh_index=idx,runner_contacts=contacts))
radius=max(math.hypot(v.y-pivot.y,v.z-pivot.z)for v in vertices)
triangles=sum(len(f)-2 for m in g.meshes for f in m['f'])
report=dict(id=13,kind='source_actual_mesh_true_rocker',pivot=list(pivot),triangles=triangles,
            all_visible_geometry_in_one_moving_part=all(m['part']=='RockingFrame'for m in g.meshes),
            floor_samples=samples,minimum_y_all_41=min(s['minimum_y']for s in samples),
            outer_radius_yz=radius,continuous_rotation_floor_lower_bound=pivot.y-radius,
            leg_runner_joins=leg_joins,
            passed=all(m['part']=='RockingFrame'for m in g.meshes)and
                   min(s['minimum_y']for s in samples)>-1e-5 and
                   pivot.y-radius>-1e-5 and all(j['runner_contacts']for j in leg_joins))
path=ROOT/'Verification/PostApocalypticFurniture/rocker013_true_motion.json'
path.write_text(json.dumps(report,indent=2),encoding='utf-8')
print('ROCKER013_ACTUAL_MESH',json.dumps({k:v for k,v in report.items()if k not in('floor_samples',)}),flush=True)
