"""Read both delivered native sources independently after all assembly edits."""
from pathlib import Path
import bpy,json,hashlib,sys,math
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit7';A=W/'authoring'
sys.path.insert(0,str(Path(__file__).parent));from audit7_verify_props import geometry
manifest=json.loads((A/'Assets/Environment/KursHub/KursHubManifest.json').read_text(encoding='utf8'))
props=json.loads((A/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json').read_text(encoding='utf8'))
before=json.loads((W/'baseline/Assets/Environment/KursHub/KursHubManifest.json').read_text(encoding='utf8'))
old_prop=json.loads((W/'geometry/props_before.json').read_text(encoding='utf8'))['props']
results=[]
for file in ('KursHubProps.blend','KursHub.blend'):
    path=A/'Sources/KursHub'/file;bpy.ops.wm.open_mainfile(filepath=str(path));deps=bpy.context.evaluated_depsgraph_get()
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];records=[]
    for o in meshes:
        evaluated=o.evaluated_get(deps);me=evaluated.to_mesh(preserve_all_data_layers=True,depsgraph=deps);m=geometry(me);evaluated.to_mesh_clear()
        assert not any(m[k] for k in ('nonManifoldEdges','degenerateFaces','invalidFaceNormals','nonPositiveSolids')),(o.name,m)
        assert m['uvLayers'] and m['finiteUV'],o.name
        assert all(s.material for s in o.material_slots),o.name
        assert all(s>0 for s in o.scale),o.name
        records.append(dict(name=o.name,triangles=m['triangles'],solids=m['connectedSolids']))
    missing=[im.name for im in bpy.data.images if im.users and im.source=='FILE' and not im.packed_file and not Path(bpy.path.abspath(im.filepath)).exists()]
    assert not missing,missing
    if file=='KursHubProps.blend':
        names={o.name for o in meshes};oldnames={o['name'] for p in old_prop for o in p['objects']};assert oldnames<=names
        assert all(bpy.data.collections.get(p['collection']) for p in props['props'])
        extra=dict(oldPropsRetained=len(old_prop),propCollections=len(props['props']),originalObjectsRetained=len(oldnames))
    else:
        original=[d['name'] for d in json.loads((W/'architecture_before.json').read_text(encoding='utf8'))['objects'] if d['type']=='MESH']
        assert set(original)<={o.name for o in meshes}
        old_instances={p['id'] for key in ('propInstances','interiorInstances','settlementInstances') for p in before[key]}
        assert old_instances<={o.name for o in bpy.data.objects}
        counts={}
        for key in ('propInstances','interiorInstances','settlementInstances'):
            for p in manifest[key]:
                o=bpy.data.objects[p['id']];assert o.instance_collection.name==p['prop'];x,y,z=p['position'];assert (o.location-__import__('mathutils').Vector((x,z,y))).length<.0001
            counts[key]=len(manifest[key])
        extra=dict(originalShellObjectsRetained=len(original),originalInstancesRetained=len(old_instances),**counts)
    results.append(dict(file=file,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),meshes=len(meshes),triangles=sum(r['triangles'] for r in records),packedImageCount=sum(bool(i.packed_file) for i in bpy.data.images),missingDependencies=missing,parts=records,**extra))
report=dict(status='AUDIT7_NATIVE_SOURCES_OK',sourceVersion=bpy.app.version_string,files=results)
(W/'release_sources.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('AUDIT7_NATIVE_SOURCES_OK',[(f['file'],f['meshes'],f['triangles']) for f in results],flush=True)
