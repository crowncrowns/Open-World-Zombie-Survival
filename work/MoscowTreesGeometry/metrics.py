import bpy,pathlib,json
root=pathlib.Path(__file__).resolve().parent; items=[]
for f in sorted((root/'Models_FBX').rglob('*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=str(f),use_anim=False)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    from mathutils import Vector
    points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
    items.append(dict(name=f.stem,meshes=len(meshes),triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),height=max(v.z for v in points)-min(v.z for v in points)))
(root/'expected_metrics.json').write_text(json.dumps(dict(items=items)))
