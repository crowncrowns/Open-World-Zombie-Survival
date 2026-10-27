"""Read-only Blender source audit. Run with Blender --background --python ... --
--asset-root <work/.../Assets/PostApocalypticFurniture> --output <work/.../before_source.json>.
Open cloth/label boundaries and compound-mesh junctions are contextual review findings,
not automatic failures. UV0 tiling is legal; UV2 overlap is a bounded heuristic.
"""
import argparse
from collections import Counter, defaultdict
import hashlib
import json
import math
from pathlib import Path
import re
import sys
import time

import bpy

REPO = Path(__file__).resolve().parents[2]
WORK = (REPO / "work" / "PostApocalypticFurniture").resolve()
AREA_EPS = 1e-13
UV_EPS = 1e-16
OPEN_SURFACE_MATERIALS = {"Labels", "Paper", "Fabric", "RedFabric", "GreenFabric", "Leather"}


def inside(path, root):
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except ValueError:
        return False


def sha(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def finite(values):
    return all(math.isfinite(float(v)) for v in values)


def stats(values):
    values = sorted(v for v in values if math.isfinite(v))
    if not values:
        return {"samples": 0}
    return {"samples": len(values), "min": values[0], "median": values[len(values)//2],
            "p95": values[min(len(values)-1, math.ceil(len(values)*.95)-1)], "max": values[-1]}


def material_name(mesh, index):
    return mesh.materials[index].name if 0 <= index < len(mesh.materials) and mesh.materials[index] else "<missing>"


def intended_open(names):
    return bool(names) and all(n in OPEN_SURFACE_MATERIALS or n.startswith("Carpet") for n in names)


def cross2(a, b):
    return a[0]*b[1]-a[1]*b[0]


def tri_area2(points):
    return abs(cross2((points[1][0]-points[0][0], points[1][1]-points[0][1]),
                      (points[2][0]-points[0][0], points[2][1]-points[0][1]))) * .5


def intersection_area(subject, clip):
    # Convex triangle clipping; shared edges have zero area and are not overlaps.
    orientation = 1 if cross2((clip[1][0]-clip[0][0], clip[1][1]-clip[0][1]),
                             (clip[2][0]-clip[0][0], clip[2][1]-clip[0][1])) >= 0 else -1
    polygon = list(subject)
    for a, b in zip(clip, clip[1:]+clip[:1]):
        if not polygon:
            return 0.0
        edge = (b[0]-a[0], b[1]-a[1])
        def side(p):
            return orientation * cross2(edge, (p[0]-a[0], p[1]-a[1]))
        result = []
        previous = polygon[-1]
        previous_side = side(previous)
        for current in polygon:
            current_side = side(current)
            if (current_side >= 0) != (previous_side >= 0):
                denominator = previous_side-current_side
                if abs(denominator) > 1e-24:
                    t = previous_side/denominator
                    result.append((previous[0]+(current[0]-previous[0])*t,
                                   previous[1]+(current[1]-previous[1])*t))
            if current_side >= 0:
                result.append(current)
            previous, previous_side = current, current_side
        polygon = result
    if len(polygon) < 3:
        return 0.0
    return abs(sum(cross2(polygon[i], polygon[(i+1) % len(polygon)]) for i in range(len(polygon))))*.5


def uv2_overlap(triangles):
    bins = max(4, min(256, math.ceil(math.sqrt(max(1, len(triangles))))))
    grid = defaultdict(list)
    boxes = []
    candidates = set()
    for index, tri in enumerate(triangles):
        box = (min(p[0] for p in tri), min(p[1] for p in tri), max(p[0] for p in tri), max(p[1] for p in tri))
        boxes.append(box)
        if box[2] < 0 or box[3] < 0 or box[0] > 1 or box[1] > 1:
            continue
        lowx, lowy = max(0, int(box[0]*bins)), max(0, int(box[1]*bins))
        highx, highy = min(bins-1, int(box[2]*bins)), min(bins-1, int(box[3]*bins))
        # A malformed giant triangle must not allocate an unbounded grid.
        for x in range(lowx, highx+1):
            for y in range(lowy, highy+1):
                cell = grid[x, y]
                for other in cell:
                    prior=boxes[other]
                    if min(prior[2],box[2])-max(prior[0],box[0])>1e-12 and min(prior[3],box[3])-max(prior[1],box[1])>1e-12:
                        candidates.add((other, index))
                cell.append(index)
                if len(candidates) > 50000:
                    break
            if len(candidates) > 50000:
                break
        if len(candidates) > 50000:
            break
    truncated = len(candidates) > 50000
    overlap_count, overlap_area, examples = 0, 0.0, []
    checked = 0
    for a, b in sorted(candidates)[:50000]:
        boxa, boxb = boxes[a], boxes[b]
        if min(boxa[2], boxb[2])-max(boxa[0], boxb[0]) <= 1e-12 or min(boxa[3], boxb[3])-max(boxa[1], boxb[1]) <= 1e-12:
            continue
        checked += 1
        area = intersection_area(triangles[a], triangles[b])
        if area > 1e-12:
            overlap_count += 1
            overlap_area += area
            if len(examples) < 8:
                examples.append({"triangleA": a, "triangleB": b, "intersectionArea": area})
    return {"method": "UV triangle AABB spatial grid plus convex intersection; bounded to 50000 positive-AABB candidate pairs per mesh",
            "bins": bins, "candidatePairs": len(candidates), "testedPairs": checked,
            "truncated": truncated, "overlappingPairs": overlap_count, "summedIntersectionArea": overlap_area, "examples": examples}


def audit_mesh(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    triangles = list(mesh.loop_triangles)
    world = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    positions = [vertex.co.copy() for vertex in mesh.vertices]
    poly_count = len(mesh.polygons)
    parent = list(range(poly_count))
    def find(value):
        while parent[value] != value:
            parent[value] = parent[parent[value]]
            value = parent[value]
        return value
    def union(a, b):
        a, b = find(a), find(b)
        if a != b:
            parent[b] = a
    edge_faces = defaultdict(list)
    face_keys, geometric_keys = set(), set()
    duplicate_index, duplicate_geometry = [], []
    degenerate, nonfinite, zero_normals = [], [], []
    per_material = defaultdict(lambda: Counter())
    vertex_face_counts = Counter()
    for poly in mesh.polygons:
        name = material_name(mesh, poly.material_index)
        per_material[name]["faces"] += 1
        per_material[name]["smoothFaces" if poly.use_smooth else "flatFaces"] += 1
        ids = tuple(poly.vertices)
        key = tuple(sorted(ids))
        geo = tuple(sorted(tuple(round(q, 7) for q in positions[i]) for i in ids))
        if key in face_keys:
            duplicate_index.append(poly.index)
        if geo in geometric_keys:
            duplicate_geometry.append(poly.index)
        face_keys.add(key)
        geometric_keys.add(geo)
        if poly.area <= AREA_EPS:
            degenerate.append(poly.index)
            per_material[name]["degenerateFaces"] += 1
        if not finite(poly.normal) or poly.normal.length < .5:
            zero_normals.append(poly.index)
        for vertex in ids:
            vertex_face_counts[vertex] += 1
        for a, b in zip(ids, ids[1:]+ids[:1]):
            edge_faces[min(a,b), max(a,b)].append((poly.index, a < b))
    for vertex in mesh.vertices:
        if not finite(vertex.co):
            nonfinite.append(vertex.index)
    boundary, nonmanifold, inconsistent, smoothing_risks = [], [], [], []
    cos60 = math.cos(math.radians(60))
    sharp_edges = {tuple(sorted(e.vertices)) for e in mesh.edges if getattr(e, "use_edge_sharp", False)}
    for edge, links in edge_faces.items():
        for link in links[1:]:
            union(links[0][0], link[0])
        names = {material_name(mesh, mesh.polygons[index].material_index) for index, _ in links}
        if len(links) == 1:
            boundary.append(edge)
            for name in names:
                per_material[name]["boundaryEdges"] += 1
                per_material[name]["contextualOpenBoundaryEdges" if intended_open(names) else "boundaryEdgesForReview"] += 1
        elif len(links) > 2:
            nonmanifold.append(edge)
            for name in names:
                per_material[name]["multiFaceEdgesForReview"] += 1
        elif links[0][1] == links[1][1]:
            inconsistent.append(edge)
            for name in names:
                per_material[name]["windingConflictEdges"] += 1
        if len(links) == 2 and edge not in sharp_edges:
            p, q = (mesh.polygons[entry[0]] for entry in links)
            if p.use_smooth and q.use_smooth and p.normal.dot(q.normal) < cos60:
                smoothing_risks.append(edge)
                for name in names:
                    per_material[name]["smoothAcrossOver60DegreeEdge"] += 1
    components = defaultdict(lambda: {"faces": 0, "boundaryEdges": 0, "multiFaceEdges": 0, "signedVolumeLocal": 0.0, "materials": set()})
    for poly in mesh.polygons:
        component = components[find(poly.index)]
        component.setdefault("_reference", positions[poly.vertices[0]].copy())
        component["faces"] += 1
        component["materials"].add(material_name(mesh, poly.material_index))
    for edge, links in edge_faces.items():
        component = components[find(links[0][0])]
        if len(links) == 1:
            component["boundaryEdges"] += 1
        if len(links) > 2:
            component["multiFaceEdges"] += 1
    uv0 = mesh.uv_layers[0].data if mesh.uv_layers else None
    uv2 = mesh.uv_layers[1].data if len(mesh.uv_layers) > 1 else None
    anisotropy, density, uv2_triangles = [], [], []
    world_area, stretched_area, severe_area, uv0_degenerate, uv2_degenerate, uv2_outside = 0.0, 0.0, 0.0, 0, 0, 0
    density_by_material = defaultdict(list)
    uv0_bounds = [math.inf, math.inf, -math.inf, -math.inf]
    for tri in triangles:
        i, j, k = tri.vertices
        p, q, r = positions[i], positions[j], positions[k]
        component = components[find(tri.polygon_index)]
        reference = component["_reference"]
        # Translate each island near zero before the scalar triple product. Absolute
        # coordinates lose the sign of tiny brass/screw islands high above the origin.
        component["signedVolumeLocal"] += (p-reference).dot((q-reference).cross(r-reference))/6.0
        e1, e2 = world[j]-world[i], world[k]-world[i]
        length = e1.length
        area = e1.cross(e2).length*.5
        world_area += area
        name = material_name(mesh, mesh.polygons[tri.polygon_index].material_index)
        if uv0:
            coords = [tuple(uv0[li].uv) for li in tri.loops]
            for u, v in coords:
                uv0_bounds = [min(uv0_bounds[0],u), min(uv0_bounds[1],v), max(uv0_bounds[2],u), max(uv0_bounds[3],v)]
            uv_area = tri_area2(coords)
            if area > AREA_EPS and uv_area <= UV_EPS:
                uv0_degenerate += 1
                per_material[name]["uv0CollapsedTriangles"] += 1
            elif area > AREA_EPS and length > 1e-12:
                x2 = e2.dot(e1)/length
                y2 = 2*area/length
                du1, dv1 = coords[1][0]-coords[0][0], coords[1][1]-coords[0][1]
                du2, dv2 = coords[2][0]-coords[0][0], coords[2][1]-coords[0][1]
                a, c = du1/length, dv1/length
                b, d = (du2-du1*x2/length)/y2, (dv2-dv1*x2/length)/y2
                trace, determinant = a*a+b*b+c*c+d*d, abs(a*d-b*c)
                largest = .5*(trace+math.sqrt(max(0.0, trace*trace-4*determinant*determinant)))
                ratio = largest/max(determinant, 1e-30)
                anisotropy.append(ratio)
                density.append(uv_area/area)
                density_by_material[name].append(uv_area/area)
                if ratio > 4:
                    stretched_area += area
                    per_material[name]["uv0AnisotropyOver4Triangles"] += 1
                if ratio > 10:
                    severe_area += area
                    per_material[name]["uv0AnisotropyOver10Triangles"] += 1
        if uv2:
            coords2 = [tuple(uv2[li].uv) for li in tri.loops]
            uv2_triangles.append(coords2)
            if tri_area2(coords2) <= UV_EPS:
                uv2_degenerate += 1
            if any(u < -1e-6 or v < -1e-6 or u > 1.000001 or v > 1.000001 for u, v in coords2):
                uv2_outside += 1
    component_records = []
    inward_closed = 0
    for value in components.values():
        value.pop("_reference", None)
        value["materials"] = sorted(value["materials"])
        value["closedManifold"] = value["boundaryEdges"] == 0 and value["multiFaceEdges"] == 0
        value["openSurfaceContext"] = intended_open(value["materials"])
        if value["closedManifold"] and value["signedVolumeLocal"] < -1e-12:
            inward_closed += 1
        component_records.append(value)
    det = obj.matrix_world.to_3x3().determinant()
    overlap = uv2_overlap(uv2_triangles) if uv2 else None
    issues = []
    def issue(severity, code, count, message):
        if count:
            issues.append({"severity": severity, "code": code, "count": count, "message": message})
    issue("critical", "nonfinite_vertex", len(nonfinite), "Non-finite source coordinates")
    issue("critical", "degenerate_face", len(degenerate), "Polygon area <= 1e-13 square source metres")
    issue("critical", "duplicate_geometric_face", len(duplicate_geometry), "Coincident polygon vertex sets quantized to 1e-7m; includes faces with different vertex IDs")
    issue("critical", "invalid_face_normal", len(zero_normals), "Non-finite or zero-length polygon normal")
    issue("critical", "inward_closed_component", inward_closed, "Negative signed local volume on a closed manifold island")
    issue("critical", "missing_uv_layers", int(len(mesh.uv_layers) < 2), "Both UV0 and separate lightmap UV2 are required")
    issue("critical", "collapsed_uv0", uv0_degenerate, "Finite-area source triangle has collapsed UV0")
    issue("critical", "invalid_uv2", uv2_degenerate+uv2_outside, "UV2 collapsed or outside 0..1")
    issue("critical", "uv2_overlap", overlap["overlappingPairs"] if overlap else 0, "Positive-area overlap from bounded UV2 triangle intersection heuristic")
    issue("critical", "missing_material", per_material.get("<missing>", {}).get("faces", 0), "Polygon has no material")
    issue("review", "negative_determinant", int(det < 0), "Mirrored world transform; inspect final winding after export")
    issue("critical", "singular_transform", int(not math.isfinite(det) or abs(det) < 1e-12), "Singular/non-finite world transform")
    issue("review", "boundary_hard_surface", sum(v["boundaryEdgesForReview"] for v in per_material.values()), "Boundary on a non-cloth/label material; inspect intentional openings before judging")
    issue("review", "multi_face_junction", len(nonmanifold), "More than two faces meet at an edge; compound welded construction may be intentional")
    issue("review", "winding_conflict", len(inconsistent), "Two adjacent faces have the same directed shared edge")
    issue("review", "smooth_hard_edge", len(smoothing_risks), "Two smooth faces cross an unmarked edge over 60 degrees")
    issue("review", "uv0_surface_stretch", int(world_area > 0 and severe_area/world_area > .05), "Over 5% of surface area has UV0 singular-value anisotropy above 10")
    issue("review", "uv2_heuristic_truncated", int(overlap and overlap["truncated"]), "Overlap candidate budget reached; no complete non-overlap claim")
    for name, values in per_material.items():
        values["uv0AreaRatio"] = stats(density_by_material[name])
    return {"object": obj.name, "mesh": mesh.name, "vertices": len(mesh.vertices), "polygons": poly_count, "triangles": len(triangles),
            "materials": {name: dict(value) for name, value in per_material.items()}, "determinantWorld": det,
            "scale": list(obj.scale), "worldBoundsMin": [min(p[i] for p in world) for i in range(3)] if world else None,
            "worldBoundsMax": [max(p[i] for p in world) for i in range(3)] if world else None,
            "topology": {"boundaryEdges": len(boundary), "multiFaceEdges": len(nonmanifold), "windingConflictEdges": len(inconsistent),
                         "wireEdges": sum(tuple(sorted(e.vertices)) not in edge_faces for e in mesh.edges),
                         "looseVertices": sum(vertex_face_counts[v.index] == 0 for v in mesh.vertices),
                         "duplicateIndexFaces": len(duplicate_index), "duplicateGeometryFaces": len(duplicate_geometry), "degenerateFaces": len(degenerate),
                         "components": component_records},
            "normals": {"smoothFaces": sum(p.use_smooth for p in mesh.polygons), "flatFaces": sum(not p.use_smooth for p in mesh.polygons),
                        "sharpEdges": len(sharp_edges), "smoothAcrossOver60DegreeEdges": len(smoothing_risks), "invalidFaceNormals": len(zero_normals)},
            "uv0": {"bounds": uv0_bounds if uv0 else None, "worldSurfaceArea": world_area, "collapsedTriangles": uv0_degenerate,
                    "anisotropy": stats(anisotropy), "uvAreaPerSquareMetre": stats(density),
                    "areaFractionAnisotropyOver4": stretched_area/world_area if world_area else 0,
                    "areaFractionAnisotropyOver10": severe_area/world_area if world_area else 0,
                    "note": "UV0 outside 0..1 is legal tiling; anisotropy is independent of texture density"},
            "uv2": {"layers": [l.name for l in mesh.uv_layers], "collapsedTriangles": uv2_degenerate, "outsideTriangles": uv2_outside, "overlapHeuristic": overlap},
            "modifiers": [{"name": m.name, "type": m.type, "render": m.show_render} for m in obj.modifiers], "issues": issues}


def audit_file(path):
    digest = sha(path)
    bpy.ops.wm.open_mainfile(filepath=str(path))
    roots = [o for o in bpy.data.objects if o.parent is None and o.name.startswith("F")]
    objects = []
    assets = []
    for root in sorted(roots, key=lambda o: o.name):
        children = [o for o in root.children_recursive if o.type == "MESH"]
        lods = [0, 0, 0]
        lod_meshes = [0, 0, 0]
        names = []
        for obj in children:
            record = audit_mesh(obj)
            objects.append(record)
            names.append(obj.name)
            match = re.search(r"_LOD([012])(?:\.\d+)?$", obj.name)
            if match:
                level = int(match.group(1))
                lods[level] += record["triangles"]
                lod_meshes[level] += 1
        points = [obj.matrix_world @ vertex.co for obj in children if re.search(r"_LOD0(?:\.\d+)?$", obj.name) for vertex in obj.data.vertices]
        assets.append({"asset": root.name, "assetId": root.get("asset_id"), "nameRu": root.get("name_ru"), "triangles": lods,
                       "lodMeshes": lod_meshes, "scale": list(root.scale), "determinant": root.matrix_world.to_3x3().determinant(),
                       "boundsMin": [min(p[i] for p in points) for i in range(3)] if points else None,
                       "boundsMax": [max(p[i] for p in points) for i in range(3)] if points else None,
                       "objects": names})
    materials = []
    missing_textures = []
    for mat in sorted(bpy.data.materials, key=lambda m: m.name):
        textures = []
        if mat.use_nodes and mat.node_tree:
            for node in mat.node_tree.nodes:
                if node.type != "TEX_IMAGE" or node.image is None:
                    continue
                image = node.image
                resolved = Path(bpy.path.abspath(image.filepath))
                exists = bool(image.packed_file) or resolved.is_file()
                textures.append({"node": node.name, "image": image.name, "path": image.filepath, "resolved": str(resolved),
                                 "exists": exists, "colorSpace": image.colorspace_settings.name, "packed": bool(image.packed_file)})
                if image.source == "FILE" and not exists:
                    missing_textures.append({"material": mat.name, "image": image.name, "path": str(resolved)})
        materials.append({"name": mat.name, "users": mat.users, "textures": textures})
    issues = []
    if len(roots) != 10:
        issues.append({"severity": "critical", "code": "root_count", "count": len(roots), "message": "Expected ten independently editable furniture roots"})
    if missing_textures:
        issues.append({"severity": "critical", "code": "missing_texture", "count": len(missing_textures), "message": "Material file texture path does not resolve"})
    for asset in assets:
        if any(count == 0 for count in asset["lodMeshes"]) or asset["triangles"][0] > 18000:
            issues.append({"severity": "critical", "code": "lod_or_budget", "asset": asset["asset"], "message": "Missing LOD or LOD0 exceeds 18000 triangles"})
    if sha(path) != digest:
        raise RuntimeError("Source file changed while auditing: "+str(path))
    return {"file": str(path), "sha256": digest, "readOnlyHashUnchanged": True, "roots": len(roots), "meshObjects": len(objects),
            "unitSystem": bpy.context.scene.unit_settings.system, "metresPerBlenderUnit": bpy.context.scene.unit_settings.scale_length,
            "axes": "Editable Blender sources are Z-up; Unity author metadata is Y-up", "assets": assets, "objects": objects,
            "materials": materials, "missingTextures": missing_textures, "issues": issues}


def main():
    args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--asset-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    options = parser.parse_args(args)
    asset_root, output = options.asset_root.resolve(), options.output.resolve()
    if not inside(asset_root, WORK) or not inside(output, WORK):
        raise RuntimeError("Audit input and output must stay under work/PostApocalypticFurniture")
    files = sorted((asset_root/"SourceBlender"/"Source~").glob("*.blend"))
    if len(files) != 20:
        raise RuntimeError("Expected exactly twenty source .blend files; found "+str(len(files)))
    start = time.time()
    report = {"schema": 1, "blender": bpy.app.version_string, "assetRoot": str(asset_root),
              "scope": "Read-only source geometry/material/UV audit. No visual fidelity, Unity import, gameplay or performance acceptance is implied.",
              "policy": {"degenerateAreaSquareMetres": AREA_EPS, "duplicateCoordinateQuantizationMetres": 1e-7,
                         "contextualOpenMaterials": sorted(OPEN_SURFACE_MATERIALS)+["Carpet*"],
                         "uv0Stretch": "Per-triangle 3D-to-UV Jacobian singular-value ratio; tiled UV0 is allowed",
                         "uv2Overlap": "Bounded spatial-grid triangle intersection heuristic, not a universal exact atlas proof"}, "files": []}
    for path in files:
        record = audit_file(path)
        report["files"].append(record)
        critical = sum(i["severity"] == "critical" for i in record["issues"])
        critical += sum(i["severity"] == "critical" for obj in record["objects"] for i in obj["issues"])
        print("PROFESSIONAL_SOURCE", path.name, "roots", record["roots"], "meshes", record["meshObjects"], "critical", critical, flush=True)
    issues = [issue for file in report["files"] for issue in file["issues"]]
    issues += [issue for file in report["files"] for obj in file["objects"] for issue in obj["issues"]]
    report["summary"] = {"files": len(files), "assets": sum(f["roots"] for f in report["files"]),
                         "assetLodGroups": sum(sum(c > 0 for c in a["lodMeshes"]) for f in report["files"] for a in f["assets"]),
                         "meshObjects": sum(f["meshObjects"] for f in report["files"]),
                         "criticalIssueRecords": sum(i["severity"] == "critical" for i in issues),
                         "reviewIssueRecords": sum(i["severity"] == "review" for i in issues),
                         "issueRecordsByCode": dict(Counter(i["code"] for i in issues)), "elapsedSeconds": time.time()-start}
    report["passed"] = report["summary"]["assets"] == 200 and report["summary"]["assetLodGroups"] == 600 and report["summary"]["criticalIssueRecords"] == 0
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False), encoding="utf-8")
    print("PROFESSIONAL_SOURCE_"+("TECHNICAL_PASS" if report["passed"] else "FINDINGS"), json.dumps(report["summary"]), flush=True)


if __name__ == "__main__":
    main()
