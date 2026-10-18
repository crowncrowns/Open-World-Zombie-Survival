import bpy, pathlib, sys, json, traceback, hashlib
from mathutils import Vector

base = pathlib.Path(__file__).resolve().parent
args = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
source = pathlib.Path(args[0]).resolve() if args and args[0]!='--diagnose' else base/'source/Moscow_Summer_Trees_Unity'
dest = pathlib.Path(args[1]).resolve() if len(args)>1 else base/'unity/Assets/MoscowTrees'
dest.mkdir(parents=True, exist_ok=True)

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def stats():
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    points = [o.matrix_world @ Vector(v) for o in meshes for v in o.bound_box]
    return dict(meshes=len(meshes), vertices=sum(len(o.data.vertices) for o in meshes),
                triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),
                uv_meshes=sum(bool(o.data.uv_layers) for o in meshes),
                pivots=sorted(o.name for o in bpy.context.scene.objects if o.name.startswith('Wind_')),
                bounds=[[min(p[i] for p in points) for i in range(3)], [max(p[i] for p in points) for i in range(3)]])

if '--diagnose' in sys.argv:
    old=next((base/'old').rglob('*.fbx'))
    try:
        bpy.ops.import_scene.fbx(filepath=str(old))
        print('OLD_IMPORT_OK', stats())
    except Exception:
        traceback.print_exc()
    sys.exit(0)

reports=[]
for model in sorted((source/'Models').rglob('*.glb')):
    reset()
    bpy.ops.import_scene.gltf(filepath=str(model))
    before=stats()
    out=dest/'Models_FBX'/model.parent.name/(model.stem+'.fbx')
    out.parent.mkdir(parents=True, exist_ok=True)
    # Export actual Blender scene, preserving all mesh and wind-pivot transforms.
    bpy.ops.export_scene.fbx(filepath=str(out), object_types={'MESH','EMPTY'},
        use_mesh_modifiers=True, apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
        path_mode='STRIP', embed_textures=False, add_leaf_bones=False, bake_anim=False)
    reset()
    bpy.ops.import_scene.fbx(filepath=str(out), use_anim=False)
    after=stats()
    for key in ('meshes','vertices','triangles','uv_meshes','pivots'):
        assert before[key]==after[key], (model.name,key,before[key],after[key])
    assert max(abs(before['bounds'][j][i]-after['bounds'][j][i]) for j in range(2) for i in range(3)) < 0.001, model.name
    reports.append(dict(model=model.name, source_sha256=hashlib.sha256(model.read_bytes()).hexdigest(),
                        fbx_sha256=hashlib.sha256(out.read_bytes()).hexdigest(), source=before, reimport=after))
    print('VERIFIED',len(reports),model.name,flush=True)
    (dest.parent.parent/'blender_validation.json').write_text(json.dumps(dict(blender=bpy.app.version_string,models=reports),indent=2))
assert len(reports)==96
print('ALL_96_VERIFIED',flush=True)
