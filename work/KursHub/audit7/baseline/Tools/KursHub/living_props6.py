"""Stage 6 living quarters and shared courtyard objects.

Executed by build_hub_props in its authoring namespace. All cloth is a closed,
thin solid; useful origins stay at the ground or the lower cloth edge. These
objects carry no gameplay, inventory, save or dynamic-physics behaviour.
"""


def _lp_tube(name, points, radius=.012, material='SandCanvas', sides=6):
    """Capped sweep with a stable frame, including vertical and Z-axis cords."""
    vertices = []
    previous_side = None
    for i, point in enumerate(points):
        direction = (Vector(points[min(i + 1, len(points) - 1)]) -
                     Vector(points[max(0, i - 1)])).normalized()
        side = (previous_side - direction * previous_side.dot(direction)
                if previous_side is not None else direction.cross(Vector((0, 1, 0))))
        if side.length < .1:
            side = direction.cross(Vector((1, 0, 0)))
        side.normalize()
        previous_side = side
        up = direction.cross(side).normalized()
        for j in range(sides):
            angle = math.tau * j / sides
            vertices.append(tuple(Vector(point) + radius *
                            (side * math.cos(angle) + up * math.sin(angle))))
    faces = [tuple(range(sides - 1, -1, -1)),
             tuple((len(points) - 1) * sides + j for j in range(sides))]
    for i in range(len(points) - 1):
        for j in range(sides):
            a, b = i * sides + j, i * sides + (j + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    return mesh(name, vertices, faces, material)


def _lp_soft(name, centre, size, material, power=.45, collider=False,
             segments=20, rings=10):
    """Seamed, gently uneven superellipsoid for fabric, pillows and soft bags."""
    cx, cy, cz = centre
    rx, ry, rz = [value / 2 for value in size]
    sgn = lambda value, exponent: math.copysign(abs(value) ** exponent, value)
    vertices = [(cx, cy - ry, cz)]
    for j in range(1, rings):
        phi = -math.pi / 2 + math.pi * j / rings
        radius = abs(math.cos(phi)) ** power
        for i in range(segments):
            a = math.tau * i / segments
            x = rx * radius * sgn(math.cos(a), power)
            z = rz * radius * sgn(math.sin(a), power)
            wrinkle = min(.012, ry * .08) * math.sin(a * 5 + j * .8) * math.cos(phi)
            vertices.append((cx + x, cy + ry * sgn(math.sin(phi), .72) + wrinkle,
                             cz + z))
    vertices.append((cx, cy + ry, cz))
    faces = [(0, 1 + (i + 1) % segments, 1 + i) for i in range(segments)]
    for j in range(rings - 2):
        for i in range(segments):
            a = 1 + j * segments + i
            b = 1 + j * segments + (i + 1) % segments
            faces.append((a, b, b + segments, a + segments))
    top = len(vertices) - 1
    for i in range(segments):
        a = 1 + (rings - 2) * segments + i
        b = 1 + (rings - 2) * segments + (i + 1) % segments
        faces.append((a, b, top))
    obj = mesh(name, vertices, faces, material, collider)
    for face in obj.data.polygons:
        face.use_smooth = True
    return obj


def _lp_cylinder(name, start, end, radius, material='Frame', sides=16,
                 collider=False, end_radius=None):
    a, b = Vector(start), Vector(end)
    direction = (b - a).normalized()
    side = direction.cross(Vector((0, 1, 0)))
    if side.length < .1:
        side = direction.cross(Vector((1, 0, 0)))
    side.normalize()
    up = direction.cross(side).normalized()
    vertices = []
    for point, r in ((a, radius), (b, end_radius if end_radius is not None else radius)):
        for i in range(sides):
            angle = math.tau * i / sides
            vertices.append(tuple(point + r * (side * math.cos(angle) +
                                               up * math.sin(angle))))
    faces = [tuple(range(sides - 1, -1, -1)), tuple(sides + i for i in range(sides))]
    faces.extend((i, (i + 1) % sides, (i + 1) % sides + sides, i + sides)
                 for i in range(sides))
    return mesh(name, vertices, faces, material, collider)


def _lp_ring(name, centre, radius, tube_radius, material='Frame', n=24, m=6):
    cx, cy, cz = centre
    vertices = []
    for i in range(n):
        angle = math.tau * i / n
        for j in range(m):
            minor = math.tau * j / m
            r = radius + tube_radius * math.cos(minor)
            vertices.append((cx + r * math.cos(angle), cy + tube_radius * math.sin(minor),
                             cz + r * math.sin(angle)))
    faces = []
    for i in range(n):
        for j in range(m):
            faces.append((i * m + j, ((i + 1) % n) * m + j,
                          ((i + 1) % n) * m + (j + 1) % m, i * m + (j + 1) % m))
    return mesh(name, vertices, faces, material)


def _lp_blanket(name, centre, width, depth, material, nx=18, nz=12,
                 hang=.12, thickness=.014, phase=0):
    """Closed cloth over a bed: uneven top, rolled edges and a hanging foot."""
    cx, cy, cz = centre
    vertices = []
    for bottom in (False, True):
        for j in range(nz + 1):
            v = j / nz
            for i in range(nx + 1):
                u = i / nx
                x, z = (u - .5) * width, (v - .5) * depth
                edge = max(0, (abs(v - .5) - .31) / .19)
                y = cy - hang * edge ** 1.7
                y += .016 * math.sin(5 * math.pi * u + phase)
                y += .014 * math.sin(7 * math.pi * v + u * 4 + phase)
                # A rumpled fold pulled across the foot end.
                y += .045 * math.exp(-((u - .68 - .05 * math.sin(v * 5)) / .07) ** 2)
                vertices.append((cx + x, y - (thickness if bottom else 0), cz + z))
    stride, layer = nx + 1, (nx + 1) * (nz + 1)
    faces = []
    for j in range(nz):
        for i in range(nx):
            a = j * stride + i
            faces.extend([(a, a + 1, a + 1 + stride, a + stride),
                          tuple(layer + k for k in (a + stride, a + stride + 1, a + 1, a))])
    outline = (list(range(stride)) + [j * stride + nx for j in range(1, nz + 1)] +
               [nz * stride + i for i in range(nx - 1, -1, -1)] +
               [j * stride for j in range(nz - 1, 0, -1)])
    for i, a in enumerate(outline):
        b = outline[(i + 1) % len(outline)]
        faces.append((a, b, b + layer, a + layer))
    return mesh(name, vertices, faces, material)


def _lp_seam(name, cx, y, cz, length, depth, material='SandCanvas'):
    # Four capped cords give a mattress its recognisable stitched border.
    for z in (-depth / 2 + .015, depth / 2 - .015):
        _lp_tube(name, [(cx - length / 2 + .07, y, cz + z),
                         (cx, y + .007, cz + z),
                         (cx + length / 2 - .07, y, cz + z)], .004, material)
    for x in (-length / 2 + .015, length / 2 - .015):
        _lp_tube(name, [(cx + x, y, cz - depth / 2 + .07),
                         (cx + x, y, cz + depth / 2 - .07)], .004, material)


begin('KH_BunkMakeshift', 'Floor centre; 2.30 x 1.08 m, 2.12 m high; head at local -X')
for x in (-1.08, 1.08):
    for z in (-.48, .48):
        box('SalvagedBunkPost', (x, 1.06, z), (.105, 2.12, .105), 'TimberRaw', True)
        box('BentCornerStrap', (x, .26, z - .056), (.15, .24, .012), 'SteelOxide')
        for y in (.22, .40, 1.31, 1.57):
            box('BunkBoltHead', (x, y, z - .069), (.026, .026, .018), 'Frame')
for level in (.34, 1.40):
    for z in (-.45, .45):
        box('UnevenSideBoard', (0, level, z), (2.17, .18, .065), 'WornWood', True)
    for x in (-1.03, 1.03):
        box('EndCrossBoard', (x, level, 0), (.07, .18, 1), 'TimberRaw', True)
    for i in range(9):
        box('BedSlat', (-.94 + i * .235, level + .05, 0), (.16, .065, .86), 'TimberRaw')
    _lp_soft('StuffedMattress', (0, level + .15, 0), (2.04, .19, .88), 'CanvasBeige',
             power=.33, collider=True)
    _lp_seam('MattressPiping', 0, level + .16, 0, 2.02, .88)
    _lp_soft('LumpyPillow', (-.71, level + .30, -.015), (.49, .18, .71), 'SandCanvas', .58)
    _lp_blanket('UnmadeBunkBlanket', (.25, level + .27, .015), 1.40, .99,
                'CanvasBlue' if level < 1 else 'CanvasRed', hang=.16,
                phase=level * 3)
# Upper safety board and braced ladder preserve the improvised silhouette.
box('UpperBackRail', (0, 1.86, .48), (2.18, .13, .07), 'TimberRaw')
box('UpperShortGuard', (.50, 1.83, -.48), (1.0, .14, .06), 'WornWood')
for x in (-.99, -.48):
    beam('LadderSide', (x, .04, -.69), (x, 1.97, -.54), .055, 'Frame', True)
for i in range(5):
    y = .30 + i * .34
    beam('LadderRung', (-.99, y, -.67 + i * .025), (-.48, y, -.67 + i * .025),
         .045, 'Frame')
beam('RearDiagonalRepair', (-1.04, .18, .54), (1.04, 1.39, .54), .065, 'WornWood')
for x in (-1.08, 1.08):
    _lp_tube('LashingOnBunk', [(x - .08, 1.42, -.54), (x + .08, 1.48, -.54),
                              (x - .08, 1.54, -.54)], .012, 'SandCanvas')


begin('KH_Bedroll', 'Floor centre; 2.10 x .94 m; low pallet mattress, head at -X')
for z in (-.31, .31):
    box('PalletRunner', (0, .055, z), (2.08, .11, .11), 'TimberRaw', True)
for i in range(8):
    box('PalletBedSlat', (-.91 + i * .26, .12, 0), (.22, .065, .91), 'WornWood')
_lp_soft('LowMattress', (0, .225, 0), (2.02, .20, .89), 'CanvasBeige', .35, True)
_lp_seam('LowMattressStitch', 0, .23, 0, 2.0, .87)
_lp_soft('BedrollPillow', (-.70, .35, -.025), (.47, .17, .71), 'SandCanvas', .56)
_lp_blanket('RumpledBedCover', (.23, .345, .02), 1.38, .96, 'CanvasBlue',
            hang=.12, phase=1.2)


begin('KH_LivingScreen', 'Bottom centre; 2.60 x 2.45 m; privacy cloth, front -Z')
for x in (-1.25, 1.25):
    box('ScreenPole', (x, 1.225, .035), (.065, 2.45, .065), 'TimberRaw', True)
    box('ScreenFoot', (x, .035, .025), (.20, .07, .44), 'TimberRaw', True)
beam('ScreenCrossRope', (-1.30, 2.37, .035), (1.30, 2.37, .035), .024, 'SandCanvas')
cloth = sheet('PrivacyCloth', 2.40, 2.26, 'CanvasBeige', nx=24, ny=14,
              thickness=.014, sag=.16, ripple=.09)
for vertex in cloth.data.vertices:
    vertex.co.z += .09
for x in (-1.14, -.58, 0, .58, 1.14):
    _lp_tube('ScreenTie', [(x, 2.26 - .12 * math.sin((x + 1.2) / 2.4 * math.pi), -.04),
                          (x + .025, 2.39, .03), (x + .06, 2.23, .035)],
             .008, 'SandCanvas')
# Visible sewn repair patches are separate thin solids, with coarse stitches.
box('ScreenClothPatch', (.63, .71, -.115), (.43, .46, .012), 'CanvasBlue')
box('ScreenClothPatch', (-.71, 1.53, -.12), (.38, .27, .012), 'CanvasRed')
for x in (.43, .83):
    for y in (.53, .64, .75, .86):
        beam('PatchStitch', (x - .015, y, -.126), (x + .015, y + .03, -.126),
             .005, 'SandCanvas')


for identifier, mat, phase in [('KH_RugRed', 'RugRed', .3),
                              ('KH_RugBlue', 'RugBlue', 2.0),
                              ('KH_RugWoven', 'RugWoven', 4.1)]:
    begin(identifier, 'Floor centre; 2.80 x 1.80 m woven rug, .12 m end fringes')
    rug = _lp_blanket('WornRugCloth', (0, .033, 0), 2.8, 1.8, mat,
                      nx=20, nz=12, hang=.008, thickness=.012, phase=phase)
    for vertex in rug.data.vertices:
        vertex.co.z = .018 + (vertex.co.z - .033) * .35
    # Rug maps describe a complete carpet, rather than tiled wall projection.
    for face in rug.data.polygons:
        for loop in face.loop_indices:
            v = rug.data.vertices[rug.data.loops[loop].vertex_index].co
            rug.data.uv_layers.active.data[loop].uv = (v.x / 2.8 + .5, v.y / 1.8 + .5)
    for side in (-1, 1):
        for i in range(23):
            z = -.85 + i * 1.7 / 22
            _lp_tube('KnottedRugFringe', [(side * 1.39, .026, z),
                      (side * 1.46, .018, z + .008 * math.sin(i * 3 + phase)),
                      (side * (1.49 + .018 * math.sin(i * 2 + phase)), .014, z + .015)],
                     .004, 'SandCanvas', sides=5)


for identifier, material in [('KH_RailDrape', 'CanvasBeige'),
                             ('KH_RailDrapeBlue', 'CanvasBlue')]:
    begin(identifier, 'Bottom centre; 2.60 m wide x 1.05 m high draped rail cloth; visual only')
    cloth = sheet('RailHangingCloth', 2.6, 1.05, material, nx=30, ny=10,
                  thickness=.012, sag=.07, ripple=.065)
    # A separate narrow fold passes over the top rail; lower edge remains the origin.
    fold = _lp_blanket('FoldOverHandrail', (0, 1.035, .045), 2.58, .20, material,
                       nx=28, nz=4, hang=.10, thickness=.012, phase=.6)
    for x in (-1.15, -.39, .42, 1.15):
        _lp_tube('RailClothTie', [(x, .94, -.06), (x + .018, 1.10, .015),
                                  (x + .06, .94, .10)], .008, 'SandCanvas')
    # Different repairs break up repeated cloth panels at balcony distance.
    box('DrapeRepair', (.73, .38, -.10), (.34, .25, .012),
        'CanvasBlue' if material == 'CanvasBeige' else 'CanvasBeige')
    for x in (.59, .87):
        for y in (.29, .38, .46):
            beam('DrapePatchStitch', (x - .018, y, -.11), (x + .018, y + .025, -.11),
                 .005, 'SandCanvas')


begin('KH_RestTable', 'Floor centre; 2.00 x .92 m communal plank table; top .78 m')
for x in (-.76, .76):
    for z in (-.30, .30):
        beam('TableSplayedLeg', (x * 1.08, .035, z * 1.18), (x, .72, z),
             .095, 'TimberRaw', True)
    box('TableCrossSupport', (x, .64, 0), (.10, .10, .84), 'WornWood')
box('TableLowerStretcher', (0, .22, .02), (1.62, .07, .08), 'TimberRaw')
for i in range(5):
    box('TableTopUnevenPlank', (0, .755 + .004 * math.sin(i * 2), -.36 + i * .18),
        (2.0 - .035 * (i % 2), .07, .174), 'WornWood', True)
for x in (-.78, .78):
    for z in (-.36, 0, .36):
        _lp_cylinder('TableNail', (x, .79, z), (x, .795, z), .009, 'Frame', sides=6)
# Enamel kettle: rounded body, lid, spout and arcing handle.
_lp_soft('KettleBody', (-.44, .91, .17), (.32, .27, .32), 'Enamel', .76)
_lp_cylinder('KettleLid', (-.44, 1.035, .17), (-.44, 1.05, .17), .13, 'Frame')
_lp_soft('KettleKnob', (-.44, 1.075, .17), (.045, .045, .045), 'WornWood', .9,
         segments=12, rings=6)
_lp_cylinder('KettleSpout', (-.56, .91, .17), (-.73, 1.01, .17), .037,
             'Enamel', 12, end_radius=.023)
_lp_tube('KettleHandle', [(-.57 + .26 * i / 12, 1.02 + .13 * math.sin(i * math.pi / 12), .17)
                         for i in range(13)], .014, 'Frame')
for cx, cz, mat in [(.43, -.19, 'Enamel'), (.76, .20, 'CanvasRed'), (-.03, .24, 'Enamel')]:
    _lp_cylinder('TinMug', (cx, .79, cz), (cx, .89, cz), .057, mat, 12)
    _lp_cylinder('DarkTea', (cx, .891, cz), (cx, .894, cz), .047, 'PlasticDark', 12)
    _lp_tube('CupLoopHandle', [(cx + .05, .87, cz), (cx + .10, .875, cz),
                              (cx + .115, .835, cz), (cx + .055, .81, cz)],
             .009, mat)
box('SharedBookCover', (.27, .799, .10), (.35, .025, .25), 'CanvasRed')
box('SharedBookPages', (.27, .817, .10), (.33, .022, .23), 'Paper')
box('LooseTablePaper', (.14, .794, -.23), (.26, .006, .25), 'Paper')
_lp_cylinder('TinPlate', (-.20, .793, -.21), (-.20, .805, -.21), .13, 'Enamel', 20)
_lp_soft('BreadLoaf', (-.22, .845, -.20), (.18, .09, .11), 'SandCanvas', .75,
         segments=14, rings=6)


begin('KH_FireBarrel', 'Floor centre; .66 m diameter x 1.03 m; open fire drum, static glowing coals')
segments = 28
vertices = []
# A continuous shell includes the top rim, inner wall and bottom thickness.
for radius, y in ((.29, .04), (.32, .20), (.325, .76), (.30, .98),
                  (.282, .98), (.307, .76), (.302, .20), (.272, .068)):
    for i in range(segments):
        a = math.tau * i / segments
        vertices.append((radius * math.cos(a), y, radius * math.sin(a)))
faces = []
for ring in range(7):
    for i in range(segments):
        faces.append((ring * segments + i, ring * segments + (i + 1) % segments,
                      (ring + 1) * segments + (i + 1) % segments, (ring + 1) * segments + i))
# Bottom outside and inside are complete polygons, making a closed concave solid.
faces.extend([tuple(range(segments - 1, -1, -1)),
              tuple(7 * segments + i for i in range(segments))])
mesh('OpenDrumSteelShell', vertices, faces, 'SteelOxide', True)
for y, r in ((.20, .324), (.74, .329), (.98, .304)):
    _lp_ring('PressedBarrelRib', (0, y, 0), r, .014, 'Frame', n=28)
for a in (.4, 2.7, 4.8):
    x, z = .315 * math.cos(a), .315 * math.sin(a)
    box('BarrelRepairPlate', (x, .48, z), (.08, .22, .02), 'Frame')
# Visible blackened logs cross above the coal bed. Coals have geometric colour,
# no transparent flame sheets and no per-instance particle simulation.
_lp_cylinder('CoalBed', (0, .65, 0), (0, .68, 0), .271, 'PlasticDark', 24)
for i, (cx, cz, r) in enumerate([(-.12, -.13, .075), (.04, -.16, .062),
                                (.15, -.07, .069), (-.16, .03, .069),
                                (0, .02, .082), (.12, .12, .067), (-.08, .17, .061)]):
    _lp_soft('GlowingCoal', (cx, .705 + .01 * (i % 2), cz), (r * 2, .09, r * 1.5),
             'Ember', .8, segments=10, rings=6)
_lp_cylinder('CharredLog', (-.22, .78, -.08), (.22, .83, .09), .06, 'WornWood', 10)
_lp_cylinder('CharredLog', (-.12, .81, .20), (.14, .79, -.17), .052, 'TimberRaw', 10)
_lp_cylinder('BlackenedLogEnd', (.19, .828, .079), (.224, .832, .092), .059,
             'PlasticDark', 10)
for x in (-.24, -.12, 0, .12, .24):
    _lp_cylinder('ImprovisedGrate', (x, .99, -.20), (x, .99, .20), .008, 'Frame', 6)


begin('KH_CampChair', 'Floor centre; .65 x .75 m folding chair, .90 m high; faces -Z')
for x in (-.28, .28):
    beam('ChairCrossedLeg', (x, .03, -.31), (x, .49, .23), .045, 'TimberRaw', True)
    beam('ChairCrossedLeg', (x, .03, .30), (x, .49, -.22), .045, 'TimberRaw', True)
    beam('ChairBackPole', (x, .43, .22), (x, .90, .35), .043, 'TimberRaw', True)
    box('ChairPivotBolt', (x + .015, .25, 0), (.065, .033, .033), 'SteelOxide')
for z in (-.23, .22):
    beam('ChairSeatBar', (-.30, .47, z), (.30, .47, z), .035, 'Frame')
beam('ChairBackTop', (-.29, .90, .35), (.29, .90, .35), .035, 'Frame')
_lp_blanket('SaggingChairSeat', (0, .47, 0), .55, .48, 'TarpGreen',
            nx=10, nz=6, hang=.035, thickness=.013)
back = sheet('ChairBackCanvas', .53, .34, 'TarpGreen', nx=12, ny=6,
              thickness=.014, sag=.025, ripple=.018)
for v in back.data.vertices:
    # Blender Z is authored vertical Y; Blender Y is authored depth Z.
    original_y = v.co.z
    v.co.z += .55
    v.co.y += .25 + original_y * .23
for x in (-.24, .24):
    for y in (.61, .80):
        _lp_tube('ChairCanvasTie', [(x, y, .26), (x * 1.15, y, .29),
                                   (x, y - .025, .29)], .005, 'SandCanvas')


begin('KH_ResidentBundle', 'Floor centre; 1.35 x .95 m resident belongings with bags, shoes and bedding')
# Bedroll with visible rolled circular ends, two retaining straps and carrying tie.
_lp_cylinder('RolledWoolBlanket', (.02, .21, .19), (.57, .21, .19), .19,
             'CanvasBeige', 20)
for x in (.013, .577):
    _lp_cylinder('RolledBlanketDarkEnd', (x - .006, .21, .19), (x + .006, .21, .19),
                 .14, 'TarpGreen', 20)
    _lp_cylinder('RolledBlanketInnerEnd', (x - .008, .21, .19), (x + .008, .21, .19),
                 .085, 'CanvasBeige', 20)
for x in (.13, .43):
    _lp_tube('BedrollTie', [(x, .21 + .197 * math.sin(math.tau * i / 16),
                             .19 + .197 * math.cos(math.tau * i / 16)) for i in range(17)],
             .014, 'Frame')
_lp_soft('StuffedResidentPack', (-.35, .33, .18), (.41, .61, .30), 'TarpGreen', .43, True)
_lp_soft('PackFrontPocket', (-.35, .26, -.005), (.33, .29, .105), 'CanvasBeige', .43)
_lp_soft('PackTopFlap', (-.35, .61, .16), (.39, .13, .32), 'TarpGreen', .4)
for x in (-.46, -.24):
    _lp_tube('BackpackWebbing', [(x, .17, -.073), (x, .50, -.075), (x, .62, .035)],
             .016, 'Frame')
    box('PackBuckle', (x, .35, -.093), (.044, .060, .015), 'SteelOxide')
_lp_tube('CarryPackHandle', [(-.43, .63, .20), (-.42, .70, .20),
                             (-.29, .70, .20), (-.28, .63, .20)], .014, 'Frame')
# Pair of actual low boots, with distinct soles, ankles, tongues and laces.
for x, z in ((-.41, -.30), (-.10, -.33)):
    _lp_soft('BootSole', (x, .035, z), (.17, .055, .34), 'Frame', .37,
             segments=16, rings=6)
    _lp_soft('BootLeather', (x, .10, z + .01), (.16, .14, .30), 'WornWood', .45,
             segments=16, rings=6)
    _lp_soft('BootAnkle', (x, .19, z + .09), (.15, .20, .15), 'WornWood', .38,
             segments=12, rings=6)
    _lp_soft('BootOpening', (x, .291, z + .09), (.10, .015, .10), 'Frame', .6,
             segments=12, rings=4)
    for i in range(4):
        _lp_tube('BootLace', [(x - .045, .17 + i * .016, z + .01 + i * .02),
                              (x + .045, .176 + i * .016, z + .025 + i * .02)],
                 .0035, 'SandCanvas', sides=5)
box('ClothBoundBook', (.29, .042, -.24), (.34, .073, .23), 'CanvasRed')
box('OldBookPages', (.29, .047, -.245), (.322, .045, .222), 'Paper')
box('FoldedResidentPaper', (.47, .012, -.30), (.30, .010, .22), 'Paper')
_lp_soft('SmallClothPouch', (.55, .09, -.13), (.22, .17, .20), 'SandCanvas', .55,
         segments=14, rings=8)
_lp_tube('PouchTie', [(.48, .17, -.13), (.55, .18, -.13), (.64, .17, -.12)],
         .008, 'Frame')


begin('KH_PottedPlant', 'Floor centre; roughly .70 x .70 m leafy pot, 1.10 m high')
segments = 18
vertices = []
for r, y in ((.155, .015), (.205, .31), (.185, .31), (.142, .045)):
    for i in range(segments):
        a = math.tau * i / segments
        vertices.append((r * math.cos(a), y, r * math.sin(a)))
faces = []
for ring in range(3):
    for i in range(segments):
        faces.append((ring * segments + i, ring * segments + (i + 1) % segments,
                      (ring + 1) * segments + (i + 1) % segments, (ring + 1) * segments + i))
faces.extend([tuple(range(segments - 1, -1, -1)),
              tuple(3 * segments + i for i in range(segments))])
mesh('ReusedFlowerPot', vertices, faces, 'Enamel', True)
_lp_ring('PotRolledLip', (0, .306, 0), .195, .012, 'SteelOxide', n=18)
_lp_cylinder('PotSoil', (0, .24, 0), (0, .266, 0), .17, 'PlasticDark', 18)
# Each broad leaf is a closed curved lozenge with a raised middle vein.
for i in range(11):
    angle = i * 2.39996
    height = .57 + .47 * ((i * 7) % 11) / 10
    spread = .19 + .18 * ((i * 3) % 11) / 10
    tip = Vector((spread * math.cos(angle), height,
                  spread * math.sin(angle)))
    stem_start = Vector((0, .265, 0))
    leaf_base = stem_start.lerp(tip, .46)
    centre = leaf_base.lerp(tip, .50)
    width = .055 + .025 * (i % 3)
    side = Vector((-math.sin(angle) * width, 0, math.cos(angle) * width))
    # Tapered diamond faces avoid the zero-area tips of a rectangular leaf grid.
    mid_ridge = centre + Vector((0, .028, 0))
    lower = Vector((0, -.008, 0))
    verts = [tuple(leaf_base), tuple(centre + side), tuple(tip), tuple(centre - side),
             tuple(mid_ridge), tuple(leaf_base + lower), tuple(centre + side + lower),
             tuple(tip + lower), tuple(centre - side + lower), tuple(centre + lower)]
    faces = [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4),
             (5, 9, 6), (6, 9, 7), (7, 9, 8), (8, 9, 5),
             (0, 5, 6, 1), (1, 6, 7, 2), (2, 7, 8, 3), (3, 8, 5, 0)]
    mesh('BroadBentLeaf', verts, faces, 'LeafGreen')
    _lp_tube('PlantStalk', [tuple(stem_start), tuple(leaf_base), tuple(mid_ridge)],
             .005, 'LeafGreen', sides=5)


begin('KH_FloorDebris', 'Floor centre; 1.40 x 1.10 m static loose papers, wood and cloth, no collision')
for i, (x, z, w, d) in enumerate([(-.44, -.30, .31, .24), (.13, .27, .25, .32),
                                  (.44, -.18, .28, .20), (-.04, -.39, .22, .18),
                                  (-.46, .32, .25, .20)]):
    # A gently bent page uses a separate thin solid with full paper UVs.
    obj = _lp_blanket('DiscardedPaper', (x, .018 + .004 * (i % 2), z), w, d,
                       'Paper', nx=4, nz=3, hang=.003, thickness=.004, phase=i)
    for vertex in obj.data.vertices:
        vertex.co.z = .012 + (vertex.co.z - .018) * .28
    for face in obj.data.polygons:
        for loop in face.loop_indices:
            v = obj.data.vertices[obj.data.loops[loop].vertex_index].co
            obj.data.uv_layers.active.data[loop].uv = ((v.x - x) / w + .5,
                                                       (v.y - z) / d + .5)
for start, end in [((-.60, .025, -.04), (-.20, .030, .15)),
                    ((.10, .035, -.07), (.58, .025, .10)),
                    ((-.22, .020, .32), (.31, .025, .48))]:
    beam('BrokenPackingSlat', start, end, .035, 'TimberRaw', depth=.015)
_lp_blanket('DiscardedClothScrap', (.33, .052, .35), .38, .29, 'CanvasBeige',
            nx=6, nz=4, hang=.02, thickness=.008, phase=1.8)
for x, z in ((-.09, .06), (.57, -.33), (-.59, .47)):
    _lp_soft('CrumpledPaperBall', (x, .036, z), (.085, .065, .075), 'Paper', .8,
             segments=10, rings=6)
