from pathlib import Path
import os
import bpy

root = Path(__file__).resolve().parents[2]
asset = root / "work/Benches_RU/UnityProject/Assets/Environment/Props/Benches_RU"
source = asset / "Source~/Benches_RU_Master.blend"
bpy.ops.wm.open_mainfile(filepath=str(source))
for image in bpy.data.images:
    if image.source == "FILE":
        image.filepath = "//../Textures/" + Path(image.filepath).name
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
print("BENCH_SOURCE_RELATIVE_PATHS_SAVED", flush=True)
os._exit(0)
