import bpy
import bmesh
import json
import math
import random
from collections import defaultdict
from pathlib import Path
from mathutils import Vector


ROOT_DIR = Path(__file__).resolve().parents[1]
ASSET_DIR = ROOT_DIR / "UnityProject" / "Assets" / "Environment" / "Windows" / "MetalGrilles"
MODEL_DIR = ASSET_DIR / "Models"
SOURCE_DIR = ROOT_DIR / "Source"
PREVIEW_DIR = ROOT_DIR / "Verification" / "BlenderPreview"
STATES = ("A", "B", "C")
TYPE_INFO = {
    1: ("Classic", "Классическая советская"),
    2: ("Forged", "Декоративная кованая"),
    3: ("Reinforced", "Усиленная антивандальная"),
    4: ("Survivor", "Самодельная постапокалиптическая"),
    5: ("Hinged", "Старая распашная"),
}
PAINT_COLOR = {
    1: (0.22, 0.31, 0.13, 1),
    2: (0.13, 0.14, 0.13, 1),
    3: (0.25, 0.26, 0.24, 1),
    4: (0.34, 0.27, 0.19, 1),
    5: (0.20, 0.24, 0.18, 1),
}
STATE_TINT = {
    "A": (0.88, 0.94, 0.82, 1),
    "B": (0.92, 0.87, 0.75, 1),
    "C": (0.72, 0.67, 0.56, 1),
}
LOD_DATA = ((20, 3, 1.0), (12, 2, 0.66), (8, 1, 0.36), (4, 0, 0.16))


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.collections,):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def create_material(name, color, roughness, metallic, texture_name=None, detail_name=None):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.roughness = roughness
    mat.metallic = metallic
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if texture_name:
        nodes=mat.node_tree.nodes
        links=mat.node_tree.links
        base=bpy.data.images.load(str(ASSET_DIR/"Textures"/f"T_{texture_name}_BaseColor.png"),check_existing=True)
        mask=bpy.data.images.load(str(ASSET_DIR/"Textures"/f"T_{texture_name}_MetallicSmoothness.png"),check_existing=True)
        detail_name=detail_name or texture_name
        normal=bpy.data.images.load(str(ASSET_DIR/"Textures"/f"T_{detail_name}_Normal.png"),check_existing=True)
        ao=bpy.data.images.load(str(ASSET_DIR/"Textures"/f"T_{detail_name}_AO.png"),check_existing=True)
        def image_node(label,image,is_data=False):
            n=nodes.new("ShaderNodeTexImage"); n.label=label; n.image=image; n.interpolation="Linear"
            if is_data: image.colorspace_settings.name="Non-Color"
            return n
        base_node=image_node("4K paint / oxide color",base)
        mask_node=image_node("Metallic (R) / Smoothness (A)",mask,True)
        normal_node=image_node("4K tangent normal",normal,True)
        ao_node=image_node("4K ambient occlusion",ao,True)
        separate=nodes.new("ShaderNodeSeparateColor"); separate.mode="RGB"
        links.new(mask_node.outputs["Color"],separate.inputs["Color"])
        invert=nodes.new("ShaderNodeMath"); invert.operation="SUBTRACT"; invert.inputs[0].default_value=1.0
        links.new(mask_node.outputs["Alpha"],invert.inputs[1])
        normal_map=nodes.new("ShaderNodeNormalMap"); normal_map.inputs["Strength"].default_value=.45
        links.new(normal_node.outputs["Color"],normal_map.inputs["Color"])
        links.new(normal_map.outputs["Normal"],shader.inputs["Normal"])
        mix=nodes.new("ShaderNodeMixRGB"); mix.blend_type="MULTIPLY"; mix.inputs[0].default_value=.36
        links.new(base_node.outputs["Color"],mix.inputs[1]); links.new(ao_node.outputs["Color"],mix.inputs[2])
        links.new(mix.outputs["Color"],shader.inputs["Base Color"])
        links.new(separate.outputs["Red"],shader.inputs["Metallic"])
        links.new(invert.outputs[0],shader.inputs["Roughness"])
        base_node.location=(-640,250); mask_node.location=(-640,-40); normal_node.location=(-640,-360); ao_node.location=(-640,500)
        separate.location=(-380,-30); invert.location=(-170,-70); normal_map.location=(-370,-350); mix.location=(-350,300)
    return mat


def empty_obj(name, collection, parent=None, loc=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.04
    collection.objects.link(obj)
    obj.location = loc
    if parent:
        obj.parent = parent
    return obj


def mesh_obj(name, verts, faces, material, collection, smooth=False):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    if material:
        mesh.materials.append(material)
    for poly in mesh.polygons:
        poly.use_smooth = smooth
    return obj


def bevel_box(obj, width, segments):
    if width <= 0 or segments <= 0:
        return
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new("Machined edge bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.profile = 0.5
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(18)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False)


def box_mesh(name, center, dims, material, collection, bevel=0.0, bevel_segments=0):
    x, y, z = center
    dx, dy, dz = (d * 0.5 for d in dims)
    verts = [
        (x-dx, y-dy, z-dz), (x+dx, y-dy, z-dz), (x+dx, y+dy, z-dz), (x-dx, y+dy, z-dz),
        (x-dx, y-dy, z+dz), (x+dx, y-dy, z+dz), (x+dx, y+dy, z+dz), (x-dx, y+dy, z+dz),
    ]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    obj = mesh_obj(name, verts, faces, material, collection, False)
    bevel_box(obj, min(bevel, min(dims) * 0.30), bevel_segments)
    return obj


def beam_mesh(name, a, b, width, depth, material, collection, bevel=0.0, bevel_segments=0):
    a, b = Vector(a), Vector(b)
    axis = (b - a).normalized()
    ref = Vector((0, 0, 1))
    if abs(axis.dot(ref)) > 0.92:
        ref = Vector((0, 1, 0))
    u = axis.cross(ref).normalized()
    v = axis.cross(u).normalized()
    verts = []
    for p in (a, b):
        for su, sv in ((-1,-1),(1,-1),(1,1),(-1,1)):
            q = p + u * (su * width * 0.5) + v * (sv * depth * 0.5)
            verts.append(tuple(q))
    faces = [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    obj = mesh_obj(name, verts, faces, material, collection, False)
    bevel_box(obj, min(bevel, width * 0.22, depth * 0.22), bevel_segments)
    return obj


def tube_mesh(name, points, radii, sides, material, collection, smooth=True):
    points = [Vector(p) for p in points]
    if isinstance(radii, (int, float)):
        radii = [float(radii)] * len(points)
    verts, faces = [], []
    for i, point in enumerate(points):
        if i == 0:
            tangent = (points[1] - points[0]).normalized()
        elif i == len(points) - 1:
            tangent = (points[-1] - points[-2]).normalized()
        else:
            tangent = (points[i+1] - points[i-1]).normalized()
        ref = Vector((0, 1, 0))
        if abs(tangent.dot(ref)) > 0.92:
            ref = Vector((1, 0, 0))
        u = tangent.cross(ref).normalized()
        v = tangent.cross(u).normalized()
        for j in range(sides):
            ang = (2.0 * math.pi * j) / sides
            q = point + radii[i] * (math.cos(ang) * u + math.sin(ang) * v)
            verts.append(tuple(q))
    for i in range(len(points) - 1):
        for j in range(sides):
            a = i * sides + j
            b = i * sides + (j + 1) % sides
            faces.append((a, b, b+sides, a+sides))
    faces.append(tuple(reversed(tuple(range(sides)))) )
    faces.append(tuple((len(points)-1)*sides+j for j in range(sides)))
    return mesh_obj(name, verts, faces, material, collection, smooth)


class Assembly:
    def __init__(self, collection, materials, typ, state, lod, sides, bevel_segments, detail):
        self.collection = collection
        self.materials = materials
        self.typ = typ
        self.state = state
        self.lod = lod
        self.sides = sides
        self.bevel_segments = bevel_segments
        self.detail = detail
        self.objects = defaultdict(list)

    def add_box(self, center, dims, mat="Paint", bevel=0.002):
        o = box_mesh(f"Part_{mat}", center, dims, self.materials[mat], self.collection,
                     bevel if self.bevel_segments else 0, self.bevel_segments)
        self.objects[("frame", mat)].append(o)
        return o

    def add_component_box(self, component, center, dims, mat="Paint", bevel=0.002):
        o = box_mesh(f"Part_{mat}", center, dims, self.materials[mat], self.collection,
                     bevel if self.bevel_segments else 0, self.bevel_segments)
        self.objects[(component, mat)].append(o)
        return o

    def add_beam(self, a, b, width, depth, mat="Paint", bevel=0.0015):
        o = beam_mesh(f"Part_{mat}", a, b, width, depth, self.materials[mat], self.collection,
                      bevel if self.bevel_segments else 0, self.bevel_segments)
        self.objects[("frame", mat)].append(o)
        return o

    def add_component_beam(self, component, a, b, width, depth, mat="Paint", bevel=0.0015):
        o = beam_mesh(f"Part_{mat}", a, b, width, depth, self.materials[mat], self.collection,
                      bevel if self.bevel_segments else 0, self.bevel_segments)
        self.objects[(component, mat)].append(o)
        return o

    def add_rod(self, points, radius, mat="Paint", component="frame", sides=None, radii=None):
        o = tube_mesh(f"Part_{mat}", points, radii if radii is not None else radius,
                      sides or self.sides, self.materials[mat], self.collection, True)
        self.objects[(component, mat)].append(o)
        return o

    def weld(self, x, z, scale=1.0, component="frame", y=None):
        if self.lod >= 2:
            return
        r = 0.0024 * scale
        if y is None: y = {1:-.010,2:-.0085,3:-.014,4:-.016,5:-.024}[self.typ]
        for k in range(3 if self.lod == 0 else 2):
            dz = (k - 1) * 0.003 * scale
            pts = [(x - .010*scale, y, z + dz), (x - .004*scale, y - .001, z + dz + .001*scale),
                   (x + .003*scale, y - .001, z + dz - .0005*scale), (x + .010*scale, y, z + dz)]
            self.add_rod(pts, r, "Rust", component, max(6, self.sides // 2))

    def bolt(self, x, z, component="frame", size=0.010, front=-.037):
        if self.lod >= 2:
            return
        y0 = front-.004
        rod = tube_mesh("Bolt", [(x, y0 + .004, z), (x, y0 - .002, z)], size, 6,
                        self.materials["Rust"], self.collection, False)
        self.objects[(component, "Rust")].append(rod)
        slot = box_mesh("BoltSlot", (x, y0 - .003, z), (size*.85, .0015, .0018),
                        self.materials["Bare"], self.collection, 0.0002, 1 if self.lod == 0 else 0)
        self.objects[(component, "Bare")].append(slot)

    def chip(self, x, z, w, h, component="frame", y=-0.030):
        # Thin, irregular exposed-steel flakes are concentrated at edges, welds and low points.
        jitter = random.random() * .003
        verts = [
            (x-w*.50, y, z-h*.45), (x-w*.20, y-jitter, z-h*.50),
            (x+w*.50, y, z-h*.22), (x+w*.34, y-jitter, z+h*.43),
            (x-w*.37, y, z+h*.50),
        ]
        o = mesh_obj("PaintChip", verts, [(0,1,2,3,4)], self.materials["Bare"], self.collection, False)
        self.objects[(component, "Bare")].append(o)

    def rust_flake(self, x, z, w, h, component="frame", y=-0.032):
        jitter = random.random() * .002
        verts = [(x-w*.45,y,z-h*.40),(x-w*.10,y-jitter,z-h*.50),(x+w*.50,y,z-h*.12),
                 (x+w*.26,y-jitter,z+h*.46),(x-w*.35,y,z+h*.35)]
        o = mesh_obj("RustScale", verts, [(0,1,2,3,4)], self.materials["Rust"], self.collection, False)
        self.objects[(component, "Rust")].append(o)

    def edge_wear(self, count, seed, component="frame"):
        rng=random.Random(seed)
        profile={1:.04,2:.043,3:.053,4:.038,5:.044}[self.typ]
        depth={1:.052,2:.048,3:.055,4:.045,5:.05}[self.typ]
        if component in ("left","right"):
            lo,hi=(-.395,-.020) if component=="left" else (.020,.395)
            low,high=.07,.91; side_width=.032; y=-.0236
        else:
            lo,hi=-.46+profile*.5,.46-profile*.5
            low,high=.02+profile*.5,.96-profile*.5
            side_width=profile; y=-depth*.5-.0006
        for _ in range(count):
            side=rng.choices(("bottom","side","top"),(.5,.34,.16))[0]
            if side=="side":
                x=rng.choice((lo,hi)); z=rng.uniform(low+.04,high-.04)
                w=rng.uniform(.005,side_width*.55); h=rng.uniform(.008,.038)
            else:
                x=rng.uniform(lo+.03,hi-.03); z=low if side=="bottom" else high
                w=rng.uniform(.009,.038); h=rng.uniform(.005,min(side_width*.52,.019))
            if rng.random()<.64: self.rust_flake(x,z,w,h,component,y)
            else: self.chip(x,z,w*.7,h*.8,component,y-.0003)

    def build_nodes(self, lod_parent, hinge=False):
        nodes = {}
        for comp, mat in list(self.objects.keys()):
            items = self.objects[(comp, mat)]
            if not items:
                continue
            bpy.ops.object.select_all(action="DESELECT")
            for o in items:
                o.select_set(True)
            active = items[0]
            bpy.context.view_layer.objects.active = active
            if len(items) > 1:
                bpy.ops.object.join()
            joined = bpy.context.view_layer.objects.active
            joined.name = mat if comp == "frame" else f"{comp}_{mat}"
            # Smart unwrap the joined hard-surface mesh to a clean non-overlapping UV set.
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.uv.smart_project(angle_limit=math.radians(62), island_margin=0.003)
            bpy.ops.mesh.quads_convert_to_tris(quad_method="BEAUTY", ngon_method="BEAUTY")
            bpy.ops.object.mode_set(mode="OBJECT")
            joined.data.update()
            if mat == "Paint":
                for vert in joined.data.vertices:
                    vert.co.z -= 0.04
                if comp == "frame":
                    joined.parent = lod_parent
                else:
                    pivot_name = "LeftLeafPivot" if comp == "left" else "RightLeafPivot"
                    pivot = nodes.get(pivot_name)
                    if pivot is None:
                        x_pivot = -0.46 if comp == "left" else 0.46
                        pivot = empty_obj(pivot_name, self.collection, lod_parent, (x_pivot, -.035, 0))
                        nodes[pivot_name] = pivot
                    # Bake the leaf's hinge-relative origin into the vertices before parenting.
                    x_pivot = -0.46 if comp == "left" else 0.46
                    for vert in joined.data.vertices:
                        vert.co.x -= x_pivot
                        vert.co.y += .035
                    joined.parent = pivot
                nodes[(comp, mat)] = joined
            else:
                for vert in joined.data.vertices:
                    vert.co.z -= 0.04
                # Create a pivot lazily for damaged/rust/bare geometry too.
                if comp == "frame":
                    joined.parent = lod_parent
                else:
                    pivot_name = "LeftLeafPivot" if comp == "left" else "RightLeafPivot"
                    pivot = nodes.get(pivot_name)
                    if pivot is None:
                        x_pivot = -0.46 if comp == "left" else 0.46
                        pivot = empty_obj(pivot_name, self.collection, lod_parent, (x_pivot, -.035, 0))
                        nodes[pivot_name] = pivot
                    x_pivot = -0.46 if comp == "left" else 0.46
                    for vert in joined.data.vertices:
                        vert.co.x -= x_pivot
                        vert.co.y += .035
                    joined.parent = pivot
                nodes[(comp, mat)] = joined
            joined.select_set(False)
        return nodes


def rect_frame(a, half_w=0.46, bottom=0.02, top=0.96, profile=0.04, depth=0.045, angle=False):
    bevel = 0.003 if a.lod == 0 else (0.001 if a.lod == 1 else 0)
    if angle:
        # Front flange + return leg reads as rolled steel angle rather than a solid slab.
        fx = half_w - profile * .5
        for x, sign in ((-fx, 1), (fx, -1)):
            a.add_box((x, -.022, (bottom+top)*.5), (profile, .008, top-bottom), "Paint", bevel)
            a.add_box((x + sign*profile*.42, 0, (bottom+top)*.5), (.008, .042, top-bottom), "Paint", bevel)
        a.add_box((0, -.022, bottom+profile*.5), (2*half_w, .008, profile), "Paint", bevel)
        a.add_box((0, 0, bottom+profile*.42), (2*half_w, .042, .008), "Paint", bevel)
        a.add_box((0, -.022, top-profile*.5), (2*half_w, .008, profile), "Paint", bevel)
        a.add_box((0, 0, top-profile*.42), (2*half_w, .042, .008), "Paint", bevel)
    else:
        t = profile
        for x in (-half_w+t*.5, half_w-t*.5):
            a.add_box((x, 0, (bottom+top)*.5), (t, depth, top-bottom), "Paint", bevel)
        for z in (bottom+t*.5, top-t*.5):
            a.add_box((0, 0, z), (2*half_w, depth, t), "Paint", bevel)


def add_mounts(a, half_w=.46, z_values=(.15,.83), plate_w=.065, plate_h=.09, bolt_size=.008):
    foot_width=.065 if a.typ==3 else .045
    foot_height=.09 if a.typ==3 else .075
    for sign in (-1,1):
        x=sign*.512
        for z in z_values:
            a.add_box((x, .0625, z), (foot_width, .009, foot_height), "Paint", .002 if a.lod < 2 else 0)
            a.add_beam((sign*(half_w-.014),0,z),(x,.058,z),.022,.009,"Paint",.001)
            a.bolt(sign*.517,z-foot_height*.25,size=bolt_size,front=.058)
            a.bolt(sign*.517,z+foot_height*.25,size=bolt_size,front=.058)
            if a.lod < 2:
                a.rust_flake(x, z-foot_height*.35, foot_width*.42, .012, y=.0574)


def sine_bar(x, z0, z1, amp, phase=0, samples=13, y=0, power=1):
    pts=[]
    for i in range(samples):
        t=i/(samples-1)
        dx=amp*(math.sin(math.pi*t)**power)*math.sin(2*math.pi*t+phase)
        pts.append((x+dx, y, z0+(z1-z0)*t))
    return pts


def make_classic(a):
    rect_frame(a, .46, .02, .96, .04, .045, True)
    if a.lod < 2:
        add_mounts(a, .46, (.16,.82), .065, .085)
    xs=[-.36,-.24,-.12,0,.12,.24,.36]
    for i,x in enumerate(xs):
        amp=0.003 if a.state=="A" and i==4 else (0.012 if a.state=="B" and i in (1,5) else (0.027 if a.state=="C" and i in (1,5) else 0.0015))
        if a.state=="C" and i==3:
            a.add_rod(sine_bar(x,.035,.455,.009,0,9),.007,"Paint")
            a.add_rod(sine_bar(x+.004,.535,.945,.010,0,9),.007,"Paint")
            if a.lod<2:
                a.chip(x,.455,.010,.012,y=-.0078)
                a.chip(x+.004,.535,.010,.012,y=-.0078)
        else:
            a.add_rod(sine_bar(x,.035,.945,amp,0,13 if a.lod==0 else 7),.0075,"Paint")
    for z in (.34,.65):
        a.add_rod([(-.44,0,z),(.44,0,z)],.009,"Paint")
        if a.lod<2:
            for x in xs:
                a.weld(x,z,1.0 if a.lod==0 else .82)
    if a.lod<2:
        count={"A":18,"B":40,"C":55}[a.state] if a.lod==0 else {"A":9,"B":18,"C":22}[a.state]
        a.edge_wear(count,110+a.typ*19+ord(a.state)+a.lod)
    if a.state=="B" and a.lod<2:
        a.add_box((.18,-.0105,.69),(.16,.009,.035),"Rust",.001)
    if a.state=="C":
        a.add_box((.18,-.0105,.69),(.16,.012,.045),"Paint",.002)
        a.bolt(.16,.69,size=.009,front=-.0165); a.bolt(.22,.69,size=.009,front=-.0165)


def spiral_points(cx, cz, radius, turns, phase, y, mirror=1, cut=1.0):
    count=max(8,int(48*cut))
    pts=[]
    for i in range(count):
        t=i/(47.0)
        r=.004+radius*t
        ang=phase+turns*2*math.pi*t
        pts.append((cx+mirror*r*math.cos(ang),y,cz+r*math.sin(ang)))
    return pts


def make_forged(a):
    rect_frame(a,.46,.02,.96,.043,.048,False)
    if a.lod<2: add_mounts(a,.46,(.16,.82),.065,.09,.009)
    xs=[-.34,-.17,0,.17,.34]
    for i,x in enumerate(xs):
        top=.935
        if a.state=="C" and i==1: top=.49
        offset=.012 if a.state=="B" and i==3 else (.004 if i==2 else 0)
        pts=[(x,0,.042),(x+offset,0,min(.44,top-.03)),(x,0,top)]
        a.add_rod(pts,.0065,"Paint")
        if a.lod==0 and top>.8:
            # Hand-forged spear caps with tapered ends.
            a.add_rod([(x,0,.87),(x,0,.91),(x,0,.94)], [.008,.007,.001],"Bare",sides=10)
    for z in (.31,.66):
        a.add_rod([(-.44,0,z),(.44,0,z)],.0075,"Paint")
        if a.lod<2:
            for x in xs: a.weld(x,z,.8)
    # Four mirrored wrought scrolls around the center and lower rail.
    cut=1.0 if a.state=="A" else (.83 if a.state=="B" else .55)
    if a.lod<3:
        for center_z,phase in ((.48,0),(.48,math.pi),(.20,math.pi/2),(.76,-math.pi/2)):
            for mirror in (-1,1):
                pts=spiral_points(0,center_z,.115,1.15,phase,-.010,mirror,cut if (a.state=="C" and mirror<0) else 1.0)
                a.add_rod(pts,.0065 if a.lod==0 else .0055,"Paint")
    if a.lod<2:
        for i,x in enumerate(xs):
            if not(a.state=="C" and i==1): a.bolt(x,.91 if x else .92,size=.007,front=-.008)
        count={"A":12,"B":28,"C":43}[a.state] if a.lod==0 else {"A":6,"B":13,"C":17}[a.state]
        a.edge_wear(count,140+a.typ*29+ord(a.state)+a.lod)
    if a.state=="C":
        a.add_rod([(-.25,-.009,.12),(-.17,-.010,.18),(-.12,-.010,.24)],.008,"Bare")
        a.add_box((.38,-.0105,.42),(.09,.012,.095),"Rust",.002)


def make_reinforced(a):
    rect_frame(a,.46,.02,.96,.053,.055,False)
    if a.lod<2:
        add_mounts(a,.46,(.12,.86),.09,.11,.011)
        for x in (-.405,.405):
            a.add_box((x,-.034,.49),(.075,.015,.045),"Paint",.002)
            a.bolt(x,.49,size=.009,front=-.0415)
    xs=[-.36,-.26,-.16,-.06,.06,.16,.26,.36]
    zs=[.16,.32,.48,.64,.80]
    for i,x in enumerate(xs):
        if a.state=="C" and i==3:
            a.add_rod([(x,0,.045),(x,0,.40)],.012,"Paint")
            a.add_rod([(x+.008,0,.58),(x+.008,0,.935)],.012,"Bare")
        else:
            bend=.01 if a.state=="B" and i in (1,6) else (.018 if a.state=="C" and i in (1,6) else 0)
            a.add_rod(sine_bar(x,.045,.935,bend,samples=11),.012,"Paint")
    for i,z in enumerate(zs):
        if a.state=="C" and i==2:
            a.add_rod([(-.44,0,z),(-.08,0,z)],.012,"Paint")
            a.add_rod([(.12,0,z),(.44,0,z)],.012,"Paint")
        else:
            a.add_rod([(-.44,0,z),(.44,0,z)],.012,"Paint")
    if a.lod<2:
        for x in xs:
            for z in zs:
                if a.state=="C" and abs(z-.48)<.001 and -.08<x<.12: continue
                a.weld(x,z,.72 if a.typ==3 else .8)
        for side in (-1,1):
            for z in (.22,.76):
                a.add_beam((side*.40,-.031,z-.075),(side*.40,-.031,z+.075),.024,.009,"Bare",.001)
                a.bolt(side*.40,z,size=.009,front=-.0355)
        count={"A":16,"B":36,"C":48}[a.state] if a.lod==0 else {"A":8,"B":16,"C":21}[a.state]
        a.edge_wear(count,210+a.typ*11+ord(a.state)+a.lod)
    if a.state=="B":
        a.add_box((-.19,-.018,.30),(.17,.013,.045),"Rust",.002)
    elif a.state=="C":
        a.add_box((-.18,-.018,.30),(.18,.014,.05),"Paint",.002)
        a.bolt(-.24,.30,size=.009,front=-.025); a.bolt(-.12,.30,size=.009,front=-.025)


def make_survivor(a):
    # Salvaged, slightly mismatched stock around a still-square welded perimeter.
    rect_frame(a,.46,.02,.96,.038,.045,False)
    if a.lod<2:
        add_mounts(a,.46,(.14,.83),.080,.09,.009)
    xs=[-.37,-.265,-.15,-.035,.095,.22,.355]
    for i,x in enumerate(xs):
        rad=[.007,.010,.006,.009,.0075,.011,.0065][i]
        z0=.043+(i%3)*.003; z1=.935-(i%2)*.003
        amp=(.02 if a.state in ("B","C") and i in (2,5) else (.006 if i==3 else .001))
        if a.state=="C" and i==4:
            a.add_rod(sine_bar(x,z0,.37,.016,0,8),rad,"Bare")
            a.add_rod(sine_bar(x+.012,.57,z1,.025,0,8),rad*.9,"Paint")
        else:
            a.add_rod(sine_bar(x,z0,z1,amp,phase=i*.31,samples=13 if a.lod==0 else 8),rad,"Paint")
    crossbars=[(.27,.015),(.53,.020),(.75,.013)]
    for i,(z,rad) in enumerate(crossbars):
        if a.state=="C" and i==1:
            a.add_rod([(-.44,0,z),(-.12,0,z+.015)],rad,"Paint")
            a.add_rod([(.15,0,z-.01),(.44,0,z)],rad,"Rust")
        else:
            a.add_rod([(-.44,0,z),(.44,0,z)],rad,"Paint")
    # Uneven repair straps and overlapping patch plates make the assembly look salvaged.
    a.add_beam((-.39,-.014,.12),(.37,-.014,.82),.018,.010,"Bare",.001)
    a.add_beam((-.35,-.013,.78),(.32,-.013,.20),.015,.009,"Paint",.001)
    if a.lod<2:
        plates=[(-.30,.25,.12,.065),(.24,.64,.11,.08),(-.10,.48,.15,.05)]
        if a.state=="C": plates.append((.29,.39,.14,.11))
        for x,z,w,h in plates:
            a.add_box((x,-.015,z),(w,.012,h),"Rust",.002)
            a.bolt(x-w*.28,z,size=.008,front=-.021); a.bolt(x+w*.28,z,size=.008,front=-.021)
        for x in xs:
            for z,rad in crossbars:
                if a.state=="C" and abs(z-.53)<.001 and -.12<x<.15: continue
                if abs(x)<.39: a.weld(x,z,.72,y=-rad-.001)
        count={"A":15,"B":37,"C":52}[a.state] if a.lod==0 else {"A":7,"B":17,"C":23}[a.state]
        a.edge_wear(count,310+a.typ*17+ord(a.state)+a.lod)
    # Visible rebar ribs on two salvaged rods at the highest LOD only.
    if a.lod==0:
        for index,x in ((1,-.265),(5,.22)):
            pts=[]; turns=11; samples=88; radius=.0118; core=.0012
            z0=.043+(index%3)*.003;z1=.935-(index%2)*.003
            amp=.02 if a.state in ("B","C") and index==5 else .001
            for i in range(samples):
                t=i/(samples-1); ang=t*turns*2*math.pi
                z=.12+t*.70;u=(z-z0)/(z1-z0)
                bend=amp*math.sin(math.pi*u)*math.sin(2*math.pi*u+index*.31)
                pts.append((x+bend+radius*math.cos(ang), -radius*math.sin(ang), z))
            a.add_rod(pts,core,"Bare",sides=5)
    if a.state=="B":
        a.add_box((-.36,-.024,.56),(.08,.016,.035),"Paint",.002)
    if a.state=="C":
        a.add_box((-.36,-.024,.56),(.09,.017,.04),"Rust",.002)


def add_hinged_leaf(a, component, lo, hi, hinge_x, side_index):
    z0,z1=.07,.91
    # Moving leaf edge, top and lower rails.
    a.add_component_box(component,((lo+hi)*.5,0,z0),(hi-lo,.046,.035),"Paint",.002)
    a.add_component_box(component,((lo+hi)*.5,0,.49),(hi-lo,.032,.024),"Paint",.002)
    a.add_component_box(component,(lo,-.002,(z0+z1)*.5),(.032,.046,z1-z0),"Paint",.002)
    a.add_component_box(component,(hi,-.002,(z0+z1)*.5),(.032,.046,z1-z0),"Paint",.002)
    a.add_component_box(component,((lo+hi)*.5,0,z1),(hi-lo,.046,.035),"Paint",.002)
    # Four slim vertical bars per leaf plus a light diagonal stiffener.
    count=4
    for i in range(count):
        t=(i+1)/(count+1)
        x=lo+(hi-lo)*t
        if a.state=="C" and side_index==0 and i==2:
            a.add_rod([(x,0,.07),(x,0,.41)],.0065,"Paint",component)
            a.add_rod([(x+.008,0,.59),(x+.008,0,.91)],.0065,"Bare",component)
        else:
            bend=.012 if a.state=="B" and i==1 else (.020 if a.state=="C" and i==1 else .001)
            a.add_rod(sine_bar(x,.07,.91,bend,samples=11),.0065,"Paint",component)
    if a.lod<2:
        # Diagonal straps and small weld beads rotate together with the leaf.
        a.add_component_beam(component,(lo+.04,-.012,.16),(hi-.04,-.012,.80),.012,.008,"Bare",.001)
        for i in range(4):
            x=lo+(hi-lo)*(i+1)/5
            for z in (.082,.49,.898): a.weld(x,z,.7,component,y=-.017 if z==.49 else -.024)
        for z in (.14,.49,.84):
            a.add_component_box(component,((hinge_x+(lo if component=="left" else hi))*.5,-.027,z),
                                (abs(hinge_x-(lo if component=="left" else hi)),.008,.026),"Paint",.001)
    if component=="right":
        # Central latch plate and horizontal bolt live on the right leaf.
        a.add_component_box(component,(lo+.008,-.028,.50),(.045,.012,.12),"Paint",.002)
        a.add_component_beam(component,(lo-.04,-.0385,.50),(lo+.10,-.0385,.50),.009,.009,"Bare",.0008)
        if a.lod<2:
            a.bolt(lo+.008,.46,component,.008,front=-.034); a.bolt(lo+.008,.54,component,.008,front=-.034)
    else:
        a.add_component_box(component,(hi+.006,-.028,.50),(.034,.012,.075),"Rust",.001)


def add_hinges(a, side, x):
    comp="frame"
    for z in (.14,.49,.84):
        # Barrel axis is vertical, with an exposed pin and two welded tabs.
        pts=[(x,-.035,z-.045),(x,-.035,z+.045)]
        a.add_rod(pts,.010,"Rust",comp,sides=10 if a.lod<2 else 6)
        a.add_box((x+side*.018,-.025,z),(.032,.012,.035),"Paint",.002)
        a.weld(x+side*.02,z,.9,y=-.032)
        if a.lod==0:
            a.add_rod([(x,-.035,z-.055),(x,-.035,z-.045)],.004,"Bare",comp,sides=8)
            a.add_rod([(x,-.035,z+.045),(x,-.035,z+.055)],.004,"Bare",comp,sides=8)


def make_hinged(a):
    rect_frame(a,.46,.02,.96,.044,.050,False)
    if a.lod<2: add_mounts(a,.46,(.13,.84),.075,.085,.009)
    add_hinges(a,-1,-.46)
    add_hinges(a,1,.46)
    add_hinged_leaf(a,"left",-.395,-.020,-.46,0)
    add_hinged_leaf(a,"right",.020,.395,.46,1)
    if a.lod<2:
        count={"A":8,"B":20,"C":29}[a.state] if a.lod==0 else {"A":4,"B":9,"C":12}[a.state]
        for comp in ("left","right"):
            a.edge_wear(count,410+a.typ*5+ord(a.state)+a.lod,comp)


BUILDERS = {1: make_classic, 2: make_forged, 3: make_reinforced, 4: make_survivor, 5: make_hinged}


def apply_leaf_pivots(root, lod, typ, collection):
    # Pivots are made by Assembly.build_nodes when the leaf geometry is joined.
    return


def collider_segment(component, a, b, width, depth):
    # Match Blender FBX axis conversion and Unity's handedness (X is mirrored).
    component={"left":"right","right":"left"}.get(component,component)
    return {"component": component, "start": [round(-a[0],5),round(a[1],5),round(a[2],5)],
            "end": [round(-b[0],5),round(b[1],5),round(b[2],5)],
            "width": round(width,5), "depth": round(depth,5)}


def colliders_for(typ, state):
    s=[]
    def add(comp,a,b,w,d=None): s.append(collider_segment(comp,a,b,w,d or w))
    def curve(comp,pts,w):
        for a,b in zip(pts,pts[1:]): add(comp,(a[0],a[2],-a[1]),(b[0],b[2],-b[1]),w,w)
    # The centerline plane is z=0; matching pivots are translated in Unity units.
    add("frame",(-.44,.04,0),(-.44,.94,0),.04,.045)
    add("frame",(.44,.04,0),(.44,.94,0),.04,.045)
    add("frame",(-.44,.04,0),(.44,.04,0),.04,.045)
    add("frame",(-.44,.94,0),(.44,.94,0),.04,.045)
    if typ==1:
        xs=[-.36,-.24,-.12,0,.12,.24,.36]
        for i,x in enumerate(xs):
            if state=="C" and i==3:
                curve("frame",sine_bar(x,.035,.455,.009,0,7),.014)
                curve("frame",sine_bar(x+.004,.535,.945,.010,0,7),.014)
            else:
                amp=.012 if state=="B" and i in (1,5) else (.027 if state=="C" and i in (1,5) else 0)
                if amp: curve("frame",sine_bar(x,.035,.945,amp,0,9),.015)
                else: add("frame",(x,.035,0),(x,.945,0),.015,.015)
        for y in (.34,.65): add("frame",(-.44,y,0),(.44,y,0),.018,.018)
    elif typ==2:
        xs=[-.34,-.17,0,.17,.34]
        for i,x in enumerate(xs):
            end=.49 if state=="C" and i==1 else .935
            add("frame",(x,.042,0),(x,end,0),.013,.013)
        for y in (.31,.66): add("frame",(-.44,y,0),(.44,y,0),.015,.015)
        for sign in (-1,1):
            add("frame",(sign*.015,.42,0),(sign*.11,.53,0),.014,.014)
            add("frame",(sign*.11,.53,0),(sign*.10,.45,0),.014,.014)
    elif typ==3:
        xs=[-.36,-.26,-.16,-.06,.06,.16,.26,.36]
        zs=[.16,.32,.48,.64,.80]
        for i,x in enumerate(xs):
            if state=="C" and i==3:
                add("frame",(x,.045,0),(x,.40,0),.024,.024); add("frame",(x+.008,.58,0),(x+.008,.935,0),.024,.024)
            else:
                amp=.01 if state=="B" and i in (1,6) else (.018 if state=="C" and i in (1,6) else 0)
                if amp: curve("frame",sine_bar(x,.045,.935,amp,samples=9),.024)
                else: add("frame",(x,.045,0),(x,.935,0),.024,.024)
        for i,y in enumerate(zs):
            if state=="C" and i==2:
                add("frame",(-.44,y,0),(-.08,y,0),.024,.024); add("frame",(.12,y,0),(.44,y,0),.024,.024)
            else: add("frame",(-.44,y,0),(.44,y,0),.024,.024)
    elif typ==4:
        xs=[-.37,-.265,-.15,-.035,.095,.22,.355]
        for i,x in enumerate(xs):
            rad=[.007,.010,.006,.009,.0075,.011,.0065][i]
            z0=.043+(i%3)*.003; z1=.935-(i%2)*.003
            amp=.02 if state in ("B","C") and i in (2,5) else (.006 if i==3 else .001)
            if state=="C" and i==4:
                curve("frame",sine_bar(x,z0,.37,.016,0,8),rad*2)
                curve("frame",sine_bar(x+.012,.57,z1,.025,0,8),rad*1.8)
            else: curve("frame",sine_bar(x,z0,z1,amp,phase=i*.31,samples=10),rad*2)
        for i,y in enumerate((.27,.53,.75)):
            if state=="C" and i==1:
                add("frame",(-.44,y,0),(-.12,y+.015,0),.020,.020); add("frame",(.15,y-.01,0),(.44,y,0),.020,.020)
            else: add("frame",(-.44,y,0),(.44,y,0),.020,.020)
        add("frame",(-.39,.12,0),(.37,.82,0),.018,.012)
        add("frame",(-.35,.78,0),(.32,.20,0),.015,.010)
    else:
        for comp,lo,hi in (("left",-.395,-.020),("right",.020,.395)):
            for i in range(4):
                x=lo+(hi-lo)*(i+1)/5
                if state=="C" and comp=="left" and i==2:
                    add(comp,(x,.07,0),(x,.41,0),.013,.013); add(comp,(x+.008,.59,0),(x+.008,.91,0),.013,.013)
                elif i==1 and state in ("B","C"):
                    curve(comp,sine_bar(x,.07,.91,.012 if state=="B" else .020,samples=9),.013)
                else: add(comp,(x,.07,0),(x,.91,0),.013,.013)
            add(comp,(lo,.07,0),(hi,.07,0),.035,.046); add(comp,(lo,.91,0),(hi,.91,0),.035,.046)
            add(comp,(lo,.07,0),(lo,.91,0),.032,.046); add(comp,(hi,.07,0),(hi,.91,0),.032,.046)
            add(comp,(lo,.49,0),(hi,.49,0),.024,.032)
    for segment in s:
        segment["start"][1]=round(segment["start"][1]-.04,5)
        segment["end"][1]=round(segment["end"][1]-.04,5)
    return s


def make_variant(typ, state, paint_mat, rust_mat, bare_mat, collider_out):
    short, _ = TYPE_INFO[typ]
    asset_name=f"SM_Grille_{typ:02d}_{short}_{state}"
    coll=bpy.data.collections.new(asset_name)
    bpy.context.scene.collection.children.link(coll)
    root=empty_obj(asset_name,coll)
    root["WindowGrilleType"]=typ
    root["WearState"]=state
    root["PivotContract"]="Root at center of lower frame; Unity Y up, front +Z"
    mats={"Paint":paint_mat,"Rust":rust_mat,"Bare":bare_mat}
    lod_roots=[]
    for lod,(sides,bevseg,detail) in enumerate(LOD_DATA):
        lod_root=empty_obj(f"LOD{lod}",coll,root)
        lod_roots.append(lod_root)
        a=Assembly(coll,mats,typ,state,lod,sides,bevseg,detail)
        random.seed(typ*1000+ord(state)*17+lod)
        BUILDERS[typ](a)
        a.build_nodes(lod_root,typ==5)
    collider_out[asset_name]=colliders_for(typ,state)
    # Ensure the hierarchy itself is selected for the genuine FBX export.
    current=list(coll.objects)
    current_ids={o.as_pointer() for o in current}
    original_names={o:o.name for o in bpy.data.objects}
    for index,obj in enumerate(o for o in bpy.data.objects if o.as_pointer() not in current_ids):
        obj.name=f"__Archive_{index:04d}_{original_names[obj]}"
    root.name=asset_name
    for i,lod_root in enumerate(lod_roots): lod_root.name=f"LOD{i}"
    for obj in current:
        if obj.type=="EMPTY" and obj is not root and obj not in lod_roots:
            index=next(i for i,p in enumerate(lod_roots) if obj.parent==p)
            if "LeftLeafPivot" in original_names[obj]: obj.name=f"LeftLeafPivot_LOD{index}"
            elif "RightLeafPivot" in original_names[obj]: obj.name=f"RightLeafPivot_LOD{index}"
    for obj in current:
        if obj.type!="MESH": continue
        mat_name=obj.data.materials[0].name.lower() if obj.data.materials else "paint"
        category="Rust" if "rust" in mat_name else ("Bare" if "bare" in mat_name else "Paint")
        parent_name=obj.parent.name if obj.parent else ""
        if parent_name=="LOD"+str(next((i for i,x in enumerate(lod_roots) if obj.parent==x),-1)):
            obj.name=("Frame_" if typ==5 else "")+category
        elif parent_name.startswith("LeftLeafPivot"): obj.name="Left_"+category
        elif parent_name.startswith("RightLeafPivot"): obj.name="Right_"+category
        else: obj.name=category
    bpy.ops.object.select_all(action="DESELECT")
    export_objects=current
    for o in export_objects: o.select_set(True)
    bpy.context.view_layer.objects.active=root
    path=MODEL_DIR/f"{asset_name}.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={"MESH","EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
        axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True,
        mesh_smooth_type="FACE", use_tspace=True, add_leaf_bones=False,
        path_mode="AUTO", bake_anim=False, use_custom_props=True)
    for obj in current: obj.name=f"__RestoreCurrent_{obj.as_pointer()}"
    for obj,name in original_names.items():
        if obj.as_pointer() not in current_ids: obj.name=name
    for obj,name in original_names.items():
        if obj.as_pointer() in current_ids: obj.name=name
    return coll


def configure_render(materials):
    sc=bpy.context.scene
    sc.render.engine="CYCLES"
    sc.cycles.samples=24
    sc.render.resolution_x=1400
    sc.render.resolution_y=1200
    sc.render.resolution_percentage=100
    sc.render.image_settings.file_format="PNG"
    sc.world.color=(0.12,0.12,0.12)
    sc.view_settings.view_transform="AgX"
    # Simple neutral studio rig. Preview is evidence of form, not a final game lighting test.
    def area(name,loc,power,size,color,target):
        data=bpy.data.lights.new(name,"AREA"); data.energy=power; data.shape="DISK"; data.size=size; data.color=color
        obj=bpy.data.objects.new(name,data); sc.collection.objects.link(obj); obj.location=loc
        obj.rotation_euler=(Vector(target)-obj.location).to_track_quat("-Z","Y").to_euler()
    area("Key softbox",(0,-3.2,2.2),580,3.0,(1.0,.88,.72),(0,0,.48))
    area("Cool rim",(2.4,1.7,1.4),720,2.2,(.60,.72,1.0),(0,0,.5))
    area("Fill",(-2,-1,.7),300,1.8,(1.0,.97,.9),(0,0,.45))


def configure_preview_camera(target=(0,0,.48), distance=2.5):
    sc=bpy.context.scene
    data=bpy.data.cameras.new("PreviewCamera")
    cam=bpy.data.objects.new("PreviewCamera",data); sc.collection.objects.link(cam)
    cam.location=(distance*.10,-distance,distance*.23)
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat("-Z","Y").to_euler()
    data.type="ORTHO"; data.ortho_scale=1.35
    sc.camera=cam


def main():
    MODEL_DIR.mkdir(parents=True,exist_ok=True)
    SOURCE_DIR.mkdir(parents=True,exist_ok=True)
    PREVIEW_DIR.mkdir(parents=True,exist_ok=True)
    reset_scene()
    colliders={}
    base_mats={}
    for typ,(short,_) in TYPE_INFO.items():
        base_mats[typ]=create_material(f"M_Placeholder_{short}",PAINT_COLOR[typ],.68,.08,f"{typ:02d}_{short}")
    rust_mat=create_material("M_Placeholder_Rust",(.36,.16,.075,1),.9,.18,"Rust_Common")
    bare_mat=create_material("M_Placeholder_BareSteel",(.39,.40,.38,1),.48,.82,"BareSteel_Common")
    for typ in range(1,6):
        short=TYPE_INFO[typ][0]
        for state in STATES:
            paint=base_mats[typ] if state=="A" else create_material(f"M_Placeholder_{short}_{state}",PAINT_COLOR[typ],.78,.08,
                f"{typ:02d}_{short}_{state}",f"{typ:02d}_{short}")
            make_variant(typ,state,paint,rust_mat,bare_mat,colliders)
    collider_file=ASSET_DIR/"Documentation"/"collider_specs.json"
    collider_file.parent.mkdir(parents=True,exist_ok=True)
    collider_file.write_text(json.dumps({"items":[{"id":k,"segments":v} for k,v in colliders.items()]},indent=2),encoding="utf-8")
    configure_render(base_mats)
    configure_preview_camera()
    # Save one editable master with a neutral studio camera and all 15 asset collections.
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR/"MetalGrilles_Master.blend"))
    # Render a compact 5 x 3 contact sheet source for later visual QA in the Unity demo.
    # Unity preview screenshots are generated after the actual URP import.
    print("BLENDER_BUILD_OK assets=15 lods=60 colliders="+str(sum(len(v) for v in colliders.values())))


if __name__ == "__main__":
    main()
