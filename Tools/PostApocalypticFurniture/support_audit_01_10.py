"""Actual visible-geometry contact audit for floor feet/legs in recipes 001--100.

No author models or Unity files are changed. Candidate slender ground-contact
primitives are checked for a positive upper connection to other visible geometry
using BVH triangle intersections, with a 0.5 mm contact allowance. Debris is not
treated as a support. This is narrower than a structural engineering analysis.
"""
import sys,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from models_01_10 import build


def bounds(mesh):
    lo=[min(v[i]for v in mesh['v'])for i in range(3)]
    hi=[max(v[i]for v in mesh['v'])for i in range(3)]
    return lo,hi


def tri(mesh,above=-1e9):
    fs=[]
    for face in mesh['f']:
        for j in range(1,len(face)-1):
            f=(face[0],face[j],face[j+1])
            if max(mesh['v'][i][1]for i in f)>=above:fs.append(f)
    return fs


report={'kind':'source_visible_support_contacts','tolerance_metres':.0005,
        'limits':'Slender floor-contact primitives only; BVH surface contact at rest, not strength or all moving poses.',
        'assets':[],'issues':[]}
for aid in range(1,101):
    g=build({'id':aid,'variant':(aid-1)%10,'category':'test','name':str(aid),'slug':str(aid)})
    bb=[bounds(m)for m in g.meshes]
    trees=[BVHTree.FromPolygons([Vector(v)for v in m['v']],tri(m),all_triangles=True,epsilon=.0005)for m in g.meshes]
    entry={'id':aid,'supports':[]}
    for index,(mesh,(lo,hi))in enumerate(zip(g.meshes,bb)):
        size=[hi[i]-lo[i]for i in range(3)]
        if g.parts[mesh['part']]['kind']=='damage':continue
        if lo[1]>.006 or size[1]<.025 or size[0]>.25 or size[2]>.25:continue
        # Only contacts above the lower third count as structural attachment.
        above=lo[1]+size[1]*.35
        foot=BVHTree.FromPolygons([Vector(v)for v in mesh['v']],tri(mesh,above),all_triangles=True,epsilon=.0005)
        contacts=[]
        for other,tree in enumerate(trees):
            if other==index or not tree:continue
            if g.parts[g.meshes[other]['part']]['kind']=='damage':continue
            olo,ohi=bb[other]
            if any(hi[i]+.001<olo[i]or lo[i]-.001>ohi[i]for i in range(3)):continue
            overlaps=foot.overlap(tree)
            if overlaps:contacts.append({'mesh_index':other,'part':g.meshes[other]['part'],'material':g.meshes[other]['mat'],'triangle_contacts':len(overlaps)})
        item={'mesh_index':index,'part':mesh['part'],'material':mesh['mat'],'bounds_min':lo,'bounds_max':hi,'contacts':contacts}
        entry['supports'].append(item)
        if not contacts:report['issues'].append({'id':aid,**item})
    report['assets'].append(entry)
report['summary']={'models':len(report['assets']),'support_primitives':sum(len(a['supports'])for a in report['assets']),
                   'detached_supports':len(report['issues'])}
dest=ROOT/'Verification/PostApocalypticFurniture/source_support_contacts_01_10.json'
dest.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('SOURCE_SUPPORT_CONTACT_SUMMARY',report['summary'],flush=True)
for issue in report['issues']:print('SUPPORT_NO_CONTACT',issue['id'],issue['mesh_index'],issue['material'],issue['bounds_min'],issue['bounds_max'],flush=True)
