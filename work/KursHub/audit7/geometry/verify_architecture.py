"""Read-only reverse import of the architectural shell, comparing legacy defects."""
import bpy,bmesh,pathlib,json,hashlib,ast,math
from mathutils import Vector
OUT=pathlib.Path(__file__).resolve().parent
W=OUT.parent
A=W/'authoring'
tree=ast.parse((OUT/'verify_exports.py').read_text(encoding='utf8'))
definition=next(node for node in tree.body if isinstance(node,ast.FunctionDef) and node.name=='geometry')
exec(compile(ast.Module(body=[definition],type_ignores=[]),'geometry_metrics','exec'))
base_manifest=json.loads((W/'baseline/Assets/Environment/KursHub/KursHubManifest.json').read_text(encoding='utf8'))
manifest=json.loads((A/'Assets/Environment/KursHub/KursHubManifest.json').read_text(encoding='utf8'))
fixes=json.loads((W/'architecture_fixes.json').read_text(encoding='utf8'))
old_names={c['name'] for c in base_manifest['chunks']}
new_names={c['name'] for c in manifest['chunks']}
all_records=[];errors=[]
files=[dict(level='baseline',path=str(W/'baseline/Assets/Environment/KursHub/Models/KursHubShell.fbx'),chunks=len(old_names))]+fixes['lods']
baseline_issues={}
for level in files:
    path=pathlib.Path(level['path'])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
    names={o.name for o in objs};issues=[];records=[]
    if len(objs)!=level['chunks']:issues.append('Unexpected chunk count')
    if not old_names<=names:issues.append(dict(missingOldChunkNames=sorted(old_names-names)))
    expected=old_names if level['level'] in ('baseline',2) else new_names
    if names!=expected:issues.append(dict(unexpectedNames=sorted(names-expected),missingNames=sorted(expected-names)))
    for obj in objs:
        metrics=geometry(obj.data)
        bad=[]
        if min(obj.scale)<=0 or obj.matrix_world.determinant()<=0:bad.append('Nonpositive scale/determinant')
        if any(abs(s-1)>1e-5 for s in obj.scale):bad.append('Nonunit object scale')
        if not metrics['uvLayers'] or not metrics['finiteUV']:bad.append('Missing/invalid UV')
        if len(obj.data.materials)!=1 or obj.data.materials[0] is None:bad.append('Missing material')
        topology=[]
        if metrics['nonManifoldEdges']:topology.append('Nonmanifold edges')
        if metrics['degenerateFaces']:topology.append('Degenerate faces')
        if metrics['invalidFaceNormals']:topology.append('Invalid face normals')
        if metrics['nonPositiveSolids']:topology.append('Nonpositive connected solid volume')
        if metrics['signedVolume']<=0:topology.append('Nonpositive chunk volume')
        if level['level']=='baseline' and topology:baseline_issues[obj.name]=dict(issues=topology,metrics=metrics)
        legacy=bool(topology and obj.name in baseline_issues and obj.name in old_names)
        # Baseline defects are documented only as the BEFORE condition. The
        # corrected assets have no topology exception, including font meshes.
        if topology and level['level']!='baseline':bad+=topology
        if bad:issues.append(dict(chunk=obj.name,issues=bad,metrics=metrics))
        points=[obj.matrix_world@v.co for v in obj.data.vertices]
        lo=[min(p[i] for p in points) for i in (0,2,1)]
        hi=[max(p[i] for p in points) for i in (0,2,1)]
        records.append(dict(chunk=obj.name,material=obj.data.materials[0].name if obj.data.materials else None,
            scale=list(obj.scale),boundsMin=lo,boundsMax=hi,topologyIssues=topology,legacyTextContract=legacy,**metrics))
    triangles=sum(r['triangles'] for r in records)
    if 'triangles' in level and triangles!=level['triangles']:issues.append(dict(triangleMismatch=[triangles,level['triangles']]))
    full_min=[min(r['boundsMin'][i] for r in records) for i in range(3)]
    full_max=[max(r['boundsMax'][i] for r in records) for i in range(3)]
    result=dict(level=level['level'],path=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),chunks=len(objs),triangles=triangles,
        boundsMin=full_min,boundsMax=full_max,issues=issues,chunkRecords=records)
    all_records.append(result)
    if issues:errors.append(dict(level=level['level'],issues=issues))
    print('ARCHITECTURE_REVERSE_IMPORT',level['level'],len(objs),triangles,'issues',len(issues),flush=True)
source=A/'Sources/KursHub/KursHub.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
old=json.loads((W/'architecture_before.json').read_text(encoding='utf8'))
old_objects={o['name'] for o in old['objects'] if o['type']=='MESH'}
current={o.name for o in bpy.context.scene.objects if o.type=='MESH'}
if not old_objects<=current:errors.append(dict(lostSourceMeshes=sorted(old_objects-current)))
depsgraph=bpy.context.evaluated_depsgraph_get()
source_checks=[]
for obj in bpy.context.scene.objects:
    if obj.type!='MESH':continue
    evaluated=obj.evaluated_get(depsgraph)
    mesh=evaluated.to_mesh(preserve_all_data_layers=True,depsgraph=depsgraph)
    metrics=geometry(mesh)
    source_issues=[]
    if metrics['nonManifoldEdges'] or metrics['degenerateFaces'] or metrics['invalidFaceNormals'] or metrics['nonPositiveSolids'] or metrics['signedVolume']<=0:source_issues.append('Invalid evaluated geometry')
    if not metrics['uvLayers'] or not metrics['finiteUV']:source_issues.append('Missing/invalid evaluated UV')
    if any(s.material is None for s in obj.material_slots):source_issues.append('Unassigned material slot')
    if min(obj.scale)<=0 or obj.matrix_world.determinant()<=0:source_issues.append('Nonpositive transform')
    item=dict(name=obj.name,isText=bool(obj.get('is_text')),issues=source_issues,**metrics)
    source_checks.append(item)
    if source_issues:errors.append(dict(sourceMesh=obj.name,issues=source_issues,metrics=metrics))
    evaluated.to_mesh_clear()
footprint_objects=[bpy.data.objects[name] for name in ('GroundBody','GroundRoundBay')]
points=[base.matrix_world@v.co for base in footprint_objects for v in base.data.vertices]
lo=[min(p[i] for p in points) for i in (0,2,1)]
hi=[max(p[i] for p in points) for i in (0,2,1)]
footprint=[hi[i]-lo[i] for i in range(3)]
if abs(footprint[0]-93)>.001 or abs(footprint[2]-130)>.001:errors.append(dict(wrongFootprint=footprint))
source_record=dict(path=str(source),sha256=hashlib.sha256(source.read_bytes()).hexdigest(),originalObjectsRetained=len(old_objects&current),sourceMeshObjects=len(current),addedMeshes=len(current-old_objects),evaluatedTriangles=sum(r['triangles'] for r in source_checks),footprintObjects=['GroundBody','GroundRoundBay'],buildingBaseBoundsMin=lo,buildingBaseBoundsMax=hi,buildingBaseSize=footprint,meshChecks=source_checks)
status='AUDIT7_ARCHITECTURE_ROUNDTRIP_OK' if not errors else 'AUDIT7_ARCHITECTURE_ROUNDTRIP_FAILED'
report=dict(status=status,errors=errors,baselineTopologyIssues=baseline_issues,source=source_record,results=all_records)
(W/'architecture_roundtrip.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(status,len(errors),'errors',flush=True)
if errors:raise RuntimeError('Read-only architectural audit found problems; see architecture_roundtrip.json')
