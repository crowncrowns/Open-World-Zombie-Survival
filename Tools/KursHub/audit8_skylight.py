"""Controlled revision-8 lantern pass; keep every existing shell FBX Model UID.

Blender only. ``apply(root, work)`` accepts a staging root under work/KursHub/
audit8 and changes the central hipped Skylight only. FrontDome, roof walkways,
guards and every unrelated mesh remain present. Four original thick glass
solids become separate closed 12-mm panes; existing frame objects are raised to
the new roof pitch and gain a white triangulated support system. New steel is
packed into existing frame chunks, so no mesh node/local-ID migration is needed.

PBR families expected from the revision-8 texture generator:
Hub8SkylightGlass: 2K, alpha ~0.12-0.18, clear centre, edge grime, no shadow pass;
Hub8SkylightFrame: 2K, off-white painted steel, physical UV, non-emissive.
Both use BaseColor/Normal/Roughness/Metallic/Occlusion/Height/MetallicSmoothness.
When maps are not yet present the native source keeps explicit useful Principled
defaults; run the normal material-packing pass after generating those maps.
"""
from pathlib import Path
from collections import defaultdict
import argparse
import hashlib
import json
import math
import shutil
import sys

import bpy
import bmesh
from mathutils import Vector, Matrix

PROJECT = Path(__file__).resolve().parents[2]
HUB = Path("Assets/Environment/KursHub")
FAMILIES = ("Hub8SkylightGlass", "Hub8SkylightFrame")
MAPS = ("BaseColor", "Normal", "Roughness", "Metallic", "Occlusion",
        "Height", "MetallicSmoothness")
EAVES, RIDGE, THICKNESS = 11., 13.4, .012
BOX = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
       (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]


def _sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _json(path, data):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n",
                          encoding="utf8")


def _guard(root):
    root = Path(root).resolve()
    staging = (PROJECT / "work/KursHub/audit8").resolve()
    assert root != PROJECT and root.is_relative_to(staging), root
    assert (root / HUB / "KursHubManifest.json").is_file(), root
    return root


def _xyz(point):
    return Vector((point[0], point[2], point[1]))


def _height(x, z):
    return EAVES + (RIDGE - EAVES) * min(
        1 - abs(x) / 15, (z + 32) / 8, (18 - z) / 8)


def _material(root, family):
    material = bpy.data.materials.get(family) or bpy.data.materials.new(family)
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    glass = family == FAMILIES[0]
    color = (.75, .85, .92, .15) if glass else (.86, .89, .88, 1)
    material.diffuse_color = color
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = .13 if glass else .54
    bsdf.inputs["Metallic"].default_value = 0 if glass else .08
    bsdf.inputs["Alpha"].default_value = .15 if glass else 1
    bsdf.inputs["IOR"].default_value = 1.45
    bsdf.inputs["Transmission Weight"].default_value = .25 if glass else 0
    material.surface_render_method = 'DITHERED'
    loaded = {}
    for suffix in MAPS:
        if suffix == "MetallicSmoothness":
            continue
        path = root / HUB / "Textures" / (family + "_" + suffix + ".png")
        if not path.exists():
            continue
        image = bpy.data.images.load(str(path), check_existing=True)
        image.colorspace_settings.name = "sRGB" if suffix == "BaseColor" else "Non-Color"
        image.pack()
        node = nodes.new("ShaderNodeTexImage")
        node.image = image
        node.extension = 'REPEAT'
        loaded[suffix] = node
    for suffix, socket in [("BaseColor", "Base Color"), ("Roughness", "Roughness"),
                           ("Metallic", "Metallic")]:
        if suffix in loaded:
            links.new(loaded[suffix].outputs["Color"], bsdf.inputs[socket])
    if glass and "BaseColor" in loaded:
        links.new(loaded["BaseColor"].outputs["Alpha"], bsdf.inputs["Alpha"])
    if "Normal" in loaded:
        normal = nodes.new("ShaderNodeNormalMap")
        normal.inputs["Strength"].default_value = .12 if glass else .35
        links.new(loaded["Normal"].outputs["Color"], normal.inputs["Color"])
        links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])
    material["KursPBRFamily"] = family
    material["KursSurfaceVersion"] = 8
    material["KursPhysicalUVPeriodMetres"] = 2.
    material["Hub8TextureMapsPending"] = len(loaded) != 6
    return material


def _mesh(name, vertices, faces, material):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    layer = mesh.uv_layers.new(name="PhysicalUV")
    for polygon in mesh.polygons:
        axis = max(range(3), key=lambda i: abs(polygon.normal[i]))
        uvaxes = ((1, 2), (0, 2), (0, 1))[axis]
        for li in polygon.loop_indices:
            p = mesh.vertices[mesh.loops[li].vertex_index].co
            layer.data[li].uv = (p[uvaxes[0]] / 2, p[uvaxes[1]] / 2)
    mesh.materials.append(material)
    return mesh


def _assert_geometry(mesh):
    from audit7_verify_props import geometry
    data = geometry(mesh)
    assert not any(data[k] for k in ("nonManifoldEdges", "degenerateFaces",
                   "invalidFaceNormals", "nonPositiveSolids")), (mesh.name, data)
    assert data["signedVolume"] > 0 and data["finiteUV"] and data["uvLayers"], data
    return data


def _clip(poly, axis, value, keep_larger):
    result = []
    for a, b in zip(poly, poly[1:] + poly[:1]):
        da, db = a[axis] - value, b[axis] - value
        ina = da >= -1e-9 if keep_larger else da <= 1e-9
        inb = db >= -1e-9 if keep_larger else db <= 1e-9
        if ina:
            result.append(a)
        if ina != inb:
            t = da / (da - db)
            result.append(tuple(a[i] + (b[i] - a[i]) * t for i in range(2)))
    clean = []
    for p in result:
        if not clean or math.dist(clean[-1], p) > 1e-7:
            clean.append(p)
    if len(clean) > 1 and math.dist(clean[0], clean[-1]) < 1e-7:
        clean.pop()
    return clean


def _panes(name, polygon, material):
    vertices, faces = [], []
    count = 0
    zcuts = [-32, -28, -24, -20, -16, -12, -8, -4, 0, 4, 8, 10, 14, 18]
    for x0 in range(-15, 15, 3):
        for z0, z1 in zip(zcuts, zcuts[1:]):
            p = polygon[:]
            for axis, value, larger in [(0, x0, True), (0, x0 + 3, False),
                                         (1, z0, True), (1, z1, False)]:
                if p:
                    p = _clip(p, axis, value, larger)
            if len(p) < 3:
                continue
            # Each manufactured pane has an actual narrow movement joint.
            cx = sum(v[0] for v in p) / len(p)
            cz = sum(v[1] for v in p) / len(p)
            scale = 1 - .024 / max(.05, min(max(v[0] for v in p) - min(v[0] for v in p),
                                             max(v[1] for v in p) - min(v[1] for v in p)))
            p = [(cx + (x - cx) * scale, cz + (z - cz) * scale) for x, z in p]
            xyz = [_xyz((x, _height(x, z), z)) for x, z in p]
            normal = (xyz[1] - xyz[0]).cross(xyz[2] - xyz[0]).normalized()
            if normal.z < 0:
                normal = -normal
            n = len(p)
            offset = len(vertices)
            vertices.extend(tuple(v - normal * THICKNESS / 2) for v in xyz)
            vertices.extend(tuple(v + normal * THICKNESS / 2) for v in xyz)
            faces.extend([tuple(offset + i for i in reversed(range(n))),
                          tuple(offset + n + i for i in range(n))])
            faces.extend((offset + i, offset + (i + 1) % n,
                          offset + n + (i + 1) % n, offset + n + i)
                         for i in range(n))
            count += 1
    mesh = _mesh(name, vertices, faces, material)
    _assert_geometry(mesh)
    return mesh, count


def _beam_mesh(name, a, b, width, depth, material):
    a, b = _xyz(a), _xyz(b)
    u = (b - a).normalized()
    ref = Vector((0, 0, 1)) if abs(u.z) < .95 else Vector((0, 1, 0))
    v = u.cross(ref).normalized()
    w = u.cross(v).normalized()
    c = (a + b) / 2
    length = (b - a).length
    vertices = [c + u * i * length / 2 + v * j * width / 2 + w * k * depth / 2
                for i, j, k in [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
                                 (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]]
    return _mesh(name, vertices, BOX, material)


def _replace(obj, mesh, bevel=0):
    assert obj.matrix_world == Matrix.Identity(4), (obj.name, "Nonidentity source")
    obj.data = mesh
    for modifier in list(obj.modifiers):
        obj.modifiers.remove(modifier)
    if bevel:
        mod = obj.modifiers.new("Hub8 manufactured edge radius", 'BEVEL')
        mod.width = bevel
        mod.segments = 3
        mod.harden_normals = True
        mod.limit_method = 'ANGLE'
        normal = obj.modifiers.new("Hub8 steel weighted normals", 'WEIGHTED_NORMAL')
        normal.keep_sharp = True
    obj["Hub8SkylightConstruction"] = True
    _assert_geometry(mesh)


def _source(root, work, manifest):
    from audit7_soft_shading import native_geometry_hash
    source = root / "Sources/KursHub/KursHub.blend"
    bpy.ops.wm.open_mainfile(filepath=str(source))
    assert not bpy.context.scene.get("Hub8SkylightRevision"), "Use a fresh staging source"
    targets = [o for o in bpy.data.objects if o.type == 'MESH' and any(
        c.name in ('Skylight', 'SkylightDetails') for c in o.users_collection)
        and o.data.materials[0].name in ('Glass', 'Frame')]
    target_names = {o.name for o in targets}
    assert len(targets) == 143, (len(targets), sorted(target_names))
    old_names = set(bpy.data.objects.keys())
    unaffected = {o.name: (native_geometry_hash(o.data), list(sum((list(row) for row in o.matrix_world), [])))
                  for o in bpy.data.objects if o.type == 'MESH' and o.name not in target_names}
    original_chunks = {(c['group'], c['material'], *c['region'], c['collider'], c.get('hidden', False)): c['name']
                       for c in manifest['chunks']}
    groups = defaultdict(list)
    for obj in targets:
        c = sum((v.co for v in obj.data.vertices), Vector()) / len(obj.data.vertices)
        key = (obj.get('construction_group') or obj.users_collection[0].name,
               obj.data.materials[0].name, math.floor(c.x / 24), math.floor(c.y / 30),
               bool(obj.get('unity_collision')), bool(obj.get('hidden_collision_proxy')))
        assert key in original_chunks, (obj.name, key)
        chunk = original_chunks[key]
        groups[chunk].append(obj)
        obj['Hub8ExportChunk'] = chunk
    expected = {c['name'] for c in manifest['chunks'] if c['group'] in ('Skylight', 'SkylightDetails')
                and c['material'] in ('Glass', 'Frame')}
    assert set(groups) == expected, (set(groups), expected)
    glass = _material(root, FAMILIES[0])
    steel = _material(root, FAMILIES[1])
    pane_specs = {
        'SkylightGlass': [(-15, -32), (15, -32), (0, -24)],
        'SkylightGlass.001': [(15, -32), (15, 18), (0, 10), (0, -24)],
        'SkylightGlass.002': [(15, 18), (-15, 18), (0, 10)],
        'SkylightGlass.003': [(-15, 18), (-15, -32), (0, -24), (0, 10)]}
    panes = []
    for name, poly in pane_specs.items():
        mesh, count = _panes(name + ' 12mm glazed panes', poly, glass)
        _replace(bpy.data.objects[name], mesh)
        panes.append(dict(name=name, panes=count, thicknessMetres=THICKNESS))
    for obj in targets:
        if obj.name in pane_specs:
            continue
        obj.data.materials[0] = steel
        if obj.name.startswith('SkylightRib'):
            # The original beams are horizontal in Z and retain their names.
            lo = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
            hi = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
            x = -15. if (lo[0] + hi[0]) / 2 < 0 else 15.
            z = (lo[1] + hi[1]) / 2
            _replace(obj, _beam_mesh(obj.name + ' raised roof rib',
                     (x, EAVES, z), (0, _height(0, z), z), .10, .15, steel), .003)
        elif obj.name.startswith('SkylightPurlin'):
            x = sum(v.co.x for v in obj.data.vertices) / len(obj.data.vertices)
            f = 1 - abs(x) / 15
            _replace(obj, _beam_mesh(obj.name + ' raised purlin',
                     (x, EAVES + (RIDGE-EAVES)*f, -32+8*f),
                     (x, EAVES + (RIDGE-EAVES)*f, 18-8*f), .07, .08, steel), .002)
        elif obj.name.startswith('SkylightRidge'):
            index = 0 if obj.name == 'SkylightRidge' else int(obj.name.rsplit('.', 1)[1])
            pairs = [((-15, EAVES, -32), (0, RIDGE, -24)),
                     ((15, EAVES, -32), (0, RIDGE, -24)),
                     ((15, EAVES, 18), (0, RIDGE, 10)),
                     ((-15, EAVES, 18), (0, RIDGE, 10)),
                     ((0, RIDGE, -24), (0, RIDGE, 10))]
            _replace(obj, _beam_mesh(obj.name + ' raised hip', *pairs[index], .16, .18, steel), .004)
        else:
            # Roof safety posts/bars stay exactly where the accepted route uses them.
            obj['Hub8SkylightConstruction'] = True
    collection = bpy.data.collections.new('Hub8 central skylight white steel')
    bpy.context.scene.collection.children.link(collection)
    new_objects = []
    detail_chunks = [c for c in manifest['chunks'] if c['group'] == 'SkylightDetails' and c['material'] == 'Frame']
    assert len(detail_chunks) == 2
    def add(name, a, b, width, depth, max_lod=2):
        centre_x = (a[0] + b[0]) / 2
        chunk = min(detail_chunks, key=lambda c: abs((c['region'][0] + .5) * 24 - centre_x))['name']
        obj = bpy.data.objects.new(name, _beam_mesh(name, a, b, width, depth, steel))
        collection.objects.link(obj)
        _replace(obj, obj.data, min(.003, width * .05))
        obj['unity_collision'] = False
        obj['construction_group'] = 'SkylightDetails'
        obj['Hub8ExportChunk'] = chunk
        obj['Hub8SkylightMaxLOD'] = max_lod
        groups[chunk].append(obj)
        new_objects.append(obj)
        return obj
    # Manufacturing mullions follow the actual 3-m pane grid; hip ends stop at
    # the intersecting roof planes, rather than cutting through other glazing.
    for x in (-12, -9, -6, -3, 3, 6, 9, 12):
        f = 1-abs(x)/15
        y = EAVES+(RIDGE-EAVES)*f
        add('Hub8 glazing longitudinal mullion '+str(x), (x,y,-32+8*f), (x,y,18-8*f), .055,.065)
    # Six triangulated load-bearing trusses are visible from both inhabited
    # floors. Lowest steel remains >4.5 m above the second-floor walking plane.
    for zi, z in enumerate((-24, -16, -8, 0, 8, 10)):
        for x in range(-15, 15, 5):
            add(f'Hub8 truss {zi} lower chord {x}', (x,10.2,z), (x+5,10.2,z), .16,.14)
            top0 = _height(x,z)-.11
            top1 = _height(x+5,z)-.11
            add(f'Hub8 truss {zi} top chord {x}', (x,top0,z), (x+5,top1,z), .14,.18)
            add(f'Hub8 truss {zi} web {x}', (x,10.27,z), (x+5,top1-.07,z), .075,.075)
        for x in range(-10,15,5):
            add(f'Hub8 truss {zi} king post {x}', (x,10.27,z), (x,_height(x,z)-.20,z), .08,.08)
        for side in (-1,1):
            x=side*14.78
            add(f'Hub8 truss {zi} bearing plate {side}', (x,10.04,z), (x,10.32,z), .28,.045)
            for bx in (-.07,.07):
                add(f'Hub8 truss {zi} bearing fastener {side} {bx}',
                    (x+bx,10.19,z-.04),(x+bx,10.19,z+.04),.028,.028,0)
    bpy.context.view_layer.update()
    assert old_names <= set(bpy.data.objects.keys()), 'An original source object disappeared'
    after_unaffected = {o.name: (native_geometry_hash(o.data), list(sum((list(row) for row in o.matrix_world), [])))
                        for o in bpy.data.objects if o.name in unaffected}
    assert unaffected == after_unaffected, 'Unrelated source geometry/UV/material/transform changed'
    bpy.context.scene['Hub8SkylightRevision'] = 8
    bpy.context.scene['Hub8SkylightDescription'] = '12 mm separated panes; raised hipped ridge 13.4 m; six white triangulated trusses; roof safety guards retained'
    bpy.ops.file.pack_all()
    pending = work / 'KursHub.roof8.pending.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(pending))
    metrics=[]
    for obj in targets+new_objects:
        ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        me=ev.to_mesh(preserve_all_data_layers=True,depsgraph=bpy.context.evaluated_depsgraph_get())
        metrics.append(dict(name=obj.name,**_assert_geometry(me)))
        ev.to_mesh_clear()
    lod_meshes=[]
    for level in (0,1,2):
        if level==1:
            for obj in targets+new_objects:
                for mod in obj.modifiers:
                    if mod.type=='BEVEL':mod.segments=1
        bpy.context.view_layer.update()
        deps=bpy.context.evaluated_depsgraph_get()
        output={}
        for name,items in groups.items():
            vertices,faces,uvs,normals,smooth=[],[],[],[],[]
            for obj in items:
                if level>obj.get('Hub8SkylightMaxLOD',2):continue
                ev=obj.evaluated_get(deps)
                mesh=ev.to_mesh(preserve_all_data_layers=True,depsgraph=deps) if level<2 else obj.data
                offset=len(vertices)
                vertices.extend(tuple(obj.matrix_world@v.co) for v in mesh.vertices)
                layer=mesh.uv_layers.active
                normal_matrix=obj.matrix_world.to_3x3().inverted().transposed()
                for polygon in mesh.polygons:
                    faces.append(tuple(offset+i for i in polygon.vertices))
                    smooth.append(polygon.use_smooth)
                    for li in polygon.loop_indices:
                        uvs.append(tuple(layer.data[li].uv))
                        normals.append(tuple((normal_matrix@mesh.corner_normals[li].vector).normalized()))
                if level<2:ev.to_mesh_clear()
            mesh=bpy.data.meshes.new(name+' roof8 LOD'+str(level))
            mesh.from_pydata(vertices,[],faces);mesh.update()
            uv=mesh.uv_layers.new(name='PhysicalUV')
            for i,value in enumerate(uvs):uv.data[i].uv=value
            for polygon,value in zip(mesh.polygons,smooth):polygon.use_smooth=value
            mesh.normals_split_custom_set(normals)
            _assert_geometry(mesh)
            output[name]=mesh
        lod_meshes.append(output)
    return pending,lod_meshes,dict(existingSourceObjectsRetained=len(old_names),
            modifiedSourceObjects=len(targets),newSteelParts=len(new_objects),
            unchangedSourceMeshes=len(unaffected),panes=panes,sourceMetrics=metrics,
            allUnrelatedSourceGeometryUVMaterialsTransformsIdentical=True,
            roofGuardPositionsUnchanged=True,lowestTrussY=10.04,
            secondFloorHeadroomAtLowestSteel=10.04-5.5,
            pbrMapsPending={f:bool(bpy.data.materials[f].get('Hub8TextureMapsPending')) for f in FAMILIES})


def _patch_fbx(path, pending, replacements, chunk_materials):
    from audit7_soft_shading import child,clone,encoded_clone,tree_hash,parse_fbx,encode_bin
    from audit8_signs import _geometry_payload,_element
    parsed,version=parse_fbx.parse(str(path));tree=clone(parsed)
    objects=child(tree,b'Objects').elems
    connections=child(tree,b'Connections').elems
    models={o.props[0]:o for o in objects if o.id==b'Model'}
    before_models={uid:tree_hash(o) for uid,o in models.items()}
    geometries={}
    for g in (o for o in objects if o.id==b'Geometry'):
        links=[c.props[2] for c in connections if c.props[0]==b'OO' and c.props[1]==g.props[0] and c.props[2] in models]
        assert len(links)==1
        name=models[links[0]].props[1].split(b'\x00\x01')[0].decode()
        geometries[name]=g
    assert set(replacements)<=set(geometries),set(replacements)-set(geometries)
    unaffected={name:tree_hash(g) for name,g in geometries.items() if name not in replacements}
    before_geom_connections={tree_hash(c) for c in connections if c.props[0]==b'OO' and c.props[1] in {g.props[0] for g in geometries.values()}}
    materials={o.props[0]:o for o in objects if o.id==b'Material'}
    material_ids={}
    all_ids={o.props[0] for o in objects if o.props and isinstance(o.props[0],int)}
    for family in FAMILIES:
        existing=[o.props[0] for o in materials.values() if o.props[1].split(b'\x00\x01')[0].decode()==family]
        assert not existing,'Use a fresh staging FBX'
        uid=int.from_bytes(hashlib.sha256(('KursHub '+family).encode()).digest()[:8],'little')&((1<<63)-1)
        assert uid not in all_ids
        original=next(o for o in materials.values() if o.props[1].split(b'\x00\x01')[0] == (b'Glass' if family==FAMILIES[0] else b'Frame'))
        material=clone(original);material.props[0]=uid;material.props[1]=family.encode()+b'\x00\x01Material'
        for p in child(material,b'Properties70').elems:
            if p.id!=b'P':continue
            if p.props[0] in (b'DiffuseColor',b'Diffuse'):p.props[4:]=[.75,.85,.92] if family==FAMILIES[0] else [.86,.89,.88]
            if p.props[0] in (b'EmissiveColor',b'Emissive'):p.props[4:]=[0.,0.,0.]
            if p.props[0]==b'TransparencyFactor':p.props[4:]=[.85 if family==FAMILIES[0] else 0.]
            if p.props[0]==b'EmissiveFactor':p.props[4:]=[0.]
        objects.append(material);material_ids[family]=uid
    models_by_name={o.props[1].split(b'\x00\x01')[0].decode():o for o in models.values()}
    retargeted=[]
    for name,mesh in replacements.items():
        geometries[name].elems=_geometry_payload(mesh)
        model=models_by_name[name]
        old_links=[c for c in connections if c.props[0]==b'OO' and c.props[2]==model.props[0] and c.props[1] in materials]
        assert len(old_links)==1,(name,old_links)
        # Only the selected model's material link changes. Shared old Frame/Glass
        # materials and their textures remain intact for the dome and facades.
        old_links[0].props[1]=material_ids[chunk_materials[name]]
        retargeted.append(name)
    assert {uid:tree_hash(o) for uid,o in models.items()}==before_models,'Model UID/name/transform changed'
    assert unaffected=={name:tree_hash(geometries[name]) for name in unaffected},'Unrelated FBX geometry changed'
    after_geom_connections={tree_hash(c) for c in connections if c.props[0]==b'OO' and c.props[1] in {g.props[0] for g in geometries.values()}}
    assert before_geom_connections==after_geom_connections,'Geometry-to-model connection changed'
    assert tree_hash(child(parsed,b'GlobalSettings'))==tree_hash(child(tree,b'GlobalSettings'))
    encode_bin.write(str(pending),encoded_clone(tree),version)
    encoded,_=parse_fbx.parse(str(pending))
    encoded_objects=child(encoded,b'Objects').elems
    assert {o.props[0]:tree_hash(o) for o in encoded_objects if o.id==b'Model'}==before_models
    assert {o.props[0] for o in encoded_objects if o.id==b'Geometry'}=={g.props[0] for g in geometries.values()}
    encoded_by_uid={o.props[0]:o for o in encoded_objects if o.id==b'Geometry'}
    assert all(tree_hash(encoded_by_uid[geometries[name].props[0]])==value for name,value in unaffected.items())
    return dict(retainedAllModelUIDsNamesTransforms=True,existingModelCount=len(models),
                retainedAllGeometryUIDs=True,unrelatedGeometryChunksIdentical=len(unaffected),
                changedGeometryChunks=sorted(replacements),retargetedMaterialLinks=retargeted,
                geometryModelConnectionsIdentical=True,globalSettingsIdentical=True,
                encodedFileModelAndGeometryUIDsVerified=True)


def _verify_fbx(path, expected_names, chunk_materials):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert {o.name for o in objects}==set(expected_names), 'Imported shell chunk names changed'
    metrics=[]
    for obj in objects:
        data=_assert_geometry(obj.data)
        assert all(abs(s-1)<1e-5 for s in obj.scale),(obj.name,obj.scale)
        assert obj.matrix_world.determinant()>0
        if obj.name in chunk_materials:
            assert len(obj.data.materials)==1 and obj.data.materials[0].name==chunk_materials[obj.name],(obj.name,[m.name for m in obj.data.materials])
        metrics.append(dict(name=obj.name,**data))
    return dict(triangles=sum(d['triangles'] for d in metrics),chunks=len(metrics),metrics=metrics)


def apply(root, work):
    sys.path.insert(0,str(Path(__file__).parent))
    root=_guard(root);work=Path(work).resolve();work.mkdir(parents=True,exist_ok=True)
    assert work.is_relative_to((PROJECT/'work/KursHub/audit8').resolve()),work
    manifest_path=root/HUB/'KursHubManifest.json'
    manifest=json.loads(manifest_path.read_text(encoding='utf8'))
    source=root/'Sources/KursHub/KursHub.blend'
    models=[root/HUB/'Models'/('KursHubShell.fbx' if level==0 else f'LOD/KursHubShell_LOD{level}.fbx') for level in (0,1,2)]
    original_sha={str(p):_sha(p) for p in [manifest_path,source,*models]}
    for p in [manifest_path,source,*models]:
        backup=work/'before'/p.relative_to(root);backup.parent.mkdir(parents=True,exist_ok=True)
        assert not backup.exists(),'Proof backup exists; use a fresh work directory'
        shutil.copy2(p,backup)
    pending_source,lods,source_record=_source(root,work,manifest)
    chunk_materials={c['name']:(FAMILIES[0] if c['material']=='Glass' else FAMILIES[1])
                     for c in manifest['chunks'] if c['group'] in ('Skylight','SkylightDetails') and c['material'] in ('Glass','Frame')}
    records=[];pending_models=[]
    # Mesh payloads are serialized before factory reset discards native data.
    # Preserve standalone mesh datablocks through read_factory_settings.
    for group in lods:
        for mesh in group.values():mesh.use_fake_user=True
    # Patch all payloads while the editable source is still loaded.
    for level,path in enumerate(models):
        pending=work/(path.stem+'.roof8.pending.fbx')
        record=_patch_fbx(path,pending,lods[level],chunk_materials)
        pending_models.append(pending);records.append(dict(level=level,model=str(path.relative_to(root)),**record))
    last=None
    for level,pending in enumerate(pending_models):
        # LOD2 has no added quality-7-only mesh nodes; use exact original names.
        from audit7_soft_shading import child,parse_fbx
        parsed,_=parse_fbx.parse(str(models[level]))
        expected=[o.props[1].split(b'\x00\x01')[0].decode() for o in child(parsed,b'Objects').elems if o.id==b'Model' and o.props[2]==b'Mesh']
        check=_verify_fbx(pending,expected,chunk_materials)
        assert last is None or check['triangles']<last,'Shell LOD triangles must decrease'
        last=check['triangles'];records[level].update(check)
        records[level]['sha256']=_sha(pending)
        print('HUB8_SKYLIGHT_FBX_OK',level,check['chunks'],check['triangles'],flush=True)
    for path,before in original_sha.items():assert _sha(path)==before,('Concurrent staging edit',path)
    # Commit only after native/evaluated geometry and all real FBX imports pass.
    for pending,path in zip(pending_models,models):pending.replace(path)
    pending_source.replace(source)
    for c in manifest['chunks']:
        if c['name'] in chunk_materials:c['material']=chunk_materials[c['name']]
    names={p['name'] for p in manifest['palette']}
    for family in FAMILIES:
        if family not in names:manifest['palette'].append(dict(name=family,rgba=[1,1,1,1]))
    manifest['hub8Skylight']=dict(ridgeHeight=RIDGE,eavesHeight=EAVES,glassThickness=THICKNESS,
                 footprint=[-15,15,-32,18],paneCount=sum(p['panes'] for p in source_record['panes']),
                 whiteSupportTrusses=6,retainedFrameChunkNames=True,frontDomeUnchanged=True)
    _json(manifest_path,manifest)
    proof=dict(status='HUB8_SKYLIGHT_SOURCE_AND_FBX_OK',root=str(root),
               beforeFileSHA256=original_sha,source=source_record,models=records,
               sourceSHA256=_sha(source),manifestSHA256=_sha(manifest_path),
               unityImportNotYetVerified=True,visualAcceptanceNotYetVerified=True,
               pbrContract={f:dict(resolution=2048,tiling=1,normalStrength=.12 if f==FAMILIES[0] else .35,
                                 uvPeriodMetres=2,transparent=f==FAMILIES[0],maps=list(MAPS)) for f in FAMILIES})
    _json(work/'skylight_geometry_proof.json',proof)
    print(proof['status'],source_record['panes'],flush=True)
    return proof


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',required=True,type=Path)
    parser.add_argument('--work',required=True,type=Path)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    apply(args.root,args.work)
