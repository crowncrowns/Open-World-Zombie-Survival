import bpy,bmesh,mathutils,json,sys,math,time,hashlib
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
from geom import MATERIALS
from models_01_10 import build as build_a
from models_11_20 import build as build_b

def vec(p):return p[0],p[2],p[1]
def recalc(mesh):
    bm=bmesh.new();bm.from_mesh(mesh)
    layer=bm.verts.layers.int.get('furniture_primitive')
    groups={}
    for v in bm.verts:groups.setdefault(v[layer] if layer else 0,[]).append(v)
    # Weld poles/seams within a solid, without fusing touching separate boards
    # into non-manifold T junctions merely because their corners coincide.
    for vertices in groups.values():bmesh.ops.remove_doubles(bm,verts=vertices,dist=.000001)
    bmesh.ops.dissolve_degenerate(bm,dist=.00000001,edges=list(bm.edges))
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    unseen=set(bm.faces)
    while unseen:
        seed=unseen.pop();island={seed};pending=[seed]
        while pending:
            face=pending.pop()
            for e in face.edges:
                for neighbour in e.link_faces:
                    if neighbour in unseen:unseen.remove(neighbour);island.add(neighbour);pending.append(neighbour)
        if all(len(e.link_faces)==2 for face in island for e in face.edges):
            vertices={v for face in island for v in face.verts};origin=sum((v.co for v in vertices),mathutils.Vector())/len(vertices)
            volume=0.0
            for face in island:
                vv=[v.co-origin for v in face.verts]
                for i in range(1,len(vv)-1):volume+=vv[0].dot(vv[i].cross(vv[i+1]))/6
            if volume < -1e-18:bmesh.ops.reverse_faces(bm,faces=list(island))
    bm.to_mesh(mesh);bm.free();mesh.update()
def uv(mesh,carpet=False,bounds=None):
    layer=mesh.uv_layers.new(name='UVMap')
    for p in mesh.polygons:
        normal=p.normal;axis=max(range(3),key=lambda k:abs(normal[k]));axes=[q for q in range(3) if q!=axis]
        for li in p.loop_indices:
            co=mesh.vertices[mesh.loops[li].vertex_index].co
            if carpet and axis==2 and bounds:
                u=(co.x-bounds[0])/(bounds[1]-bounds[0]);v=(co.y-bounds[2])/(bounds[3]-bounds[2]);layer.data[li].uv=(u,v)
            else:layer.data[li].uv=(co[axes[0]]*1.8,co[axes[1]]*1.8)
    # Repacked independently after triangulation below.
    mesh.uv_layers.new(name='LightmapUV',do_init=True)
def lightmap(obj):
    # Exact planar charts preserve metric density. Smart projection can fold
    # an irregular concave island over itself; here every chart is coplanar
    # and every padded rectangle occupies a separate deterministic shelf.
    mesh=obj.data
    layer=mesh.uv_layers.get('LightmapUV') or mesh.uv_layers.new(name='LightmapUV')
    adjacent={};edges={}
    for face in mesh.polygons:
        for li in face.loop_indices:edges.setdefault(mesh.loops[li].edge_index,[]).append(face.index)
    for neighbours in edges.values():
        if len(neighbours)==2:
            a,b=neighbours;adjacent.setdefault(a,[]).append(b);adjacent.setdefault(b,[]).append(a)
    unseen=set(range(len(mesh.polygons)));charts=[]
    while unseen:
        seed=unseen.pop();first=mesh.polygons[seed];normal=first.normal.copy();origin=mesh.vertices[first.vertices[0]].co.copy()
        faces=[seed];pending=[seed]
        while pending:
            current=pending.pop()
            for neighbour in adjacent.get(current,[]):
                if neighbour not in unseen:continue
                face=mesh.polygons[neighbour]
                if face.normal.dot(normal)<.9999999 or any(abs((mesh.vertices[i].co-origin).dot(normal))>1e-7 for i in face.vertices):continue
                unseen.remove(neighbour);pending.append(neighbour);faces.append(neighbour)
        points=[mesh.vertices[i].co for i in first.vertices]
        tangent=max((b-a for a,b in zip(points,points[1:]+points[:1])),key=lambda v:v.length_squared).normalized();bitangent=normal.cross(tangent).normalized()
        coordinates={li:((mesh.vertices[mesh.loops[li].vertex_index].co-origin).dot(tangent),(mesh.vertices[mesh.loops[li].vertex_index].co-origin).dot(bitangent)) for fi in faces for li in mesh.polygons[fi].loop_indices}
        xs=[v[0] for v in coordinates.values()];ys=[v[1] for v in coordinates.values()]
        charts.append(dict(coords=coordinates,minx=min(xs),miny=min(ys),w=max(xs)-min(xs),h=max(ys)-min(ys)))
    area=sum(p.area for p in mesh.polygons);padding=max(math.sqrt(area)/256,1e-6)
    width=max(max(c['w']+padding*2 for c in charts),math.sqrt(sum((c['w']+padding*2)*(c['h']+padding*2) for c in charts))*1.15)
    x=y=row_h=used_w=0
    for chart in sorted(charts,key=lambda c:(-c['h'],-c['w'])):
        w,h=chart['w']+padding*2,chart['h']+padding*2
        if x+w>width and x>0:x=0;y+=row_h;row_h=0
        chart['x']=x+padding;chart['y']=y+padding;x+=w;row_h=max(row_h,h);used_w=max(used_w,x)
    scale=max(used_w,y+row_h,1e-6)
    for chart in charts:
        for li,(u,v) in chart['coords'].items():layer.data[li].uv=((chart['x']+u-chart['minx'])/scale,(chart['y']+v-chart['miny'])/scale)
    mesh.uv_layers.active_index=0

def corner_normals(obj):
    mesh=obj.data
    for polygon in mesh.polygons:polygon.use_smooth=True
    mesh.set_sharp_from_angle(angle=math.radians(65))
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    mod=obj.modifiers.new('Area weighted corner normals','WEIGHTED_NORMAL')
    mod.mode='FACE_AREA_WITH_ANGLE';mod.weight=50;mod.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=mod.name);obj.select_set(False)
def tri(mesh):return sum(len(p.vertices)-2 for p in mesh.polygons)
def make_mesh(g,part,level,mats):
    vertices=[];faces=[];mi=[];sm=[];used=[];uv_overrides={};charts=[];primitive_ids=[]
    pivot=g.parts[part]['pivot']
    for primitive,item in enumerate(g.meshes):
        if item['part']!=part:continue
        if item['mat'] not in used:used.append(item['mat'])
        idx=len(vertices);vertices += [vec(tuple(p[k]-pivot[k] for k in range(3))) for p in item['v']]
        primitive_ids.extend([primitive]*len(item['v']))
        if 'uv' in item:
            for k,q in enumerate(item['uv']):uv_overrides[tuple(round(v,6) for v in vertices[idx+k])]=q
        faces.extend([tuple(idx+i for i in (reversed(f) if item['mat']=='Labels' else f)) for f in item['f']]);mi.extend([used.index(item['mat'])]*len(item['f']));sm.extend([item['smooth']]*len(item['f']))
        charts.extend(item.get('face_uv',[None]*len(item['f'])))
    if not vertices:return None
    mesh=bpy.data.meshes.new(part+'_LOD0_Mesh');mesh.from_pydata(vertices,[],faces);mesh.update()
    attribute=mesh.attributes.new('furniture_primitive','INT','POINT');attribute.data.foreach_set('value',primitive_ids)
    for mat in used:mesh.materials.append(mats[mat])
    for i,p in enumerate(mesh.polygons):p.material_index=mi[i];p.use_smooth=sm[i]
    xs=[v.co.x for v in mesh.vertices];ys=[v.co.y for v in mesh.vertices]
    uv(mesh,any(m.startswith('Carpet') for m in used),(min(xs),max(xs)+1e-7,min(ys),max(ys)+1e-7))
    for poly,chart in zip(mesh.polygons,charts):
        if chart and not mesh.materials[poly.material_index].name.startswith('Carpet'):
            for li,point in zip(poly.loop_indices,chart):mesh.uv_layers[0].data[li].uv=point
    recalc(mesh)
    for poly in mesh.polygons:
        if mesh.materials[poly.material_index].name=='Labels':
            for li in poly.loop_indices:
                co=tuple(round(v,6) for v in mesh.vertices[mesh.loops[li].vertex_index].co)
                if co in uv_overrides:mesh.uv_layers[0].data[li].uv=uv_overrides[co]
    obj=bpy.data.objects.new(part+'_LOD0',mesh);bpy.context.collection.objects.link(obj)
    return obj

def simplify(obj,ratio):
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    mod=obj.modifiers.new('Silhouette optimisation','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=mod.name);obj.select_set(False)
    recalc(obj.data)

def triangulate(obj):
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.triangulate(bm,faces=list(bm.faces))
    dead=[f for f in bm.faces if f.calc_area()<1e-11]
    if dead:bmesh.ops.delete(bm,geom=dead,context='FACES_ONLY')
    bm.to_mesh(obj.data);bm.free();obj.data.validate(verbose=False,clean_customdata=True);obj.data.update()
    repair_uv(obj.data)

def repair_uv(mesh):
    layer=mesh.uv_layers[0]
    for face in mesh.polygons:
        if len(face.vertices)!=3:continue
        a,b,c=[mesh.vertices[i].co for i in face.vertices];e1=b-a;e2=c-a
        l=e1.length
        if l<1e-10:continue
        x=e2.dot(e1)/l;y=math.sqrt(max(0,e2.length_squared-x*x))
        if y<1e-10:continue
        u0,u1,u2=[layer.data[i].uv.copy() for i in face.loop_indices];d1=u1-u0;d2=u2-u0
        aa=d1.x/l;cc=d1.y/l;bb=(d2.x-d1.x*x/l)/y;dd=(d2.y-d1.y*x/l)/y
        determinant=abs(aa*dd-bb*cc);trace=aa*aa+bb*bb+cc*cc+dd*dd
        anisotropy=(trace+math.sqrt(max(0,trace*trace-4*determinant*determinant)))/(2*max(determinant,1e-30))
        if determinant<1e-10 or anisotropy>8:
            axis=max(range(3),key=lambda k:abs(face.normal[k]));axes=[k for k in range(3) if k!=axis]
            for li,co in zip(face.loop_indices,[a,b,c]):layer.data[li].uv=(co[axes[0]]*2,co[axes[1]]*2)

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    for d in ['Models','SourceBlender/Source~','LODs','Documentation']: (ASSET/d).mkdir(parents=True,exist_ok=True)
    mats={}
    for name in MATERIALS:
        mat=bpy.data.materials.new(name);mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF')
        for file,socket in [('BaseColor','Base Color'),('Roughness','Roughness'),('Metallic','Metallic')]:
            path=ASSET/'Textures'/name/(file+'.png')
            if name=='Labels' and file=='BaseColor':path=ASSET/'Textures/Labels_RU.png'
            if path.exists():
                node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=bpy.data.images.load(str(path),check_existing=True)
                if file!='BaseColor':node.image.colorspace_settings.name='Non-Color'
                mat.node_tree.links.new(node.outputs['Color'],bs.inputs[socket])
        normal=ASSET/'Textures'/name/'Normal.png'
        if normal.exists():
            tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(normal),check_existing=True);tex.image.colorspace_settings.name='Non-Color'
            bump=mat.node_tree.nodes.new('ShaderNodeNormalMap');mat.node_tree.links.new(tex.outputs['Color'],bump.inputs['Color']);mat.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
        mats[name]=mat
    result=[];allmeta=[];group_objects=[];lastcat=None
    chosen=ROWS
    if '--ids' in sys.argv:
        wanted={int(s) for s in sys.argv[sys.argv.index('--ids')+1].split(',')};chosen=[r for r in ROWS if r['id'] in wanted]
    for row in chosen:
        if lastcat and lastcat!=row['category']:
            bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'SourceBlender/Source~'/(lastcat+'.blend')),compress=True)
            bpy.ops.file.make_paths_relative();bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'SourceBlender/Source~'/(lastcat+'.blend')),compress=True)
            for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
            for m in list(bpy.data.meshes):
                if m.users==0:bpy.data.meshes.remove(m)
        lastcat=row['category'];g=(build_a if row['id']<=100 else build_b)(row)
        root=bpy.data.objects.new(row['slug'],None);scene.collection.objects.link(root);root['asset_id']=row['id'];root['name_ru']=row['name']
        objects=[root];lods=[0,0,0];partrecords=[];pivots={}
        lod0=[]
        for name,part in g.parts.items():
            pivot=bpy.data.objects.new(name,None);scene.collection.objects.link(pivot);pivot.parent=root;pivot.location=vec(part['pivot']);objects.append(pivot)
            pivots[name]=pivot
            obj=make_mesh(g,name,0,mats)
            if obj:obj.parent=pivot;objects.append(obj);lod0.append(obj)
            partrecords.append(part)
        bpy.context.view_layer.update()
        for name,part in g.parts.items():
            if part.get('parent'):
                world=pivots[name].matrix_world.copy();pivots[name].parent=pivots[part['parent']];pivots[name].matrix_world=world
        bpy.context.view_layer.update()
        total=sum(tri(o.data) for o in lod0)
        if total>18000:
            for o in lod0:simplify(o,max(.12,17850/total))
        for obj in lod0:
            base=tri(obj.data);triangulate(obj);lods[0]+=tri(obj.data)
            for level,ratio in [(1,.49),(2,.19)]:
                copy=bpy.data.objects.new(obj.name.replace('_LOD0',f'_LOD{level}'),obj.data.copy());scene.collection.objects.link(copy);copy.parent=obj.parent
                simplify(copy,max(12/max(12,base),ratio));triangulate(copy);lods[level]+=tri(copy.data);objects.append(copy)
        for obj in objects:
            if obj.type=='MESH':corner_normals(obj);lightmap(obj)
        (ASSET/'LODs'/(row['slug']+'.json')).write_text(json.dumps({'meshes':[{'name':o.name,'materials':[m.name for m in o.data.materials]} for o in objects if o.type=='MESH']},indent=2),encoding='utf-8')
        if lods[0]>18000:raise RuntimeError(f'{row["slug"]} exceeds triangle hard limit {lods}')
        bpy.ops.object.select_all(action='DESELECT')
        for o in objects:o.select_set(True)
        bpy.context.view_layer.objects.active=root
        out=ASSET/'Models'/row['category']/(row['slug']+'.fbx');out.parent.mkdir(parents=True,exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',mesh_smooth_type='FACE',use_mesh_modifiers=True)
        # Blender object names are global; namespace earlier models only AFTER their individual export.
        # Thus every FBX keeps exact stable part names while the category .blend remains editable.
        for obj in objects:
            if obj!=root:obj.name=row['slug']+'__'+obj.name
        points=[p for m in g.meshes for p in m['v']];lo=[min(p[k] for p in points) for k in range(3)];hi=[max(p[k] for p in points) for k in range(3)]
        record=dict(**row,triangles=lods,parts=partrecords,colliders=g.colliders,zones=g.zones,lights=g.lights,devices=g.devices,damageGroups=g.damage_groups,boundsMin=lo,boundsMax=hi)
        allmeta.append(record);result.append(dict(id=row['id'],name=row['name'],category=row['category'],triangles=lods,parts=len(partrecords),colliders=len(g.colliders),zones=len(g.zones),sha256=hashlib.sha256(out.read_bytes()).hexdigest()))
        print('FURNITURE_EXPORTED',row['id'],row['slug'],lods,flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'SourceBlender/Source~'/(lastcat+'.blend')),compress=True)
    bpy.ops.file.make_paths_relative();bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'SourceBlender/Source~'/(lastcat+'.blend')),compress=True)
    if len(chosen)<200:
        old=json.loads((ASSET/'Documentation/catalog.json').read_text(encoding='utf-8'))['assets']
        merged={r['id']:r for r in old};merged.update({r['id']:r for r in allmeta});allmeta=[merged[i] for i in sorted(merged)]
        old=json.loads((VERIFY/'geometry_export.json').read_text(encoding='utf-8'));merged={r['id']:r for r in old};merged.update({r['id']:r for r in result});result=[merged[i] for i in sorted(merged)]
    (ASSET/'Documentation/catalog.json').write_text(json.dumps({'assets':allmeta},ensure_ascii=False,indent=2),encoding='utf-8')
    VERIFY.mkdir(parents=True,exist_ok=True);(VERIFY/'geometry_export.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('FURNITURE_200_EXPORT_PASS',len(result),flush=True)
main()
