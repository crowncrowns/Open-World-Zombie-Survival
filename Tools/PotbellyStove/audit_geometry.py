import bpy,bmesh,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Sources/PotbellyStove/PotbellyStove_Master.blend'))
rows=[]
for root in [o for o in bpy.context.scene.objects if o.name in ['LOD0','LOD1','LOD2']]:
    meshes=[]
    for o in root.children_recursive:
        if o.type!='MESH':continue
        bm=bmesh.new();bm.from_mesh(o.data)
        row={'name':o.name,'triangles':sum(len(f.verts)-2 for f in bm.faces),'boundaryEdges':sum(e.is_boundary for e in bm.edges),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'volume':bm.calc_volume(signed=True),'uv0':len(o.data.uv_layers)>0}
        bm.free();meshes.append(row)
    rows.append({'lod':root.name,'triangles':sum(m['triangles'] for m in meshes),'meshes':meshes})
(ROOT/'Verification/PotbellyStove/GeometryAudit.json').write_text(json.dumps(rows,indent=2))
assert all(m['uv0'] and m['volume']>0 and m['nonManifoldEdges']==0 for r in rows for m in r['meshes'])
print('STOVE_CLOSED_GEOMETRY_PASS')
