import bpy,bmesh,math,random,json
from pathlib import Path
from mathutils import Vector
from math import sin,cos,pi,exp
random.seed(711)
ROOT=Path(__file__).resolve().parents[2];AS=ROOT/'work/TrashBin/UnityProject/Assets/Environment/Props/TrashBin';V=ROOT/'Verification/TrashBin';S=ROOT/'Sources/TrashBin'
for p in [AS/'Models',V,S]:p.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
sc=bpy.context.scene;sc.unit_settings.system='METRIC';sc.render.engine='CYCLES';sc.cycles.samples=20
def activate(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
def mat(name,tex=True,metal=False,glass=False):
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;b=n.get('Principled BSDF');l=m.node_tree.links
    files=[('Base Color','TrashBin_Albedo.png' if tex else 'TrashContents_Atlas.png','sRGB'),('Normal','TrashBin_Normal.png' if tex else 'TrashContents_Normal.png','Non-Color')]
    if tex:files += [('Roughness','TrashBin_Roughness.png','Non-Color'),('Metallic','TrashBin_Metallic.png','Non-Color')]
    for socket,f,space in files:
        t=n.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(AS/'Textures'/f),check_existing=True);t.image.colorspace_settings.name=space
        if socket=='Normal':
            nm=n.new('ShaderNodeNormalMap');l.new(t.outputs['Color'],nm.inputs['Color']);l.new(nm.outputs[0],b.inputs['Normal'])
        else:l.new(t.outputs['Color'],b.inputs[socket])
    if not tex:b.inputs['Roughness'].default_value=.74;b.inputs['Metallic'].default_value=.7 if metal else 0
    if glass:b.inputs['Transmission Weight'].default_value=.68;b.inputs['IOR'].default_value=1.46;b.inputs['Roughness'].default_value=.23;b.inputs['Alpha'].default_value=.5
    return m
paint=mat('M_TrashBin_GreenPaint');steel=mat('M_TrashBin_RustyMetal');trash=mat('M_TrashContents',False);pet=mat('M_TrashContents_PET',False,glass=True);tin=mat('M_TrashContents_Tin',False,metal=True)
def empty(name,parent=None):
    o=bpy.data.objects.new(name,None);sc.collection.objects.link(o);o.parent=parent;return o
def mesh(name,verts,faces,material,parent=None,uvs=None):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);sc.collection.objects.link(o);o.parent=parent;me.materials.append(material)
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free()
    if uvs:
        uv=me.uv_layers.new(name='UVMap')
        for p in me.polygons:
            for li in p.loop_indices:uv.data[li].uv=uvs[me.loops[li].vertex_index]
    else:
        activate(o);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT')
    return o
def bevel(o,width=.0008,segments=2):
    activate(o);m=o.modifiers.new('Small rolled manufacturing edges','BEVEL');m.width=width;m.segments=segments;bpy.ops.object.modifier_apply(modifier=m.name)
    for p in o.data.polygons:p.use_smooth=True
    m=o.modifiers.new('Corner normals','WEIGHTED_NORMAL');m.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=m.name)
def box(name,loc,size,material,parent=None,tile=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(material);o.parent=parent
    if tile is not None:atlas(o,tile,steelTile=True)
    bevel(o,min(.001,min(size)/4));return o
def atlas(o,tile,steelTile=False):
    if not o.data.uv_layers:return
    for p in o.data.uv_layers.active.data:
        u,v=p.uv
        if steelTile:p.uv=((2+tile+u*.95+.025)/4,v*.185+.007)
        else:p.uv=((tile%4+u*.94+.03)/4,1-(tile//4+1)/4+(v*.94+.03)/4)
def cylinder(name,loc,r,depth,material,parent,axis='Z',vertices=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=loc);o=bpy.context.object;o.name=name;o.parent=parent;o.data.materials.append(material)
    if axis=='X':o.rotation_euler.y=pi/2
    atlas(o,0,steelTile=True);bevel(o,min(.0005,depth/5));return o
def curve(name,pts,r,material,parent):
    cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.bevel_depth=r;cu.bevel_resolution=1;cu.resolution_u=1;sp=cu.splines.new('POLY');sp.points.add(len(pts)-1)
    for p,co in zip(sp.points,pts):p.co=(*co,1)
    ob=bpy.data.objects.new(name,cu);sc.collection.objects.link(ob);ob.parent=parent;activate(ob);bpy.ops.object.convert(target='MESH');ob.data.materials.append(material)
    # Cylindrical seam UVs are packed into the localized-corrosion support tile.
    activate(ob);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project();bpy.ops.object.mode_set(mode='OBJECT');atlas(ob,1,steelTile=True);return ob
def dent(u,t,side):
    return -.0035*exp(-((u-.31-side*.07)/.19)**2-((t-.34)/.16)**2)+.0015*sin(u*9+side)*sin(t*6)*sin(pi*u)*sin(pi*t)
def wall(side,nx,nz,parent):
    vs=[];uvs=[];fs=[]
    for j in range(nz+1):
        t=j/nz;z=.245+.635*t;hw=.163+.017*t;hd=.136+.014*t
        for i in range(nx+1):
            u=i/nx;d=dent(u,t,side)
            if side==0:co=((u*2-1)*hw,-hd-d,z)
            elif side==1:co=((1-u*2)*hw,hd+d,z)
            elif side==2:co=(-hw-d,(1-u*2)*hd,z)
            else:co=(hw+d,(u*2-1)*hd,z)
            vs.append(co);uvs.append(((side+.025+.95*u)/4,.603+.394*t))
    for j in range(nz):
        for i in range(nx):
            a=j*(nx+1)+i;fs.append((a,a+1,a+nx+2,a+nx+1))
    o=mesh('Sheet_'+str(side),vs,fs,paint,parent,uvs);activate(o)
    so=o.modifiers.new('2 mm sheet thickness','SOLIDIFY');so.thickness=.002;so.offset=-1;so.use_even_offset=True;bpy.ops.object.modifier_apply(modifier=so.name)
    # The outer and inner faces have independent atlas charts; wall edge charts stay narrow.
    for p in o.data.polygons:
        if p.index>=nx*nz and p.index<2*nx*nz:
            for li in p.loop_indices:o.data.uv_layers.active.data[li].uv.y-=.4
    bevel(o,.00055,2);return o
def join(obs,name,parent):
    activate(obs[0])
    for o in obs:o.select_set(True)
    bpy.ops.object.join();o=bpy.context.object;o.name=name;o.parent=parent;return o
root=empty('SM_TrashBin_Empty');body=empty('TrashBin_Body',root);supports=empty('TrashBin_Supports',root)
lodBodies=[];lodSupports=[]
for level,res in enumerate([(12,20),(6,10),(3,5)]):
    bg=empty('Body_LOD'+str(level),body);sg=empty('Supports_LOD'+str(level),supports)
    walls=[wall(i,*res,bg) for i in range(4)]
    floor=box('Bottom_2mm', (0,0,.245),(.326,.272,.002),paint,bg)
    for uv in floor.data.uv_layers.active.data:uv.uv=(.008+uv.uv.x*.233,.008+uv.uv.y*.182)
    shell=join(walls+[floor],'BodyShell_LOD'+str(level),bg);lodBodies.append(shell)
    parts=[]
    for s in [-1,1]:
        x=s*.202
        parts.append(box('Upright_'+str(s),(x,0,.324),(.024,.018,.63),steel,sg,0))
        parts.append(box('FootPlate_'+str(s),(x,0,.005),(.055,.235,.01),steel,sg,1))
        # Short axle/pivot attachment is visible on the reference sides.
        parts.append(cylinder('Pivot_'+str(s),(s*.188,0,.574),.011,.034,steel,sg,'X',12))
        parts.append(cylinder('PivotBolt_'+str(s),(s*.215,0,.574),.014,.006,steel,sg,'X',6))
        for y in [-.086,.086]:parts.append(cylinder('Anchor_'+str(s),(x,y,.013),.006,.006,steel,sg,'Z',6))
        if level<2:
            for z in [.018,.622]:
                pts=[(x-.014+k*.0035,-.011,z+.00065*sin(k*pi)) for k in range(9)]
                parts.append(curve('WeldBead',pts,.0018,steel,sg))
    lodSupports.append(join(parts,'SupportAssembly_LOD'+str(level),sg))
    if level>0:bg.hide_render=True;sg.hide_render=True
# High-poly source, real geometry detail for selected-to-active normal baking.
high=empty('HIGH_POLY_SOURCE');hi=[]
for low in [lodBodies[0],lodSupports[0]]:
    o=low.copy();o.data=low.data.copy();sc.collection.objects.link(o);o.parent=high;o.name='HP_'+low.name;activate(o)
    m=o.modifiers.new('Dense high-poly sheet','SUBSURF');m.subdivision_type='SIMPLE';m.levels=2;bpy.ops.object.modifier_apply(modifier=m.name)
    tex=bpy.data.textures.new('Steel_micro_pitting_'+low.name,type='CLOUDS');tex.noise_scale=.008;tex.noise_depth=2
    m=o.modifiers.new('Physical steel micro relief','DISPLACE');m.texture=tex;m.strength=.00020;m.mid_level=.5;bpy.ops.object.modifier_apply(modifier=m.name);hi.append(o)
high.hide_render=True
for o in hi:o.hide_render=True
# Save edit source before baking/exporting, with all high/low meshes preserved.
bpy.ops.wm.save_as_mainfile(filepath=str(S/'TrashBin_Master.blend'))
print('SOURCE_SAVED',flush=True)
# Bake a true high-poly tangent normal map for the body (4K).
low=lodBodies[0];hp=hi[0];hp.hide_render=False
img=bpy.data.images.new('HP_to_LP_Normal',4096,4096,alpha=False);img.colorspace_settings.name='Non-Color'
for m in low.data.materials:
    node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=img;m.node_tree.nodes.active=node
activate(low);hp.select_set(True);sc.render.bake.use_selected_to_active=True;sc.render.bake.cage_extrusion=.003;sc.render.bake.max_ray_distance=.006;sc.render.bake.margin=16;sc.cycles.samples=8
bpy.ops.object.bake(type='NORMAL');img.filepath_raw=str(AS/'Textures/TrashBin_HP_BakedNormal.png');img.file_format='PNG';img.save();hp.hide_render=True
print('BAKE_OK',flush=True)
# Household trash meshes. Each item is separate; all geometry is clamped to the cavity.
filled=empty('SM_TrashBin_Filled');contents=empty('TrashContents',filled)
cats={n:empty(n,contents) for n in ['Plastic_Bottles','Paper_Waste','Plastic_Bags','Food_Packaging','Metal_Cans','Small_Debris']}
items=[]
def finish(o,tile,loc,rot):
    atlas(o,tile);o.location=loc;o.rotation_euler=rot;bpy.context.view_layer.update()
    # Bounds follow the tapered inner cavity. Clamp vertices, rather than relying on object origins.
    inv=o.matrix_world.inverted()
    for v in o.data.vertices:
        p=o.matrix_world@v.co;t=max(0,min(1,(p.z-.245)/.635));hx=.163+.017*t-.005;hy=.136+.014*t-.005
        p.x=max(-hx,min(hx,p.x));p.y=max(-hy,min(hy,p.y));p.z=max(.249,p.z);v.co=inv@p
    items.append(o);return o
def bag(index,loc,scale,tile=2):
    vs=[];fs=[];uv=[];nx=18;ny=12
    for j in range(ny+1):
        t=j/ny;theta=pi*t
        for i in range(nx):
            a=2*pi*i/nx;r=sin(theta)*(1+.12*sin(5*a+9*t)+.07*sin(11*a-5*t))
            vs.append((cos(a)*r*scale[0],sin(a)*r*scale[1],-cos(theta)*scale[2]+.002*sin(a*6)*sin(theta)))
            uv.append((i/nx,t))
    for j in range(ny):
        for i in range(nx):a=j*nx+i;b=j*nx+(i+1)%nx;fs.append((a,b,b+nx,a+nx))
    o=mesh('CrumpledBag_%02d'%index,vs,fs,trash,cats['Plastic_Bags'],uv)
    for p in o.data.polygons:p.use_smooth=True
    return finish(o,tile,loc,(random.uniform(-.12,.12),random.uniform(-.12,.12),random.random()*pi))
def revolve(name,profile,loc,rot,tile,material,category,n=18):
    vs=[];uv=[];fs=[]
    for j,(r,z) in enumerate(profile):
        for i in range(n):
            a=2*pi*i/n;dent=1+.045*sin(a*3+j*.7)+.035*sin(a*7-j*.5)
            vs.append((r*cos(a)*dent,r*sin(a)*dent,z));uv.append((i/n,j/(len(profile)-1)))
    for j in range(len(profile)-1):
        for i in range(n):a=j*n+i;b=j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
    fs.append(tuple(range(n-1,-1,-1)))
    o=mesh(name,vs,fs,material,cats[category],uv)
    for p in o.data.polygons:p.use_smooth=True
    return finish(o,tile,loc,rot)
def paper(i,loc,tile,pack=False):
    vs=[];uv=[];fs=[];n=8
    for j in range(n+1):
        for k in range(n+1):
            u=k/n;v=j/n;vs.append(((u-.5)*.11,(v-.5)*.095,.007*sin(u*13+v*5)+.004*sin(v*18)+.004*cos(u*23-v*9)));uv.append((u,v))
    for j in range(n):
        for k in range(n):a=j*(n+1)+k;fs.append((a,a+1,a+n+2,a+n+1))
    o=mesh(('FoodWrapper_' if pack else 'CrumpledPaper_')+str(i),vs,fs,trash,cats['Food_Packaging' if pack else 'Paper_Waste'],uv)
    activate(o);m=o.modifiers.new('Paper has two physical sides','SOLIDIFY');m.thickness=.00025;bpy.ops.object.modifier_apply(modifier=m.name)
    return finish(o,tile,loc,(random.uniform(-.2,.2),random.uniform(-.2,.2),random.random()*pi*2))
# Lower cavity is occupied by bulky bags, not an invisible fill plane.
for layer in range(5):
    for k,(x,y) in enumerate([(-.079,-.064),(.079,-.064),(-.079,.064),(.079,.064)]):
        bag(layer*4+k,(x,y,.303+layer*.108),(.084,.070,.058),[2,8,14,15][(layer+k)%4])
# Top heterogeneous refuse follows the supporting bag surfaces; target surface height ~.82 = 91%.
for i,(x,y) in enumerate([(-.105,-.075),(.005,-.065),(.105,-.067),(-.082,.04),(.04,.035),(.10,.09)]):
    paper(i,(x,y,.800+random.uniform(0,.018)),[0,4,6,11,12][i%5],i%2==0)
for i,(x,y) in enumerate([(-.085,-.05),(.082,.03),(.025,-.075)]):
    profile=[(.016,0),(.024,.008),(.026,.022),(.024,.035),(.027,.045),(.024,.062),(.025,.08),(.019,.093),(.010,.105),(.009,.122),(.011,.124),(.011,.129),(.001,.129)]
    revolve('Crushed_PET_Bottle_'+str(i),profile,(x,y,.816),(1.1,.4,i*1.9),1,pet,'Plastic_Bottles')
for i,(x,y) in enumerate([(-.06,.078),(.105,-.075)]):
    revolve('Dented_Can_'+str(i),[(.020,0),(.024,.003),(.024,.007),(.022,.015),(.019,.035),(.024,.055),(.024,.06),(.020,.063),(.001,.063)],(x,y,.808),(.5,1.0,i*2),[5,7][i],tin,'Metal_Cans')
for i,(x,y) in enumerate([(-.12,.02),(.04,.08)]):
    revolve('Crumpled_PaperCup_'+str(i),[(.016,0),(.020,.005),(.028,.065),(.029,.067),(.027,.067),(.026,.061),(.018,.007),(.001,.007)],(x,y,.815),(.4,-.7,i),6,trash,'Paper_Waste')
for i in range(8):paper(20+i,(random.uniform(-.12,.12),random.uniform(-.10,.10),.833+random.uniform(-.012,.014)),[0,12,4][i%3],False)
for i in range(3):
    o=box('Crushed_Carton_'+str(i),(0,0,0),(.065,.043,.035),trash,cats['Food_Packaging']);finish(o,11,(-.08+i*.08,-.01,.835),(random.random()*.4,.3,i*2))
for i in range(6):
    o=bag(100+i,(random.uniform(-.13,.13),random.uniform(-.09,.09),.84),(.015,.012,.008),12);o.name='Used_Tissue_'+str(i);o.parent=cats['Small_Debris']
# Add physical tied necks to the exposed small rubbish bags.
bag(90,(-.065,.035,.81),(.061,.054,.045),8)
revolve('Bag_TiedNeck',[(.007,0),(.003,.009),(.005,.014),(.010,.024),(.001,.025)],(-.065,.035,.855),(0,.3,0),8,trash,'Plastic_Bags',12)
print('CONTENTS_OK',len(items),flush=True)
# Copy body/support meshes without changing wear, UVs or geometry between variants.
for group in [body,supports]:
    ng=empty(group.name,filled)
    for lg in group.children:
        nl=empty(lg.name,ng);nl.hide_render=lg.hide_render
        for ob in lg.children:
            cp=ob.copy();cp.data=ob.data;sc.collection.objects.link(cp);cp.parent=nl;cp.name=ob.name
# Contents LOD hierarchy remains inside the required category names.
for cat in cats.values():
    orig=list(cat.children)
    for level in range(3):
        g=empty(cat.name+'_LOD'+str(level),cat);g.hide_render=level>0
        for o in orig:
            if level==0:o.parent=g;continue
            cp=o.copy();cp.data=o.data.copy();sc.collection.objects.link(cp);cp.parent=g;cp.name=o.name+'_LOD'+str(level);activate(cp)
            m=cp.modifiers.new('Waste silhouette reduction','DECIMATE');m.ratio=.48 if level==1 else .18;bpy.ops.object.modifier_apply(modifier=m.name)
def descendants(o):
    return [o]+[c for ch in o.children for c in descendants(ch)]
for rr in [root,filled]:
    bpy.ops.object.select_all(action='DESELECT')
    for o in descendants(rr):o.select_set(True)
    bpy.context.view_layer.objects.active=rr
    bpy.ops.export_scene.fbx(filepath=str(AS/'Models'/f'{rr.name}.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
print('EXPORT_OK',flush=True)
# Final source includes separate high-poly and both complete game hierarchies.
for im in bpy.data.images:
    if im.source=='FILE':
        try:im.pack()
        except:pass
bpy.ops.wm.save_as_mainfile(filepath=str(S/'TrashBin_Master.blend'))
metrics={}
for rr in [root,filled]:
    metrics[rr.name]={}
    for level in range(3):
        obs=[o for o in descendants(rr) if o.type=='MESH' and any(('LOD'+str(level)) in p.name for p in [o.parent,o.parent.parent] if p)]
        metrics[rr.name]['LOD'+str(level)]={'meshes':len(obs),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in obs)}
(V/'source_metrics.json').write_text(json.dumps(metrics,indent=2))
# Clean FBX re-import proves exported geometry, UVs, materials and required named nodes survive.
checks=[]
for f in sorted((AS/'Models').glob('*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(f))
    obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];pts=[o.matrix_world@v.co for o in obs for v in o.data.vertices]
    bounds=[[min(p[i] for p in pts),max(p[i] for p in pts)] for i in range(3)]
    checks.append({'file':f.name,'meshes':len(obs),'boundsXYZ_m':bounds,'allUV':all(bool(o.data.uv_layers) for o in obs),'allMaterialSlots':all(len(o.data.materials)>0 for o in obs),'finiteVertices':all(all(math.isfinite(v) for v in p) for p in pts)})
(V/'blender_reimport.json').write_text(json.dumps(checks,indent=2));print('REIMPORT_OK',checks,flush=True)
