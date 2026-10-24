import bpy,bmesh,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'work/LawnFence/UnityProject/Assets/Environment/Fences'
report=[]
for path in sorted(list((ASSET/'Models').glob('*.fbx'))+list((ASSET/'Colliders').glob('*.fbx'))):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
    checks=[]
    for ob in [o for o in bpy.context.scene.objects if o.type=='MESH']:
        me=ob.data;bm=bmesh.new();bm.from_mesh(me);bm.transform(ob.matrix_world)
        boundary=sum(not e.is_manifold for e in bm.edges)
        coords=[v.co for v in bm.verts];dims=[max(v[i] for v in coords)-min(v[i] for v in coords) for i in range(3)]
        vol=bm.calc_volume(signed=True)
        assert boundary==0 and vol>0 and me.uv_layers and all(math.isfinite(x) for v in coords for x in v)
        if 'Post' not in path.name:assert 1.79<dims[0]<1.84
        checks.append({'mesh':ob.name,'dimensions_m':dims,'nonmanifold_edges':boundary,'signed_volume_m3':vol,'uv':True});bm.free()
    assert checks
    report.append({'fbx':path.name,'meshes':checks})
(ROOT/'Verification/LawnFence/blender_reimport.json').write_text(json.dumps(report,indent=2))
print('LAWN_FENCE_WORLD_SPACE_REIMPORT_PASS')
