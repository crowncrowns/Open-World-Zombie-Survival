from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np


ROOT = Path(__file__).resolve().parents[2]
MODEL_DIR = ROOT / "work" / "Benches_RU" / "UnityProject" / "Assets" / "Environment" / "Props" / "Benches_RU" / "Models"
REPORT = ROOT / "work" / "Benches_RU" / "Verification" / "BlenderReimport.json"
UV_BINS = 1024


def mesh_triangles(mesh):
    return sum(len(poly.vertices) - 2 for poly in mesh.polygons)


def cross2(a,b):
    return float(a[0]*b[1]-a[1]*b[0])


def polygon_area(poly):
    if len(poly)<3:
        return 0.0
    return abs(sum(poly[i][0]*poly[(i+1)%len(poly)][1]-poly[(i+1)%len(poly)][0]*poly[i][1] for i in range(len(poly)))*0.5)


def triangle_intersection_area(subject, clipper):
    output=[tuple(p) for p in subject]
    orientation=1.0 if cross2(clipper[1]-clipper[0],clipper[2]-clipper[0])>=0 else -1.0
    for edge_index in range(3):
        a=clipper[edge_index]
        b=clipper[(edge_index+1)%3]
        edge=b-a
        input_poly=output
        output=[]
        if not input_poly:
            break
        s=np.array(input_poly[-1],dtype=np.float64)
        s_inside=orientation*cross2(edge,s-a)>=-1e-10
        for raw_e in input_poly:
            e=np.array(raw_e,dtype=np.float64)
            e_inside=orientation*cross2(edge,e-a)>=-1e-10
            if e_inside != s_inside:
                direction=e-s
                denom=cross2(direction,edge)
                if abs(denom)>1e-14:
                    t=cross2(a-s,edge)/denom
                    point=s+direction*t
                    output.append((float(point[0]),float(point[1])))
            if e_inside:
                output.append((float(e[0]),float(e[1])))
            s,s_inside=e,e_inside
    return polygon_area(output)


def uv_overlap_audit(mesh, layer_name):
    layer = mesh.uv_layers.get(layer_name)
    if layer is None:
        return None
    mesh.calc_loop_triangles()
    triangles=[]
    ranges=[]
    triangle_polygons=[]
    triangle_vertices=[]
    bins={}
    for tri in mesh.loop_triangles:
        uv = np.array([layer.data[li].uv[:] for li in tri.loops], dtype=np.float64)
        p = uv
        xmin = max(0, min(UV_BINS-1, int(np.floor(np.min(p[:, 0])*UV_BINS))))
        xmax = max(0, min(UV_BINS-1, int(np.floor(np.max(p[:, 0])*UV_BINS))))
        ymin = max(0, min(UV_BINS-1, int(np.floor(np.min(p[:, 1])*UV_BINS))))
        ymax = max(0, min(UV_BINS-1, int(np.floor(np.max(p[:, 1])*UV_BINS))))
        if xmax < xmin or ymax < ymin:
            continue
        index=len(triangles)
        triangles.append(p)
        ranges.append((xmin,xmax,ymin,ymax))
        triangle_polygons.append(tri.polygon_index)
        triangle_vertices.append(tuple(tri.vertices))
        for y in range(ymin,ymax+1):
            for x in range(xmin,xmax+1):
                bins.setdefault((x,y),[]).append(index)
    candidate_count=0
    overlap_pairs=0
    overlap_area=0.0
    max_overlap_area=0.0
    meaningful_overlap_pairs=0
    largest_pairs=[]
    for (cell_x,cell_y),indices in bins.items():
        for i in range(len(indices)):
            for j in range(i+1,len(indices)):
                a,b=indices[i],indices[j]
                if a==b:
                    continue
                ra,rb=ranges[a],ranges[b]
                # Process a pair only in the lower-left cell of its shared UV bounds.
                if cell_x != max(ra[0],rb[0]) or cell_y != max(ra[2],rb[2]):
                    continue
                candidate_count+=1
                area=triangle_intersection_area(triangles[a],triangles[b])
                if area>1e-8:
                    overlap_pairs+=1
                    overlap_area+=area
                    max_overlap_area=max(max_overlap_area,area)
                    if area>1e-6:
                        meaningful_overlap_pairs+=1
                        shared_vertices=len(set(triangle_vertices[a]).intersection(triangle_vertices[b]))
                        largest_pairs.append((area,a,triangle_polygons[a],b,triangle_polygons[b],shared_vertices))
                        largest_pairs=sorted(largest_pairs,reverse=True)[:12]
    return {"candidatePairs":candidate_count,"overlapPairs":overlap_pairs,"overlapArea":round(overlap_area,10),
            "maxOverlapArea":round(max_overlap_area,10),"meaningfulOverlapPairsAt1e6":meaningful_overlap_pairs,
            "largestOverlapPairs": [[round(v,10) if i==0 else int(v) for i,v in enumerate(item)] for item in largest_pairs]}


def inspect_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), axis_forward="-Z", axis_up="Y", use_custom_normals=True)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    meshes.sort(key=lambda o: o.name)
    lod_map = {}
    for obj in meshes:
        lod = next((i for i in (0,1,2) if f"LOD{i}" in obj.name.upper()), None)
        if lod is not None:
            lod_map[lod] = obj
    if set(lod_map) != {0,1,2}:
        raise RuntimeError(f"{path.name}: expected LOD0/1/2; got {[o.name for o in meshes]}")
    lod0 = lod_map[0]
    world_points = [lod0.matrix_world @ v.co for v in lod0.data.vertices]
    mins = [min(p[i] for p in world_points) for i in range(3)]
    maxs = [max(p[i] for p in world_points) for i in range(3)]
    report = {
        "file": path.name,
        "lodTriangles": [mesh_triangles(lod_map[i].data) for i in range(3)],
        "dimensions": [round(maxs[i]-mins[i], 5) for i in range(3)],
        "min": [round(v, 5) for v in mins],
        "max": [round(v, 5) for v in maxs],
        "materials": sorted({m.name for obj in meshes for m in obj.data.materials if m}),
        "missingMaterialSlots": sum(1 for obj in meshes for m in obj.data.materials if m is None),
        "uvLayers": [list(l.name for l in lod_map[i].data.uv_layers) for i in range(3)],
        "uvLoops": [len(lod_map[i].data.uv_layers.active.data) if lod_map[i].data.uv_layers.active else 0 for i in range(3)],
        "vertexCounts": [len(lod_map[i].data.vertices) for i in range(3)],
        "faceCounts": [len(lod_map[i].data.polygons) for i in range(3)],
        "lod0Uv2OverlapAudit": uv_overlap_audit(lod0.data, "LightmapUV"),
        "allFinite": all(math.isfinite(float(c)) for obj in meshes for v in obj.data.vertices for c in v.co),
        "meshObjectCount": len(meshes),
    }
    report["lod1Ratio"] = round(report["lodTriangles"][1]/max(1,report["lodTriangles"][0]),4)
    report["lod2Ratio"] = round(report["lodTriangles"][2]/max(1,report["lodTriangles"][0]),4)
    report["groundContact"] = abs(mins[2]) <= 0.012
    expected_height = 0.38 <= report["dimensions"][2] <= 0.54 if path.name.startswith("Bench_RU_05_") else 0.72 <= report["dimensions"][2] <= 0.98
    report["dimensionsPlausible"] = 1.35 <= report["dimensions"][0] <= 1.95 and expected_height
    return report


def main():
    files = sorted(MODEL_DIR.glob("Bench_RU_*.fbx"))
    results = []
    for path in files:
        print("BLENDER_REIMPORT_START",path.name,flush=True)
        item=inspect_fbx(path)
        results.append(item)
        print("BLENDER_REIMPORT_DONE",path.name,"uv2",item["lod0Uv2OverlapAudit"],flush=True)
    errors = []
    if len(files) != 15:
        errors.append(f"Expected 15 FBX, found {len(files)}")
    for item in results:
        if not item["allFinite"]:
            errors.append(item["file"] + ": non-finite vertex found")
        if item["missingMaterialSlots"]:
            errors.append(item["file"] + ": empty material slot")
        if not item["groundContact"]:
            errors.append(item["file"] + f": pivot bottom offset {item['min'][2]:.5f}m")
        if not item["dimensionsPlausible"]:
            errors.append(item["file"] + ": implausible size " + str(item["dimensions"]))
        if item["lod1Ratio"] < 0.38 or item["lod1Ratio"] > 0.54:
            errors.append(item["file"] + f": LOD1 ratio {item['lod1Ratio']}")
        if item["lod2Ratio"] < 0.10 or item["lod2Ratio"] > 0.21:
            errors.append(item["file"] + f": LOD2 ratio {item['lod2Ratio']}")
        if any("LightmapUV" not in names for names in item["uvLayers"]):
            errors.append(item["file"] + ": LightmapUV missing from one LOD")
        if item["lod0Uv2OverlapAudit"] is None:
            errors.append(item["file"] + ": LightmapUV could not be inspected")
        elif item["lod0Uv2OverlapAudit"]["meaningfulOverlapPairsAt1e6"] > 0:
            errors.append(item["file"] + f": UV2 overlaps above 1e-6 atlas-area tolerance={item['lod0Uv2OverlapAudit']['meaningfulOverlapPairsAt1e6']}")
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps({"result":"PASS" if not errors else "FAIL", "fbxCount":len(files), "results":results, "errors":errors}, indent=2), encoding="utf-8")
    for item in results:
        print(f"FBX_REIMPORT {item['file']} tris={item['lodTriangles']} ratio=({item['lod1Ratio']},{item['lod2Ratio']}) dim={item['dimensions']} ground={item['min'][2]} uv2Audit={item['lod0Uv2OverlapAudit']}", flush=True)
    print("BLENDER_REIMPORT_" + ("PASS" if not errors else "FAIL") + " errors=" + json.dumps(errors), flush=True)
    if errors:
        raise RuntimeError("Blender FBX reimport validation failed")


if __name__ == "__main__":
    main()
