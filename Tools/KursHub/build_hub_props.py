"""Blender 4.5+: reusable, metre-sized Kurs exterior and interior modules.

Run with Blender --background --python this_file -- --root <staging-project>.
Authoring collections stay separate in KursHubProps.blend; each FBX is local to
its own useful placement origin. No prop becomes part of the mall shell mesh.
"""
import argparse
import json
import math
import pathlib
import sys
import runpy
from collections import defaultdict

import bmesh
import bpy
from mathutils import Vector


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--root', type=pathlib.Path,
                    default=pathlib.Path(__file__).resolve().parents[2])
ROOT = parser.parse_args(sys.argv[sys.argv.index('--') + 1:]
                         if '--' in sys.argv else []).root.resolve()
OUT = ROOT / 'Assets/Environment/KursHub/Props'
SOURCE = ROOT / 'Sources/KursHub'
REPORT = ROOT / 'work/KursHub'
for directory in (OUT / 'Models', SOURCE, REPORT):
    directory.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
PALETTE = {
    'WornWood': (.31, .23, .15, 1),
    'TarpGreen': (.14, .20, .13, 1),
    'SandCanvas': (.52, .46, .31, 1),
    'SteelOxide': (.26, .15, .09, 1),
    'Frame': (.095, .12, .13, 1),
    'LampGlass': (.96, .72, .34, 1),
    'DirtStain': (.14, .105, .065, .45),
    # Match the shell palette so Unity can share its existing finish assets.
    'Concrete': (.57, .55, .49, 1),
    'CreamPanel': (.64, .62, .53, 1),
    'MallBlue': (.055, .26, .46, 1),
    'Glass': (.22, .40, .43, .28),
    'Lettering': (.80, .79, .70, 1),
    'SignRed': (.35, .08, .065, 1),
}
PALETTE.update(runpy.run_path(str(pathlib.Path(__file__).with_name('stage6_palette.py')))['PALETTE6'])
SURFACES = {'SteelOxide': 'RustedSteel', 'DirtStain': 'DirtStreaks',
            'Concrete': 'CreamConcrete', 'CreamPanel': 'CreamConcrete',
            'MallBlue': 'BluePaint', 'Glass': 'GlassGrime'}
materials = {}
for name, color in PALETTE.items():
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    shader = nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = color
    shader.inputs['Roughness'].default_value = .82
    if name in ('SteelOxide', 'Frame'):
        shader.inputs['Metallic'].default_value = .45
    if name in ('LampGlass','Ember'):
        shader.inputs['Emission Color'].default_value = (1, .55, .16, 1)
        shader.inputs['Emission Strength'].default_value = 1.4
        shader.inputs['Roughness'].default_value = .24
    if name == 'Glass':
        shader.inputs['Alpha'].default_value = color[3]
        shader.inputs['Roughness'].default_value = .18
        material.surface_render_method = 'DITHERED'
    stem = SURFACES.get(name, name)
    texture_root = ROOT / 'Assets/Environment/KursHub/Textures'
    base = texture_root / (stem + '_BaseColor.png')
    if base.exists():
        image = nodes.new('ShaderNodeTexImage')
        image.image = bpy.data.images.load(str(base), check_existing=True)
        links.new(image.outputs['Color'], shader.inputs['Base Color'])
        if name == 'DirtStain':
            links.new(image.outputs['Alpha'], shader.inputs['Alpha'])
            material.surface_render_method = 'DITHERED'
        if name == 'Glass':
            alpha = nodes.new('ShaderNodeMath')
            alpha.operation = 'MULTIPLY'
            alpha.inputs[1].default_value = .5
            links.new(image.outputs['Alpha'], alpha.inputs[0])
            links.new(alpha.outputs[0], shader.inputs['Alpha'])
        normal = texture_root / (stem + '_Normal.png')
        if normal.exists():
            image_n = nodes.new('ShaderNodeTexImage')
            image_n.image = bpy.data.images.load(str(normal), check_existing=True)
            image_n.image.colorspace_settings.name = 'Non-Color'
            normal_node = nodes.new('ShaderNodeNormalMap')
            normal_node.inputs['Strength'].default_value = .55
            links.new(image_n.outputs['Color'], normal_node.inputs['Color'])
            links.new(normal_node.outputs['Normal'], shader.inputs['Normal'])
    materials[name] = material

prototypes = []
current = None
BOX_FACES = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
             (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]


def xyz(point):
    # Match the shell's tested FBX basis: source +Y becomes Unity +Z.
    return (point[0], point[2], point[1])


def begin(identifier, description):
    global current
    index = len(prototypes)
    collection = bpy.data.collections.new(identifier)
    scene.collection.children.link(collection)
    offset = ((index % 4) * 13.0, 0, (index // 4) * 10.0)
    collection.instance_offset = xyz(offset)
    current = dict(id=identifier, description=description, collection=collection,
                   offset=offset, objects=[], chunks=defaultdict(list))
    collection['UnityPlacementOrigin'] = description
    prototypes.append(current)


def mesh(name, vertices, faces, material, collider=False, fixed_uv=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata([xyz(vertex) for vertex in vertices], [], faces)
    data.update()
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if bm.calc_volume(signed=True) < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    current['collection'].objects.link(obj)
    obj.location = xyz(current['offset'])
    obj['unity_collision'] = collider
    obj['prop_id'] = current['id']
    data.materials.append(materials[material])
    uv = data.uv_layers.new(name='UVMap')
    for face in data.polygons:
        # All dirt faces use the same front projection; Blender Y is Unity Z.
        if fixed_uv:
            for loop in face.loop_indices:
                position = data.vertices[data.loops[loop].vertex_index].co
                uv.data[loop].uv = (position.x / fixed_uv[0] + .5,
                                    position.z / fixed_uv[1])
        else:
            axis = max(range(3), key=lambda i: abs(face.normal[i]))
            axes = [i for i in range(3) if i != axis]
            for loop in face.loop_indices:
                position = data.vertices[data.loops[loop].vertex_index].co
                uv.data[loop].uv = (position[axes[0]] / 2, position[axes[1]] / 2)
    current['objects'].append(obj)
    current['chunks'][(material, collider)].append(obj)
    return obj


def box(name, center, size, material='WornWood', collider=False, tilt=0):
    sx, sy, sz = [value / 2 for value in size]
    cx, cy, cz = center
    cosine, sine = math.cos(tilt), math.sin(tilt)
    corners = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, -sy, sz), (-sx, -sy, sz),
               (-sx, sy, -sz), (sx, sy, -sz), (sx, sy, sz), (-sx, sy, sz)]
    vertices = [(cx + x, cy + y * cosine - z * sine,
                 cz + y * sine + z * cosine) for x, y, z in corners]
    return mesh(name, vertices, BOX_FACES, material, collider)


def beam(name, start, end, width=.08, material='WornWood', collider=False,
         depth=None, front_normal=None):
    a, b = Vector(start), Vector(end)
    direction = (b - a).normalized()
    if front_normal is not None:
        normal = Vector(front_normal).normalized()
        side = direction.cross(normal).normalized()
        up = normal
    else:
        side = direction.cross(Vector((0, 1, 0)))
        if side.length < .1:
            side = Vector((1, 0, 0))
        side.normalize()
        up = direction.cross(side).normalized()
    depth = depth if depth is not None else width
    vertices = [tuple(point + side * x * width / 2 + up * y * depth / 2)
                for point in (a, b) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    return mesh(name, vertices, BOX_FACES, material, collider)


def sheet(name, width, height, material, nx=32, ny=10, thickness=.018,
          sag=.25, ripple=.05, corrugated=False):
    vertices = []
    for back in (False, True):
        for j in range(ny + 1):
            v = j / ny
            for i in range(nx + 1):
                u = i / nx
                x = (u - .5) * width
                top_sag = sag * math.sin(math.pi * u) ** 1.1
                bottom = .025 * (1 - math.cos(7 * math.pi * u))
                y = bottom + v * (height - top_sag - bottom)
                z = (ripple * math.sin(u * math.pi * (32 if corrugated else 6)) *
                     (1 if corrugated else (.45 + .55 * v)))
                if not corrugated:
                    z += .07 * math.sin(math.pi * u) * math.sin(v * math.pi * 2)
                z += thickness / 2 if back else -thickness / 2
                vertices.append((x, y, z))
    stride = nx + 1
    layer = stride * (ny + 1)
    faces = []
    for j in range(ny):
        for i in range(nx):
            a = j * stride + i
            faces.append((a, a + 1, a + 1 + stride, a + stride))
            faces.append(tuple(layer + k for k in (a + stride, a + 1 + stride, a + 1, a)))
    outline = (list(range(stride)) + [j * stride + nx for j in range(1, ny + 1)] +
               [ny * stride + i for i in range(nx - 1, -1, -1)] +
               [j * stride for j in range(ny - 1, 0, -1)])
    for i, a in enumerate(outline):
        b = outline[(i + 1) % len(outline)]
        faces.append((a, a + layer, b + layer, b))
    return mesh(name, vertices, faces, material)


def tube(name, points, radius=.019, material='Frame', sides=8):
    vertices = []
    for i, point in enumerate(points):
        direction = (Vector(points[min(i + 1, len(points) - 1)]) -
                     Vector(points[max(i - 1, 0)])).normalized()
        side = direction.cross(Vector((0, 0, 1))).normalized()
        up = direction.cross(side).normalized()
        for j in range(sides):
            angle = j * math.tau / sides
            vertices.append(tuple(Vector(point) + radius * (
                side * math.cos(angle) + up * math.sin(angle))))
    faces = [tuple(range(sides - 1, -1, -1)),
             tuple((len(points) - 1) * sides + j for j in range(sides))]
    for i in range(len(points) - 1):
        for j in range(sides):
            a, b = i * sides + j, i * sides + (j + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    obj = mesh(name, vertices, faces, material)
    for face in obj.data.polygons:
        face.use_smooth = len(face.vertices) == 4
    return obj


begin('KH_Sandbag', 'Ground centre; .85 m wide, .35 m high, .50 m deep')
vertices = [(0, 0, 0)]
longitude, latitude = 24, 10
for j in range(1, latitude):
    phi = -math.pi / 2 + math.pi * j / latitude
    sign = lambda value, power: math.copysign(abs(value) ** power, value)
    for i in range(longitude):
        angle = math.tau * i / longitude
        r = abs(math.cos(phi)) ** .48
        vertices.append((.425 * r * sign(math.cos(angle), .38),
                         .175 + .175 * sign(math.sin(phi), .68),
                         .25 * r * sign(math.sin(angle), .42)))
vertices.append((0, .35, 0))
faces = [(0, 1 + (i + 1) % longitude, 1 + i) for i in range(longitude)]
for j in range(latitude - 2):
    for i in range(longitude):
        a = 1 + j * longitude + i
        b = 1 + j * longitude + (i + 1) % longitude
        faces.append((a, b, b + longitude, a + longitude))
top = len(vertices) - 1
for i in range(longitude):
    a = 1 + (latitude - 2) * longitude + i
    b = 1 + (latitude - 2) * longitude + (i + 1) % longitude
    faces.append((a, b, top))
bag = mesh('SoftSack', vertices, faces, 'SandCanvas', True)
for face in bag.data.polygons:
    face.use_smooth = True
for end in (-1, 1):
    tube('SackStitchedEnd', [(end * .41, .145 + .05 * math.sin(i * math.pi / 8),
                              -.13 + .26 * i / 8) for i in range(9)],
         radius=.005, material='SandCanvas', sides=6)

begin('KH_Barricade', 'Ground centre; 2.4 x 1.2 m reclaimed plank barricade')
for x in (-.88, .88):
    for z in (-.24, .24):
        beam('SplayedLeg', (x, .04, z), (x, 1.12, -.02), .10, collider=True)
    box('Shoe', (x, .045, 0), (.19, .09, .60), collider=True)
for y in (.45, .80, 1.12):
    box('FrontPlank', (0, y, -.12), (2.40, .19, .075), collider=True)
beam('DiagonalBrace', (-1.1, .35, -.17), (1.1, 1.19, -.17), .15,
     depth=.045, front_normal=(0, 0, -1), collider=True)
box('MetalRepair', (-.35, .73, -.225), (.54, .64, .025), 'SteelOxide')
for x in (-1.04, .98):
    for y in (.45, .80, 1.12):
        box('Bolt', (x, y, -.165), (.028, .028, .017), 'SteelOxide')

begin('KH_WatchPost', 'Platform bottom centre; 4 x 4 m floor, rear +Z opening 1.2 m')
for i in range(16):
    box('PlatformPlank', (-1.875 + i * .25, .075, 0), (.242, .15, 4), collider=True)
for x in (-1.85, 1.85):
    for z in (-1.85, 1.85):
        box('CornerPost', (x, 1.65, z), (.15, 3.0, .15), collider=True)
    beam('RoofSide', (x, 3.15, -1.95), (x, 3.15, 1.95), .12)
    beam('SideBrace', (x, 2.37, -1.85), (x, 3.13, -.95), .085)
    beam('SideBrace', (x, 2.37, 1.85), (x, 3.13, .95), .085)
for z in (-1.85, 1.85):
    beam('RoofCross', (-1.95, 3.15, z), (1.95, 3.15, z), .12)
for y in (.57, 1.25):
    beam('GuardFront', (-1.85, y, -1.85), (1.85, y, -1.85), .10, collider=True)
    for x in (-1.85, 1.85):
        beam('GuardSide', (x, y, -1.85), (x, y, 1.85), .10, collider=True)
    for side in (-1, 1):
        beam('GuardRear', (side * .60, y, 1.85), (side * 1.85, y, 1.85),
             .10, collider=True)
for x in (-.60, .60):
    box('OpeningPost', (x, .70, 1.85), (.09, 1.10, .09), collider=True)
for x in (-1.85, 1.85):
    beam('GuardDiagonal', (x, .21, -1.8), (x, 1.21, .0), .075)
beam('Ridge', (0, 3.62, -2.20), (0, 3.62, 2.20), .09)
# A closed thin double pitched cloth roof: waves give the sagging tarp silhouette.
roof_vertices = []
nx, nz = 16, 16
for inside in (False, True):
    for j in range(nz + 1):
        z = -2.22 + 4.44 * j / nz
        for i in range(nx + 1):
            x = -2.22 + 4.44 * i / nx
            y = 3.64 - .50 * abs(x) / 2.22
            y -= .07 * math.sin(math.pi * j / nz) * abs(x) / 2.22
            y += .018 * math.sin(i * math.pi * .75 + j * math.pi * .5)
            roof_vertices.append((x, y - (.018 if inside else 0), z))
stride, layer = nx + 1, (nx + 1) * (nz + 1)
roof_faces = []
for j in range(nz):
    for i in range(nx):
        a = j * stride + i
        roof_faces.append((a, a + 1, a + 1 + stride, a + stride))
        roof_faces.append(tuple(layer + k for k in (a + stride, a + 1 + stride, a + 1, a)))
outline = (list(range(stride)) + [j * stride + nx for j in range(1, nz + 1)] +
           [nz * stride + i for i in range(nx - 1, -1, -1)] +
           [j * stride for j in range(nz - 1, 0, -1)])
for i, a in enumerate(outline):
    b = outline[(i + 1) % len(outline)]
    roof_faces.append((a, b, b + layer, a + layer))
mesh('PitchedCanvasRoof', roof_vertices, roof_faces, 'TarpGreen')
for x in (-2.1, 2.1):
    for z in (-2.1, 2.1):
        tube('RoofTie', [(x, 3.17, z), (x * .89, 2.83, z * .89)],
             radius=.012, material='SandCanvas', sides=6)

for identifier, width, height in [('KH_TarpWide', 6, 2.3), ('KH_TarpNarrow', 3, 2.5)]:
    begin(identifier, 'Bottom centre; front faces local -Z; visual cloth, no collision')
    sheet('LooseCloth', width, height, 'TarpGreen', sag=.32, ripple=.055)
    for x in (-width / 2 + .08, width / 2 - .08):
        tube('CornerCord', [(x, height - .07, -.02), (x, height + .09, -.015)],
             radius=.008, material='SandCanvas', sides=6)

begin('KH_WindowBoards', 'Bottom centre; front faces local -Z; 2.8 x 2 m boarded patch')
for y, z, width in [(.22, 0, 2.76), (.67, .035, 2.80), (1.18, .01, 2.64), (1.73, -.02, 2.72)]:
    box('WeatheredPlank', (0, y, z), (width, .24, .065), 'WornWood')
beam('CrossedBoardA', (-1.26, .08, -.08), (1.23, 1.98, -.08), .20,
     depth=.065, front_normal=(0, 0, -1))
beam('CrossedBoardB', (-1.24, 1.99, -.15), (1.25, .07, -.15), .19,
     depth=.065, front_normal=(0, 0, -1))
for x in (-1.15, 1.14):
    for y in (.22, .67, 1.18, 1.73):
        box('NailHead', (x, y, -.08), (.018, .018, .012), 'SteelOxide')

begin('KH_CorrugatedPatch', 'Bottom centre; front faces local -Z; 2.6 x 2.4 m sheet')
sheet('RibbedSteel', 2.6, 2.4, 'SteelOxide', nx=96, ny=4,
      thickness=.012, sag=0, ripple=.035, corrugated=True)
for x in (-1.18, 1.18):
    for y in (.15, 2.25):
        box('Washer', (x, y, -.046), (.045, .045, .014), 'Frame')

begin('KH_CableSag', 'Midpoint at endpoints height; 10 m span local X, sag 1 m, no collision')
tube('InsulatedCable', [(-5 + 10 * i / 40, -math.sin(math.pi * i / 40),
                         .015 * math.sin(i * math.pi / 20)) for i in range(41)],
     radius=.021, sides=8)
for x in (-5, 5):
    box('EndMount', (x, 0, .035), (.10, .12, .12), 'SteelOxide')

begin('KH_Floodlight', 'Wall bracket centre; forward -Z, lens pitched down 35 degrees')
box('WallPlate', (0, 0, .018), (.16, .25, .036), 'SteelOxide')
beam('LampArm', (0, 0, -.01), (0, -.11, -.25), .045, 'SteelOxide')
angle = -math.radians(35)
box('LampHousing', (0, -.10, -.31), (.50, .40, .25), 'Frame', tilt=angle)
front = Vector((0, -.10, -.31)) + Vector((0, -math.sin(-angle), -math.cos(angle))) * .132
box('RecessedLens', tuple(front), (.414, .314, .014), 'LampGlass', tilt=angle)
for x in (-.29, .29):
    beam('SideBracket', (x, -.01, -.15), (x, -.10, -.32), .04, 'SteelOxide')
    box('PivotBolt', (x, -.1, -.31), (.035, .07, .07), 'SteelOxide')

begin('KH_GrimePatch', 'Bottom centre; front -Z; 4 x 3 m fixed UV dirt overlay, no collision')
sx, sy, sz = 2, 1.5, .001
mesh('StainSurface', [(-sx, 0, -sz), (sx, 0, -sz), (sx, 0, sz), (-sx, 0, sz),
                       (-sx, sy * 2, -sz), (sx, sy * 2, -sz),
                       (sx, sy * 2, sz), (-sx, sy * 2, sz)],
     BOX_FACES, 'DirtStain', fixed_uv=(4, 3))

begin('KH_Railing', 'Ground centre; 2 m metal section, 1.15 m top, continuous collision')
for x in (-.95, .95):
    box('RailPost', (x, .555, 0), (.065, 1.11, .065), 'Frame', collider=True)
    box('RailFoot', (x, .012, 0), (.10, .024, .10), 'SteelOxide', collider=True)
for y in (.15, .60, 1.1175):
    box('RailBar', (0, y, 0), (2, .065, .065), 'Frame', collider=True)

# Append modules after the established eleven props. Their source offsets,
# export chunk names and geometry therefore retain their previous identities.
interior_hook = pathlib.Path(__file__).with_name('interior_modules.py')
exec(compile(interior_hook.read_text(encoding='utf-8'), str(interior_hook), 'exec'), globals())
for hook_name in ('market_props6.py','living_props6.py','sign_checkpoint6.py'):
    hook_path=pathlib.Path(__file__).with_name(hook_name)
    exec(compile(hook_path.read_text(encoding='utf-8'),str(hook_path),'exec'),globals())

# Fail on every malformed part before producing any deployable files.
source_metrics = []
for prop in prototypes:
    for obj in prop['objects']:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        assert all(edge.is_manifold for edge in bm.edges), f'Open solid: {obj.name}'
        volume = bm.calc_volume(signed=True)
        assert volume > 1e-11, f'Non-positive volume: {obj.name}: {volume}'
        assert all(face.calc_area() > 1e-12 for face in bm.faces), f'Degenerate face: {obj.name}'
        source_metrics.append(dict(prop=prop['id'], name=obj.name,
                                   vertices=len(bm.verts), volume=volume,
                                   collider=bool(obj['unity_collision'])))
        bm.free()

preview = bpy.data.collections.new('Preview')
scene.collection.children.link(preview)
camera_data = bpy.data.cameras.new('PropsOverview')
camera = bpy.data.objects.new('PropsOverview', camera_data)
preview.objects.link(camera)
camera.location = xyz((43, 31, -28))
camera.rotation_euler = (Vector(xyz((17, 1, 10))) - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera_data.lens = 42
scene.camera = camera
scene['KursHubPropsStage'] = '5: reusable exterior props and empty interior architecture'
scene['Coordinates'] = 'Metres; vertices local; collection instances subtract instance_offset'
scene['Placement'] = 'Each prop FBX has its own useful origin; authoring prototypes use a display grid'
for area in bpy.context.screen.areas:
    if area.type == 'VIEW_3D':
        area.spaces.active.region_3d.view_distance = 49
        area.spaces.active.region_3d.view_location = xyz((18, 1, 10))
        area.spaces.active.clip_end = 1000
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'KursHubProps.blend'))

export_collection = bpy.data.collections.new('EXPORT_TEMP')
scene.collection.children.link(export_collection)
manifest = dict(version=1, stage=6,
                coordinates='Unity metres; X right, Y up, front -Z',
                palette=[dict(name=name, rgba=list(color)) for name, color in PALETTE.items()],
                props=[])
total_triangles = 0
for prop in prototypes:
    chunks = []
    exports = []
    for (material, collider), objects in sorted(prop['chunks'].items()):
        bpy.ops.object.select_all(action='DESELECT')
        copies = []
        for obj in objects:
            copy = obj.copy()
            copy.data = obj.data.copy()
            copy.location = (0, 0, 0)
            export_collection.objects.link(copy)
            copy.select_set(True)
            copies.append(copy)
        bpy.context.view_layer.objects.active = copies[0]
        if len(copies) > 1:
            bpy.ops.object.join()
        exported = bpy.context.object
        # Keep the staged sign chunk identities when replacing reflective
        # metal/lettering with matte fabric ink. Other props retain their names.
        chunk_material = 'Lettering' if material == 'ClothInk' else 'Frame' if material == 'InkDark' and prop['id'] == 'KH_SignMedicalTrade' else material
        exported.name = prop['id'] + '_' + chunk_material + ('_C' if collider else '_NC')
        exported.data.name = exported.name
        exports.append(exported)
        chunks.append(dict(name=exported.name, material=material, collider=collider))
    # Unity can collapse a single-mesh FBX into its model root. Keep an explicit
    # empty parent so every prop has the same root/mesh hierarchy and chunk IDs.
    export_root = bpy.data.objects.new(prop['id'] + '_Root', None)
    export_collection.objects.link(export_root)
    bpy.ops.object.select_all(action='DESELECT')
    export_root.select_set(True)
    for obj in exports:
        obj.parent = export_root
        obj.select_set(True)
    bpy.context.view_layer.objects.active = exports[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT / 'Models' / (prop['id'] + '.fbx')),
                            use_selection=True, object_types={'EMPTY', 'MESH'},
                            axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
                            bake_space_transform=False, use_mesh_modifiers=True,
                            add_leaf_bones=False, bake_anim=False, path_mode='AUTO')
    vertices = [vertex.co for obj in exports for vertex in obj.data.vertices]
    lo = [min(v[i] for v in vertices) for i in range(3)]
    hi = [max(v[i] for v in vertices) for i in range(3)]
    triangles = sum(len(face.vertices) - 2 for obj in exports for face in obj.data.polygons)
    total_triangles += triangles
    manifest['props'].append(dict(id=prop['id'],
        model='Assets/Environment/KursHub/Props/Models/' + prop['id'] + '.fbx',
        collection=prop['id'], sourceOffset=list(prop['offset']), chunks=chunks,
        origin=prop['description'], triangles=triangles,
        boundsMin=[lo[0], lo[2], lo[1]], boundsMax=[hi[0], hi[2], hi[1]]))
    for obj in exports:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.objects.remove(export_root, do_unlink=True)

(OUT / 'KursHubPropsManifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
(REPORT / 'props_geometry.json').write_text(json.dumps(dict(
    sourceObjects=len(source_metrics), sourceSolidsValidated=True,
    propCount=len(prototypes), triangles=total_triangles, parts=source_metrics,
    props=[{key: value for key, value in prop.items() if key != 'chunks'}
           for prop in manifest['props']]), indent=2), encoding='utf-8')
print('KURS_PROPS_GEOMETRY_OK', len(prototypes), 'reusable props;',
      total_triangles, 'triangles;', len(source_metrics), 'validated solids')
