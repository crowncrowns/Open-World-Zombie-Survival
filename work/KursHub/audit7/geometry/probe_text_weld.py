import bpy,bmesh,pathlib,json
W=pathlib.Path(__file__).resolve().parent.parent
bpy.ops.wm.open_mainfile(filepath=str(W/'authoring/Sources/KursHub/KursHub.blend'))
results=[]
def metrics(bm):
    return dict(vertices=len(bm.verts),faces=len(bm.faces),nonManifoldEdges=sum(not e.is_manifold for e in bm.edges),
        degenerateFaces=sum(f.calc_area()<=1e-12 for f in bm.faces),volume=bm.calc_volume(signed=True))
for obj in bpy.context.scene.objects:
    if obj.type!='MESH' or not obj.get('is_text'):continue
    bm=bmesh.new();bm.from_mesh(obj.data);before=metrics(bm);bm.free()
    if not before['nonManifoldEdges'] and not before['degenerateFaces'] and before['volume']>0:continue
    trials=[]
    for distance in (1e-7,1e-6,1e-5,.0001):
        bm=bmesh.new();bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=distance)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        after=metrics(bm)
        trials.append(dict(distance=distance,**after))
        bm.free()
    results.append(dict(name=obj.name,before=before,trials=trials))
report=dict(status='TEXT_WELD_PROBE',objects=results)
(W/'text_weld_probe.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
for r in results:
    print(r['name'],r['before']['nonManifoldEdges'],[(t['distance'],t['nonManifoldEdges'],t['degenerateFaces'],t['volume']) for t in r['trials']],flush=True)
print('TEXT_WELD_PROBE',len(results),flush=True)
