import bpy,bmesh,pathlib,json,math,hashlib
from mathutils import Vector
import argparse,sys
parser=argparse.ArgumentParser(description='Repair the original Moscow Trees tube winding/caps and thicken trunks. Input must be unmodified Blender-exported original models, not an already repaired set.')
parser.add_argument('--input',required=True)
parser.add_argument('--output',required=True)
parser.add_argument('--report',required=True)
parser.add_argument('--diameter-scale',type=float,default=1.6)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
source=pathlib.Path(args.input).resolve(); dest=pathlib.Path(args.output).resolve()
assert source!=dest, 'Keep the input unchanged; choose a separate output directory'
assert .5<=args.diameter_scale<=3
report=pathlib.Path(args.report).resolve(); report.parent.mkdir(parents=True,exist_ok=True)
reports=[]

def topology(mesh):
    bm=bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=0.00001)
    result=dict(boundary_edges=sum(e.is_boundary for e in bm.edges),
                manifold=all(e.is_manifold for e in bm.edges),volume=bm.calc_volume(signed=True))
    bm.free(); return result

for file in sorted(source.rglob('*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(file),use_anim=False)
    record=dict(model=file.name,bark=[])
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH' or not (obj.name.startswith('Trunk_Mesh') or obj.name.startswith('Bark_')): continue
        mesh=obj.data; before=topology(mesh)
        if obj.name.startswith('Trunk_Mesh'):
            rings=10 if file.parent.name=='LOD0' else 6
            oldside=len(mesh.vertices)//rings-1
            assert oldside in (7,10),(file,obj.name,len(mesh.vertices),oldside)
            points=[v.co.copy() for v in mesh.vertices]
            uvold=mesh.uv_layers.active
            uvbyvertex={}
            if uvold:
                for loop in mesh.loops: uvbyvertex.setdefault(loop.vertex_index,uvold.data[loop.index].uv.copy())
            sides=20 if rings==10 else 12
            vertices=[]; faces=[]; uvs=[]; centers=[]
            for j in range(rings):
                ring=points[j*(oldside+1):(j+1)*(oldside+1)]
                center=sum(ring[:oldside],Vector())/oldside
                assert (ring[0]-ring[-1]).length<.0001,(file,'ring seam/order')
                a=ring[0]-center
                b=((ring[1]-center)-a*math.cos(math.tau/oldside))/math.sin(math.tau/oldside)
                assert max(abs((p-center).length-a.length) for p in ring)<.001,(file,'noncircular ring')
                centers.append(center)
                v=uvbyvertex.get(j*(oldside+1),Vector((0,j))).y
                for i in range(sides+1):
                    t=math.tau*i/sides
                    vertices.append(center+args.diameter_scale*(a*math.cos(t)+b*math.sin(t)))
                    uvs.append((i/sides,v))
            for j in range(rings-1):
                for i in range(sides):
                    a=j*(sides+1)+i; b=a+sides+1
                    faces.append((a,a+1,b+1,b))
            repaired=bpy.data.meshes.new(mesh.name+'_Solid')
            repaired.from_pydata(vertices,[],faces); repaired.update()
            uv=repaired.uv_layers.new(name='UVMap')
            for loop in repaired.loops: uv.data[loop.index].uv=uvs[loop.vertex_index]
            for mat in mesh.materials: repaired.materials.append(mat)
            obj.data=repaired; mesh=repaired
            record['trunk_diameter_multiplier']=args.diameter_scale
        bm=bmesh.new(); bm.from_mesh(mesh)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
        boundaries=[e for e in bm.edges if e.is_boundary]
        caps=bmesh.ops.holes_fill(bm,edges=boundaries,sides=0)['faces']
        for f in caps: f.smooth=False
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        # Each disconnected tube must have outward winding, not just a positive aggregate volume.
        pending=set(bm.faces)
        while pending:
            component=set(); todo=[pending.pop()]
            while todo:
                face=todo.pop(); component.add(face)
                for edge in face.edges:
                    for other in edge.link_faces:
                        if other in pending: pending.remove(other); todo.append(other)
            volume=sum(f.calc_area()*f.calc_center_median().dot(f.normal)/3 for f in component)
            if volume<0: bmesh.ops.reverse_faces(bm,faces=list(component))
        assert all(e.is_manifold for e in bm.edges),(file,obj.name,'still open')
        bm.to_mesh(mesh); bm.free(); mesh.update()
        if mesh.has_custom_normals:
            bpy.context.view_layer.objects.active=obj; obj.select_set(True)
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
            obj.select_set(False)
        for poly in mesh.polygons:
            if len(poly.vertices)==4: poly.use_smooth=True
        after=topology(mesh)
        assert after['volume']>0,(file,obj.name,after)
        record['bark'].append(dict(name=obj.name,before=before,after=after))
    # Unity uses the external .mat assets; remove broken texture paths inherited from the old FBX.
    for mat in bpy.data.materials:
        if mat.use_nodes:
            for node in mat.node_tree.nodes:
                if node.type=='TEX_IMAGE': node.image=None
    output=dest/file.parent.name/file.name; output.parent.mkdir(exist_ok=True,parents=True)
    bpy.ops.export_scene.fbx(filepath=str(output),object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',
        apply_unit_scale=True,path_mode='STRIP',bake_anim=False,add_leaf_bones=False)
    expected={o.name:topology(o.data) for o in bpy.context.scene.objects if o.type=='MESH' and (o.name.startswith('Trunk_Mesh') or o.name.startswith('Bark_'))}
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=str(output),use_anim=False)
    for obj in bpy.context.scene.objects:
        if obj.name in expected:
            t=topology(obj.data)
            assert t['manifold'] and t['boundary_edges']==0 and t['volume']>0,(file,obj.name,t)
    record['reimport']='PASS'; record['sha256']=hashlib.sha256(output.read_bytes()).hexdigest()
    reports.append(record)
    report.write_text(json.dumps(reports,indent=2))
    print('VERIFIED',len(reports),file.name,flush=True)
assert len(reports)==96
