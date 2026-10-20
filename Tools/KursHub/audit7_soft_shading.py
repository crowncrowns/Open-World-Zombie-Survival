"""Normals-only soft-fabric correction; preserve the exact model geometry and UVs.

Apply to the current staged PBR-packed sources, not to the original geometry
pass. Binary FBX is rewritten with Blender's official FBX encoder: only the
normal layer changes, avoiding a coordinate/UV roundtrip during mesh export.
Every real FBX is subsequently imported and checked again.
"""
import argparse,array,hashlib,importlib.util,json,math,pathlib,re,shutil,struct,sys
import bpy
from io_scene_fbx import parse_fbx,encode_bin,data_types
from types import SimpleNamespace

PROJECT=pathlib.Path(__file__).resolve().parents[2]

def selected(material):
    material=re.sub(r'\.\d{3}$','',material)
    return material.startswith(('Canvas','Rug')) or material in {'SandCanvas','TarpGreen','ClothInk','InkDark'}

def packed_floats(values):
    return array.array('f',values).tobytes()

def native_geometry_hash(mesh):
    digest=hashlib.sha256()
    digest.update(packed_floats(c for v in mesh.vertices for c in v.co))
    digest.update(array.array('i',(i for e in mesh.edges for i in e.vertices)).tobytes())
    for face in mesh.polygons:
        digest.update(array.array('i',[len(face.vertices),*face.vertices,face.material_index]).tobytes())
    for layer in mesh.uv_layers:
        digest.update(layer.name.encode('utf8'))
        digest.update(packed_floats(c for datum in layer.data for c in datum.uv))
    for material in mesh.materials:digest.update((material.name if material else 'NULL').encode('utf8'))
    return digest.hexdigest()

def smooth_mesh(mesh):
    for polygon in mesh.polygons:polygon.use_smooth=True
    mesh.set_sharp_from_angle(angle=math.radians(60))
    # Zero custom normals request Blender's automatic smooth fan normals.
    if mesh.has_custom_normals:mesh.normals_split_custom_set([(0.,0.,0.)]*len(mesh.loops))
    mesh.update()

def apply_native(path,backup):
    backup.parent.mkdir(parents=True,exist_ok=True)
    if not backup.exists():shutil.copy2(path,backup)
    before_file_sha=hashlib.sha256(path.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(path))
    before={mesh.name:native_geometry_hash(mesh) for mesh in bpy.data.meshes}
    changed=[]
    for mesh in bpy.data.meshes:
        if any(material and selected(material.name) for material in mesh.materials):
            smooth_mesh(mesh);changed.append(mesh.name)
    after={mesh.name:native_geometry_hash(mesh) for mesh in bpy.data.meshes}
    assert before==after,('Native mesh/UV/material data changed',path)
    assert hashlib.sha256(path.read_bytes()).hexdigest()==before_file_sha,('Concurrent native source edit',path)
    bpy.context.scene['KursSoftShadingRevision']='7: smooth fabric and padded solids; 60 degree sharp boundary; geometry and UV unchanged'
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    return dict(path=str(path),beforeFileSHA256=before_file_sha,afterFileSHA256=hashlib.sha256(path.read_bytes()).hexdigest(),
        meshes=len(before),changedMeshes=len(changed),changedNames=changed,geometryAndUVHashesIdentical=True,
        geometryDigest=hashlib.sha256(json.dumps(before,sort_keys=True).encode()).hexdigest())

def child(element,identifier):
    return next(e for e in element.elems if e.id==identifier)

def clone(element):
    return SimpleNamespace(id=element.id,props=list(element.props),props_type=bytearray(element.props_type),elems=[clone(e) for e in element.elems])

def encoded_clone(element):
    result=encode_bin.FBXElem(element.id)
    methods={data_types.BOOL:'add_bool',data_types.CHAR:'add_char',data_types.INT8:'add_int8',
        data_types.INT16:'add_int16',data_types.INT32:'add_int32',data_types.INT64:'add_int64',
        data_types.FLOAT32:'add_float32',data_types.FLOAT64:'add_float64',data_types.BYTES:'add_bytes',
        data_types.STRING:'add_string',data_types.FLOAT32_ARRAY:'add_float32_array',data_types.FLOAT64_ARRAY:'add_float64_array',
        data_types.INT32_ARRAY:'add_int32_array',data_types.INT64_ARRAY:'add_int64_array',
        data_types.BOOL_ARRAY:'add_bool_array',data_types.BYTE_ARRAY:'add_byte_array'}
    for kind,prop in zip(element.props_type,element.props):getattr(result,methods[kind])(prop)
    result.elems=[encoded_clone(e) for e in element.elems]
    return result

def tree_hash(element,skip_normals=False):
    digest=hashlib.sha256()
    def visit(e):
        if skip_normals and e.id in (b'LayerElementNormal',b'LayerElementSmoothing'):return
        digest.update(struct.pack('<I',len(e.id)));digest.update(e.id);digest.update(bytes(e.props_type))
        for prop in e.props:
            if isinstance(prop,array.array):value=prop.typecode.encode()+prop.tobytes()
            elif isinstance(prop,bytes):value=prop
            else:value=repr(prop).encode('ascii')
            digest.update(struct.pack('<Q',len(value)));digest.update(value)
        for nested in e.elems:visit(nested)
    visit(element)
    return digest.hexdigest()

def fbx_geometry_hashes(root):
    objects=child(root,b'Objects')
    return {g.props[1].decode('utf8'):tree_hash(g,skip_normals=True) for g in objects.elems if g.id==b'Geometry'}

def fbx_non_normal_contract(root):
    # Encoder refreshes CreationTimeStamp/FileID only. Preserve every object,
    # material, texture, transform, ID and connection apart from normal layers.
    return {key.decode():tree_hash(child(root,key),skip_normals=True) for key in (b'Objects',b'Connections',b'GlobalSettings')}

def geometry_mesh(geometry):
    coordinates=child(geometry,b'Vertices').props[0]
    indices=child(geometry,b'PolygonVertexIndex').props[0]
    vertices=[tuple(coordinates[i:i+3]) for i in range(0,len(coordinates),3)]
    faces=[];current=[]
    for index in indices:
        current.append(index if index>=0 else -index-1)
        if index<0:faces.append(tuple(current));current=[]
    assert not current,'Unterminated FBX polygon'
    mesh=bpy.data.meshes.new('SoftNormalOnly_TEMP')
    mesh.from_pydata(vertices,[],faces);mesh.update();smooth_mesh(mesh)
    assert len(mesh.loops)==len(indices),'Raw polygon loop order changed'
    return mesh

def patch_fbx(path,materials,backup):
    parsed,version=parse_fbx.parse(str(path));root=clone(parsed)
    before_geometry=fbx_geometry_hashes(root);before_contract=fbx_non_normal_contract(root)
    objects=child(root,b'Objects').elems
    models={model.props[0]:model.props[1].split(b'\x00\x01',1)[0].decode('utf8') for model in objects if model.id==b'Model'}
    connections=child(root,b'Connections').elems
    geometries=[g for g in objects if g.id==b'Geometry']
    changed=[]
    for geometry in geometries:
        names={models[c.props[2]] for c in connections if c.props[0]==b'OO' and c.props[1]==geometry.props[0] and c.props[2] in models}
        assert len(names)==1,('Geometry must have one preserved chunk model',path,geometry.props[1],names)
        name=names.pop()
        material=materials[name]
        if not selected(material):continue
        mesh=geometry_mesh(geometry)
        normals=array.array('d',(component for normal in mesh.corner_normals for component in normal.vector))
        assert len(normals)==len(mesh.loops)*3
        assert all(math.isfinite(v) for v in normals)
        layer=child(geometry,b'LayerElementNormal')
        previous=tree_hash(layer)
        child(layer,b'MappingInformationType').props[0]=b'ByPolygonVertex'
        child(layer,b'ReferenceInformationType').props[0]=b'Direct'
        normal_node=child(layer,b'Normals');normal_node.props[0]=normals;normal_node.props_type=bytearray(b'd')
        layer.elems=[e for e in layer.elems if e.id not in (b'NormalsIndex',b'NormalsW')]
        if previous!=tree_hash(layer):changed.append(dict(chunk=name,material=material,faceCorners=len(mesh.loops)))
        bpy.data.meshes.remove(mesh)
    if changed:
        backup.parent.mkdir(parents=True,exist_ok=True)
        if not backup.exists():shutil.copy2(path,backup)
        temporary=path.with_suffix('.soft_normals.fbx')
        encode_bin.write(str(temporary),encoded_clone(root),version)
        check,_=parse_fbx.parse(str(temporary))
        assert fbx_geometry_hashes(check)==before_geometry,('FBX geometry/UV arrays changed',path)
        assert fbx_non_normal_contract(check)==before_contract,('FBX non-normal objects/connections/settings changed',path)
        temporary.replace(path)
    # Actual Blender importer must accept the resulting real FBX, and the
    # strict geometry verifier checks the imported normals and closed solids.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    imported=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert {o.name for o in imported}==set(materials),('Imported chunk names changed',path)
    for obj in imported:
        assert obj.data.has_custom_normals,('FBX importer lost authored normals',path,obj.name)
        assert obj.data.uv_layers and all(abs(v-1)<1e-5 for v in obj.scale),(path,obj.name,'UV/scale')
    return dict(path=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),changedChunks=changed,
        geometryAndUVHashesIdentical=True,nonNormalObjectsConnectionsSettingsIdentical=True,
        geometryHashes=before_geometry,actualImportChunks=len(imported))

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=pathlib.Path,default=PROJECT/'work/KursHub/audit7/authoring')
    parser.add_argument('--report',type=pathlib.Path,default=PROJECT/'work/KursHub/audit7/soft_shading')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    root=args.root.resolve();report=args.report.resolve();report.mkdir(parents=True,exist_ok=True)
    manifest=json.loads((root/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json').read_text(encoding='utf8'))
    native=[]
    for name in ('KursHubProps.blend','KursHub.blend'):
        native.append(apply_native(root/'Sources/KursHub'/name,report/'before/Sources/KursHub'/name))
        print('SOFT_NATIVE_OK',name,native[-1]['changedMeshes'],flush=True)
    files=[]
    for prop in manifest['props']:
        materials={chunk['name']:chunk['material'] for chunk in prop['chunks']}
        for level in prop['lods']:
            if 'model' not in level:continue
            record=patch_fbx(root/level['model'],materials,report/'before'/level['model'])
            record.update(id=prop['id'],level=level['level']);files.append(record)
            print('SOFT_FBX_OK',prop['id'],level['level'],len(record['changedChunks']),flush=True)
    result=dict(status='AUDIT7_SOFT_SHADING_OK',sharpAngleDegrees=60,geometryOrUVChanged=False,
        nativeSources=native,fbxExports=len(files),changedFBX=sum(bool(f['changedChunks']) for f in files),
        changedChunks=sum(len(f['changedChunks']) for f in files),files=files,
        limitation='Normals-only correction. It cannot add missing cloth drape geometry, furniture detail or material variation.')
    (report/'soft_shading.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    print(result['status'],len(files),result['changedFBX'],result['changedChunks'],flush=True)

if __name__=='__main__':main()
