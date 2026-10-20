"""Stage 6: hanging market signs, checkpoint fittings and festoon lamps.

Executed in build_hub_props.py after interior_modules.py. All meshes are closed
static solids in Unity metre coordinates. Cloth lettering follows the actual
folded surface; the large central banner is readable from either gallery.
"""


def sc6_recalc(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    # Letter caps and bird silhouettes become non-planar on a folded banner.
    # Triangulate them before volume/normals/export so Blender and FBX use the
    # same real cap surface rather than an implicit non-planar ngon fan.
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if bm.calc_volume(signed=True) < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def sc6_fold(x, y, width, height):
    u = x / width + .5
    v = y / height
    return (.042 * math.sin(6 * math.pi * u) * (.4 + .6 * v) +
            .028 * math.sin(11 * math.pi * u + 1.7 * v) +
            .045 * math.sin(math.pi * u) * math.sin(2 * math.pi * v))


def sc6_cloth(width, height, material, big=False):
    nx, ny = (40, 32) if big else (24, 10)
    vertices = []
    for back in (False, True):
        for j in range(ny + 1):
            y = height * j / ny
            for i in range(nx + 1):
                x = width * (i / nx - .5)
                edge_wave = .025 * math.sin(i / nx * math.pi * 7)
                sag = (.12 if big else .055) * math.sin(math.pi * i / nx)
                yy = y - sag * (j / ny) + edge_wave * (1 - j / ny)
                zz = sc6_fold(x, yy, width, height) + (.007 if back else -.007)
                vertices.append((x, yy, zz))
    stride, layer = nx + 1, (nx + 1) * (ny + 1)
    faces = []
    for j in range(ny):
        for i in range(nx):
            a = j * stride + i
            faces.append((a, a + 1, a + stride + 1, a + stride))
            faces.append(tuple(layer + k for k in (a + stride, a + stride + 1, a + 1, a)))
    outline = (list(range(stride)) + [j * stride + nx for j in range(1, ny + 1)] +
               [ny * stride + i for i in range(nx - 1, -1, -1)] +
               [j * stride for j in range(ny - 1, 0, -1)])
    for i, a in enumerate(outline):
        b = outline[(i + 1) % len(outline)]
        faces.append((a, a + layer, b + layer, b))
    mesh('FoldedSignCloth', vertices, faces, material)
    # Rolled hems follow the same cloth folds, rather than floating straight
    # rods. Irregular short stitches are deliberately a separate pale material.
    for y in (.045, height - .07):
        tube('SewnClothHem', [(x, y, sc6_fold(x, y, width, height))
             for x in [-width / 2 + width * i / 32 for i in range(33)]],
             radius=.013 if big else .009, material=material, sides=6)
    for side in (-1, 1):
        x = side * (width / 2 - .045)
        tube('SewnSideHem', [(x, height * i / 20,
              sc6_fold(x, height * i / 20, width, height)) for i in range(21)],
             radius=.012 if big else .008, material=material, sides=6)
        tube('KnottedSuspensionCord', [(x, height - .12, .015),
             (x + side * .025, height + .045, .025),
             (x + side * .06, height + (.5 if big else .23), .045)],
             radius=.018 if big else .012, material='SandCanvas', sides=6)
        for y in (.12, height - .16):
            # Grommet ring: both annular faces and inner/outer rims are closed.
            n = 12
            radius, inner = (.037, .021) if big else (.027, .014)
            z = sc6_fold(x, y, width, height)
            verts = [(x + r * math.cos(i * math.tau / n),
                      y + r * math.sin(i * math.tau / n), z + depth)
                     for depth in (-.014, .014) for r in (radius, inner)
                     for i in range(n)]
            fs = []
            for i in range(n):
                k = (i + 1) % n
                fs += [(i, k, n + k, n + i),
                       (2 * n + i, 3 * n + i, 3 * n + k, 2 * n + k),
                       (i, 2 * n + i, 2 * n + k, k),
                       (n + i, n + k, 3 * n + k, 3 * n + i)]
            mesh('RustedClothEyelet', verts, fs, 'SteelOxide')


def sc6_ink(name, value, x, y, h, max_width, width, height,
            material='ClothInk', back=False):
    obj = module_text(name, value, (x, y, 0), h, max_width, material)
    sc6_refine(obj)
    # module_text stores Unity Y in Blender Z. Preserve its closed extrusion
    # while bending the whole inscription a few millimetres off the fabric.
    for v in obj.data.vertices:
        xx = -v.co.x if back else v.co.x
        yy = v.co.z
        thickness = -v.co.y
        zz = sc6_fold(xx, yy, width, height)
        zz += (.028 + thickness) * (1 if back else -1)
        v.co = xyz((xx, yy, zz))
    sc6_recalc(obj)
    return obj


def sc6_refine(obj):
    # Long letter caps must sample cloth folds, otherwise the fabric cuts
    # through them between sparse font vertices and makes words unreadable.
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    for _ in range(6):
        edges=[edge for edge in bm.edges if edge.calc_length()>.16]
        if not edges:break
        bmesh.ops.subdivide_edges(bm,edges=edges,cuts=1,use_grid_fill=True)
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free();obj.data.update()


def sc6_flat_shape(name, outline, material='ClothInk', depth=.012,
                   cloth=None, back=False):
    n = len(outline)
    verts = []
    for side in (0, 1):
        for x, y in outline:
            xx = -x if back else x
            verts.append((xx, y, depth * side))
    fs = [tuple(range(n - 1, -1, -1)), tuple(range(n, n * 2))]
    fs += [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    obj = mesh(name, verts, fs, material)
    sc6_refine(obj)
    for vertex in obj.data.vertices:
        x,y,depth=vertex.co.x,vertex.co.z,vertex.co.y
        vertex.co.y=(sc6_fold(x,y,cloth[0],cloth[1]) if cloth else 0)+(.03+depth)*(1 if back else -1)
    sc6_recalc(obj)
    return obj


def sc6_bird(x, y, size, cloth, back=False):
    # The original banner's light bird/sail emblem reads across the atrium.
    # Overlapping filled silhouettes remain individual closed solids.
    shapes = [
        [(-.07, -.18), (.01, -.06), (.18, .12), (.42, .19),
         (.21, .22), (.16, .28), (.07, .25), (-.04, .12), (-.15, .01)],
        [(-.10, -.02), (-.36, .07), (-.66, .48), (-.56, .50),
         (-.39, .36), (-.43, .62), (-.31, .57), (-.25, .36),
         (-.17, .56), (-.08, .48), (-.04, .21), (.08, .10)],
        [(.0, .02), (.15, .30), (.13, .65), (.23, .59),
         (.33, .35), (.30, .08), (.07, -.14)],
        [(-.04, -.05), (-.35, -.23), (-.61, -.22), (-.41, -.12),
         (-.53, -.08), (-.30, -.08), (-.17, .02)],
    ]
    for index, shape in enumerate(shapes):
        sc6_flat_shape('WhiteBird' + str(index),
             [(x + a * size, y + b * size) for a, b in shape],
             cloth=cloth, back=back)
    sc6_flat_shape('RedSailAccent',
         [(x + a * size, y + b * size) for a, b in
          ((.38, -.17), (.59, .13), (.46, .46), (.40, .32), (.44, .08), (.20, -.16))],
         'SignRed', cloth=cloth, back=back)


def sc6_cross(name, x, y, size, width, height, material='SignRed'):
    outline = [(-.13, -.5), (.13, -.5), (.13, -.13), (.5, -.13),
               (.5, .13), (.13, .13), (.13, .5), (-.13, .5),
               (-.13, .13), (-.5, .13), (-.5, -.13), (-.13, -.13)]
    sc6_flat_shape(name, [(x + a * size, y + b * size) for a, b in outline],
                   material, cloth=(width, height))


def sc6_rifle(x, y, sign, width, height):
    # Hand-cut crossed rifle silhouettes echo the weapons stall banner.
    outline = [(-.50, -.035), (-.33, -.035), (-.21, -.09), (-.12, -.09),
               (-.12, -.035), (.06, -.035), (.06, -.14), (.13, -.14),
               (.13, -.03), (.51, -.03), (.51, .018), (.23, .018),
               (.23, .045), (.02, .045), (-.09, .09), (-.18, .09),
               (-.20, .035), (-.45, .035), (-.48, .075), (-.55, .075)]
    angle = sign * .53
    cs, sn = math.cos(angle), math.sin(angle)
    sc6_flat_shape('PaintedCrossedRifle',
         [(x + a * cs - b * sn, y + a * sn + b * cs) for a, b in outline],
         cloth=(width, height))


begin('KH_KursAtriumBanner',
      'Cloth bottom centre; 4.5 x 6 m; folded blue fabric with double-sided white Kurs wordmark; suspension top Y 6.5')
sc6_cloth(4.5, 6, 'CanvasBlue', big=True)
for back in (False, True):
    sc6_bird(.05, 4.05, 2.65, (4.5, 6), back)
    sc6_ink('KursWordmarkBack' if back else 'KursWordmark', 'Курс',
            0, 2.48, 1.13, 4.04, 4.5, 6, back=back)
    sc6_ink('KursSubtitleBack' if back else 'KursSubtitle', 'Торговый центр',
            0, 1.41, .35, 3.94, 4.5, 6, back=back)
    # A subtle painted rule joins the sign's typography to the handmade cloth.
    sc6_flat_shape('BannerPaintedUnderline',
          [(-1.94, .88), (-1.18, .93), (.37, .9), (1.89, .94),
           (1.9, .91), (.38, .86), (-1.16, .89), (-1.94, .85)],
          cloth=(4.5, 6), back=back)


begin('KH_SignWeapons', 'Cloth bottom centre; 4 x .95 m; front -Z; weapons shop banner')
sc6_cloth(4, .95, 'CanvasDark')
sc6_rifle(-1.37, .46, -1, 4, .95)
sc6_rifle(-1.37, .46, 1, 4, .95)
sc6_ink('WeaponsPaintedName', 'ОРУЖИЕ', .44, .46, .45, 2.45, 4, .95)


begin('KH_SignMedicalTrade',
      'Cloth bottom centre; 4 x 1.3 m; front -Z; medicines, provisions and barter banner')
sc6_cloth(4, 1.3, 'CanvasBeige')
sc6_cross('MedicalTradeRedCross', -1.48, .67, .72, 4, 1.3)
for i, value in enumerate(('МЕДИКАМЕНТЫ', 'ПРОДУКТЫ', 'ОБМЕН')):
    sc6_ink('MedicalTradeLine' + str(i), value, .38, 1.02 - i * .35,
            .225, 2.75, 4, 1.3, 'InkDark')


begin('KH_SignGear', 'Cloth bottom centre; 4 x 1.25 m; front -Z; packs and tools banner')
sc6_cloth(4, 1.25, 'CanvasDark')
# A backpack emblem, assembled as closed shallow embossed paint regions.
sc6_flat_shape('BackpackOutline', [(-1.68, .30), (-1.68, .77), (-1.60, .88),
      (-1.21, .88), (-1.13, .77), (-1.13, .30)], cloth=(4, 1.25))
sc6_flat_shape('BackpackPocket', [(-1.59, .36), (-1.22, .36), (-1.22, .56),
      (-1.59, .56)], 'PlasticDark', depth=.02, cloth=(4, 1.25))
for x in (-1.61, -1.22):
    sc6_flat_shape('BackpackStrap', [(x, .42), (x + .035, .42),
          (x + .035, .80), (x, .80)], 'SteelOxide', depth=.022, cloth=(4, 1.25))
for i, value in enumerate(('СНАРЯЖЕНИЕ', 'РЮКЗАКИ', 'ИНСТРУМЕНТЫ')):
    sc6_ink('GearPaintedLine' + str(i), value, .37, .98 - i * .34,
            .23, 2.76, 4, 1.25)


begin('KH_SignRepair', 'Cloth bottom centre; 3 x 1.5 m; front -Z; repair stall banner')
sc6_cloth(3, 1.5, 'CanvasDark')
sc6_ink('RepairHeading', 'РЕМОНТ', 0, 1.18, .32, 2.72, 3, 1.5)
for i, value in enumerate(('СНАРЯЖЕНИЕ', 'ОДЕЖДА  ОБУВЬ', 'ИНСТРУМЕНТЫ')):
    sc6_ink('RepairLine' + str(i), value, 0, .81 - i * .28,
            .19, 2.70, 3, 1.5)


begin('KH_CheckpointHeader',
      'Cloth bottom centre; 6 x 1.4 m; front -Z; checkpoint main entrance banner')
sc6_cloth(6, 1.4, 'CanvasDark')
sc6_bird(-2.10, .54, 1.02, (6, 1.4))
sc6_ink('CheckpointMainEntrance', 'ГЛАВНЫЙ ВХОД', .53, .96, .36, 4.43, 6, 1.4)
sc6_ink('CheckpointHeaderLine', 'КОНТРОЛЬНЫЙ ПУНКТ', .53, .48, .31, 4.43, 6, 1.4)


begin('KH_CheckpointRules',
      'Cloth bottom centre; 2.6 x 1.6 m; front -Z; inspection and visitor rules')
sc6_cloth(2.6, 1.6, 'CanvasDark')
for i, value in enumerate(('ПРОВЕРКА ВЕЩЕЙ', 'РЕГИСТРАЦИЯ', 'СОБЛЮДАЙТЕ ПОРЯДОК')):
    y = 1.24 - i * .44
    # Checkbox and irregular tick shape, printed on the same fabric.
    x = -1.05
    for a, b in (((x - .055, y - .055), (x + .055, y - .055)),
                 ((x + .055, y - .055), (x + .055, y + .055)),
                 ((x + .055, y + .055), (x - .055, y + .055)),
                 ((x - .055, y + .055), (x - .055, y - .055))):
        beam('RulesCheckbox', (a[0], a[1], -.02), (b[0], b[1], -.02),
             .009, 'ClothInk')
    sc6_flat_shape('RulesTick', [(x - .035, y), (x - .012, y - .024),
          (x + .055, y + .052), (x + .065, y + .04), (x - .01, y - .045),
          (x - .045, y - .012)], cloth=(2.6, 1.6))
    sc6_ink('InspectionRule' + str(i), value, .15, y, .165, 1.96, 2.6, 1.6)


begin('KH_MetalDetector',
      'Floor centre; 1.6 x 2.6 x .50 m; central passage 1.22 m wide, 2.36 m high; static inspection portal')
for side in (-1, 1):
    x = side * .705
    box('DetectorUpright', (x, 1.20, 0), (.19, 2.4, .50), 'Enamel', True)
    box('DetectorOuterTrim', (x + side * .103, 1.20, 0), (.025, 2.4, .48), 'Frame')
    box('DetectorRubberFoot', (x + side * .035, .035, 0), (.27, .07, .66), 'PlasticDark', True)
    for y in (.40, .93, 1.46, 1.99):
        box('DetectorSensorInset', (x, y, -.256), (.11, .095, .012), 'PlasticDark')
    for y in (.12, 2.28):
        for z in (-.17, .17):
            box('DetectorFastener', (x - side * .103, y, z), (.012, .025, .025), 'SteelOxide')
box('DetectorOverheadUnit', (0, 2.48, 0), (1.6, .24, .50), 'Enamel', True)
box('DetectorBlackScreen', (0, 2.48, -.257), (.65, .12, .02), 'PlasticDark')
for x in (-.31, .31):
    box('DetectorStatusLamp', (x, 2.48, -.271), (.028, .036, .014), 'Ember')
for i in range(5):
    box('DetectorIndicatorSegment', (-.18 + i * .055, 2.48, -.271),
        (.035, .021, .014), 'LampGlass')
module_text('DetectorSmallLabel', 'КОНТРОЛЬ', (.0, 2.56, -.262), .036, .60, 'Frame')


begin('KH_SecurityFence',
      'Floor centre; 2.5 x 2.5 x .16 m checkpoint wire fence; physical posts and welded mesh')
for x in (-1.2, 1.2):
    box('FencePost', (x, 1.25, 0), (.10, 2.5, .13), 'Frame', True)
    box('FenceFoot', (x, .025, 0), (.28, .05, .40), 'SteelOxide', True)
for y in (.15, 1.19, 2.45):
    box('FenceCrossFrame', (0, y, 0), (2.4, .06, .10), 'Frame', True)
box('FenceKickSheet', (0, .35, .028), (2.28, .40, .025), 'SteelOxide', True)
for i in range(16):
    x = -1.125 + .15 * i
    box('FenceThinUprightWire', (x, 1.49, 0), (.012, 1.84, .012), 'Frame', True)
for i in range(13):
    y = .61 + .145 * i
    box('FenceHorizontalWire', (0, y, -.008), (2.3, .012, .012), 'SteelOxide', True)
for x in (-1.06, 1.06):
    for y in (.19, .50, 1.22, 2.34):
        box('FencePanelBinding', (x, y, -.03), (.065, .025, .035), 'Copper')


begin('KH_StringLights',
      'Cable endpoint midpoint; 6 m span local X, endpoints Y 0; sag .45 m; eight bulbs to Y -.75; no realtime lights')
sc6_points = [(-3 + 6 * i / 36, -.45 * math.sin(math.pi * i / 36),
                .025 * math.sin(i * math.pi / 18)) for i in range(37)]
tube('FestoonInsulatedWire', sc6_points, .012, 'PlasticDark', sides=6)
for i in range(8):
    x = -2.65 + i * 5.3 / 7
    y = -.45 * math.sin(math.pi * (x + 3) / 6)
    drop = .11 + .035 * math.sin(i * 1.71)
    tube('PendantFlexibleLead', [(x, y, 0), (x + .018, y - drop, .008)],
         .008, 'PlasticDark', sides=6)
    # Screw sockets, metal caps and faceted rounded glass are distinct shared
    # materials. Bulbs glow via emission; the scene uses a few bounded lights.
    box('FestoonSocket', (x + .018, y - drop - .035, .008),
        (.067, .07, .067), 'PlasticDark')
    box('FestoonCopperCollar', (x + .018, y - drop - .072, .008),
        (.062, .023, .062), 'Copper')
    cx, cy, cz = x + .018, y - drop - .135, .008
    n = 10
    rings = [(-.088, .015), (-.065, .052), (-.015, .065), (.037, .052), (.065, .026)]
    vs = [(cx + r * math.cos(j * math.tau / n), cy + yy,
           cz + r * math.sin(j * math.tau / n)) for yy, r in rings for j in range(n)]
    fs = [tuple(range(n - 1, -1, -1)),
          tuple((len(rings) - 1) * n + j for j in range(n))]
    for r in range(len(rings) - 1):
        for j in range(n):
            a, b = r * n + j, r * n + (j + 1) % n
            fs.append((a, b, b + n, a + n))
    glass = mesh('FestoonWarmBulb', vs, fs, 'LampGlass')
    for face in glass.data.polygons:
        face.use_smooth = len(face.vertices) == 4
for x in (-3, 3):
    box('FestoonEndTie', (x, 0, .025), (.075, .045, .10), 'Copper')

