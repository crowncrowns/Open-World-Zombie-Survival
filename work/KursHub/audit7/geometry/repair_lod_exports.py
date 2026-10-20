"""Restore failed FBX chunks from the preceding verified level, then re-export."""
import bpy, json, pathlib, shutil, hashlib

OUT=pathlib.Path(__file__).resolve().parent
PACKAGE=OUT/'output'
manifest_path=PACKAGE/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json'
manifest=json.loads(manifest_path.read_text(encoding='utf8'))
report=json.loads((OUT/'fbx_roundtrip.json').read_text(encoding='utf8'))
props={p['id']:p for p in manifest['props']}
fixes=[]
for failure in sorted(report['errors'],key=lambda f:(f['id'],f['level'])):
    assert failure['level']>0,'LOD0 source must never be replaced with a lower detail asset'
    prop=props[failure['id']]
    current=next(l for l in prop['lods'] if l['level']==failure['level'])
    previous=next(l for l in prop['lods'] if l['level']==failure['level']-1)
    chunks=[i['chunk'] for i in failure['issues'] if isinstance(i,dict)]
    if not chunks:continue
    path=PACKAGE/current['model']
    backup=OUT/'rejected_fbx'/path.name
    backup.parent.mkdir(parents=True,exist_ok=True)
    if not backup.exists():shutil.copy2(path,backup)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(PACKAGE/previous['model']))
    replacement={name:bpy.data.objects[name].data.copy() for name in chunks}
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    for name,data in replacement.items():
        obj=bpy.data.objects[name]
        obj.data=data
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    triangles=0
    for obj in objects:
        obj.data.calc_loop_triangles();triangles+=len(obj.data.loop_triangles)
    assert triangles<previous['triangles'],(prop['id'],current,previous,triangles)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'EMPTY','MESH'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,
        use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
    fixes.append(dict(id=prop['id'],level=current['level'],fromLevel=previous['level'],
        restoredChunks=chunks,oldTriangles=current['triangles'],triangles=triangles,
        beforeSHA256=hashlib.sha256(backup.read_bytes()).hexdigest(),afterSHA256=hashlib.sha256(path.read_bytes()).hexdigest(),
        reason='Actual FBX roundtrip exposed collapsed nonmanifold small solids; restore only failed chunks from validated previous LOD'))
    current['triangles']=triangles
    current['roundtripRestoredChunks']=chunks
    print('REPAIRED',prop['id'],current['level'],chunks,triangles,flush=True)
manifest['audit7LODCorrection']='Defective small decimated solids detected by actual roundtrip are restored from the preceding safe LOD, preserving all chunk identities and descending triangle counts.'
manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
(OUT/'lods.json').write_text(json.dumps([dict(id=p['id'],levels=p['lods']) for p in manifest['props']],ensure_ascii=False,indent=2),encoding='utf8')
(OUT/'lod_export_repairs.json').write_text(json.dumps(dict(status='AUDIT7_LOD_REPAIRS_EXPORTED',fixes=fixes),ensure_ascii=False,indent=2),encoding='utf8')
print('REPAIRS_EXPORTED',len(fixes),'levels',sum(len(f['restoredChunks']) for f in fixes),'chunks',flush=True)
