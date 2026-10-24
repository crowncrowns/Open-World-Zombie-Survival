from pathlib import Path
import bpy

ROOT = Path(__file__).resolve().parents[2]
fbx = ROOT / "work" / "Benches_RU" / "UnityProject" / "Assets" / "Environment" / "Props" / "Benches_RU" / "Models" / "Bench_RU_01_Wear01.fbx"
bpy.ops.import_scene.fbx(filepath=str(fbx), axis_forward="-Z", axis_up="Y")
obj = next(o for o in bpy.context.scene.objects if o.type == "MESH" and "LOD0" in o.name.upper())
mesh = obj.data
mesh.calc_loop_triangles()
layer = mesh.uv_layers.get("LightmapUV")
for tri_idx in (12410, 12516, 12642, 12748):
    tri = mesh.loop_triangles[tri_idx]
    poly = mesh.polygons[tri.polygon_index]
    material = mesh.materials[poly.material_index].name if poly.material_index < len(mesh.materials) else "<none>"
    uv = [tuple(round(float(c), 7) for c in layer.data[li].uv) for li in tri.loops]
    xyz = [tuple(round(float(c), 5) for c in (obj.matrix_world @ mesh.vertices[vi].co)) for vi in tri.vertices]
    print("UV_PAIR_TRI", tri_idx, "poly", tri.polygon_index, "mat", material, "vertices", tuple(tri.vertices), "uv", uv, "xyz", xyz, flush=True)
