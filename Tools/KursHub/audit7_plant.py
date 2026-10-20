"""Replace the eleven diamond leaves with closed curved lanceolate blades.

Only KH_PottedPlant changes geometry. Native names and all FBX Model IDs remain
stable; other props and the pot/soil/rim FBX geometry are preserved exactly.
"""
import argparse,array,hashlib,json,math,pathlib,shutil,sys
import bpy,bmesh
from mathutils import Vector
from io_scene_fbx import parse_fbx,encode_bin,data_types
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent))
import audit7_soft_shading as shading
from audit7_verify_props import geometry

PROJECT=pathlib.Path(__file__).resolve().parents[2]

def leaf_mesh(name,controls,segments,material):
    base,tip,side,ridge=(Vector(v) for v in controls)
    normal=(tip-base).cross(side).normalized()
    if normal.z<0:normal=-normal
    vertices=[];parameters=[];rows=[]
    for index in range(segments+1):
        t=index/segments
        curve=math.sin(math.pi*t)
        centre=base.lerp(tip,t)
        row=[]
        across=(0,) if index in (0,segments) else (-1,0,1)
        for u in across:
            point=centre+side*(u*curve**.88)+ridge*(curve*(1 if u==0 else .10))
            row.append(len(vertices));vertices.append(point);parameters.append(((u+1)/2,t))
        rows.append(row)
    top_count=len(vertices)
    vertices+= [point-normal*.0016 for point in vertices]
    parameters+=parameters[:]
    faces=[]
    for index in range(segments):
        a,b=rows[index],rows[index+1]
        if len(a)==1:faces.extend(((a[0],b[0],b[1]),(a[0],b[1],b[2])))
        elif len(b)==1:faces.extend(((a[0],b[0],a[1]),(a[1],b[0],a[2])))
        else:faces.extend(((a[0],b[0],b[1],a[1]),(a[1],b[1],b[2],a[2])))
    faces+= [tuple(top_count+i for i in reversed(face)) for face in faces[:]]
    outline=[rows[0][0]]+[row[0] for row in rows[1:-1]]+[rows[-1][0]]+[row[-1] for row in reversed(rows[1:-1])]
    for index,a in enumerate(outline):
        b=outline[(index+1)%len(outline)]
        faces.append((a,a+top_count,b+top_count,b))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces);mesh.materials.append(material)
    uv=mesh.uv_layers.new(name='UVMap')
    for face in mesh.polygons:
        for loop in face.loop_indices:uv.data[loop].uv=parameters[mesh.loops[loop].vertex_index]
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update();bm.to_mesh(mesh);bm.free()
    shading.smooth_mesh(mesh)
    metrics=geometry(mesh)
    assert not metrics['nonManifoldEdges'] and not metrics['degenerateFaces'] and not metrics['nonPositiveSolids'],(name,metrics)
    return mesh

def merge_meshes(meshes,name):
    vertices=[];faces=[];uvs=[];normals=[];smooth=[]
    for mesh in meshes:
        offset=len(vertices);vertices.extend(tuple(v.co) for v in mesh.vertices)
        layer=mesh.uv_layers.active
        for face in mesh.polygons:
            faces.append(tuple(offset+i for i in face.vertices));smooth.append(face.use_smooth)
            for loop in face.loop_indices:
                uvs.append(tuple(layer.data[loop].uv));normals.append(tuple(mesh.corner_normals[loop].vector))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    layer=mesh.uv_layers.new(name='UVMap')
    for index,uv in enumerate(uvs):layer.data[index].uv=uv
    for face,value in zip(mesh.polygons,smooth):face.use_smooth=value
    mesh.normals_split_custom_set(normals);mesh.update()
    metrics=geometry(mesh)
    assert not metrics['nonManifoldEdges'] and not metrics['degenerateFaces'] and not metrics['nonPositiveSolids'],metrics
    return mesh

def replace_array(parent,name,kind,data):
    node=shading.child(parent,name);node.props=[data];node.props_type=bytearray(kind)

def replace_leaf_chunk(path,mesh,chunk_name,backup):
    parsed,version=parse_fbx.parse(str(path));root=shading.clone(parsed)
    objects=shading.child(root,b'Objects').elems
    models={o.props[0]:o.props[1].split(b'\x00\x01',1)[0].decode('utf8') for o in objects if o.id==b'Model'}
    connections=shading.child(root,b'Connections').elems
    target=None
    for node in objects:
        if node.id!=b'Geometry':continue
        names={models[c.props[2]] for c in connections if c.props[0]==b'OO' and c.props[1]==node.props[0] and c.props[2] in models}
        if chunk_name in names:target=node
    assert target is not None,('Missing preserved plant leaf chunk',path)
    other_before={node.props[0]:shading.tree_hash(node) for node in objects if node is not target}
    target_id=target.props[0]
    replace_array(target,b'Vertices',b'd',array.array('d',(c for v in mesh.vertices for c in v.co)))
    indices=[]
    for face in mesh.polygons:indices.extend(list(face.vertices[:-1])+[-face.vertices[-1]-1])
    replace_array(target,b'PolygonVertexIndex',b'i',array.array(data_types.ARRAY_INT32,indices))
    target.elems=[e for e in target.elems if e.id!=b'Edges']
    layer=shading.child(target,b'LayerElementNormal')
    shading.child(layer,b'MappingInformationType').props[0]=b'ByPolygonVertex'
    shading.child(layer,b'ReferenceInformationType').props[0]=b'Direct'
    replace_array(layer,b'Normals',b'd',array.array('d',(c for n in mesh.corner_normals for c in n.vector)))
    layer.elems=[e for e in layer.elems if e.id not in (b'NormalsIndex',b'NormalsW')]
    layer=shading.child(target,b'LayerElementUV')
    shading.child(layer,b'MappingInformationType').props[0]=b'ByPolygonVertex'
    shading.child(layer,b'ReferenceInformationType').props[0]=b'IndexToDirect'
    uv=mesh.uv_layers.active
    replace_array(layer,b'UV',b'd',array.array('d',(c for datum in uv.data for c in datum.uv)))
    replace_array(layer,b'UVIndex',b'i',array.array(data_types.ARRAY_INT32,range(len(mesh.loops))))
    backup.parent.mkdir(parents=True,exist_ok=True)
    if not backup.exists():shutil.copy2(path,backup)
    temporary=path.with_suffix('.curved_leaves.fbx')
    encode_bin.write(str(temporary),shading.encoded_clone(root),version)
    check,_=parse_fbx.parse(str(temporary))
    other_after={node.props[0]:shading.tree_hash(node) for node in shading.child(check,b'Objects').elems if node.props[0]!=target_id}
    assert other_before==other_after,('Non-leaf FBX objects changed',path)
    assert shading.tree_hash(shading.child(parsed,b'Connections'))==shading.tree_hash(shading.child(check,b'Connections'))
    temporary.replace(path)
    return dict(nonLeafObjectsExactlyPreserved=True,modelIDsAndConnectionsPreserved=True,sha256=hashlib.sha256(path.read_bytes()).hexdigest())

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=pathlib.Path,default=PROJECT/'work/KursHub/audit7/authoring')
    parser.add_argument('--report',type=pathlib.Path,default=PROJECT/'work/KursHub/audit7/plant')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    root=args.root.resolve();report=args.report.resolve();report.mkdir(parents=True,exist_ok=True)
    manifest_path=root/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json'
    manifest=json.loads(manifest_path.read_text(encoding='utf8'))
    prop=next(p for p in manifest['props'] if p['id']=='KH_PottedPlant')
    source=root/'Sources/KursHub/KursHubProps.blend'
    backup=report/'before/Sources/KursHub/KursHubProps.blend';backup.parent.mkdir(parents=True,exist_ok=True)
    if not backup.exists():shutil.copy2(source,backup)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    collection=bpy.data.collections[prop['collection']]
    leaves=sorted([o for o in collection.objects if o.name.startswith('BroadBentLeaf')],key=lambda o:o.name)
    assert len(leaves)==11,len(leaves)
    old_leaf_names={o.data.name for o in leaves}
    unchanged_before={m.name:shading.native_geometry_hash(m) for m in bpy.data.meshes if m.name not in old_leaf_names}
    controls={}
    for obj in leaves:
        data=obj.data
        assert len(data.vertices)==10,('Expected original editable diamond leaf',obj.name,len(data.vertices))
        base=data.vertices[0].co.copy();tip=data.vertices[2].co.copy()
        side=(data.vertices[1].co-data.vertices[3].co)/2
        ridge=data.vertices[4].co-(base+tip)/2
        controls[obj.name]=[list(base),list(tip),list(side),list(ridge)]
    leaf_chunk=next(c for c in prop['chunks'] if c['material']=='LeafGreen')['name']
    deps=bpy.context.evaluated_depsgraph_get()
    stalks=[]
    for obj in collection.objects:
        if obj in leaves or not obj.data.materials or obj.data.materials[0].name!='LeafGreen':continue
        data=bpy.data.meshes.new_from_object(obj.evaluated_get(deps),preserve_all_data_layers=True,depsgraph=deps)
        stalks.append(data)
    material=bpy.data.materials['LeafGreen'];levels=[]
    for level,segments in ((0,16),(1,8),(2,4)):
        generated=[leaf_mesh(obj.name+'_LOD'+str(level),controls[obj.name],segments,material) for obj in leaves]
        merged=merge_meshes(generated+stalks,'CurvedPlantLeaves_TEMP')
        record=replace_leaf_chunk(root/prop['lods'][level]['model'],merged,leaf_chunk,report/'before'/prop['lods'][level]['model'])
        levels.append(dict(level=level,segments=segments,**record))
        for data in generated+[merged]:bpy.data.meshes.remove(data)
    for data in stalks:bpy.data.meshes.remove(data)
    for obj in leaves:
        old=obj.data;name=old.name
        obj.data=leaf_mesh(name+'_new',controls[obj.name],16,material)
        if old.users==0:bpy.data.meshes.remove(old)
        obj.data.name=name
        obj['Audit7LeafRevision']='Closed lanceolate leaf; 16 longitudinal sections, centre vein ridge, curved taper, unique leaf UV 0..1'
    unchanged_after={m.name:shading.native_geometry_hash(m) for m in bpy.data.meshes if m.name not in old_leaf_names}
    assert unchanged_before==unchanged_after,'An unrelated native mesh was changed'
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    results=[]
    for level in prop['lods']:
        path=root/level['model'];bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
        objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
        assert {o.name for o in objects}=={c['name'] for c in prop['chunks']}
        count=0
        for obj in objects:
            metrics=geometry(obj.data);count+=metrics['triangles']
            assert not metrics['nonManifoldEdges'] and not metrics['degenerateFaces'] and not metrics['nonPositiveSolids'] and not metrics['invalidFaceNormals'],(level,obj.name,metrics)
            assert all(abs(s-1)<1e-5 for s in obj.scale) and obj.data.uv_layers
        points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
        lo=[min(v[i] for v in points) for i in (0,2,1)];hi=[max(v[i] for v in points) for i in (0,2,1)]
        if level['level']==0:
            previous_bounds=[prop['boundsMin'],prop['boundsMax']]
            assert max(abs(lo[i]-prop['boundsMin'][i]) for i in range(3))<.04
            assert max(abs(hi[i]-prop['boundsMax'][i]) for i in range(3))<.04
            prop['boundsMin']=lo;prop['boundsMax']=hi;prop['triangles']=count
        level['triangles']=count
        results.append(dict(level=level['level'],triangles=count,boundsMin=lo,boundsMax=hi))
    assert results[0]['triangles']>results[1]['triangles']>results[2]['triangles']
    assert sum(p['triangles'] for p in manifest['props'])<250000
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
    final=dict(status='AUDIT7_CURVED_PLANT_OK',prop='KH_PottedPlant',existingLeafObjects=11,originalObjectAndChunkNamesPreserved=True,
        otherNativeMeshHashesIdentical=True,geometryException='Only the LeafGreen chunk of KH_PottedPlant changes geometry; all other props retain the normals-pass geometry hashes.',
        oldBounds=previous_bounds,levels=results,exportProof=levels,sourceSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),controls=controls)
    (report/'plant_geometry.json').write_text(json.dumps(final,ensure_ascii=False,indent=2),encoding='utf8')
    print(final['status'],results,flush=True)

if __name__=='__main__':main()
