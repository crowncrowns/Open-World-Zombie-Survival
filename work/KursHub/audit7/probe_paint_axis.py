import sys
sys.path.insert(0,'E:/git/Open-World-Zombie-Survival/Tools/KursHub')
from audit7_soft_shading import *
p=PROJECT/'work/KursHub/audit7/authoring/Assets/Environment/KursHub/Props/Models/KH_KursAtriumBanner.fbx'
r,v=parse_fbx.parse(str(p));obs=child(r,b'Objects').elems
for g in [o for o in obs if o.id==b'Geometry']:
 me=geometry_mesh(g); print(g.props[1],[(min(v.co[i] for v in me.vertices),max(v.co[i] for v in me.vertices)) for i in range(3)])
