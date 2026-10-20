import bpy,bmesh,pathlib,json
W=pathlib.Path(__file__).resolve().parent.parent
bpy.ops.wm.open_mainfile(filepath=str(W/'authoring/Sources/KursHub/KursHub.blend'))
results=[]
for obj in bpy.context.scene.objects:
    if obj.type!='MESH' or not obj.get('is_text'):continue
    trials=[]
    for mode in ('limit','triangulate','limit_triangulate','degenerate_limit_triangulate'):
        bm=bmesh.new();bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
        if mode.startswith('degenerate'):bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-5)
        if 'limit' in mode:bmesh.ops.dissolve_limit(bm,angle_limit=.0001,use_dissolve_boundaries=False,verts=list(bm.verts),edges=list(bm.edges),delimit=set())
        if 'triangulate' in mode:bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
        metrics=dict(mode=mode,vertices=len(bm.verts),faces=len(bm.faces),nonManifoldEdges=sum(not e.is_manifold for e in bm.edges),degenerateFaces=sum(f.calc_area()<=1e-12 for f in bm.faces),invalidNormals=sum(f.normal.length<.99 for f in bm.faces),volume=bm.calc_volume(signed=True))
        trials.append(metrics);bm.free()
    results.append(dict(name=obj.name,trials=trials))
report=dict(status='TEXT_CAP_PROBE',objects=results)
(W/'text_caps_probe.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
for mode in ('limit','triangulate','limit_triangulate','degenerate_limit_triangulate'):
    failed=[(o['name'],t) for o in results for t in o['trials'] if t['mode']==mode and (t['nonManifoldEdges'] or t['degenerateFaces'] or t['invalidNormals'] or t['volume']<=0)]
    print(mode,'failed',len(failed),failed,flush=True)
