"""Blender 4.5+: editable stage-1 Kurs shell, FBX chunks and Unity manifest.

All construction coordinates below are Unity metres (X right, Y up, Z back).
The entrance faces -Z; the building envelope, excluding the apron, is 93 x 130.
"""
import bpy
import bmesh
import json
import math
import pathlib
import sys
from collections import defaultdict
from mathutils import Vector

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Environment/KursHub"
SOURCE = ROOT / "Sources/KursHub"
REPORT = ROOT / "work/KursHub"
for directory in (OUT / "Models", SOURCE, REPORT):
    directory.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
palette = {
    "Concrete": (0.57, 0.55, 0.49, 1),
    "Floor": (0.39, 0.40, 0.37, 1),
    "BlueCladding": (0.035, 0.16, 0.27, 1),
    "Frame": (0.095, 0.12, 0.13, 1),
    "Glass": (0.22, 0.40, 0.43, 0.28),
    "Roof": (0.22, 0.24, 0.25, 1),
    "Safety": (0.65, 0.43, 0.11, 1),
    "Lettering": (0.80, 0.79, 0.70, 1),
}
materials = {}
for name, color in palette.items():
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = color
    bsdf.inputs['Roughness'].default_value = 0.65 if name != 'Glass' else 0.18
    if name == 'Glass':
        bsdf.inputs['Alpha'].default_value = 0.28
        mat.surface_render_method = 'DITHERED'
    materials[name] = mat

collections = {}
objects = []
collision_boxes = []
chunk_objects = defaultdict(list)

def xyz(p):
    return (p[0], -p[2], p[1])

def collection(name):
    if name not in collections:
        c = bpy.data.collections.new(name)
        scene.collection.children.link(c)
        collections[name] = c
    return collections[name]

def mesh(name, verts, faces, material, group, collide=True):
    data = bpy.data.meshes.new(name)
    data.from_pydata([xyz(v) for v in verts], [], faces)
    data.update()
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    collection(group).objects.link(obj)
    data.materials.append(materials[material])
    obj['unity_collision'] = collide
    obj['construction_group'] = group
    objects.append(obj)
    center = sum((v.co for v in data.vertices), Vector()) / len(data.vertices)
    # Spatial chunks allow Unity to cull wings separately; source stays editable.
    zone = (math.floor(center.x / 24), math.floor(-center.y / 30))
    chunk_objects[(group, material, *zone, collide)].append(obj)
    return obj

def box(name, center, size, material='Concrete', group='Structure', angle=0, collide=True):
    x, y, z = center
    sx, sy, sz = (s / 2 for s in size)
    c, s = math.cos(angle), math.sin(angle)
    verts = [(x + c*a + s*d, y+b, z-s*a+c*d)
             for a, b, d in [(-sx,-sy,-sz),(sx,-sy,-sz),(sx,-sy,sz),(-sx,-sy,sz),
                             (-sx,sy,-sz),(sx,sy,-sz),(sx,sy,sz),(-sx,sy,sz)]]
    return mesh(name, verts, [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],
                material, group, collide)

def beam(name, a, b, width=.09, material='Frame', group='Rails', collide=False):
    a, b = Vector(a), Vector(b)
    direction = (b-a).normalized()
    side = direction.cross(Vector((0,1,0)))
    if side.length < .1:
        side = Vector((1,0,0))
    side.normalize()
    up = direction.cross(side).normalized()
    verts = [tuple(p + side*i*width/2 + up*j*width/2)
             for p in (a,b) for i,j in ((-1,-1),(1,-1),(1,1),(-1,1))]
    return mesh(name, verts, [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],
                material, group, collide)

def slab(name, polygon, bottom, top, material='Floor', group='Floors'):
    # All supplied polygons are convex: split floor/roof around every void.
    n = len(polygon)
    return mesh(name, [(x,y,z) for y in (bottom,top) for x,z in polygon],
                [tuple(range(n-1,-1,-1)),tuple(range(n,2*n))] +
                [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)], material, group)

def rect(name, x0,x1,z0,z1,y0,y1, material='Floor', group='Floors'):
    return box(name, ((x0+x1)/2,(y0+y1)/2,(z0+z1)/2), (x1-x0,y1-y0,z1-z0),material,group)

def rail(name,a,b,y,height=1.15,group='Rails'):
    a,b = Vector((a[0],y,a[1])),Vector((b[0],y,b[1]))
    length=(b-a).length
    for h in (.12,.58,height):
        beam(name+'_bar',a+Vector((0,h,0)),b+Vector((0,h,0)),group=group)
    for i in range(max(1,math.ceil(length/1.8))+1):
        p=a.lerp(b,i/max(1,math.ceil(length/1.8)))
        beam(name+'_post',p,p+Vector((0,height,0)),width=.075,group=group)
    middle=(a+b)/2+Vector((0,height/2,0))
    collision_boxes.append(dict(name=name,center=list(middle),size=[length,height,.11],
                                angle=math.degrees(math.atan2(-(b-a).z,(b-a).x))))

# Rectangular body plus a broad elliptical entrance bay. Bounds exactly +/-46.5, -65..65.
arc = [(30*math.cos(math.pi*i/32), -50-15*math.sin(math.pi*i/32)) for i in range(33)]
body = [(-46.5,-50),(46.5,-50),(46.5,65),(-46.5,65)]
slab('GroundBody',body,-.35,0)
slab('GroundRoundBay',arc,-.35,0)

# Upper floor wraps the 30 x 50 m atrium (X +/-15, Z -32..18).
for label,y0,y1,mat,group in [('First',5.1,5.5,'Floor','Floors'),('Roof',10.65,11,'Roof','RoofDeck')]:
    rect(label+'_West',-46.5,-15,-50,65,y0,y1,mat,group)
    rect(label+'_East',15,46.5,-50,65,y0,y1,mat,group)
    rect(label+'_Front',-15,15,-50,-32,y0,y1,mat,group)
    rect(label+'_Rear',-15,15,18,65,y0,y1,mat,group)
    slab(label+'_RoundBay',arc,y0,y1,mat,group)

# Replace the west roof strip with tiles around the roof stair opening.
roof_west=bpy.data.objects.get('Roof_West')
for key,items in chunk_objects.items():
    if roof_west in items: items.remove(roof_west)
objects.remove(roof_west)
bpy.data.objects.remove(roof_west,do_unlink=True)
for name,r in [('Front',(-46.5,-15,-50,38)),('Rear',(-46.5,-15,45,65)),
               ('Outer',(-46.5,-42,38,45)),('Inner',(-34,-15,38,45))]:
    rect('RoofWest_'+name,*r,10.65,11,'Roof','RoofDeck')

# Straight side/rear walls and curved frontage, segmented for matching frames.
segments=[]
def edge(a,b,max_length=8):
    distance=math.dist(a,b)
    steps=math.ceil(distance/max_length)
    for i in range(steps):
        segments.append(((a[0]+(b[0]-a[0])*i/steps,a[1]+(b[1]-a[1])*i/steps),
                         (a[0]+(b[0]-a[0])*(i+1)/steps,a[1]+(b[1]-a[1])*(i+1)/steps)))
edge((-46.35,-49.85),(-30,-49.85))
wall_arc=[(x*29.85/30,-50+(z+50)*14.85/15) for x,z in arc]
for a,b in zip(wall_arc,wall_arc[1:]):segments.append((a,b))
edge((30,-49.85),(46.35,-49.85))
edge((-46.35,-49.85),(-46.35,64.85))
edge((46.35,-49.85),(46.35,64.85))
edge((-46.35,64.85),(46.35,64.85))
for i,(a,b) in enumerate(segments):
    dx,dz=b[0]-a[0],b[1]-a[1]
    length=math.hypot(dx,dz)
    cx,cz=(a[0]+b[0])/2,(a[1]+b[1])/2
    angle=math.atan2(-dz,dx)
    front=cz < -49
    entrance=front and abs(cx)<3
    def panel(suffix,lo,hi,mat,thickness=.30):
        box(f'Facade_{i:03}_{suffix}',(cx,(lo+hi)/2,cz),(length+.025,hi-lo,thickness),mat,'Facade',angle)
    if not entrance:
        panel('Plinth',0,1,'Concrete')
        if front:
            panel('GroundWindow',1,4.45,'Glass',.16)
        else:
            panel('LowerPanel',1,2,'Concrete')
            panel('GroundWindow',2,3.3,'Glass',.16)
            panel('UpperPanel',3.3,4.45,'Concrete')
    else:
        panel('EntranceHeader',3.8,4.45,'Frame')
    panel('Belt',4.45,5.5,'Concrete')
    panel('UpperWindow',5.5,9.35,'Glass',.16)
    panel('BlueFascia',9.35,11.35,'BlueCladding')
    # No vertical post in the open entrance.
    if not entrance:
        box(f'Facade_{i:03}_Mullion',(a[0],5.1,a[1]),(.085,8.4,.34),'Frame','Facade',angle)
    for y in (1,4.45,5.5,9.35):
        if not entrance or y>=4.45:
            box(f'Facade_{i:03}_Transom',(cx,y,cz),(length,.065,.34),'Frame','Facade',angle,False)

# Columns use a 12 m structural rhythm. No column occupies the atrium or stairs.
for x in (-36,-24,-16,16,24,36):
    for z in (-40,-28,-16,-4,8,20,32,44,56):
        box(f'Column_{x}_{z}',(x,5.325,z),(.65,10.65,.65))
for z in (-32,18):
    box('AtriumCrossBeam',(0,4.8,z),(31,.6,.6))
for x in (-15.35,15.35):
    box('AtriumLongBeam',(x,4.8,-7),(.7,.6,50))
    for z in (-32,-20,-8,4,18):
        box('AtriumColumn',(x,5.325,z),(.65,10.65,.65))

# Two generous ordinary staircases rise through the atrium onto the rear gallery.
for x in (-10,10):
    for i in range(30):
        height=(i+1)*5.5/30
        box(f'MainStair_{x}_{i:02}',(x,height/2,8+(i+.5)/3),(4,height,1/3),'Concrete','MainStairs')
    for side in (-2,2):
        beam('MainHandrail',(x+side,1.12,8),(x+side,6.62,18),group='MainStairs')
        # Closed low side stringer; the rail gets a continuous dedicated collision wall.
        verts=[(x+side+dx,y,z) for dx in (-.065,.065)
               for y,z in ((0,8),(1.15,8),(6.65,18),(5.5,18))]
        mesh('StairGuard',verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],
             'Frame','MainStairs',True)

# Gallery edge protection, leaving both stair landings open.
rail('AtriumFront',(-15,-32),(15,-32),5.5)
rail('AtriumWest',(-15,-32),(-15,18),5.5)
rail('AtriumEast',(15,-32),(15,18),5.5)
for a,b in [(-15,-12.1),(-7.9,7.9),(12.1,15)]:
    rail('AtriumRear',(a,18),(b,18),5.5)

# Roof access is an ordinary U stair, not a third storey.
for i in range(15):
    h=(i+1)*2.75/15
    box(f'RoofStair_A_{i:02}',(-39.5,5.5+h/2,38+(i+.5)*.3),(2.6,h,.3),'Concrete','RoofStairs')
    box(f'RoofStair_B_{i:02}',(-36.5,8.25+h/2,42.5-(i+.5)*.3),(2.6,h,.3),'Concrete','RoofStairs')
rect('RoofStairHalfLanding',-41,-35,42.5,45,8.0,8.25,'Concrete','RoofStairs')
for x in (-40.9,-38.1):
    beam('RoofStairHandrailA',(x,6.6,38),(x,9.35,42.5),group='RoofStairs')
for x in (-37.9,-35.1):
    beam('RoofStairHandrailB',(x,9.35,42.5),(x,12.1,38),group='RoofStairs')
rail('RoofStairLandingBack',(-41,45),(-35,45),8.25,group='RoofStairs')
rail('RoofStairLandingWest',(-41,42.5),(-41,45),8.25,group='RoofStairs')
rail('RoofStairLandingEast',(-35,42.5),(-35,45),8.25,group='RoofStairs')
box('RoofAccessWest',(-42.25,12.5,41.25),(.3,3,9.5),'Concrete','RoofAccess')
box('RoofAccessEast',(-33.75,12.5,41.25),(.3,3,9.5),'Concrete','RoofAccess')
box('RoofAccessBack',(-38,12.5,45.85),(8.8,3,.3),'Concrete','RoofAccess')
for x,w in [(-40,4.7),(-34.9,2.0)]:
    box('RoofAccessDoorSide',(x,12.5,36.65),(w,3,.3),'Concrete','RoofAccess')
box('RoofAccessDoorHeader',(-36.5,13.65,36.65),(2.2,.7,.3),'Concrete','RoofAccess')
box('RoofAccessCap',(-38,14.15,41.25),(9,.3,9.5),'Roof','RoofAccess')

# Low external parapet and protected skylight perimeter.
for i,(a,b) in enumerate(segments):
    dx,dz=b[0]-a[0],b[1]-a[1]
    box('RoofParapet',((a[0]+b[0])/2,11.65,(a[1]+b[1])/2),(math.hypot(dx,dz)+.02,.6,.24),
        'Concrete','RoofDeck',math.atan2(-dz,dx))
for a,b in [((-15,-32),(15,-32)),((-15,18),(15,18)),((-15,-32),(-15,18)),((15,-32),(15,18))]:
    rail('SkylightGuard',a,b,11,height=1.1,group='Skylight')
corners=[(-15,11,-32),(15,11,-32),(15,11,18),(-15,11,18)]
ridgeA=(0,14,-24)
ridgeB=(0,14,10)
for i,face in enumerate([[corners[0],corners[1],ridgeA],
                          [corners[1],corners[2],ridgeB,ridgeA],
                          [corners[2],corners[3],ridgeB],
                          [corners[3],corners[0],ridgeA,ridgeB]]):
    # Thin closed glazing, not a one-sided surface.
    n=len(face)
    mesh('SkylightGlass',face+[(x,y-.08,z) for x,y,z in face],
         [tuple(range(n)),tuple(range(2*n-1,n-1,-1))]+
         [(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)],'Glass','Skylight')
for a,b in [(corners[0],ridgeA),(corners[1],ridgeA),(corners[2],ridgeB),(corners[3],ridgeB),(ridgeA,ridgeB)]:
    beam('SkylightRidge',a,b,.16,group='Skylight')
for z in range(-28,19,4):
    t=max(0,min(1,(z+32)/8,(18-z)/8))
    apex=(0,11+3*t,z)
    beam('SkylightRib',(-15,11,z),apex,.10,group='Skylight')
    beam('SkylightRib',apex,(15,11,z),.10,group='Skylight')

# Apron belongs to the prefab but is explicitly excluded from the 93 x 130 envelope.
rect('EntranceApron',-20,20,-85,-64.7,-.35,0,'Floor','Approach')

# Small identification on the actual entry side; no vendor logos or invented decoration.
def text_object(text,location,size,material):
    curve=bpy.data.curves.new('KursSign','FONT')
    curve.body=text
    font=pathlib.Path('C:/Windows/Fonts/arialbd.ttf')
    if font.exists(): curve.font=bpy.data.fonts.load(str(font))
    curve.size=size
    curve.align_x='CENTER'
    curve.extrude=.035
    obj=bpy.data.objects.new('KursSign',curve)
    collection('Facade').objects.link(obj)
    obj.location=xyz(location)
    obj.rotation_euler=(math.pi/2,0,0)
    obj.data.materials.append(materials[material])
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    obj=bpy.context.object
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj['unity_collision']=False
    objects.append(obj)
    chunk_objects[('Sign',material,0,-3,False)].append(obj)
    obj.select_set(False)
text_object('КУРС',(0,9.7,-65.25),1.35,'Lettering')

# Validate source solids before saving; individual parts remain available for editing.
metrics=[]
for obj in objects:
    bm=bmesh.new();bm.from_mesh(obj.data)
    # Text has intentional open outlines after font conversion; structural solids do not.
    if obj.name!='KursSign':
        assert all(e.is_manifold for e in bm.edges),obj.name
        assert bm.calc_volume(signed=True)>0,obj.name
    metrics.append(dict(name=obj.name,vertices=len(obj.data.vertices),faces=len(obj.data.polygons)))
    bm.free()

scene['KursHubStage']='1: empty two-storey shell; no NPC/service/safe-zone logic'
scene['Dimensions']='93 m X x 130 m Z external walls; floors Y=0/5.5, roof Y=11'
scene['Atrium']='30 x 50 m; X=-15..15, Z=-32..18'
scene['UnityOrigin']='ground centre of rectangular envelope; entrance -Z'

# Neutral preview cameras remain in .blend, not in exported FBX.
def camera(name,point,target,lens=35):
    data=bpy.data.cameras.new(name);obj=bpy.data.objects.new(name,data)
    collection('Preview').objects.link(obj)
    obj.location=xyz(point)
    obj.rotation_euler=(Vector(xyz(target))-obj.location).to_track_quat('-Z','Y').to_euler()
    data.lens=lens
    return obj
camera('Exterior',(105,80,-145),(0,4,-12),40)
camera('EntranceInterior',(0,1.7,-57),(0,3,10),22)
camera('Gallery',(20,7.2,-28),(-5,2,-2),24)
scene.camera=bpy.data.objects['Exterior']
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_distance=170
        area.spaces.active.clip_end=1500
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'KursHub.blend'))

# Export combined spatial chunks, keeping individual authoring objects in the .blend.
export_collection=collection('EXPORT_TEMP')
exports=[]
chunks=[]
for index,(key,items) in enumerate(sorted(chunk_objects.items())):
    if not items: continue
    group,material,zx,zz,collide=key
    bpy.ops.object.select_all(action='DESELECT')
    copies=[]
    for obj in items:
        copy=obj.copy();copy.data=obj.data.copy();export_collection.objects.link(copy)
        copy.select_set(True);copies.append(copy)
    bpy.context.view_layer.objects.active=copies[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=f'{group}_{material}_{index:03}'+('_NC' if not collide else '')
    obj.data.name=obj.name
    exports.append(obj)
    chunks.append(dict(name=obj.name,material=material,collider=collide))
bpy.ops.object.select_all(action='DESELECT')
for obj in exports:obj.select_set(True)
bpy.context.view_layer.objects.active=exports[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'Models/KursHubShell.fbx'),use_selection=True,
                        object_types={'MESH'},axis_forward='-Z',axis_up='Y',
                        apply_unit_scale=True,bake_space_transform=False,
                        use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,
                        path_mode='AUTO')
manifest=dict(stage=1,width=93,depth=130,floorHeight=5.5,atrium=[30,50],
              chunks=chunks,guardColliders=collision_boxes,
              palette=[dict(name=n,rgba=list(c)) for n,c in palette.items()])
(OUT/'KursHubManifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(REPORT/'geometry.json').write_text(json.dumps(dict(sourceObjects=len(objects),
    sourceVertices=sum(x['vertices'] for x in metrics),exportChunks=len(exports),
    sourceSolidsValidated=True,parts=metrics),indent=2),encoding='utf-8')
print('KURS_GEOMETRY_OK',len(objects),'editable objects;',len(exports),'spatial chunks')
