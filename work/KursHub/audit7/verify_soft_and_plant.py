"""Final source and exact array-preservation audit after the visual corrections."""
import pathlib,json,hashlib,sys
import bpy
from io_scene_fbx import parse_fbx
from mathutils import Vector
W=pathlib.Path(__file__).resolve().parent
PROJECT=W.parents[2]
sys.path.insert(0,str(PROJECT/'Tools/KursHub'))
import audit7_soft_shading as shading
from audit7_verify_props import geometry
ROOT=W/'authoring'
manifest=json.loads((ROOT/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json').read_text(encoding='utf8'))
soft=json.loads((W/'soft_shading/soft_shading.json').read_text(encoding='utf8'))
files=[];unchanged=0;plant_exceptions=0
for record in soft['files']:
    path=pathlib.Path(record['path']);tree,_=parse_fbx.parse(str(path))
    now=shading.fbx_geometry_hashes(tree)
    before=record['geometryHashes']
    if record['id']!='KH_PottedPlant':
        assert before==now,('Unrelated FBX geometry or UV array changed',path)
        unchanged+=1
    else:
        changed=[key for key in now if now[key]!=before[key]]
        assert len(changed)==1 and 'LeafGreen' in changed[0],(path,changed)
        plant_exceptions+=1
    files.append(dict(path=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),geometryHashes=now))
source=ROOT/'Sources/KursHub/KursHubProps.blend'
source_hash=hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source))
deps=bpy.context.evaluated_depsgraph_get();sources=[];count=0;triangles=0
for prop in manifest['props']:
    collection=bpy.data.collections[prop['collection']]
    offset=Vector((prop['sourceOffset'][0],prop['sourceOffset'][2],prop['sourceOffset'][1]))
    assert (collection.instance_offset-offset).length<1e-5
    objects=[]
    for obj in collection.objects:
        if obj.type!='MESH':continue
        assert (obj.location-offset).length<1e-5,(obj.name,'Origin changed')
        evaluated=obj.evaluated_get(deps)
        mesh=evaluated.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
        metrics=geometry(mesh)
        assert not metrics['nonManifoldEdges'] and not metrics['degenerateFaces'] and not metrics['invalidFaceNormals'] and not metrics['nonPositiveSolids'],(obj.name,metrics)
        assert metrics['finiteUV'] and metrics['uvLayers'] and all(slot.material for slot in obj.material_slots)
        objects.append(dict(name=obj.name,**metrics));triangles+=metrics['triangles'];count+=1
        evaluated.to_mesh_clear()
    sources.append(dict(id=prop['id'],objects=objects))
assert count==2004 and len(sources)==54 and triangles<250000,(count,len(sources),triangles)
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash,'Concurrent native file edit; rerun after PBR integration'
final=dict(status='AUDIT7_SOFT_AND_PLANT_FINAL_OK',props=54,fbx=160,unchangedFBXGeometryAndUV=unchanged,plantLeafGeometryExceptions=plant_exceptions,
    sourceObjects=count,sourceEvaluatedTriangles=triangles,sourceSHA256=source_hash,files=files,sourceResults=sources)
(W/'soft_shading/final_preservation_and_source.json').write_text(json.dumps(final,ensure_ascii=False,indent=2),encoding='utf8')
print(final['status'],unchanged,plant_exceptions,count,triangles,flush=True)
