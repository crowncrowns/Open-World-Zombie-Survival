"""Audit and improve the existing Kurs props without regenerating their identities.

Blender 4.5 background entry point. Input is an editable source, never a Unity
prefab. Output is isolated staging: source objects and material/collision chunk
names are retained; only evaluated copies are joined for real FBX exports.
"""
import argparse
import collections
import hashlib
import json
import math
import pathlib
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


PROJECT = pathlib.Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', type=pathlib.Path, default=PROJECT / 'Sources/KursHub/KursHubProps.blend')
parser.add_argument('--manifest', type=pathlib.Path, default=PROJECT / 'Assets/Environment/KursHub/Props/KursHubPropsManifest.json')
parser.add_argument('--out', type=pathlib.Path, default=PROJECT / 'work/KursHub/audit7/geometry')
parser.add_argument('--verify-only', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
OUT = args.out.resolve()
PACKAGE = OUT / 'output'
PROPS = PACKAGE / 'Assets/Environment/KursHub/Props'
SOURCE = PACKAGE / 'Sources/KursHub'
for directory in (OUT, PROPS / 'Models/LOD', SOURCE):
    directory.mkdir(parents=True, exist_ok=True)
manifest = json.loads(args.manifest.read_text(encoding='utf8'))
before_manifest = json.loads(json.dumps(manifest))


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding='utf8')


def xyz(v):
    return Vector((v[0], v[2], v[1]))


def unity(v):
    return [float(v[0]), float(v[2]), float(v[1])]


def geometry(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bad_edges = sum(not edge.is_manifold for edge in bm.edges)
    degenerate = sum(face.calc_area() <= 1e-12 for face in bm.faces)
    negative_faces = sum(face.normal.length < .99 for face in bm.faces)
    volume = bm.calc_volume(signed=True)
    bm.free()
    mesh.calc_loop_triangles()
    values = dict(vertices=len(mesh.vertices), polygons=len(mesh.polygons),
        triangles=len(mesh.loop_triangles), nonManifoldEdges=bad_edges,
        degenerateFaces=degenerate, invalidFaceNormals=negative_faces,
        signedVolume=volume, uvLayers=[layer.name for layer in mesh.uv_layers])
    return values


def audit(prop_records, evaluated=False):
    result = []
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for prop in prop_records:
        collection = bpy.data.collections[prop['collection']]
        objects = []
        for obj in sorted(collection.objects, key=lambda item: item.name):
            if obj.type != 'MESH':
                continue
            data = obj.evaluated_get(depsgraph).to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph) if evaluated else obj.data
            metrics = geometry(data)
            metrics.update(name=obj.name, prop=prop['id'], mesh=obj.data.name,
                materialSlots=[slot.material.name if slot.material else None for slot in obj.material_slots],
                scale=list(obj.scale), determinant=obj.matrix_world.determinant(),
                collider=bool(obj.get('unity_collision', False)),
                modifiers=[dict(type=mod.type, name=mod.name) for mod in obj.modifiers],
                added=bool(obj.get('Audit7Added', False)),
                correction=obj.get('Audit7Correction', ''),
                boundsMin=unity(Vector([min(v.co[i] for v in data.vertices) for i in range(3)])),
                boundsMax=unity(Vector([max(v.co[i] for v in data.vertices) for i in range(3)])))
            if evaluated:
                obj.evaluated_get(depsgraph).to_mesh_clear()
            assert metrics['nonManifoldEdges'] == 0, ('Open mesh', obj.name, metrics)
            assert metrics['degenerateFaces'] == 0, ('Degenerate face', obj.name, metrics)
            assert metrics['signedVolume'] > 1e-11, ('Inverted mesh', obj.name, metrics)
            assert metrics['uvLayers'], ('Missing UV', obj.name)
            assert min(obj.scale) > 0, ('Negative scale', obj.name)
            assert all(metrics['materialSlots']), ('Missing material', obj.name)
            objects.append(metrics)
        result.append(dict(id=prop['id'], origin=prop['origin'], sourceOffset=prop['sourceOffset'],
            triangles=sum(obj['triangles'] for obj in objects), objects=objects,
            chunks=prop['chunks']))
    return result


def recalc(data):
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if bm.calc_volume(signed=True) < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    data.update()


def cuboid(obj):
    """Prove an authored solid is a rectangular box, not just its AABB."""
    mesh = obj.data
    if len(mesh.vertices) != 8 or len(mesh.edges) != 12 or len(mesh.polygons) != 6 or any(len(p.vertices) != 4 for p in mesh.polygons):
        return None
    origin = mesh.vertices[0].co
    edges = []
    for edge in mesh.edges:
        if 0 in edge.vertices:
            other = edge.vertices[1] if edge.vertices[0] == 0 else edge.vertices[0]
            edges.append(mesh.vertices[other].co - origin)
    if len(edges) != 3 or min(edge.length for edge in edges) < 1e-6:
        return None
    axes = [edge.normalized() for edge in edges]
    if max(abs(axes[a].dot(axes[b])) for a, b in ((0, 1), (0, 2), (1, 2))) > 1e-4:
        return None
    centre = sum((v.co for v in mesh.vertices), Vector()) / 8
    lengths = [edge.length for edge in edges]
    for vertex in mesh.vertices:
        if any(abs(abs((vertex.co - centre).dot(axis)) - lengths[i] / 2) > 2e-5 for i, axis in enumerate(axes)):
            return None
    return centre, axes, lengths


def box_collider(obj, proven):
    centre, axes, sizes = proven
    directions = [xyz(axis) for axis in axes]
    if directions[0].cross(directions[1]).dot(directions[2]) < 0:
        directions[2] = -directions[2]
    rotation = Matrix(tuple(directions)).transposed().to_quaternion()
    return dict(source=obj.name, name=obj.name, center=unity(centre), size=sizes,
        rotation=[rotation.x, rotation.y, rotation.z, rotation.w])


def project_wood_uv(obj, proven):
    centre, axes, lengths = proven
    long_i = max(range(3), key=lambda i: lengths[i])
    remaining = [i for i in range(3) if i != long_i]
    along = axes[long_i]
    # All wood uses two metres per UV unit; new maps have V-aligned grain.
    uv = obj.data.uv_layers.active
    for face in obj.data.polygons:
        across = axes[max(remaining, key=lambda i: abs(face.normal.cross(along).dot(axes[i])))]
        for loop in face.loop_indices:
            position = obj.data.vertices[obj.data.loops[loop].vertex_index].co
            uv.data[loop].uv = (position.dot(across) / 2, position.dot(along) / 2)
    obj['Audit7WoodUV'] = 'V aligned to longest construction edge; metres / 2'


def bevel(obj, width, segments=2):
    mod = obj.modifiers.new('Audit7 physical edge radius', 'BEVEL')
    mod.width = width
    mod.segments = segments
    mod.limit_method = 'ANGLE'
    mod.angle_limit = math.radians(35)
    mod.use_clamp_overlap = True
    mod.harden_normals = True
    mod.loop_slide = True
    # Source keeps editable modifiers. Evaluated normal export is explicit.
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.set_sharp_from_angle(angle=math.radians(70))
    normals = obj.modifiers.new('Audit7 weighted corner normals', 'WEIGHTED_NORMAL')
    normals.keep_sharp = True
    normals.weight = 50
    obj['Audit7BevelMetres'] = width
    obj['Audit7Correction'] = 'Physical rounded hard edge with weighted large face normals'


def add_mesh(prop_id, name, verts, faces, material, collider=False, correction=''):
    col = bpy.data.collections[prop_id]
    data = bpy.data.meshes.new(name)
    data.from_pydata([xyz(v) for v in verts], [], faces)
    recalc(data)
    obj = bpy.data.objects.new(name, data)
    col.objects.link(obj)
    obj.location = col.instance_offset
    obj['prop_id'] = prop_id
    obj['unity_collision'] = collider
    obj['Audit7Added'] = True
    obj['Audit7Correction'] = correction
    data.materials.append(bpy.data.materials[material])
    uv = data.uv_layers.new(name='UVMap')
    for face in data.polygons:
        axis = max(range(3), key=lambda i: abs(face.normal[i]))
        axes = [i for i in range(3) if i != axis]
        for loop in face.loop_indices:
            v = data.vertices[data.loops[loop].vertex_index].co
            uv.data[loop].uv = v[axes[0]] / 2, v[axes[1]] / 2
    return obj


BOX_FACES = ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7))


def add_box(prop_id, name, center, size, material='Frame', collider=False, radius=.001):
    c = Vector(center)
    verts = [tuple(c + Vector((x * size[0], y * size[1], z * size[2])) / 2)
        for x, y, z in ((-1,-1,-1),(1,-1,-1),(1,-1,1),(-1,-1,1),(-1,1,-1),(1,1,-1),(1,1,1),(-1,1,1))]
    obj = add_mesh(prop_id, name, verts, BOX_FACES, material, collider,
        'Added real joinery/structural fitting, not a decal')
    if radius:
        bevel(obj, min(radius, min(size) * .17), 2)
    return obj


def add_cylinder(prop_id, name, start, end, radius, material='Frame', sides=8, end_radius=None):
    a, b = Vector(start), Vector(end)
    axis = (b - a).normalized()
    reference = min((Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1))), key=lambda v:abs(v.dot(axis)))
    side = axis.cross(reference).normalized()
    up = axis.cross(side).normalized()
    verts = [tuple(p + r * (side * math.cos(i * math.tau / sides) + up * math.sin(i * math.tau / sides)))
        for p,r in ((a,radius),(b,end_radius if end_radius is not None else radius)) for i in range(sides)]
    faces = [tuple(range(sides-1,-1,-1)),tuple(sides+i for i in range(sides))]
    faces += [(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
    obj = add_mesh(prop_id,name,verts,faces,material,False,'Capped volumetric fastener or barrel; real radial silhouette')
    for face in obj.data.polygons:
        face.use_smooth = len(face.vertices) == 4
    return obj


def add_profile(prop_id, name, outline, depth, material, transform=None):
    verts = [(x, y, z) for z in (-depth/2,depth/2) for x,y in outline]
    if transform:
        verts = [tuple(transform(Vector(v))) for v in verts]
    n=len(outline)
    faces = [tuple(range(n-1,-1,-1)),tuple(n+i for i in range(n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    obj=add_mesh(prop_id,name,verts,faces,material,False,'Recognisable shaped rifle component with real thickness')
    bevel(obj,min(depth*.12,.002),2)
    return obj


def surface_patch(obj, target):
    """Replace floating flat fabric patches with closed, conformed cloth grids."""
    data=obj.data
    lo=[min(v.co[i] for v in data.vertices) for i in range(3)]
    hi=[max(v.co[i] for v in data.vertices) for i in range(3)]
    tree=BVHTree.FromPolygons([v.co for v in target.data.vertices],[tuple(p.vertices) for p in target.data.polygons],all_triangles=False)
    nx,nz=6,4
    verts=[]
    for layer in (0,.007):
        for j in range(nz+1):
            for i in range(nx+1):
                x=lo[0]+(hi[0]-lo[0])*i/nx
                z=lo[2]+(hi[2]-lo[2])*j/nz
                hit=tree.ray_cast(Vector((x,-3,z)),Vector((0,1,0)))
                y=hit[0].y if hit[0] is not None else (lo[1]+hi[1])/2
                verts.append((x,y-.003-layer,z))
    stride=nx+1;count=stride*(nz+1)
    faces=[]
    for j in range(nz):
        for i in range(nx):
            a=j*stride+i
            faces += [(a,a+1,a+1+stride,a+stride),tuple(count+k for k in (a+stride,a+1+stride,a+1,a))]
    outline=list(range(stride))+[j*stride+nx for j in range(1,nz+1)]+[nz*stride+i for i in range(nx-1,-1,-1)]+[j*stride for j in range(nz-1,0,-1)]
    for i,a in enumerate(outline):
        b=outline[(i+1)%len(outline)]
        faces.append((a,a+count,b+count,b))
    new=bpy.data.meshes.new(data.name+'_conformed')
    new.from_pydata(verts,[],faces)
    recalc(new)
    for material in data.materials:new.materials.append(material)
    uv=new.uv_layers.new(name='UVMap')
    for face in new.polygons:
        for loop in face.loop_indices:
            v=new.vertices[new.loops[loop].vertex_index].co
            uv.data[loop].uv=v.x/2,v.z/2
    obj.data=new
    obj['Audit7Correction']='Fabric repair conforms to actual folded cloth with 7 mm sewn thickness'


def add_weapons():
    identifier='KH_WeaponsDisplay'
    collection=bpy.data.collections.new(identifier)
    bpy.context.scene.collection.children.link(collection)
    index=len(manifest['props'])
    offset=[(index%4)*13.0,0,(index//4)*10.0]
    collection.instance_offset=xyz(offset)
    collection['UnityPlacementOrigin']='Countertop centre; 1.82 m wide, roughly 1.22 m high, .55 m deep; four static rifles and magazines, front -Z'
    for x in (-.83,.83):
        add_box(identifier,'Audit7_RackFoot',(x,.026,.04),(.16,.052,.50),'WornWood',False,.004)
        add_box(identifier,'Audit7_RackUpright',(x,.51,.15),(.065,1.02,.065),'TimberRaw',False,.004)
    for y in (.12,.49,.86):
        add_box(identifier,'Audit7_RackCrossRail',(0,y,.16),(1.72,.065,.06),'WornWood',False,.004)
    # Four complete 1.0 m silhouettes: barrel, receiver, butt stock, handguard,
    # curved detachable magazine, pistol grip, gas tube, sights and trigger bow.
    for index,x in enumerate((-.63,-.21,.21,.63)):
        angle=math.radians(-84+(index%2)*5)
        c,s=math.cos(angle),math.sin(angle)
        def place(v):
            return Vector((x+c*v.x-s*v.y,.40+s*v.x+c*v.y,-.01+v.z))
        def cylinder(name,a,b,r,mat='Frame',sides=10,r2=None):
            return add_cylinder(identifier,'Audit7_Rifle'+str(index)+'_'+name,place(Vector(a)),place(Vector(b)),r,mat,sides,r2)
        # Local rifle muzzle points -X; its butt rests on the rack's low rail.
        add_profile(identifier,'Audit7_Rifle'+str(index)+'_ButtStock',
            [(-.04,-.042),(.15,-.052),(.23,-.105),(.25,-.10),(.25,.065),(.17,.068),(.035,.026)],
            .055,'TimberRaw',place)
        add_profile(identifier,'Audit7_Rifle'+str(index)+'_Receiver',
            [(-.38,-.037),(-.045,-.037),(.025,.026),(-.015,.065),(-.33,.065),(-.38,.031)],
            .06,'Frame',place)
        add_profile(identifier,'Audit7_Rifle'+str(index)+'_PistolGrip',
            [(-.035,-.027),(.024,-.028),(.067,-.17),(.012,-.18)],.041,'PlasticDark',place)
        add_profile(identifier,'Audit7_Rifle'+str(index)+'_CurvedMagazine',
            [(-.255,-.026),(-.17,-.026),(-.151,-.13),(-.115,-.25),(-.187,-.275),(-.235,-.14)],
            .045,'SteelOxide',place)
        add_profile(identifier,'Audit7_Rifle'+str(index)+'_TimberHandguard',
            [(-.57,-.024),(-.37,-.025),(-.35,.035),(-.57,.038)],.052,'WornWood',place)
        cylinder('RifledBarrel',(-.76,.012,0),(-.54,.012,0),.012,'Frame',12)
        cylinder('MuzzleCollar',(-.79,.012,0),(-.755,.012,0),.018,'SteelOxide',12)
        cylinder('GasTube',(-.63,.052,0),(-.35,.052,0),.010,'Frame',10)
        cylinder('ChargingLever',(-.15,.042,-.032),(-.15,.042,-.060),.008,'SteelOxide',8)
        cylinder('RearSight',(-.33,.068,0),(-.29,.074,0),.014,'Frame',8)
        # Trigger bow is four capped beams, leaving a true central opening.
        for a,b in (((-.12,-.045,0),(-.11,-.09,0)),((-.11,-.09,0),(-.045,-.09,0)),
                    ((-.045,-.09,0),(-.025,-.042,0)),((-.025,-.042,0),(-.12,-.045,0))):
            cylinder('TriggerGuard',a,b,.005,'Frame',6)
        cylinder('Trigger',(-.075,-.04,0),(-.066,-.073,0),.004,'SteelOxide',6)
        cylinder('FrontSightPost',(-.70,.017,0),(-.70,.078,0),.008,'Frame',8)
        cylinder('FrontSightEars',(-.70,.075,-.018),(-.70,.075,.018),.008,'Frame',8)
        # Receiver pins and slots are modest three dimensional details.
        for u in (-.29,-.12):
            cylinder('ReceiverPin',(u,.0,-.034),(u,.0,-.039),.006,'SteelOxide',8)
        for u in (-.52,-.46,-.40):
            cylinder('HandguardVent',(u,.006,-.028),(u,.006,-.032),.005,'PlasticDark',6)
    for i in range(3):
        x=-.30+i*.24
        add_box(identifier,'Audit7_SpareMagazine',(x,.05,-.18),(.17,.07,.25),'SteelOxide',False,.006)
        add_box(identifier,'Audit7_MagazineBaseplate',(x,.028,-.18),(.18,.018,.265),'Frame',False,.002)
    prop=dict(id=identifier,model='Assets/Environment/KursHub/Props/Models/'+identifier+'.fbx',
        collection=identifier,sourceOffset=offset,chunks=[],
        origin=collection['UnityPlacementOrigin'],triangles=0,boundsMin=[0,0,0],boundsMax=[0,0,0])
    manifest['props'].append(prop)


def added_joinery(prop):
    collection=bpy.data.collections[prop['id']]
    # Only timber faces whose fastening was absent get nail heads. Existing
    # BunkBoltHead/BoardNail/TableNail objects are retained, never duplicated.
    keywords=('FrontSlat','EndSlat','PalletDeckBoard','CounterTopPlank','RearShelfBoard',
        'SalvagedCounterPlank','WorkbenchTopPlank','ToolBoardRecoveredPlank',
        'BenchSeatPlank','BenchBackPlank','StoolSeatPlank')
    for obj in list(collection.objects):
        if not any(word in obj.name for word in keywords):continue
        proven=cuboid(obj)
        if proven is None:continue
        centre,axes,lengths=proven
        thickness=min(range(3),key=lambda i:lengths[i])
        longest=max(range(3),key=lambda i:lengths[i])
        if longest==thickness:continue
        outward=axes[thickness]
        # Front-facing for vertical crate sides, upward for horizontal planks.
        if abs(outward.z)>.5:
            if outward.z<0:outward=-outward
        elif outward.y>0:outward=-outward
        for sign in (-1,1):
            position=centre+axes[longest]*(sign*(lengths[longest]/2-min(.065,lengths[longest]*.17)))+outward*(lengths[thickness]/2+.0005)
            start=unity(position);end=unity(position+outward*.0035)
            radius=min(.007,min(lengths)*.12)
            nail=add_cylinder(prop['id'],'Audit7_RecessedBoardFastener',start,end,radius,'Frame',6)
            nail['Audit7Correction']='Two small physical nail heads at the board-to-frame fastening points'
    if prop['id'] in ('KH_StallTimber','KH_StallPipe'):
        for x in (-1.97,1.97):
            for z in (-1.28,1.28):
                for y in (1.02,2.72):
                    add_box(prop['id'],'Audit7_StallJointPlate',(x,y,z-.073),(.16,.16,.012),'SteelOxide',False,.0015)
                    for dx in (-.052,.052):
                        add_cylinder(prop['id'],'Audit7_StallJointBolt',(x+dx,y,z-.082),(x+dx,y,z-.087),.008,'Frame',6)
    if prop['id']=='KH_Stool':
        for i in range(3):
            a=i*math.tau/3;b=(i+1)*math.tau/3
            add_cylinder(prop['id'],'Audit7_StoolRung',(.195*math.cos(a),.20,.195*math.sin(a)),
                (.195*math.cos(b),.20,.195*math.sin(b)),.014,'TimberRaw',8)
    if prop['id']=='KH_MetalDetector':
        for side in (-1,1):
            for y in (1.05,1.11,1.17,1.23):
                add_box(prop['id'],'Audit7_DetectorVent',(side*.811,y,.13),(.008,.012,.16),'PlasticDark',False,.001)
    if prop['id']=='KH_Floodlight':
        for x in (-.14,0,.14):
            add_box(prop['id'],'Audit7_LampHeatSink',(x,-.10,-.14),(.025,.32,.05),'Frame',False,.003)


def improve():
    for prop in manifest['props']:
        collection=bpy.data.collections[prop['collection']]
        for obj in list(collection.objects):
            if obj.type!='MESH':continue
            material=obj.data.materials[0].name
            proven=cuboid(obj)
            if proven:
                min_size=min(proven[2]);max_size=max(proven[2])
                if material in ('WornWood','TimberRaw'):
                    project_wood_uv(obj,proven)
                    if min_size>=.025:
                        bevel(obj,min(.006,min_size*.095),2)
                elif material in ('Frame','SteelOxide','Enamel','Copper','PlasticDark','MallBlue','CreamPanel','Concrete'):
                    # No bevel on 12 mm grid wires, foil labels, printed signs,
                    # glass, or tiny sensor sheets; their profiles stay exact.
                    if min_size>=.022 and max_size>=.045:
                        bevel(obj,min(.003,min_size*.10),2)
            if ('RootVegetables' in obj.name or 'StoredSack' in obj.name or 'GrainSack' in obj.name or
                'LumpySupplyBag' in obj.name or 'Backpack' in obj.name or 'TradedShoe' in obj.name or
                'FoldedClothing' in obj.name or 'BootToe' in obj.name or 'BootUpper' in obj.name or
                'ArmyCanteen' in obj.name or 'WorkbenchRag' in obj.name or 'RolledBedmat' in obj.name):
                if proven is None:
                    for face in obj.data.polygons:face.use_smooth=True
                    obj['Audit7Correction']='Smooth sewn or organic silhouette; preserve seams and volume'
            if 'ScreenClothPatch' in obj.name:
                target=next((o for o in collection.objects if o.name.startswith('PrivacyCloth')),None)
                if target:surface_patch(obj,target)
            if 'DrapeRepair' in obj.name:
                target=next((o for o in collection.objects if o.name.startswith('RailHangingCloth')),None)
                if target:surface_patch(obj,target)
        added_joinery(prop)
    add_weapons()


def chunk_name(prop,material,collision):
    for chunk in prop['chunks']:
        if chunk['material']==material and bool(chunk['collider'])==collision:
            return chunk['name']
    alias='Lettering' if material=='ClothInk' else 'Frame' if material=='InkDark' and prop['id']=='KH_SignMedicalTrade' else material
    return prop['id']+'_'+alias+('_C' if collision else '_NC')


def evaluated_copy(obj,collection,depsgraph):
    evaluated=obj.evaluated_get(depsgraph)
    data=bpy.data.meshes.new_from_object(evaluated,preserve_all_data_layers=True,depsgraph=depsgraph)
    copy=bpy.data.objects.new('Audit7_export_part',data)
    collection.objects.link(copy)
    copy.matrix_world=Matrix.Identity(4)
    copy.select_set(True)
    return copy


def export_all():
    export_collection=bpy.data.collections.new('AUDIT7_EXPORT_TEMP')
    bpy.context.scene.collection.children.link(export_collection)
    depsgraph=bpy.context.evaluated_depsgraph_get()
    lod_report=[]
    for prop in manifest['props']:
        groups=collections.defaultdict(list)
        for obj in bpy.data.collections[prop['collection']].objects:
            if obj.type=='MESH':groups[(obj.data.materials[0].name,bool(obj.get('unity_collision',False)))].append(obj)
        exports=[]
        chunks=[]
        for (material,collision),objects in sorted(groups.items()):
            bpy.ops.object.select_all(action='DESELECT')
            copies=[evaluated_copy(obj,export_collection,depsgraph) for obj in objects]
            bpy.context.view_layer.objects.active=copies[0]
            if len(copies)>1:bpy.ops.object.join()
            copy=bpy.context.object
            copy.name=chunk_name(prop,material,collision)
            copy.data.name=copy.name
            copy['materialName']=material
            exports.append(copy)
            chunks.append(dict(name=copy.name,material=material,collider=collision))
        assert {c['name'] for c in prop['chunks']} <= {c['name'] for c in chunks}, ('Lost chunk identity',prop['id'])
        prop['chunks']=chunks
        root=bpy.data.objects.new(prop['id']+'_Root',None)
        export_collection.objects.link(root)
        bpy.ops.object.select_all(action='DESELECT')
        root.select_set(True)
        for obj in exports:
            obj.parent=root
            obj.select_set(True)
        bpy.context.view_layer.objects.active=exports[0]
        def export(path):
            bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'EMPTY','MESH'},
                axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,
                use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
        export(PROPS/'Models'/(prop['id']+'.fbx'))
        points=[v.co for obj in exports for v in obj.data.vertices]
        prop['boundsMin']=unity(Vector([min(p[i] for p in points) for i in range(3)]))
        prop['boundsMax']=unity(Vector([max(p[i] for p in points) for i in range(3)]))
        for obj in exports:
            metrics=geometry(obj.data)
            assert metrics['nonManifoldEdges']==0 and metrics['degenerateFaces']==0 and metrics['signedVolume']>0,(obj.name,metrics)
        triangles=sum(geometry(obj.data)['triangles'] for obj in exports)
        prop['triangles']=triangles
        levels=[dict(level=0,triangles=triangles,model=prop['model'])]
        # Decimate COLLAPSE works on the evaluated bevel surfaces and folded
        # cloth, retaining disconnected physical stock. It never replaces LOD0.
        # Very cheap signs/overlays do not get pointless LODGroups.
        if triangles>=400:
            previous=triangles
            base_data={obj.name:obj.data.copy() for obj in exports}
            for level,ratio in ((1,.60),(2,.30)):
                for obj in exports:
                    obj.data=base_data[obj.name].copy()
                    modifier=obj.modifiers.new('Audit7 LOD silhouette reduction','DECIMATE')
                    modifier.decimate_type='COLLAPSE'
                    modifier.ratio=ratio
                    modifier.use_collapse_triangulate=True
                    modifier.use_symmetry=False
                    bpy.context.view_layer.objects.active=obj
                    bpy.ops.object.modifier_apply(modifier=modifier.name)
                    metrics=geometry(obj.data)
                    if metrics['nonManifoldEdges'] or metrics['degenerateFaces'] or metrics['signedVolume']<=0:
                        # Thin painted letters are fragile under aggressive
                        # collapse. Keep that original chunk instead of shipping
                        # a broken reduction; other chunks still reduce.
                        obj.data=base_data[obj.name].copy()
                        obj['LODKeptForTopology']=True
                count=sum(geometry(obj.data)['triangles'] for obj in exports)
                path=PROPS/'Models/LOD'/(prop['id']+'_LOD'+str(level)+'.fbx')
                if count<previous:
                    export(path)
                    levels.append(dict(level=level,triangles=count,
                        model='Assets/Environment/KursHub/Props/Models/LOD/'+path.name))
                    previous=count
                else:
                    # No synthetic claim of a lower level when a topology
                    # preserving reduction cannot lower this asset safely.
                    levels.append(dict(level=level,triangles=count,skipped='No safe descending reduction'))
            for data in base_data.values():
                if data.users==0:bpy.data.meshes.remove(data)
        prop['lods']=levels
        lod_report.append(dict(id=prop['id'],levels=levels))
        for obj in exports:bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.objects.remove(root,do_unlink=True)
        print('AUDIT7_PROP_EXPORTED',prop['id'],triangles,[(x['level'],x['triangles']) for x in levels],flush=True)
    bpy.data.collections.remove(export_collection)
    return lod_report


def roundtrip():
    """Check actual exports; repair only reduced levels, then check all again."""
    import importlib.util
    def helper(filename):
        path=pathlib.Path(__file__).resolve().parent/filename
        spec=importlib.util.spec_from_file_location(path.stem,path)
        module=importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module
    verifier=helper('audit7_verify_props.py')
    report=verifier.run(OUT,raise_on_failure=False)
    if report['errors'] and not args.verify_only:
        # No failed LOD0 may be silently replaced. The repair helper refuses
        # it and preserves originals of every rejected reduced FBX file.
        helper('audit7_repair_lods.py').run(OUT)
        report=verifier.run(OUT,raise_on_failure=False)
    if report['errors']:
        raise RuntimeError('Actual FBX import remains invalid; see fbx_roundtrip.json')
    return report


if args.verify_only:
    manifest=json.loads((PROPS/'KursHubPropsManifest.json').read_text(encoding='utf8'))
    roundtrip()
else:
    bpy.ops.wm.open_mainfile(filepath=str(args.source.resolve()))
    before=audit(manifest['props'])
    write_json(OUT/'props_before.json',dict(status='AUDIT7_PROPS_BASELINE_OK',props=before,
        sourceSHA256=hashlib.sha256(args.source.read_bytes()).hexdigest(),
        materials=[dict(name=m.name,users=m.users) for m in bpy.data.materials],
        images=[dict(name=i.name,path=i.filepath,packed=bool(i.packed_file),size=list(i.size)) for i in bpy.data.images]))
    # Collider proof comes from the exact unmodified source cuboids. Rounded
    # render bevels do not move the extents or fill any actual portal.
    collider_records=[]
    for prop in manifest['props']:
        cubes=[];remaining=[]
        chunks=collections.defaultdict(lambda:dict(boxes=[],remainingMeshSources=[]))
        for obj in bpy.data.collections[prop['collection']].objects:
            if obj.type!='MESH' or not obj.get('unity_collision',False):continue
            chunk=chunks[chunk_name(prop,obj.data.materials[0].name,True)]
            proven=cuboid(obj)
            if proven:
                box=box_collider(obj,proven)
                cubes.append(box)
                chunk['boxes'].append(box)
            else:
                remaining.append(obj.name)
                chunk['remainingMeshSources'].append(obj.name)
        collider_records.append(dict(id=prop['id'],boxes=cubes,remainingMeshSources=remaining,
            chunks=[dict(name=name,allBoxes=not value['remainingMeshSources'],
                remainingSolids=len(value['remainingMeshSources']),**value) for name,value in sorted(chunks.items())]))
    write_json(OUT/'colliders.json',dict(coordinates='Unity metres, prop-local; quaternion x/y/z/w',props=collider_records))
    improve()
    bpy.context.view_layer.update()
    after=audit(manifest['props'],evaluated=True)
    total=sum(p['triangles'] for p in after)
    assert total<=250000,('Budget exceeded',total)
    assert {obj['name'] for p in before for obj in p['objects']} <= {obj['name'] for p in after for obj in p['objects']},'Lost source object'
    write_json(OUT/'props_after.json',dict(status='AUDIT7_PROPS_GEOMETRY_OK',props=after,triangles=total,
        sourceObjects=sum(len(p['objects']) for p in after),retainedSourceObjects=sum(len(p['objects']) for p in before)))
    bpy.context.scene['KursHubPropsStage']='7: audited editable bevels, joinery, conformed repairs, silhouette LODs and weapons stock'
    bpy.context.scene['Audit7Geometry']='No original objects removed; export retains all earlier chunk names and placement origins'
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'KursHubProps.blend'))
    lods=export_all()
    manifest['stage']=7
    manifest['audit7Geometry']='Edited source solids retain original names; evaluated FBX with editable Bevel/WeightedNormal source modifiers'
    write_json(PROPS/'KursHubPropsManifest.json',manifest)
    write_json(OUT/'lods.json',lods)
    write_json(OUT/'geometry_summary.json',dict(status='AUDIT7_PROPS_EXPORTED',beforeProps=len(before),afterProps=len(after),
        beforeTriangles=sum(p['triangles'] for p in before),afterTriangles=total,
        retainedObjects=sum(len(p['objects']) for p in before),addedObjects=sum(obj['added'] for p in after for obj in p['objects']),
        physicalBevelObjects=sum(any(mod['type']=='BEVEL' for mod in obj['modifiers']) for p in after for obj in p['objects']),
        woodUVObjects=sum(1 for obj in bpy.data.objects if obj.get('Audit7WoodUV')),
        fbxExports=sum(sum('model' in level for level in levels['levels']) for levels in lods)))
    roundtrip()
