import bpy,sys,json,math,hashlib
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
results=[];data=json.loads((ASSET/'manifest.json').read_text(encoding='utf-8'))
for path in sorted((ASSET/'Models').rglob('*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert meshes,path
    verts=0;uv=True;finite=True;tri=[0,0,0];coords=[]
    for o in meshes:
        me=o.data;verts+=len(me.vertices);uv &= bool(me.uv_layers)
        for v in me.vertices:
            p=o.matrix_world@v.co;finite &= all(math.isfinite(x) for x in p)
            if 'LOD0_' in o.name or path.parent.name=='Decor':coords.append(tuple(round(float(x),6) for x in p))
        for lod in range(3):
            if o.name.startswith('LOD'+str(lod)+'_'):tri[lod]+=sum(len(p.vertices)-2 for p in me.polygons)
        assert len(me.materials)>0,(path,o.name)
    assert uv and finite,(path,uv,finite)
    if path.parent.name!='Decor':assert tri[0]>tri[1]>tri[2]>0,(path,tri)
    results.append(dict(file=str(path.relative_to(ASSET)),meshCount=len(meshes),vertices=verts,uv=uv,finite=finite,triangles=tri,geometryHash=hashlib.sha256(str(sorted(coords)).encode()).hexdigest()))
    print('REIMPORT_PASS',path.name,flush=True)
assert len(results)==len(data['fixtures'])+10
for row in catalog()[:len(data['fixtures'])//3]:
    subset=[r for r in results if ('Light_'+row['name']+'_') in r['file']]
    assert len(subset)==3
    assert next(r['geometryHash'] for r in subset if '_Damaged' in r['file'])!=next(r['geometryHash'] for r in subset if '_Used' in r['file'])
used=[r['geometryHash'] for r in results if '_Used.fbx' in r['file']]
assert len(set(used))==len(used)
VERIFY.mkdir(exist_ok=True,parents=True)
(VERIFY/'blender_reimport.json').write_text(json.dumps(dict(passed=True,blender=bpy.app.version_string,count=len(results),uniqueUsedGeometries=len(set(used)),results=results),indent=2),encoding='utf-8')
print('ALL_FBX_REIMPORT_PASS',len(results),flush=True)
