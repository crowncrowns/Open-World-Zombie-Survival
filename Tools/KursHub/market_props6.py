"""Lived-in market props appended inside build_hub_props.py's Blender namespace.

Dimensions/origins are Unity metres, +Y up, customer/front side -Z. Assemblies
stay independent from the shell. Thin cloth is a closed folded solid, ropes
and goods are real low polygon silhouettes rather than boxes with labels.
"""


def _mp6_tube(name, points, radius=.018, material='SandCanvas', sides=6):
    vertices = []
    for index, point in enumerate(points):
        direction = (Vector(points[min(index + 1, len(points) - 1)]) -
                     Vector(points[max(index - 1, 0)])).normalized()
        # A coil changes direction; choose a basis that never collapses at Z.
        reference = min((Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))),
                        key=lambda axis: abs(direction.dot(axis)))
        side = direction.cross(reference).normalized()
        up = direction.cross(side).normalized()
        for step in range(sides):
            angle = step * math.tau / sides
            vertices.append(tuple(Vector(point) + radius * (
                side * math.cos(angle) + up * math.sin(angle))))
    faces = [tuple(range(sides - 1, -1, -1)),
             tuple((len(points) - 1) * sides + step for step in range(sides))]
    for index in range(len(points) - 1):
        for step in range(sides):
            a, b = index * sides + step, index * sides + (step + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    return mesh(name, vertices, faces, material)


def _mp6_lathe(name, center, rings, material='Enamel', sides=10, collider=False):
    x, y, z = center
    vertices = [(x + radius * math.cos(step * math.tau / sides), y + height,
                 z + radius * math.sin(step * math.tau / sides))
                for height, radius in rings for step in range(sides)]
    faces = [tuple(range(sides - 1, -1, -1)),
             tuple((len(rings) - 1) * sides + step for step in range(sides))]
    for index in range(len(rings) - 1):
        for step in range(sides):
            a, b = index * sides + step, index * sides + (step + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    return mesh(name, vertices, faces, material, collider)


def _mp6_lump(name, center, size, material='SandCanvas', sides=10, collider=False):
    """Closed irregular sewn bag; flattened ends remain non-degenerate."""
    x, y, z = center
    width, height, depth = size
    vertices = []
    for level, radius in ((0, .53), (.12, .92), (.46, 1), (.80, .91), (1, .48)):
        for step in range(sides):
            angle = step * math.tau / sides
            ripple = 1 + .045 * math.sin(step * 3.3 + level * 8)
            vertices.append((x + width / 2 * radius * math.cos(angle) * ripple,
                             y + height * level,
                             z + depth / 2 * radius * math.sin(angle) * ripple))
    faces = [tuple(range(sides - 1, -1, -1)),
             tuple(4 * sides + step for step in range(sides))]
    for index in range(4):
        for step in range(sides):
            a, b = index * sides + step, index * sides + (step + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    return mesh(name, vertices, faces, material, collider)


def _mp6_roof(name, width=4.2, depth=2.8, material='TarpGreen',
              front_y=2.72, rear_y=3.0, nx=12, nz=6):
    vertices = []
    for layer in (-.006, .006):
        for zi in range(nz + 1):
            v = zi / nz
            for xi in range(nx + 1):
                u = xi / nx
                y = (front_y + (rear_y - front_y) * v -
                     .11 * math.sin(u * math.pi) -
                     .025 * math.sin(u * math.pi * 8) * math.sin(v * math.pi))
                vertices.append(((u - .5) * width, y + layer, (v - .5) * depth))
    stride = nx + 1
    count = stride * (nz + 1)
    faces = []
    for zi in range(nz):
        for xi in range(nx):
            a = zi * stride + xi
            faces += [(a, a + 1, a + 1 + stride, a + stride),
                      tuple(count + k for k in (a + stride, a + 1 + stride, a + 1, a))]
    outline = (list(range(stride)) + [j * stride + nx for j in range(1, nz + 1)] +
               [nz * stride + i for i in range(nx - 1, -1, -1)] +
               [j * stride for j in range(nz - 1, 0, -1)])
    for index, a in enumerate(outline):
        b = outline[(index + 1) % len(outline)]
        faces.append((a, a + count, b + count, b))
    return mesh(name, vertices, faces, material)


def _mp6_drape(name, center, width, height, material='CanvasBeige',
               rotate_y=0, nx=12, ny=5):
    obj = sheet(name, width, height, material, nx=nx, ny=ny,
                thickness=.013, sag=.08, ripple=.042)
    c, s = math.cos(rotate_y), math.sin(rotate_y)
    for vertex in obj.data.vertices:
        # The generic sheet is already Blender XYZ, undo the common basis.
        x, y, z = vertex.co.x, vertex.co.z, vertex.co.y
        vertex.co = xyz((center[0] + x * c + z * s, center[1] + y,
                         center[2] - x * s + z * c))
    obj.data.update()
    return obj


def _mp6_lashing(name, center, radius=.085, height=.16):
    x, y, z = center
    points = [(x + radius * math.cos(step * math.tau / 8),
               y + height * step / 24, z + radius * math.sin(step * math.tau / 8))
              for step in range(25)]
    _mp6_tube(name, points, .013, 'SandCanvas', 5)
    _mp6_tube(name + 'KnotTail', [(x + radius, y + .08, z),
              (x + radius + .055, y - .02, z - .04),
              (x + radius - .015, y - .21, z - .09)], .013, 'SandCanvas', 5)


def _mp6_crate(name, center, size=(.65, .46, .48), material='WornWood',
               collider=False):
    x, y, z = center
    width, height, depth = size
    for step in range(4):
        xx = x + (step - 1.5) * width / 4
        box(name + 'FloorSlat', (xx, y + .025, z),
            (width / 4 - .012, .05, depth), material, collider)
    for yy in (.15 * height, .48 * height, .82 * height):
        for zz in (-depth / 2 + .02, depth / 2 - .02):
            box(name + 'FrontSlat', (x, y + yy, z + zz),
                (width, height * .25, .04), material, collider)
        for xx in (-width / 2 + .02, width / 2 - .02):
            box(name + 'EndSlat', (x + xx, y + yy, z),
                (.04, height * .25, depth), 'TimberRaw', collider)
    for xx in (-width / 2 + .04, width / 2 - .04):
        for zz in (-depth / 2 + .04, depth / 2 - .04):
            box(name + 'Corner', (x + xx, y + height / 2, z + zz),
                (.06, height, .06), 'TimberRaw', collider)


def _mp6_bottle(name, center, height=.30, material='PlasticDark'):
    radius = height * .19
    _mp6_lathe(name, center, [(0, radius * .80), (.025, radius),
        (height * .67, radius), (height * .81, radius * .43),
        (height * .96, radius * .43)], material, 8)
    _mp6_lathe(name + 'Cap', (center[0], center[1] + height * .94, center[2]),
                [(0, radius * .50), (height * .06, radius * .50)], 'Copper', 8)
    _mp6_lathe(name + 'PaperLabel', (center[0], center[1] + height * .28, center[2]),
                [(0, radius * 1.015), (height * .26, radius * 1.015)], 'Paper', 8)


def _mp6_can(name, center, height=.16, radius=.065, material='Enamel'):
    _mp6_lathe(name, center, [(0, radius * .94), (.012, radius),
                (height - .012, radius), (height, radius * .94)], material, 10)
    _mp6_lathe(name + 'LidRim', (center[0], center[1] + height - .009, center[2]),
                [(0, radius * 1.015), (.012, radius * 1.015)], 'Frame', 10)


def _mp6_coil(name, center, radius=.14, material='PlasticDark', loops=3):
    x, y, z = center
    points = [(x + radius * math.cos(step * math.tau / 14),
               y + .018 + step * .0008,
               z + radius * math.sin(step * math.tau / 14))
              for step in range(loops * 14 + 1)]
    _mp6_tube(name, points, .014, material, 5)


def _mp6_backpack(name, center, scale=1, material='CanvasBlue'):
    x, y, z = center
    _mp6_lump(name, center, (.36 * scale, .49 * scale, .25 * scale), material)
    _mp6_lump(name + 'FrontPocket', (x, y + .05 * scale, z - .13 * scale),
               (.28 * scale, .23 * scale, .09 * scale), material)
    box(name + 'Flap', (x, y + .43 * scale, z - .02 * scale),
        (.32 * scale, .04 * scale, .27 * scale), 'SandCanvas', tilt=-.12)
    for side in (-1, 1):
        box(name + 'Webbing', (x + side * .105 * scale,
            y + .25 * scale, z - .185 * scale),
            (.025 * scale, .25 * scale, .018 * scale), 'Frame')
        box(name + 'Buckle', (x + side * .105 * scale,
            y + .32 * scale, z - .198 * scale),
            (.043 * scale, .045 * scale, .012 * scale), 'Copper')
        _mp6_tube(name + 'ShoulderStrap', [
            (x + side * .10 * scale, y + .42 * scale, z + .09 * scale),
            (x + side * .17 * scale, y + .28 * scale, z + .23 * scale),
            (x + side * .11 * scale, y + .08 * scale, z + .10 * scale)],
            .018 * scale, 'Frame', 5)
    _mp6_tube(name + 'GrabLoop', [
        (x - .06 * scale, y + .47 * scale, z),
        (x - .05 * scale, y + .55 * scale, z),
        (x + .05 * scale, y + .55 * scale, z),
        (x + .06 * scale, y + .47 * scale, z)], .014 * scale, 'Frame', 5)


def _mp6_tool(name, center, kind=0):
    x, y, z = center
    if kind == 0:  # Hammer, angled head and wooden handle.
        beam(name + 'Handle', (x - .12, y + .03, z - .13),
             (x + .09, y + .03, z + .12), .035, 'TimberRaw')
        beam(name + 'Head', (x + .02, y + .06, z + .19),
             (x + .20, y + .06, z + .04), .07, 'Frame', depth=.08)
    elif kind == 1:  # Pliers, two distinct tapered arms and opposing jaws.
        for side in (-1, 1):
            beam(name + 'Handle', (x + side * .09, y + .035, z - .15),
                 (x, y + .035, z + .02), .025, 'CanvasRed')
            beam(name + 'Jaw', (x, y + .035, z + .02),
                 (x + side * .04, y + .035, z + .13), .026, 'Frame')
        _mp6_lathe(name + 'Pivot', (x, y + .02, z + .02),
                    [(0, .03), (.035, .03)], 'Copper', 8)
    elif kind == 2:  # Hand saw: trapezoid blade plus unmistakable wooden grip.
        profile = [(-.20, -.12), (.21, -.12), (.26, .06), (-.20, .10)]
        vertices = [(x + xx, y + yy, z + zz) for yy in (.025, .045)
                    for xx, zz in profile]
        mesh(name + 'SawBlade', vertices, BOX_FACES, 'Frame')
        box(name + 'SawGrip', (x - .20, y + .055, z - .01),
            (.065, .065, .24), 'TimberRaw')
        for step in range(8):
            box(name + 'SawTooth', (x - .14 + step * .045, y + .035, z - .13),
                (.02, .018, .035), 'Frame')
    else:  # Screwdriver with contrasting plastic grip.
        beam(name + 'Shaft', (x, y + .03, z - .14),
             (x + .04, y + .03, z + .12), .014, 'Frame')
        beam(name + 'Grip', (x + .035, y + .03, z + .07),
             (x + .055, y + .03, z + .20), .045, 'CanvasRed')


def _mp6_stall(kind):
    timber = kind == 'Timber'
    begin('KH_Stall' + kind,
          'Floor centre; customer side -Z; 4.2 x 3.1 x 2.8 m; counter top Y 1.05; rear shelf Y 1.55/2.08; separate stock fits on top')
    for x in (-1.98, 1.98):
        for z in (-1.29, 1.29):
            if timber:
                beam('UnevenSalvagedPost', (x, .02, z),
                     (x + (.045 if x < 0 else -.025), 3.04, z),
                     .115, 'TimberRaw', True, depth=.13)
            else:
                _mp6_tube('GalvanisedPipeUpright', [(x, .03, z), (x, 3.05, z)],
                          .045, 'SteelOxide', 8)
                box('PipeFoot', (x, .025, z), (.20, .05, .20), 'Frame', True)
            _mp6_lashing('RoofRopeBinding', (x, 2.79 if z < 0 else 2.94, z), .085)
            _mp6_lashing('ShelfRopeBinding', (x, 1.48, z), .077, .12)
    for z, y in ((-1.29, 2.79), (1.29, 3.04)):
        beam('AwningCrossMember', (-2.04, y, z), (2.04, y, z),
             .085, 'WornWood' if timber else 'SteelOxide')
    for x in (-1.98, 1.98):
        beam('AwningSideMember', (x, 2.79, -1.35), (x, 3.04, 1.35),
             .07, 'WornWood' if timber else 'Frame')
    _mp6_roof('SaggingStallAwning', material='CanvasBeige' if timber else 'TarpGreen')
    # Sewn overlaid corner patch has a useful contrasting material silhouette.
    box('AwningRepairPatch', (1.05, 2.85, .55), (.63, .018, .48),
        'CanvasBlue' if timber else 'SandCanvas', tilt=.10)
    for x in (-1.93, 1.93):
        beam('RearDiagonalBrace', (x, .20, .20), (x, 2.78, 1.26),
             .055, 'SteelOxide')
    _mp6_drape('BackCurtain', (0, .12, 1.33), 3.85, 2.80,
               'CanvasBlue' if timber else 'CanvasBeige')
    _mp6_drape('SideCurtain', (-1.99 if timber else 1.99, .20, .62),
               1.25, 2.40, 'CanvasRed' if timber else 'CanvasBlue', math.pi / 2)
    # Front apron is built from individual mismatched recovered boards.
    for step in range(15):
        x = -1.82 + step * .26
        yy = .43 + .025 * math.sin(step * 2.7)
        box('CounterVerticalBoard', (x, yy, -1.08),
            (.245, .80 + .04 * math.sin(step), .055),
            'TimberRaw' if step % 4 == 0 else 'WornWood', True)
        for y in (.13, .72):
            box('BoardNail', (x, y, -1.112), (.018, .018, .012), 'Frame')
    for step in range(5):
        box('CounterTopPlank', (0, 1.01 + .005 * (step % 2), -1.03 + step * .15),
            (3.96, .08, .142), 'TimberRaw' if step % 3 == 0 else 'WornWood', True)
    for x in (-1.76, 0, 1.76):
        box('CounterLeg', (x, .49, -.66), (.09, .98, .09), 'WornWood', True)
    for yy in (.55, 1.55, 2.08):
        for step in range(3):
            box('RearShelfBoard', (0, yy, .89 + step * .16),
                (3.82, .055, .153), 'WornWood', True)
        for x in (-1.78, 0, 1.78):
            beam('ShelfBracket', (x, yy - .25, 1.25), (x, yy, .78),
                 .04, 'SteelOxide')
    for x in (-1.78, 1.78):
        box('ShelfUpright', (x, 1.32, 1.24), (.07, 2.64, .07), 'Frame', True)
    # A few stored objects belong to the assembly; most stock remains reusable.
    _mp6_crate('UnderCounterCrate', (-1.28, .03, -.10), (.65, .45, .50))
    _mp6_lump('StoredSack', (1.40, .02, .50), (.55, .65, .43), 'SandCanvas')
    for step in range(4):
        _mp6_can('ShelfTin', (-1.52 + step * .15, 1.58, 1.03),
                 .16 + .04 * (step % 2), .057,
                 'CanvasRed' if step % 2 else 'Enamel')
    _mp6_coil('HangingSpareCord', (1.27, 2.13, 1.03), .13, 'Copper', 2)
    for x in (-1.9, 1.9):
        _mp6_tube('CounterRopeTie', [(x, 1.09, -.42), (x + .04, .95, -.55),
                    (x, .76, -.51)], .012, 'SandCanvas', 5)


_mp6_stall('Timber')
_mp6_stall('Pipe')

begin('KH_StallCounter',
      'Floor centre; customer side -Z; 3 x 1.08 x 1.15 m open recovered plank counter; top Y 1.05')
for _mp6_x in (-1.36, 1.36):
    for _mp6_z in (-.46, .46):
        box('CounterSquareLeg', (_mp6_x, .51, _mp6_z), (.11, 1.02, .11), 'TimberRaw', True)
for _mp6_i in range(5):
    box('SalvagedCounterPlank', (0, 1.01, -.46 + _mp6_i * .23),
        (3, .08, .218), 'TimberRaw' if _mp6_i == 2 else 'WornWood', True)
for _mp6_z in (-.46, .46):
    beam('CounterBrace', (-1.38, .18, _mp6_z), (1.38, .73, _mp6_z), .07, 'SteelOxide')
for _mp6_i in range(4):
    box('LowerCounterShelf', (0, .28, -.39 + _mp6_i * .26), (2.75, .055, .25), 'WornWood', True)
_mp6_drape('CounterHangingCloth', (0, .12, -.57), 2.8, .77, 'CanvasBeige')
for _mp6_x in (-1.35, 1.35):
    _mp6_lashing('CounterCornerLashing', (_mp6_x, .85, -.46), .087, .13)

begin('KH_GoodsFood',
      'Stock bottom centre; 1.7 x .9 x .76 m bottles, tin cans, jars, vegetable crate and tied grain bags; no collision')
_mp6_crate('FoodCrate', (-.48, 0, .04), (.70, .43, .60), 'WornWood')
for _mp6_i in range(6):
    _mp6_lump('RootVegetables', (-.70 + (_mp6_i % 3) * .18, .36, -.10 + (_mp6_i // 3) * .24),
               (.17, .13, .18), 'TimberRaw')
for _mp6_i in range(5):
    _mp6_bottle('PreservedBottle', (.06 + (_mp6_i % 3) * .15, .015, -.21 + (_mp6_i // 3) * .20),
                .32 + .03 * (_mp6_i % 2), 'PlasticDark')
for _mp6_i in range(4):
    _mp6_can('PreservedTin', (.52 + (_mp6_i % 2) * .15, .01 + (_mp6_i // 2) * .18, -.19),
             .17, .065, 'CanvasRed' if _mp6_i % 2 else 'Enamel')
for _mp6_x in (.12, .45):
    _mp6_lump('GrainSack', (_mp6_x, 0, .23), (.33, .58, .32), 'SandCanvas')
    _mp6_lashing('GrainSackTiedNeck', (_mp6_x, .50, .23), .075, .06)
_mp6_can('WideGlassJar', (.73, .02, .21), .30, .10, 'PlasticDark')
box('FoldedPaperPacket', (-.20, .015, -.32), (.25, .03, .15), 'Paper', tilt=.05)

begin('KH_GoodsTools',
      'Stock bottom centre; 1.75 x .55 x .72 m hammer, saw, pliers, screwdrivers, cable coils and parts trays; no collision')
box('ToolDisplayBoard', (-.28, .018, 0), (1.08, .036, .66), 'WornWood')
for _mp6_i in range(4):
    _mp6_tool('HandTool', (-.66 + (_mp6_i % 2) * .41, .037, -.17 + (_mp6_i // 2) * .32), _mp6_i)
_mp6_coil('CableCoil', (.56, 0, -.13), .18, 'PlasticDark', 3)
_mp6_coil('CopperWireCoil', (.58, .015, .23), .13, 'Copper', 2)
box('PartsTray', (.04, .035, .17), (.25, .07, .23), 'SteelOxide')
for _mp6_i in range(6):
    _mp6_lathe('LooseFitting', (-.03 + (_mp6_i % 3) * .065, .078,
                  .11 + (_mp6_i // 3) * .10), [(0, .022), (.035, .022)], 'Copper', 6)
box('OilCanBody', (.07, .14, -.20), (.19, .28, .14), 'Enamel')
_mp6_tube('OilCanSpout', [(.07, .27, -.20), (.12, .36, -.20), (.19, .38, -.20)], .013, 'Copper')
box('WornPartsBox', (-.70, .21, .18), (.29, .18, .22), 'Paper')

begin('KH_GoodsGear',
      'Stock bottom centre; 1.8 x .85 x .8 m backpacks, rolled bedmat, paired boots and canteen; no collision')
_mp6_backpack('KhakiBackpack', (-.52, .01, .07), 1.14, 'SandCanvas')
_mp6_backpack('BlueBackpack', (.04, .01, .13), .87, 'CanvasBlue')
_mp6_lump('RolledBedmat', (.52, .02, .20), (.60, .29, .36), 'TarpGreen')
for _mp6_x in (.31, .69):
    _mp6_tube('BedmatStrap', [(_mp6_x, .03, .38), (_mp6_x, .28, .34),
        (_mp6_x, .33, .20), (_mp6_x, .28, .06), (_mp6_x, .03, .02)], .017, 'Frame', 5)
for _mp6_x in (.39, .61):
    _mp6_lump('BootToe', (_mp6_x, .01, -.25), (.17, .12, .32), 'PlasticDark')
    _mp6_lump('BootUpper', (_mp6_x, .04, -.15), (.15, .26, .15), 'WornWood')
    for _mp6_y in (.13, .18, .23):
        beam('BootLace', (_mp6_x - .055, _mp6_y, -.235),
             (_mp6_x + .055, _mp6_y + .015, -.235), .009, 'SandCanvas')
_mp6_lump('ArmyCanteen', (-.08, .01, -.28), (.23, .29, .13), 'TarpGreen')
_mp6_lathe('CanteenCap', (-.08, .28, -.28), [(0, .037), (.045, .037)], 'Frame', 8)

begin('KH_PalletCrates',
      'Floor centre; 1.5 x 1.45 x 1.25 m open slatted crates on pallet with bottle and tied box; static collision')
for _mp6_x in (-.58, 0, .58):
    box('PalletRunner', (_mp6_x, .06, 0), (.12, .12, 1.22), 'TimberRaw', True)
for _mp6_i in range(6):
    box('PalletDeckBoard', (0, .15, -.53 + _mp6_i * .21), (1.50, .06, .19), 'WornWood', True)
_mp6_crate('LowerCrateA', (-.37, .18, -.18), (.68, .47, .72), collider=True)
_mp6_crate('LowerCrateB', (.37, .18, .15), (.68, .62, .70), collider=True)
_mp6_crate('TopCrate', (-.27, .65, -.12), (.62, .48, .59), collider=True)
box('WrappedParcel', (.36, .92, .11), (.49, .26, .43), 'Paper', True)
for _mp6_x in (.20, .52):
    box('ParcelRope', (_mp6_x, 1.055, .11), (.016, .012, .44), 'SandCanvas')
_mp6_bottle('CrateBottle', (-.32, 1.10, -.12), .32)

begin('KH_SupplySackStack',
      'Floor centre; 1.5 x 1.10 x 1.15 m mismatched sewn sacks and patched grain bag with tied necks; static collision')
for _mp6_i, (_mp6_x, _mp6_y, _mp6_z, _mp6_size) in enumerate([
        (-.39, 0, -.27, (.67, .31, .49)), (.37, 0, -.27, (.66, .32, .47)),
        (-.35, 0, .27, (.67, .34, .45)), (.37, 0, .25, (.68, .30, .48)),
        (-.28, .30, -.10, (.75, .33, .58)), (.35, .31, .15, (.64, .34, .56)),
        (.03, .63, .01, (.66, .33, .53))]):
    _mp6_lump('LumpySupplyBag', (_mp6_x, _mp6_y, _mp6_z), _mp6_size,
               'SandCanvas' if _mp6_i % 3 else 'CanvasBeige', collider=_mp6_i < 4)
    box('SackSewnPatch', (_mp6_x, _mp6_y + _mp6_size[1] * .56,
                          _mp6_z - _mp6_size[2] * .48),
        (.15, .10, .012), 'CanvasBlue' if _mp6_i % 2 else 'CanvasRed')
    _mp6_tube('SackSeam', [(_mp6_x - .20, _mp6_y + .09, _mp6_z - .22),
                (_mp6_x, _mp6_y + .12, _mp6_z - .25),
                (_mp6_x + .20, _mp6_y + .08, _mp6_z - .22)], .006, 'WornWood', 4)

begin('KH_LowDisplay',
      'Floor centre; 2.25 x .70 x 1.4 m ground trader display: textile mat, open crate, books, boots, enamel bowls and folded clothing; no collision')
box('GroundDisplayMat', (0, .013, 0), (2.25, .026, 1.4), 'CanvasRed')
for _mp6_z in (-.65, .65):
    box('MatBorder', (0, .029, _mp6_z), (2.18, .007, .045), 'SandCanvas')
_mp6_crate('LowDisplayCrate', (-.72, .028, .20), (.62, .36, .54))
for _mp6_i in range(4):
    box('SecondHandBook', (-.13 + _mp6_i * .025, .055 + _mp6_i * .055, .31),
        (.32, .048, .44), 'Paper' if _mp6_i % 2 else 'CanvasBlue')
for _mp6_i in range(3):
    _mp6_lump('FoldedClothing', (.56, .03 + _mp6_i * .095, .22),
               (.55, .10, .40), ('CanvasBeige', 'CanvasBlue', 'TarpGreen')[_mp6_i])
for _mp6_x in (-.66, -.42):
    _mp6_lump('TradedShoe', (_mp6_x, .03, -.37), (.19, .16, .34), 'PlasticDark')
for _mp6_x in (.15, .54, .89):
    _mp6_lathe('EnamelBowl', (_mp6_x, .03, -.38),
                [(0, .07), (.04, .105), (.11, .14), (.13, .14),
                 (.13, .12), (.045, .085), (.035, .03)], 'Enamel', 10)
_mp6_coil('FloorRopeCoil', (.80, .03, .35), .16, 'SandCanvas', 2)
_mp6_bottle('FloorCanteen', (-.78, .39, .19), .27, 'PlasticDark')

begin('KH_WorkbenchStocked',
      'Floor centre; front -Z; 2.8 x 2.35 x 1.2 m scarred timber repair bench, vise, hanging real tools and stocked slatted shelves; static collision')
for _mp6_x in (-1.22, 1.22):
    for _mp6_z in (-.43, .43):
        box('WorkbenchLeg', (_mp6_x, .48, _mp6_z), (.12, .96, .12), 'TimberRaw', True)
for _mp6_i in range(5):
    box('WorkbenchTopPlank', (0, .99, -.47 + _mp6_i * .235),
        (2.8, .10, .226), 'WornWood' if _mp6_i % 2 else 'TimberRaw', True)
for _mp6_i in range(4):
    box('UnderBenchSlat', (0, .28, -.36 + _mp6_i * .24), (2.56, .055, .23), 'WornWood', True)
for _mp6_x in (-1.28, 1.28):
    box('BackboardUpright', (_mp6_x, 1.18, .52), (.075, 2.35, .075), 'Frame', True)
for _mp6_i in range(5):
    box('ToolBoardRecoveredPlank', (0, 1.30 + _mp6_i * .18, .52),
        (2.48, .17, .05), 'WornWood' if _mp6_i % 2 else 'TimberRaw')
for _mp6_x in (-.97, -.58, -.18, .25, .69, 1.04):
    beam('ToolHook', (_mp6_x, 1.95, .46), (_mp6_x, 1.95, .36), .018, 'SteelOxide')
    beam('HangingToolHandle', (_mp6_x, 1.57, .35), (_mp6_x, 1.95, .35), .035, 'TimberRaw')
    beam('HangingToolHead', (_mp6_x - .10, 1.84, .34), (_mp6_x + .10, 1.84, .34), .065, 'Frame')
box('ViseBase', (-.96, 1.08, -.28), (.32, .08, .28), 'Frame')
box('ViseBody', (-.96, 1.19, -.28), (.19, .17, .22), 'SteelOxide')
for _mp6_z in (-.39, -.15):
    box('ViseJaw', (-.96, 1.29, _mp6_z), (.27, .09, .06), 'Frame')
_mp6_tube('ViseScrew', [(-.96, 1.19, -.54), (-.96, 1.19, -.15)], .028, 'Frame', 8)
_mp6_tube('ViseHandle', [(-.96, 1.06, -.55), (-.96, 1.34, -.55)], .016, 'Frame', 6)
_mp6_tool('BenchHammer', (-.20, 1.05, -.21), 0)
_mp6_tool('BenchPliers', (.29, 1.05, -.20), 1)
_mp6_coil('BenchCopperWire', (.80, 1.04, -.02), .18, 'Copper', 2)
_mp6_can('WorkbenchOilCan', (.95, 1.04, .29), .28, .09, 'CanvasRed')
_mp6_crate('WorkbenchStorageCrate', (.69, .31, .05), (.63, .44, .65))
_mp6_lump('WorkbenchRag', (-.50, 1.045, .20), (.37, .04, .27), 'CanvasBeige')

begin('KH_Stool', 'Floor centre; .5 x .52 x .5 m three-legged handmade plank stool; static collision')
for _mp6_i in range(3):
    _mp6_angle = _mp6_i * math.tau / 3
    beam('StoolSplayedLeg', (.22 * math.cos(_mp6_angle), .015, .22 * math.sin(_mp6_angle)),
         (.15 * math.cos(_mp6_angle), .46, .15 * math.sin(_mp6_angle)),
         .065, 'TimberRaw', True)
for _mp6_i in range(3):
    box('StoolSeatPlank', (( _mp6_i - 1) * .16, .485, 0), (.15, .07, .49),
        'WornWood' if _mp6_i % 2 else 'TimberRaw', True)

begin('KH_Bench', 'Floor centre; front -Z; 1.8 x .88 x .65 m recovered plank bench with back; static collision')
for _mp6_x in (-.68, .68):
    for _mp6_z in (-.22, .22):
        beam('BenchLeg', (_mp6_x, .015, _mp6_z * 1.2),
             (_mp6_x, .45, _mp6_z), .065, 'Frame', True)
    beam('BenchBackSupport', (_mp6_x, .30, .24),
         (_mp6_x, .88, .30), .055, 'SteelOxide', True)
for _mp6_i in range(3):
    box('BenchSeatPlank', (0, .45, -.22 + _mp6_i * .21), (1.8, .07, .20),
        'WornWood' if _mp6_i % 2 else 'TimberRaw', True)
for _mp6_y in (.64, .81):
    box('BenchBackPlank', (0, _mp6_y, .28), (1.8, .14, .05), 'WornWood', True, tilt=.10)
beam('BenchLowerBrace', (-.7, .21, 0), (.7, .21, 0), .055, 'Frame')
