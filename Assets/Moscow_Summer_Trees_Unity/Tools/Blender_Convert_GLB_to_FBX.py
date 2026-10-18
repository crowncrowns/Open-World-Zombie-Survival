"""Blender background conversion for the tree pack.
Run from Blender's scripting workspace or:
blender --background --python Blender_Convert_GLB_to_FBX.py -- /path/to/Moscow_Summer_Trees_Unity
Export only LOD0/LOD1/LOD2 mesh roots to separate FBX, preserving Wind_* pivots.
"""
import bpy, pathlib, sys
if '--' in sys.argv:
    root=pathlib.Path(sys.argv[sys.argv.index('--')+1]).resolve()
else:
    root=pathlib.Path(bpy.path.abspath('//')).resolve()
print('PACK ROOT',root)
for lod in (0,1,2):
    destination=root/'Models_FBX'/f'LOD{lod}'
    destination.mkdir(parents=True, exist_ok=True)
    for model in sorted((root/'Models'/f'LOD{lod}').glob('*.glb')):
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.object.delete(use_global=False)
        bpy.ops.import_scene.gltf(filepath=str(model))
        bpy.ops.export_scene.fbx(
            filepath=str(destination/(model.stem+'.fbx')),
            use_selection=False,
            object_types={'MESH','EMPTY'},
            use_mesh_modifiers=True,
            apply_unit_scale=True,
            axis_forward='-Z', axis_up='Y',
            path_mode='COPY', embed_textures=True,
            add_leaf_bones=False,
            bake_anim=False)
        print('EXPORTED',model.name)
print('DONE')
