from __future__ import annotations

import math
import os
import random
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
ASSET = ROOT / "work" / "Benches_RU" / "UnityProject" / "Assets" / "Environment" / "Props" / "Benches_RU"
MODEL_DIR = ASSET / "Models"
SOURCE_DIR = ASSET / "Source~"
TEX_DIR = ASSET / "Textures"
SEED = 904217


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    base = bpy.data.collections.get("Collection")
    if base is None:
        base = bpy.data.collections.new("Benches_RU_Source")
        scene.collection.children.link(base)
    base.name = "Benches_RU_Source"
    for block in list(bpy.data.collections):
        if block != base:
            bpy.data.collections.remove(block)
    for child in list(scene.collection.children):
        if child != base:
            scene.collection.children.unlink(child)
    if base.name not in [child.name for child in scene.collection.children]:
        scene.collection.children.link(base)
    layer = bpy.context.view_layer.layer_collection
    active = next((child for child in layer.children if child.collection == base), None)
    if active is not None:
        bpy.context.view_layer.active_layer_collection = active
    return base


def make_collection(name, parent):
    col = bpy.data.collections.new(name)
    parent.children.link(col)
    return col


def move_to_collection(obj, collection):
    for col in list(obj.users_collection):
        col.objects.unlink(obj)
    collection.objects.link(obj)


def make_material(name, texture_prefix=None, tint=(1, 1, 1), metal_scalar=0.0, rough_scalar=0.7):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*tint, 1.0)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Metallic"].default_value = metal_scalar
    shader.inputs["Roughness"].default_value = rough_scalar
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    if texture_prefix:
        color = nodes.new("ShaderNodeTexImage")
        color.image = bpy.data.images.load(str(TEX_DIR / f"Tex_{texture_prefix}_BaseColor.png"), check_existing=True)
        color.label = "Base Color / sRGB"
        color.location = (-650, 160)
        if tint != (1, 1, 1):
            multiply = nodes.new("ShaderNodeMixRGB")
            multiply.blend_type = "MULTIPLY"
            multiply.inputs[0].default_value = 1.0
            multiply.inputs[2].default_value = (*tint, 1)
            multiply.location = (-360, 170)
            links.new(color.outputs["Color"], multiply.inputs[1])
            links.new(multiply.outputs["Color"], shader.inputs["Base Color"])
        else:
            links.new(color.outputs["Color"], shader.inputs["Base Color"])
        normal = nodes.new("ShaderNodeTexImage")
        normal.image = bpy.data.images.load(str(TEX_DIR / f"Tex_{texture_prefix}_Normal.png"), check_existing=True)
        normal.image.colorspace_settings.name = "Non-Color"
        normal.label = "Tangent normal"
        normal.location = (-650, -100)
        normal_map = nodes.new("ShaderNodeNormalMap")
        normal_map.location = (-360, -80)
        links.new(normal.outputs["Color"], normal_map.inputs["Color"])
        links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
        metal = nodes.new("ShaderNodeTexImage")
        metal.image = bpy.data.images.load(str(TEX_DIR / f"Tex_{texture_prefix}_MetallicSmoothness.png"), check_existing=True)
        metal.image.colorspace_settings.name = "Non-Color"
        metal.label = "Unity metallic / smoothness (RGBA)"
        metal.location = (-650, -340)
        sep = nodes.new("ShaderNodeSeparateColor")
        sep.mode = "RGB"
        sep.location = (-360, -310)
        links.new(metal.outputs["Color"], sep.inputs["Color"])
        links.new(sep.outputs["Red"], shader.inputs["Metallic"])
        rough = nodes.new("ShaderNodeTexImage")
        rough.image = bpy.data.images.load(str(TEX_DIR / f"Tex_{texture_prefix}_Roughness.png"), check_existing=True)
        rough.image.colorspace_settings.name = "Non-Color"
        rough.label = "Roughness"
        rough.location = (-650, -560)
        links.new(rough.outputs["Color"], shader.inputs["Roughness"])
    else:
        shader.inputs["Base Color"].default_value = (*tint, 1.0)
    return mat


def build_materials():
    materials = {
        "wood_green": make_material("M_WoodPaint_Green", "WoodGreen", (1,1,1), 0.0, 0.7),
        "wood_blue": make_material("M_WoodPaint_Blue", "WoodBlue", (1,1,1), 0.0, 0.7),
        "wood_brown": make_material("M_WoodPaint_Brown", "WoodBrown", (1,1,1), 0.0, 0.74),
        "wood_raw": make_material("M_WoodWeathered", "WoodRaw", (0.92, 0.91, 0.88), 0.0, 0.82),
        "wood_rot": make_material("M_WoodRotten", "WoodRaw", (0.53, 0.48, 0.37), 0.0, 0.92),
        "steel_green": make_material("M_SteelPaint_Green", "SteelPaint", (0.42, 0.56, 0.32), 0.28, 0.61),
        "steel_blue": make_material("M_SteelPaint_Blue", "SteelPaint", (0.45, 0.60, 0.92), 0.27, 0.61),
        "steel_gray": make_material("M_SteelPaint_Gray", "SteelPaint", (0.56, 0.58, 0.57), 0.30, 0.58),
        "steel_bare": make_material("M_SteelBare", "SteelBare", (1,1,1), 0.92, 0.43),
        "steel_rust": make_material("M_SteelRust", "SteelRust", (0.98, 0.95, 0.91), 0.28, 0.84),
        "cast": make_material("M_CastIron", "CastIron", (0.83, 0.84, 0.80), 0.74, 0.74),
        "concrete": make_material("M_Concrete_Porous", "Concrete", (0.98, 0.98, 0.95), 0.0, 0.91),
        "moss": make_material("M_Moss", None, (0.105, 0.17, 0.07), 0.0, 0.96),
        "dirt": make_material("M_Dirt_Dark", None, (0.115, 0.092, 0.065), 0.0, 0.96),
        "graffiti": make_material("M_Graffiti_FadedRed", None, (0.46, 0.12, 0.09), 0.0, 0.82),
        "paper": make_material("M_Sticker_OldPaper", None, (0.50, 0.43, 0.30), 0.0, 0.9),
        "bolt": make_material("M_Bolt_Galvanized", "SteelBare", (1,1,1), 0.92, 0.38),
        "black": make_material("M_Crack_Shadow", None, (0.045, 0.032, 0.023), 0.0, 0.98),
    }
    return materials


def assign_material(obj, material):
    if material:
        obj.data.materials.clear()
        obj.data.materials.append(material)


def apply_mod(obj, modifier):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def box(name, dims, loc, material, bevel=0.006, bevel_segments=2, rotation=(0, 0, 0), jitter=0.0, rng=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dims
    obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Real edge bevel", "BEVEL")
        mod.width = bevel
        mod.segments = bevel_segments
        mod.limit_method = "ANGLE"
        mod.harden_normals = True
        apply_mod(obj, mod)
    if jitter and rng:
        # Small hand-made waviness is concentrated along board edges; faces remain planar enough to catch light.
        for v in obj.data.vertices:
            edge = max(abs(v.co.y) / max(dims[1] * 0.5, 1e-5), abs(v.co.z) / max(dims[2] * 0.5, 1e-5))
            if edge > 0.83:
                v.co.x += rng.uniform(-jitter, jitter)
                if abs(v.co.z) > dims[2] * 0.37:
                    v.co.z += rng.uniform(-jitter * 0.5, jitter * 0.5)
    assign_material(obj, material)
    for p in obj.data.polygons:
        p.use_smooth = False
    return obj


def beam(name, a, b, width, material, depth=None, bevel=0.004, rng=None):
    pa, pb = Vector(a), Vector(b)
    delta = pb - pa
    obj = box(name, (width, depth if depth else width, delta.length), (pa + pb) * 0.5, material, bevel=bevel)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = delta.to_track_quat("Z", "Y")
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return obj


def cylinder(name, radius, depth, loc, material, axis=(0, 0, 1), vertices=16, bevel=0.0008):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector(axis).to_track_quat("Z", "Y")
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel:
        mod = obj.modifiers.new("Turned edge", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        apply_mod(obj, mod)
    assign_material(obj, material)
    for p in obj.data.polygons:
        p.use_smooth = len(p.vertices) <= 16
    return obj


def tube(name, points, radius, material, bevel_resolution=2, cyclic=False, resolution_u=12):
    curve = bpy.data.curves.new(name + "Path", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = resolution_u
    curve.bevel_depth = radius
    curve.bevel_resolution = bevel_resolution
    curve.use_fill_caps = True
    smooth_path = any(word in name.lower() for word in ("curved", "arched", "rolled steel"))
    spline = curve.splines.new("BEZIER" if smooth_path else "POLY")
    if smooth_path:
        spline.bezier_points.add(len(points)-1)
        for point, coords in zip(spline.bezier_points,points):
            point.co = coords; point.handle_left_type = "AUTO"; point.handle_right_type = "AUTO"
    else:
        spline.points.add(len(points) - 1)
        for index, point in enumerate(points): spline.points[index].co = (*point, 1.0)
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    assign_material(obj, material)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    obj.name = name
    return obj


def line_weld(name, a, b, material, radius=0.006):
    return tube(name, [a, b], radius, material, bevel_resolution=2)


def board(name, length, width, thick, loc, material, rng, rotation_x=0.0, wear=1, broken=False, crack_material=None):
    jitter = {1: 0.0011, 2: 0.0025, 3: 0.0042}[wear]
    if broken:
        # A visibly sawn-away plank end with two uneven splinters keeps the damaged board physical.
        original_length = length
        kept_fraction = rng.uniform(0.47, 0.70)
        length = original_length * kept_fraction
        direction = -1 if rng.random() < 0.5 else 1
        # Keep one original end in place and expose the other as a snapped edge.
        end_sign = direction
        loc = (loc[0] + end_sign * (original_length - length) * 0.5, loc[1], loc[2])
    nx, ny = 64, 4
    wave_phase = rng.random() * math.tau
    verts = []
    for top in (True, False):
        for ix in range(nx + 1):
            x = (ix / nx - 0.5) * length
            for iy in range(ny + 1):
                y = (iy / ny - 0.5) * width
                wave = math.sin(x * 4.0 + wave_phase) * jitter * 0.45
                crown = math.sin(iy / ny * math.pi) * jitter * 0.12
                z = (thick * 0.5 if top else -thick * 0.5) + wave + crown
                verts.append((x, y, z))
    stride = ny + 1
    count = (nx + 1) * stride
    faces = []
    for ix in range(nx):
        for iy in range(ny):
            a = ix * stride + iy
            b = a + count
            faces.extend(((a, a + stride, a + stride + 1, a + 1),
                          (b, b + 1, b + stride + 1, b + stride)))
    for ix in range(nx):
        for iy in (0, ny):
            a = ix * stride + iy
            b = a + stride
            faces.append((a, a + count, b + count, b) if iy == 0 else (a, b, b + count, a + count))
    for iy in range(ny):
        for ix in (0, nx):
            a = ix * stride + iy
            b = a + 1
            faces.append((a, b, b + count, a + count) if ix == 0 else (a, a + count, b + count, b))
    mesh = bpy.data.meshes.new(name + "TimberMeshV2")
    mesh.from_pydata(verts, [], faces)
    bm = bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(mesh); bm.free(); mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = (rotation_x, 0, 0)
    obj["TimberUV"] = True
    assign_material(obj, material)
    bevel = obj.modifiers.new("Rounded timber edges V2", "BEVEL")
    bevel.width = min(0.0025, thick * 0.08)
    bevel.segments = 3
    bevel.limit_method = "ANGLE"
    bevel.use_clamp_overlap = True
    apply_mod(obj, bevel)
    for poly in obj.data.polygons: poly.use_smooth = True
    obj.data.set_sharp_from_angle(angle=math.radians(50))
    weighted = obj.modifiers.new("Area weighted timber normals", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    apply_mod(obj, weighted)
    if broken:
        # Two tapered wood splinters protrude from the snapped edge.
        sign = -1 if loc[0] > 0 else 1
        edge_x = loc[0] + sign * length * 0.5
        top_z = loc[2] + thick * 0.44
        for i in range(2):
            yy = loc[1] + rng.uniform(-width * 0.35, width * 0.35)
            tip = (edge_x + sign * rng.uniform(0.025, 0.060), yy + rng.uniform(-0.012, 0.012), top_z + rng.uniform(-0.005, 0.009))
            base1 = (edge_x - sign * 0.045, yy - 0.004, loc[2] + thick * 0.1)
            base2 = (edge_x - sign * 0.010, yy + 0.004, loc[2] + thick * 0.32)
            verts = [base1, base2, tip, (base1[0], base1[1], base1[2] - 0.012)]
            faces = [(0, 1, 2), (0, 2, 3), (1, 3, 2), (0, 3, 1)]
            mesh = bpy.data.meshes.new(name + "SplinterMesh")
            mesh.from_pydata(verts, [], faces)
            splinter = bpy.data.objects.new(name + "Splinter", mesh)
            bpy.context.collection.objects.link(splinter)
            assign_material(splinter, material)
    if wear > 1 and crack_material and rng.random() < (0.55 if wear == 2 else 0.90):
        cracks = 1 if wear == 2 else 2
        for c in range(cracks):
            x0 = loc[0] + rng.uniform(-length * 0.42, length * 0.16)
            y0 = loc[1] + rng.uniform(-width * 0.34, width * 0.34)
            z0 = loc[2] + thick * 0.52
            span = rng.uniform(0.10, min(0.48, length * 0.4))
            pts = [(x0 + span * k / 8, y0 + rng.uniform(-0.003, 0.003), z0 + 0.0005) for k in range(9)]
            tube(name + "Crack", pts, 0.0009 if wear == 2 else 0.00125, crack_material, bevel_resolution=1, resolution_u=1)
    return obj


def add_bolt(name, point, axis, mat, missing=False, large=False):
    if missing:
        return
    p = Vector(point)
    a = Vector(axis).normalized()
    r = 0.010 if large else 0.007
    washer_r = r * 1.50
    cylinder(name + "Washer", washer_r, 0.0025, p, mat, a, 16, 0.0004)
    head_center = p + a * 0.0038
    cylinder(name + "Head", r, 0.0045, head_center, mat, a, 12, 0.0006)
    # A dark screwdriver slot is cut-like detail on the screw head.
    if not large:
        tangent = Vector((1, 0, 0)) if abs(a.x) < 0.75 else Vector((0, 1, 0))
        tangent = (tangent - a * tangent.dot(a)).normalized()
        tip = head_center + a * 0.0026
        line_weld(name + "Slot", tip - tangent * r * 0.53, tip + tangent * r * 0.53, mats["black"], 0.00055)


def board_hardware(length, centers, z, y, wear, rng, material, x_bounds=None):
    for ci, cy in enumerate(centers):
        for x in (-length * 0.34, length * 0.34):
            if x_bounds and not (x_bounds[0]+0.01 <= x <= x_bounds[1]-0.01): continue
            missing = wear == 2 and rng.random() < 0.08 or wear == 3 and rng.random() < 0.22
            add_bolt(f"SeatFastener_{ci}_{x:.2f}", (x, cy, z), (0, 0, 1), material, missing)


def back_hardware(length, z, y, wear, rng, material, x_bounds=None):
    for x in (-length * 0.34, length * 0.34):
        if x_bounds and not (x_bounds[0]+0.01 <= x <= x_bounds[1]-0.01): continue
        missing = wear == 2 and rng.random() < 0.11 or wear == 3 and rng.random() < 0.32
        add_bolt(f"BackFastener_{x:.2f}_{z:.2f}", (x, y, z), (0, -1, 0), material, missing)


def add_grime_and_moss(wear, rng, length, seat_z, mats, amount=1):
    if wear < 2: return
    # Deposits stay in the two structural seat joints, below the exposed sitting surface.
    for side in (-1,1):
        for index in range(3 if wear == 2 else 7):
            x=side*length*0.34+rng.uniform(-0.012,0.012)
            y=rng.choice((-0.15,0.15))+rng.uniform(-0.006,0.006)
            z=seat_z-0.023+rng.uniform(-0.004,0.004)
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=(x,y,z))
            obj=bpy.context.object; obj.name="Organic deposit within support joint"
            obj.scale=(rng.uniform(0.006,0.012),rng.uniform(0.004,0.008),rng.uniform(0.002,0.005))
            bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
            assign_material(obj,mats["moss"] if wear==3 and index%2 else mats["dirt"])


def add_text(name, text, loc, size, material, rotation=(math.pi / 2, 0, 0), font_path=r"C:\Windows\Fonts\arial.ttf"):
    curve = bpy.data.curves.new(name + "Text", "FONT")
    try:
        curve.font = bpy.data.fonts.load(font_path, check_existing=True)
    except Exception:
        pass
    curve.body = text
    curve.size = size
    curve.resolution_u = 3
    curve.extrude = 0.00015
    curve.bevel_depth = 0.00005
    curve.bevel_resolution = 0
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = rotation
    assign_material(obj, material)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    obj.name = name
    return obj


def add_wooden_slatted_body(kind, wear, rng, mats, length, seat_count, back_count=3,
                            seat_width=0.088, seat_thick=0.036, seat_z=0.452,
                            painted="wood_green", broken_index=None, back_y=0.225,
                            graffiti=False):
    seat_centers = []
    gap = 0.012
    total_width = seat_count * seat_width + (seat_count - 1) * gap
    start = -total_width * 0.5 + seat_width * 0.5
    for i in range(seat_count):
        cy = start + i * (seat_width + gap)
        seat_centers.append(cy)
        absent = wear == 3 and broken_index == i
        if absent:
            continue
        mat = mats[painted] if wear == 1 else (mats[painted] if wear == 2 and rng.random()<0.38 else mats["wood_raw"] if wear == 2 else mats["wood_rot"] if rng.random()<0.42 else mats["wood_raw"])
        broken = wear == 3 and i == (broken_index + 1) % seat_count and rng.random() < 0.78
        timber = board(f"Seat board {i+1}", length, seat_width, seat_thick,
              (rng.uniform(-0.012, 0.012), cy, seat_z), mat, rng, wear=wear, broken=broken, crack_material=mats["black"])
        board_hardware(length, (cy,), seat_z + seat_thick * 0.50, cy, wear, rng, mats["bolt"], (timber.location.x-timber.dimensions.x*0.5,timber.location.x+timber.dimensions.x*0.5))
    if back_count > 0:
        z_base = 0.59
        back_h = 0.072 if back_count <= 3 else 0.065
        for j in range(back_count):
            z = z_base + j * (back_h + 0.018)
            absent = wear == 3 and j == (broken_index or 0) % back_count and rng.random() < 0.62
            if absent:
                continue
            mat = mats[painted] if wear == 1 else mats[painted] if wear == 2 and rng.random()<0.35 else mats["wood_raw"] if wear == 2 or rng.random()<0.62 else mats["wood_rot"]
            broken = wear == 3 and j == (broken_index or 0) % back_count and not absent
            timber = board(f"Back slat {j+1}", length, 0.032, back_h,
                  (0, back_y, z), mat, rng, rotation_x=math.radians(-11), wear=wear, broken=broken, crack_material=mats["black"])
            back_hardware(length, z, back_y - 0.020, wear, rng, mats["bolt"], (timber.location.x-timber.dimensions.x*0.5,timber.location.x+timber.dimensions.x*0.5))
    return seat_centers


def cast_iron_side(x, wear, mats, rng, side_sign):
    steel = mats["cast"] if wear == 1 else mats["steel_rust"] if wear == 3 and rng.random() < 0.55 else mats["cast"]
    # Open cast structure: all negative space remains genuinely open in every view.
    bend = 0.025 if wear == 3 and side_sign > 0 else 0.0
    tube("Cast curved front leg", [(x,-0.26,0.025),(x,-0.23,0.24),(x,-0.20,0.44),(x,-0.23,0.61)], 0.024, steel, 4)
    tube("Cast curved rear upright", [(x+bend,0.25,0.025),(x,0.18,0.27),(x,0.18,0.46),(x,0.26,0.80)], 0.024, steel, 4)
    tube("Cast arched armrest", [(x,-0.23,0.61),(x,-0.18,0.65),(x,0.00,0.66),(x,0.20,0.65)], 0.018, steel, 4)
    beam("Cast seat bearer", (x,-0.23,0.414), (x,0.23,0.414), 0.035, steel, depth=0.027, bevel=0.006)
    for y in (-0.26,0.25):
        box("Cast flared foot", (0.065,0.10,0.018), (x,y,0.009), steel, 0.006, 3)
    # Raised cast scrolls and leaf medallion are genuine rounded geometry.
    plane_x = x + side_sign * 0.013
    for direction in (-1, 1):
        center_y = -0.035 + direction * 0.085
        center_z = 0.265 + (0.035 if direction > 0 else 0)
        pts = []
        for i in range(42):
            t = i / 41
            angle = t * math.pi * 2.6
            rad = 0.014 + t * 0.082
            pts.append((plane_x, center_y + math.cos(angle) * rad, center_z + math.sin(angle) * rad))
        tube("Cast scroll ornament", pts, 0.009, steel, 2)
    # Small leaf-shaped cast medallion.
    leaf = box("Cast leaf boss", (0.014, 0.062, 0.092), (plane_x, -0.015, 0.36), steel, 0.012, 3, rotation=(0.0, math.radians(0), math.radians(30)))
    if wear == 2:
        # Slight sag in the armrest at one side only.
        tube("Old armrest repair bead", [(x, -0.25, 0.76), (x, -0.12, 0.774), (x, 0.02, 0.755), (x, 0.22, 0.79)], 0.013, mats["steel_rust"], 2)
    if wear == 3 and side_sign > 0:
        # Bent rear foot and one broken scroll remain attached to the frame.
        tube("Bent cast leg", [(x, 0.21, 0.40), (x+0.015, 0.23, 0.26), (x+0.032, 0.25, 0.09)], 0.025, mats["steel_rust"], 3)


def create_bench_01(wear, rng, mats):
    length = 1.78
    broken = rng.randrange(4)
    centers = add_wooden_slatted_body(1, wear, rng, mats, length, 5, 3, seat_width=0.080, seat_thick=0.036,
                                       seat_z=0.446, painted="wood_green", broken_index=broken, back_y=0.222,
                                       graffiti=wear >= 2)
    for x, sign in [(-0.74, -1), (0.74, 1)]:
        dx = 0.028 if wear == 3 and sign > 0 else 0.0
        cast_iron_side(x + dx, wear, mats, rng, sign)
        for y in (-0.18, 0.19):
            add_bolt("Cast bracket bolt", (x + sign*0.014, y, 0.43), (sign, 0, 0), mats["bolt"], wear == 3 and rng.random() < 0.25, True)
    beam("Underseat tie rail", (-0.75, 0.04, 0.39), (0.75, 0.04, 0.39), 0.035,
         mats["cast"] if wear < 3 else mats["steel_rust"], depth=0.030, bevel=0.006)
    add_grime_and_moss(wear, rng, length, 0.446, mats, 2)
    if wear == 2:
        add_text("Faded scratched Russian name", "ВИТЯ", (-0.40, 0.205, 0.68), 0.045, mats["graffiti"])
    if wear == 3:
        add_text("Old Russian warning", "НЕ САДИСЬ", (-0.56, 0.194, 0.69), 0.036, mats["graffiti"])
    return length


def create_bench_02(wear, rng, mats):
    length = 1.68
    broken = rng.randrange(4)
    add_wooden_slatted_body(2, wear, rng, mats, length, 4, 3, seat_width=0.093, seat_thick=0.040,
                            seat_z=0.448, painted="wood_blue", broken_index=broken, back_y=0.213)
    paint = mats["steel_green"] if rng.random() < 0.42 else mats["steel_blue"]
    if wear == 3:
        paint = mats["steel_rust"]
    for x in (-0.68, 0.68):
        bend = 0.052 if wear == 3 and x > 0 else 0.0
        points = [(x, -0.245, 0.008), (x, -0.235, 0.420), (x, 0.205, 0.420),
                  (x + bend, 0.220, 0.815), (x + bend, 0.275, 0.842)]
        for i in range(len(points)-1):
            beam("Welded square tube frame", points[i], points[i+1], 0.034, paint, depth=0.034, bevel=0.004)
        beam("Rear welded ground leg",(x,0.205,0.420),(x+bend,0.235,0.010),0.034,paint,depth=0.034,bevel=0.004)
        for foot_x,foot_y in ((x,-0.245),(x+bend,0.235)):
            box("Welded foot plate",(0.12,0.13,0.012),(foot_x,foot_y,0.006),paint,0.004,2)
            for offset in (-0.035,0.035):
                add_bolt("Ground anchor",(foot_x+offset,foot_y,0.012),(0,0,1),mats["bolt"],wear==3 and rng.random()<0.3,True)
        for y, z in [(-0.235, 0.40), (0.205, 0.40), (0.22, 0.58), (0.25, 0.80)]:
            # Weld beads at visible tube junctions.
            tube("Visible fillet weld", [(x-0.025, y, z), (x+0.025, y, z)], 0.0048, mats["steel_rust"] if wear > 1 else paint, 1)
    beam("Lower back cross rail", (-0.68, 0.223, 0.58), (0.68, 0.223, 0.58), 0.026, paint, depth=0.026, bevel=0.003)
    beam("Seat support cross rail", (-0.68, -0.015, 0.405), (0.68, -0.015, 0.405), 0.030, paint, depth=0.030, bevel=0.003)
    # One chipped old sticker on a single variant.
    if wear >= 2 and rng.random() < 0.60:
        box("Faded apartment notice", (0.105, 0.002, 0.063), (-0.36, 0.194, 0.68), mats["paper"], 0.001, 1)
        add_text("Sticker print", "ЖЭК", (-0.405, 0.191, 0.67), 0.025, mats["graffiti"], rotation=(math.pi/2,0,0))
    add_grime_and_moss(wear, rng, length, 0.448, mats, 2)
    if wear == 3:
        # Localized bent repair strip, bolted over one cracked timber slat.
        beam("Handmade flat strap repair", (0.43, -0.13, 0.475), (0.63, -0.13, 0.468), 0.018, mats["steel_rust"], depth=0.003, bevel=0.001)
        for x in (0.45,0.61):
            add_bolt("Repair strap bolt", (x,-0.13,0.478), (0,0,1), mats["bolt"], False)
    return length


def irregular_concrete_support(x, wear, mats, rng, side):
    width, depth, height = 0.32, 0.43, 0.415
    obj = box("Poured concrete support", (width, depth, height), (x, 0.0, height * 0.5), mats["concrete"], 0.018, 2)
    # Slightly tapered precast support with material removed at damaged corners.
    for v in obj.data.vertices:
        factor = 1.07 - (v.co.z / height + 0.5) * 0.16
        v.co.x *= factor
        v.co.y *= factor
        if v.co.z > -height * 0.40:
            v.co.x += rng.uniform(-0.006, 0.006)
            v.co.y += rng.uniform(-0.006, 0.006)
    if wear >= 2:
        for i in range(3 if wear == 2 else 6):
            yy = rng.choice([-1,1]) * depth * 0.49
            zz = rng.uniform(0.09, height*0.95)
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1, location=(x+side*width*0.48, yy, zz))
            cutter = bpy.context.object
            radius = rng.uniform(0.012,0.025) if wear == 2 else rng.uniform(0.025,0.055)
            cutter.scale = (radius,radius*1.4,radius*1.6)
            bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
            modifier = obj.modifiers.new("Actual spalled corner", "BOOLEAN")
            modifier.operation = "DIFFERENCE"; modifier.solver = "EXACT"; modifier.object = cutter
            apply_mod(obj, modifier)
            bpy.data.objects.remove(cutter, do_unlink=True)
    return obj


def create_bench_03(wear, rng, mats):
    length = 1.72
    broken = rng.randrange(5)
    add_wooden_slatted_body(3, wear, rng, mats, length, 5, 3, seat_width=0.075, seat_thick=0.040,
                            seat_z=0.448, painted="wood_brown", broken_index=broken, back_y=0.235)
    for x, side in [(-0.60,-1),(0.60,1)]:
        irregular_concrete_support(x, wear, mats, rng, side)
        # Embedded galvanized angles and visible fixing hardware.
        bracket_mat=mats["steel_rust"] if wear==3 else mats["steel_bare"]
        box("Embedded seat cleat",(0.26,0.40,0.014),(x,0.0,0.422),bracket_mat,0.003,2)
        beam("Embedded back bracket foot",(x,0.15,0.35),(x,0.26,0.52),0.035,bracket_mat,depth=0.028,bevel=0.003)
        beam("Concrete bench back upright",(x,0.26,0.52),(x,0.26,0.795),0.035,bracket_mat,depth=0.028,bevel=0.003)
    if wear == 3:
        beam("Exposed reinforcement", (-0.60,-0.11,0.19), (-0.60,0.08,0.20), 0.008, mats["steel_rust"], bevel=0.001)
    add_grime_and_moss(wear, rng, length, 0.448, mats, 3)
    if wear == 2:
        add_text("Carved year on concrete", "2024", (-0.67, -0.218, 0.21), 0.045, mats["black"], rotation=(math.pi/2,0,0))
    return length


def panel_point(u, v, surface, deform=0.0):
    if surface == "seat":
        y = v
        z = 0.445 + 0.022 * ((v + 0.055) / 0.25) ** 2
        n = Vector((0.0, -0.18*(v+0.055), 1.0)).normalized()
    else:
        z = v
        y = 0.14 + (z - 0.50) * 0.27
        n = Vector((0.0, -1.0, 0.27)).normalized()
    z += deform * (math.sin(u*5.3 + v*9.0) + 0.30*math.sin(u*12.0 - v*6.0))
    return Vector((u, y, z)), n


def perforated_panel(name, length, surface, v0, v1, wear, rng, material, far=False):
    cols = 16 if far else 28
    rows = (4 if surface == "seat" else 3) if far else (7 if surface == "seat" else 5)
    du = length / cols
    dv = (v1-v0) / rows
    hole_segments = 8 if far else 16
    vertices, faces = [], []
    deformation = 0.0 if wear == 1 else 0.002 if wear == 2 else 0.012
    omitted = set()
    if wear == 3:
        for _ in range(12):
            c = rng.randrange(cols)
            r = rng.randrange(rows)
            if (c/cols > 0.73 or c/cols < 0.12) and rng.random() < 0.8:
                omitted.add((c,r))
    thickness = 0.003
    hole_r = 0.010 if surface == "seat" else 0.009
    for c in range(cols):
        for r in range(rows):
            if (c,r) in omitted:
                continue
            u0 = -length*0.5 + c*du
            u1 = u0 + du
            a0 = v0 + r*dv
            a1 = a0 + dv
            cu, cv = (u0+u1)*0.5, (a0+a1)*0.5
            # Outer loop follows a square tile; inner loop is circular, producing a real through-hole.
            outer_coords = []
            inner_coords = []
            for i in range(hole_segments):
                ang = (i / hole_segments) * math.tau
                co, si = math.cos(ang), math.sin(ang)
                scale = 0.5 / max(abs(co), abs(si))
                outer_coords.append((cu + co*du*scale, cv + si*dv*scale))
                inner_coords.append((cu + co*hole_r, cv + si*hole_r))
            front_outer, front_inner, back_outer, back_inner = [], [], [], []
            for u,v in outer_coords:
                p,n = panel_point(u,v,surface,deformation)
                front_outer.append(len(vertices)); vertices.append(tuple(p+n*thickness*0.5))
            for u,v in inner_coords:
                p,n = panel_point(u,v,surface,deformation)
                front_inner.append(len(vertices)); vertices.append(tuple(p+n*thickness*0.5))
            for u,v in outer_coords:
                p,n = panel_point(u,v,surface,deformation)
                back_outer.append(len(vertices)); vertices.append(tuple(p-n*thickness*0.5))
            for u,v in inner_coords:
                p,n = panel_point(u,v,surface,deformation)
                back_inner.append(len(vertices)); vertices.append(tuple(p-n*thickness*0.5))
            for i in range(hole_segments):
                j = (i+1) % hole_segments
                # Visible front face, reverse side, and cylindrical wall of the open perforation.
                faces.append((front_outer[i], front_outer[j], front_inner[j], front_inner[i]))
                faces.append((back_outer[i], back_inner[i], back_inner[j], back_outer[j]))
                faces.append((front_inner[i], front_inner[j], back_inner[j], back_inner[i]))
                # Only the panel perimeter gets a side wall; adjoining tile seams stay flush.
                at_edge = (c == 0 and abs(outer_coords[i][0]-u0) < du*0.16 or
                           c == cols-1 and abs(outer_coords[i][0]-u1) < du*0.16 or
                           r == 0 and abs(outer_coords[i][1]-a0) < dv*0.16 or
                           r == rows-1 and abs(outer_coords[i][1]-a1) < dv*0.16)
                if at_edge:
                    faces.append((front_outer[i], back_outer[i], back_outer[j], front_outer[j]))
    mesh = bpy.data.meshes.new(name + " perforated sheet")
    mesh.from_pydata(vertices, [], faces)
    bm = bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.00001)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(mesh); bm.free()
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    assign_material(obj, material)
    return obj


def create_bench_04(wear, rng, mats):
    length = 1.82
    sheet_mat = mats["steel_gray"] if wear == 1 else mats["steel_bare"] if wear == 2 else mats["steel_rust"]
    perforated_panel("Perforated curved seat with open holes", length, "seat", -0.285, 0.145, wear, rng, sheet_mat)
    perforated_panel("Perforated angled back with open holes", length, "back", 0.535, 0.855, wear, rng, sheet_mat)
    frame = mats["steel_gray"] if wear == 1 else mats["steel_rust"]
    for x in (-0.77,0.77):
        k = 0.035 if wear == 3 and x > 0 else 0.0
        # Bent continuous profile gives the seat and back a real formed-sheet support.
        pts = [(x,-0.33,0.018),(x,-0.32,0.29),(x,-0.25,0.405),(x,0.12,0.425),
               (x,0.18,0.48),(x+k,0.27,0.83),(x+k,0.32,0.87)]
        tube("Rolled steel side frame", pts, 0.018, frame, 3)
        tube("Rear curved ground leg",[(x,0.12,0.425),(x,0.26,0.30),(x,0.32,0.018)],0.018,frame,3)
        # Under-panel saddles and formed seams.
        beam("Perforated seat cross support", (x,-0.12,0.40), (x,0.06,0.40), 0.022, frame, depth=0.022, bevel=0.003)
        for y,z in [(-0.26,0.43),(0.12,0.46)]:
            add_bolt("Panel mounting bolt", (x,y,z), (1,0,0), mats["bolt"], wear == 3 and rng.random()<0.38, True)
        for foot_y in (-0.33,0.32):
            box("Rolled frame foot plate",(0.10,0.09,0.012),(x,foot_y,0.006),frame,0.003,2)
            add_bolt("Rolled frame anchor",(x+0.03,foot_y,0.012),(0,0,1),mats["bolt"],False,True)
    # Shaped end returns and a shallow, folded hem along exposed metal edges.
    for sign in (-1,1):
        tube("Seat rolled hem", [(sign*0.88,-0.28,0.45),(sign*0.88,-0.08,0.455),(sign*0.88,0.14,0.47)],0.006,frame,2)
    if wear >= 2:
        for i in range(7 if wear == 2 else 16):
            surface = "seat" if i%2 == 0 else "back"
            low,high,rows = (-0.285,0.145,7) if surface == "seat" else (0.535,0.855,5)
            x = -length*0.5+(rng.randrange(28)+0.15)*length/28
            v = low+(rng.randrange(rows)+0.15)*(high-low)/rows
            point,normal = panel_point(x,v,surface,0.002 if wear==2 else 0.012)
            cylinder("Localized corrosion on sheet",rng.uniform(0.002,0.004),0.0006,point+normal*0.0018,mats["steel_rust"],normal,8,0)
    if wear == 3:
        # A single torn steel strip hangs from the damaged back edge.
        beam("Torn folded back fragment",(0.72,0.29,0.69),(0.82,0.31,0.57),0.027,mats["steel_rust"],depth=0.004,bevel=0.001)
    return length


def create_bench_05(wear, rng, mats):
    length = 1.52
    broken = rng.randrange(4)
    centers = [-0.153,-0.051,0.051,0.153]
    for i, y in enumerate(centers):
        absent = wear == 3 and i == broken and rng.random()<0.66
        if absent:
            continue
        material = mats["wood_raw"] if wear < 3 else mats["wood_rot"] if rng.random()<0.60 else mats["wood_raw"]
        timber = board(f"Backless bench seat board {i+1}",length,0.088,0.044,(0,y,0.441),material,rng,
              wear=wear,broken=(wear==3 and i==(broken+1)%4 and rng.random()<0.8),crack_material=mats["black"])
        board_hardware(length,(y,),0.465,y,wear,rng,mats["bolt"], (timber.location.x-timber.dimensions.x*0.5,timber.location.x+timber.dimensions.x*0.5))
    # Low welded angle brackets, fixed to two squat feet; one has a visible workshop repair.
    support = mats["steel_green"] if wear == 1 else mats["steel_rust"] if wear == 3 else mats["steel_bare"]
    for x in (-0.56,0.56):
        bend=0.055 if wear==3 and x>0 else 0
        pts=[(x,-0.19,0.025),(x,-0.19,0.40),(x,0.18,0.40),(x+bend,0.19,0.025)]
        for a,b in zip(pts,pts[1:]):
            beam("Low angle-leg support",a,b,0.032,support,depth=0.032,bevel=0.004)
        beam("Cross seat batten",(x,-0.19,0.401),(x,0.19,0.401),0.030,support,depth=0.030,bevel=0.003)
        for foot_x,foot_y in ((x,-0.19),(x+bend,0.19)):
            box("Small anchoring pad",(0.13,0.09,0.012),(foot_x,foot_y,0.006),support,0.003,1)
            for offset in (-0.035,0.035):
                add_bolt("Rough repair foot bolt",(foot_x+offset,foot_y,0.012),(0,0,1),mats["bolt"],wear==3 and rng.random()<0.30)
    if wear == 3:
        # Hand-cut metal strap bridges an old split board, offset so the repair reads from the front.
        box("Crude repair strap",(0.042,0.003,0.16),(0.34,-0.20,0.42),mats["steel_rust"],0.003,1,rotation=(math.radians(-8),0,math.radians(5)))
        for z in (0.36,0.48):
            add_bolt("Crude repair rivet",(0.34,-0.203,z),(0,-1,0),mats["bolt"],False)
    add_grime_and_moss(wear,rng,length,0.441,mats,2)
    if wear == 2 and rng.random()<0.7:
        add_text("Initials carved into timber","АНЯ",(-0.25,-0.18,0.467),0.033,mats["black"],rotation=(0,0,0))
    return length


BUILDERS = {1:create_bench_01,2:create_bench_02,3:create_bench_03,4:create_bench_04,5:create_bench_05}


def join_meshes(objects, name):
    mesh_objects = [o for o in objects if o and o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in mesh_objects:
        obj.select_set(True)
    active = mesh_objects[0]
    bpy.context.view_layer.objects.active = active
    bpy.ops.object.join()
    active.name = name
    active.data.name = name + "Mesh"
    # Preserve each component's authored split/weighted normals through the join.
    bpy.context.scene.cursor.location = (0,0,0)
    bpy.context.view_layer.objects.active = active
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR", center="MEDIAN")
    return active


def unwrap_component(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    for layer in list(obj.data.uv_layers): obj.data.uv_layers.remove(layer)
    base = obj.data.uv_layers.new(name="UVMap")
    offset = random.Random(SEED + sum(ord(c) for c in obj.name)).uniform(0.05, 0.85)
    timber = bool(obj.get("TimberUV", False))
    for poly in obj.data.polygons:
        normal = poly.normal
        axis = max(range(3), key=lambda i: abs(normal[i]))
        for loop_index in poly.loop_indices:
            point = obj.data.vertices[obj.data.loops[loop_index].vertex_index].co
            if timber:
                # Grain runs along the length on all four long faces; two metres per texture repeat.
                v = point.y if axis == 2 else point.z
                base.data[loop_index].uv = (point.x / 2.0 + offset, v / 0.45 + offset)
            else:
                axes = ((1,2),(0,2),(0,1))[axis]
                base.data[loop_index].uv = (point[axes[0]] * 2.0 + offset, point[axes[1]] * 2.0 + offset)
    obj.data.uv_layers.new(name="LightmapUV")
    obj.data.uv_layers.active_index = 1
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    # A lower angle threshold splits tight metal folds and bevel transitions before atlas packing.
    # This avoids local UV foldovers that the default 66-degree smart projection can leave on cast parts.
    bpy.ops.uv.smart_project(angle_limit=math.radians(20), island_margin=0.010,
                             area_weight=0.1, rotate_method="AXIS_ALIGNED")
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.data.uv_layers.active_index = 0
    obj.data.uv_layers[0].active_render = True


def pack_uv_and_create_lightmap(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    obj.data.uv_layers.active_index = obj.data.uv_layers.find("LightmapUV")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.data.uv_layers.active_index = obj.data.uv_layers.find("UVMap")
    obj.data.uv_layers.active.active_render = True


def mesh_triangles(obj):
    return sum(max(0, len(p.vertices)-2) for p in obj.data.polygons)


def create_lods(parts, base_name, collection):
    # Bake every component transform into the mesh before joining; this keeps the root pivot at floor center.
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        if not part.get("TimberUV", False):
            bm = bmesh.new(); bm.from_mesh(part.data)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
            bm.to_mesh(part.data); bm.free()
        bpy.context.view_layer.objects.active=part
        part.select_set(True)
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        part.select_set(False)
    for part in parts:
        unwrap_component(part)
    far_parts = []
    if base_name.startswith("Bench_RU_04_"):
        for part in parts:
            if "Perforated curved seat" in part.name or "Perforated angled back" in part.name: continue
            duplicate = part.copy(); duplicate.data = part.data.copy()
            collection.objects.link(duplicate); far_parts.append(duplicate)
        wear = int(base_name[-2:])
        material = next(p.data.materials[0] for p in parts if "Perforated curved seat" in p.name)
        for surface, low, high in (("seat",-0.285,0.145),("back",0.535,0.855)):
            panel = perforated_panel("Far perforation " + surface,1.82,surface,low,high,wear,random.Random(SEED+wear),material,far=True)
            move_to_collection(panel,collection); unwrap_component(panel); far_parts.append(panel)
    bpy.context.view_layer.update()
    lod0 = join_meshes(parts, "LOD0")
    move_to_collection(lod0, collection)
    lod0.location = (0,0,0)
    lod0.rotation_euler = (0,0,0)
    lod0.scale = (1,1,1)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    pack_uv_and_create_lightmap(lod0)
    lod_objects = [lod0]
    for name, ratio in (("LOD1",0.46),("LOD2",0.16)):
        if name == "LOD2" and far_parts:
            obj = join_meshes(far_parts,name)
            pack_uv_and_create_lightmap(obj)
            ratio = 0.45
        else:
            mesh = lod0.data.copy()
            obj = bpy.data.objects.new(name, mesh)
            collection.objects.link(obj)
        obj.location=(0,0,0)
        dec = obj.modifiers.new("Silhouette-preserving screen-size reduction", "DECIMATE")
        dec.decimate_type="COLLAPSE"
        dec.ratio=ratio
        dec.use_collapse_triangulate=True
        bpy.context.view_layer.objects.active=obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=dec.name)
        if name == "LOD2" and base_name in ("Bench_RU_01_Wear01","Bench_RU_05_Wear03"):
            obj.data.uv_layers.active_index=obj.data.uv_layers.find("LightmapUV")
            bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.uv.smart_project(angle_limit=math.radians(10),island_margin=0.004,area_weight=0.1,rotate_method="AXIS_ALIGNED")
            bpy.ops.uv.pack_islands(rotate=True,margin=0.004)
            bpy.ops.object.mode_set(mode="OBJECT")
            obj.data.uv_layers.active_index=obj.data.uv_layers.find("UVMap")
            obj.data.uv_layers.active.active_render=True
        lod_objects.append(obj)
    for lod in lod_objects:
        lod.name = lod.name.replace(".001", "")
        lod["AssetRootPivot"] = "bottom-center; all transforms applied"
        lod["TriangleCount"] = mesh_triangles(lod)
        lod["UV0"] = "UVMap"
        lod["UV1"] = "LightmapUV (non-overlapping smart projection)"
    return lod_objects


def export_fbx(objects, path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={"MESH"},
        axis_forward="-Z", axis_up="Y", global_scale=1.0,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False, use_mesh_modifiers=True,
        mesh_smooth_type="FACE", use_mesh_modifiers_render=True,
        add_leaf_bones=False, bake_anim=False, path_mode="AUTO",
    )


def build_all(root_collection, materials):
    results=[]
    for kind in range(1,6):
        for wear in range(1,4):
            asset_name=f"Bench_RU_{kind:02d}_Wear{wear:02d}"
            print(f"BUILD_START {asset_name}",flush=True)
            asset_col=make_collection(asset_name,root_collection)
            parts_col=make_collection("Authoring_Parts",asset_col)
            lod_col=make_collection("Export_LODs",asset_col)
            rng=random.Random(SEED + kind*100 + wear*17)
            before=set(bpy.data.objects.keys())
            builder=BUILDERS[kind]
            builder(wear,rng,materials)
            created=[obj for obj in bpy.data.objects if obj.name not in before and obj.type=="MESH"]
            for obj in created:
                move_to_collection(obj,parts_col)
            # Keep an editable authoring collection while a copy is joined and reduced for export.
            export_parts=[]
            for obj in created:
                duplicate=obj.copy()
                duplicate.data=obj.data.copy()
                duplicate.name="ExportSource_"+obj.name
                lod_col.objects.link(duplicate)
                export_parts.append(duplicate)
            parts_col.hide_render=True
            parts_col.hide_viewport=True
            lods=create_lods(export_parts,asset_name,lod_col)
            tris=[mesh_triangles(obj) for obj in lods]
            MODEL_DIR.mkdir(parents=True,exist_ok=True)
            export_fbx(lods,MODEL_DIR/f"{asset_name}.fbx")
            results.append({"name":asset_name,"lodTriangles":tris,"dimensions":tuple(round(v,4) for v in lods[0].dimensions)})
            print(f"BUILD_DONE {asset_name} LOD={tris} bounds={tuple(round(v,3) for v in lods[0].dimensions)}",flush=True)
    return results


def main():
    MODEL_DIR.mkdir(parents=True,exist_ok=True)
    SOURCE_DIR.mkdir(parents=True,exist_ok=True)
    root=clear_scene()
    global mats
    mats=build_materials()
    results=build_all(root,mats)
    for image in bpy.data.images:
        if image.source=="FILE":
            try:
                image.filepath="//../Textures/"+Path(image.filepath).name
            except Exception:
                pass
    bpy.context.preferences.filepaths.save_version = 0
    bpy.data.orphans_purge(do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR/"Benches_RU_Master.blend"))
    print("ALL_BUILDS_DONE",results,flush=True)
    os._exit(0)


if __name__ == "__main__":
    main()
