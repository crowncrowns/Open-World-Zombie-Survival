from pathlib import Path
import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
fbx = ROOT / "work" / "Benches_RU" / "UnityProject" / "Assets" / "Environment" / "Props" / "Benches_RU" / "Models" / "Bench_RU_01_Wear01.fbx"
bpy.ops.import_scene.fbx(filepath=str(fbx), axis_forward="-Z", axis_up="Y")
obj = next(o for o in bpy.context.scene.objects if o.type == "MESH" and "LOD0" in o.name.upper())
mesh = obj.data
mesh.calc_loop_triangles()
layer = mesh.uv_layers.get("LightmapUV")
triangles = [np.array([layer.data[li].uv[:],], dtype=np.float64) for li in []]
triangles = [np.array([layer.data[li].uv[:] for li in tri.loops], dtype=np.float64) for tri in mesh.loop_triangles]

for bins_n in (64, 128, 256, 512, 1024):
    bins = {}
    ranges = []
    for index, p in enumerate(triangles):
        xmin = max(0, min(bins_n - 1, int(np.floor(np.min(p[:, 0]) * bins_n))))
        xmax = max(0, min(bins_n - 1, int(np.floor(np.max(p[:, 0]) * bins_n))))
        ymin = max(0, min(bins_n - 1, int(np.floor(np.min(p[:, 1]) * bins_n))))
        ymax = max(0, min(bins_n - 1, int(np.floor(np.max(p[:, 1]) * bins_n))))
        if xmax < xmin or ymax < ymin:
            ranges.append(None)
            continue
        ranges.append((xmin, xmax, ymin, ymax))
        for y in range(ymin, ymax + 1):
            for x in range(xmin, xmax + 1):
                bins.setdefault((x, y), []).append(index)
    candidates = 0
    for (cx, cy), indices in bins.items():
        for i in range(len(indices)):
            a = indices[i]
            ra = ranges[a]
            for j in range(i + 1, len(indices)):
                b = indices[j]
                if a == b:
                    continue
                rb = ranges[b]
                if cx != max(ra[0], rb[0]) or cy != max(ra[2], rb[2]):
                    continue
                candidates += 1
    print("UV_GRID_PROFILE", bins_n, "triangles", len(triangles), "cells", len(bins), "candidatePairs", candidates, flush=True)
