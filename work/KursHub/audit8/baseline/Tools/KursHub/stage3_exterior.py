"""Stage-3 authoring layer, executed by build_kurs_hub.py inside Blender.

The references disagree in small details. Retain the confirmed 93 x 130 m
envelope and circulation; reconstruct their common architectural silhouette.
"""

def remove_part(obj):
    for items in chunk_objects.values():
        if obj in items: items.remove(obj)
    objects.remove(obj)
    bpy.data.objects.remove(obj,do_unlink=True)

def half_plane(poly,a,b,inside):
    if not poly:return []
    def distance(p):return (b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0])
    result=[]
    for p,q in zip(poly,poly[1:]+poly[:1]):
        dp,dq=distance(p),distance(q)
        pin=dp>=-1e-7 if inside else dp<=1e-7
        qin=dq>=-1e-7 if inside else dq<=1e-7
        if pin:result.append(p)
        if pin!=qin:
            t=dp/(dp-dq)
            result.append((p[0]+t*(q[0]-p[0]),p[1]+t*(q[1]-p[1])))
    clean=[]
    for p in result:
        if not clean or math.dist(p,clean[-1])>1e-6:clean.append(p)
    if len(clean)>1 and math.dist(clean[0],clean[-1])<1e-6:clean.pop()
    return clean

# A true opening under the front cupola, rather than glazing laid on solid roof.
DOME_Z=-50
hole=[(10.6*math.cos(2*math.pi*i/24),DOME_Z+10.6*math.sin(2*math.pi*i/24)) for i in range(24)]
for name in ('Roof_Front','Roof_RoundBay'):
    obj=bpy.data.objects[name]
    poly=[(v.co.x,v.co.y) for v in obj.data.vertices if abs(v.co.z-11)<.001]
    cx=sum(p[0] for p in poly)/len(poly);cz=sum(p[1] for p in poly)/len(poly)
    poly.sort(key=lambda p:math.atan2(p[1]-cz,p[0]-cx))
    pieces=[];remaining=poly
    for a,b in zip(hole,hole[1:]+hole[:1]):
        outside=half_plane(remaining,a,b,False)
        area=abs(sum(p[0]*q[1]-q[0]*p[1] for p,q in zip(outside,outside[1:]+outside[:1])))/2
        if len(outside)>=3 and area>1e-5:pieces.append(outside)
        remaining=half_plane(remaining,a,b,True)
        if len(remaining)<3:break
    remove_part(obj)
    for i,piece in enumerate(pieces):slab(name+f'_DomeCut_{i:02}',piece,10.65,11,'Roof','RoofDeck')

for i in range(24):
    a,b=2*math.pi*i/24,2*math.pi*(i+1)/24
    ring=[(r*math.cos(t),DOME_Z+r*math.sin(t)) for r,t in ((10.6,a),(11,a),(11,b),(10.6,b))]
    slab('DomeKerb',ring,11,11.4,'CreamPanel','FrontDome')
    # Closed thin triangular panes, each with its own outward normals.
    outer=[(11*math.cos(a),11.4,DOME_Z+11*math.sin(a)),
           (11*math.cos(b),11.4,DOME_Z+11*math.sin(b)),(0,15.7,DOME_Z)]
    verts=outer+[(x,y-.07,z) for x,y,z in outer]
    mesh('DomeGlass',verts,[(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)],'Glass','FrontDome')
    beam('DomeRadialRib',outer[0],outer[2],.12,'CreamPanel','DomeDetails')
    for t in (.43,.76,1):
        p=tuple(outer[2][j]+t*(outer[0][j]-outer[2][j]) for j in range(3))
        q=tuple(outer[2][j]+t*(outer[1][j]-outer[2][j]) for j in range(3))
        beam('DomeRing',p,q,.085,'CreamPanel','DomeDetails')
    pa=(11.55*math.cos(a),DOME_Z+11.55*math.sin(a));pb=(11.55*math.cos(b),DOME_Z+11.55*math.sin(b))
    rail('FrontDomeGuard',pa,pb,11,height=1.1,group='DomeGuards')

# Opaque advertising shoulders and a cream terrace make the front asymmetric.
# Hollow wall construction keeps the source editable, rather than solid boxes.
def shoulder(name,x0,x1,z0,z1,top):
    for x in (x0,x1):box(name+'Side',(x,(5.5+top)/2,(z0+z1)/2),(.24,top-5.5,z1-z0),'MallBlue','RaisedWings')
    for z in (z0,z1):box(name+'End',((x0+x1)/2,(5.5+top)/2,z),(x1-x0,top-5.5,.24),'MallBlue','RaisedWings')
    rect(name+'Cap',x0,x1,z0,z1,top-.2,top,'Roof','RaisedWings')
    for x in range(math.ceil(x0/1.6),math.floor(x1/1.6)+1):
        box(name+'PanelJoint',(x*1.6,(5.5+top)/2,z0-.13),(.018,top-5.5,.025),'Frame','ExteriorDetails',collide=False)
    for y in (6.5,8.1,9.7,11.3,12.9):
        if y<top:box(name+'PanelJoint',((x0+x1)/2,y,z0-.13),(x1-x0,.018,.025),'Frame','ExteriorDetails',collide=False)
shoulder('LeftAdvertisingBlock',-44,-30,-49.7,-28,14.1)
shoulder('RightAdvertisingBlock',30,45,-49.7,-35,12.8)
# Offset rear service bulkhead and low mechanical housings: no third floor.
shoulder('RearBlueBulkhead',-28,-12,49,64.6,13.1)
for x,z,w,d,h in [(-27,7,5,10,2.2),(31,32,6,8,1.8),(19,51,4,6,2.3)]:
    box('AirHandlingUnit',(x,11+h/2,z),(w,h,d),'Concrete','RoofPlant')
    for yy in (.3,.65,1,1.35):
        if yy<h:box('VentLouvre',(x,11+yy,z-d/2-.025),(w-.4,.045,.05),'Frame','ExteriorDetails',collide=False)
    box('VentDuct',(x-w/2-1,11.6,z),(2,1.2,2),'CreamPanel','RoofPlant')
for x in (-41,39):
    beam('RoofAntenna',(x,11,-10),(x,17.5,-10),.08,'Frame','RoofPlant',False)
    for y in (14.6,15.7,16.8):box('AntennaPanel',(x+.24,y,-10),(.32,.65,.18),'Concrete','RoofPlant',collide=False)

# Grouped exposed service risers distinguish the rear from the public facade.
for x,top in ((22,16.8),(26,15.4),(29,17.4)):
    box('RearVentRiser',(x,(2.5+top)/2,65.35),(1.7,top-2.5,1.1),'Concrete','ServiceRisers')
    box('RearVentTop',(x,top,64.3),(1.7,.35,3.2),'CreamPanel','ServiceRisers')
    for y in range(3,math.floor(top)):
        box('RearVentBand',(x,y,65.93),(1.82,.06,.05),'Frame','ExteriorDetails',collide=False)
    for y in (4,8,12):
        box('RearVentBracket',(x,y,65.1),(2.2,.16,1.5),'Frame','ServiceRisers')
for x in (17.5,18.3):
    beam('RearPipe',(x,1.8,65.45),(x,14.8,65.45),.28,'CreamPanel','ServiceRisers',True)
    beam('RearPipeReturn',(x,14.8,65.45),(x,14.8,63.5),.28,'CreamPanel','ServiceRisers',True)
rear_word=text_object('Курс',(-14,9.8,65.18),3.7,'Lettering','RearKurs','RearSign')
for v in rear_word.data.vertices:
    v.co.x=-14-(v.co.x+14);v.co.y=65.18-(v.co.y-65.18)
rear_sub=text_object('ТОРГОВЫЙ ЦЕНТР',(-14,8.85,65.18),.73,'Lettering','RearSubtitle','RearSign')
for v in rear_sub.data.vertices:
    v.co.x=-14-(v.co.x+14);v.co.y=65.18-(v.co.y-65.18)

rect('FrontLeftTerrace',-44,-29.6,-56,-49.5,4.05,4.4,'CreamPanel','Terraces')
box('TerraceFrontWall',(-36.8,2.025,-55.85),(14.4,4.05,.3),'CreamPanel','Terraces')
for x in (-43.85,-29.75):box('TerraceSideWall',(x,2.025,-52.75),(.3,4.05,6.5),'CreamPanel','Terraces')
rail('TerraceFront',(-44,-55.85),(-29.6,-55.85),4.4,group='TerraceRails')
for x in (-44,-29.6):rail('TerraceSide',(x,-55.85),(x,-49.5),4.4,group='TerraceRails')
for x in (-42.4,-40.8,-39.2,-37.6,-36,-34.4,-32.8,-31.2):
    box('TerracePanelJoint',(x,2.1,-56.015),(.018,4.1,.018),'Frame','ExteriorDetails',collide=False)

# Large open roof rack behind the dome, as in the exterior references.
for x in (-14,-8,-2,4,10,15):
    beam('KursRackUpright',(x,11.1,-36),(x,22.2,-36),.13,'SteelOxide','RoofSign',True)
    beam('KursRackLeg',(x,11.1,-30),(x,21.8,-36),.12,'SteelOxide','RoofSign',True)
for y in (12.3,15.3,19,22):
    beam('KursRackCrossbar',(-14.5,y,-36),(15.5,y,-36),.105,'SteelOxide','RoofSign')
for x in (-14,-8,-2,4,10):beam('KursRackDiagonal',(x,12.3,-36),(x+6,22,-36),.07,'SteelOxide','RoofSign')
word=text_object('Курс',(-2.6,18.2,-36.18),6.2,'MallBlue','RoofKursText','RoofSignText')
# Scale letters to the reference width independently of installed font metrics.
xs=[v.co.x for v in word.data.vertices];factor=23/(max(xs)-min(xs))
for v in word.data.vertices:v.co.x=-2.6+(v.co.x+2.6)*factor
text_object('ТОРГОВЫЙ ЦЕНТР',(-1,15.7,-36.22),1.15,'SignRed','RoofSubtitle','RoofSignText')
def sail(name,points,material,group='RoofSignText'):
    n=len(points)
    mesh(name,points+[(x,y,z+.10) for x,y,z in points],
         [tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],material,group,False)
sail('KursSailBlue',[(10.2,18.3,-36.2),(12,22,-36.2),(14,19.3,-36.2)],'MallBlue')
sail('KursSailOrange',[(11.4,18.5,-36.3),(11,21.5,-36.3),(12.6,20.2,-36.3)],'SailOrange')
sail('KursSailHull',[(9.8,18,-36.3),(14.7,18.6,-36.3),(12.5,17.8,-36.3)],'CreamPanel')

# Tenant boards belong to the building's architecture, before settlement props.
text_object('familia',(-37,9.5,-50.08),2.2,'Lettering','FamiliaSign','TenantSigns')
text_object('Семья. Дом. Выгода.',(-37,8.65,-50.08),.49,'Lettering','FamiliaSub','TenantSigns')
text_object('КИНОСТРАХ',(-37,12.35,-50.08),1.05,'Lettering','CinemaSign','TenantSigns')

def curved_text(text,theta0,theta1,y,size,material,name):
    # Individual letters tangent to the elliptical fascia, facing outward.
    # Text geometry itself stays native and editable in the source.
    weights=[.45 if c==' ' else .65 for c in text];total=sum(weights);cursor=0
    for index,(char,w) in enumerate(zip(text,weights)):
        t=(cursor+w/2)/total;theta=theta0+(theta1-theta0)*t;cursor+=w
        x=30.23*math.cos(theta);z=-50-15.23*math.sin(theta)
        if char==' ':continue
        obj=text_object(char,(x,y,z),size,material,name+f'_{index:02}','TenantSigns')
        # Front at theta=pi/2 has zero rotation, side letters turn with fascia.
        angle=math.atan2(15.23*math.cos(theta),30.23*math.sin(theta))
        c,s=math.cos(angle),math.sin(angle)
        for v in obj.data.vertices:
            xx,zz=v.co.x-x,v.co.y-z
            v.co.x=x+c*xx-s*zz;v.co.y=z+s*xx+c*zz
curved_text('Перекрёсток',math.pi*.64,math.pi*.43,10.1,2.45,'Lettering','Perekrestok')
curved_text('СУПЕРМАРКЕТ',math.pi*.59,math.pi*.44,9.55,.63,'TenantGreen','Supermarket')
curved_text('Кораблик',math.pi*.32,math.pi*.06,10.25,1.8,'Lettering','Korablik')
theta=math.pi*.67
origin=Vector((30.3*math.cos(theta),10.9,-50-15.3*math.sin(theta)))
tangent=Vector((30.3*math.sin(theta),0,15.3*math.cos(theta))).normalized()
for ox,oy in ((-.37,-.37),(.37,-.37),(-.37,.37),(.37,.37)):
    points=[origin+tangent*(ox+.40*math.cos(2*math.pi*i/16))+Vector((0,oy+.40*math.sin(2*math.pi*i/16),0)) for i in range(16)]
    for a,b in zip(points,points[1:]+points[:1]):beam('GreenClover',a,b,.095,'TenantGreen','TenantSigns')
# Upper facade long blue retail bands, with intermittent signage rather than glass.
for x,y,z,text,size in [(-46.54,10.1,-10,'СПОРТМАСТЕР',1.05),(-46.54,10.1,37,'ДЕТСКИЙ МИР',1.1),(46.54,9.4,8,'ОДЕЖДА  ОБУВЬ',.85)]:
    obj=text_object(text,(0,y,0),size,'Lettering','SideTenant','TenantSigns')
    for v in obj.data.vertices:
        xx,zz=v.co.x,v.co.y
        v.co.x=x+(zz if x<0 else -zz);v.co.y=z+(-xx if x<0 else xx)

# New guards append to the original order; stage 1/2 guard IDs stay stable.
