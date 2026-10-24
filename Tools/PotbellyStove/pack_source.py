import bpy
from pathlib import Path
root=Path(__file__).resolve().parents[2]
path=root/'Sources/PotbellyStove/PotbellyStove_Master.blend'
bpy.ops.wm.open_mainfile(filepath=str(path))
bpy.context.preferences.filepaths.save_version=0
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(path))
print('STOVE_SOURCE_PACKED')
