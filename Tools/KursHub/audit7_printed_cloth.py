"""Conform painted symbols to the actual cloth, with a 0.3 mm paint layer."""
from pathlib import Path
import bpy,bmesh,array,json,math,statistics,sys,shutil
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit7';A=W/'authoring'
sys.path.insert(0,str(Path(__file__).parent))
from audit7_soft_shading import child,clone,encoded_clone,geometry_mesh,smooth_mesh,parse_fbx,encode_bin,tree_hash
from audit7_verify_props import geometry
SIZES={'KH_KursAtriumBanner':(4.5,6),'KH_SignWeapons':(4,.95),'KH_SignMedicalTrade':(4,1.3),'KH_SignGear':(4,1.25),'KH_SignRepair':(3,1.5),'KH_CheckpointHeader':(6,1.4),'KH_CheckpointRules':(2.6,1.6)}
INK={'ClothInk','InkDark','SignRed'}
clearances=[]
def fold(x,y,w,h):
    u=x/w+.5;v=y/h
    return .042*math.sin(6*math.pi*u)*(.4+.6*v)+.028*math.sin(11*math.pi*u+1.7*v)+.045*math.sin(math.pi*u)*math.sin(2*math.pi*v)
def conform(mesh,cloth,size):
    tree=BVHTree.FromPolygons([v.co for v in cloth.vertices],[list(p.vertices) for p in cloth.polygons],all_triangles=False)
    parents=list(range(len(mesh.vertices)))
    def find(i):
        while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
        return i
    for edge in mesh.edges:
        a,b=edge.vertices;parents[find(a)]=find(b)
    components={}
    for v in mesh.vertices:components.setdefault(find(v.index),[]).append(v)
    changed=0;weights={};sides={}
    for vertices in components.values():
        deltas=[v.co.y-fold(v.co.x,v.co.z,*size) for v in vertices]
        sign=1 if statistics.median(deltas)>0 else -1
        d=[abs(x) for x in deltas];lo,hi=min(d),max(d)
        for v,depth in zip(vertices,d):
            origin=Vector((v.co.x,sign*2,v.co.z));direction=Vector((0,-sign,0))
            hit,normal,index,distance=tree.ray_cast(origin,direction,4)
            assert hit is not None,('Paint outside real cloth',mesh.name,tuple(v.co))
            fraction=(depth-lo)/(hi-lo) if hi-lo>1e-8 else .5
            weights[v.index]=fraction;sides[v.index]=sign
            v.co.y=hit.y+sign*(.001+.0003*fraction);changed+=1
    # Check cap interiors as well as vertices. Cloth has a coarser grid than
    # glyphs; use a measured clearance per connected glyph to keep the entire
    # thin layer outside those facets without unsafe LOD tessellation.
    mesh.update();mesh.calc_loop_triangles();needed={}
    for triangle in mesh.loop_triangles:
        a,b,c=[mesh.vertices[i].co for i in triangle.vertices]
        if abs((b.x-a.x)*(c.z-a.z)-(c.x-a.x)*(b.z-a.z))<1e-12:continue
        key=find(triangle.vertices[0]);sign=sides[triangle.vertices[0]]
        for i in range(6):
            for j in range(6-i):
                point=a*(i/5)+b*(j/5)+c*((5-i-j)/5)
                hit,_,_,_=tree.ray_cast(Vector((point.x,sign*2,point.z)),Vector((0,-sign,0)),4)
                assert hit is not None
                needed[key]=max(needed.get(key,0),.001-sign*(point.y-hit.y))
    assert max(needed.values(),default=0)<.025,('Unsafe paint gap',mesh.name,max(needed.values()))
    for key,vertices in components.items():
        clearance=max(0,needed.get(key,0))
        for v in vertices:v.co.y+=sides[v.index]*clearance
        if clearance>.0001:clearances.append(dict(mesh=mesh.name,extraClearanceMetres=clearance,component=key))
    mesh.update();smooth_mesh(mesh)
    # Font bevel normals become misleading when its extrusion is compressed
    # to paint thickness. Ink follows the fabric's shading normal; it should
    # not retain the rounded metal-letter highlight/shadow of the old mesh.
    normals=[]
    for v in mesh.vertices:
        sign=sides[v.index]
        _,normal,_,_=tree.ray_cast(Vector((v.co.x,sign*2,v.co.z)),Vector((0,-sign,0)),4)
        if normal.y*sign<0:normal=-normal
        normals.append(normal)
    mesh.normals_split_custom_set_from_vertices(normals);mesh.update()
    check=geometry(mesh)
    assert not any(check[k] for k in ('nonManifoldEdges','degenerateFaces','invalidFaceNormals','nonPositiveSolids')),(mesh.name,check)
    return changed

def replace_geometry(g,mesh):
    child(g,b'Vertices').props[0]=array.array('d',(x for v in mesh.vertices for x in v.co))
    nl=child(g,b'LayerElementNormal');child(nl,b'MappingInformationType').props[0]=b'ByPolygonVertex';child(nl,b'ReferenceInformationType').props[0]=b'Direct'
    child(nl,b'Normals').props[0]=array.array('d',(x for n in mesh.corner_normals for x in n.vector));nl.elems=[e for e in nl.elems if e.id not in (b'NormalsIndex',b'NormalsW')]

def read_uvs(g,mesh):
    for uv in (e for e in g.elems if e.id==b'LayerElementUV'):
        assert child(uv,b'MappingInformationType').props[0]==b'ByPolygonVertex'
        data=child(uv,b'UV').props[0];indices=child(uv,b'UVIndex').props[0]
        layer=mesh.uv_layers.new(name=child(uv,b'Name').props[0].decode())
        for i,loop in enumerate(layer.data):loop.uv=data[indices[i]*2:indices[i]*2+2]
records=[]
# A stopped pass may have written only part of the set. Restore this pass's
# own snapshot before a deterministic retry; never touch the original baseline.
for filename in ('KursHubProps.blend','KursHub.blend'):
    saved=W/'printed_cloth/before'/filename
    if saved.exists():shutil.copy2(saved,A/'Sources/KursHub'/filename)
for saved in (W/'printed_cloth/before/Assets').rglob('*.fbx') if (W/'printed_cloth/before/Assets').exists() else []:
    shutil.copy2(saved,A/saved.relative_to(W/'printed_cloth/before'))
# Native controls retain every object and UV. Other source geometry is untouched.
for filename in ('KursHubProps.blend','KursHub.blend'):
    path=A/'Sources/KursHub'/filename;backup=W/'printed_cloth/before'/filename;backup.parent.mkdir(parents=True,exist_ok=True)
    if not backup.exists():shutil.copy2(path,backup)
    bpy.ops.wm.open_mainfile(filepath=str(path));count=0
    for id,size in SIZES.items():
        collection=bpy.data.collections[id];meshes=[o for o in collection.objects if o.type=='MESH']
        cloth=max((o for o in meshes if 'FoldedSignCloth' in o.name),key=lambda o:len(o.data.polygons))
        for o in meshes:
            mat=o.data.materials[0].name.split('.')[0]
            if mat in INK:count+=conform(o.data,cloth.data,size)
    bpy.context.scene['KursPrintedCloth']='Actual cloth ray projection, 0.3 mm closed paint, measured per-glyph clearance against coarse cloth facets'
    bpy.ops.wm.save_as_mainfile(filepath=str(path));records.append(dict(source=filename,paintVertices=count))
manifest_path=A/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json';manifest=json.loads(manifest_path.read_text(encoding='utf8'))
for prop in manifest['props']:
    if prop['id'] not in SIZES:continue
    for level in prop['lods']:
        path=A/level['model'];parsed,version=parse_fbx.parse(str(path));root=clone(parsed);objects=child(root,b'Objects').elems;connections=child(root,b'Connections').elems
        models={o.props[0]:o.props[1].split(b'\x00\x01')[0].decode() for o in objects if o.id==b'Model'}
        material={c['name']:c['material'] for c in prop['chunks']};parts={}
        for g in (o for o in objects if o.id==b'Geometry'):
            name=next(models[c.props[2]] for c in connections if c.props[0]==b'OO' and c.props[1]==g.props[0] and c.props[2] in models)
            me=geometry_mesh(g);read_uvs(g,me);parts[name]=(g,me)
        cloth=max((me for name,(g,me) in parts.items() if material[name].startswith('Canvas')),key=lambda me:len(me.polygons))
        changed=[];contracts={name:tree_hash(g) for name,(g,me) in parts.items() if material[name] not in INK}
        fallback=False
        try:
            for name,(g,me) in parts.items():
                if material[name] not in INK:continue
                count=conform(me,cloth,SIZES[prop['id']]);changed.append(dict(chunk=name,vertices=count))
                replace_geometry(g,me)
        except AssertionError:
            if level['level']==0:raise
            # A collapsed distant glyph can have nonmatching front/back cap
            # vertices. Retain the preceding validated painted-cloth pair;
            # reduce its suspension/grommets at this level instead.
            previous=prop['lods'][level['level']-1];prev,_=parse_fbx.parse(str(A/previous['model']))
            po=child(prev,b'Objects').elems;pc=child(prev,b'Connections').elems
            pm={o.props[0]:o.props[1].split(b'\x00\x01')[0].decode() for o in po if o.id==b'Model'}
            pg={next(pm[c.props[2]] for c in pc if c.props[0]==b'OO' and c.props[1]==g.props[0] and c.props[2] in pm):g for g in po if g.id==b'Geometry'}
            for name,(g,me) in parts.items():
                if material[name] in INK or material[name].startswith('Canvas'):g.elems=[clone(e) for e in pg[name].elems]
            contracts={name:value for name,value in contracts.items() if not material[name].startswith('Canvas')}
            fallback=True
        backup=W/'printed_cloth/before'/level['model'];backup.parent.mkdir(parents=True,exist_ok=True)
        if not backup.exists():shutil.copy2(path,backup)
        tmp=path.with_suffix('.paint.fbx');encode_bin.write(str(tmp),encoded_clone(root),version);tmp.replace(path)
        assert all(tree_hash(g)==contracts[name] for name,(g,me) in parts.items() if name in contracts)
        bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
        imported=[o for o in bpy.context.scene.objects if o.type=='MESH']
        for o in imported:
            m=geometry(o.data);assert not any(m[k] for k in ('nonManifoldEdges','degenerateFaces','invalidFaceNormals','nonPositiveSolids')),(path,o.name,m)
        level['triangles']=sum(geometry(o.data)['triangles'] for o in imported)
        points=[o.matrix_world@v.co for o in imported for v in o.data.vertices]
        if level['level']==0:prop['boundsMin']=[min(v[i] for v in points) for i in (0,2,1)];prop['boundsMax']=[max(v[i] for v in points) for i in (0,2,1)]
        records.append(dict(model=level['model'],paintChunks=changed,otherChunkArraysUnchanged=True,actualReverseImport=True,retainedPreviousSafePaintedCloth=fallback))
        print('PRINTED_CLOTH_OK',prop['id'],level['level'],flush=True)
manifest_path.write_text(json.dumps(manifest,indent=2),encoding='utf8')
(W/'printed_cloth/printed_cloth.json').write_text(json.dumps(dict(status='PRINTED_CLOTH_OK',nominalSeparationMetres=.001,paintThicknessMetres=.0003,adaptiveClearances=clearances,records=records),indent=2),encoding='utf8')
