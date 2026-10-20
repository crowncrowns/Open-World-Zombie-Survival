"""Native branch-and-needle evergreen landmark visible in the interior references."""
from pathlib import Path
import bpy,bmesh,math,json,random
from mathutils import Vector
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit7';A=W/'authoring';OUT=A/'Assets/Environment/KursHub/Props'
bpy.ops.wm.read_factory_settings(use_empty=True);rng=random.Random(71107)
palette=json.loads((A/'Assets/Environment/KursHub/KursHubManifest.json').read_text(encoding='utf8'))['palette']
mats={}
for p in palette:
    mat=bpy.data.materials.new(p['name']);mat.diffuse_color=p['rgba'];mat.use_nodes=True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=p['rgba'];mats[p['name']]=mat
coll=bpy.data.collections.new('KH_AtriumFir');bpy.context.scene.collection.children.link(coll)
offset=Vector((26,130,0));coll.instance_offset=offset
objects=[]
def xyz(p):return Vector((p[0],p[2],p[1]))
def mesh(name,vs,fs,material,collision=False):
    me=bpy.data.meshes.new(name);me.from_pydata([xyz(v) for v in vs],[],fs);me.update()
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update();assert all(e.is_manifold for e in bm.edges),name;assert bm.calc_volume(signed=True)>1e-12,name;bm.to_mesh(me);bm.free()
    o=bpy.data.objects.new(name,me);coll.objects.link(o);o.location=offset;me.materials.append(mats[material]);o['unity_collision']=collision;o['Audit7Added']='Reference evergreen: individual branches, needles and wrapped lights'
    uv=me.uv_layers.new(name='MetreUV')
    for f in me.polygons:
        f.use_smooth=True
        axes=[i for i in range(3) if i!=max(range(3),key=lambda j:abs(f.normal[j]))]
        for li in f.loop_indices:
            p=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=(p[axes[0]]/2,p[axes[1]]/2)
    objects.append(o);return o
def tube(name,a,b,r,material='TimberRaw',n=7,collision=False,r2=None):
    a,b=Vector(a),Vector(b);d=(b-a).normalized();u=d.cross(Vector((0,1,0)))
    if u.length<.01:u=Vector((1,0,0))
    u.normalize();v=d.cross(u).normalized();r2=r if r2 is None else r2
    vs=[tuple(c+(u*math.cos(math.tau*i/n)+v*math.sin(math.tau*i/n))*rad) for c,rad in ((a,r),(b,r2)) for i in range(n)]
    fs=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vs,fs,material,collision)
def needle_batch(name,needles):
    verts=[];faces=[]
    for a,b in needles:
        a,b=Vector(a),Vector(b);d=(b-a).normalized();u=d.cross(Vector((0,1,0)))
        if u.length<.1:u=Vector((1,0,0))
        u.normalize();v=d.cross(u).normalized();i=len(verts)
        verts += [tuple(a+u*.0008),tuple(a-u*.0008+v*.0007),tuple(a-u*.0008-v*.0007),tuple(b)]
        faces += [(i,i+2,i+1),(i,i+1,i+3),(i+1,i+2,i+3),(i+2,i,i+3)]
    return mesh(name,verts,faces,'LeafGreen')
tube('KH_AtriumFir_Trunk',(0,0,0),(0,3.35,0),.085,n=14,collision=True,r2=.016)
# Low dark foliage is made from separate needle fans; no solid cone at LOD0.
for tier in range(7):
    height=.42+tier*.40;length=1.02*(1-tier/8.1)
    for branch in range(6):
        angle=branch*math.tau/6+tier*.53+rng.uniform(-.12,.12);direction=Vector((math.cos(angle),0,math.sin(angle)))
        a=Vector((0,height+.13,0));tip=a+direction*length+Vector((0,-.14+length*.05,0))
        tube(f'KH_AtriumFir_Branch_{tier}_{branch}',a,tip,.016*(1-tier/9),n=6,r2=.004)
        needles=[]
        for shoot in range(5):
            t=.25+shoot*.14;base=a.lerp(tip,t);lateral=Vector((-direction.z,0,direction.x))
            side=-1 if shoot%2 else 1;end=base+direction*(.21*length)+lateral*(side*.22*length)+Vector((0,.045,0))
            tube(f'KH_AtriumFir_Shoot_{tier}_{branch}_{shoot}',base,end,.003,'TimberRaw',5,r2=.0015)
            forward=(end-base).normalized();cross=forward.cross(Vector((0,1,0))).normalized()
            for card in (0,1):
                sidevec=cross if card==0 else Vector((0,1,0));normal=forward.cross(sidevec).normalized()
                wide=.10*length+.032;back=base-forward*.015;front=end+forward*.09
                vertices=[tuple(c+sidevec*s*wide+normal*h) for h in (-.0005,.0005) for c,s in ((back,-1),(back,1),(front,1),(front,-1))]
                frond=mesh(f'KH_AtriumFir_Frond_{tier}_{branch}_{shoot}_{card}',vertices,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],'FirNeedles')
                for face in frond.data.polygons:
                    for li in face.loop_indices:
                        vi=frond.data.loops[li].vertex_index%4
                        frond.data.uv_layers.active.data[li].uv=[(0,0),(1,0),(1,1),(0,1)][vi]
            for j in range(12):
                point=base.lerp(end,(j+.35)/12);flank=lateral*(-1 if j%2 else 1)
                vector=(flank*.7+direction*.45+Vector((0,.20,0))).normalized()
                needles.append((point,point+vector*rng.uniform(.025,.047)))
        # One source object per branch remains easy to select and edit.
        needle_batch(f'KH_AtriumFir_Needles_{tier}_{branch}',needles)
points=[]
for i in range(97):
    t=i/96;y=.50+2.62*t;radius=max(.06,1.03*(1-y/3.5));angle=t*math.tau*4
    points.append((radius*math.cos(angle),y,radius*math.sin(angle)))
for i,(a,b) in enumerate(zip(points,points[1:])):tube(f'KH_AtriumFir_LightCord_{i}',a,b,.003,'Frame',5)
for i in range(0,len(points),3):
    p=Vector(points[i]);tube(f'KH_AtriumFir_Bulb_{i}',p,p+Vector((0,-.045,0)),.018,'LampGlass',8,r2=.014)
# Source offset is only a prototype display position. Instances subtract it.
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene['Authoring']='Branch/needle evergreen from interior refs; source units metres'
source=W/'extras/KursHubAtriumFir.blend';source.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(source))
chunks=[];levels=[]
for level in range(3):
    ec=bpy.data.collections.new('EXPORT'+str(level));bpy.context.scene.collection.children.link(ec);exports=[]
    groups={}
    for o in objects:
        # Successive levels retain deterministic branch/needle subsets.
        if level and '_Needles_' in o.name:
            parts=o.name.rsplit('_',2);tier,branch=map(int,parts[-2:])
            if (branch+tier)% (2 if level==1 else 3):continue
        if level==2 and '_Shoot_' in o.name:continue
        if level==2 and '_Frond_' in o.name and int(o.name.rsplit('_',2)[-2])%2:continue
        if level and '_Bulb_' in o.name and int(o.name.rsplit('_',1)[1])%(6 if level==1 else 12):continue
        mat=o.data.materials[0].name;key=(mat,bool(o['unity_collision']));groups.setdefault(key,[]).append(o)
    for (mat,collision),group in sorted(groups.items()):
        vs=[];fs=[];uvs=[];sm=[]
        for o in group:
            start=len(vs);vs.extend(tuple(v.co) for v in o.data.vertices)
            for f in o.data.polygons:
                fs.append(tuple(start+i for i in f.vertices));sm.append(f.use_smooth)
                uvs.extend(tuple(o.data.uv_layers.active.data[i].uv) for i in f.loop_indices)
        name='KH_AtriumFir_'+mat+('_C' if collision else '_NC');me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.materials.append(mats[mat]);uv=me.uv_layers.new(name='MetreUV')
        for i,value in enumerate(uvs):uv.data[i].uv=value
        for f,s in zip(me.polygons,sm):f.use_smooth=s
        o=bpy.data.objects.new(name,me);ec.objects.link(o);exports.append(o)
        if level==0:chunks.append(dict(name=name,material=mat,collider=collision))
    root=bpy.data.objects.new('KH_AtriumFir_Root',None);ec.objects.link(root)
    bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
    for o in exports:o.parent=root;o.select_set(True)
    path=OUT/'Models'/('KH_AtriumFir.fbx' if level==0 else f'LOD/KH_AtriumFir_LOD{level}.fbx');path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
    levels.append(dict(level=level,triangles=sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in exports),model=path.relative_to(A).as_posix()))
    for o in exports+[root]:bpy.data.objects.remove(o,do_unlink=True)
allvs=[v.co for o in objects for v in o.data.vertices]
mins=[min(v[i] for v in allvs) for i in (0,2,1)];maxs=[max(v[i] for v in allvs) for i in (0,2,1)]
prop=dict(id='KH_AtriumFir',collection='KH_AtriumFir',sourceOffset=[26,0,130],origin='Floor centre; 3.35m evergreen with needle fans, cutout fronds, wrapped light cable and bulbs',model='Assets/Environment/KursHub/Props/Models/KH_AtriumFir.fbx',chunks=chunks,lods=levels,triangles=levels[0]['triangles'],boundsMin=mins,boundsMax=maxs)
(W/'extras/fir_manifest.json').write_text(json.dumps(prop,indent=2),encoding='utf8');print('ATRIUM_FIR_EXPORTED',levels,flush=True)
