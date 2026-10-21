"""Build new service equipment in staging; preserve every revision-7 asset."""
from pathlib import Path
import bpy,json,sys,importlib,hashlib
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit8';A=W/'authoring';sys.path.insert(0,str(Path(__file__).parent))
from audit8_modeling import Builder,export_prop
from audit7_verify_props import geometry
modules=[importlib.import_module(n) for n in ('hub8_industrial','hub8_domestic')]
source=A/'Sources/KursHub/KursHubProps.blend';before=W/'baseline/Sources/KursHub/KursHubProps.blend'
path=A/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json';manifest=json.loads((W/'baseline/Assets/Environment/KursHub/Props/KursHubPropsManifest.json').read_text())
old_ids={p['id'] for p in manifest['props']};new={};labels=None;source_records=[]

def retain_missing_chunks(prop,previous):
 names={o.get('Hub8ExportChunk',f"{prop['id']}_{o.data.materials[0].name}_{'C' if o['unity_collision'] else 'NC'}") for o in prop['objects']}
 expected={c['name']:c for c in previous['chunks']};missing=set(expected)-names
 if not missing:return
 prior=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(A/previous['lods'][-1]['model']))
 imported=[o for o in bpy.data.objects if o not in prior];kept=[]
 for obj in imported:
  if obj.type!='MESH' or obj.name not in missing:continue
  chunk=expected[obj.name];matrix=obj.matrix_world.copy();obj.parent=None;obj.matrix_world=matrix;obj.location+=prop['offset']
  obj.data.materials.clear();obj.data.materials.append(bpy.data.materials[chunk['material']]);obj['unity_collision']=chunk['collider'];obj['Hub8ExportChunk']=chunk['name'];obj['Audit8RetainedSafeLODChunk']=True
  # Keep the export node name free; Blender otherwise appends .001 to the merged chunk.
  obj.name='Audit8RetainedSource_'+chunk['name']
  for col in list(obj.users_collection):col.objects.unlink(obj)
  prop['col'].objects.link(obj);prop['objects'].append(obj);kept.append(obj)
 assert {o['Hub8ExportChunk'] for o in kept}==missing,('Missing real previous LOD chunk',missing)
 for obj in imported:
  if obj not in kept:bpy.data.objects.remove(obj,do_unlink=True)
 print('AUDIT8_KEEP_SAFE_LOD_CHUNKS',prop['id'],sorted(missing),flush=True)
bpy.ops.wm.open_mainfile(filepath=str(before))
for level in range(3):
 api=Builder(level,len(old_ids))
 if labels is not None:api.labels=dict(labels)
 for module in modules:module.build(api)
 assert api.current is None
 if labels is None:labels=api.labels
 else:assert labels==api.labels,'Label atlas identities changed between LODs'
 deps=bpy.context.evaluated_depsgraph_get()
 for prop in api.props:
  assert prop['id'] not in old_ids
  if level:retain_missing_chunks(prop,new[prop['id']])
  deps=bpy.context.evaluated_depsgraph_get()
  for obj in prop['objects']:
   ev=obj.evaluated_get(deps);mesh=ev.to_mesh(preserve_all_data_layers=True,depsgraph=deps);m=geometry(mesh);ev.to_mesh_clear()
   assert not any(m[k] for k in ('nonManifoldEdges','degenerateFaces','invalidFaceNormals','nonPositiveSolids')),(level,prop['id'],obj.name,m)
  record,extra=export_prop(prop,A,level)
  if level==0:
   new[prop['id']]=dict(id=prop['id'],model=record['model'],collection=prop['collection'],sourceOffset=prop['sourceOffset'],origin=prop['origin'],triangles=record['triangles'],lods=[],**extra)
   source_records.append(dict(id=prop['id'],parts=len(prop['objects']),**extra))
  else:
   assert new[prop['id']]['chunks']==extra['chunks'],('Changing export chunk identities',level,prop['id'])
   assert record['triangles']<new[prop['id']]['lods'][-1]['triangles'],('LOD did not reduce',prop['id'],record)
  new[prop['id']]['lods'].append(record)
  print('AUDIT8_NEW_PROP',prop['id'],'LOD',level,'triangles',record['triangles'],flush=True)
 if level==0:
  bpy.context.scene['KursHubQualityRevision']='8: manufactured service equipment; editable sources, atlas-printed labels, no vendor dependencies'
  bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(source))
 for prop in api.props:
  for obj in prop['objects']:
   mesh=obj.data;bpy.data.objects.remove(obj,do_unlink=True)
   if mesh.users==0:bpy.data.meshes.remove(mesh)
  bpy.data.collections.remove(prop['col'])
manifest['props'].extend(new.values());manifest['stage']=8
manifest['audit8Geometry']=dict(retainedProps=len(old_ids),newProps=list(new),lodMethod='Constructed ring density and bevel segments; no collapse decimation')
palette_names={entry['name'] for entry in manifest['palette']}
for name in sorted({'Hub8Labels'+chr(65+slot//32) for slot in labels.values()}):
 if name not in palette_names:
  manifest['palette'].append(dict(name=name,rgba=[1,1,1,1]));palette_names.add(name)
path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
(W/'label_atlas_entries.json').write_text(json.dumps([dict(prop=key[0],name=key[1],text=key[2],ink=key[3],index=index) for key,index in labels.items()],ensure_ascii=False,indent=2),encoding='utf8')
(W/'new_prop_geometry.json').write_text(json.dumps(dict(status='AUDIT8_NEW_PROP_SOURCE_OK',sourceRecords=source_records,props=list(new.values())),ensure_ascii=False,indent=2),encoding='utf8')
print('AUDIT8_NEW_PROP_SOURCE_OK',len(new),'new types; labels',len(labels),flush=True)
