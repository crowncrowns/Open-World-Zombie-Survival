from __future__ import annotations

import math
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
        "wood_green": make_material("M_WoodPaint_Green", "WoodPaint", (0.57, 0.64, 0.43), 0.02, 0.7),
        "wood_blue": make_material("M_WoodPaint_Blue", "WoodPaint", (0.48, 0.58, 0.88), 0.02, 0.7),
        "wood_brown": make_material("M_WoodPaint_Brown", "WoodPaint", (0.73, 0.57, 0.39), 0.02, 0.74),
        "wood_raw": make_material("M_WoodWeathered", "WoodRaw", (0.92, 0.91, 0.88), 0.0, 0.82),
        "wood_rot": make_material("M_WoodRotten", "WoodRaw", (0.53, 0.48, 0.37), 0.0, 0.92),
        "steel_green": make_material("M_SteelPaint_Green", "SteelPaint", (0.42, 0.56, 0.32), 0.28, 0.61),
        "steel_blue": make_material("M_SteelPaint_Blue", "SteelPaint", (0.45, 0.60, 0.92), 0.27, 0.61),
        "steel_gray": make_material("M_SteelPaint_Gray", "SteelPaint", (0.56, 0.58, 0.57), 0.30, 0.58),
        "steel_bare": make_material("M_SteelBare", "SteelPaint", (0.75, 0.77, 0.76), 0.76, 0.43),
        "steel_rust": make_material("M_SteelRust", "SteelRust", (0.98, 0.95, 0.91), 0.28, 0.84),
        "cast": make_material("M_CastIron", "CastIron", (0.83, 0.84, 0.80), 0.74, 0.74),
        "concrete": make_material("M_Concrete_Porous", "Concrete", (0.98, 0.98, 0.95), 0.0, 0.91),
        "moss": make_material("M_Moss", None, (0.105, 0.17, 0.07), 0.0, 0.96),
        "dirt": make_material("M_Dirt_Dark", None, (0.115, 0.092, 0.065), 0.0, 0.96),
        "graffiti": make_material("M_Graffiti_FadedRed", None, (0.46, 0.12, 0.09), 0.0, 0.82),
        "paper": make_material("M_Sticker_OldPaper", None, (0.50, 0.43, 0.30), 0.0, 0.9),
        "bolt": make_material("M_Bolt_Galvanized", "SteelPaint", (0.80, 0.82, 0.78), 0.82, 0.38),
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
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for index, point in enumerate(points):
        spline.points[index].co = (*point, 1.0)
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    assign_material(obj, material)
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
    # Dense, gently warped timber surfaces make broad highlights and edge wear real geometry.
    if wear == 2:
        nx, ny = (48, 4)
    elif wear == 3 and "Backless" not in name:
        nx, ny = (50, 5)
    else:
        nx, ny = (72, 6)
    wave_phase = rng.random() * math.tau
    verts = []
    for top in (True, False):
        for ix in range(nx+1):
            x = (ix/nx - 0.5) * length
            for iy in range(ny+1):
                y = (iy/ny - 0.5) * width
                wave = (math.sin(x*5.4 + y*22.0 + wave_phase) * 0.40 +
                        math.sin(x*1.7 - y*38.0 + wave_phase*0.7) * 0.34 +
                        math.sin(x*13.0 + y*4.0 + wave_phase*1.3) * 0.26) * jitter
                z = thick*0.5 + wave if top else -thick*0.5
                verts.append((x,y,z))
    layer_count=(nx+1)*(ny+1)
    faces=[]
    top_start=0
    bottom_start=layer_count
    for ix in range(nx):
        for iy in range(ny):
            a=top_start+ix*(ny+1)+iy
            faces.append((a,a+ny+1,a+ny+2,a+1))
            b=bottom_start+ix*(ny+1)+iy
            faces.append((b,b+1,b+ny+2,b+ny+1))
    # Sidewalls are a single quad per edge segment; the bevel modifier rounds only the exterior.
    for ix in range(nx):
        for iy in (0,ny):
            t0=top_start+ix*(ny+1)+iy; t1=t0+ny+1
            b0=bottom_start+ix*(ny+1)+iy; b1=b0+ny+1
            faces.append((t0,b0,b1,t1))
    for iy in range(ny):
        for ix in (0,nx):
            t0=top_start+ix*(ny+1)+iy; t1=t0+1
            b0=bottom_start+ix*(ny+1)+iy; b1=b0+1
            faces.append((t0,t1,b1,b0))
    mesh=bpy.data.meshes.new(name+"DenseTimberMesh")
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    obj.location=loc
    obj.rotation_euler=(rotation_x,0,0)
    assign_material(obj,material)
    for poly in obj.data.polygons:
        poly.use_smooth = poly.index < nx*ny*2
    bevel=obj.modifiers.new("Rounded timber arris", "BEVEL")
    bevel.width=min(0.006,thick*0.17)
    bevel.segments=2
    bevel.limit_method="ANGLE"
    apply_mod(obj,bevel)
    for vertex in obj.data.vertices:
        if abs(vertex.co.z) > thick*0.43:
            vertex.co.x += rng.uniform(-jitter*0.08,jitter*0.08)
    if broken:
        # Two tapered wood splinters protrude from the snapped edge.
        sign = 1 if loc[0] > 0 else -1
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


def board_hardware(length, centers, z, y, wear, rng, material):
    for ci, cy in enumerate(centers):
        for x in (-length * 0.34, length * 0.34):
            missing = wear == 2 and rng.random() < 0.08 or wear == 3 and rng.random() < 0.22
            add_bolt(f"SeatFastener_{ci}_{x:.2f}", (x, cy, z), (0, 0, 1), material, missing)


def back_hardware(length, z, y, wear, rng, material):
    for x in (-length * 0.34, length * 0.34):
        missing = wear == 2 and rng.random() < 0.11 or wear == 3 and rng.random() < 0.32
        add_bolt(f"BackFastener_{x:.2f}_{z:.2f}", (x, y, z), (0, -1, 0), material, missing)


def add_grime_and_moss(wear, rng, length, seat_z, mats, amount=1):
    if wear < 2:
        return
    for i in range(amount * (2 if wear == 3 else 1)):
        x = rng.uniform(-length * 0.38, length * 0.38)
        y = rng.uniform(-0.18, 0.18)
        z = seat_z + 0.028 + rng.uniform(0.0, 0.003)
        # Small, irregular dried dirt pads sit on seams; they stay out of primary support surfaces.
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=1.0, location=(x, y, z))
        obj = bpy.context.object
        obj.name = "Localized grime deposit"
        obj.scale = (rng.uniform(0.025, 0.052), rng.uniform(0.009, 0.020), rng.uniform(0.0015, 0.0030))
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        assign_material(obj, mats["dirt"])
        for p in obj.data.polygons:
            p.use_smooth = True
    if wear == 3:
        for i in range(4):
            x = rng.choice([-1, 1]) * rng.uniform(length * 0.28, length * 0.40)
            y = rng.choice([-1, 1]) * rng.uniform(0.08, 0.21)
            z = rng.uniform(0.20, seat_z + 0.03)
            h = rng.uniform(0.018, 0.050)
            bpy.ops.mesh.primitive_cone_add(vertices=5, radius1=rng.uniform(0.007, 0.015), radius2=0.001,
                                            depth=h, location=(x, y, z + h * 0.5))
            obj = bpy.context.object
            obj.name = "Moss tuft at joint"
            assign_material(obj, mats["moss"])


def add_text(name, text, loc, size, material, rotation=(math.pi / 2, 0, 0), font_path=r"C:\Windows\Fonts\arial.ttf"):
    curve = bpy.data.curves.new(name + "Text", "FONT")
    try:
        curve.font = bpy.data.fonts.load(font_path, check_existing=True)
    except Exception:
        pass
    curve.body = text
    curve.size = size
    curve.extrude = 0.0008
    curve.bevel_depth = 0.0003
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
        mat = mats[painted] if wear == 1 else (mats["wood_raw"] if wear == 2 else mats["wood_rot"] if rng.random() < 0.42 else mats["wood_raw"])
        broken = wear == 3 and i == (broken_index + 1) % seat_count and rng.random() < 0.78
        board(f"Seat board {i+1}", length, seat_width, seat_thick,
              (rng.uniform(-0.012, 0.012), cy, seat_z), mat, rng, wear=wear, broken=broken, crack_material=mats["black"])
        board_hardware(length, (cy,), seat_z + seat_thick * 0.50, cy, wear, rng, mats["bolt"])
    if back_count > 0:
        z_base = 0.59
        back_h = 0.072 if back_count <= 3 else 0.065
        for j in range(back_count):
            z = z_base + j * (back_h + 0.018)
            absent = wear == 3 and j == (broken_index or 0) % back_count and rng.random() < 0.62
            if absent:
                continue
            mat = mats[painted] if wear == 1 else mats["wood_raw"] if wear == 2 or rng.random() < 0.62 else mats["wood_rot"]
            broken = wear == 3 and j == (broken_index or 0) % back_count and not absent
            board(f"Back slat {j+1}", length, 0.032, back_h,
                  (0, back_y, z), mat, rng, rotation_x=math.radians(-11), wear=wear, broken=broken, crack_material=mats["black"])
            back_hardware(length, z, back_y - 0.020, wear, rng, mats["bolt"])
    return seat_centers


def cast_iron_side(x, wear, mats, rng, side_sign):
    steel = mats["cast"] if wear == 1 else mats["steel_rust"] if wear == 3 and rng.random() < 0.55 else mats["cast"]
    # Broad cast side plate with a period-style curved armrest and scrollwork.
    outer = [
        (x, -0.285, 0.006), (x, -0.240, 0.430), (x, -0.225, 0.535),
        (x, 0.150, 0.535), (x, 0.215, 0.790), (x, 0.280, 0.790),
        (x, 0.250, 0.455), (x, 0.205, 0.395), (x, 0.245, 0.055)
    ]
    thickness = 0.017
    verts = [(p[0] - thickness * 0.5, p[1], p[2]) for p in outer] + [(p[0] + thickness * 0.5, p[1], p[2]) for p in outer]
    n = len(outer)
    faces = [tuple(range(n)), tuple(range(n, 2*n))]
    for i in range(n):
        faces.append((i, (i+1) % n, (i+1) % n + n, i+n))
    mesh = bpy.data.meshes.new("Cast side profile")
    mesh.from_pydata(verts, [], faces)
    side = bpy.data.objects.new("Cast iron side cheek", mesh)
    bpy.context.collection.objects.link(side)
    assign_material(side, steel)
    mod = side.modifiers.new("Soft foundry edge", "BEVEL")
    mod.width, mod.segments = 0.006, 2
    apply_mod(side, mod)
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
        # Foot plates and M10 anchors.
        box("Foot plate", (0.12, 0.25, 0.012), (x, -0.02, 0.006), paint, 0.004, 2)
        for y in (-0.095, 0.055):
            add_bolt("Ground anchor", (x, y, 0.015), (0, 0, 1), mats["bolt"], wear == 3 and rng.random() < 0.3, True)
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
    width, depth, height = 0.32, 0.43, 0.435
    obj = box("Poured concrete support", (width, depth, height), (x, 0.0, height * 0.5), mats["concrete"], 0.018, 2)
    # Irregular chipped corners and shallow pitting are mesh geometry, concentrated above the ground face.
    for v in obj.data.vertices:
        if v.co.z > -height * 0.40:
            v.co.x += rng.uniform(-0.006, 0.006)
            v.co.y += rng.uniform(-0.006, 0.006)
    if wear >= 2:
        for i in range(5 if wear == 2 else 9):
            yy = rng.choice([-1,1]) * rng.uniform(depth*0.28, depth*0.49)
            zz = rng.uniform(0.08, height*0.87)
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1, location=(x+side*width*0.49, yy, zz))
            chip = bpy.context.object
            chip.name = "Concrete corner chip recess"
            chip.scale = (0.012 if wear == 2 else 0.020, 0.018, 0.014)
            bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
            assign_material(chip, mats["dirt"])
    return obj


def create_bench_03(wear, rng, mats):
    length = 1.72
    broken = rng.randrange(5)
    add_wooden_slatted_body(3, wear, rng, mats, length, 5, 3, seat_width=0.075, seat_thick=0.040,
                            seat_z=0.448, painted="wood_brown", broken_index=broken, back_y=0.235)
    for x, side in [(-0.60,-1),(0.60,1)]:
        irregular_concrete_support(x, wear, mats, rng, side)
        # Embedded galvanized angles and visible fixing hardware.
        box("Embedded seat cleat", (0.26, 0.40, 0.045), (x, 0.0, 0.455), mats["steel_rust"] if wear == 3 else mats["steel_bare"], 0.004, 2)
        for y in (-0.15,0.15):
            add_bolt("Concrete insert anchor", (x, y, 0.482), (0,0,1), mats["bolt"], wear == 3 and rng.random() < 0.22, True)
        if wear >= 2:
            # Old pale limewash remains as a thin irregular patch on one side.
            for i in range(2):
                box("Flaked limewash", (0.003, rng.uniform(0.06,0.12), rng.uniform(0.10,0.20)),
                    (x+side*0.164, rng.uniform(-0.10,0.10), rng.uniform(0.18,0.34)), mats["paper"], 0.003, 1)
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


def perforated_panel(name, length, surface, v0, v1, wear, rng, material):
    cols = 20
    rows = 5 if surface == "seat" else 4
    du = length / cols
    dv = (v1-v0) / rows
    hole_segments = 14
    vertices, faces = [], []
    deformation = 0.0 if wear < 3 else 0.012
    omitted = set()
    if wear == 2:
        omitted.add((rng.randrange(cols), rng.randrange(rows)))
    elif wear == 3:
        for _ in range(12):
            c = rng.randrange(cols)
            r = rng.randrange(rows)
            if (c/cols > 0.73 or c/cols < 0.12) and rng.random() < 0.8:
                omitted.add((c,r))
    thickness = 0.003
    hole_r = 0.014 if surface == "seat" else 0.013
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
                outer_coords.append((cu + co*du*scale*0.985, cv + si*dv*scale*0.985))
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
        pts = [(x,-0.33,0.055),(x,-0.32,0.29),(x,-0.25,0.405),(x,0.12,0.425),
               (x,0.18,0.48),(x+k,0.27,0.83),(x+k,0.32,0.87)]
        tube("Rolled steel side frame", pts, 0.018, frame, 3)
        # Under-panel saddles and formed seams.
        beam("Perforated seat cross support", (x,-0.12,0.40), (x,0.06,0.40), 0.022, frame, depth=0.022, bevel=0.003)
        for y,z in [(-0.26,0.43),(0.12,0.46)]:
            add_bolt("Panel mounting bolt", (x,y,z), (1,0,0), mats["bolt"], wear == 3 and rng.random()<0.38, True)
        cylinder("Ground anchor boss",0.022,0.020,(x,-0.24,0.010),frame,(0,0,1),12,0.002)
    # Shaped end returns and a shallow, folded hem along exposed metal edges.
    for sign in (-1,1):
        tube("Seat rolled hem", [(sign*0.88,-0.28,0.45),(sign*0.88,-0.08,0.455),(sign*0.88,0.14,0.47)],0.006,frame,2)
    if wear >= 2:
        for i in range(7 if wear == 2 else 16):
            x = rng.uniform(-0.86,0.86)
            z = rng.uniform(0.18,0.66)
            y = 0.22 if z > 0.50 else -0.18
            cylinder("Localized corrosion pit",rng.uniform(0.003,0.008),0.0015,(x,y,z),mats["steel_rust"],(0,-1,0),8,0)
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
        board(f"Backless bench seat board {i+1}",length,0.088,0.044,(0,y,0.441),material,rng,
              wear=wear,broken=(wear==3 and i==(broken+1)%4 and rng.random()<0.8),crack_material=mats["black"])
        board_hardware(length,(y,),0.465,y,wear,rng,mats["bolt"])
    # Low welded angle brackets, fixed to two squat feet; one has a visible workshop repair.
    support = mats["steel_green"] if wear == 1 else mats["steel_rust"] if wear == 3 else mats["steel_bare"]
    for x in (-0.56,0.56):
        bend=0.055 if wear==3 and x>0 else 0
        pts=[(x,-0.19,0.025),(x,-0.19,0.40),(x,0.18,0.40),(x+bend,0.19,0.025)]
        for a,b in zip(pts,pts[1:]):
            beam("Low angle-leg support",a,b,0.032,support,depth=0.032,bevel=0.004)
        beam("Cross seat batten",(x,-0.19,0.401),(x,0.19,0.401),0.030,support,depth=0.030,bevel=0.003)
        box("Small anchoring pad",(0.13,0.10,0.012),(x,0,0.006),support,0.003,1)
        for y in (-0.03,0.03):
            add_bolt("Rough repair foot bolt",(x,y,0.012),(0,0,1),mats["bolt"],wear==3 and rng.random()<0.30)
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
    # Recalculate consistent exterior winding after the parts are joined.
    bm = bmesh.new()
    bm.from_mesh(active.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(active.data)
    bm.free()
    bpy.context.scene.cursor.location = (0,0,0)
    bpy.context.view_layer.objects.active = active
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR", center="MEDIAN")
    return active


def unwrap_component(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    for layer in list(obj.data.uv_layers):
        obj.data.uv_layers.remove(layer)
    obj.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    # A lower angle threshold splits tight metal folds and bevel transitions before atlas packing.
    # This avoids local UV foldovers that the default 66-degree smart projection can leave on cast parts.
    bpy.ops.uv.smart_project(angle_limit=math.radians(20), island_margin=0.010,
                             area_weight=0.1, rotate_method="AXIS_ALIGNED")
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.data.uv_layers.active.name = "UVMap"


def pack_uv_and_create_lightmap(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=0.025)
    bpy.ops.object.mode_set(mode="OBJECT")
    for layer in list(obj.data.uv_layers):
        if layer.name != "UVMap":
            obj.data.uv_layers.remove(layer)
    obj.data.uv_layers.active = obj.data.uv_layers.get("UVMap")
    lightmap = obj.data.uv_layers.new(name="LightmapUV")
    source = obj.data.uv_layers.get("UVMap")
    for i in range(len(source.data)):
        lightmap.data[i].uv = source.data[i].uv


def mesh_triangles(obj):
    return sum(max(0, len(p.vertices)-2) for p in obj.data.polygons)


def create_lods(parts, base_name, collection):
    # Bake every component transform into the mesh before joining; this keeps the root pivot at floor center.
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        bpy.context.view_layer.objects.active=part
        part.select_set(True)
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        part.select_set(False)
    for part in parts:
        unwrap_component(part)
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
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR/"Benches_RU_Master.blend"))
    print("ALL_BUILDS_DONE",results,flush=True)


if __name__ == "__main__":
    main()
