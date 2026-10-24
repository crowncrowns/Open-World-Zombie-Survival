import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
for p in (ROOT/'Sources/LawnFence').glob('*.blend'):
    bpy.ops.wm.open_mainfile(filepath=str(p))
    for im in bpy.data.images:
        path=im.filepath.replace('\\','/')
        if '/Textures/' in path:im.filepath='//../../Assets/Environment/Fences/Textures/'+path.split('/Textures/')[-1]
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(p),relative_remap=False)
print('PORTABLE_LAWN_FENCE_SOURCES_READY')
