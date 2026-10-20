import bpy,bmesh,json,pathlib,hashlib,math
from mathutils import Vector
OUT=pathlib.Path(__file__).resolve().parent
PACKAGE=OUT/'output'
path=PACKAGE/'Sources/KursHub/KursHubProps.blend'
manifest=json.loads((PACKAGE/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json').read_text(encoding='utf8'))
before=json.loads((OUT/'props_before.json').read_text(encoding='utf8'))
after=json.loads((OUT/'props_after.json').read_text(encoding='utf8'))
old={o['name'] for p in before['props'] for o in p['objects']}
bpy.ops.wm.open_mainfile(filepath=str(path))
depsgraph=bpy.context.evaluated_depsgraph_get()
names=set();records=[];triangles=0
for prop in manifest['props']:
    collection=bpy.data.collections[prop['collection']]
    offset=Vector((prop['sourceOffset'][0],prop['sourceOffset'][2],prop['sourceOffset'][1]))
    assert (collection.instance_offset-offset).length<1e-5,(prop['id'],'Wrong native collection offset')
    objs=[]
    for obj in collection.objects:
        if obj.type!='MESH':continue
        names.add(obj.name)
        assert (obj.location-offset).length<1e-5,(prop['id'],obj.name,'Changed placement origin')
        assert min(obj.scale)>0 and obj.matrix_world.determinant()>0,(obj.name,'Negative transform')
        assert obj.data.uv_layers and all(s.material is not None for s in obj.material_slots),(obj.name,'Missing UV/material')
        evaluated=obj.evaluated_get(depsgraph)
        mesh=evaluated.to_mesh(preserve_all_data_layers=True,depsgraph=depsgraph)
        bm=bmesh.new();bm.from_mesh(mesh)
        assert all(e.is_manifold for e in bm.edges),(obj.name,'Open evaluated surface')
        assert all(f.calc_area()>1e-12 and f.normal.length>.99 for f in bm.faces),(obj.name,'Degenerate face/normals')
        volume=bm.calc_volume(signed=True)
        assert volume>0,(obj.name,'Inverted solid')
        bm.free();mesh.calc_loop_triangles();count=len(mesh.loop_triangles);triangles+=count
        objs.append(dict(name=obj.name,triangles=count,volume=volume,modifiers=[dict(name=m.name,type=m.type) for m in obj.modifiers],uv=[l.name for l in mesh.uv_layers]))
        evaluated.to_mesh_clear()
    records.append(dict(id=prop['id'],sourceOffset=prop['sourceOffset'],objects=objs))
assert old<=names,('Lost original source objects',old-names)
assert len(records)==54 and len(names)==2004 and triangles==219190,(len(records),len(names),triangles)
result=dict(status='AUDIT7_PROPS_SOURCE_REOPEN_OK',sourceSHA256=hashlib.sha256(path.read_bytes()).hexdigest(),props=len(records),objects=len(names),retainedOriginalObjects=len(old),addedObjects=len(names-old),evaluatedTriangles=triangles,results=records)
(OUT/'source_roundtrip.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print(result['status'],result['props'],result['objects'],result['evaluatedTriangles'],flush=True)
