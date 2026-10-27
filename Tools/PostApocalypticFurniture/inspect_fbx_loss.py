"""Read-only forensic comparison of author mesh, binary FBX and clean import."""
import sys,json,collections,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy
from io_scene_fbx import parse_fbx
from catalog import ASSET,VERIFY,ROWS


def topo(vertices,faces):
    counts=collections.Counter(tuple(sorted(f))for f in faces)
    geometric=collections.Counter(tuple(sorted(tuple(round(q,7)for q in vertices[i])for i in f))for f in faces)
    repeat=sum(1 for f in faces if len(set(f))<len(f))
    zeros=0;small=0;areas=[]
    for f in faces:
        if len(f)!=3:continue
        a,b,c=[vertices[i]for i in f]
        u=[b[i]-a[i]for i in range(3)];v=[c[i]-a[i]for i in range(3)]
        cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
        area=math.sqrt(sum(q*q for q in cross))*.5;areas.append(area)
        zeros+=area<1e-14;small+=area<1e-11
    edge_count=collections.Counter(tuple(sorted((f[i],f[(i+1)%len(f)])))for f in faces for i in range(len(f)))
    return dict(vertices=len(vertices),faces=len(faces),triangles=sum(max(0,len(f)-2)for f in faces),
                repeated_vertex_faces=repeat,duplicate_index_faces=sum(n-1 for n in counts.values()if n>1),
                duplicate_position_faces=sum(n-1 for n in geometric.values()if n>1),
                zero_area_faces=zeros,area_under_1e11=small,min_triangle_area=min(areas or[0]),
                boundary_edges=sum(n==1 for n in edge_count.values()),nonmanifold_edges=sum(n>2 for n in edge_count.values()))


def meshstats(mesh):
    return topo([tuple(v.co)for v in mesh.vertices],[list(p.vertices)for p in mesh.polygons])


def duplicate_details(mesh):
    seen={};detail=[]
    for p in mesh.polygons:
        key=tuple(sorted(p.vertices))
        if key in seen:
            points=[mesh.vertices[i].co for i in p.vertices]
            detail.append(dict(material=mesh.materials[p.material_index].name,
                               center=list(sum(points,points[0]*0)/len(points)),
                               bounds_size=[max(v[i]for v in points)-min(v[i]for v in points)for i in range(3)]))
        else:seen[key]=p.index
    return detail


def component_stats(mesh):
    from mathutils import Vector
    neighbours=[set()for _ in mesh.vertices]
    for p in mesh.polygons:
        for vi in p.vertices:neighbours[vi].update(p.vertices)
    group={};sets=[]
    for start,n in enumerate(neighbours):
        if start in group or not n:continue
        index=len(sets);todo=[start];verts=set()
        while todo:
            vi=todo.pop()
            if vi in verts:continue
            verts.add(vi);group[vi]=index;todo.extend(neighbours[vi]-verts)
        sets.append(verts)
    faces=[[]for _ in sets]
    for p in mesh.polygons:faces[group[p.vertices[0]]].append(p)
    result=[]
    for vertices,polys in zip(sets,faces):
        points=[mesh.vertices[i].co for i in vertices]
        size=[max(v[i]for v in points)-min(v[i]for v in points)for i in range(3)]
        volume=0
        for p in polys:
            vv=[mesh.vertices[i].co for i in p.vertices]
            for j in range(1,len(vv)-1):volume+=vv[0].dot(vv[j].cross(vv[j+1]))/6
        result.append(dict(triangles=sum(len(p.vertices)-2 for p in polys),bounds_size=size,
                           center=list(sum(points,Vector())/len(points)),signed_volume=volume,
                           materials=sorted({mesh.materials[p.material_index].name for p in polys})))
    return result


def binary(path):
    elem,version=parse_fbx.parse(str(path))
    objects=next(e for e in elem.elems if e.id==b'Objects')
    model_names={o.props[0]:o.props[1].split(b'\x00')[0].decode('utf-8')for o in objects.elems if o.id==b'Model'}
    connections=next(e for e in elem.elems if e.id==b'Connections')
    geometry_models={e.props[1]:model_names[e.props[2]]for e in connections.elems
                     if e.id==b'C' and e.props[0]==b'OO' and e.props[2]in model_names}
    geometries={}
    for geom in objects.elems:
        if geom.id!=b'Geometry':continue
        name=geometry_models.get(geom.props[0],geom.props[1].split(b'\x00')[0].decode('utf-8'))
        data={e.id:e for e in geom.elems}
        array=data[b'Vertices'].props[0];vertices=[tuple(array[i:i+3])for i in range(0,len(array),3)]
        faces=[];face=[]
        for raw in data[b'PolygonVertexIndex'].props[0]:
            face.append(raw if raw>=0 else raw^-1)
            if raw<0:faces.append(face);face=[]
        geometries[name]=topo(vertices,faces)
    return {'fbx_version':version,'geometries':geometries}


report={'kind':'read_only_author_binary_import_forensics','assets':[]}
for asset_id in (1,3,57):
    row=ROWS[asset_id-1]
    source=ASSET/'SourceBlender/Source~'/(row['category']+'.blend')
    bpy.ops.wm.open_mainfile(filepath=str(source))
    author={};validated={}
    for obj in list(bpy.data.objects):
        if obj.type!='MESH' or not obj.name.startswith(row['slug']+'__'):continue
        name=obj.name.split('__',1)[1]
        author[name]=dict(**meshstats(obj.data),duplicate_details=duplicate_details(obj.data))
        copy=obj.data.copy();changed=copy.validate(verbose=False,clean_customdata=False)
        validated[name]=dict(changed=changed,**meshstats(copy),components=component_stats(copy));bpy.data.meshes.remove(copy)
    path=ASSET/'Models'/row['category']/(row['slug']+'.fbx')
    parsed=binary(path)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    imported={o.name:meshstats(o.data)for o in bpy.context.scene.objects if o.type=='MESH'}
    entry=dict(id=asset_id,name=row['slug'],author=author,validated_author=validated,binary=parsed,imported=imported)
    report['assets'].append(entry)
    for name,stats in author.items():
        if name.endswith('LOD2'):
            print('FBX_FORENSIC',asset_id,name,'authorTriangles',stats['triangles'],'duplicates',stats['duplicate_index_faces'],
                  'validatedTriangles',validated[name]['triangles'],'binaryTriangles',parsed['geometries'].get(name,{}).get('triangles'),
                  'importTriangles',imported.get(name,{}).get('triangles'),flush=True)
dest=VERIFY/'fbx_lod_loss.json';dest.write_text(json.dumps(report,indent=2),encoding='utf-8')
print('FORENSICS_DONE',str(dest),flush=True)
