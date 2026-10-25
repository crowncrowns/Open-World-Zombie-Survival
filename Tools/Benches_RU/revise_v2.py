from pathlib import Path

path = Path(__file__).with_name("build_benches.py")
text = path.read_text(encoding="utf-8")
start = text.index("    # Dense, gently warped timber")
end = text.index("    if broken:\n        # Two tapered", start)
text = text[:start] + '''    nx, ny = 64, 4
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
''' + text[end:]
start = text.index("    # Broad cast side plate")
end = text.index("    # Raised cast scrolls", start)
text = text[:start] + '''    # Open cast structure: all negative space remains genuinely open in every view.
    bend = 0.025 if wear == 3 and side_sign > 0 else 0.0
    tube("Cast curved front leg", [(x,-0.26,0.025),(x,-0.23,0.24),(x,-0.20,0.44),(x,-0.23,0.61)], 0.024, steel, 4)
    tube("Cast curved rear upright", [(x+bend,0.25,0.025),(x,0.18,0.27),(x,0.18,0.46),(x,0.26,0.80)], 0.024, steel, 4)
    tube("Cast arched armrest", [(x,-0.23,0.61),(x,-0.18,0.65),(x,0.00,0.66),(x,0.20,0.65)], 0.018, steel, 4)
    beam("Cast seat bearer", (x,-0.23,0.414), (x,0.23,0.414), 0.035, steel, depth=0.027, bevel=0.006)
    for y in (-0.26,0.25):
        box("Cast flared foot", (0.065,0.10,0.018), (x,y,0.009), steel, 0.006, 3)
''' + text[end:]
# Join/recalculation no longer changes the authored smooth normal field after it was weighted.
path.write_text(text, encoding="utf-8")
print("V2_GEOMETRY_REVISED")
