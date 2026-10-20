import bpy,pathlib,json,hashlib,sys
from mathutils import Vector
W=pathlib.Path(__file__).resolve().parent;ROOT=W/'authoring';PROJECT=W.parents[2]
sys.path.insert(0,str(PROJECT/'Tools/KursHub'))
from audit7_verify_props import geometry
prop=json.loads((W/'extras/fir_manifest.json').read_text(encoding='utf8'))
results=[]
for level in prop['lods']:
    path=ROOT/level['model'];bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
    root=bpy.data.objects[prop['id']+'_Root'];assert root.type=='EMPTY'
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert sorted(o.name for o in objects)==sorted(c['name'] for c in prop['chunks'])
    records=[]
    for obj in objects:
        metrics=geometry(obj.data)
        assert not metrics['nonManifoldEdges'] and not metrics['degenerateFaces'] and not metrics['invalidFaceNormals'] and not metrics['nonPositiveSolids'] and metrics['signedVolume']>0,(obj.name,metrics)
        assert metrics['uvLayers'] and metrics['finiteUV'] and len(obj.data.materials)==1 and obj.data.materials[0]
        assert obj.parent==root and all(abs(s-1)<1e-5 for s in obj.scale) and obj.matrix_world.determinant()>0
        records.append(dict(chunk=obj.name,**metrics))
    triangles=sum(r['triangles'] for r in records)
    assert triangles==level['triangles'],(triangles,level)
    points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
    lo=[min(v[i] for v in points) for i in (0,2,1)];hi=[max(v[i] for v in points) for i in (0,2,1)]
    if level['level']==0:
        assert max(abs(lo[i]-prop['boundsMin'][i]) for i in range(3))<.00015
        assert max(abs(hi[i]-prop['boundsMax'][i]) for i in range(3))<.00015
    results.append(dict(level=level['level'],path=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),triangles=triangles,boundsMin=lo,boundsMax=hi,chunks=records))
    print('FIR_REVERSE_IMPORT_OK',level['level'],triangles,len(objects),flush=True)
source=W/'extras/KursHubAtriumFir.blend';bpy.ops.wm.open_mainfile(filepath=str(source))
collection=bpy.data.collections[prop['collection']]
assert (collection.instance_offset-Vector((26,130,0))).length<1e-5
records=[]
for obj in collection.objects:
    assert obj.type=='MESH' and (obj.location-collection.instance_offset).length<1e-5
    metrics=geometry(obj.data)
    assert not metrics['nonManifoldEdges'] and not metrics['degenerateFaces'] and not metrics['invalidFaceNormals'] and not metrics['nonPositiveSolids'],(obj.name,metrics)
    assert metrics['uvLayers'] and metrics['finiteUV'] and all(s.material for s in obj.material_slots)
    records.append(dict(name=obj.name,**metrics))
assert sum(r['triangles'] for r in records)==prop['triangles']
report=dict(status='AUDIT7_FIR_REVERSE_IMPORT_AND_SOURCE_OK',levels=results,sourceSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),sourceObjects=len(records),sourceResults=records)
(W/'extras/fir_validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(report['status'],len(records),'native objects',flush=True)
