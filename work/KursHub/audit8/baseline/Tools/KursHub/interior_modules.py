"""Stage 5 architecture appended by build_hub_props.py inside Blender.

All dimensions use Unity metres. Width is local X, height is local Y and the
front faces local -Z. Every origin is bottom-centre on the wall/sign plane.
Doorways contain no glass, threshold or collision proxy. Decorative sign text
is a closed extruded mesh, so the library's solid-volume checks also cover it.
"""

_font_path = pathlib.Path('C:/Windows/Fonts/arialbd.ttf')
assert _font_path.is_file(), 'Arial Bold Cyrillic font is required for Kurs signs'
_interior_font = bpy.data.fonts.load(str(_font_path), check_existing=True)


def module_text(name, value, center, height, max_width, material='Lettering'):
    """Fit Arial Bold text, preserving aspect ratio and a fixed 8 mm depth."""
    curve = bpy.data.curves.new(name + '_Type', 'FONT')
    curve.body = value
    curve.font = _interior_font
    curve.size = 1
    curve.align_x = 'CENTER'
    curve.fill_mode = 'BOTH'
    curve.resolution_u = 4
    curve.extrude = .006
    obj = bpy.data.objects.new(name + '_Type', curve)
    current['collection'].objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target='MESH')
    obj = bpy.context.object
    # Curve caps share coincident contour vertices after conversion. Weld them
    # before registering the closed solid with the normal export pipeline.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.000001)
    # Arial's cap tessellation includes collinear sliver triangles on straight
    # strokes. Dissolve their internal edges before scaling small sign text;
    # keep the contour walls and let the strict solid validator check the result.
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=.000001)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    points = [vertex.co.copy() for vertex in obj.data.vertices]
    assert points, 'Empty Cyrillic text: ' + value
    lo = [min(point[i] for point in points) for i in range(3)]
    hi = [max(point[i] for point in points) for i in range(3)]
    factor = min(height / (hi[1] - lo[1]), max_width / (hi[0] - lo[0]))
    x, y, z = center
    vertices = [(x + (point.x - (lo[0] + hi[0]) / 2) * factor,
                 y + (point.y - (lo[1] + hi[1]) / 2) * factor,
                 z - .008 * (point.z - lo[2]) / (hi[2] - lo[2]))
                for point in points]
    faces = [tuple(polygon.vertices) for polygon in obj.data.polygons]
    data = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.meshes.remove(data)
    result = mesh(name, vertices, faces, material)
    bm = bmesh.new()
    bm.from_mesh(result.data)
    # Single-precision vertex storage can flatten a remaining near-collinear
    # cap triangle only after the final placement transform. Clean that result.
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=.00001)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(result.data)
    bm.free()
    result.data.update()
    return result


def module_arrow(name, center, material='Lettering'):
    """A right-pointing solid arrow with a simple unambiguous silhouette."""
    x, y, z = center
    outline = [(-.26, -.045), (.06, -.045), (.06, -.12),
               (.28, 0), (.06, .12), (.06, .045), (-.26, .045)]
    count = len(outline)
    vertices = [(x + a, y + b, z + depth) for depth in (-.005, .005)
                for a, b in outline]
    faces = [tuple(range(count - 1, -1, -1)), tuple(range(count, 2 * count))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count)
              for i in range(count)]
    return mesh(name, vertices, faces, material)


def perimeter_frame(width, height, depth=.18, material='Frame'):
    for x in (-width / 2 + .04, width / 2 - .04):
        box('ModuleSidePost', (x, height / 2, 0), (.08, height, depth),
            material, collider=True)
    for y in (.04, height - .04):
        box('ModuleHorizontalRail', (0, y, 0), (width, .08, depth),
            material, collider=True)


begin('KH_Partition4',
      'Floor bottom centre; front -Z; 4 x 3.7 x .18 m opaque cream partition')
box('CreamWallPanel', (0, 1.85, 0), (3.92, 3.62, .12),
    'CreamPanel', collider=True)
perimeter_frame(4, 3.7)
box('PanelCentralSeam', (0, 1.85, -.066), (.035, 3.54, .012), 'Concrete')
box('PanelKickPlate', (0, .16, -.067), (3.84, .24, .014), 'Frame')


begin('KH_Shopfront6',
      'Floor bottom centre; front -Z; 6 x 3.7 m shopfront; clear portal X +/-1.2, Y 0..2.8')
# Jamb inner faces define the requested 2.4 m unobstructed opening.
for side in (-1, 1):
    for x in (side * 2.96, side * 1.24):
        box('ShopWindowJamb', (x, 1.4, 0), (.08, 2.8, .18),
            'Frame', collider=True)
    box('ShopWindowGlass', (side * 2.10, 1.47, 0), (1.64, 2.58, .035),
        'Glass', collider=True)
    box('ShopWindowSill', (side * 2.10, .09, 0), (1.72, .18, .18),
        'CreamPanel', collider=True)
    box('ShopWindowTransom', (side * 2.10, 2.76, 0), (1.72, .08, .18),
        'Frame', collider=True)
box('ShopBlueFascia', (0, 3.25, 0), (6, .90, .18),
    'MallBlue', collider=True)
box('ShopFasciaBottomTrim', (0, 2.83, -.097), (6, .06, .014), 'Frame')
box('ShopFasciaTopTrim', (0, 3.67, -.097), (6, .06, .014), 'Frame')
module_text('ShopUnitLabel', 'ПОМЕЩЕНИЕ', (0, 3.26, -.103), .31, 4.6)


begin('KH_DoorPartition4',
      'Floor bottom centre; front -Z; 4 x 3.7 x .18 m wall; clear portal X +/- .8, Y 0..2.6')
for side in (-1, 1):
    box('DoorSidePanel', (side * 1.40, 1.85, 0), (1.20, 3.70, .12),
        'CreamPanel', collider=True)
    box('DoorOuterEdge', (side * 1.96, 1.85, 0), (.08, 3.70, .18),
        'Frame', collider=True)
    box('DoorInnerJamb', (side * .84, 1.30, 0), (.08, 2.60, .18),
        'Frame', collider=True)
box('DoorUpperPanel', (0, 3.15, 0), (1.60, 1.10, .12),
    'CreamPanel', collider=True)
box('DoorLintel', (0, 2.64, 0), (1.76, .08, .18), 'Frame', collider=True)
box('DoorTopRail', (0, 3.66, 0), (4, .08, .18), 'Frame', collider=True)


begin('KH_MeshPartition4',
      'Floor bottom centre; front -Z; 4 x 3.7 x .18 m steel grid with .6 m kick panel; no doorway')
perimeter_frame(4, 3.7)
box('GridKickPanel', (0, .34, 0), (3.84, .52, .10),
    'CreamPanel', collider=True)
box('GridKickRail', (0, .62, 0), (3.84, .06, .15), 'Frame', collider=True)
for index in range(19):
    x = -1.8 + .2 * index
    box('GridVertical', (x, 2.135, 0), (.018, 2.97, .025),
        'Frame', collider=True)
for index in range(15):
    y = .76 + .2 * index
    box('GridHorizontal', (0, y, 0), (3.84, .018, .025),
        'Frame', collider=True)


begin('KH_DirectionBoard',
      'Sign bottom centre; front -Z; 3.6 x 1.8 m board; suspension top Y 2.15 m; no collision')
box('DirectionBackplate', (0, .90, 0), (3.60, 1.80, .06), 'Frame')
for x in (-1.5, 1.5):
    box('DirectionSuspensionRod', (x, 1.975, 0), (.035, .35, .035), 'SteelOxide')
    box('DirectionCeilingPlate', (x, 2.14, 0), (.12, .02, .10), 'SteelOxide')
box('DirectionHeader', (0, 1.57, -.034), (3.50, .36, .012), 'MallBlue')
module_text('DirectionTitle', 'ТЦ КУРС', (0, 1.58, -.047), .19, 2.7)
for index, value in enumerate(('ТОРГОВЛЯ', 'МАСТЕРСКИЕ', 'ЖИЛЫЕ ЗОНЫ')):
    y = 1.18 - .44 * index
    module_text('DirectionLabel' + str(index), value, (-.35, y, -.035), .18, 2.35)
    module_arrow('DirectionArrow' + str(index), (1.32, y, -.043))
    if index < 2:
        box('DirectionRowDivider', (0, y - .22, -.036), (3.34, .012, .01),
            'CreamPanel')
for x in (-1.67, 1.67):
    for y in (.09, 1.71):
        box('DirectionMountBolt', (x, y, -.039), (.035, .035, .018), 'SteelOxide')


begin('KH_MedicalSign',
      'Sign bottom centre; front -Z; 2.4 x .9 m wall board; no collision')
box('MedicalBackplate', (0, .45, 0), (2.40, .90, .06), 'CreamPanel')
for y in (.035, .865):
    box('MedicalBorder', (0, y, -.038), (2.40, .07, .016), 'MallBlue')
for x in (-1.165, 1.165):
    box('MedicalBorder', (x, .45, -.038), (.07, .90, .016), 'MallBlue')
box('MedicalCrossVertical', (-.82, .45, -.048), (.14, .49, .018), 'SignRed')
box('MedicalCrossHorizontal', (-.82, .45, -.048), (.49, .14, .018), 'SignRed')
module_text('MedicalLabel', 'МЕДПУНКТ', (.30, .46, -.047), .19, 1.55,
            material='Frame')
