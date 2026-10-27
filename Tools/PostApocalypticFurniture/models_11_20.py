"""Authored construction recipes for furniture catalogue items 101--200.

Dimensions and pivots use metres with Y up and the accessible face towards +Z.
Each visible door, drawer, cover and control is a named assembly, never a
material variation standing in for a different catalogue entry.
"""
from math import sin, cos, pi
import math
from geom import Geo


def handle(g, x, y, z, span=.12, vertical=False, mat='Chrome'):
    if vertical:
        g.path([(x,y-span/2,z),(x,y-span/2,z+.025),(x,y+span/2,z+.025),(x,y+span/2,z)], .007, mat, n=8)
    else:
        g.path([(x-span/2,y,z),(x-span/2,y,z+.025),(x+span/2,y,z+.025),(x+span/2,y,z)], .007, mat, n=8)


def screw(g, x, y, z, mat='Steel'):
    g.rod((x,y,z),(x,y,z+.003),.0045,mat,n=8)
    g.box((x,y,z+.0034),(.006,.001,.001), 'Rubber', bevel=0)


def feet(g,w,d,y=.075,mat='DarkWood',r=.025):
    for x in [-w*.40,w*.40]:
        for z in [-d*.37,d*.37]:
            g.rod((x,0,z),(x,y,z),r,mat,n=10)


def leaf_panel(g,x,y,w,h,z,mat,thickness=.023):
    """Construction-aware door front, contained inside the former solid envelope."""
    if mat in ['Wood','DarkWood','PaintedWood'] and w>.18 and h>.10:
        t=min(.045,w*.105,h*.13)
        for xx in [x-w/2+t/2,x+w/2-t/2]:g.box((xx,y,z),(t,h,thickness),mat,bevel=.003)
        for yy in [y-h/2+t/2,y+h/2-t/2]:g.box((x,yy,z),(w-2*t,t,thickness),mat,bevel=.003)
        g.box((x,y,z-.004),(w-2*t+.001,h-2*t+.001,thickness-.008),mat,bevel=.003)
    elif mat in ['Enamel','Steel','WhitePlastic'] and min(w,h)>.10:
        # One closed stamped/moulded shell. Separate overlapping skin/flange
        # boxes formerly put identical bevel polygons on the same surface.
        skin=.006 if mat in ['Enamel','Steel'] else .010
        edge=.012 if mat!='WhitePlastic' else .016
        front=z+thickness/2;back=z-thickness/2;vertices=[];faces=[]
        profiles=[(w-.004,h-.004,front),(w,h,front-.002),(w,h,back),(w-2*edge,h-2*edge,back),(w-2*edge,h-2*edge,front-skin)]
        for ww,hh,zz in profiles:
            a,b=ww/2,hh/2;e=min(.003,a*.2,b*.2)
            for xx,yy in [(-a+e,-b),(a-e,-b),(a,-b+e),(a,b-e),(a-e,b),(-a+e,b),(-a,b-e),(-a,-b+e)]:vertices.append((x+xx,y+yy,zz))
        faces.extend([tuple(range(7,-1,-1)),tuple(range(32,40))])
        for ring in range(4):
            for k in range(8):faces.append((ring*8+k,ring*8+(k+1)%8,(ring+1)*8+(k+1)%8,(ring+1)*8+k))
        g.add(vertices,faces,mat,True)
        g.colliders.append(dict(part=g.current,center=[x,y,front-skin/2],size=[w-.004,h-.004,skin],rotation=[0,0,0]))
        for xx in [x-w/2+edge/2,x+w/2-edge/2]:g.colliders.append(dict(part=g.current,center=[xx,y,z],size=[edge,h-.004,thickness-.004],rotation=[0,0,0]))
        for yy in [y-h/2+edge/2,y+h/2-edge/2]:g.colliders.append(dict(part=g.current,center=[x,yy,z],size=[w-2*edge,edge,thickness-.004],rotation=[0,0,0]))
    else:g.box((x,y,z),(w,h,thickness),mat,bevel=.005)


def small_bolt(g,p,r=.004,mat='Steel',axis='z'):
    x,y,z=p
    q=(x,y,z+.003) if axis=='z' else (x,y+.003,z)
    g.rod(p,q,r,mat,n=6,collider=False)
    if axis=='z':g.box((x,y,z+.0032),(r*1.3,.0008,.0005),'Rubber',bevel=0,collider=False)
    else:g.box((x,y+.0032,z),(r*1.3,.0005,.0008),'Rubber',bevel=0,collider=False)


def hollow_tube(g,points,r,thickness=.003,mat='Steel',n=16):
    from mathutils import Vector
    vs=[];fs=[]
    for k,p in enumerate(points):
        center=Vector(p);before=Vector(points[max(0,k-1)]);after=Vector(points[min(len(points)-1,k+1)])
        tangent=(after-before).normalized();u=tangent.cross(Vector((0,0,1)))
        if u.length<.01:u=tangent.cross(Vector((0,1,0)))
        u.normalize();v=tangent.cross(u)
        for radius in [r,r-thickness]:
            for j in range(n):
                a=j*2*pi/n;vs.append(tuple(center+radius*(u*cos(a)+v*sin(a))))
    for k in range(len(points)-1):
        for j in range(n):
            jj=(j+1)%n;a=k*2*n+j;b=k*2*n+jj;c=(k+1)*2*n+jj;d=(k+1)*2*n+j
            fs += [(a,b,c,d),(a+n,d+n,c+n,b+n)]
    for offset in [0,(len(points)-1)*2*n]:
        for j in range(n):
            q=(j+1)%n;fs.append((offset+j,offset+j+n,offset+q+n,offset+q))
    g.add(vs,fs,mat,True)


def l_slab(g,y,thick,mat='Wood'):
    # The two intersecting source rectangles are replaced by one concave slab.
    outline=[(-.58,-.29),(.01,-.29),(.01,-.70),(.39,-.70),(.39,.29),(-.58,.29)]
    vs=[(x,yy,z) for yy in [y-thick/2,y+thick/2] for x,z in outline];n=len(outline)
    fs=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)]
    g.add(vs,fs,mat,False)
    for x,z,w,d in [(-.285,0,.59,.58),(.20,-.205,.38,.99)]:g.colliders.append(dict(part=g.current,center=[x,y,z],size=[w,thick,d],rotation=[0,0,0]))


def pressed_sink(g):
    # Closed thin pressed sheet with rounded corners and an actual drain bore.
    profile=[(.052,.052,.690,True),(.40,.28,.690,False),(.50,.38,.847,False),(.55,.43,.877,False),(.515,.395,.880,False),(.478,.358,.847,False),(.386,.266,.704,False),(.052,.052,.704,True)]
    vs=[];fs=[];n=40
    for w,d,y,circle in profile:
        for j in range(n):
            a=j*2*pi/n
            xx=cos(a) if circle else math.copysign(abs(cos(a))**.30,cos(a))
            zz=sin(a) if circle else math.copysign(abs(sin(a))**.30,sin(a))
            vs.append((w*.5*xx,y,d*.5*zz))
    for k in range(len(profile)):
        kk=(k+1)%len(profile)
        for j in range(n):fs.append((k*n+j,k*n+(j+1)%n,kk*n+(j+1)%n,kk*n+j))
    g.add(vs,fs,'Chrome',True)
    for side in [-1,1]:
        g.colliders.append(dict(part=g.current,center=[side*.225,.7775,0],size=[.008,.155,.32],rotation=[0,0,side*-18]))
        g.colliders.append(dict(part=g.current,center=[0,.7775,side*.164],size=[.41,.155,.008],rotation=[side*20,0,0]))
    for x,z,w,d in [(-.1095,0,.165,.26),(.1095,0,.165,.26),(0,-.0785,.054,.103),(0,.0785,.054,.103)]:g.colliders.append(dict(part=g.current,center=[x,.6975,z],size=[w,.009,d],rotation=[0,0,0]))


def waste_trap(g,top_y,rear_z):
    # Rigid grey waste piping is kept at the rear, away from placement zones.
    points=[(0,top_y,0),(0,top_y-.030,0),(0,top_y-.030,rear_z),(0,top_y-.16,rear_z),(.055,top_y-.19,rear_z),(.11,top_y-.16,rear_z),(.11,top_y-.045,rear_z)]
    hollow_tube(g,points,.018,.003,'WhitePlastic',n=14)
    for x,y,z in [points[1],points[3],points[-1]]:g.ring((x,y,z),.022,.003,'WhitePlastic',axis='y',n=16)


def phone_cord_source(g):
    """Closed-pose equivalent of FurnitureCordVisual, one continuous capped tube."""
    from mathutils import Vector
    a=Vector((-.119,.052,-.040));b=Vector((-.135,.118,-.035))
    c1=a+Vector((-.07,0,0));c2=b+Vector((-.07,0,0));samples=128;sides=6;centers=[]
    previous_tangent=None;normal=None
    def frame(tangent,previous,normal):
        if previous is None:
            normal=tangent.cross(Vector((0,1,0)))
            if normal.length<1e-7:normal=tangent.cross(Vector((0,0,1)))
        else:normal=previous.rotation_difference(tangent)@normal
        normal-=tangent*normal.dot(tangent);normal.normalize()
        return normal,tangent.cross(normal)
    for k in range(samples):
        t=k/(samples-1);u=1-t
        center=a*u**3+c1*(3*u*u*t)+c2*(3*u*t*t)+b*t**3
        tangent=((c1-a)*(3*u*u)+(c2-c1)*(6*u*t)+(b-c2)*(3*t*t)).normalized()
        normal,binormal=frame(tangent,previous_tangent,normal);previous_tangent=tangent
        edge=max(0,min(1,min(t/.06,(1-t)/.06)));envelope=edge*edge*(3-2*edge);angle=t*20*2*pi
        centers.append(center+(normal*cos(angle)+binormal*sin(angle))*.0055*envelope)
    centers[0]=a;centers[-1]=b;vs=[];fs=[];previous_tangent=None;normal=None
    for k,center in enumerate(centers):
        tangent=(centers[min(samples-1,k+1)]-centers[max(0,k-1)]).normalized()
        normal,binormal=frame(tangent,previous_tangent,normal);previous_tangent=tangent
        for j in range(sides):
            angle=j*2*pi/sides;vs.append(tuple(center+(normal*cos(angle)+binormal*sin(angle))*.0012))
    for k in range(samples-1):
        for j in range(sides):fs.append((k*sides+j,k*sides+(j+1)%sides,(k+1)*sides+(j+1)%sides,(k+1)*sides+j))
    fs += [tuple(range(sides-1,-1,-1)),tuple((samples-1)*sides+j for j in range(sides))]
    g.part('CordSource',kind='static',amount=0,label='Исходный витой провод')
    g.add(vs,fs,'Rubber',True)
    g.static()


def burned_rug(g,w,d,mat):
    from math import atan2
    hx,hz=w*.11,-d*.08
    angles=[2*pi*k/40 for k in range(40)]+[atan2(z-hz,x-hx)%(2*pi) for x,z in [(-w/2,-d/2),(w/2,-d/2),(w/2,d/2),(-w/2,d/2)]]
    angles=sorted(set(round(a,10) for a in angles));n=len(angles);vs=[];fs=[];inner=[];steps=[0,.10,.45,1]
    for layer in [0,1]:
        for t in steps:
            for a in angles:
                dx,dz=cos(a),sin(a);rs=[]
                if abs(dx)>1e-9:rs.append(((w/2 if dx>0 else -w/2)-hx)/dx)
                if abs(dz)>1e-9:rs.append(((d/2 if dz>0 else -d/2)-hz)/dz)
                outer=min(q for q in rs if q>0)
                hole=1/math.sqrt((dx/.14)**2+(dz/.10)**2)*(1+.14*sin(a*5)+.09*cos(a*9))
                radius=hole+(outer-hole)*t;x=hx+dx*radius;z=hz+dz*radius
                top=.006+.003*(1+sin(x*11+z*8))*.5+.008*(1-t)**5*(.5+.5*sin(a*3))
                vs.append((x,top-(.006 if layer else 0),z))
                if layer==0 and t==0:inner.append((x,top+.0005,z))
    stride=n*len(steps)
    for layer in [0,1]:
        for k in range(len(steps)-1):
            for j in range(n):
                q=layer*stride+k*n+j;r=layer*stride+k*n+(j+1)%n
                f=(q,r,r+n,q+n);fs.append(f if layer==0 else tuple(reversed(f)))
    for k in [0,len(steps)-1]:
        for j in range(n):
            a=k*n+j;b=k*n+(j+1)%n;fs.append((a,a+stride,b+stride,b) if k==0 else (a,b,b+stride,a+stride))
    g.add(vs,fs,mat,True)
    g.path(inner+[inner[0]],.0021,'Rubber',n=5,collider=False)
    for k,p in enumerate(inner):
        if k%3:continue
        x,y,z=p;dx,dz=hx-x,hz-z;length=math.hypot(dx,dz)
        g.path([p,(x+dx/length*.012,y+.002,z+dz/length*.012),(x+dx/length*.019,y+.001,z+dz/length*.019)],.0008,'Rubber',n=5,collider=False)
    bx0,bx1=hx-.18,hx+.18;bz0,bz1=hz-.14,hz+.14
    for x,z,sw,sd in [((-w/2+bx0)/2,0,bx0+w/2,d),((bx1+w/2)/2,0,w/2-bx1,d),(hx,(-d/2+bz0)/2,bx1-bx0,bz0+d/2),(hx,(bz1+d/2)/2,bx1-bx0,d/2-bz1)]:g.colliders.append(dict(part=g.current,center=[x,.004,z],size=[sw,.008,sd],rotation=[0,0,0]))


def torn_rug(g,w,d,mat):
    nx,nz=24,16;vs=[];fs=[];outline=[]
    for layer in [0,1]:
        for j in range(nz+1):
            z=-d/2+d*j/nz;t=max(0,(j-10)/6)
            cut=.20*t**1.4+(.014*sin(j*4.7) if t else 0)
            for i in range(nx+1):
                x=-w/2+(w-cut)*i/nx
                y=.006+.003*(1+sin(x*13-z*9))*.5+.012*(i/nx)**8*t
                vs.append((x,y-(.006 if layer else 0),z))
    stride=(nx+1)*(nz+1)
    for layer in [0,1]:
        for j in range(nz):
            for i in range(nx):
                q=layer*stride+j*(nx+1)+i;f=(q,q+nx+1,q+nx+2,q+1);fs.append(f if layer==0 else tuple(reversed(f)))
    rim=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,nz+1)]+[nz*(nx+1)+i for i in range(nx-1,-1,-1)]+[j*(nx+1) for j in range(nz-1,0,-1)]
    for a,b in zip(rim,rim[1:]+rim[:1]):fs.append((a,b,b+stride,a+stride))
    g.add(vs,fs,mat,True)
    for j in range(10,nz+1):
        p=vs[j*(nx+1)+nx];x,y,z=p
        for k in [-1,1]:g.path([p,(x+.013,y+.003,z+k*.003),(x+.024,y+.001,z+k*.007)],.0009,'Fabric',n=5,collider=False)
    for j in range(nz):
        z=-d/2+d*(j+.5)/nz;t=max(0,(j+.5-10)/6);cut=.20*t**1.4+(.014*sin((j+.5)*4.7) if t else 0)
        g.colliders.append(dict(part=g.current,center=[-cut/2,.004,z],size=[w-cut,.008,d/nz],rotation=[0,0,0]))


def shell(g,w,h,d,b=.08,mat='Wood',shelves=2,t=.021,back='Plywood',legs=True,zones=True):
    """Open-front carcass, with actual placement space between panels."""
    g.static()
    if legs: feet(g,w,d,b,mat)
    g.box((-w/2+t/2,(h+b)/2,0),(t,h-b,d),mat)
    g.box((w/2-t/2,(h+b)/2,0),(t,h-b,d),mat)
    g.box((0,b+t/2,0),(w-2*t,t,d),mat)
    g.box((0,h-t/2,0),(w-2*t,t,d),mat)
    g.box((0,(h+b)/2,-d/2+.006),(w-2*t,h-b,.012),back,bevel=.002)
    if mat in ['Wood','DarkWood','PaintedWood','WhiteLaminate','Chipboard']:
        # Rear fixing cleats and exposed corner heads are real assembly details.
        for yy in [b+.028,h-.045]:
            g.box((0,yy,-d/2+.018),(w-.035,.025,.015),'Plywood',bevel=.002)
            for xx in [-w*.36,w*.36]:small_bolt(g,(xx,yy,-d/2+.026),.0032,'Steel')
    ys=[]
    for k in range(shelves):
        y=b+(h-b)*(k+1)/(shelves+1)
        g.box((0,y,0),(w-2*t,.018,d-.025),mat,bevel=.003)
        ys.append(y)
    levels=[b+t]+ys+[h-t]
    if zones:
        for k,(lo,hi) in enumerate(zip(levels,levels[1:])):
            g.zone('Compartment_%02d'%k,(0,(lo+hi)/2,.01),(w-2*t-.02,hi-lo-.035,d-.06))


def door(g,name,x,y,w,h,z,mat='Wood',side='left',glass=False,knob='Chrome',angle=105):
    px=x-w/2 if side=='left' else x+w/2
    g.part(name,pivot=(px,y-h/2,z),kind='hinge',axis=(0,1,0),amount=-angle if side=='left' else angle,label='Дверца')
    if glass:
        t=.037
        g.box((x-w/2+t/2,y,z),(t,h,.026),mat)
        g.box((x+w/2-t/2,y,z),(t,h,.026),mat)
        g.box((x,y-h/2+t/2,z),(w-2*t,t,.026),mat)
        g.box((x,y+h/2-t/2,z),(w-2*t,t,.026),mat)
        g.box((x,y,z-.003),(w-2*t,h-2*t,.006),'Glass',bevel=.001)
        iw,ih=w-2*t,h-2*t
        g.path([(x-iw/2,y-ih/2,z+.002),(x+iw/2,y-ih/2,z+.002),(x+iw/2,y+ih/2,z+.002),(x-iw/2,y+ih/2,z+.002),(x-iw/2,y-ih/2,z+.002)],.0024,'Rubber',n=6,collider=False)
    else:
        leaf_panel(g,x,y,w,h,z,mat)
    hx=x+w*.34 if side=='left' else x-w*.34
    handle(g,hx,y,z+.016,.09,True,knob)
    for yy in [y-h*.32,y+h*.32]:
        g.rod((px,yy-.023,z),(px,yy+.023,z),.010,knob,n=10)
    g.static()


def double_doors(g,w,y,h,z,mat='Wood',prefix='Door',glass=False):
    dw=(w-.010)/2
    door(g,prefix+'_L',-w/4,y,dw,h,z,mat,'left',glass)
    door(g,prefix+'_R',w/4,y,dw,h,z,mat,'right',glass)


def drawer(g,name,x,y,w,h,d,z,mat='Wood',missing=False):
    g.part(name,pivot=(x,y,z),kind='slide',axis=(0,0,1),amount=d*.74,label='Ящик')
    # z is the front face. All of the tray moves with the front.
    leaf_panel(g,x,y,w,h,z,mat,.022)
    rear=z-d+.025
    g.box((x,y-h*.38,z-d/2),(w-.025,.014,d-.025),'Plywood',bevel=.002)
    for xx in [x-w*.47,x+w*.47]:
        g.box((xx,y-h*.03,z-d/2),(.015,h*.75,d-.025),'Plywood',bevel=.002)
    g.box((x,y-h*.03,rear),(w-.025,h*.75,.014),'Plywood',bevel=.002)
    if h>.075:
        for xx in [x-w*.43,x+w*.43]:
            small_bolt(g,(xx,y-h*.22,z-.013),.0028,'Steel')
    if not missing:handle(g,x,y,z+.017,min(.15,w*.36))
    else:
        screw(g,x-.055,y,z+.014)
        screw(g,x+.055,y,z+.014)
    if h>=.065:
        g.zone(name+'_Storage',(x,y+h*.04,z-d*.5),(w*.90-.015,h*.57,d-.07))
    g.static()
    # The runners remain on the cabinet, outside the moving tray.
    for xx in [x-w*.50,x+w*.50]:
        g.box((xx,y-h*.27,z-d*.52),(.008,.012,d*.87),'Steel',bevel=.001)


def knob(g,name,x,y,z,kind='power',r=.018,mat='Plastic',label=None):
    g.part(name,pivot=(x,y,z),kind=kind,axis=(0,0,1),amount=28,label=label or {'power':'Питание','channel':'Канал','volume':'Громкость','brightness':'Яркость','tune':'Настройка'}.get(kind,'Регулятор'))
    g.rod((x,y,z),(x,y,z+.021),r,mat,n=16,collider=True)
    g.box((x,y+r*.48,z+.022),(.003,r*.65,.002),'WhitePlastic',bevel=0)
    g.static()


def button(g,name,x,y,z,kind='power',w=.023,h=.016,label='Питание'):
    g.part(name,pivot=(x,y,z),kind=kind,axis=(0,0,1),amount=.004,label=label)
    g.box((x,y,z+.005),(w,h,.012),'Plastic',bevel=.003)
    g.static()


def hooks(g,w,y,z,n=5,mat='Steel',xoffset=0):
    for k in range(n):
        x=xoffset+(k-(n-1)/2)*w/max(1,n-1)
        g.rod((x,y-.016,z),(x,y+.016,z),.012,mat,n=10)
        g.path([(x,y,z),(x,y-.052,z+.055),(x,y-.052,z+.078),(x,y-.028,z+.084)],.008,mat,n=8)
        screw(g,x,y+.019,z+.009)


def washer_front(g):
    """A closed sheet-metal rectangle with a genuine circular service aperture."""
    cx,cy,front,back=0,.40,.297,.281
    xlo,xhi,ylo,yhi=-.275,.275,.10,.6325
    angles=[k*2*pi/32 for k in range(32)]
    from math import atan2
    angles+= [atan2(y-cy,x-cx)%(2*pi) for x,y in [(xlo,ylo),(xhi,ylo),(xhi,yhi),(xlo,yhi)]]
    angles=sorted(set(round(a,10) for a in angles));n=len(angles)
    outer=[];inner=[]
    for a in angles:
        dx,dy=cos(a),sin(a)
        distances=[]
        if abs(dx)>1e-9:distances.append((xhi if dx>0 else xlo)/dx)
        if abs(dy)>1e-9:distances.append(((yhi if dy>0 else ylo)-cy)/dy)
        r=min(t for t in distances if t>0)
        outer.append((cx+dx*r,cy+dy*r))
        inner.append((cx+dx*.205,cy+dy*.205))
    vs=[]
    for z,ring in [(front,outer),(front,inner),(back,outer),(back,inner)]:vs += [(x,y,z) for x,y in ring]
    fs=[]
    for i in range(n):
        j=(i+1)%n
        fs += [(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)]
    g.add(vs,fs,'Enamel',False)
    # Simple proxies follow the solid sheet and leave the drum opening clear.
    for x in [-.245,.245]:g.colliders.append(dict(part=g.current,center=[x,.36625,.289],size=[.06,.5325,.016],rotation=[0,0,0]))
    for y,h in [(.1425,.085),(.62375,.0175)]:g.colliders.append(dict(part=g.current,center=[0,y,.289],size=[.43,h,.016],rotation=[0,0,0]))
    for a in [45,135,225,315]:
        rad=a*pi/180
        g.colliders.append(dict(part=g.current,center=[.24*cos(rad),cy+.24*sin(rad),.289],size=[.05,.12,.016],rotation=[0,0,a]))


def shelves(row):
    g=Geo(row);v=row['variant']
    if v==0: # A wall-mounted book shelf has brackets and a restrained retaining rail.
        w,h,d=1.05,.52,.25
        for y in [.022,h-.022]:g.box((0,y,0),(w,.035,d),'DarkWood')
        for x in [-w/2+.017,w/2-.017]:g.box((x,h/2,0),(.034,h,d),'DarkWood')
        g.box((0,h/2,-d/2),(w-.05,h-.05,.009),'Plywood',bevel=.001)
        for x in [-.37,.37]:
            g.path([(x,.06,-.12),(x,.01,-.12),(x,.01,.10)],.009,'Steel')
            screw(g,x,.17,-.114)
            g.box((x,.22,-.131),(.060,.14,.010),'Steel',bevel=.003)
            small_bolt(g,(x,.27,-.124),.004,'Steel')
        g.zone('Books',(0,.25,0),(.98,.42,.21))
    elif v==1:
        w,h,d=.88,1.72,.31
        for x in [-.405,.405]:
            for z in [-.135,.135]:g.box((x,h/2,z),(.045,h,.045),'Wood')
        for y in [.08,.46,.85,1.24,1.66]:
            g.box((0,y,0),(w,.035,d),'Wood')
            g.zone('Shelf_%d'%int(y*100),(0,y+.15,0),(.74,.25,.22))
            for x in [-.389,.389]:g.box((x,y-.035,0),(.045,.035,d-.04),'DarkWood',bevel=.003)
        for x in [-.39,.39]:g.rod((x,.12,-.14),(-x,1.62,-.14),.008,'Steel',n=8)
    elif v==2:
        w,h,d=.90,1.85,.42
        for x in [-.43,.43]:
            for z in [-.19,.19]:
                # Rolled-angle uprights, rather than four solid timber poles.
                g.box((x,h/2,z),(.027,h,.004),'Steel',bevel=.001)
                g.box((x-.012,h/2,z+.012),(.004,h,.027),'Steel',bevel=.001)
                for y in [.1,.5,.9,1.3,1.7]:g.box((x+.003,y,z+.004),(.007,.012,.002),'Rubber',bevel=0)
        for k in range(5):
            y=.10+k*.42
            g.box((0,y,0),(w,.018,d),'Steel',bevel=.002)
            for z in [-.20,.20]:g.box((0,y-.017,z),(w,.035,.012),'Steel',bevel=.002)
            g.zone('Shelf_%d'%k,(0,y+.16,0),(.82,.30,.35))
            for x in [-.438,.438]:
                g.box((x,y-.015,0),(.024,.045,.42),'Steel',bevel=.002)
                small_bolt(g,(x,y-.017,.217),.004,'Steel')
        g.rod((-.42,.12,-.2),(.42,1.79,-.2),.008,'Steel',n=8)
        g.rod((.42,.12,-.2),(-.42,1.79,-.2),.008,'Steel',n=8)
    elif v==3:
        w,h,d=1.02,1.82,.35
        shell(g,w,h,d,.09,'DarkWood',4)
        double_doors(g,w-.025,1.11,1.31,d/2+.01,'DarkWood',glass=True)
        double_doors(g,w-.025,.255,.29,d/2+.01,'DarkWood',prefix='Base')
        g.box((0,h+.015,0),(w+.055,.055,d+.035),'DarkWood')
        g.box((0,h+.050,0),(w+.085,.018,d+.060),'DarkWood',bevel=.004)
    elif v==4:
        # Open quarter-depth corner shelving built from two overlapping narrow runs.
        for k,y in enumerate([.05,.43,.81,1.19,1.57]):
            g.box((-.18,y,0),(.76,.028,.30),'Wood')
            g.box((.20,y,-.20),(.31,.028,.68),'Wood')
            g.zone('CornerShelf%d'%k,(-.18,y+.12,0),(.66,.21,.24))
            g.zone('CornerReturn%d'%k,(.20,y+.12,-.32),(.24,.21,.35))
        for x,z in [(-.54,-.12),(-.54,.12),(.33,.12),(.33,-.53),(.07,-.53)]:g.box((x,.82,z),(.035,1.65,.035),'Wood')
        g.rod((.33,.16,-.53),(.33,1.52,.12),.0065,'Steel',n=8,collider=False)
    elif v==5:
        g.box((0,.035,0),(.96,.05,.24),'PaintedWood')
        for x in [-.39,.39]:
            g.rod((x,.06,-.09),(x,.65,-.09),.008,'Steel',n=8)
            g.rod((x,.06,.09),(x,.65,-.09),.008,'Steel',n=8)
            g.ring((x,.66,-.09),.018,.003,'Steel',axis='z',n=12)
            g.box((x,.66,-.105),(.055,.065,.014),'Steel',bevel=.003)
            small_bolt(g,(x,.675,-.096),.004,'Steel')
        g.zone('SuspendedShelf',(0,.17,0),(.88,.22,.20))
    elif v==6:
        shell(g,.72,1.56,.28,.02,'Chipboard',4,legs=False)
        for side in [-1,1]:g.rod((side*.31,.08,-.147),(-side*.31,1.50,-.147),.0035,'Steel',n=6,collider=False)
        for x in [-.31,.31]:
            for y in [.08,1.50]:g.box((x,y,-.142),(.025,.025,.008),'Steel',bevel=.002)
        for yy in [.12,.43,.74,1.05,1.36]:
            for xx in [-.34,.34]:g.rod((xx,yy,-.03),(xx+.004,yy,-.03),.003,'Chrome',n=6)
    elif v==7:
        for k in range(4):
            y=.06+k*.38
            for j in range(3):g.box((0,y+(j-1)*.003,(j-1)*.095),(.93,.028,.087),'Wood',rot=(0,(j-1)*.5,(j-1)*.4))
            g.zone('PlankShelf%d'%k,(0,y+.14,0),(.82,.24,.24))
        for x in [-.39,.39]:
            g.box((x,.64,-.14),(.055,1.29,.04),'Wood',rot=(0,0,(-1 if x<0 else 1)*1.2))
            for y in [.06,.44,.82,1.20]:screw(g,x,y,-.115,'Rust')
            for y in [.06,.44,.82,1.20]:g.box((x,y-.0315,0),(.045,.035,.27),'Wood',bevel=.002)
        g.rod((-.39,.1,-.14),(.39,1.23,-.14),.009,'Rust',n=8)
    elif v==8:
        shell(g,.94,1.55,.37,.09,'WhiteLaminate',3)
        double_doors(g,.91,.48,.72,.195,'WhiteLaminate')
        g.box((0,1.13,.192),(.89,.013,.015),'Chrome',bevel=.002)
    else:
        w,h,d=1.38,1.86,.44
        shell(g,w,h,d,.11,'DarkWood',3,zones=False)
        g.box((0,.90,0),(.023,1.65,d-.03),'DarkWood')
        levels=[.131,.5475,.985,1.4225,1.839]
        for k,(lo,hi) in enumerate(zip(levels,levels[1:])):
            for side in [-1,1]:g.zone('Side_%d_Level_%d'%(side,k),(side*.34,(lo+hi)/2,.01),(.62,hi-lo-.035,d-.06))
        double_doors(g,w-.03,1.30,1.05,d/2+.012,'DarkWood',prefix='Glazed',glass=True)
        double_doors(g,w-.03,.40,.54,d/2+.012,'DarkWood',prefix='Lower')
        g.box((0,h+.026,0),(w+.07,.07,d+.055),'DarkWood')
        g.box((0,h+.068,0),(w+.105,.018,d+.08),'DarkWood',bevel=.004)
        for x in [-.60,.60]:
            g.part('Glazed_L' if x<0 else 'Glazed_R')
            g.box((x,1.27,.248),(.033,1.05,.016),'Brass')
        g.static()
    return g


def kitchen(row):
    g=Geo(row);v=row['variant']
    if v==0:
        shell(g,.60,.84,.57,.10,'PaintedWood',1)
        g.box((0,.86,.015),(.63,.042,.63),'WhiteLaminate')
        door(g,'Door',0,.47,.555,.69,.298,'PaintedWood')
        g.box((0,.055,.24),(.53,.09,.027),'PaintedWood')
    elif v==1:
        shell(g,.72,.73,.31,.015,'WhiteLaminate',1,legs=False)
        double_doors(g,.69,.37,.69,.166,'WhiteLaminate')
        for x in [-.25,.25]:g.box((x,.68,-.164),(.045,.065,.005),'Steel',bevel=.001)
    elif v==2:
        # L-shaped kitchen corner module. The inset service door opens independently.
        l_slab(g,.85,.040,'WhiteLaminate')
        l_slab(g,.11,.025,'PaintedWood')
        l_slab(g,.44,.018,'PaintedWood')
        g.box((-.565,.47,0),(.022,.72,.58),'PaintedWood')
        g.box((.375,.47,-.205),(.022,.72,.99),'PaintedWood')
        g.box((-.285,.47,-.283),(.59,.72,.014),'Plywood')
        g.box((.20,.47,-.693),(.38,.72,.014),'Plywood')
        g.box((.018,.47,-.495),(.016,.72,.41),'PaintedWood')
        door(g,'CornerDoor',-.11,.46,.86,.68,.30,'PaintedWood')
        g.zone('CornerStorage',(-.285,.28,0),(.53,.25,.48))
        g.zone('CornerReturnStorage',(.20,.28,-.38),(.29,.25,.50))
        for x,z in [(-.48,.20),(-.48,-.20),(.29,.20),(.29,-.59)]:g.box((x,.05,z),(.05,.10,.05),'DarkWood')
    elif v==3:
        shell(g,.80,.84,.57,.10,'WhiteLaminate',0)
        g.zones[-1]['center'][1]=.38;g.zones[-1]['size'][1]=.50
        g.zones[-1]['center'][2]=.0475;g.zones[-1]['size'][2]=.375
        # Worktop is made around a genuine open sink, not a solid slab over it.
        for x in [-.335,.335]:g.box((x,.855,0),(.13,.035,.61),'WhiteLaminate')
        for z in [-.255,.255]:g.box((0,.855,z),(.54,.035,.10),'WhiteLaminate')
        pressed_sink(g)
        g.ring((0,.695,0),.026,.005,'Steel',axis='y',n=16)
        waste_trap(g,.695,-.22)
        g.path([(.22,.86,-.21),(.22,1.07,-.21),(.15,1.13,-.21),(.05,1.13,-.17),(.05,1.04,-.09)],.013,'Chrome',n=12)
        g.ring((.22,.875,-.21),.025,.003,'Chrome',axis='y',n=20)
        g.ring((.05,1.04,-.09),.014,.002,'Chrome',axis='y',n=16)
        knob(g,'Tap',.27,.895,-.205,'power',.021,'Chrome','Кран')
        double_doors(g,.76,.46,.67,.298,'WhiteLaminate')
    elif v==4:
        shell(g,.55,.84,.57,.095,'PaintedWood',0,zones=False)
        g.box((0,.86,.01),(.58,.038,.62),'WhiteLaminate')
        for k in range(4):drawer(g,'Drawer_%d'%k,0,.185+k*.181,.505,.165,.51,.298,'PaintedWood')
    elif v==5:
        shell(g,.53,2.05,.55,.10,'WhiteLaminate',0,zones=False)
        for k,y in enumerate([.77,1.15,1.55]):
            g.box((0,y,0),(.485,.018,.525),'WhiteLaminate',bevel=.003)
            hi=[1.15,1.55,2.029][k]
            g.zone('PantryShelf%d'%k,(0,(y+hi)/2,.01),(.46,hi-y-.04,.49))
        door(g,'UpperDoor',0,1.37,.49,1.29,.288,'WhiteLaminate')
        for k in range(2):drawer(g,'Drawer%d'%k,0,.25+k*.24,.49,.215,.49,.288,'WhiteLaminate')
        g.box((0,.625,0),(.485,.018,.525),'WhiteLaminate',bevel=.003)
        g.zone('OpenUtensilSlot',(0,.665,.01),(.44,.052,.45))
        g.box((0,2.065,0),(.56,.035,.57),'WhiteLaminate')
    elif v==6:
        # A preparation island with a shallow utensil drawer and open bottom shelf.
        shell(g,1.12,.87,.63,.10,'Wood',1)
        g.zones[-1]['center'][1]=.575;g.zones[-1]['size'][1]=.14
        g.box((0,.895,0),(1.19,.055,.69),'DarkWood')
        drawer(g,'UtensilDrawer',0,.73,1.05,.17,.56,.33,'Wood')
        double_doors(g,1.07,.395,.49,.33,'Wood')
        g.rod((-.63,.78,-.20),(-.63,.78,.20),.013,'Chrome',n=12)
        for z in [-.2,.2]:g.rod((-.56,.78,z),(-.63,.78,z),.009,'Chrome',n=8)
    elif v==7:
        shell(g,1.08,.94,.49,.11,'PaintedWood',1)
        g.zones[-1]['center'][1]=.6525;g.zones[-1]['size'][1]=.215
        double_doors(g,1.04,.47,.67,.26,'PaintedWood',prefix='Base')
        drawer(g,'Cutlery',0,.84,1.03,.14,.43,.26,'PaintedWood')
        g.box((0,.965,0),(1.14,.045,.56),'WhiteLaminate')
        # Raised hutch with visible gap above the worktop.
        for x in [-.51,.51]:g.box((x,1.50,-.07),(.035,1.06,.31),'PaintedWood')
        g.box((0,2.02,-.07),(1.06,.035,.31),'PaintedWood')
        g.box((0,1.35,-.215),(1.03,.74,.013),'Plywood')
        g.box((0,1.49,-.07),(1.02,.02,.29),'PaintedWood')
        double_doors(g,1.02,1.75,.49,.092,'PaintedWood',prefix='Hutch',glass=True)
        g.box((0,2.06,-.07),(1.16,.06,.37),'PaintedWood')
        g.zone('HutchOpen',(0,1.20,-.05),(.97,.35,.26))
    elif v==8:
        shell(g,.82,.73,.32,.02,'PaintedWood',0,legs=False)
        g.zones.clear()
        double_doors(g,.78,.37,.68,.172,'PaintedWood')
        for y in [.14,.43]:
            for k in range(10):
                x=-.32+k*.071
                g.rod((x,y,-.12),(x,y,.10),.0035,'Steel',n=6)
                g.path([(x,y,-.10),(x,y+.08,-.07),(x,y+.08,.04),(x,y,.08)],.0025,'Steel',n=6)
        g.box((0,.075,0),(.74,.014,.28),'WhitePlastic')
        g.zone('DryingRack',(0,.29,0),(.70,.12,.22))
        g.zone('UpperDryingRack',(0,.58,0),(.70,.12,.22))
    else:
        shell(g,.91,.88,.54,.10,'DarkWood',2)
        double_doors(g,.87,.49,.72,.283,'DarkWood',glass=True)
        g.box((0,.902,0),(.96,.045,.59),'WhiteLaminate')
        for x in [-.40,.40]:
            g.part('Door_L' if x<0 else 'Door_R')
            for y in [.19,.77]:screw(g,x,y,.298,'Brass')
        g.static()
    return g


def oven(g,w=.6,h=.85,d=.58,gas=False,table=False):
    # Hollow oven box, enamel liner and a bottom-hinged glazed door.
    b=.055
    shell(g,w,h,d,b,'Enamel',0,t=.025,back='Enamel',zones=False)
    g.box((0,h*.43,-.05),(w-.12,.014,d-.12),'Steel')
    for k in range(7):g.rod((-(w-.14)/2,h*.43,-d*.35+k*(d*.70/6)),((w-.14)/2,h*.43,-d*.35+k*(d*.70/6)),.0035,'Chrome',n=6)
    g.part('OvenDoor',pivot=(0,.10,d/2+.018),kind='hinge',axis=(1,0,0),amount=88,label='Дверца духовки')
    dw=w-.035;dh=h*.68;cy=.10+dh/2
    face_panel(g,-dw/2,dw/2,.10,.10+dh,d/2+.018,[(-dw*.375,dw*.375,cy-dh*.31,cy+dh*.31)],'Enamel',thickness=.035)
    g.box((0,cy,d/2+.038),(dw*.75,dh*.62,.008),'Glass',bevel=.012)
    g.box((0,cy,d/2+.004),(dw*.75,dh*.62,.004),'Glass',bevel=.008)
    g.path([(-dw*.375,cy-dh*.31,d/2+.035),(dw*.375,cy-dh*.31,d/2+.035),(dw*.375,cy+dh*.31,d/2+.035),(-dw*.375,cy+dh*.31,d/2+.035),(-dw*.375,cy-dh*.31,d/2+.035)],.003,'Rubber',n=6,collider=False)
    handle(g,0,cy+dh*.34,d/2+.041,dw*.69)
    g.static()
    g.box((0,h-.075,d/2),(w-.025,.12,.028),'Enamel')
    for k in range(4 if not table else 2):knob(g,'BurnerControl%d'%k,(k-1.5 if not table else k-.5)*w*.20,h-.075,d/2+.026,'power',.022,'Plastic','Регулятор нагрева')
    if gas:
        for x in [-w*.26,w*.26]:
            for z in [-d*.24,d*.24]:
                g.ring((x,h+.019,z),.065,.009,'Steel',axis='y',n=18)
                g.rod((x,h+.005,z),(x,h+.025,z),.032,'Steel',n=16)
                for a in [0,pi/2,pi,3*pi/2]:
                    g.rod((x+cos(a)*.047,h+.035,z+sin(a)*.047),(x+cos(a)*.10,h+.035,z+sin(a)*.10),.006,'Steel',n=6)
        g.part('HobLid',pivot=(0,h+.065,-d/2),kind='hinge',axis=(1,0,0),amount=-98,label='Крышка плиты')
        g.box((0,h+.071,0),(w,.014,d),'Enamel')
        g.static()
        for x in [-w*.35,w*.35]:
            g.box((x,h+.0365,-d/2),(.022,.073,.025),'Steel',bevel=.003)
        g.rod((-w*.39,h+.065,-d/2),(w*.39,h+.065,-d/2),.009,'Steel',n=12)
    elif not table:
        for x in [-w*.26,w*.26]:
            for z in [-d*.24,d*.24]:
                g.rod((x,h+.001,z),(x,h+.010,z),.085,'Steel',n=24)
                for r in [.027,.048,.068]:g.ring((x,h+.012,z),r,.0025,'Steel',axis='y',n=20)
    g.zone('OvenCavity',(0,(h*1.23+.02)/2,0),(w-.15,h*.37-.02,d-.16))
    g.device(kind='device')


def kettle(g,electric):
    # Turned body section and a real liftable lid; the spout remains visibly hollow.
    if electric:
        g.lathe((0,.045,0),[(.10,0),(.115,.025),(.115,.17),(.083,.225),(.077,.225),(.105,.165),(.104,.027),(.09,.012)],'WhitePlastic',n=28,closed=True)
        g.lathe((0,0,0),[(.115,0),(.12,.017),(.112,.034),(.02,.034),(.02,0)],'Plastic',n=24)
        g.box((-.104,.15,0),(.018,.11,.034),'Glass',bevel=.006)
        g.path([(.095,.23,0),(.155,.235,0),(.184,.19,0),(.184,.075,0),(.114,.07,0)],.015,'Plastic',n=12)
        hollow_tube(g,[(-.09,.22,0),(-.145,.22,0),(-.15,.245,0)],.018,.003,'WhitePlastic',n=16)
        g.part('KettleLid',pivot=(.065,.268,0),kind='hinge',axis=(0,0,1),amount=-95,label='Крышка чайника')
        g.lathe((0,.266,0),[(.083,0),(.086,.008),(.065,.015),(.0,.019)],'Plastic',n=28,collider=True)
        g.static();button(g,'Power',.123,.075,.018,'power',.028,.020)
        g.box((-.10,.07,.055),(.015,.008,.003),'Emission',bevel=.003)
        g.path([(.03,.015,-.105),(.09,.012,-.17),(.20,.012,-.19),(.25,.012,-.14)],.004,'Rubber',n=8)
        g.device(kind='device')
    else:
        g.lathe((0,.02,0),[(.09,0),(.14,.035),(.15,.11),(.12,.19),(.075,.20),(.07,.193),(.112,.181),(.139,.11),(.13,.04),(.085,.012)],'Steel',n=32,closed=True)
        hollow_tube(g,[(.13,.09,0),(.20,.13,0),(.245,.22,0)],.024,.003,'Steel',n=18)
        g.ring((.245,.22,0),.024,.0035,'Steel',axis='y',n=16)
        g.path([(-.11,.11,0),(-.15,.25,0),(-.07,.34,0),(.06,.34,0),(.105,.18,0)],.012,'Plastic',n=12)
        # Pull the removable lid up and forwards through the carry-handle arch.
        g.part('KettleLid',pivot=(0,.22,0),kind='lift',axis=(0,.25,.9682458),amount=.18,label='Крышка чайника')
        g.lathe((0,.218,0),[(.073,0),(.079,.006),(.04,.023),(.0,.027)],'Steel',n=28,collider=True)
        g.sphere((0,.254,0),(.035,.032,.035),'Plastic',n=12,rings=6)
        g.static()


def appliances(row):
    g=Geo(row);v=row['variant']
    if v in [0,1]:oven(g,gas=v==0)
    elif v==2:
        w,h,d=.49,.29,.37
        shell(g,w,h,d,.018,'WhitePlastic',0,t=.018,back='Steel',legs=False,zones=False)
        g.box((0,.025,0),(.43,.012,.31),'Enamel')
        g.rod((-.05,.040,0),(-.05,.049,0),.117,'Glass',n=28)
        g.rod((-.05,.031,0),(-.05,.040,0),.017,'WhitePlastic',n=14)
        for k in range(3):
            a=k*2*pi/3;x=-.05+.082*cos(a);z=.082*sin(a)
            g.sphere((x,.035,z),(.010,.010,.010),'Rubber',n=10,rings=5)
        door(g,'MicrowaveDoor',-.035,.15,.38,.26,.195,'Plastic','left',glass=True,angle=105)
        g.box((.207,.15,.19),(.066,.27,.025),'WhitePlastic')
        knob(g,'Timer',.205,.107,.208,'channel',.021,'Plastic','Таймер')
        button(g,'Power',.205,.044,.209,'power',.039,.020)
        g.box((.205,.235,.211),(.045,.025,.003),'Emission',bevel=.002)
        for k in range(7):g.box((-.20+k*.027,.285,-.08),(.015,.001,.11),'Rubber',bevel=0)
        g.zone('MicrowaveCavity',(-.035,.1525,0),(.33,.185,.27));g.device(kind='device')
    elif v in [3,4]:kettle(g,v==3)
    elif v==5:
        w,h,d=.60,.85,.57
        shell(g,w,h,d,.04,'Enamel',0,t=.025,back='Steel',zones=False)
        g.box((0,h-.035,0),(w,.065,d),'WhitePlastic')
        washer_front(g)
        # Recessed drum and its rounded front rim.
        g.lathe((0,.40,.17),[(.20,0),(.20,.07),(.183,.086),(.17,.086),(.17,.005)],'Chrome',axis='z',n=36,closed=True)
        g.lathe((0,.40,-.19),[(.17,0),(.17,.32),(.162,.32),(.162,.01),(.0,.01)],'Steel',axis='z',n=28)
        g.part('WasherDoor',pivot=(-.21,.40,.31),kind='hinge',axis=(0,1,0),amount=-135,label='Люк стиральной машины')
        g.ring((0,.40,.29),.195,.025,'WhitePlastic',axis='z',n=36)
        g.lathe((0,.40,.29),[(.16,0),(.153,.025),(.115,.042),(.0,.047)],'Glass',axis='z',n=32,collider=True)
        handle(g,.166,.4,.31,.06,True,'Plastic');g.static()
        # The detergent tray exits through a true aperture in the control fascia.
        g.box((.125,.71,.27),(.32,.15,.025),'WhitePlastic')
        g.box((-.29,.71,.27),(.02,.15,.025),'WhitePlastic')
        for y in [.645,.80]:g.box((0,y,.27),(.58,.025,.025),'WhitePlastic')
        drawer(g,'DetergentTray',-.17,.735,.20,.10,.18,.291,'WhitePlastic')
        g.part('DetergentTray')
        for x in [-.203,-.137]:g.box((x,.730,.211),(.007,.065,.14),'WhitePlastic',bevel=.002)
        g.static()
        zone=next(z for z in g.zones if z['name']=='DetergentTray_Storage')
        zone['center'][0]=-.2315;zone['size'][0]=.043
        for index,x,width in [(1,-.17,.049),(2,-.1085,.043)]:g.zones.append(dict(name='DetergentCompartment%d'%index,center=[x,zone['center'][1],zone['center'][2]],size=[width,zone['size'][1],zone['size'][2]],part='DetergentTray'))
        knob(g,'Program',.106,.742,.291,'channel',.027,'Plastic','Программа')
        button(g,'Power',.235,.742,.291,'power',.025,.022)
        g.box((0,.075,.295),(.48,.035,.008),'WhitePlastic')
        g.zone('Drum',(0,.40,-.005),(.22,.22,.28));g.device(kind='device')
    elif v==6:
        oven(g,.48,.32,.35,table=True)
        # Resistance heaters behind the glass, safe and purely visual.
        for yy in [.09,.25]:g.path([(-.16,yy,-.10),(.16,yy,-.10),(.16,yy,.10),(-.16,yy,.10)],.004,'Steel',n=6)
    elif v==7:
        g.box((0,.03,0),(.60,.06,.46),'Enamel')
        # Stepped hood canopy, thin sheet-metal panels around a real filter opening.
        for x in [-.28,.28]:g.box((x,.12,0),(.035,.19,.42),'Enamel',rot=(0,0,(-1 if x<0 else 1)*25))
        g.box((0,.19,-.03),(.35,.04,.30),'Enamel')
        g.box((0,.42,-.09),(.22,.43,.20),'Enamel')
        for j in range(12):g.box((0,.008,-.17+j*.030),(.48,.008,.007),'Steel',bevel=.001)
        button(g,'Power',-.19,.07,.238,'power',.032,.018)
        button(g,'Speed',-.13,.07,.238,'channel',.032,.018,'Скорость вентилятора')
        button(g,'Light',-.07,.07,.238,'brightness',.032,.018,'Свет вытяжки')
        for x in [-.20,.20]:
            g.box((x,.001,.12),(.07,.007,.05),'Emission',bevel=.01)
            g.light((x,-.018,.12),intensity=.5,range=1.5)
        g.device(kind='light')
    elif v==8:
        shell(g,.60,.82,.58,.06,'WhitePlastic',0,t=.025,back='Steel',zones=False)
        # A fixed tub with two wire baskets and a drop-down door.
        for k in range(2):
            y=.26+k*.29
            g.part('Basket%d'%k,pivot=(0,y,.22),kind='slide',axis=(0,0,1),amount=.34,label='Корзина посудомойки')
            for z in [-.22,-.11,0,.11,.22]:g.rod((-.23,y,z),(.23,y,z),.003,'Steel',n=6,collider=True)
            for x in [-.23,-.115,0,.115,.23]:
                g.rod((x,y,-.22),(x,y,.22),.003,'Steel',n=6,collider=True)
                g.rod((x,y,-.22),(x,y+.065,-.22),.003,'Steel',n=6,collider=True)
            g.zone('Basket%d_Storage'%k,(0,y+.06,0),(.41,.10,.38));g.static()
        g.part('DishwasherDoor',pivot=(0,.095,.30),kind='hinge',axis=(1,0,0),amount=87,label='Дверца посудомойки')
        g.box((0,.44,.30),(.57,.69,.035),'Enamel')
        handle(g,0,.73,.324,.35)
        g.box((0,.62,.276),(.19,.14,.026),'WhitePlastic')
        g.static()
        button(g,'Power',-.22,.79,.307,'power',.032,.018)
        knob(g,'Program',.19,.79,.307,'channel',.018,'Plastic','Программа')
        g.device(kind='device')
    else:
        # Long-axis two-slot toaster, not a scaled kettle.
        g.box((-.153,.09,0),(.014,.15,.18),'Chrome',bevel=.007)
        for z,depth in [(-.0415,.097),(.0565,.067)]:g.box((.153,.09,z),(.014,.15,depth),'Chrome',bevel=.004)
        for y,height in [(.035,.040),(.150,.030)]:g.box((.153,y,.015),(.014,height,.016),'Chrome',bevel=.003)
        g.box((0,.09,-.083),(.292,.15,.014),'Chrome',bevel=.007)
        g.box((0,.11,.083),(.292,.11,.014),'Chrome',bevel=.007)
        g.box((0,.004,0),(.32,.008,.18),'Plastic',bevel=.003)
        for z in [-.078,0,.078]:g.box((0,.164,z),(.292,.008,.018),'Chrome',bevel=.003)
        for z in [-.045,.045]:
            g.box((0,.168,z),(.24,.002,.019),'Rubber',bevel=.007)
            for x in [-.128,.128]:g.box((x,.169,z),(.005,.004,.023),'Steel',bevel=.001)
        g.part('ToastLever',pivot=(.175,.115,.015),kind='slide',axis=(0,1,0),amount=-.055,label='Рычаг тостера')
        g.box((.18,.12,.015),(.028,.018,.035),'Plastic')
        g.rod((.153,.12,.015),(.18,.12,.015),.005,'Steel',n=10,collider=False);g.static()
        knob(g,'ToastSetting',.155,.054,.101,'channel',.017,'Plastic','Степень прожарки')
        button(g,'Power',-.13,.055,.101,'power',.024,.017)
        drawer(g,'CrumbTray',0,.027,.29,.025,.13,.094,'Steel')
        g.device(kind='device')
    return g


def refrigerator(row):
    g=Geo(row);v=row['variant']
    dims=[(.64,1.24,.62),(.59,1.45,.61),(.56,1.22,.60),(.62,1.86,.64),(.50,1.52,.57),(.63,1.72,.64),(.62,1.93,.64),(.47,.86,.49),(.90,1.82,.70),(.61,1.51,.63)]
    w,h,d=dims[v];mat='Enamel' if v not in [4,7,8,9] else ('Rust' if v==9 else 'WhitePlastic')
    shell(g,w,h,d,.075,mat,0,t=.045,back='WhitePlastic',zones=False)
    # A liner is visibly separate from the outer shell and includes radiused ledges.
    for x in [-w/2+.052,w/2-.052]:g.box((x,h*.52,-.016),(.012,h-.15,d-.095),'WhitePlastic',bevel=.009)
    g.box((0,.105,0),(w-.10,.025,d-.10),'WhitePlastic',bevel=.009)
    for yy in [.33,.58,.83,1.09,1.35,1.60]:
        if yy>h-.13:continue
        if v==9 and .5<yy<.9:continue
        cap=h-.355 if v not in [3,5,6,8] else h-.15
        if yy+.055>cap:continue
        next_boundary=cap
        if v in [3,5,6]:
            cut=h*(1-(.29 if v in [3,5] else .63))
            if yy<cut:next_boundary=min(next_boundary,cut-.025)
        zone_lo=yy+.014;zone_hi=min(yy+.19,next_boundary-.015)
        g.box((0,yy,-.045),(w-.13,.010,d-.24),'Glass',bevel=.002)
        g.box((0,yy,d/2-.165),(w-.13,.018,.015),'WhitePlastic',bevel=.003)
        if zone_hi>zone_lo+.025:
            if v==8:
                for side in [-1,1]:g.zone('ColdShelf_%d_%d'%(side,int(yy*100)),(side*w/4,(zone_lo+zone_hi)/2,-.045),(w/2-.13,zone_hi-zone_lo,d-.29))
            else:g.zone('ColdShelf_%d'%int(yy*100),(0,(zone_lo+zone_hi)/2,-.045),(w-.17,zone_hi-zone_lo,d-.29))
        for xx in [-w*.39,w*.39]:g.box((xx,yy-.01,-.045),(.023,.014,d-.25),'WhitePlastic',bevel=.002)
    g.box((0,h-.102,-d*.30),(.12,.04,.045),'Emission',bevel=.008)
    g.light((0,h-.14,-d*.20),intensity=.7,range=1.4)
    def fridge_door(name,cx,cy,dw,dh,side='left'):
        px=cx-dw/2 if side=='left' else cx+dw/2
        g.part(name,pivot=(px,cy-dh/2,d/2+.010),kind='hinge',axis=(0,1,0),amount=-112 if side=='left' else 112,label='Дверца холодильника')
        # The gasket sits on the rear of the outer skin, not buried in it.
        # Its swept hinge-side corner stays outside the horizontal case lips.
        g.box((cx,cy,d/2+.035),(dw,dh,.055),mat,bevel=.019 if v in [0,2] else .008)
        gw,gh=dw-.04,dh-.04;seal=.014
        for xx in [cx-gw/2+seal/2,cx+gw/2-seal/2]:g.box((xx,cy,d/2+.002),(seal,gh,.010),'Rubber',bevel=.003)
        for yy in [cy-gh/2+seal/2,cy+gh/2-seal/2]:g.box((cx,yy,d/2+.002),(gw-2*seal,seal,.010),'Rubber',bevel=.003)
        g.box((cx,cy,d/2-.0145),(dw-.105,dh-.105,.045),'WhitePlastic',bevel=.015)
        hx=cx+dw*.32 if side=='left' else cx-dw*.32
        if v==2:
            handle(g,cx,cy+dh*.27,d/2+.055,min(.22,dw*.45),False,'Chrome')
            for xx in [cx-dw*.23,cx+dw*.23]:g.box((xx,cy+dh*.27,d/2+.066),(.026,.046,.008),'Chrome',bevel=.005)
            g.box((cx,cy+dh*.39,d/2+.064),(dw*.72,.008,.006),'Chrome',bevel=.003)
        else:handle(g,hx,cy+dh*.14,d/2+.055,min(.29,dh*.36),True,'Chrome' if v==0 else 'WhitePlastic')
        for k in range(3 if dh>.9 else 1):
            yy=cy-dh*.34+k*dh*.28
            if name=='MainDoor' and yy+.08>h-.39:continue
            g.box((cx,yy,d/2-.072),(dw-.15,.015,.085),'WhitePlastic')
            g.box((cx,yy+.04,d/2-.112),(dw-.15,.080,.012),'WhitePlastic')
            for xx in [cx-dw/2+.075,cx+dw/2-.075]:g.box((xx,yy+.04,d/2-.072),(.014,.080,.08),'WhitePlastic')
            g.zone(name+'_Pocket%d'%k,(cx,yy+.044,d/2-.07),(dw-.19,.059,.054))
        if v in [0,1,2] and name=='MainDoor':
            g.box((cx,cy+dh*.31,d/2+.0635),(.15,.035,.004),'Chrome',bevel=.002,collider=False)
            g.label((cx,cy+dh*.31,d/2+.0656),(.14,.025),index=v)
        # Hinge sleeves stay coaxial with the established saved mechanism axis.
        for yy in [cy-dh*.34,cy+dh*.34]:
            g.lathe((px,yy-.025,d/2+.010),[(.012,0),(.012,.050),(.006,.050),(.006,0)],'Steel',axis='y',n=16,closed=True)
        g.static()
    if v==8:
        fridge_door('LeftDoor',-w/4,h*.515,w/2-.008,h-.105,'left')
        fridge_door('RightDoor',w/4,h*.515,w/2-.008,h-.105,'right')
        g.box((0,h*.51,0),(.028,h-.13,d-.11),'WhitePlastic')
    elif v in [3,5,6]:
        upper=.29 if v in [3,5] else .63
        cut=h*(1-upper)
        g.box((0,cut,0),(w-.10,.03,d-.09),'WhitePlastic')
        fridge_door('LowerDoor',0,(.08+cut)/2,w-.016,cut-.087)
        fridge_door('UpperDoor',0,(cut+h)/2,w-.016,h-cut-.013)
    else:
        fridge_door('MainDoor',0,(h+.085)/2,w-.016,h-.095)
        # Small internal freezer door, with an independent top hinge.
        fw=w-.15;fh=min(.25,h*.22);fy=h-fh*.60-.08
        g.box((0,fy-fh/2,-.05),(fw,.018,d-.26),'WhitePlastic')
        g.part('FreezerFlap',pivot=(0,fy+fh/2,d/2-.10),kind='hinge',axis=(1,0,0),amount=-92,label='Морозильная камера')
        g.box((0,fy,d/2-.10),(fw,fh,.018),'WhitePlastic')
        handle(g,0,fy-fh*.32,d/2-.086,.12,False,'WhitePlastic');g.static()
    # A compressor recess and thin rear condenser tubes establish readable construction.
    g.box((0,.13,-d/2-.009),(w*.51,.12,.09),'Steel',bevel=.025)
    for x in [-w*.19,w*.19]:g.box((x,.078,-d/2-.010),(.035,.018,.13),'Steel',bevel=.003)
    for x in [-w*.30,w*.30]:g.box((x,h*.52,-d/2-.018),(.020,h*.72,.012),'Steel',bevel=.002)
    for k in range(9):
        y=.31+k*(h-.43)/9
        g.rod((-w*.33,y,-d/2-.02),(w*.33,y,-d/2-.02),.004,'Steel',n=6)
    for x in [-w*.33,w*.33]:g.rod((x,.31,-d/2-.02),(x,h-.13,-d/2-.02),.005,'Steel',n=8)
    if v==2:
        g.box((0,.048,d/2+.020),(w-.07,.055,.028),'Steel',bevel=.006)
        for k in range(8):g.box(((k-3.5)*.047,.050,d/2+.037),(.028,.007,.003),'Rubber',bevel=.001)
    if v==9:
        g.damage('LooseLowerPanel')
        g.box((0,.14,d/2+.062),(w*.78,.09,.018),'Rust',rot=(0,0,-3.5));g.static()
        g.path([(-.25,h-.05,-.12),(-.28,h-.02,-.11),(-.21,h-.06,-.08)],.003,'Rust',n=6)
    g.device(kind='fridge')
    return g


def speaker_grille(g,cx,cy,z,w,h,round=False):
    g.box((cx,cy,z),(w,h,.008),'Rubber',bevel=.012)
    if round:
        r=min(w,h)*.43
        g.ring((cx,cy,z+.005),r,.006,'Chrome',axis='z',n=24)
        # Concentric pressed surround, recessed cone and domed dust cap.
        g.lathe((cx,cy,z+.006),[(r*.96,0),(r*.87,.006),(r*.70,.003),(r*.23,-.008),(r*.20,-.011),(r*.69,-.001),(r*.96,.002)],'Rubber',axis='z',n=24,closed=True)
        g.sphere((cx,cy,z+.009),(r*.55,r*.55,r*.13),'Rubber',n=16,rings=8)
    else:
        for k in range(8):g.box((cx,cy-h*.40+k*h*.115,z+.007),(w*.88,.003,.005),'Steel',bevel=.001)


def tuning_scale(g,cx,cy,z,w,h):
    g.box((cx,cy,z),(w,h,.008),'Paper',bevel=.002)
    g.box((cx,cy,z+.006),(w*.98,h*.95,.004),'Glass',bevel=.002)
    for k in range(17):
        xx=cx-w*.44+k*w*.055
        g.box((xx,cy,z+.008),(.0015,h*(.64 if k%4==0 else .32),.001),'DarkWood',bevel=0)
    g.part('FrequencyNeedle',pivot=(cx,cy,z+.010),kind='visual',axis=(1,0,0),amount=w*.8,label='Стрелка частоты')
    g.box((cx,cy,z+.010),(.002,h*.82,.002),'RedFabric',bevel=0);g.static()


def face_panel(g,xlo,xhi,ylo,yhi,z,holes,mat='Plastic',thickness=.008):
    """One manifold sheet around actual apertures; no coincident cell caps."""
    xs=sorted(set([xlo,xhi]+[q for h in holes for q in h[:2]]))
    ys=sorted(set([ylo,yhi]+[q for h in holes for q in h[2:]]))
    cells=set()
    for i,(a,b) in enumerate(zip(xs,xs[1:])):
        for j,(c,d) in enumerate(zip(ys,ys[1:])):
            x,y=(a+b)/2,(c+d)/2
            if any(h[0]<x<h[1] and h[2]<y<h[3] for h in holes):continue
            if b-a>.0001 and d-c>.0001:
                cells.add((i,j));g.colliders.append(dict(part=g.current,center=[x,y,z],size=[b-a,d-c,thickness],rotation=[0,0,0]))
    vertices=[];faces=[];indices={}
    def v(i,j,k):
        key=(i,j,k)
        if key not in indices:indices[key]=len(vertices);vertices.append((xs[i],ys[j],z+(-.5 if k==0 else .5)*thickness))
        return indices[key]
    for i,j in sorted(cells):
        faces.extend([(v(i,j,1),v(i+1,j,1),v(i+1,j+1,1),v(i,j+1,1)),(v(i,j+1,0),v(i+1,j+1,0),v(i+1,j,0),v(i,j,0))])
        for neighbour,a,b in [((i-1,j),(i,j),(i,j+1)),((i+1,j),(i+1,j+1),(i+1,j)),((i,j-1),(i+1,j),(i,j)),((i,j+1),(i,j+1),(i+1,j+1))]:
            if neighbour not in cells:faces.append((v(*a,0),v(*b,0),v(*b,1),v(*a,1)))
    g.add(vertices,faces,mat,False)


def top_panel(g,xlo,xhi,zlo,zhi,y,holes,mat='Plastic',thickness=.008):
    start_mesh=len(g.meshes);start_col=len(g.colliders)
    face_panel(g,xlo,xhi,zlo,zhi,y,holes,mat,thickness)
    for mesh in g.meshes[start_mesh:]:mesh['v']=[(x,z,yy) for x,yy,z in mesh['v']]
    for col in g.colliders[start_col:]:col['center']=[col['center'][0],col['center'][2],col['center'][1]];col['size']=[col['size'][0],col['size'][2],col['size'][1]]


def cassette_front_bay(g,x,y,width,height,front,depth=.060):
    """Recessed transport with a separate cassette, hubs and pressure-pad bridge."""
    rear=front-depth
    g.box((x,y,rear),(width,height,.005),'Plastic',bevel=.002)
    for xx in [x-width/2,x+width/2]:g.box((xx,y,(front+rear)/2),(.005,height,depth),'Plastic',bevel=.001)
    for yy in [y-height/2,y+height/2]:g.box((x,yy,(front+rear)/2),(width,.005,depth),'Plastic',bevel=.001)
    tape_w=width-.012;tape_h=height-.014;tz=rear+.012
    g.box((x,y,tz),(tape_w,tape_h,.012),'DarkWood',bevel=.004)
    g.box((x,y+.004,tz+.007),(tape_w*.84,tape_h*.58,.002),'Paper',bevel=.002)
    for xx in [x-tape_w*.25,x+tape_w*.25]:
        r=min(.013,tape_h*.24)
        g.rod((xx,y+.004,tz+.008),(xx,y+.004,tz+.012),r,'Rubber',n=16)
        g.ring((xx,y+.004,tz+.013),r*.75,.0013,'Chrome',axis='z',n=16)
        g.rod((xx,y+.004,tz+.012),(xx,y+.004,tz+.017),r*.22,'Chrome',n=10)
    g.box((x,y-tape_h*.31,tz+.009),(tape_w*.56,.005,.005),'Steel',bevel=.001)


def radio(row):
    g=Geo(row);v=row['variant']
    # Deliberately different architectures: table valve sets, handbag, deck,
    # boombox, hi-fi stack, long-antenna transistor, DIY car set and compact speaker.
    dims=[(.52,.31,.19),(.59,.39,.25),(.66,.36,.24),(.32,.22,.11),(.39,.15,.23),(.64,.25,.17),(.76,.57,.30),(.37,.27,.12),(.34,.19,.22),(.20,.22,.13)]
    w,h,d=dims[v];m='DarkWood' if v in [0,1,2,8] else ('WhitePlastic' if v==3 else 'Plastic')
    feet(g,w,d,.022,'Rubber',.012)
    if v==6:
        for x in [-.14,.14]:g.box((x,(h+.02)/2,0),(.012,h-.02,d),m)
        for y in [.025,h-.006]:g.box((0,y,0),(.28,.012,d),m)
        g.box((0,(h+.02)/2,-d/2),(.28,h-.02,.012),m)
    elif v in [3,4,5]:
        for x in [-w/2+.004,w/2-.004]:g.box((x,(h+.02)/2,0),(.008,h-.02,d),m,bevel=.003)
        g.box((0,.026,0),(w-.016,.008,d-.016),m,bevel=.002)
        if v==4:
            top_panel(g,-w/2+.008,w/2-.008,-d/2,d/2,h-.004,[(-.017,.147,-.064,.070)],m)
        else:g.box((0,h-.004,0),(w-.016,.008,d-.016),m,bevel=.002)
        if v==3:
            face_panel(g,-w/2+.008,w/2-.008,.03,h-.008,-d/2+.004,[(-.095,.095,.045,.135)],m)
        else:g.box((0,(h+.02)/2,-d/2+.004),(w-.016,h-.02,.008),m,bevel=.002)
        if v==5:
            holes=[(x-.051,x+.051,.081,.169) for x in [-.0675,.0675]]
            face_panel(g,-w/2+.008,w/2-.008,.03,h-.008,d/2-.004,holes,m)
        else:g.box((0,(h+.02)/2,d/2-.004),(w-.016,h-.02,.008),m,bevel=.002)
    else:g.box((0,(h+.02)/2,0),(w,h-.02,d),m,bevel=.018 if v in [1,3,9] else .007)
    f=d/2+.006
    # Back ventilation slots are physical grooves, with only low-cost strip silhouettes.
    for k in range(8):g.box(((k-3.5)*w*.09,.185 if v==3 else h*.57,-d/2-.001),(w*.032,.036 if v==3 else h*.42,.002),'Rubber',bevel=.002)
    if v in [0,1,2]:
        grille_w=w*(.44 if v==2 else (.55 if v==0 else .84))
        cx=-w*.18 if v==2 else (-w*.19 if v==0 else 0);cy=h*.50 if v==2 else (h*.57 if v==0 else h*.62)
        speaker_grille(g,cx,cy,f,grille_w,h*(.64 if v==2 else .57),round=v==2)
        if v==2:
            for k in range(5):g.box((cx+(k-2)*w*.075,cy,f+.018),(.007,h*.59,.012),'Wood',bevel=.002)
            for x in [-w*.45,w*.45]:g.box((x,h*.52,f+.013),(.022,h*.82,.018),'Wood')
        if v==1:
            tuning_scale(g,0,h*.21,f+.004,w*.65,.055)
            controls=[(-w*.40,h*.17),(w*.40,h*.17)]
        else:
            tuning_scale(g,w*.24,h*.70,f+.004,w*.32,.075)
            controls=[(w*.20,h*.28),(w*.39,h*.28)]
        for x in [-w*.43,w*.43]:
            g.box((x,h*.52,f+.008),(.006,h*.79,.007),'Brass',bevel=.001)
        knob(g,'Power',controls[0][0],controls[0][1],f+.012,'power',.022,'Plastic')
        knob(g,'TuningDial',controls[1][0],controls[1][1],f+.012,'tune',.026,'Plastic')
        button(g,'Channel',-.05,.049,f+.010,'channel',.035,.014,'Переключить станцию')
        knob(g,'Volume',.045,.049,f+.010,'volume',.012,'Plastic')
        for k in range(5):
            x=-w*.38+k*w*.083;y=.035 if v==1 else .067
            g.box((x,y,f+.006),(.032,.015,.008),'Rubber',bevel=.003)
            g.part('BandKey%d'%k,pivot=(x,y,f+.022),kind='channel',axis=(0,0,1),amount=.004,label='Клавиша радиоканала')
            g.box((x,y,f+.022),(.029,.012,.022),'WhitePlastic',bevel=.003);g.static()
        g.box((0,h*.91,f+.009),(.12,.022,.003),'Brass',bevel=.003)
    elif v==3:
        speaker_grille(g,-.055,.125,f,.17,.15,round=True)
        tuning_scale(g,.10,.157,f+.004,.085,.07)
        knob(g,'Power',.102,.071,f+.008,'power',.015)
        knob(g,'TuningDial',.151,.100,f+.008,'tune',.014)
        knob(g,'Volume',-.12,.040,f+.008,'volume',.012)
        button(g,'Channel',-.073,.040,f+.008,'channel',.027,.014,'Станция')
        # Rigid carry handle pivots at both top corners.
        g.part('CarryHandle',pivot=(0,h-.025,0),kind='hinge',axis=(1,0,0),amount=72,label='Ручка переноски')
        # The arms straddle the outer case, so folding cannot cut through its face.
        g.path([(-.17,h-.025,0),(-.17,h+.055,0),(.17,h+.055,0),(.17,h-.025,0)],.009,'Plastic',n=8);g.static()
        for side in [-1,1]:g.rod((side*.156,h-.025,0),(side*.174,h-.025,0),.011,'Steel',n=12)
        # The pocket is open behind the sliding cover and has visible AA cells.
        g.box((0,.09,-.003),(.19,.09,.006),'Plastic',bevel=.002)
        for x in [-.092,.092]:g.box((x,.09,-.027),(.006,.09,.045),'Plastic',bevel=.001)
        for y in [.048,.132]:g.box((0,y,-.027),(.178,.006,.045),'Plastic',bevel=.001)
        for x in [-.050,.050]:
            for y in [.067,.112]:
                g.rod((x-.023,y,-.030),(x+.023,y,-.030),.008,'Paper',n=12)
                g.rod((x+.023,y,-.030),(x+.026,y,-.030),.005,'Brass',n=10)
                for xx in [x-.027,x-.030]:g.ring((xx,y,-.030),.007,.0012,'Steel',axis='x',n=10)
        g.part('BatteryCover',pivot=(0,.09,-d/2),kind='slide',axis=(0,-1,0),amount=.035,label='Батарейный отсек')
        g.box((0,.09,-d/2-.004),(.20,.10,.008),'WhitePlastic',bevel=.004);g.static()
    elif v==4:
        # Desktop cassette machine: top cassette hatch, planar speaker and recorder keys.
        g.box((-w*.27,h+.002,.005),(.115,.004,.16),'Rubber',bevel=.007)
        for k in range(8):g.box((-w*.27,h+.006,-.064+k*.020),(.10,.003,.003),'Steel',bevel=.001)
        # A cassette rests in a recessed top-loading transport below the aperture.
        for x in [-.014,.144]:g.box((x,.108,.003),(.006,.080,.130),'Plastic',bevel=.001)
        for z in [-.061,.067]:g.box((.065,.108,z),(.158,.080,.006),'Plastic',bevel=.001)
        g.box((.065,.066,.003),(.158,.006,.128),'Plastic',bevel=.002)
        g.box((.065,.076,.005),(.112,.014,.071),'DarkWood',bevel=.004)
        g.box((.065,.084,.005),(.096,.002,.052),'Paper',bevel=.002)
        for x in [.035,.095]:
            g.rod((x,.085,.005),(x,.088,.005),.014,'Rubber',n=18)
            g.ring((x,.089,.005),.011,.0013,'Chrome',axis='y',n=18)
            g.rod((x,.089,.005),(x,.092,.005),.003,'Chrome',n=10)
        g.part('CassetteDoor',pivot=(.055,h,-.075),kind='hinge',axis=(1,0,0),amount=-64,label='Кассетный отсек')
        top_panel(g,-.025,.155,-.069,.081,h+.005,[(0,.13,-.020,.044)],'Plastic',thickness=.014)
        g.box((.065,h+.014,.012),(.13,.006,.064),'Glass',bevel=.004);g.static()
        tuning_scale(g,0,h*.60,f+.004,w*.65,.035)
        for k in range(5):button(g,'DeckKey%d'%k,-.12+k*.057,.035,f+.005,'power' if k==0 else 'channel',.042,.018,'Питание' if k==0 else 'Режим кассеты')
        knob(g,'TuningDial',.152,.095,f+.004,'tune',.016)
        knob(g,'Volume',-.158,.095,f+.004,'volume',.014)
        button(g,'Channel',0,.096,f+.004,'channel',.042,.016,'Станция')
        # Primary stable id remains Power even when keyboard includes functional keys.
        button(g,'Power',-.16,.035,f+.005,'power',.019,.018)
    elif v==5:
        for x in [-w*.34,w*.34]:speaker_grille(g,x,h*.52,f,w*.27,h*.79,round=True)
        for k,x in enumerate([-.0675,.0675]):
            cassette_front_bay(g,x,.125,.096,.080,.079,.065)
            g.part('CassetteDoor%d'%k,pivot=(x,.07,f+.020),kind='hinge',axis=(1,0,0),amount=65,label='Кассетный отсек')
            face_panel(g,x-.0575,x+.0575,.075,.175,f+.005,[(x-.048,x+.048,.101,.153)],'Plastic',thickness=.013)
            g.box((x,.127,f+.014),(.096,.052,.004),'Glass');g.static()
        tuning_scale(g,0,h*.86,f+.003,w*.38,.030)
        knob(g,'TuningDial',.14,.205,f+.008,'tune',.016)
        knob(g,'Volume',-.14,.205,f+.008,'volume',.015)
        button(g,'Power',-.04,.042,f+.008,'power',.032,.014)
        button(g,'Channel',.04,.042,f+.008,'channel',.032,.014,'Станция')
        g.part('CarryHandle',pivot=(0,h+.025,0),kind='hinge',axis=(1,0,0),amount=74,label='Ручка переноски')
        g.path([(-.20,h+.025,0),(-.20,h+.100,0),(.20,h+.100,0),(.20,h+.025,0)],.011,'Plastic',n=8);g.static()
        for side in [-1,1]:
            # The static bearings sit beside the arms, not below their rotating
            # rounded ends. A metal pin physically bridges each bearing joint.
            g.box((side*.22,h+.010,0),(.018,.030,.034),'Plastic',bevel=.003)
            g.rod((side*.19,h+.025,0),(side*.24,h+.025,0),.009,'Steel',n=12,collider=False)
    elif v==6:
        # Centre unit flanked by independent box speakers; a visibly stacked transport.
        # Both cassette transports and the CD tray have apertures through the fascia.
        holes=[(-.105,.105,.338,.372),(-.105,-.015,.057,.143),(.015,.105,.057,.143)]
        face_panel(g,-.145,.145,.03,.54,f+.004,holes,'Steel',thickness=.010)
        for x in [-.26,.26]:
            g.box((x,h*.5,0),(.23,h,.30),'DarkWood')
            speaker_grille(g,x,h*.38,f+.004,.19,.29,round=True)
            g.ring((x,h*.80,f+.014),.040,.008,'Chrome',axis='z',n=20)
        for yy in [.15,.29,.43]:g.box((0,yy,f+.011),(.28,.011,.008),'Chrome',bevel=.002)
        tuning_scale(g,0,.49,f+.015,.23,.052)
        drawer(g,'CDTray',0,.355,.20,.022,.20,f+.010,'Plastic')
        for k,x in enumerate([-.06,.06]):
            cassette_front_bay(g,x,.10,.084,.075,f-.008,.055)
            g.part('CassetteDoor%d'%k,pivot=(x-.05,.045,f+.01),kind='hinge',axis=(1,0,0),amount=28,label='Кассетная дека')
            face_panel(g,x-.05,x+.05,.05,.15,f+.02,[(x-.0375,x+.0375,.09,.13)],'Plastic',thickness=.012)
            g.box((x,.11,f+.028),(.075,.040,.003),'Glass');g.static()
        knob(g,'Volume',.09,.245,f+.016,'volume',.024)
        knob(g,'TuningDial',-.09,.245,f+.016,'tune',.020)
        button(g,'Power',-.09,.185,f+.015,'power',.035,.018)
        button(g,'Channel',.0,.185,f+.015,'channel',.035,.018,'Станция')
    elif v==7:
        speaker_grille(g,-.072,.13,f,.19,.20,round=True)
        tuning_scale(g,.105,.19,f+.004,.095,.085)
        knob(g,'TuningDial',.11,.093,f+.008,'tune',.026)
        knob(g,'Volume',.15,.040,f+.008,'volume',.012)
        button(g,'Power',-.135,.035,f+.008,'power',.023,.015)
        button(g,'Channel',.07,.040,f+.008,'channel',.025,.015,'Станция')
        for k in range(4):g.rod((.12,h-.01+k*.14,-.026),(.12,h+.14+k*.14,-.026),.0045-k*.0008,'Chrome',n=10)
        g.ring((.12,h-.01,-.026),.012,.003,'Chrome',axis='y',n=12)
        g.path([(-.13,h-.015,0),(-.13,h+.04,0),(.06,h+.04,0)],.008,'Plastic',n=8)
    elif v==8:
        # DIY plywood box includes exposed screws and a steel automotive insert.
        g.box((0,.10,f+.010),(.23,.072,.02),'Steel')
        for xx in [-.145,.145]:
            for yy in [.035,.158]:screw(g,xx,yy,f+.010,'Steel')
        tuning_scale(g,0,.112,f+.023,.13,.028)
        knob(g,'Power',-.093,.11,f+.026,'power',.016)
        knob(g,'TuningDial',.094,.11,f+.026,'tune',.016)
        button(g,'Channel',-.025,.072,f+.025,'channel',.030,.012,'Станция')
        knob(g,'Volume',.035,.072,f+.025,'volume',.011)
        g.box((0,.04,f+.012),(.20,.020,.008),'Rubber',bevel=.002)
        g.rod((-.145,.18,-.05),(-.19,.38,-.07),.0025,'Steel',n=8)
    else:
        speaker_grille(g,0,.12,f,.165,.16,round=True)
        g.ring((0,.13,f+.012),.065,.008,'Rubber',axis='z',n=24)
        g.box((0,.045,f+.014),(.11,.020,.005),'Screen',bevel=.003)
        button(g,'Power',-.055,.21,f+.010,'power',.024,.012)
        button(g,'Channel',.0,.21,f+.010,'channel',.024,.012,'Станция')
        knob(g,'Volume',.057,.21,f+.010,'volume',.010)
        knob(g,'TuningDial',.075,.065,f+.010,'tune',.010)
        g.part('FrequencyNeedle',pivot=(0,.045,f+.020),kind='visual',axis=(1,0,0),amount=.08,label='Индикатор частоты')
        g.box((0,.045,f+.020),(.002,.013,.001),'Emission',bevel=0);g.static()
        g.path([(-.07,h-.015,0),(-.10,h+.07,0),(-.02,h+.09,0),(.02,h+.04,0)],.007,'Rubber',n=8)
    # All ten expose the same indicator and speaker reference for the controller.
    g.box((w*.43,h*.87,f+.013),(.008,.008,.004),'Emission',bevel=.003)
    if v in [0,1,2,3,7]:g.label((0,h*.91,f+.016),(w*.23,.016),index=6)
    if v in [0,2]:g.label((0,.026,f+.016),(.12,.009),index=5)
    g.part('SpeakerCone',pivot=(-w*.22,h*.53,f+.010),kind='visual',axis=(0,0,1),amount=.0005,label='Диффузор')
    g.rod((-w*.22,h*.53,f+.010),(-w*.22,h*.53,f+.012),min(w*.09,.026),'Rubber',n=16);g.static()
    g.device(kind='radio',dial='TuningDial',needle='FrequencyNeedle',speaker='SpeakerCone')
    return g


def television(g,v):
    dims=[(.61,.56,.42),(.69,.57,.46),(.36,.35,.30),(.64,.53,.49),(.94,.59,.075),(.42,.43,.42)]
    w,h,d=dims[v];wood=v in [0,1];m='DarkWood' if wood else ('WhitePlastic' if v==5 else 'Plastic')
    if v==4:
        g.box((0,.34,0),(w,.53,d),m,bevel=.018)
        g.box((0,.35,d/2+.004),(w-.06,.46,.006),'Screen',bevel=.005)
        g.rod((0,.04,0),(0,.10,0),.025,'Steel',n=16)
        g.box((0,.026,0),(.38,.025,.23),'Plastic',bevel=.020)
        g.box((0,.065,0),(.035,.07,.035),'Plastic')
        for x in [-.12,.12]:
            for z in [-.07,.07]:g.box((x,.007,z),(.04,.014,.030),'Rubber',bevel=.003)
        for xx in [-.24,.24]:g.box((xx,.35,-.043),(.017,.10,.008),'Steel')
        button(g,'Power',.40,.105,.042,'power',.021,.012)
        button(g,'Channel',.36,.105,.042,'channel',.021,.012,'Канал')
        knob(g,'Volume',.32,.105,.042,'volume',.008)
    else:
        if v==5:
            g.lathe((0,0,0),[(.12,0),(.15,.008),(.135,.025),(.07,.030),(.055,.045),(.045,.055),(0,.055),(0,0)],'WhitePlastic',axis='y',n=32,collider=True)
        else:feet(g,w,d,.05,'Plastic',.022)
        g.box((0,(h+.055)/2,0),(w,h-.055,d),m,bevel=.02 if v in [2,3,5] else .008)
        screen_w=w*(.71 if v in [0,1,2] else .82);screen_h=h*.68
        sx=-w*.07 if v in [0,1,2] else 0;sy=h*.55
        g.box((sx,sy,d/2+.006),(screen_w+.025,screen_h+.025,.022),'Rubber',bevel=.03)
        # CRT surface slightly bulges instead of a flat rectangular decal.
        g.cushion((sx,sy,d/2+.026),(screen_w,screen_h,.028),'Screen',sag=-.008,seed=1,segments=8)
        bezel='WhitePlastic' if v==5 else 'Plastic'
        lip=.010 if v==2 else .023
        for x in [sx-screen_w/2-lip/2,sx+screen_w/2+lip/2]:g.box((x,sy,d/2+.031),(lip,screen_h+.048,.022),bezel,bevel=.004)
        g.box((sx,sy+screen_h/2+.012,d/2+.031),(screen_w,.024,.022),bezel,bevel=.006)
        g.box((sx,sy-screen_h/2-.012,d/2+.015),(screen_w,.024,.008),bezel,bevel=.004)
        if v in [0,1,2]:
            cx=w*.40
            knob(g,'Power',cx,h*.74,d/2+.017,'power',.025)
            knob(g,'Channel',cx,h*.50,d/2+.017,'channel',.027)
            knob(g,'Volume',cx,h*.28,d/2+.017,'volume',.017)
            for k in range(9):g.box((cx,h*.16+k*.008,d/2+.010),(w*.13,.003,.005),'Rubber',bevel=.001)
        else:
            button(g,'Power',-w*.34,.083,d/2+.016,'power',.030,.018)
            button(g,'Channel',w*.28,.083,d/2+.016,'channel',.022,.012,'Канал')
            knob(g,'Volume',w*.35,.083,d/2+.016,'volume',.012)
        for k in range(10):g.box(((k-4.5)*w*.065,h*.62,-d/2-.002),(w*.024,h*.39,.003),'Rubber',bevel=.002)
        if v in [0,2]:
            for s in [-1,1]:g.rod((0,h,-d*.14),(s*w*.40,h+.34,-d*.13),.0035,'Chrome',n=8)
        g.box((w*.39,.09,d/2+.018),(.008,.007,.004),'Emission',bevel=.002)
    g.device(kind='television')


def electronics(row):
    g=Geo(row);v=row['variant']
    if v<6:television(g,v)
    elif v==6:
        w,h,d=.21,.44,.43
        # Separate sheet-metal panels leave an honest serviceable interior.
        g.box((w/2,h/2,0),(.005,h,d),'Steel',bevel=.002)
        # The removable left panel covers a service opening, not another solid wall.
        for y in [.012,h-.012]:g.box((-w/2,y,0),(.005,.024,d),'Steel',bevel=.002)
        for z in [-d/2+.012,d/2-.012]:g.box((-w/2,h/2,z),(.005,h-.048,.024),'Steel',bevel=.002)
        for y in [.006,h-.006]:g.box((0,y,0),(w-.01,.012,d),'Steel',bevel=.002)
        g.box((0,h/2,-d/2),(w-.01,h,.007),'Steel',bevel=.002)
        g.box((-.085,.22,0),(.009,.32,.29),'GreenFabric',bevel=.001)
        g.box((-.065,.26,-.025),(.035,.065,.065),'Steel',bevel=.002)
        for k in range(5):g.box((-.057,.26,-.052+k*.013),(.017,.061,.004),'Chrome',bevel=.001)
        g.box((0,.365,-.13),(.17,.10,.14),'Steel',bevel=.002)
        g.path([(-.03,.34,-.07),(.02,.30,.01),(.025,.17,.07),(-.05,.12,.08)],.004,'Rubber',n=6)
        holes=[(-.083,.083,.35-k*.045-.017,.35-k*.045+.017) for k in range(3)]
        face_panel(g,-(w-.006)/2,(w-.006)/2,.003,h-.003,d/2+.009,holes,'WhitePlastic',thickness=.017)
        for k in range(3):
            y=.35-k*.045
            for x in [-.079,.079]:g.box((x,y-.008,.125),(.006,.020,.18),'Steel',bevel=.001)
            for x in [-.055,.055]:g.box((x,y-.007,.125),(.018,.006,.17),'Steel',bevel=.001)
            g.box((0,y,.032),(.16,.034,.008),'Steel',bevel=.001)
        for k in range(3):
            g.part('DriveTray%d'%k,pivot=(0,.35-k*.045,d/2+.02),kind='slide',axis=(0,0,1),amount=.15,label='Дисковод')
            g.box((0,.35-k*.045,d/2+.021),(.16,.032,.014),'WhitePlastic')
            g.box((0,.35-k*.045,d/2-.05),(.14,.008,.15),'Plastic');g.static()
        for k in range(6):g.box((0,.09+k*.014,d/2+.021),(.14,.004,.003),'Rubber',bevel=.001)
        button(g,'Power',-.055,.23,d/2+.023,'power',.027,.027)
        button(g,'Reset',.045,.23,d/2+.023,'channel',.014,.014,'Режим')
        g.box((.072,.23,d/2+.024),(.006,.007,.003),'Emission',bevel=.002)
        g.part('SidePanel',pivot=(-w/2,h*.5,0),kind='slide',axis=(0,0,-1),amount=.25,label='Боковая крышка')
        g.box((-w/2-.006,h*.5,0),(.006,h-.025,d-.025),'Steel',bevel=.003);g.static()
        for yy in [.12,.28]:
            g.ring((0,yy,-d/2-.004),.055,.004,'Steel',axis='z',n=18)
            for k in range(5):g.box((0,yy-.037+k*.018,-d/2-.006),(.085,.003,.002),'Rubber',bevel=.001)
        g.device(kind='device')
    elif v==7:
        g.box((0,.045,0),(.24,.075,.21),'WhitePlastic',bevel=.025)
        for x in [-.080,.080]:
            for z in [-.065,.065]:g.box((x,.004,z),(.025,.008,.025),'Rubber',bevel=.002)
        for x in [-.105,.105]:g.box((x,.09,-.035),(.035,.020,.027),'Plastic',bevel=.003)
        # The receiver has an actual hand-grip and two ear cups.
        g.part('Receiver',pivot=(0,.10,-.035),kind='lift',axis=(0,1,0),amount=.18,label='Телефонная трубка')
        g.path([(-.11,.12,-.035),(-.075,.145,-.035),(.075,.145,-.035),(.11,.12,-.035)],.019,'WhitePlastic',n=12)
        for x in [-.105,.105]:g.sphere((x,.12,-.035),(.065,.045,.066),'WhitePlastic',n=16,rings=8)
        g.static()
        for x in [-.055,0,.055]:
            for k in range(4):
                z=.012+k*.023
                g.part('Dial_%d_%d'%(int(x*100),k),pivot=(x,.086,z),kind='channel',axis=(0,1,0),amount=.004,label='Набрать номер')
                g.box((x,.090,z),(.025,.008,.018),'Plastic',bevel=.003);g.static()
        g.part('Power',pivot=(.089,.086,.076),kind='power',axis=(0,1,0),amount=.004,label='Связь')
        g.box((.089,.090,.076),(.020,.008,.018),'Plastic',bevel=.003);g.static()
        g.part('Volume',pivot=(-.09,.09,.076),kind='volume',axis=(0,1,0),amount=28,label='Громкость')
        g.rod((-.09,.083,.076),(-.09,.101,.076),.010,'Plastic',n=16,collider=True)
        g.box((-.09,.102,.080),(.002,.002,.007),'WhitePlastic',bevel=0);g.static()
        # The runtime cord is driven by these two physically seated grommets.
        g.rod((-.115,.052,-.040),(-.123,.052,-.040),.006,'Rubber',n=12,collider=False)
        g.part('Receiver')
        g.rod((-.130,.118,-.035),(-.138,.118,-.035),.006,'Rubber',n=12,collider=False)
        g.static()
        phone_cord_source(g)
        g.device(kind='device')
    elif v==8:
        for x in [-.2115,.2115]:g.box((x,.032,0),(.007,.06,.26),'Steel',bevel=.003)
        for y in [.005,.059]:g.box((0,y,0),(.416,.006,.26),'Steel',bevel=.003)
        g.box((0,.032,-.127),(.416,.048,.006),'Steel',bevel=.002)
        for x,w in [(-.19,.05),(.16,.11)]:g.box((x,.034,.137),(w,.055,.014),'Plastic',bevel=.003)
        for y,h in [(.012,.014),(.057,.010)]:g.box((0,y,.137),(.426,h,.014),'Plastic',bevel=.003)
        drawer(g,'DiscTray',-.03,.038,.24,.020,.19,.147,'Plastic')
        button(g,'Power',-.18,.035,.151,'power',.020,.020)
        button(g,'Eject',.15,.035,.151,'channel',.024,.014,'Режим')
        g.box((.155,.049,.151),(.080,.010,.003),'Screen',bevel=.002)
        for x in [-.17,.17]:
            for z in [-.09,.09]:g.rod((x,0,z),(x,.009,z),.014,'Rubber',n=10)
        for x in [-.045,0,.045]:hollow_tube(g,[(x,.027,-.127),(x,.027,-.146)],.006,.001,'Chrome',n=12)
        g.device(kind='device')
    else:
        g.box((0,.075,0),(.43,.13,.30),'Steel',bevel=.006)
        g.box((0,.075,.158),(.43,.13,.015),'DarkWood',bevel=.005)
        knob(g,'Power',-.18,.075,.17,'power',.020,'Chrome')
        knob(g,'Volume',.16,.075,.17,'volume',.031,'Chrome')
        knob(g,'Mode',.085,.075,.17,'channel',.018,'Chrome','Источник')
        for k,x in enumerate([-.085,-.015]):
            g.box((x,.082,.171),(.055,.035,.004),'Paper',bevel=.003)
            g.rod((x-.015,.070,.177),(x+.013,.091,.177),.001,'Steel',n=6)
        for k in range(12):g.box((-.16+k*.029,.143,-.025),(.010,.003,.15),'Rubber',bevel=.001)
        g.box((-.18,.118,.175),(.006,.006,.003),'Emission',bevel=.002)
        feet(g,.43,.3,.015,'Rubber',.013);g.device(kind='device')
    return g


def carpets(row):
    g=Geo(row);v=row['variant'];mat='Carpet%d'%v
    dims=[(1.75,1.2),(2.45,1.65),(2.2,1.48),(1.10,1.10),(1.8,1.30),(2.1,1.45),(1.55,1.10),(1.75,1.2),(.75,.45),(.70,.43)]
    w,d=dims[v]
    if v==0:
        # Wall carpet hangs on a pole; its woven panel has a slight sag.
        g.cloth((0,.65,0),(w,.012,1.22),mat,fold=.018,nx=22,nz=16,rot=(90,0,0))
        g.rod((-w/2-.03,1.29,-.025),(w/2+.03,1.29,-.025),.012,'DarkWood',n=12)
        for k in range(12):
            x=-w*.46+k*w*.92/11
            g.ring((x,1.27,-.025),.014,.002,'Brass',axis='x',n=10)
        # Fringe hangs downward rather than standing as solid triangle patches.
        for k in range(32):
            x=-w*.48+k*w*.96/31
            g.path([(x,.045,.008),(x+.003,.018,.012),(x-.003,.003,.016)],.0015,'Fabric',n=5)
    elif v==3:
        # Circular topology carries the texture, edging and a gently lifted rim.
        g.lathe((0,.008,0),[(0,0),(.24,.001),(.47,.003),(.545,.009),(.55,.003),(.48,-.003),(.24,-.005),(0,-.005)],mat,axis='y',n=56,collider=True)
        g.ring((0,.016,0),.538,.004,mat,axis='y',n=56)
    elif v==6:
        burned_rug(g,w,d,mat)
    elif v==7:
        torn_rug(g,w,d,mat)
        for k in range(20):
            x=-w*.47+k*w*.94/19
            g.rod((x,.014,-d/2),(x+.004,.010,-d/2-.035),.0015,'Fabric',n=5)
    else:
        g.cloth((0,.022,0),(w,.014,d),mat,fold=.015 if v in [1,2,5] else .007,nx=22,nz=16)
        # Bound edges have a separate low-poly cord, including believable corner rolls.
        g.path([(-w/2,.015,-d/2),(w/2,.015,-d/2),(w/2,.015,d/2),(-w/2,.015,d/2),(-w/2,.015,-d/2)],.0035,mat,n=6)
        if v!=9:
            for k in range(24):
                x=-w*.47+k*w*.94/23
                for s in [-1,1]:g.rod((x,.012,s*d/2),(x+.002,.007,s*(d/2+.035)),.0013,'Fabric',n=5)
        else:
            g.box((0,.004,0),(w+.016,.008,d+.016),'Rubber',bevel=.004)
    if v!=0:
        # A floor rug has a real lowest contact point, irrespective of folds.
        floor=min(p[1] for mesh in g.meshes for p in mesh['v'])
        for mesh in g.meshes:mesh['v']=[(x,y-floor,z) for x,y,z in mesh['v']]
        for collider in g.colliders:
            if 'center' in collider:collider['center'][1]-=floor
            else:
                collider['a'][1]-=floor;collider['b'][1]-=floor
    return g


def bulb(g,x,y,z,scale=1,direction=1):
    start=len(g.meshes)
    g.lathe((x,y-.025*scale,z),[(.018*scale,0),(.018*scale,.025*scale),(.011*scale,.025*scale),(.011*scale,.006*scale),(.006*scale,.006*scale),(.006*scale,0)],'Brass',axis='y',n=18,closed=True)
    g.lathe((x,y,z),[(.010*scale,0),(.014*scale,.020*scale),(.026*scale,.046*scale),(.028*scale,.066*scale),(.017*scale,.083*scale),(0,.09*scale)],'Emission',axis='y',n=20)
    for k in range(3):g.ring((x,y+.006*k*scale,z),.012*scale,.0015*scale,'Brass',axis='y',n=12)
    if direction<0:
        for mesh in g.meshes[start:]:mesh['v']=[(xx,2*y-yy,2*z-zz) for xx,yy,zz in mesh['v']]


def lampshade(g,c,r0,r1,height,mat='Fabric',n=32,collider=False):
    x,y,z=c
    g.lathe((x,y,z),[(r0,0),(r1,height),(max(.005,r1-.004),height),(max(.005,r0-.004),0)],mat,axis='y',n=n,closed=True,collider=collider)
    g.ring((x,y,z),r0,.003,'Steel',axis='y',n=n)
    g.ring((x,y+height,z),r1,.003,'Steel',axis='y',n=n)
    spider_y=y+.020
    g.ring((x,spider_y,z),.020,.003,'Steel',axis='y',n=18)
    for a in [0,pi*2/3,pi*4/3]:
        g.rod((x+.011*cos(a),spider_y,z+.011*sin(a)),(x+.023*cos(a),spider_y,z+.023*sin(a)),.002,'Steel',n=6)
        g.rod((x+.023*cos(a),spider_y,z+.023*sin(a)),(x+r1*.9*cos(a),y+height,z+r1*.9*sin(a)),.0025,'Steel',n=6)
    if mat in ['Fabric','GreenFabric','RedFabric']:
        g.ring((x,y+.004,z),r0+.0005,.0025,mat,axis='y',n=n)
        g.ring((x,y+height-.004,z),r1+.0005,.0025,mat,axis='y',n=n)


def lighting(row):
    g=Geo(row);v=row['variant']
    if v==0:
        # Five curved Soviet chandelier arms and upward glass tulips.
        g.lathe((0,.62,0),[(.07,0),(.06,.035),(.025,.06),(.025,.08)],'Brass',n=24)
        g.rod((0,.28,0),(0,.63,0),.014,'Brass',n=12)
        g.sphere((0,.26,0),(.10,.09,.10),'Brass',n=16,rings=8)
        for k in range(5):
            a=k*2*pi/5;x=.26*cos(a);z=.26*sin(a)
            g.path([(0,.28,0),(.10*cos(a),.16,.10*sin(a)),(x,.17,z),(x,.25,z)],.009,'Brass',n=10)
            lampshade(g,(x,.20,z),.075,.048,.12,'Glass',n=24)
            bulb(g,x,.255,z,.6);g.light((x,.29,z),intensity=.55,range=4)
        button(g,'Power',0,.31,.059,'power',.034,.018)
        knob(g,'Brightness',.03,.23,.06,'brightness',.012,'Brass')
    elif v==1:
        g.box((0,.70,0),(.22,.035,.075),'Chrome')
        for x in [-.06,.06]:g.rod((x,.20,0),(x,.70,0),.003,'Steel',n=6)
        g.box((0,.18,0),(.82,.045,.08),'Steel',bevel=.013)
        g.box((0,.155,0),(.78,.007,.063),'Emission',bevel=.008)
        for x in [-.27,0,.27]:g.light((x,.13,0),intensity=.8,range=4)
        button(g,'Power',.36,.19,.05,'power',.023,.016)
        knob(g,'Brightness',.32,.19,.05,'brightness',.012)
    elif v==2:
        g.lathe((0,.12,0),[(.17,0),(.17,.025),(.14,.045),(.02,.048),(.02,.025),(.15,.018)],'Steel',n=36)
        g.lathe((0,0,0),[(.02,0),(.12,.025),(.165,.075),(.16,.12),(.15,.12),(.153,.076),(.11,.037),(.02,.012)],'Glass',n=36,closed=True)
        bulb(g,0,.105,0,.6,direction=-1);g.light((0,.025,0),intensity=1.2,range=4)
        for a in [0,pi*2/3,pi*4/3]:g.rod((.16*cos(a),.10,.16*sin(a)),(.18*cos(a),.10,.18*sin(a)),.004,'Steel',n=6)
        button(g,'Power',0,.128,.16,'power',.028,.014)
    elif v==3:
        g.lathe((0,0,0),[(.09,0),(.105,.015),(.09,.035),(.026,.04),(.02,.023)],'Brass',n=28)
        g.rod((0,.03,0),(0,.245,0),.012,'Brass',n=12)
        lampshade(g,(0,.23,0),.16,.075,.19,'GreenFabric',n=32)
        bulb(g,0,.25,0,.7);g.light((0,.26,0),intensity=.7,range=3)
        knob(g,'Power',.06,.039,.035,'power',.013,'Brass')
        knob(g,'Brightness',-.05,.039,.035,'brightness',.011,'Brass')
        g.path([(0,.02,-.09),(.06,.01,-.20),(.16,.01,-.24)],.003,'Rubber',n=6)
    elif v==4:
        g.lathe((0,0,0),[(.19,0),(.20,.025),(.15,.048),(.02,.05)],'Steel',n=32)
        g.rod((0,.035,0),(0,1.195,0),.013,'Brass',n=14)
        lampshade(g,(0,1.10,0),.28,.17,.35,'Fabric',n=40)
        bulb(g,0,1.20,0,1);g.light((0,1.20,0),intensity=1,range=4)
        knob(g,'Power',0,.86,.022,'power',.014)
        knob(g,'Brightness',.025,.81,.022,'brightness',.012)
        g.path([(.02,.018,-.15),(.28,.009,-.17),(.38,.009,-.30)],.0035,'Rubber',n=6)
    elif v==5:
        g.box((0,.17,0),(.10,.25,.023),'Brass',bevel=.02)
        g.path([(0,.10,.02),(0,.07,.13),(0,.14,.19)],.010,'Brass',n=10)
        lampshade(g,(0,.12,.19),.09,.055,.15,'Glass',n=28)
        bulb(g,0,.14,.19,.55);g.light((0,.19,.19),intensity=.65,range=3)
        button(g,'Power',0,.10,.040,'power',.029,.019)
        g.path([(.025,.14,.15),(.04,.04,.15),(.04,.01,.15)],.0015,'Brass',n=5)
    elif v==6:
        g.lathe((0,0,0),[(.07,0),(.08,.018),(.06,.04),(.015,.05)],'WhitePlastic',n=24)
        g.sphere((0,.12,0),(.15,.18,.15),'Emission',n=24,rings=12)
        g.lathe((0,.025,0),[(.051,0),(.055,.009),(.052,.026),(.045,.030),(.042,.025),(.045,.009)],'WhitePlastic',axis='y',n=28,closed=True)
        g.light((0,.12,0),intensity=.3,range=2)
        button(g,'Power',0,.03,.075,'power',.023,.014)
        knob(g,'Brightness',.035,.026,.061,'brightness',.010,'WhitePlastic')
    elif v==7:
        # Spring clamp, gooseneck and adjustable lamp head.
        for y,rot in [(.015,-7),(.055,7)]:g.box((0,y,0),(.045,.012,.11),'Steel',rot=(rot,0,0))
        g.box((0,.003,.020),(.041,.006,.055),'Rubber',bevel=.002)
        g.rod((-.032,.036,-.025),(.032,.036,-.025),.005,'Steel',n=8)
        g.ring((0,.038,-.022),.013,.003,'Steel',axis='x',n=12)
        pts=[(0,.06,-.02),(.01,.15,-.04),(.04,.25,-.015),(.075,.33,.03)]
        g.path(pts,.009,'Steel',n=10)
        spring=[]
        for k in range(177):
            t=k/176;a=t*22*2*pi
            spring.append((.01+t*.056+.011*cos(a),.16+t*.16,-.032+t*.055+.011*sin(a)))
        g.path(spring,.0015,'Steel',n=5,collider=False)
        g.part('AdjustableHead',pivot=(.075,.33,.03),kind='hinge',axis=(1,0,0),amount=55,label='Наклон плафона')
        lampshade(g,(.075,.30,.045),.06,.025,.09,'Enamel',n=24,collider=True)
        g.path([(.075,.33,.03),(.075,.372,.015),(.075,.375,.045)],.004,'Steel',n=8,collider=False)
        bulb(g,.075,.367,.045,.4,direction=-1);g.light((.075,.31,.045),intensity=.5,range=2)
        g.static();button(g,'Power',0,.073,.015,'power',.022,.014)
    elif v==8:
        g.rod((0,.24,0),(0,.65,0),.006,'Steel',n=10)
        g.lathe((0,.10,0),[(.21,0),(.20,.035),(.13,.12),(.035,.17),(.027,.17),(.12,.112),(.19,.03),(.20,0)],'Enamel',n=40,closed=True)
        g.lathe((0,.265,0),[(.035,0),(.035,.05),(.02,.07),(.015,.06)],'Steel',n=20)
        bulb(g,0,.235,0,.8,direction=-1);g.light((0,.12,0),intensity=1.2,range=5)
        g.rod((0,.250,0),(0,.276,0),.009,'Steel',n=12,collider=False)
        g.lathe((0,.645,0),[(.020,0),(.050,.018),(.050,.035),(.020,.040),(.015,.035),(.015,.005)],'Steel',axis='y',n=24,closed=True)
        g.ring((0,.102,0),.20,.004,'Steel',axis='y',n=32)
        for k in range(8):
            a=k*2*pi/8
            g.path([(.20*cos(a),.10,.20*sin(a)),(.15*cos(a),.20,.15*sin(a)),(.03*cos(a),.27,.03*sin(a))],.003,'Steel',n=6)
        button(g,'Power',0,.29,.041,'power',.024,.018)
    else:
        # An actual missing shade sector and bent cage; damaged geometry is authored.
        g.box((0,.22,0),(.13,.31,.025),'Rust')
        g.path([(0,.17,.02),(.02,.11,.12),(.04,.17,.20)],.012,'Rust',n=8)
        bulb(g,.04,.185,.20,.7)
        for k in range(7):
            a=(k+1)*2*pi/9
            g.path([(.04+.075*cos(a),.14,.20+.075*sin(a)),(.04+.09*cos(a),.27,.20+.09*sin(a)),(.04,.33,.20)],.004,'Rust',n=6)
        g.ring((.04,.14,.20),.075,.004,'Rust',axis='y',n=24)
        g.light((.04,.22,.20),intensity=.45,range=3)
        button(g,'Power',0,.075,.034,'power',.024,.018)
        g.path([(-.04,.09,.019),(-.05,.02,.018),(-.08,0,.04)],.004,'Rubber',n=6)
    g.device(kind='light')
    return g


def hallway(row):
    g=Geo(row);v=row['variant']
    if v==0:
        # Two genuinely opening tilt-out shoe baskets instead of horizontal drawers.
        shell(g,.74,.85,.25,.08,'Chipboard',0,zones=False)
        for k in range(2):
            y=.12+k*.35
            g.part('ShoeFlap%d'%k,pivot=(0,y,.135),kind='hinge',axis=(1,0,0),amount=38,label='Обувной отсек')
            g.box((0,y+.16,.135),(.69,.31,.022),'Wood')
            g.box((0,y+.035,.015),(.63,.018,.20),'Plywood')
            for x in [-.31,.31]:g.box((x,y+.10,.015),(.014,.15,.20),'Plywood')
            handle(g,0,y+.26,.151,.17)
            g.zone('ShoeTray%d'%k,(0,y+.10,.018),(.58,.11,.17));g.static()
    elif v==1:
        for k in range(4):g.box(((k-1.5)*.25,.34,0),(.13,.68,.025),'Wood')
        for yy in [.10,.55]:g.box((0,yy,-.022),(.94,.045,.022),'DarkWood')
        hooks(g,.80,.49,.028,5,'Steel')
        g.box((0,.70,.08),(1.02,.035,.24),'Wood')
    elif v==2:
        g.rod((0,.07,0),(0,1.67,0),.023,'DarkWood',n=16)
        for k in range(4):
            a=k*pi/2
            g.path([(0,.17,0),(.17*cos(a),.06,.17*sin(a)),(.32*cos(a),.035,.32*sin(a))],.020,'DarkWood',n=10)
            g.box((.32*cos(a),.009,.32*sin(a)),(.055,.018,.055),'Rubber',bevel=.004)
        for k in range(6):
            a=k*pi/3
            g.path([(0,1.46,0),(.15*cos(a),1.62,.15*sin(a)),(.13*cos(a),1.73,.13*sin(a))],.011,'DarkWood',n=10)
            g.path([(.018*cos(a),1.24,.018*sin(a)),(.12*cos(a),1.24,.12*sin(a)),(.13*cos(a),1.29,.13*sin(a))],.009,'DarkWood',n=8)
        g.sphere((0,1.70,0),(.066,.066,.066),'DarkWood',n=12,rings=6)
    elif v==3:
        g.box((0,.62,0),(.59,1.20,.025),'DarkWood')
        g.box((0,.65,.017),(.52,1.07,.008),'Mirror',bevel=.006)
        for x in [-.29,.29]:g.box((x,.62,.025),(.025,1.23,.025),'Brass')
        g.box((0,.045,.10),(.67,.04,.27),'DarkWood')
        for x in [-.23,.23]:g.path([(x,.03,-.012),(x,-.06,-.012),(x,.03,.20)],.009,'Steel',n=8)
    elif v==4:
        shell(g,.76,1.06,.36,.10,'WhiteLaminate',3)
        double_doors(g,.72,.58,.91,.19,'WhiteLaminate')
        g.box((0,1.08,0),(.80,.035,.40),'DarkWood')
        for y in [.31,.55,.79]:g.rod((-.29,y,.06),(.29,y,.06),.005,'Steel',n=8)
    elif v==5:
        shell(g,1.04,.48,.40,.06,'Wood',0,zones=False)
        g.part('BenchLid',pivot=(0,.46,-.20),kind='hinge',axis=(1,0,0),amount=-82,label='Сиденье скамьи')
        g.box((0,.475,0),(1.08,.035,.44),'Wood')
        g.cushion((0,.52,0),(1.01,.075,.39),'GreenFabric',sag=.018,segments=12)
        g.static()
        # Front remains a grille so wet footwear can ventilate.
        for k in range(8):g.box((-.43+k*.123,.24,.21),(.068,.27,.018),'Wood')
        g.zone('ShoeStorage',(0,.26,0),(.93,.27,.30))
    elif v==6:
        w,d=.90,.28
        for x in [-.38,.38]:
            for z in [-.10,.10]:g.box((x,.40,z),(.035,.80,.035),'DarkWood')
        g.box((0,.82,0),(.94,.04,.32),'DarkWood')
        drawer(g,'ConsoleDrawer',0,.72,.69,.13,.24,.15,'DarkWood')
        g.box((0,.13,0),(.80,.025,.23),'DarkWood')
        g.zone('LowerShelf',(0,.26,0),(.68,.22,.14))
    elif v==7:
        shell(g,.67,1.91,.43,.10,'Chipboard',2)
        g.zones[0]['center'][1]=.50;g.zones[0]['size'][1]=.32
        door(g,'WardrobeDoor',-.13,1.02,.36,1.71,.227,'Chipboard',glass=False)
        # Narrow exposed service strip with mirror and hooks is a separate feature.
        g.box((.205,1.135,.225),(.22,1.51,.025),'Chipboard')
        g.box((.075,1.135,.225),(.04,1.51,.025),'DarkWood')
        g.box((.205,1.26,.242),(.20,.94,.008),'Mirror')
        hooks(g,.16,.67,.245,2,'Chrome',xoffset=.205)
        drawer(g,'ShoeDrawer',0,.22,.61,.20,.37,.227,'Chipboard')
        g.box((0,1.95,0),(.72,.045,.48),'DarkWood')
    elif v==8:
        shell(g,.37,.47,.085,.015,'PaintedWood',0,t=.014,legs=False,zones=False)
        door(g,'KeyBoxDoor',0,.24,.34,.43,.052,'PaintedWood',glass=False,angle=110)
        hooks(g,.25,.33,-.022,5,'Brass')
        g.box((0,.095,-.015),(.30,.018,.045),'PaintedWood')
        g.zone('KeysShelf',(0,.15,.006),(.27,.08,.025))
    else:
        g.lathe((0,0,0),[(.15,0),(.16,.025),(.11,.04),(.025,.045)],'Steel',n=28)
        g.box((0,.86,0),(.042,1.66,.042),'Steel')
        for yy,n,r in [(1.06,3,.18),(1.38,4,.21),(1.66,4,.22)]:
            for k in range(n):
                a=k*2*pi/n+yy
                g.path([(0,yy,0),(r*cos(a),yy+.02,r*sin(a)),(r*cos(a),yy+.09,r*sin(a))],.008,'Steel',n=8)
                g.sphere((r*cos(a),yy+.09,r*sin(a)),(.025,.025,.025),'Wood',n=10,rings=5)
        g.box((0,.38,0),(.31,.022,.27),'Steel')
    return g


def bathroom(row):
    g=Geo(row);v=row['variant']
    if v==0:
        shell(g,.61,.79,.43,.10,'WhiteLaminate',1)
        g.zones[-1]['center'][1]=.575;g.zones[-1]['size'][1]=.20
        for zone in g.zones:zone['center'][2]=.05;zone['size'][2]=.24
        double_doors(g,.575,.43,.64,.227,'WhiteLaminate')
        # Basin built from a closed radial cross-section, with visible central drain.
        g.lathe((0,.70,0),[(.022,.012),(.10,.012),(.20,.065),(.29,.145),(.29,.165),(.265,.17),(.235,.13),(.095,.04),(.022,.028)],'Ceramic',axis='y',n=40,closed=True,collider=True)
        g.ring((0,.729,0),.021,.003,'Chrome',axis='y',n=14)
        waste_trap(g,.730,-.15)
        g.path([(0,.82,-.17),(0,1.00,-.17),(0,1.025,-.11),(0,.98,-.07)],.011,'Chrome',n=12)
        knob(g,'Tap',.05,.86,-.15,'power',.014,'Chrome','Кран')
    elif v==1:
        shell(g,.62,.72,.16,.015,'WhitePlastic',2,t=.017,legs=False)
        door(g,'MirrorDoor',0,.365,.59,.68,.093,'WhitePlastic')
        # Mirror belongs to the opening door, rather than the fixed cabinet body.
        g.part('MirrorDoor',pivot=(-.295,.025,.093),kind='hinge',axis=(0,1,0),amount=-110,label='Зеркальная дверца')
        g.box((0,.365,.108),(.54,.63,.006),'Mirror',bevel=.005);g.static()
        g.box((0,.755,0),(.65,.035,.18),'WhitePlastic')
        g.box((0,.747,.10),(.42,.008,.008),'Emission',bevel=.003)
        g.light((0,.72,.14),intensity=.4,range=1.6)
        button(g,'Power',.27,.055,.109,'power',.021,.015);g.device(kind='light')
    elif v==2:
        shell(g,.40,1.02,.30,.07,'WhitePlastic',3,t=.016)
        door(g,'PlasticDoor',0,.55,.36,.88,.163,'WhitePlastic')
        for yy in [.21,.37,.53,.69,.85]:
            g.part('PlasticDoor',pivot=(-.18,.11,.163),kind='hinge',axis=(0,1,0),amount=-105,label='Дверца')
            g.box((0,yy,.177),(.25,.005,.003),'WhitePlastic',bevel=.001);g.static()
    elif v==3:
        shell(g,.41,.53,.17,.015,'Enamel',2,t=.014,back='Steel',legs=False)
        door(g,'MedicineDoor',0,.27,.38,.49,.097,'Enamel')
        g.part('MedicineDoor',pivot=(-.19,.025,.097),kind='hinge',axis=(0,1,0),amount=-105,label='Аптечный шкаф')
        g.box((0,.30,.112),(.14,.034,.003),'RedFabric',bevel=.003)
        g.box((0,.30,.113),(.034,.14,.003),'RedFabric',bevel=.003)
        for x in [-.15,.15]:screw(g,x,.48,.112,'Rust')
        g.static()
    elif v==4:
        # Perforated laundry basket: real rim and ribs, open interior, not a solid box.
        w,h,d=.39,.51,.32
        g.box((0,.018,0),(w,.028,d),'WhitePlastic',bevel=.012)
        for z in [-d/2,d/2]:
            for k in range(9):g.box(((-4+k)*.043,h/2,z),(.014,h,.012),'WhitePlastic',bevel=.004)
            for yy in [.12,.25,.38,.49]:g.box((0,yy,z),(w,.013,.013),'WhitePlastic',bevel=.003)
        for x in [-w/2,w/2]:
            for k in range(6):g.box((x,h/2,(-2.5+k)*.059),(.014,h,.014),'WhitePlastic',bevel=.004)
            for yy in [.12,.25,.38,.49]:g.box((x,yy,0),(.013,.013,d),'WhitePlastic',bevel=.003)
        hinge_z=-(d+.025)/2-.010
        g.part('BasketLid',pivot=(0,h+.004,hinge_z),kind='hinge',axis=(1,0,0),amount=-100,label='Крышка корзины')
        g.box((0,h+.015,0),(w+.025,.022,d+.025),'WhitePlastic',bevel=.012)
        handle(g,0,h+.029,.03,.11,False,'WhitePlastic')
        for x in [-w*.30,w*.30]:g.box((x,h+.012,hinge_z+.009),(.018,.008,.018),'Steel',bevel=.001)
        g.static()
        for x in [-w*.30,w*.30]:
            g.box((x,h-.008,-.171),(.025,.024,.037),'Steel',bevel=.002)
            g.rod((x-.019,h+.004,hinge_z),(x+.019,h+.004,hinge_z),.004,'Steel',n=10)
        g.zone('Laundry',(0,.2625,0),(w-.045,h-.08,d-.045))
    elif v==5:
        w,h,d=.54,.70,.18
        for x in [-.24,.24]:g.rod((x,.025,-.075),(x,h,-.075),.008,'Chrome',n=10)
        for k in range(3):
            y=.05+k*.30
            g.box((0,y,0),(w,.009,d),'Glass',bevel=.003)
            g.rod((-.26,y+.04,.083),(.26,y+.04,.083),.005,'Chrome',n=8)
            for x in [-.25,.25]:g.rod((x,y,.08),(x,y+.04,.08),.005,'Chrome',n=8)
            g.zone('BathroomShelf%d'%k,(0,y+.12,0),(.46,.18,.13))
    elif v==6:
        shell(g,.39,1.91,.34,.085,'WhiteLaminate',5)
        door(g,'UpperDoor',0,1.37,.355,.99,.184,'WhiteLaminate')
        door(g,'LowerDoor',0,.47,.355,.67,.184,'WhiteLaminate')
        g.box((0,.825,0),(.345,.012,.31),'WhiteLaminate',bevel=.002)
        # The additional divider splits the generic carcass's third bay.
        # Retain its lower usable volume rather than a zone through the board.
        for zone in g.zones:
            if zone['name']=='Compartment_02':zone['center'][1]=.755;zone['size'][1]=.10
        g.zone('SmallOpenSlot',(0,.850,.01),(.31,.025,.26))
        g.box((0,1.93,0),(.42,.03,.37),'WhiteLaminate')
    elif v==7:
        # Washing machine support cabinet has an unobstructed full-size lower opening.
        w,h,d=.73,1.86,.66
        for x in [-w/2+.014,w/2-.014]:g.box((x,h/2,0),(.028,h,d),'WhiteLaminate')
        g.box((0,.945,0),(w-.045,.032,d),'WhiteLaminate')
        g.box((0,1.85,0),(w,.028,d),'WhiteLaminate')
        g.box((0,1.40,-d/2+.007),(w-.04,.88,.014),'Plywood')
        g.box((0,1.40,0),(w-.04,.018,d-.03),'WhiteLaminate')
        double_doors(g,w-.04,1.405,.84,d/2+.014,'WhiteLaminate')
        g.zone('UpperStorage',(0,1.17,0),(.64,.35,.57))
        g.zone('MachineOpening',(0,.46,0),(.65,.88,.60))
    elif v==8:
        shell(g,.29,1.27,.31,.07,'WhitePlastic',3,t=.017)
        for z in g.zones:z['center'][2]=-.02;z['size'][2]=.20
        door(g,'ChemicalsDoor',0,.67,.253,1.14,.168,'WhitePlastic')
        # Low retaining lips prevent bottle placement from looking physically unsupported.
        for y in [.37,.67,.97]:g.box((0,y+.035,.124),(.235,.07,.012),'WhitePlastic')
    else:
        shell(g,.78,1.76,.47,.10,'PaintedWood',3,t=.024,back='Plywood')
        g.zones[0]['center'][1]=.385;g.zones[0]['size'][1]=.23
        # Asymmetric plank door and utility drawer leave a recognisable shed-cupboard face.
        door(g,'UtilityDoor',-.075,1.00,.59,1.42,.252,'PaintedWood',angle=113)
        g.part('UtilityDoor',pivot=(-.37,.29,.252),kind='hinge',axis=(0,1,0),amount=-113,label='Дверца')
        for xx in [-.22,-.075,.07]:g.box((xx,1.00,.269),(.004,1.36,.004),'DarkWood',bevel=0)
        for yy in [.44,1.57]:g.box((-.075,yy,.274),(.52,.055,.016),'Rust',bevel=.002)
        g.static()
        drawer(g,'UtilityDrawer',0,.19,.72,.15,.40,.252,'PaintedWood',missing=True)
        for yy in [.46,1.50]:screw(g,.30,yy,.267,'Rust')
    return g


def build(row):
    category=(int(row['id'])-1)//10+1
    functions={11:shelves,12:kitchen,13:appliances,14:refrigerator,15:radio,16:electronics,17:carpets,18:lighting,19:hallway,20:bathroom}
    if category not in functions:raise ValueError('Not owned by models_11_20: %s'%row['id'])
    return functions[category](row)
