from pathlib import Path
import math
import sys
import bpy

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools" / "Benches_RU"))
from verify_fbx import uv_overlap_audit
blend = ROOT / "work" / "Benches_RU" / "UnityProject" / "Assets" / "Environment" / "Props" / "Benches_RU" / "Source" / "Benches_RU_Master.blend"
bpy.ops.wm.open_mainfile(filepath=str(blend))
col = bpy.data.collections["Bench_RU_01_Wear01"].children["Authoring_Parts"]
obj = next(o for o in col.objects if o.type == "MESH" and "Cast iron side cheek" in o.name)
obj = obj.copy()
obj.data = obj.data.copy()
bpy.context.scene.collection.objects.link(obj)
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
for layer in list(obj.data.uv_layers): obj.data.uv_layers.remove(layer)
obj.data.uv_layers.new(name="UVMap")
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(20), island_margin=0.01, area_weight=0.1, rotate_method="AXIS_ALIGNED")
bpy.ops.object.mode_set(mode="OBJECT")
print("CAST_PART", obj.name, "faces", len(obj.data.polygons), "audit", uv_overlap_audit(obj.data, "UVMap"), flush=True)
