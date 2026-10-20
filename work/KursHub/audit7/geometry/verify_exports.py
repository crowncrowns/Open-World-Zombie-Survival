"""Verify every staged FBX, collecting all failures rather than stopping early."""
import bpy, bmesh, json, pathlib, hashlib, math
from mathutils import Vector

OUT=pathlib.Path(__file__).resolve().parent
PACKAGE=OUT/'output'
manifest=json.loads((PACKAGE/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json').read_text(encoding='utf8'))

def geometry(mesh):
    bm=bmesh.new();bm.from_mesh(mesh)
    values=dict(vertices=len(mesh.vertices),polygons=len(mesh.polygons),
        nonManifoldEdges=sum(not e.is_manifold for e in bm.edges),
        degenerateFaces=sum(f.calc_area()<=1e-12 for f in bm.faces),
        invalidFaceNormals=sum(f.normal.length<.99 for f in bm.faces),
        signedVolume=bm.calc_volume(signed=True),uvLayers=[l.name for l in mesh.uv_layers])
    bm.free()
    mesh.calc_loop_triangles()
    values['triangles']=len(mesh.loop_triangles)
    values['finiteUV']=all(math.isfinite(c) for layer in mesh.uv_layers for uv in layer.data for c in uv.uv)
    parents=list(range(len(mesh.vertices)))
    def find(a):
        while parents[a]!=a:
            parents[a]=parents[parents[a]];a=parents[a]
        return a
    for edge in mesh.edges:
        a,b=edge.vertices;parents[find(a)]=find(b)
    volumes={};origins={}
    for tri in mesh.loop_triangles:
        key=find(tri.vertices[0])
        origin=origins.setdefault(key,tuple(mesh.vertices[tri.vertices[0]].co))
        a,b,c=([float(mesh.vertices[index].co[axis])-origin[axis] for axis in range(3)] for index in tri.vertices)
        determinant=(a[0]*(b[1]*c[2]-b[2]*c[1]) + a[1]*(b[2]*c[0]-b[0]*c[2]) + a[2]*(b[0]*c[1]-b[1]*c[0]))
        volumes[key]=volumes.get(key,0.0)+determinant/6
    values['connectedSolids']=len(volumes)
    values['nonPositiveSolids']=sum(v<=0 for v in volumes.values())
    values['smallestSolidVolume']=min(volumes.values(),default=0)
    return values

results=[];errors=[]
for prop in manifest['props']:
    for level in prop['lods']:
        if 'model' not in level:continue
        path=PACKAGE/level['model'];current=[];records=[]
        try:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.fbx(filepath=str(path))
            root=bpy.data.objects.get(prop['id']+'_Root')
            objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
            if root is None or root.type!='EMPTY':current.append('Missing empty root')
            if sorted(o.name for o in objects)!=sorted(c['name'] for c in prop['chunks']):current.append('Chunk identities changed')
            for obj in objects:
                metrics=geometry(obj.data)
                issues=[]
                if obj.parent!=root:issues.append('Wrong parent')
                if any(abs(s-1)>1e-5 for s in obj.scale):issues.append('Nonunit scale')
                if obj.matrix_world.determinant()<=0:issues.append('Negative transform determinant')
                if metrics['nonManifoldEdges']:issues.append('Nonmanifold edges')
                if metrics['degenerateFaces']:issues.append('Degenerate faces')
                if metrics['invalidFaceNormals']:issues.append('Invalid normals')
                if metrics['signedVolume']<=0:issues.append('Nonpositive chunk volume')
                if metrics['nonPositiveSolids']:issues.append('Nonpositive connected solid volume')
                if not metrics['uvLayers'] or not metrics['finiteUV']:issues.append('Missing or invalid UV')
                if len(obj.data.materials)!=1 or not obj.data.materials[0]:issues.append('Material slots')
                if issues:current.append(dict(chunk=obj.name,issues=issues,metrics=metrics))
                records.append(dict(chunk=obj.name,scale=list(obj.scale),determinant=obj.matrix_world.determinant(),**metrics))
            count=sum(r['triangles'] for r in records)
            if count!=level['triangles']:current.append('Triangle count mismatch')
            points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
            lo=[min(p[i] for p in points) for i in (0,2,1)]
            hi=[max(p[i] for p in points) for i in (0,2,1)]
            if level['level']==0 and max(abs(lo[i]-prop['boundsMin'][i]) for i in range(3))>.00015:current.append('LOD0 min bounds mismatch')
            if level['level']==0 and max(abs(hi[i]-prop['boundsMax'][i]) for i in range(3))>.00015:current.append('LOD0 max bounds mismatch')
            results.append(dict(id=prop['id'],level=level['level'],path=level['model'],sha256=hashlib.sha256(path.read_bytes()).hexdigest(),triangles=count,boundsMin=lo,boundsMax=hi,chunks=records,issues=current))
        except Exception as error:
            current.append(repr(error))
        if current:errors.append(dict(id=prop['id'],level=level['level'],model=level['model'],issues=current))
        print('VERIFIED',prop['id'],level['level'],'issues',len(current),flush=True)
report=dict(status='AUDIT7_PROP_ROUNDTRIP_OK' if not errors else 'AUDIT7_PROP_ROUNDTRIP_FAILED',props=len(manifest['props']),exports=len(results),errors=errors,results=results)
(OUT/'fbx_roundtrip.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(report['status'],'exports',len(results),'failed',len(errors),flush=True)
if errors:raise RuntimeError('FBX roundtrip found failures; see fbx_roundtrip.json')
