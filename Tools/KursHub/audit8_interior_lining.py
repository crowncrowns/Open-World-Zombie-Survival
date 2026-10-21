"""Face-proven interior cream lining, preserving exterior and collider payloads.

Blender only; staging roots only. Uses the read-only face classifications in
audit8/agent_checks. It creates closed 25-mm plaster liners, 8 mm inside opaque
blue wall faces, with inset edges. Glass, portals and the original facade are
never replaced. Seven proven playable-facing bulkhead surfaces are included;
two narrow unverified service voids are explicitly excluded.

New lining is appended to the existing sixteen FacadeDetails/Concrete/NC FBX
geometry payloads at all three LODs. Every old Model/Geometry UID, material link,
old mesh-array prefix and every collision-enabled payload remains unchanged.
Source and FBXs commit only after native geometry checks and real FBX imports.
"""
from pathlib import Path
from collections import defaultdict
import argparse
import array
import hashlib
import json
import math
import shutil
import sys

import bpy
from mathutils import Vector

PROJECT=Path(__file__).resolve().parents[2]
HUB=Path('Assets/Environment/KursHub')
DATA=PROJECT/'work/KursHub/audit8/agent_checks'
THICKNESS=.025
OFFSET=.008
EXCLUDED_VOID_FACES={'LeftAdvertisingBlockSide','RightAdvertisingBlockSide.001'}


def _sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def _json(path,value):
    Path(path).parent.mkdir(parents=True,exist_ok=True)
    Path(path).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def _xyz(p):return Vector((p[0],p[2],p[1]))
def _unity(p):return [float(p[0]),float(p[2]),float(p[1])]
def _guard(root):
    root=Path(root).resolve()
    assert root!=PROJECT and root.is_relative_to((PROJECT/'work/KursHub/audit8').resolve()),root
    assert (root/HUB/'KursHubManifest.json').is_file(),root
    return root


def _data():
    facade_path=DATA/'cream_lining_candidates.json'
    raised_path=DATA/'raised_wing_lining_candidates.json'
    facade=json.loads(facade_path.read_text(encoding='utf8'))
    raised=json.loads(raised_path.read_text(encoding='utf8'))
    candidates=[dict(p,classification='Exterior opaque panel, floor proven inside') for p in facade['candidates']]
    rejected=[]
    for p in raised['candidates']:
        if p['object'] in EXCLUDED_VOID_FACES:rejected.append(p);continue
        candidates.append(dict(p,classification='Closed bulkhead exterior facing playable upper floor'))
    assert len(facade['candidates'])==228 and len(candidates)==235,(len(facade['candidates']),len(candidates))
    return candidates,dict(facadeClassificationSHA256=_sha(facade_path),raisedClassificationSHA256=_sha(raised_path),
        facadeFaces=228,playableBulkheadFaces=7,unverifiedNarrowVoidsExcluded=rejected,
        sealedUpperFacadeFacesExcluded=facade.get('sealedUpperDiscarded'),
        basis='Measured source face/normal, actual floor polygon inside/outside samples; no bounding-box recolour')


def _validate_face(candidate):
    obj=bpy.data.objects.get(candidate['object'])
    assert obj is not None and obj.type=='MESH',candidate['object']
    assert obj.data.materials[0].name in ('BlueCladding','MallBlue'),(obj.name,[m.name for m in obj.data.materials])
    face=obj.data.polygons[candidate['faceIndex']]
    normal=(obj.matrix_world.to_3x3().inverted().transposed()@face.normal).normalized()
    expected=_xyz(candidate['sourceNormal']).normalized()
    assert (normal-expected).length<.0001,(obj.name,'Face normal changed',normal,expected)
    assert abs(normal.z)<.01,(obj.name,'Not a vertical wall face')
    original=[obj.matrix_world@obj.data.vertices[i].co for i in face.vertices]
    plane=original[0]
    for point in candidate['clippedVertices']:
        point=_xyz(point)
        assert abs((point-plane).dot(normal))<.0001,(obj.name,'Classification plane changed')
        for axis in range(3):assert min(p[axis] for p in original)-.0001<=point[axis]<=max(p[axis] for p in original)+.0001
    # The candidates are clipped subsets of real opaque faces. Insetting these
    # vertices cannot bridge a window/door gap or protrude past an exterior edge.
    return obj,normal


def _plate(candidate,material):
    from audit8_skylight import _mesh,_assert_geometry
    original,normal=_validate_face(candidate)
    poly=[_xyz(p) for p in candidate['clippedVertices']]
    tangent=normal.cross(Vector((0,0,1))).normalized()
    centre=sum(poly,Vector())/len(poly)
    width=max(p.dot(tangent) for p in poly)-min(p.dot(tangent) for p in poly)
    height=max(p.z for p in poly)-min(p.z for p in poly)
    assert width>.08 and height>.08,(original.name,width,height)
    # Original metal-panel faces overlap 25 mm between neighbouring bays.
    # A 14-mm inset at each end leaves a narrow real plaster movement joint,
    # instead of overlapping coplanar cream skins and introducing z-fighting.
    sx=max(.1,1-.028/width);sy=max(.1,1-.008/height)
    inset=[]
    for p in poly:
        relative=p-centre
        inset.append(centre+tangent*(relative.dot(tangent)*sx)+Vector((0,0,relative.z*sy)))
    count=len(inset)
    vertices=[p+normal*OFFSET for p in inset]+[p+normal*(OFFSET+THICKNESS) for p in inset]
    faces=[tuple(reversed(range(count))),tuple(count+i for i in range(count))]
    faces.extend((i,(i+1)%count,count+(i+1)%count,count+i) for i in range(count))
    name='Hub8 lining '+original.name+' face'+str(candidate['faceIndex'])
    mesh=_mesh(name,vertices,faces,material)
    layer=mesh.uv_layers.active
    for loop in mesh.loops:
        p=mesh.vertices[loop.vertex_index].co
        layer.data[loop.index].uv=(p.dot(tangent)/2,p.z/2)
    _assert_geometry(mesh)
    # Front/back are strictly roomward of the actual opaque wall plane. Only
    # the tiny closed edge thickness is new; the external silhouette survives.
    distances=[(v.co-poly[0]).dot(normal) for v in mesh.vertices]
    assert min(distances)>OFFSET-.0001 and max(distances)<OFFSET+THICKNESS+.0001
    return name,mesh,width*height,normal


def _source(root,work,candidates,chunks):
    from audit7_soft_shading import native_geometry_hash
    from audit8_skylight import _assert_geometry
    source=root/'Sources/KursHub/KursHub.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    assert not bpy.context.scene.get('Hub8InteriorLiningRevision'),'Use a fresh lining source'
    old_names=set(bpy.data.objects.keys())
    before={o.name:(native_geometry_hash(o.data),[list(row) for row in o.matrix_world]) for o in bpy.data.objects if o.type=='MESH'}
    material=bpy.data.materials['Concrete']
    collection=bpy.data.collections.new('Hub8 interior cream plaster lining')
    bpy.context.scene.collection.children.link(collection)
    objects=[];records=[];groups=defaultdict(list)
    for candidate in candidates:
        name,mesh,area,normal=_plate(candidate,material)
        obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj)
        obj['unity_collision']=False;obj['construction_group']='Hub8 interior lining'
        obj['Hub8LiningSourceObject']=candidate['object'];obj['Hub8LiningSourceFace']=candidate['faceIndex']
        obj['Hub8LiningFloorY']=candidate['floorY'];obj['Hub8LiningThicknessMetres']=THICKNESS
        centre=sum((v.co for v in mesh.vertices),Vector())/len(mesh.vertices)
        chunk=min(chunks,key=lambda c:((c['region'][0]+.5)*24-centre.x)**2+((c['region'][1]+.5)*30-centre.y)**2)
        obj['Hub8ExportChunk']=chunk['name'];groups[chunk['name']].append(obj)
        bevel=obj.modifiers.new('Plaster board softened edge 1.5 mm','BEVEL');bevel.width=.0015;bevel.segments=2;bevel.limit_method='ANGLE'
        objects.append(obj)
        records.append(dict(sourceObject=candidate['object'],sourceFace=candidate['faceIndex'],sourceNormal=candidate['sourceNormal'],
            clippedVertices=candidate['clippedVertices'],floorY=candidate['floorY'],classification=candidate['classification'],
            sourceObjectName=obj.name,chunk=chunk['name'],areaSquareMetres=area,
            proofInsideFloorPoint=candidate.get('insideFloorPoint',candidate.get('openFloorPoint')),
            proofOutsideFloorOrClosedVolumePoint=candidate.get('outsideFloorPoint',candidate.get('closedVolumePoint'))))
    bpy.context.view_layer.update()
    after={o.name:(native_geometry_hash(o.data),[list(row) for row in o.matrix_world]) for o in bpy.data.objects if o.name in before}
    assert before==after,'An original source mesh/UV/material/transform was changed'
    assert old_names<=set(bpy.data.objects.keys())
    bpy.context.scene['Hub8InteriorLiningRevision']=8
    bpy.context.scene['Hub8InteriorLiningDescription']='235 measured opaque interior wall faces; closed 25 mm plaster, 8 mm offset, NC geometry only; original exterior/glass/portals/collision meshes unchanged'
    pending=work/'KursHub.lining8.pending.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(pending))
    lods=[]
    for level in (0,1,2):
        for obj in objects:
            for mod in obj.modifiers:
                if mod.type=='BEVEL':mod.segments=1 if level else 2
        bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();output={}
        for name,items in groups.items():
            vertices,faces,uvs,normals=[],[],[],[]
            for obj in items:
                ev=obj.evaluated_get(deps);mesh=ev.to_mesh(preserve_all_data_layers=True,depsgraph=deps) if level<2 else obj.data
                _assert_geometry(mesh)
                offset=len(vertices);vertices.extend(tuple(v.co) for v in mesh.vertices)
                for polygon in mesh.polygons:
                    faces.append(tuple(offset+i for i in polygon.vertices))
                    for li in polygon.loop_indices:uvs.append(tuple(mesh.uv_layers.active.data[li].uv));normals.append(tuple(mesh.corner_normals[li].vector))
                if level<2:ev.to_mesh_clear()
            mesh=bpy.data.meshes.new(name+' appended interior lining');mesh.from_pydata(vertices,[],faces);mesh.update()
            layer=mesh.uv_layers.new(name='PhysicalUV')
            for i,uv in enumerate(uvs):layer.data[i].uv=uv
            mesh.normals_split_custom_set(normals);_assert_geometry(mesh);mesh.use_fake_user=True
            output[name]=mesh
        lods.append(output)
    return pending,lods,dict(originalSourceObjectsRetained=len(old_names),originalMeshesUnchanged=len(before),
            newClosedLiners=len(objects),areaSquareMetres=sum(p['areaSquareMetres'] for p in records),
            panelThicknessMetres=THICKNESS,insideFaceOffsetMetres=OFFSET,horizontalEdgeInsetMetres=.014,
            verticalEdgeInsetMetres=.004,exteriorGlassPortalsAndOldSourceMeshesUnchanged=True,panels=records)


def _append_array(element,values,prefix_proof):
    original=element.props[0]
    assert isinstance(original,array.array),(element.id,type(original))
    prefix_proof.append((element.id,len(original),original.tobytes()))
    value=array.array(original.typecode,original);value.extend(values);element.props[0]=value


def _append_geometry(geometry,mesh):
    from audit7_soft_shading import child
    vertices=child(geometry,b'Vertices');indices=child(geometry,b'PolygonVertexIndex')
    vertex_offset=len(vertices.props[0])//3;loop_offset=len(indices.props[0]);proof=[]
    _append_array(vertices,(float(c) for v in mesh.vertices for c in v.co),proof)
    new_indices=[]
    for p in mesh.polygons:
        ids=[int(i)+vertex_offset for i in p.vertices]
        new_indices.extend(ids[:-1]);new_indices.append(-ids[-1]-1)
    _append_array(indices,new_indices,proof)
    normal=child(geometry,b'LayerElementNormal')
    assert child(normal,b'MappingInformationType').props[0]==b'ByPolygonVertex'
    mapping=child(normal,b'ReferenceInformationType').props[0]
    normal_values=child(normal,b'Normals');normal_offset=len(normal_values.props[0])//3
    _append_array(normal_values,(float(c) for n in mesh.corner_normals for c in n.vector),proof)
    if mapping==b'IndexToDirect':_append_array(child(normal,b'NormalsIndex'),range(normal_offset,normal_offset+len(mesh.loops)),proof)
    else:assert mapping==b'Direct'
    layers=[e for e in geometry.elems if e.id==b'LayerElementUV']
    assert len(layers)==1,'Preserve extra UV channels explicitly before using this pass'
    uv=layers[0]
    assert child(uv,b'MappingInformationType').props[0]==b'ByPolygonVertex'
    assert child(uv,b'ReferenceInformationType').props[0]==b'IndexToDirect'
    uv_values=child(uv,b'UV');uv_offset=len(uv_values.props[0])//2
    _append_array(uv_values,(float(c) for item in mesh.uv_layers.active.data for c in item.uv),proof)
    _append_array(child(uv,b'UVIndex'),range(uv_offset,uv_offset+len(mesh.loops)),proof)
    edge_node=next((e for e in geometry.elems if e.id==b'Edges'),None)
    if edge_node:
        first={}
        for li,loop in enumerate(mesh.loops):first.setdefault(loop.edge_index,li+loop_offset)
        _append_array(edge_node,(first[i] for i in range(len(mesh.edges))),proof)
    assert not any(e.id in (b'LayerElementSmoothing',b'LayerElementTangent',b'LayerElementBinormal') for e in geometry.elems), 'Unexpected extra layer requires explicit append support'
    material=child(geometry,b'LayerElementMaterial')
    assert child(material,b'MappingInformationType').props[0]==b'AllSame' and list(child(material,b'Materials').props[0])==[0]
    return [dict(array=name.decode(),oldElements=count,oldBytePrefixSHA256=hashlib.sha256(data).hexdigest()) for name,count,data in proof]


def _array_nodes(element,path=()):
    result={}
    if element.props and isinstance(element.props[0],array.array):result[path+(element.id,)]=element.props[0]
    for child in element.elems:result.update(_array_nodes(child,path+(element.id,)))
    return result


def _patch(path,pending,additions,manifest):
    from audit7_soft_shading import child,clone,encoded_clone,tree_hash,parse_fbx,encode_bin
    parsed,version=parse_fbx.parse(str(path));tree=clone(parsed)
    objects=child(tree,b'Objects').elems;connections=child(tree,b'Connections').elems
    models={o.props[0]:o for o in objects if o.id==b'Model'}
    geometries={}
    for g in (o for o in objects if o.id==b'Geometry'):
        target=[c.props[2] for c in connections if c.props[0]==b'OO' and c.props[1]==g.props[0] and c.props[2] in models]
        assert len(target)==1
        geometries[models[target[0]].props[1].split(b'\x00\x01')[0].decode()]=g
    assert set(additions)<=set(geometries),'The original NC chunks must exist at all LODs'
    changed_ids={geometries[name].props[0] for name in additions}
    unchanged={o.props[0]:tree_hash(o) for o in objects if o.props and o.props[0] not in changed_ids}
    before_arrays={name:{key:(len(value),value.tobytes()) for key,value in _array_nodes(geometries[name]).items()} for name in additions}
    nc={c['name']:c for c in manifest['chunks']}
    array_proof={}
    for name,mesh in additions.items():
        assert nc[name]['material']=='Concrete' and not nc[name]['collider'] and not nc[name].get('hidden',False)
        array_proof[name]=_append_geometry(geometries[name],mesh)
    assert tree_hash(child(parsed,b'Connections'))==tree_hash(child(tree,b'Connections'))
    encode_bin.write(str(pending),encoded_clone(tree),version)
    encoded,_=parse_fbx.parse(str(pending));after_objects=child(encoded,b'Objects').elems
    by_uid={o.props[0]:o for o in after_objects if o.props}
    assert all(tree_hash(by_uid[uid])==digest for uid,digest in unchanged.items()),'An unrelated FBX object/model/material/texture changed'
    for name,old_arrays in before_arrays.items():
        after_arrays=_array_nodes(by_uid[geometries[name].props[0]])
        for key,(length,data) in old_arrays.items():assert after_arrays[key][:length].tobytes()==data,(name,key,'Original array prefix changed')
    assert tree_hash(child(encoded,b'Connections'))==tree_hash(child(parsed,b'Connections'))
    assert tree_hash(child(encoded,b'GlobalSettings'))==tree_hash(child(parsed,b'GlobalSettings'))
    collision_names={c['name'] for c in manifest['chunks'] if c['collider']}
    collisions=[name for name in geometries if name in collision_names]
    assert not set(collisions)&set(additions)
    return dict(existingModelCount=len(models),modifiedNCChunks=sorted(additions),
                oldObjectsOutsideModifiedNCGeometryIdentical=len(unchanged),allModelAndGeometryUIDsPreserved=True,
                allConnectionsMaterialsTexturesAndTransformsIdentical=True,allOriginalMeshArrayPrefixesIdentical=True,
                collisionGeometryPayloadsIdentical=len(collisions),unchangedGlobalSettings=True,arrayPrefixProof=array_proof),set(geometries)


def _verify(path,names):
    from audit8_skylight import _assert_geometry
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert {o.name for o in objects}==names
    metrics=[]
    for obj in objects:
        assert all(abs(s-1)<.00001 for s in obj.scale) and obj.matrix_world.determinant()>0,(obj.name,obj.scale)
        metrics.append(dict(name=obj.name,**_assert_geometry(obj.data)))
    return dict(chunks=len(objects),triangles=sum(p['triangles'] for p in metrics),geometryMetrics=metrics)


def apply(root,work):
    sys.path.insert(0,str(Path(__file__).parent))
    root=_guard(root);work=Path(work).resolve();work.mkdir(parents=True,exist_ok=True)
    assert work.is_relative_to((PROJECT/'work/KursHub/audit8').resolve())
    path=root/HUB/'KursHubManifest.json';manifest=json.loads(path.read_text(encoding='utf8'))
    chunks=[c for c in manifest['chunks'] if c['group']=='FacadeDetails' and c['material']=='Concrete' and not c['collider']]
    assert len(chunks)==16,len(chunks)
    source=root/'Sources/KursHub/KursHub.blend'
    models=[root/HUB/'Models'/('KursHubShell.fbx' if i==0 else f'LOD/KursHubShell_LOD{i}.fbx') for i in (0,1,2)]
    hashes={str(p):_sha(p) for p in [source,*models]}
    for p in [path,source,*models]:
        backup=work/'before'/p.relative_to(root);backup.parent.mkdir(parents=True,exist_ok=True)
        assert not backup.exists(),'Keep prior proof backups; use a fresh work directory'
        shutil.copy2(p,backup)
    candidates,classification=_data()
    pending_source,lods,source_proof=_source(root,work,candidates,chunks)
    pending_models=[];records=[]
    for level,model in enumerate(models):
        pending=work/(model.stem+'.lining8.pending.fbx')
        proof,names=_patch(model,pending,lods[level],manifest)
        pending_models.append((pending,names));records.append(dict(level=level,model=str(model.relative_to(root)),**proof))
    last=None
    for level,(pending,names) in enumerate(pending_models):
        proof=_verify(pending,names)
        assert last is None or proof['triangles']<last,'Shell LOD triangle counts must decrease'
        last=proof['triangles'];records[level].update(proof);records[level]['sha256']=_sha(pending)
        print('HUB8_INTERIOR_LINING_FBX_OK',level,proof['chunks'],proof['triangles'],flush=True)
    for p,digest in hashes.items():assert _sha(p)==digest,('Concurrent geometry/source change',p)
    # Other agents may add unrelated palette/prop metadata during the proof.
    # Re-read, require the original geometry contract, then update our own field.
    current=json.loads(path.read_text(encoding='utf8'))
    assert current['chunks']==manifest['chunks'],'Concurrent shell geometry contract edit'
    for (pending,names),model in zip(pending_models,models):pending.replace(model)
    pending_source.replace(source)
    current['hub8InteriorLining']=dict(panels=source_proof['newClosedLiners'],areaSquareMetres=source_proof['areaSquareMetres'],
        thickness=THICKNESS,insideFaceOffset=OFFSET,colliderChanges=False,exteriorAndPortalsUnchanged=True,
        classification=classification['basis'],unverifiedVoidFacesExcluded=sorted(EXCLUDED_VOID_FACES))
    _json(path,current)
    result=dict(status='HUB8_INTERIOR_LINING_SOURCE_FBX_OK',root=str(root),classification=classification,source=source_proof,
        models=records,sourceSHA256=_sha(source),manifestSHA256=_sha(path),unityImportNotYetVerified=True,
        navmeshNotYetRepeated=True,visualAcceptanceNotYetVerified=True)
    _json(work/'interior_lining_proof.json',result)
    print(result['status'],source_proof['newClosedLiners'],source_proof['areaSquareMetres'],flush=True)
    return result


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--root',required=True,type=Path);p.add_argument('--work',required=True,type=Path)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    apply(args.root,args.work)
