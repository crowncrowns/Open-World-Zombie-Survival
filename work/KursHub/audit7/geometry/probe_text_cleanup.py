import bpy,bmesh,pathlib,json
W=pathlib.Path(__file__).resolve().parent.parent
bpy.ops.wm.open_mainfile(filepath=str(W/'authoring/Sources/KursHub/KursHub.blend'))
results=[]
for obj in bpy.context.scene.objects:
    if obj.type!='MESH' or not obj.get('is_text'):continue
    bm=bmesh.new();bm.from_mesh(obj.data)
    before=dict(vertices=len(bm.verts),nonManifoldEdges=sum(not e.is_manifold for e in bm.edges),degenerateFaces=sum(f.calc_area()<=1e-12 for f in bm.faces))
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-6)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    after=dict(vertices=len(bm.verts),faces=len(bm.faces),nonManifoldEdges=sum(not e.is_manifold for e in bm.edges),degenerateFaces=sum(f.calc_area()<=1e-12 for f in bm.faces),invalidNormals=sum(f.normal.length<.99 for f in bm.faces),volume=bm.calc_volume(signed=True))
    results.append(dict(name=obj.name,before=before,after=after))
    print(obj.name,before,'=>',after,flush=True)
    bm.free()
report=dict(status='TEXT_CLEANUP_PROBE',objects=results)
(W/'text_cleanup_probe.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print('TEXT_CLEANUP_PROBE',len(results),flush=True)
