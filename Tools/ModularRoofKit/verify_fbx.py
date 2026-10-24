import bpy,bmesh,json,math,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
A=ROOT/'work/ModularRoofKit/UnityProject/Assets/Environment/ModularRoofKit'
if '--stage' in sys.argv:A=ROOT/'work/ModularRoofKit/Rebuilt'
report={'blender':bpy.app.version_string,'passed':False,'files':[]}
for path in sorted((A/'Models').rglob('*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert len(meshes)==3,(path,len(meshes))
    item={'file':str(path.relative_to(A)),'lods':[]}
    for ob in meshes:
        me=ob.data;me.calc_loop_triangles();assert me.uv_layers and len(me.vertices)>0
        assert all(math.isfinite(c) for v in me.vertices for c in v.co)
        assert all(math.isfinite(c) for uv in me.uv_layers[0].data for c in uv.uv)
        bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
        degenerate=sum(1 for f in bm.faces if f.calc_area()<1e-13)
        bounds=[min((ob.matrix_world@v.co)[k] for v in me.vertices) for k in range(3)]+[max((ob.matrix_world@v.co)[k] for v in me.vertices) for k in range(3)]
        item['lods'].append({'name':ob.name,'triangles':len(me.loop_triangles),'uvSets':len(me.uv_layers),'degenerateFaces':degenerate,'boundaryEdges':sum(e.is_boundary for e in bm.edges),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'signedVolume':bm.calc_volume(signed=True),'boundsBlender':bounds})
        # Leaves are intentionally two-sided polygon fragments; report rather than hide boundaries.
        assert degenerate==0,(path.name,ob.name,degenerate)
        bm.free()
    report['files'].append(item);print('CLEAN_REIMPORT_PASS',path.name,flush=True)
report['passed']=True
(ROOT/'Verification/ModularRoofKit/blender_reimport.json').write_text(json.dumps(report,indent=2))
