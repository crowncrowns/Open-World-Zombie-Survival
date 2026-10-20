"""Blender 4.5+: editable stage-6 Kurs architecture, FBX chunks and manifest.

All construction coordinates below are Unity metres (X right, Y up, Z back).
The entrance faces -Z; the building envelope, excluding the apron, is 93 x 130.
"""
import bpy
import bmesh
import json
import math
import pathlib
import sys
import argparse
import re
from collections import defaultdict
from mathutils import Vector

ROOT = pathlib.Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--root',type=pathlib.Path,default=ROOT,help='Separate output root for authoring/verification before deployment')
ROOT=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []).root.resolve()
OUT = ROOT / "Assets/Environment/KursHub"
SOURCE = ROOT / "Sources/KursHub"
REPORT = ROOT / "work/KursHub"
for directory in (OUT / "Models", SOURCE, REPORT):
    directory.mkdir(parents=True, exist_ok=True)
previous_chunks={}
if (OUT/'KursHubManifest.json').exists():
    for record in json.loads((OUT/'KursHubManifest.json').read_text(encoding='utf-8'))['chunks']:
        if 'region' in record:
            key=(record['group'],record['material'],*record['region'],record['collider'],record.get('hidden',False))
            previous_chunks[key]=record['name']
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
palette = {
    "Concrete": (0.57, 0.55, 0.49, 1),
    "Floor": (0.39, 0.40, 0.37, 1),
    "BlueCladding": (0.055, 0.26, 0.46, 1),
    "Frame": (0.095, 0.12, 0.13, 1),
    "Glass": (0.22, 0.40, 0.43, 0.28),
    "Roof": (0.22, 0.24, 0.25, 1),
    "Safety": (0.65, 0.43, 0.11, 1),
    "Lettering": (0.80, 0.79, 0.70, 1),
    "MallBlue": (0.055, 0.26, 0.46, 1),
    "CreamPanel": (0.64, 0.62, 0.53, 1),
    "SteelOxide": (0.26, 0.15, 0.09, 1),
    "TenantGreen": (0.22, 0.42, 0.09, 1),
    "SignRed": (0.35, 0.08, 0.065, 1),
    "SailOrange": (0.73, 0.29, 0.13, 1),
    "TarpGreen": (.17,.21,.14,1),
    "SandCanvas": (.46,.43,.30,1),
    "WornWood": (.30,.24,.16,1),
    "LampGlass": (1,.72,.35,1),
    "DirtStain": (.25,.22,.17,.65),
}
materials = {}
import runpy
palette.update(runpy.run_path(str(pathlib.Path(__file__).with_name('stage6_palette.py')))['PALETTE6'])
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
    # Unity's FBX importer handles handedness: +Blender Y arrives as +Unity Z.
    # Recalculate source face winding after this axis permutation, never negate
    # a prefab scale to compensate (visuals and collision must share the basis).
    return (p[0], p[2], p[1])

def collection(name):
    if name not in collections:
        c = bpy.data.collections.new(name)
        scene.collection.children.link(c)
        collections[name] = c
    return collections[name]

def mesh(name, verts, faces, material, group, collide=True, hidden=False):
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
    obj['hidden_collision_proxy'] = hidden
    if hidden:
        obj.hide_render=True
        obj.display_type='WIRE'
    objects.append(obj)
    center = sum((v.co for v in data.vertices), Vector()) / len(data.vertices)
    # Spatial chunks allow Unity to cull wings separately; source stays editable.
    zone = (math.floor(center.x / 24), math.floor(center.y / 30))
    chunk_objects[(group, material, *zone, collide, hidden)].append(obj)
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
    curved=front and abs(cx)<30
    # The real building reads as closed blue wings around one glazed rotunda.
    # Selected window bays deliberately interrupt the side/rear panel fields.
    window_bay=curved or (not front and i%5 in (1,2))
    tall_bay=not front and i%9==2
    top=12.15 if curved else (13.6 if (cx< -30 and cz< -20) else 12.35 if cz>35 else 11.8)
    if not entrance:
        panel('Plinth',0,.85,'CreamPanel')
        if curved:
            panel('GroundWindow',.85,4.45,'Glass',.16)
        elif window_bay:
            panel('LowerPanel',.85,1.1 if tall_bay else 1.5,'CreamPanel')
            panel('GroundWindow',1.1 if tall_bay else 1.5,4.25 if tall_bay else 3.55,'Glass',.16)
            panel('UpperPanel',4.25 if tall_bay else 3.55,4.45,'BlueCladding')
        else:
            base=1.1 if not front and i%7 in (0,1) else 2.1
            panel('GroundClosedBase',.85,base,'CreamPanel')
            panel('GroundClosedBlue',base,4.45,'BlueCladding')
    else:
        panel('EntranceHeader',3.8,4.45,'Frame')
    panel('Belt',4.45,5.5,'CreamPanel' if curved else 'BlueCladding')
    if curved:
        panel('UpperWindow',5.5,9.35,'Glass',.16)
    elif window_bay:
        panel('BlueLower',5.5,5.9,'BlueCladding')
        panel('UpperWindow',5.9,8.05,'Glass',.16)
        panel('BlueUpper',8.05,9.35,'BlueCladding')
    else:
        panel('BlueClosed',5.5,9.35,'BlueCladding')
    # Retain the original Facade/BlueCladding chunk identity used by the scene.
    panel('BlueFascia',9.35,top,'BlueCladding')
    # No vertical post in the open entrance.
    if not entrance and window_bay:
        box(f'Facade_{i:03}_Mullion',(a[0],(5.5+9.35)/2,a[1]),(.085,3.85,.34),'Frame','Facade',angle)
    for y in (.85,4.45,5.5,9.35,top):
        if not entrance or y>=4.45:
            box(f'Facade_{i:03}_Transom',(cx,y,cz),(length,.065,.34),'Frame','Facade',angle,False)

# Columns use a 12 m structural rhythm. No column occupies the atrium or stairs.
for x in (-36,-24,-16,16,24,36):
    for z in (-40,-28,-16,-4,8,20,32,44,56):
        if x==-36 and z==44: continue  # Keep the roof stair half-landing clear.
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
for x,ya,yb,za,zb in [(-40.9,5.5,8.25,38,42.5),(-38.1,5.5,8.25,38,42.5),
                       (-37.9,8.25,11,42.5,38),(-35.1,8.25,11,42.5,38)]:
    verts=[(x+dx,y,z) for dx in (-.055,.055)
           for y,z in ((ya,za),(ya+1.1,za),(yb+1.1,zb),(yb,zb))]
    mesh('RoofStairGuard',verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],
         'Frame','RoofStairs')
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
for a,b in [((-42,38),(-37.9,38)),((-35.1,38),(-34,38)),((-42,45),(-34,45)),
             ((-42,38),(-42,45)),((-34,38),(-34,45))]:
    rail('RoofOpeningGuard',a,b,11,group='RoofAccess')

# Low external parapet and protected skylight perimeter.
for i,(a,b) in enumerate(segments):
    dx,dz=b[0]-a[0],b[1]-a[1]
    cx,cz=(a[0]+b[0])/2,(a[1]+b[1])/2
    top=12.15 if cz< -49 and abs(cx)<30 else (13.6 if (cx< -30 and cz< -20) else 12.35 if cz>35 else 11.8)
    box('RoofParapet',(cx,top-.15,cz),(math.hypot(dx,dz)+.02,.30,.24),
        'MallBlue','RoofDeck',math.atan2(-dz,dx))
for a,b in [((-15,-32),(15,-32)),((-15,18),(15,18)),((-15,-32),(-15,18)),((15,-32),(15,18))]:
    rail('SkylightGuard',a,b,11,height=1.1,group='Skylight')
corners=[(-15,11,-32),(15,11,-32),(15,11,18),(-15,11,18)]
ridgeA=(0,11.55,-24)
ridgeB=(0,11.55,10)
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
    apex=(0,11+.55*t,z)
    beam('SkylightRib',(-15,11,z),apex,.10,group='Skylight')
    beam('SkylightRib',apex,(15,11,z),.10,group='Skylight')

# Apron belongs to the prefab but is explicitly excluded from the 93 x 130 envelope.
rect('EntranceApron',-20,20,-85,-64.7,-.35,0,'Floor','Approach')

# Small identification on the actual entry side; no vendor logos or invented decoration.
def text_object(text,location,size,material,name='KursSign',group='Sign'):
    curve=bpy.data.curves.new('KursSign','FONT')
    curve.body=text
    font=pathlib.Path('C:/Windows/Fonts/arialbd.ttf')
    if font.exists(): curve.font=bpy.data.fonts.load(str(font),check_existing=True)
    curve.size=size
    curve.align_x='CENTER'
    curve.extrude=.035
    obj=bpy.data.objects.new(name,curve)
    collection(group).objects.link(obj)
    obj.location=xyz(location)
    obj.rotation_euler=(math.pi/2,0,0)
    obj.data.materials.append(materials[material])
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    obj=bpy.context.object
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj['unity_collision']=False
    obj['is_text']=True
    objects.append(obj)
    zone=(0,-3) if group=='Sign' else (math.floor(location[0]/24),math.floor(location[2]/30))
    chunk_objects[(group,material,*zone,False,False)].append(obj)
    obj.select_set(False)
    return obj

# The stage-1 names remain reserved even as architecture gains new groups.
# Existing prefab instances can override individual model children by fileID.
legacy_names={key:f'{key[0]}_{key[1]}_{index:03}'+('_NC' if not key[4] else '')
              for index,key in enumerate(sorted(chunk_objects))}

# Stage 2: a generous open vestibule, with floor level unchanged at Y=0.
for x in (-6,6):
    box('VestibuleSideGlass',(x,1.9,-60.7),(.12,3.8,7.4),'Glass','Vestibule')
    for z in (-64.4,-62,-59.5,-57):
        box('VestibuleSidePost',(x,1.9,z),(.10,3.8,.10),'Frame','Vestibule')
    for y in (.12,2.45,3.75):
        box('VestibuleSideTransom',(x,y,-60.7),(.15,.075,7.4),'Frame','Vestibule')
for z in (-64.4,-57):
    for x in (-4.5,4.5):
        box('VestibuleFixedGlass',(x,1.9,z),(2.9,3.8,.12),'Glass','Vestibule')
    for x in (-6,-3,3,6):
        box('VestibuleJamb',(x,1.9,z),(.12,3.8,.18),'Frame','Vestibule')
    box('VestibuleHeader',(0,3.67,z),(12,.26,.22),'Frame','Vestibule')
    box('LevelThreshold',(0,.004,z),(5.7,.008,.18),'Safety','VestibuleDetails',collide=False)
box('VestibuleCeiling',(0,3.98,-60.7),(12.3,.20,7.7),'Concrete','Vestibule')
text_object('ГЛАВНЫЙ ВХОД',(0,3.17,-64.56),.38,'Lettering','EntranceLabel','VestibuleDetails')

# Stage 4: canopy, roof beams and all four posts removed at user request.

# Fine facade framing and panel joints are visual only; the original closed
# wall/glass meshes continue to define player collision.
for i,(a,b) in enumerate(segments):
    dx,dz=b[0]-a[0],b[1]-a[1]
    length=math.hypot(dx,dz);angle=math.atan2(-dz,dx)
    cx,cz=(a[0]+b[0])/2,(a[1]+b[1])/2
    entrance=cz < -49 and abs(cx)<3
    curved=cz < -49 and abs(cx)<30
    window_bay=curved or (cz>=-49 and i%5 in (1,2))
    tall_bay=cz>=-49 and i%9==2
    top=12.15 if curved else (13.6 if (cx< -30 and cz< -20) else 12.35 if cz>35 else 11.8)
    n=max(1,math.ceil(length/1.8))
    for j in range(1,n):
        x,z=a[0]+dx*j/n,a[1]+dz*j/n
        if window_bay:
            lo,hi=(5.5,9.35) if curved else (5.9,8.05)
            box('UpperWindowDivision',(x,(lo+hi)/2,z),(.055,hi-lo,.35),'Frame','FacadeDetails',angle,False)
        if not entrance and window_bay:
            lo,hi=(.85,4.45) if curved else ((1.1,4.25) if tall_bay else (1.5,3.55))
            box('LowerWindowDivision',(x,(lo+hi)/2,z),(.055,hi-lo,.35),'Frame','FacadeDetails',angle,False)
        lo=9.35 if curved else 5.5
        box('CladdingVerticalJoint',(x,(lo+top)/2,z),(.018,top-lo,.325),'Frame','FacadeDetails',angle,False)
        if not curved:
            box('CreamVerticalJoint',(x,2.2,z),(.015,4.35,.325),'Frame','FacadeDetails',angle,False)
    for y in (6.8,8.2,9.6,11.0,12.4):
        if y<top and not (window_bay and y<9.35):
            box('PanelHorizontalJoint',(cx,y,cz),(length,.016,.325),'Frame','FacadeDetails',angle,False)
    for y,material in [(top,'Frame'),(4.48,'Concrete')]+([(7.45,'Frame')] if curved else []):
        box('FacadeContinuousTrim',(cx,y,cz),(length,.035,.34),material,'FacadeDetails',angle,False)

# Visible floor/roof beams and column capitals express the mall's structural grid.
for x in (-36,-24,-16,16,24,36):
    for z in (-40,-28,-16,-4,8,20,32,44,56):
        if x==-36 and z==44:continue
        for y in (5.0,10.55):
            box('ColumnCapital',(x,y,z),(1.05,.20,1.05),'Concrete','StructuralDetails')
for z in (-40,-28,-16,-4,8,20,32,44,56):
    for y in (4.92,10.45):
        for x in (-31,31):
            box('WingCeilingBeam',(x,y,z),(30.3,.40,.38),'Concrete','StructuralDetails')
        if z==-40 or z>=20:
            box('CrossCeilingBeam',(0,y,z),(32.2,.40,.38),'Concrete','StructuralDetails')
for x in (-15.35,15.35):
    box('GalleryEdgeFascia',(x,4.92,-7),(.72,.52,50),'Concrete','AtriumDetails')
    box('GalleryEdgeTrim',(x,5.46,-7),(.77,.065,50),'Frame','AtriumDetails',collide=False)
for z in (-32,18):
    box('GalleryEndFascia',(0,4.92,z),(30.7,.52,.65),'Concrete','AtriumDetails')

# Replace solid stair side panels with low kickplates + open posts. Full-height
# invisible collision proxies keep the original fall protection contract.
for obj in list(objects):
    if not (obj.name.startswith('StairGuard') or obj.name.startswith('RoofStairGuard')):continue
    verts=[(v.co.x,v.co.z,v.co.y) for v in obj.data.vertices]
    faces=[tuple(p.vertices) for p in obj.data.polygons]
    mesh('StairSafetyProxy',verts,faces,'Frame','CollisionProxy',True,True)
    # Original box order: side, then bottom-start/top-start/top-end/bottom-end.
    for offset in (0,4):
        obj.data.vertices[offset+1].co.z=obj.data.vertices[offset].co.z+.18
        obj.data.vertices[offset+2].co.z=obj.data.vertices[offset+3].co.z+.18
    obj.data.update()
for x in (-10,10):
    for side in (-2,2):
        for i in range(0,31,3):
            z=8+i/3;y=i*5.5/30
            beam('StairRailPost',(x+side,y+.18,z),(x+side,y+1.12,z),.065,group='StairDetails')
        beam('StairMidRail',(x+side,.64,8),(x+side,6.14,18),.055,group='StairDetails')
    for i in range(30):
        box('MainStairNosing',(x,(i+1)*5.5/30+.003,8+i/3+.045),(3.82,.006,.055),
            'Safety','StairDetails',collide=False)
for x,ya,yb,za,zb in [(-40.9,5.5,8.25,38,42.5),(-38.1,5.5,8.25,38,42.5),
                      (-37.9,8.25,11,42.5,38),(-35.1,8.25,11,42.5,38)]:
    for i in range(0,16,3):
        t=i/15;y=ya+(yb-ya)*t;z=za+(zb-za)*t
        beam('RoofRailPost',(x,y+.18,z),(x,y+1.1,z),.065,group='StairDetails')

# Skylight kerb and longitudinal glazing divisions, all below existing guards.
for x in (-15,15):
    box('SkylightKerb',(x,11.08,-7),(.32,.30,50.2),'Concrete','SkylightDetails')
for z in (-32,18):
    box('SkylightKerb',(0,11.08,z),(30,.30,.32),'Concrete','SkylightDetails')
for x in (-10,-5,5,10):
    t=1-abs(x)/15
    beam('SkylightPurlin',(x,11+.55*t+.05,-32+8*t),(x,11+.55*t+.05,18-8*t),.065,group='SkylightDetails')

# Reference-led exterior massing, dome and signs are kept separate for authoring.
import runpy
runpy.run_path(str(pathlib.Path(__file__).with_name('stage3_exterior.py')),init_globals=globals())

prop_instances=[]
import runpy
runpy.run_path(str(pathlib.Path(__file__).with_name('stage4_finish.py')),init_globals=globals())
interior_layout=runpy.run_path(str(pathlib.Path(__file__).with_name('stage5_interior.py')))['layout']()
runpy.run_path(str(pathlib.Path(__file__).with_name('build_stage5_interior.py')))['attach'](ROOT,interior_layout['interiorInstances'])
settlement_layout=runpy.run_path(str(pathlib.Path(__file__).with_name('stage6_interior.py')))['layout6']()
runpy.run_path(str(pathlib.Path(__file__).with_name('build_stage6_interior.py')))['finish6'](ROOT,settlement_layout['settlementInstances'],settlement_layout['settlementLights'])

# Validate source solids before saving; individual parts remain available for editing.
metrics=[]
for obj in objects:
    bm=bmesh.new();bm.from_mesh(obj.data)
    # Text has intentional open outlines after font conversion; structural solids do not.
    if not obj.get('is_text',False):
        assert all(e.is_manifold for e in bm.edges),obj.name
        assert bm.calc_volume(signed=True)>0,obj.name
    # Planar metre-based UVs keep stage-3 material work independent of the mesh exporter.
    if not obj.data.uv_layers:
        uv=obj.data.uv_layers.new(name='MetreUV')
        for face in obj.data.polygons:
            axis=max(range(3),key=lambda j:abs(face.normal[j]))
            axes=[j for j in range(3) if j!=axis]
            for loop in face.loop_indices:
                p=obj.data.vertices[obj.data.loops[loop].vertex_index].co
                uv.data[loop].uv=(p[axes[0]]/2,p[axes[1]]/2)
    metrics.append(dict(name=obj.name,vertices=len(obj.data.vertices),faces=len(obj.data.polygons)))
    bm.free()

scene['KursHubStage']='6: dense market, housing, checkpoint and ceramic floor; no entry canopy'
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
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'KursHub.blend'))

# Export combined spatial chunks, keeping individual authoring objects in the .blend.
export_collection=collection('EXPORT_TEMP')
exports=[]
chunks=[]
next_index=max([len(legacy_names)-1]+[int(re.search(r'_(\d+)(?:_NC)?$',name)[1]) for name in previous_chunks.values()])+1
for key,items in sorted(chunk_objects.items()):
    if not items: continue
    group,material,zx,zz,collide,hidden=key
    bpy.ops.object.select_all(action='DESELECT')
    copies=[]
    for obj in items:
        copy=obj.copy();copy.data=obj.data.copy();export_collection.objects.link(copy)
        copy.hide_render=False
        copy.select_set(True);copies.append(copy)
    bpy.context.view_layer.objects.active=copies[0]
    if len(copies)>1:bpy.ops.object.join()
    obj=bpy.context.object
    name=previous_chunks.get(key,legacy_names.get(key))
    if name is None:
        name=f'{group}_{material}_{next_index:03}'+('_NC' if not collide else '')
        next_index+=1
    obj.name=name
    obj.data.name=obj.name
    exports.append(obj)
    chunks.append(dict(name=obj.name,group=group,region=[zx,zz],material=material,collider=collide,hidden=hidden))
bpy.ops.object.select_all(action='DESELECT')
for obj in exports:obj.select_set(True)
bpy.context.view_layer.objects.active=exports[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'Models/KursHubShell.fbx'),use_selection=True,
                        object_types={'MESH'},axis_forward='-Z',axis_up='Y',
                        apply_unit_scale=True,bake_space_transform=False,
                        use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,
                        path_mode='AUTO')
manifest=dict(stage=6,width=93,depth=130,floorHeight=5.5,atrium=[30,50],
              chunks=chunks,guardColliders=collision_boxes,propInstances=prop_instances,
              palette=[dict(name=n,rgba=list(c)) for n,c in palette.items()])
manifest.update(interior_layout)
manifest.update(settlement_layout)
(OUT/'KursHubManifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(REPORT/'geometry.json').write_text(json.dumps(dict(sourceObjects=len(objects),
    sourceVertices=sum(x['vertices'] for x in metrics),exportChunks=len(exports),
    sourceSolidsValidated=True,parts=metrics),indent=2),encoding='utf-8')
print('KURS_GEOMETRY_OK',len(objects),'editable objects;',len(exports),'spatial chunks')
