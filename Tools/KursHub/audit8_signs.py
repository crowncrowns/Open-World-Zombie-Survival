"""Replace fractured mesh lettering with raster printing on the actual fabric.

Run only on a staging root, after the revision 8 props integration.  ``apply``
updates both editable sources and the 21 existing sign FBXs.  Every FBX Model
UID/name and connection is retained; untouched cloth/cord/eyelet geometry arrays
are retained byte-for-byte.  Original glyph objects survive, hidden, in an
explicit source archive.  No canonical project or Unity scene is written.

The texture-only CLI uses the main Python (Pillow/NumPy); Blender imports the
same module without importing either optional package.  No image service or
downloaded/third-party art is used.
"""

from pathlib import Path
from types import SimpleNamespace
import argparse
import array
import hashlib
import json
import math
import os
import shutil
import subprocess
import sys


PROJECT = Path(__file__).resolve().parents[2]
RUNTIME_PYTHON = Path(
    "C:/Users/Alex/.cache/codex-runtimes/codex-primary-runtime/"
    "dependencies/python/python.exe")
HUB = Path("Assets/Environment/KursHub")
SIGNS = {
    "KH_KursAtriumBanner": dict(size=(4.5, 6.), family="Hub8SignKurs",
                                canvas="CanvasBlue", ink="ClothInk"),
    "KH_SignWeapons": dict(size=(4., .95), family="Hub8SignWeapons",
                           canvas="CanvasDark", ink="ClothInk"),
    "KH_SignMedicalTrade": dict(size=(4., 1.3), family="Hub8SignMedical",
                                canvas="CanvasBeige", ink="InkDark"),
    "KH_SignGear": dict(size=(4., 1.25), family="Hub8SignGear",
                        canvas="CanvasDark", ink="ClothInk"),
    "KH_SignRepair": dict(size=(3., 1.5), family="Hub8SignRepair",
                          canvas="CanvasDark", ink="ClothInk"),
    "KH_CheckpointHeader": dict(size=(6., 1.4), family="Hub8SignCheckpoint",
                                canvas="CanvasDark", ink="ClothInk"),
    "KH_CheckpointRules": dict(size=(2.6, 1.6), family="Hub8SignRules",
                               canvas="CanvasDark", ink="ClothInk"),
}
MAPS = ("BaseColor", "Normal", "Roughness", "Metallic", "Occlusion",
        "Height", "MetallicSmoothness")


def _json(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n",
                    encoding="utf-8")


def _sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _guard(root):
    root = Path(root).resolve()
    if root == PROJECT or not (root / HUB / "KursHubManifest.json").is_file():
        raise ValueError("A separate complete staging project is required")
    return root


def _backup(path, root, work):
    target = work / "before" / path.relative_to(root)
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists():
        shutil.copy2(path, target)


def _font_path():
    choices = (Path("C:/Windows/Fonts/arialbd.ttf"),
               Path("C:/Windows/Fonts/calibrib.ttf"),
               Path("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"))
    for p in choices:
        if p.is_file():
            return p
    raise FileNotFoundError("A real Cyrillic font is required; no fake glyphs")


def _design(identifier, width, height):
    """Draw in physical aspect first; square exchange UV reverses the resize."""
    from PIL import Image, ImageDraw, ImageFont
    spec = SIGNS[identifier]
    physical_w, physical_h = spec["size"]
    # A square texture should not make 4:1 signs carry 4:1 stretched letters.
    longest = 3072
    w = max(256, round(longest * physical_w / max(physical_w, physical_h)))
    h = max(256, round(longest * physical_h / max(physical_w, physical_h)))
    base = ((29, 49, 72) if identifier == "KH_KursAtriumBanner" else
            (192, 181, 156) if identifier == "KH_SignMedicalTrade" else
            (37, 37, 32))
    ink = (212, 205, 182)
    dark = (38, 35, 29)
    red = (135, 54, 41)
    image = Image.new("RGB", (w, h), base)
    d = ImageDraw.Draw(image)
    fontpath = str(_font_path())

    def rect(r):
        return tuple(round(c * (w if i % 2 == 0 else h))
                     for i, c in enumerate(r))

    def words(text, bounds, color=ink, align="center"):
        x0, y0, x1, y1 = rect(bounds)
        target_w, target_h = x1 - x0, y1 - y0
        size = max(6, target_h)
        while size > 6:
            f = ImageFont.truetype(fontpath, size)
            bb = d.textbbox((0, 0), text, font=f)
            if bb[2] - bb[0] <= target_w and bb[3] - bb[1] <= target_h:
                break
            size -= 1
        x = x0 if align == "left" else x0 + (target_w - bb[2] + bb[0]) / 2
        y = y0 + (target_h - bb[3] + bb[1]) / 2 - bb[1]
        d.text((x, y), text, font=f, fill=color, stroke_width=0)

    def symbol(kind, bounds, color=ink):
        x0, y0, x1, y1 = rect(bounds)
        ww, hh = x1 - x0, y1 - y0

        def p(x, y):
            return (x0 + x * ww, y0 + y * hh)

        def poly(points, fill=color):
            d.polygon([p(x, y) for x, y in points], fill=fill)

        stroke = max(2, round(min(ww, hh) * .045))
        if kind == "bird":
            # Authored interpretation of the reference bird/sail emblem.
            poly([(.50, .57), (.36, .45), (.17, .08), (.19, .44),
                  (.05, .31), (.16, .60), (.40, .72), (.27, .86),
                  (.06, .92), (.39, .94), (.55, .77), (.67, .74),
                  (.81, .58), (.69, .60), (.65, .50), (.67, .16),
                  (.54, .37)])
            poly([(.75, .53), (.85, .05), (.98, .60), (.84, .80),
                  (.65, .86), (.82, .64)], red)
        elif kind == "cross":
            poly([(.37, .06), (.63, .06), (.63, .36), (.94, .36),
                  (.94, .63), (.63, .63), (.63, .94), (.37, .94),
                  (.37, .63), (.06, .63), (.06, .36), (.37, .36)], red)
        elif kind == "pack":
            d.rounded_rectangle((p(.21, .25), p(.79, .94)),
                                radius=min(ww, hh) * .09, outline=color,
                                width=stroke)
            d.arc((p(.34, .04), p(.66, .41)), 180, 360,
                  fill=color, width=stroke)
            d.rounded_rectangle((p(.31, .59), p(.69, .88)),
                                radius=min(ww, hh) * .035, outline=color,
                                width=max(2, stroke // 2))
            for xx in (.34, .66):
                d.line([p(xx, .30), p(xx, .56)], fill=color, width=stroke)
        elif kind == "rifles":
            gun = [(.04, .41), (.23, .43), (.30, .40), (.39, .40),
                   (.39, .34), (.63, .34), (.67, .37), (.93, .37),
                   (.94, .34), (.98, .34), (.98, .43), (.67, .44),
                   (.62, .47), (.62, .55), (.67, .66), (.64, .76),
                   (.56, .73), (.54, .61), (.53, .52), (.47, .52),
                   (.42, .66), (.38, .65), (.40, .51), (.31, .51),
                   (.22, .58), (.05, .62)]
            for angle in (-.66, .66):
                points = []
                for x, y in gun:
                    x, y = (x - .5), (y - .5)
                    points.append((.5 + x * math.cos(angle) - y * math.sin(angle),
                                   .5 + x * math.sin(angle) + y * math.cos(angle)))
                poly(points)
        elif kind == "tools":
            d.line([p(.17, .84), p(.78, .20)], fill=color,
                   width=stroke * 2)
            poly([(.68, .08), (.91, .18), (.91, .33), (.79, .30),
                  (.73, .22), (.63, .22)])
            d.line([p(.83, .85), p(.24, .23)], fill=color,
                   width=stroke * 2)
            poly([(.08, .13), (.35, .08), (.43, .25), (.29, .36),
                  (.13, .29)])

    if identifier == "KH_KursAtriumBanner":
        symbol("bird", (.12, .10, .88, .44))
        words("Курс", (.09, .465, .91, .655))
        words("торговый центр", (.10, .682, .90, .757))
    elif identifier == "KH_SignWeapons":
        symbol("rifles", (.025, .12, .23, .88))
        words("ОРУЖИЕ", (.255, .20, .965, .80))
    elif identifier == "KH_SignMedicalTrade":
        symbol("cross", (.035, .20, .21, .79))
        for text, yy in (("МЕДИКАМЕНТЫ", .10), ("ПРОДУКТЫ", .38),
                         ("ОБМЕН", .66)):
            words(text, (.24, yy, .965, yy + .22), dark)
    elif identifier == "KH_SignGear":
        symbol("pack", (.035, .12, .225, .89))
        for text, yy in (("СНАРЯЖЕНИЕ", .10), ("РЮКЗАКИ", .38),
                         ("ИНСТРУМЕНТЫ", .66)):
            words(text, (.25, yy, .965, yy + .22))
    elif identifier == "KH_SignRepair":
        symbol("tools", (.035, .16, .255, .80))
        words("РЕМОНТ", (.27, .10, .965, .34))
        for text, yy in (("СНАРЯЖЕНИЕ", .43), ("ОДЕЖДА / ОБУВЬ", .60),
                         ("ИНСТРУМЕНТЫ", .77)):
            words(text, (.27, yy, .965, yy + .13))
    elif identifier == "KH_CheckpointHeader":
        symbol("bird", (.025, .14, .235, .89))
        words("ГЛАВНЫЙ ВХОД", (.25, .13, .975, .43))
        words("КОНТРОЛЬНЫЙ ПУНКТ", (.25, .55, .975, .85))
    elif identifier == "KH_CheckpointRules":
        for text, yy in (("ПРОВЕРКА ВЕЩЕЙ", .14),
                         ("РЕГИСТРАЦИЯ ПОСЕТИТЕЛЕЙ", .43),
                         ("СОБЛЮДЕНИЕ ПОРЯДКА", .72)):
            x0, y0, x1, y1 = rect((.045, yy + .015, .12, yy + .15))
            d.rectangle((x0, y0, x1, y1), outline=ink,
                        width=max(2, int(h * .007)))
            d.line([(x0 + (x1 - x0) * .17, y0 + (y1 - y0) * .54),
                    (x0 + (x1 - x0) * .40, y0 + (y1 - y0) * .78),
                    (x0 + (x1 - x0) * .91, y0 + (y1 - y0) * .12)],
                   fill=ink, width=max(2, int(h * .008)))
            words(text, (.16, yy, .965, yy + .18), align="left")
    return image.resize((width, height), Image.Resampling.LANCZOS), base


def textures(root, work):
    """Generate seven physical printed-cloth PBR families, 49 real PNGs."""
    import numpy as np
    from PIL import Image, ImageFilter
    sys.path.insert(0, str(Path(__file__).parent))
    from audit7_surfaces import normal_map, record, write_png
    root = _guard(root)
    work = Path(work).resolve()
    folder = root / HUB / "Textures"
    manifest_path = folder / "Audit7PBRManifest.json"
    _backup(manifest_path, root, work)
    pbr = json.loads(manifest_path.read_text(encoding="utf-8"))
    families, records, previews = [], [], []
    n = 2048
    uv_y, uv_x = np.mgrid[0:n, 0:n].astype(np.float32) / n
    for identifier, spec in SIGNS.items():
        family = spec["family"]
        rng = np.random.default_rng(int.from_bytes(
            hashlib.sha256(family.encode("ascii")).digest()[:4], "little"))
        art, base = _design(identifier, n, n)
        color = np.asarray(art, dtype=np.float32) / 255
        base_color = np.asarray(base, dtype=np.float32) / 255
        ink = np.linalg.norm(color - base_color, axis=2) > .12

        def field(cells, blur=0):
            im = Image.fromarray(rng.random((cells, cells), dtype=np.float32))
            im = im.resize((n, n), Image.Resampling.BICUBIC)
            if blur:
                # PIL does not GaussianBlur F images; convert to an L field.
                im = Image.fromarray(np.uint8(np.clip(np.asarray(im), 0, 1) * 255))
                im = im.filter(ImageFilter.GaussianBlur(blur))
                return np.asarray(im, dtype=np.float32) / 255
            return np.clip(np.asarray(im, dtype=np.float32), 0, 1)

        broad, medium, grain = field(8), field(57), field(331)
        edge = np.minimum.reduce((uv_x, 1 - uv_x, uv_y, 1 - uv_y))
        handling = np.clip((.09 - edge) / .09, 0, 1) * (.025 + .04 * broad)
        stains = np.clip((.35 - broad) * .11, 0, .035)
        # No rectangle cut-outs or missing strokes: wear is continuous and mild.
        fade = (medium - .5) * .040 + (grain - .5) * .010
        color = color * (1 - handling[..., None] - stains[..., None])
        color += fade[..., None]
        color[ink] = color[ink] * .97 + base_color * .03
        w, h = spec["size"]
        pitch = max(.004, max(w, h) / n * 2.8)
        weave = np.sin(2 * np.pi * uv_x * w / pitch) * np.sin(
            2 * np.pi * uv_y * h / pitch)
        height = weave * .000022 + (grain - .5) * .000030
        amplitude = .00005
        roughness = np.clip(.86 + (medium - .5) * .055
                            + handling * .18 - ink.astype(np.float32) * .055,
                            .76, .96)
        metal = np.zeros((n, n), np.float32)
        packed = np.zeros((n, n, 4), np.float32)
        packed[..., 3] = 1 - roughness
        maps = dict(BaseColor=np.clip(color, 0, 1),
                    Normal=normal_map(height, h, w), Roughness=roughness,
                    Metallic=metal, Occlusion=np.ones_like(metal),
                    Height=(height + amplitude) / (2 * amplitude),
                    MetallicSmoothness=packed)
        for suffix, pixels in maps.items():
            path = folder / (family + "_" + suffix + ".png")
            write_png(pixels, path, height=suffix == "Height")
            records.append(record(path))
        preview = work / "atlas_previews" / (identifier + ".png")
        preview.parent.mkdir(parents=True, exist_ok=True)
        # Preview restores physical aspect; the exchange texture is square UV.
        preview_size = (1400, max(200, round(1400 * h / w)))
        Image.open(folder / (family + "_BaseColor.png")).resize(
            preview_size, Image.Resampling.LANCZOS).save(preview)
        previews.append(str(preview))
        families.append(dict(name=family, resolution=n, periodMetres=w,
                             periodYMetres=h, tiling=1, normalStrength=1,
                             heightMinMetres=-amplitude,
                             heightMaxMetres=amplitude, alpha=False,
                             uv="Unique printed fabric; back U mirrored",
                             heightUsage="exchange only; no displacement/parallax",
                             originalArtwork=True, sourceProp=identifier,
                             emission=False))
        print("SIGN_ATLAS_OK", identifier, flush=True)
    names = {s["family"] for s in SIGNS.values()}
    # Other staging passes own unrelated families. Raster generation takes
    # time, so merge into the current file rather than the early snapshot.
    pbr = json.loads(manifest_path.read_text(encoding="utf-8"))
    pbr["families"] = [p for p in pbr["families"] if p["name"] not in names] + families
    pbr["maps"] = [p for p in pbr["maps"] if not any(
        p["file"].startswith(name + "_") for name in names)] + records
    _json(manifest_path, pbr)
    # Both manifests carry an explicit LIST palette, including in revision 8.
    # Keep the library portable independently of the building manifest.
    for relative in ("KursHubManifest.json", "Props/KursHubPropsManifest.json"):
        palette_path = root / HUB / relative
        _backup(palette_path, root, work)
        manifest = json.loads(palette_path.read_text(encoding="utf-8"))
        assert isinstance(manifest["palette"], list), palette_path
        manifest["palette"] = [p for p in manifest["palette"] if p["name"] not in names]
        manifest["palette"] += [dict(name=name, rgba=[1, 1, 1, 1])
                                for name in sorted(names)]
        _json(palette_path, manifest)
    report = dict(status="HUB8_SIGN_ATLASES_OK", families=7, maps=49,
                  resolution=2048, previews=previews, materialFamilies=families,
                  source="Original raster typography and schematic icons; not scans",
                  noEmission=True, noPerforatedOrClippedText=True)
    _json(work / "texture_proof.json", report)
    return report


def _skin_mesh(cloth, size, family):
    """Actual folded cloth, all closed faces; expand front/back by 0.7 mm."""
    import bpy
    from audit7_soft_shading import smooth_mesh
    mesh = cloth.copy()
    mesh.name = "Revision8 raster printed fabric " + family
    mesh.update()
    for v in mesh.vertices:
        # The same XY grid and real folds survive; there is no floating glyph.
        side = 1 if v.normal.y >= 0 else -1
        v.co.y += side * .0007
    mesh.update()
    for layer in list(mesh.uv_layers):
        mesh.uv_layers.remove(layer)
    uv = mesh.uv_layers.new(name="PrintedFabricUV")
    w, h = size
    for polygon in mesh.polygons:
        back = polygon.normal.y > 0
        for li in polygon.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            u = co.x / w + .5
            uv.data[li].uv = (1 - u if back else u, co.z / h)
    smooth_mesh(mesh)
    return mesh


def _repair_patch(cloth, size, level, name):
    """A visible 14 x 12 cm corner binding patch, not an invisible ID keeper."""
    import bpy
    import bmesh
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    w, h = size
    nx, nz = ((4, 4), (3, 3), (2, 2))[level]
    tree = BVHTree.FromPolygons([v.co for v in cloth.vertices],
                               [list(f.vertices) for f in cloth.polygons])
    vertices = []
    for side in (0, 1):
        for j in range(nz + 1):
            z = h - .205 + j * .12 / nz
            for i in range(nx + 1):
                x = w / 2 - .235 + i * .14 / nx
                hit, _, _, _ = tree.ray_cast(Vector((x, -2, z)),
                                              Vector((0, 1, 0)), 4)
                if hit is None:
                    raise AssertionError((name, "Corner patch misses real cloth", x, z))
                vertices.append((x, hit.y - .0010 - side * .0014, z))
    count = (nx + 1) * (nz + 1)
    faces = []
    for j in range(nz):
        for i in range(nx):
            a = j * (nx + 1) + i
            quad = (a, a + 1, a + nx + 2, a + nx + 1)
            faces.append(tuple(reversed(quad)))
            faces.append(tuple(index + count for index in quad))
    boundary = ([i for i in range(nx + 1)]
                + [j * (nx + 1) + nx for j in range(1, nz + 1)]
                + [nz * (nx + 1) + i for i in range(nx - 1, -1, -1)]
                + [j * (nx + 1) for j in range(nz - 1, 0, -1)])
    for a, b in zip(boundary, boundary[1:] + boundary[:1]):
        faces.append((a, b, b + count, a + count))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if bm.calc_volume(signed=True) < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    uv = mesh.uv_layers.new(name="PhysicalUV")
    for loop in mesh.loops:
        p = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (p.x / 2, p.z / 2)
    mesh.update()
    return mesh


def _assert_geometry(mesh):
    from audit7_verify_props import geometry
    result = geometry(mesh)
    keys = ("nonManifoldEdges", "degenerateFaces", "invalidFaceNormals",
            "nonPositiveSolids")
    assert not any(result[k] for k in keys), (mesh.name, result)
    assert result["signedVolume"] > 0 and result["finiteUV"], result
    return result


def _hook_material(root, family):
    import bpy
    material = bpy.data.materials.get(family) or bpy.data.materials.new(family)
    material.use_nodes = True
    material.diffuse_color = (1, 1, 1, 1)
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Emission Color"].default_value = (0, 0, 0, 1)
    bsdf.inputs["Emission Strength"].default_value = 0
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    output.location = (620, 120)
    bsdf.location = (320, 120)
    textures_by_name = {}
    for i, suffix in enumerate(MAPS):
        if suffix == "MetallicSmoothness":
            continue
        node = nodes.new("ShaderNodeTexImage")
        node.name = family + " " + suffix
        node.location = (-450, 360 - i * 260)
        node.extension = "EXTEND"
        image = bpy.data.images.load(str(root / HUB / "Textures" /
                                         (family + "_" + suffix + ".png")),
                                     check_existing=False)
        image.colorspace_settings.name = "sRGB" if suffix == "BaseColor" else "Non-Color"
        image["KursPBRVersion"] = 8
        image.pack()
        node.image = image
        textures_by_name[suffix] = node
    for source, target in (("BaseColor", "Base Color"),
                           ("Roughness", "Roughness"), ("Metallic", "Metallic")):
        links.new(textures_by_name[source].outputs["Color"], bsdf.inputs[target])
    normal = nodes.new("ShaderNodeNormalMap")
    normal.location = (30, -380)
    normal.inputs["Strength"].default_value = 1
    links.new(textures_by_name["Normal"].outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])
    textures_by_name["Occlusion"].label = "Local white AO; no baked building shadow"
    textures_by_name["Height"].label = "Exchange only: -0.05..+0.05 mm; no parallax"
    material["KursSurfaceVersion"] = 8
    material["KursPBRFamily"] = family
    material["KursUniqueUV"] = True
    return material


def _source(root, work, filename, props):
    import bpy
    from audit7_soft_shading import native_geometry_hash
    path = root / "Sources/KursHub" / filename
    _backup(path, root, work)
    before_file = _sha(path)
    bpy.ops.wm.open_mainfile(filepath=str(path))
    archive = bpy.data.collections.get("Revision7 lettering archive")
    if archive is None:
        archive = bpy.data.collections.new("Revision7 lettering archive")
        bpy.context.scene.collection.children.link(archive)
    archive.hide_render = True
    archive.hide_viewport = True
    records = []
    for identifier, spec in SIGNS.items():
        prop = props[identifier]
        collection = bpy.data.collections[prop["collection"]]
        if collection.get("Hub8PrintedSigns") == 8:
            assert any(o.get("Hub8PrintedSkin") for o in collection.objects)
            records.append(dict(id=identifier, alreadyApplied=True))
            continue
        objects = [o for o in collection.objects if o.type == "MESH"]
        cloth = max((o for o in objects if "FoldedSignCloth" in o.name),
                    key=lambda o: len(o.data.polygons))
        cloth_hash = native_geometry_hash(cloth.data)
        original = {o.name: native_geometry_hash(o.data) for o in objects}
        by_material = {c["material"]: c for c in prop["chunks"]}
        main_chunk = by_material[spec["ink"]]["name"]
        auxiliary = {m: c for m, c in by_material.items()
                     if m in ("SignRed", "PlasticDark")}
        if identifier != "KH_SignGear":
            auxiliary.pop("PlasticDark", None)
        old_ink = {spec["ink"], *auxiliary}
        glyphs = [o for o in objects if o.data.materials
                  and o.data.materials[0].name.split(".")[0] in old_ink]
        assert glyphs, (identifier, "No old lettering to archive")
        sub = bpy.data.collections.get("Revision7 lettering " + identifier)
        if sub is None:
            sub = bpy.data.collections.new("Revision7 lettering " + identifier)
            archive.children.link(sub)
        for obj in glyphs:
            for old_collection in list(obj.users_collection):
                old_collection.objects.unlink(obj)
            sub.objects.link(obj)
            obj.hide_render = True
            obj.hide_viewport = True
            obj["Revision8ArchivedGlyph"] = True
        # Remaining original source solids only receive the preserved chunk tag.
        for obj in collection.objects:
            if obj.type != "MESH":
                continue
            mat = obj.data.materials[0].name.split(".")[0]
            assert mat in by_material, (identifier, obj.name, mat)
            obj["Hub8ExportChunk"] = by_material[mat]["name"]
        skin = cloth.copy()
        skin.data = _skin_mesh(cloth.data, spec["size"], spec["family"])
        skin.name = identifier + " Actual fabric raster printing"
        skin.data.materials.clear()
        skin.data.materials.append(_hook_material(root, spec["family"]))
        skin.hide_render = False
        skin.hide_viewport = False
        skin["unity_collision"] = False
        skin["Hub8ExportChunk"] = main_chunk
        skin["Hub8PrintedSkin"] = True
        collection.objects.link(skin)
        metrics = _assert_geometry(skin.data)
        for material, chunk in auxiliary.items():
            patch = bpy.data.objects.new(identifier + " " + material +
                                         " corner binding repair",
                                         _repair_patch(cloth.data, spec["size"],
                                                       0, identifier + material))
            patch.matrix_world = cloth.matrix_world.copy()
            patch.data.materials.append(bpy.data.materials[material])
            patch["unity_collision"] = False
            patch["Hub8ExportChunk"] = chunk["name"]
            patch["Hub8RepairBinding"] = True
            collection.objects.link(patch)
            _assert_geometry(patch.data)
        assert native_geometry_hash(cloth.data) == cloth_hash
        assert all(native_geometry_hash(bpy.data.objects[name].data) == value
                   for name, value in original.items()), identifier
        collection["Hub8PrintedSigns"] = 8
        records.append(dict(id=identifier, archiveObjects=len(glyphs),
                            archivedNames=[o.name for o in glyphs],
                            originalSourceGeometryUnchanged=True,
                            skinMetrics=metrics, material=spec["family"],
                            retainedSourceChunkNames=[c["name"] for c in prop["chunks"]],
                            repairBindingChunks=[c["name"] for c in auxiliary.values()]))
    bpy.context.scene["KursPrintedSignsRevision"] = 8
    bpy.context.scene["KursPrintedSignsMethod"] = (
        "Original glyph history hidden; raster printing on the actual closed "
        "folded fabric, 0.7 mm expanded face clearance and unique front/back UV")
    assert _sha(path) == before_file, "Concurrent native source edit"
    # Blender's Windows .blend1 rotation can fail even when the source itself
    # is writable (for example, a backup is still open in another process).
    # We already retain the exact before file. Save beside our proof instead
    # and replace the staged source only after a second concurrency check.
    native_work = work / "native" / filename
    native_work.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(native_work), relative_remap=False)
    assert _sha(path) == before_file, "Concurrent native edit during save"
    native_work.replace(path)
    return dict(source=filename, beforeSHA256=before_file,
                afterSHA256=_sha(path), signs=records)


def _element(name, props=(), types=b"", children=()):
    return SimpleNamespace(id=name.encode() if isinstance(name, str) else name,
                           props=list(props), props_type=bytearray(types),
                           elems=list(children))


def _geometry_payload(mesh):
    """Independent valid FBX geometry payload, keeping the original object UID."""
    from io_scene_fbx import data_types
    def field(name, value, kind):
        return _element(name, [value], kind)

    def string(name, value):
        return field(name, value.encode(), b"S")

    mesh.update()
    # Blender's encoder selects the platform's exact 32-bit array type. On
    # Windows it can be 'l'; hard-coded array('i') has the same width but is
    # rejected by the official encoder's strict typecode contract.
    indices = array.array(data_types.ARRAY_INT32)
    for polygon in mesh.polygons:
        vertices = list(polygon.vertices)
        indices.extend(vertices[:-1])
        indices.append(-vertices[-1] - 1)
    uv = mesh.uv_layers.active
    uv_values = array.array(data_types.ARRAY_FLOAT64, (c for loop in uv.data for c in loop.uv))
    normals = array.array(data_types.ARRAY_FLOAT64, (c for normal in mesh.corner_normals
                                for c in normal.vector))
    common = lambda mapping, reference: [field("Version", 101, b"I"),
        string("Name", ""), string("MappingInformationType", mapping),
        string("ReferenceInformationType", reference)]
    normal_layer = _element("LayerElementNormal", [0], b"I", [
        *common("ByPolygonVertex", "Direct"), field("Normals", normals, b"d")])
    uv_layer = _element("LayerElementUV", [0], b"I", [
        *common("ByPolygonVertex", "IndexToDirect"),
        field("UV", uv_values, b"d"),
        field("UVIndex", array.array(data_types.ARRAY_INT32, range(len(mesh.loops))), b"i")])
    material_layer = _element("LayerElementMaterial", [0], b"I", [
        *common("AllSame", "IndexToDirect"),
        field("Materials", array.array(data_types.ARRAY_INT32, [0]), b"i")])
    layer = _element("Layer", [0], b"I", [field("Version", 100, b"I")])
    for kind in ("LayerElementNormal", "LayerElementUV", "LayerElementMaterial"):
        layer.elems.append(_element("LayerElement", children=[
            string("Type", kind), field("TypedIndex", 0, b"I")]))
    return [field("GeometryVersion", 124, b"I"),
            field("Vertices", array.array(data_types.ARRAY_FLOAT64, (c for v in mesh.vertices
                                                  for c in v.co)), b"d"),
            field("PolygonVertexIndex", indices, b"i"), normal_layer,
            material_layer, uv_layer, layer]


def _fbx(root, work, prop, spec, level):
    import bpy
    from audit7_soft_shading import (child, clone, encoded_clone, geometry_mesh,
                                    tree_hash, parse_fbx, encode_bin)
    path = root / level["model"]
    _backup(path, root, work)
    parsed, version = parse_fbx.parse(str(path))
    tree = clone(parsed)
    objects = child(tree, b"Objects").elems
    connections = child(tree, b"Connections").elems
    connection_hash = tree_hash(child(tree, b"Connections"))
    models = {o.props[0]: o for o in objects if o.id == b"Model"}
    models_by_name = {o.props[1].split(b"\x00\x01")[0].decode(): o
                      for o in models.values()}
    geometries = {}
    for g in (o for o in objects if o.id == b"Geometry"):
        model = next(models[c.props[2]] for c in connections
                     if c.props[0] == b"OO" and c.props[1] == g.props[0]
                     and c.props[2] in models)
        geometries[model.props[1].split(b"\x00\x01")[0].decode()] = g
    by_material = {c["material"]: c for c in prop["chunks"]}
    cloth_name = by_material[spec["canvas"]]["name"]
    # On retry the manifest ink has already been migrated; model name survives.
    main = next(c for c in prop["chunks"]
                if c["material"] in (spec["ink"], spec["family"]))
    auxiliary = {m: c for m, c in by_material.items()
                 if m == "SignRed" or (m == "PlasticDark"
                                        and prop["id"] == "KH_SignGear")}
    changed = {main["name"], *(c["name"] for c in auxiliary.values())}
    contracts = {name: tree_hash(g) for name, g in geometries.items()
                 if name not in changed}
    cloth = geometry_mesh(geometries[cloth_name])
    # Original cloth UV is not reused: the whole print has one unique unwrap.
    skin = _skin_mesh(cloth, spec["size"], spec["family"])
    _assert_geometry(skin)
    geometries[main["name"]].elems = _geometry_payload(skin)
    for material, chunk in auxiliary.items():
        patch = _repair_patch(cloth, spec["size"], level["level"],
                              prop["id"] + " " + material + " binding")
        _assert_geometry(patch)
        geometries[chunk["name"]].elems = _geometry_payload(patch)
    main_model = models_by_name[main["name"]]
    materials = {o.props[0]: o for o in objects if o.id == b"Material"}
    material_id = next(c.props[1] for c in connections
                       if c.props[0] == b"OO" and c.props[2] == main_model.props[0]
                       and c.props[1] in materials)
    material = materials[material_id]
    material.props[1] = spec["family"].encode() + b"\x00\x01Material"
    properties = next((e for e in material.elems if e.id == b"Properties70"), None)
    if properties:
        for p in properties.elems:
            if p.id != b"P":
                continue
            if p.props[0] in (b"DiffuseColor", b"Diffuse"):
                p.props[4:] = [1., 1., 1.]
            elif p.props[0] in (b"EmissiveColor", b"Emissive"):
                p.props[4:] = [0., 0., 0.]
            elif p.props[0] == b"EmissiveFactor":
                p.props[4:] = [0.]
    # Existing FBX albedo texture/video IDs can be retargeted without changing
    # any connection. Some old FBX files contain only a diffuse material; their
    # exact target-Unity PBR still comes from the external material manifest.
    texture_objects = {o.props[0]: o for o in objects if o.id == b"Texture"}
    videos = {o.props[0]: o for o in objects if o.id == b"Video"}
    albedo_ids = {c.props[1] for c in connections
                  if c.props[0] == b"OP" and c.props[2] == material_id
                  and len(c.props) > 3 and c.props[3] in (b"DiffuseColor", b"Diffuse")
                  and c.props[1] in texture_objects}
    albedo_path = root / HUB / "Textures" / (spec["family"] + "_BaseColor.png")
    relative_albedo = os.path.relpath(albedo_path, path.parent).replace("\\", "/")
    for texture_id in albedo_ids:
        texture = texture_objects[texture_id]
        texture.props[1] = (spec["family"] + "_BaseColor").encode() + b"\x00\x01Texture"
        linked = [texture]
        linked += [videos[c.props[1]] for c in connections
                   if c.props[0] == b"OO" and c.props[2] == texture_id
                   and c.props[1] in videos]
        for asset in linked:
            for field in asset.elems:
                if field.id in (b"FileName", b"Filename"):
                    field.props[0] = str(albedo_path).replace("\\", "/").encode()
                elif field.id in (b"RelativeFilename", b"RelativeFileName"):
                    field.props[0] = relative_albedo.encode()
                elif asset.id == b"Video" and field.id == b"Content":
                    # Embedded files take precedence over the filename in
                    # some importers. Keep their UID/link, replace old artwork.
                    field.props[0] = albedo_path.read_bytes()
    assert tree_hash(child(tree, b"Connections")) == connection_hash
    assert {uid: tree_hash(model) for uid, model in models.items()} == {
        obj.props[0]: tree_hash(obj) for obj in child(parsed, b"Objects").elems
        if obj.id == b"Model"}, "An existing FBX Model UID/name/transform changed"
    assert all(tree_hash(geometries[name]) == value
               for name, value in contracts.items()), "Unrelated FBX geometry changed"
    temporary = path.with_suffix(".printed8.fbx")
    encode_bin.write(str(temporary), encoded_clone(tree), version)
    temporary.replace(path)
    # Prove a real independent Blender FBX import, not only the encoder tree.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    imported = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    assert {o.name for o in imported} == {c["name"] for c in prop["chunks"]}
    metrics = {o.name: _assert_geometry(o.data) for o in imported}
    ink_obj = bpy.data.objects[main["name"]]
    assert ink_obj.data.materials[0].name == spec["family"], (
        path, "New material identity lost", ink_obj.data.materials[0].name)
    count = sum(m["triangles"] for m in metrics.values())
    points = [o.matrix_world @ v.co for o in imported for v in o.data.vertices]
    lo = [min(p[i] for p in points) for i in (0, 2, 1)]
    hi = [max(p[i] for p in points) for i in (0, 2, 1)]
    result = dict(model=level["model"], level=level["level"],
                  sha256=_sha(path), triangles=count, boundsMin=lo, boundsMax=hi,
                  retainedAllModelUIDsAndNames=True, retainedConnections=True,
                  existingFBXAlbedoRetargeted=bool(albedo_ids),
                  otherGeometryArraysUnchanged=True,
                  changedGeometryChunks=sorted(changed), actualReverseImport=True,
                  chunks=metrics)
    print("SIGN_FBX_OK", prop["id"], level["level"], count, flush=True)
    return result


def apply(root, work):
    """Blender entry point. Root/work are explicit, separate staging paths."""
    root = _guard(root)
    work = Path(work).resolve()
    work.mkdir(parents=True, exist_ok=True)
    sys.path.insert(0, str(Path(__file__).parent))
    if not RUNTIME_PYTHON.is_file():
        raise FileNotFoundError(RUNTIME_PYTHON)
    # Mutations are sequential: maps/metadata, sources, FBXs, final manifest.
    result = subprocess.run([str(RUNTIME_PYTHON), str(Path(__file__)),
                             "--textures", "--root", str(root),
                             "--work", str(work)], capture_output=True,
                            text=True, encoding="utf-8", errors="replace")
    (work / "textures.log").write_text(result.stdout + result.stderr,
                                        encoding="utf-8")
    if result.returncode:
        raise RuntimeError("Sign atlas generation failed; see textures.log")
    manifest_path = root / HUB / "Props/KursHubPropsManifest.json"
    _backup(manifest_path, root, work)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    props = {p["id"]: p for p in manifest["props"]}
    assert set(SIGNS) <= props.keys()
    source_records = [_source(root, work, name, props)
                      for name in ("KursHubProps.blend", "KursHub.blend")]
    fbx_records = []
    for identifier, spec in SIGNS.items():
        prop = props[identifier]
        old_names = {c["name"] for c in prop["chunks"]}
        previous = None
        for lod in sorted(prop["lods"], key=lambda item: item["level"]):
            record = _fbx(root, work, prop, spec, lod)
            if previous is not None:
                assert record["triangles"] < previous, (
                    identifier, "Unsafe/nondecreasing LOD", previous,
                    record["triangles"])
            previous = record["triangles"]
            lod["triangles"] = record["triangles"]
            if lod["level"] == 0:
                prop["triangles"] = record["triangles"]
                prop["boundsMin"] = record["boundsMin"]
                prop["boundsMax"] = record["boundsMax"]
            fbx_records.append(dict(id=identifier, **record))
        for chunk in prop["chunks"]:
            if chunk["material"] == spec["ink"]:
                chunk["material"] = spec["family"]
        assert {c["name"] for c in prop["chunks"]} == old_names
        prop["signQualityRevision"] = 8
        prop["signPrint"] = dict(material=spec["family"], uniqueUV=True,
                                 depthExpansionMetres=.0007,
                                 originalGlyphSourceArchive="Revision7 lettering archive")
    _json(manifest_path, manifest)
    proof = dict(status="HUB8_SIGNS_SOURCE_AND_FBX_OK", signs=7, models=21,
                 skinClearanceMetres=.0007, glyphGeometryRemovedFromExports=True,
                 sourceHistoryPreserved=True, allLegacyChunkNamesPreserved=True,
                 sourceRecords=source_records, fbxRecords=fbx_records,
                 textureProof=json.loads((work / "texture_proof.json").read_text(
                     encoding="utf-8")),
                 limitations=["Original authored interpretation of references, not an exact measured logo reconstruction",
                              "Old ink materials are repurposed into visible corner repair bindings to retain native references",
                              "Target Unity shader/material reassignment and player-height visual acceptance remain separate checks"])
    _json(work / "sign_proof.json", proof)
    return proof


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, type=Path)
    parser.add_argument("--work", required=True, type=Path)
    parser.add_argument("--textures", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:]
                             if "--" in sys.argv else sys.argv[1:])
    if args.textures:
        textures(args.root, args.work)
    else:
        apply(args.root, args.work)
