import bpy, bmesh, math, random, json
from pathlib import Path
from mathutils import Vector
from math import sin,cos,pi
ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'work/LawnFence/UnityProject/Assets/Environment/Fences'
SOURCE=ROOT/'Sources/LawnFence';SOURCE.mkdir(parents=True,exist_ok=True)
PROOF=ROOT/'Verification/LawnFence';PROOF.mkdir(parents=True,exist_ok=True)
for d in ['Models','Colliders']: (ASSET/d).mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system='METRIC'
mats=[]
for name in ['Old','Heavy_Rust']:
    m=bpy.data.materials.new('MAT_Fence_'+name);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF')
    for folder,socket in [('Albedo','Base Color'),('Metallic','Metallic'),('Roughness','Roughness')]:
        tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ASSET/'Textures'/folder/(name+'_'+folder+'.png')))
        if folder!='Albedo':tex.image.colorspace_settings.name='Non-Color'
        m.node_tree.links.new(tex.outputs['Color'],bs.inputs[socket])
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ASSET/'Textures/Normal'/(name+'_Normal.png')));tex.image.colorspace_settings.name='Non-Color'
    normal=m.node_tree.nodes.new('ShaderNodeNormalMap');m.node_tree.links.new(tex.outputs['Color'],normal.inputs['Color']);m.node_tree.links.new(normal.outputs[0],bs.inputs['Normal'])
    mats.append(m)

def top(x):return .43+.085*sin(pi*x/1.8)
def deformation(p,kind):
    x,y,z=p
    if kind=='Bent':
        w=sin(pi*max(0,min(1,x/1.8)))**2
        y+=.29*w*max(.2,z/.52);z-=.055*w
    if kind=='Broken_Rods':y+=.035*sin(pi*x/1.8)**2*(z/.52)
    if kind=='Destroyed':
        # Panel collapsed towards the lawn about its bottom rail.
        h=z-.105;y+=h*.945;z=.055+h*.327
        z+=.015*sin(x*5);y+=.06*sin(pi*x/1.8)
    return Vector((x,y,z))

class Mesh:
    def __init__(self):self.v=[];self.f=[];self.uv=[];self.mi=[]
    def tube(self,path,r,n=16,mat=0,seed=1,vertical=False,fracture=False):
        rr=random.Random(seed);path=[Vector(p) for p in path];base=len(self.v)
        phase=rr.random()*6;u0=rr.uniform(.025,.85);v0=rr.uniform(.15,.32)
        lengths=[0]
        for a,b in zip(path,path[1:]):lengths.append(lengths[-1]+(b-a).length)
        total=max(lengths[-1],.00001)
        for k,p in enumerate(path):
            tangent=(path[min(k+1,len(path)-1)]-path[max(0,k-1)]).normalized()
            ref=Vector((0,1,0)) if abs(tangent.y)<.9 else Vector((1,0,0))
            a=tangent.cross(ref).normalized();b=tangent.cross(a).normalized()
            for j in range(n+1):
                ang=2*pi*(j%n)/n
                rad=r*(1+.025*sin(ang*3+k*.9+phase))
                v=p+rad*(cos(ang)*a+sin(ang)*b)
                if fracture and k==len(path)-1:v+=tangent*(.0015*sin((j%n)*2.7+phase)+.0012*cos((j%n)*4.1))
                self.v.append(tuple(v));self.uv.append((u0+.085*j/n,(.045+.91*lengths[k]/total) if vertical else v0+.48*lengths[k]/total))
        for k in range(len(path)-1):
            for j in range(n):
                a=base+k*(n+1)+j;b=a+n+1
                self.f.append((a,a+1,b+1,b));self.mi.append(mat)
        # Caps share seam duplicates which are welded before topology checks.
        self.f.append(tuple(base+j for j in range(n-1,-1,-1)));self.mi.append(mat)
        last=base+(len(path)-1)*(n+1)
        self.f.append(tuple(last+j for j in range(n)));self.mi.append(1 if fracture else mat)
    def object(self,name):
        me=bpy.data.meshes.new(name);me.from_pydata(self.v,[],self.f);me.update()
        ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
        for m in mats:me.materials.append(m)
        uv=me.uv_layers.new(name='UV0')
        for p,idx in zip(me.polygons,self.mi):
            p.material_index=idx;p.use_smooth=len(p.vertices)==4
            for li in p.loop_indices:uv.data[li].uv=self.uv[me.loops[li].vertex_index]
        bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000005);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free();me.update()
        return ob

def paths(kind,lod):
    rr=random.Random(391)
    steps=[48,24,12][lod];vs=[12,7,4][lod]
    parts=[]
    heavy=kind in ['Heavy_Rust','Destroyed']
    mat=int(heavy)
    # Upper rail, with an opened, ragged fracture in the collapsed panel.
    ranges=[(0,1.8)] if kind!='Destroyed' else [(0,.78),(.82,1.8)]
    for a,b in ranges:
        ps=[deformation((a+(b-a)*i/steps,0,top(a+(b-a)*i/steps)),kind) for i in range(steps+1)]
        if kind=='Destroyed' and a>.8:
            ps=[p+Vector((0,.04*sin((p.x-a)/(b-a)*pi),-.018*sin((p.x-a)/(b-a)*pi))) for p in ps]
        parts.append((ps,.0075,mat,False,kind=='Destroyed'))
    parts.append(([deformation((1.8*i/steps,0,.115+.008*sin(i/steps*pi) if kind=='Bent' else .115),kind) for i in range(steps+1)],.007,mat,False,False))
    for j,x in enumerate([.235,.492,.751,1.025,1.307,1.568]):
        if kind=='Broken_Rods' and j==2:continue
        lo=.115;hi=top(x);partial=kind=='Broken_Rods' and j==4
        if partial:hi=.275
        ps=[]
        for k in range(vs+1):
            t=k/vs;xx=x+.0015*sin(pi*t)*sin(2*pi*t+j)+.004*sin(j*4)*t
            y=.003*sin(t*pi+j)
            if kind=='Broken_Rods' and j==1:y+=.09*sin(t*pi/2);xx+=.025*sin(t*pi)
            if kind=='Bent':y+=.035*sin(pi*t)*sin(j*2);xx+=.027*sin(pi*t)*cos(j*1.7)
            if kind=='Destroyed':y+=.055*sin(pi*t+j);xx+=.04*sin(pi*t)*sin(j*2)
            ps.append(deformation((xx,y,lo+(hi-lo)*t),kind))
        radius=.005*(.85 if kind=='Heavy_Rust' and j in [1,4] else 1)
        parts.append((ps,radius,mat,True,partial))
        # Rough weld collars follow the bar axis, including torn joint residues.
        if lod<2:
            for end in ([0] if partial else [0,1]):
                p=ps[0 if end==0 else -1];tangent=(ps[1]-ps[0] if end==0 else ps[-1]-ps[-2]).normalized()
                a=tangent.cross(Vector((0,1,0))).normalized();b=tangent.cross(a).normalized()
                ring=[]
                for k in range(25 if lod==0 else 13):
                    ang=2*pi*k/(24 if lod==0 else 12);rad=.0063*(1+.12*sin(ang*5+j))
                    ring.append(p+tangent*.003+rad*(a*cos(ang)+b*sin(ang)))
                parts.append((ring,.0018 if not heavy else .0024,1 if heavy or kind=='Broken_Rods' else 0,False,False))
    if kind=='Heavy_Rust':
        # Opened corrosion hole: two slender tips no longer meet at one bar base.
        parts=[p for p in parts if not (p[3] and abs(p[0][0].x-.492)<.01)]
        parts.append(([deformation((.492,0,z),kind) for z in [.15,.22,.35,top(.492)]],.004,1,True,True))
        parts.append(([Vector((.492,0,.115)),Vector((.491,0,.131))],.0045,1,True,True))
    if kind=='Destroyed':
        parts.append(([Vector((.65,.62,.008)),Vector((.77,.64,.012)),Vector((.89,.69,.019))],.005,1,False,True))
        parts.append(([Vector((1.48,.34,.01)),Vector((1.55,.43,.026)),Vector((1.61,.52,.012))],.0075,1,False,True))
    return parts

def select(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
def export(objects,path):
    select(objects)
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='STRIP',use_custom_props=True)

manifest=[]
for kind in ['Intact','Broken_Rods','Bent','Heavy_Rust','Destroyed']:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    obs=[];entry={'name':'Fence_'+kind,'lods':[]}
    for lod in range(3):
        mesh=Mesh()
        for i,(ps,r,mat,vertical,fracture) in enumerate(paths(kind,lod)):
            mesh.tube(ps,r,[16,10,6][lod],mat,100+i,vertical,fracture)
        ob=mesh.object('Fence_'+kind+'_LOD'+str(lod));obs.append(ob)
        ob.data.calc_loop_triangles();entry['lods'].append({'triangles':len(ob.data.loop_triangles),'vertices':len(ob.data.vertices)})
    # Low poly physical silhouette, never a solid box across the open rails.
    cm=Mesh()
    for i,(ps,r,mat,vertical,fracture) in enumerate(paths(kind,2)):
        cm.tube(ps,r,6,mat,i,vertical,fracture)
    collider=cm.object('COL_Fence_'+kind)
    export([collider],ASSET/'Colliders'/('COL_Fence_'+kind+'.fbx'))
    collider.hide_render=True;collider.hide_viewport=True
    export(obs,ASSET/'Models'/('Fence_'+kind+'.fbx'))
    # Authoring file contains LODs; render only LOD0 by default.
    for o in obs[1:]:o.hide_render=True;o.hide_set(True)
    select([obs[0]])
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/('Fence_'+kind+'.blend')))
    manifest.append(entry)

bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
obs=[]
for lod in range(3):
    me=Mesh();me.tube([(0,0,-.075),(0,0,.0),(0,0,.43),(0,0,.465)],.013,[20,12,8][lod],0,67,True)
    obs.append(me.object('Fence_Post_LOD'+str(lod)))
export(obs,ASSET/'Models/Fence_Post.fbx')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Fence_Post.blend'))

# A genuinely clean re-import for every FBX: topology, UVs, finite coordinates, scale.
report=[]
for path in sorted(list((ASSET/'Models').glob('*.fbx'))+list((ASSET/'Colliders').glob('*.fbx'))):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];checks=[]
    for ob in meshes:
        me=ob.data;bm=bmesh.new();bm.from_mesh(me)
        boundary=sum(1 for e in bm.edges if not e.is_manifold)
        finite=all(math.isfinite(c) for v in me.vertices for c in v.co)
        assert len(me.uv_layers)>0 and len(me.polygons)>0 and boundary==0 and finite,(path.name,ob.name,boundary)
        dims=[max((ob.matrix_world@v.co)[i] for v in me.vertices)-min((ob.matrix_world@v.co)[i] for v in me.vertices) for i in range(3)]
        checks.append({'mesh':ob.name,'dimensions_m':dims,'nonmanifold_edges':boundary,'signed_volume':bm.calc_volume(signed=True),'uv':True});bm.free()
    assert meshes
    report.append({'fbx':path.name,'meshes':checks})
(PROOF/'blender_reimport.json').write_text(json.dumps(report,indent=2))
(PROOF/'geometry.json').write_text(json.dumps(manifest,indent=2))
print('LAWN_FENCE_BLENDER_REIMPORT_PASS')
