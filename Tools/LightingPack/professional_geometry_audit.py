import bpy,bmesh,sys,json,math,hashlib
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
proof=VERIFY/'ProfessionalAudit';only=sys.argv[sys.argv.index('--only')+1] if '--only' in sys.argv else None
result=[a for a in json.loads((proof/'clean_fbx_audit.json').read_text())['results'] if a['name']!=only] if only else []
for path in sorted((ASSET/'Models').rglob('*.fbx')):
    if only and path.stem!=only:continue
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert meshes,path
    info=dict(name=path.stem,path=path.relative_to(ASSET).as_posix(),meshes=[],triangles=[0,0,0],coordinatesHash='',fbxSHA256=hashlib.sha256(path.read_bytes()).hexdigest())
    positions=[]
    for o in meshes:
        me=o.data;assert me.vertices and me.polygons,(path,o.name,'empty')
        assert o.matrix_world.determinant()>0,(path,o.name,'negative transform')
        assert len(me.uv_layers)>=2,(path,o.name,'UV2 missing')
        for layer in list(me.uv_layers)[:2]:
            for uv in layer.data:assert all(math.isfinite(a) for a in uv.uv),(path,o.name,'nonfinite UV')
        uv2=me.uv_layers[1]
        assert all(-.0001<=a<=1.0001 for uv in uv2.data for a in uv.uv),(path,o.name,'UV2 out of range')
        assert all(math.isfinite(a) for v in me.vertices for a in v.co),(path,o.name,'NaN vertex')
        bm=bmesh.new();bm.from_mesh(me);zero=sum(f.calc_area()<1e-12 for f in bm.faces);boundary=sum(e.is_boundary for e in bm.edges);nonmanifold=sum(not e.is_manifold and not e.is_boundary for e in bm.edges)
        assert zero==0,(path,o.name,'zero area',zero)
        assert all(p.normal.length>.5 for p in me.polygons),(path,o.name,'invalid normals')
        signed=bm.calc_volume(signed=True);bm.free()
        assert all(m is not None for m in me.materials),(path,o.name,'missing material')
        tri=sum(len(p.vertices)-2 for p in me.polygons)
        if o.name.startswith('LOD'):info['triangles'][int(o.name[3])]+=tri
        if o.name.startswith('LOD0') or path.parent.name=='Decor':positions.extend([tuple(round(float(a),6) for a in o.matrix_world@v.co) for v in me.vertices])
        info['meshes'].append(dict(name=o.name,vertices=len(me.vertices),triangles=tri,zeroArea=zero,boundaryEdges=boundary,nonmanifoldEdges=nonmanifold,signedVolume=signed,uvSets=len(me.uv_layers)))
    if path.parent.name!='Decor':assert info['triangles'][0]>info['triangles'][1]>info['triangles'][2]>0,(path,info['triangles'])
    info['coordinatesHash']=hashlib.sha256(str(sorted(positions)).encode()).hexdigest();result.append(info)
    print('CLEAN_REIMPORT_AUDIT_PASS',path.name,flush=True)
assert len(result)==100
used=[i['coordinatesHash'] for i in result if i['name'].endswith('_Used')];assert len(set(used))==30
for i in result:
    if i['name'].endswith('_Used'):
        damaged=next(d for d in result if d['name']==i['name'][:-5]+'_Damaged');assert i['coordinatesHash']!=damaged['coordinatesHash']
(proof/'clean_fbx_audit.json').write_text(json.dumps(dict(passed=True,blender=bpy.app.version_string,files=100,uniqueTypes=30,results=result),indent=2),encoding='utf-8')
print('ALL_100_CLEAN_REIMPORT_AUDIT_PASS',flush=True)
