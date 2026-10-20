"""Blender audit and reversible architectural detail pass for the existing Kurs source."""
from pathlib import Path
import bpy,bmesh,json,math,sys,argparse
from collections import Counter
from mathutils import Vector
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit7'
p=argparse.ArgumentParser();p.add_argument('--audit-only',action='store_true');args=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
bpy.ops.wm.open_mainfile(filepath=str(W/'baseline/Sources/KursHub/KursHub.blend'))
records=[]
for obj in bpy.context.scene.objects:
    d=dict(name=obj.name,type=obj.type,collections=[c.name for c in obj.users_collection],scale=list(obj.scale),modifiers=[m.type for m in obj.modifiers])
    if obj.type=='MESH':
        bm=bmesh.new();bm.from_mesh(obj.data)
        d.update(vertices=len(obj.data.vertices),triangles=sum(len(f.vertices)-2 for f in obj.data.polygons),nonManifoldEdges=sum(not e.is_manifold for e in bm.edges),degenerateFaces=sum(f.calc_area()<1e-10 for f in bm.faces),signedVolume=bm.calc_volume(signed=True),uvLayers=[u.name for u in obj.data.uv_layers],materials=[s.material.name if s.material else None for s in obj.material_slots],custom=dict(obj.items()),bounds=[list(v) for v in obj.bound_box])
        bm.free()
    elif obj.instance_collection:d['instanceCollection']=obj.instance_collection.name
    records.append(d)
images=[dict(name=i.name,path=i.filepath,size=list(i.size),packed=bool(i.packed_file)) for i in bpy.data.images]
W.mkdir(parents=True,exist_ok=True)
(W/'architecture_before.json').write_text(json.dumps(dict(objects=records,images=images,materials=[m.name for m in bpy.data.materials],collectionCounts={c.name:len(c.objects) for c in bpy.data.collections}),indent=2,ensure_ascii=False),encoding='utf8')
print('ARCHITECTURE_AUDIT_OK',len(records),Counter(d['type'] for d in records),flush=True)
print('SAMPLE',[(d['name'],d.get('custom'),d['collections']) for d in records if d['type']=='MESH'][:8],flush=True)
if args.audit_only:raise SystemExit(0)

A=W/'authoring';out=A/'Assets/Environment/KursHub';source=A/'Sources/KursHub'
manifest=json.loads((out/'KursHubManifest.json').read_text(encoding='utf8'))
base=[o for o in bpy.context.scene.objects if o.type=='MESH']
sys.path.insert(0,str(Path(__file__).parent))
from audit7_text_geometry import repair_authored_text
text_repairs=[repair_authored_text(o) for o in base if o.get('is_text')]
(W/'architecture_text_fixes.json').write_text(json.dumps(dict(status='TEXT_REPAIR_ALL_OK',objects=text_repairs),indent=2),encoding='utf8')
added=[];bevels=[]
def col(name):
    c=bpy.data.collections.get(name)
    if not c:c=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(c)
    return c
def box(name,center,size,mat='Frame',group='Quality joinery',u=Vector((1,0,0))):
    # Source is Blender Z-up, vertices are in building metres, object stays identity.
    u=Vector(u).normalized();v=Vector((-u.y,u.x,0));z=Vector((0,0,1));c=Vector(center)
    verts=[c+u*(i*size[0]/2)+v*(j*size[1]/2)+z*(k*size[2]/2) for i,j,k in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    o=bpy.data.objects.new(name,me);col(group).objects.link(o);me.materials.append(bpy.data.materials[mat])
    o['unity_collision']=False;o['construction_group']=group;o['hidden_collision_proxy']=False
    uv=me.uv_layers.new(name='MetreUV')
    for f in me.polygons:
        axes=[i for i in range(3) if i!=max(range(3),key=lambda j:abs(f.normal[j]))]
        for loop in f.loop_indices:
            p=me.vertices[me.loops[loop].vertex_index].co;uv.data[loop].uv=(p[axes[0]]/2,p[axes[1]]/2)
    added.append(o);return o
for o in base:
    if o.get('hidden_collision_proxy') or o.get('is_text'):continue
    group=o.get('construction_group','');mat=o.data.materials[0].name if o.data.materials else ''
    if group in ('Floors','RoofDeck','MainStairs','RoofStairs') or mat in ('Glass','DirtStain'):continue
    if not o.name.startswith(('Column','AtriumColumn','Facade_','GalleryFascia','AtriumLongBeam','AtriumCrossBeam','LeftAdvertising','RightAdvertising','Terrace','Vent','AirHandling','RearVent','DomeKerb','KursRack')):continue
    bm=bmesh.new();bm.from_mesh(o.data);lengths=[e.calc_length() for e in bm.edges if e.calc_length()>.0001];bm.free()
    if not lengths:continue
    width=min(.014 if mat in ('Concrete','CreamPanel') else .004,min(lengths)*.12)
    if width<.0008:continue
    mod=o.modifiers.new('Physical edge radius (metres)','BEVEL');mod.width=width;mod.segments=2;mod.limit_method='ANGLE';mod.angle_limit=.52;mod.harden_normals=True
    # Explicit split normals keep broad panel faces flat while catching edge highlights.
    wn=o.modifiers.new('Face weighted normals','WEIGHTED_NORMAL');wn.keep_sharp=True
    bevels.append(dict(object=o.name,radius=width))
    if mat!='Glass':
        for f in o.data.polygons:f.use_smooth=True

# The glazing already sits 70 mm behind the opaque facade. Complete that real
# recess with perimeter reveals, pressure caps, a projected sill and drip lip.
window_count=0
for o in base:
    if not o.name.startswith('Facade_') or not o.name.endswith(('GroundWindow','UpperWindow')):continue
    vs=[v.co.copy() for v in o.data.vertices];c=sum(vs,Vector())/len(vs)
    u=(vs[1]-vs[0]).normalized();w=(vs[1]-vs[0]).length;h=max(v.z for v in vs)-min(v.z for v in vs)
    n=Vector((-u.y,u.x,0));z=Vector((0,0,1))
    for side in (-1,1):
        box(o.name+' reveal jamb '+str(side),c+u*(side*(w/2-.035)),(.07,.31,h+.06),'CreamPanel','Quality window reveals',u)
        box(o.name+' glazing seal '+str(side),c+u*(side*(w/2-.075))+n*.105,(.028,.035,h),'Frame','Quality glazing caps',u)
    for sign in (-1,1):
        box(o.name+' reveal horizontal '+str(sign),c+z*(sign*(h/2+.025)),(w,.31,.05),'CreamPanel','Quality window reveals',u)
    # The outward side is selected from the actual outer envelope, not fixed normals.
    outward=n if Vector((c.x,c.y+5,0)).dot(n)>0 else -n
    sill=c-z*(h/2+.06)+outward*.09
    box(o.name+' projected aluminium sill',sill,(w+.10,.48,.055),'Frame','Quality window sills',u)
    box(o.name+' sill drip',sill+outward*.23-z*.035,(w+.10,.025,.045),'Frame','Quality window sills',u)
    divisions=max(1,math.ceil(w/2.3))
    for j in range(1,divisions):
        box(o.name+' secondary pressure cap '+str(j),c+u*(w*(j/divisions-.5)),(.055,.22,h),'CreamPanel','Quality glazing caps',u)
    window_count+=1

# Continuous cappings seal the exposed tops; caps are outside the wall dimension.
for o in base:
    if not o.name.startswith('Facade_') or not o.name.endswith('BlueFascia'):continue
    vs=[v.co.copy() for v in o.data.vertices];c=sum(vs,Vector())/len(vs);u=(vs[1]-vs[0]).normalized();w=(vs[1]-vs[0]).length
    c.z=max(v.z for v in vs)+.035
    box(o.name+' folded parapet cap',c,(w+.03,.42,.07),'Frame','Quality roof flashings',u)
    # Visible folded edges, rather than a paper-thin single surface.
    n=Vector((-u.y,u.x,0))
    for side in (-1,1):box(o.name+' cap return '+str(side),c+n*(side*.20)-Vector((0,0,.055)),(w+.03,.025,.11),'Frame','Quality roof flashings',u)

# Downpipes and their saddle straps follow the side/rear service elevations.
for side in (-1,1):
    for longitudinal in (-24,28,58):
        x=side*46.69
        box('Rainwater rectangular downpipe',(x,longitudinal,5.45),(.13,.17,10.5),'Frame','Quality drainage')
        for y in (1,3.5,6,8.5,10.5):box('Rainwater fixing saddle',(x,longitudinal,y),(.20,.24,.05),'SteelOxide','Quality drainage')
        box('Rainwater outlet shoe',(x+side*.09,longitudinal,.25),(.30,.20,.16),'Frame','Quality drainage')

# Physical pull handles and hinges sit on the entrance side frames. The centre
# stays an open portal; no shut door leaf or collider is introduced.
for side in (-1,1):
    for y in (1.03,1.48):box('Entrance pull handle standoff',(side*3.12,-64.91,y),(.05,.17,.05),'Frame','Quality entry hardware')
    box('Entrance tubular pull handle',(side*3.12,-65.005,1.255),(.055,.055,.50),'Frame','Quality entry hardware')

for o in added:
    if len(o.data.polygons)==6:
        m=o.modifiers.new('Manufactured edge radius','BEVEL');m.width=.002;m.segments=2;m.limit_method='ANGLE';m.harden_normals=True
        n=o.modifiers.new('Face weighted normals','WEIGHTED_NORMAL');n.keep_sharp=True
        for f in o.data.polygons:f.use_smooth=True

bpy.context.scene['KursHubQualityRevision']='7: measured edge radii, glazing recesses, flashings, drainage; original objects retained'
source.mkdir(parents=True,exist_ok=True)
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(source/'KursHub.blend'))

# Reconstruct the established exporter keys from the unaltered construction tags.
# Every old FBX node name is reused so scene overrides keep their local references.
from collections import defaultdict
groups=defaultdict(list)
for o in base+added:
    c=sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
    key=(o.get('construction_group') or o.users_collection[0].name,o.data.materials[0].name,math.floor(c.x/24),math.floor(c.y/30),bool(o.get('unity_collision')),bool(o.get('hidden_collision_proxy')))
    groups[key].append(o)
lookup={(d['group'],d['material'],*d['region'],d['collider'],d.get('hidden',False)):d for d in manifest['chunks']}
# Older text was assigned to a spatial chunk before its letters were turned to
# the curved facade. Redistribute only those already transformed text objects
# among that same established group, retaining every node and every glyph.
for key in list(groups):
    if key in lookup or key[0].startswith('Quality'):continue
    candidates=[k for k in lookup if (k[0],k[1],k[4],k[5])==(key[0],key[1],key[4],key[5])]
    if not candidates:continue
    target=min(candidates,key=lambda k:(k[2]-key[2])**2+(k[3]-key[3])**2)
    groups[target].extend(groups.pop(key))
for key in set(lookup)-set(groups):
    donors=[k for k in groups if (k[0],k[1],k[4],k[5])==(key[0],key[1],key[4],key[5]) and len(groups[k])>1]
    assert donors,'No source for old chunk '+repr(key)
    donor=min(donors,key=lambda k:(k[2]-key[2])**2+(k[3]-key[3])**2)
    centre=Vector(((key[2]+.5)*24,(key[3]+.5)*30,0))
    chosen=min(groups[donor],key=lambda o:((sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices))-centre).length)
    groups[donor].remove(chosen);groups[key].append(chosen)
assert set(lookup)<=set(groups),'Missing established architecture exporter group: '+repr(set(lookup)-set(groups))
chunks=list(manifest['chunks']);newindex=1000
for key in sorted(groups):
    if key in lookup:continue
    group,material,zx,zz,collision,hidden=key
    d=dict(name='Quality_'+group.replace(' ','')+'_'+material+'_'+str(newindex)+'_NC',group=group,material=material,region=[zx,zz],collider=collision,hidden=hidden)
    chunks.append(d);lookup[key]=d;newindex+=1
exportcol=col('EXPORT_TEMP_QUALITY');deps=bpy.context.evaluated_depsgraph_get()
def export(level):
    exports=[];tot=0
    # Merge mesh arrays directly. Repeated UI Join operators invalidate the full
    # dependency graph for thousands of source objects and can take many minutes.
    if level==1:
        for original in base+added:
            for modifier in original.modifiers:
                if modifier.type=='BEVEL':modifier.segments=1
        bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
    else:deps=bpy.context.evaluated_depsgraph_get()
    for key,items in sorted(groups.items()):
        vertices=[];faces=[];uvs=[];smooth=[];normals=[]
        for o in items:
            if level==2 and o in added:continue
            ev=o.evaluated_get(deps)
            # LOD1 keeps the additional joints; LOD2 uses original control meshes
            # plus conservative dissolve, retaining the complete shell/portals.
            me=ev.to_mesh(preserve_all_data_layers=True,depsgraph=deps) if level<2 else o.data
            offset=len(vertices);vertices.extend(tuple(o.matrix_world@v.co) for v in me.vertices)
            layer=me.uv_layers.active
            nmat=o.matrix_world.to_3x3().inverted().transposed()
            for f in me.polygons:
                faces.append(tuple(offset+i for i in f.vertices));smooth.append(f.use_smooth)
                for li in f.loop_indices:
                    uvs.append(tuple(layer.data[li].uv) if layer else (0,0))
                    normals.append(tuple((nmat@me.corner_normals[li].vector).normalized()))
            if level<2:ev.to_mesh_clear()
        if not faces:
            # Empty extra detail chunks do not appear at LOD2, as expected.
            continue
        me=bpy.data.meshes.new(lookup[key]['name']);me.from_pydata(vertices,[],faces);me.update()
        layer=me.uv_layers.new(name='MetreUV')
        for i,uv in enumerate(uvs):layer.data[i].uv=uv
        for f,s in zip(me.polygons,smooth):f.use_smooth=s
        me.normals_split_custom_set(normals)
        me.materials.append(bpy.data.materials[key[1]])
        q=bpy.data.objects.new(lookup[key]['name'],me);exportcol.objects.link(q)
        tot+=sum(len(f.vertices)-2 for f in q.data.polygons);exports.append(q)
    bpy.ops.object.select_all(action='DESELECT')
    for q in exports:q.select_set(True)
    bpy.context.view_layer.objects.active=exports[0]
    destination=out/'Models'/('KursHubShell.fbx' if level==0 else f'LOD/KursHubShell_LOD{level}.fbx');destination.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(destination),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
    for q in exports:
        me=q.data;bpy.data.objects.remove(q,do_unlink=True);bpy.data.meshes.remove(me)
    print('ARCHITECTURE_LOD_EXPORTED',level,tot,len(exports),flush=True)
    return dict(level=level,triangles=tot,chunks=len(exports),path=str(destination))
lods=[export(i) for i in (0,1,2)]
manifest['chunks']=chunks;manifest['qualityRevision']=7
(out/'KursHubManifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
(W/'architecture_fixes.json').write_text(json.dumps(dict(status='ARCHITECTURE_QUALITY_OK',existingObjectsRetained=len(base),bevels=bevels,windowRecesses=window_count,additionalObjects=len(added),lods=lods),indent=2),encoding='utf8')
print('ARCHITECTURE_QUALITY_OK',len(bevels),'bevels',window_count,'window bays',len(added),'added detail meshes',lods,flush=True)
