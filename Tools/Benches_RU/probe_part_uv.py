from pathlib import Path
import sys
import bpy

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools" / "Benches_RU"))
from verify_fbx import uv_overlap_audit
BLEND = ROOT / "work" / "Benches_RU" / "UnityProject" / "Assets" / "Environment" / "Props" / "Benches_RU" / "Source" / "Benches_RU_Master.blend"
bpy.ops.wm.open_mainfile(filepath=str(BLEND))
col = bpy.data.collections["Bench_RU_01_Wear01"].children["Authoring_Parts"]
targets = sorted((o for o in col.objects if o.type == "MESH" and any(m and "WoodPaint" in m.name for m in o.data.materials)), key=lambda o: len(o.data.polygons), reverse=True)[:8]

def overlap_count(obj, layer_name="UVMap"):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    obj.data.uv_layers.active_index = obj.data.uv_layers.find(layer_name)
    bpy.context.scene.tool_settings.use_uv_select_sync = True
    bpy.context.tool_settings.mesh_select_mode = (False, False, True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.uv.select_overlap(extend=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return [p.index for p in obj.data.polygons if p.select]

for source in targets:
    obj = source.copy()
    obj.data = source.data.copy()
    bpy.context.scene.collection.objects.link(obj)
    obj.name = "UVProbe_" + source.name
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    for layer in list(obj.data.uv_layers):
        obj.data.uv_layers.remove(layer)
    obj.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(island_margin=0.010, area_weight=0.1, rotate_method="AXIS_ALIGNED")
    bpy.ops.object.mode_set(mode="OBJECT")
    before_pack = overlap_count(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=0.025)
    bpy.ops.object.mode_set(mode="OBJECT")
    after_pack = overlap_count(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    result = bpy.ops.uv.lightmap_pack(PREF_CONTEXT="ALL_FACES", PREF_PACK_IN_ONE=True, PREF_NEW_UVLAYER=True, PREF_BOX_DIV=12, PREF_MARGIN_DIV=0.1)
    bpy.ops.object.mode_set(mode="OBJECT")
    lightmap = obj.data.uv_layers[-1]
    lightmap.name = "LightmapUV"
    after_lightmap = overlap_count(obj, "LightmapUV")
    layer_names = [ascii(layer.name) for layer in obj.data.uv_layers]
    samples = [[tuple(round(float(c), 4) for c in layer.data[0].uv) for layer in obj.data.uv_layers]
               for _ in (0,)]
    audit_uv = uv_overlap_audit(obj.data, "UVMap")
    audit_lightmap = uv_overlap_audit(obj.data, "LightmapUV")
    payload = [source.name, len(obj.data.polygons), len(before_pack), len(after_pack), len(after_lightmap), before_pack[:12], after_lightmap[:12], layer_names, samples, audit_uv, audit_lightmap, ascii(result)]
    print("PART_UV", ascii(payload), flush=True)
    bpy.data.objects.remove(obj, do_unlink=True)
    break
