"""Authored construction recipes for apartment furniture 001--100.

All distances are metres.  +Z is the accessible/front side and +Y is up.
The cabinet recipes deliberately describe boards rather than solid proxy boxes:
doors and drawers can open onto usable cavities, and their hardware shares the
corresponding movable pivot.  Recipes share joinery, never an entire silhouette.
"""
from math import sin, cos, pi, atan2, exp
from geom import Geo


def _profiled_timber_leg(g,a,b,r,mat='Wood',n=10):
    """Continuous tapered timber, with a real neck and retained end shoulders.

    The contained collision core is narrower than every cross-section. Keeping
    the original end sections preserves the existing mounting and floor planes.
    """
    from mathutils import Vector
    av,bv=Vector(a),Vector(b);axis=(bv-av).normalized()
    u=axis.cross(Vector((0,1,0)))
    if u.length<.01:u=axis.cross(Vector((1,0,0)))
    u.normalize();v=axis.cross(u)
    profile=[(0,1),(.055,1),(.13,.91),(.72,.72),(.88,.79),(.96,.94),(1,1)]
    vertices=[]
    for t,scale in profile:
        center=av+(bv-av)*t
        for k in range(n):
            angle=k*2*pi/n
            vertices.append(tuple(center+r*scale*(u*cos(angle)+v*sin(angle))))
    faces=[tuple(range(n-1,-1,-1)),tuple((len(profile)-1)*n+k for k in range(n))]
    for j in range(len(profile)-1):
        for k in range(n):faces.append((j*n+k,j*n+(k+1)%n,(j+1)*n+(k+1)%n,(j+1)*n+k))
    # Reuse the normal primitive collision policy, then replace its visible skin.
    g.rod(a,b,r*.70,mat,n=n)
    g.meshes.pop()
    g.add(vertices,faces,mat,True)


def _fastener(g,p,normal=(0,0,1),r=.004,mat='Steel'):
    from mathutils import Vector
    p=Vector(p);n=Vector(normal).normalized()
    g.rod(tuple(p-n*.001),tuple(p+n*.0025),r,mat,n=8,collider=False)
    tangent=n.cross(Vector((0,1,0)))
    if tangent.length<.1:tangent=n.cross(Vector((1,0,0)))
    tangent.normalize()
    g.rod(tuple(p+n*.0028-tangent*r*.62),tuple(p+n*.0028+tangent*r*.62),
          r*.13,'Rubber',n=4,collider=False)


def _fabric_button(g,x,y,skin_index,mat='Fabric'):
    """Mount a tuft on the actual upholstery surface, not an assumed front Z."""
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    mesh=g.meshes[skin_index]
    bvh=BVHTree.FromPolygons([Vector(p)for p in mesh['v']],mesh['f'])
    z=max(p[2]for p in mesh['v'])+.04
    hit,normal,_,_=bvh.ray_cast(Vector((x,y,z)),Vector((0,0,-1)),.5)
    if hit is not None:
        # A 10 mm thick button embeds 3 mm in the textile rather than hovering.
        g.sphere((x,y,hit.z+.002),(.021,.021,.010),mat,n=8,rings=4)


def _gusset(g,x,y,z,sx,sz,arm=.072,height=.065,mat='Wood'):
    """A triangular prism mounting block joins a leg to its two adjoining rails."""
    points=[(x,z),(x-sx*arm,z),(x,z-sz*arm)]
    vertices=[(xx,y+dy,zz)for dy in(-height*.5,height*.5)for xx,zz in points]
    g.add(vertices,[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],mat,False)


def _curve_shell(g,c,size,mat='WhitePlastic',nx=12,nz=12):
    """Closed moulded seat shell: an ergonomic basin, rolled lip and real wall."""
    x0,y0,z0=c;w,thick,d=size;vertices=[];faces=[]
    for layer in(0,1):
        for j in range(nz+1):
            zz=j/nz*2-1
            for i in range(nx+1):
                xx=i/nx*2-1
                bowl=.018*xx**4+.016*zz**4
                front_lip=.006*max(0,zz)**6
                vertices.append((x0+xx*w*.5,y0+bowl+front_lip-layer*thick,z0+zz*d*.5))
    count=(nx+1)*(nz+1)
    for layer in(0,1):
        for j in range(nz):
            for i in range(nx):
                k=layer*count+j*(nx+1)+i;face=(k,k+1,k+nx+2,k+nx+1)
                faces.append(face if not layer else tuple(reversed(face)))
    rim=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,nz+1)]+[nz*(nx+1)+i for i in range(nx-1,-1,-1)]+[j*(nx+1)for j in range(nz-1,0,-1)]
    for a,b in zip(rim,rim[1:]+rim[:1]):faces.append((a,b,b+count,a+count))
    g.add(vertices,faces,mat,True)
    g.colliders.append(dict(part=g.current,center=[x0,y0-thick*.65,z0],
                            size=[w*.91,thick*.60,d*.90],rotation=[0,0,0]))


def _oilcloth_skirt(g,w,d):
    """Four thin, closed hanging ribbons; no 150 mm solid 'cloth' side slabs."""
    for axis,extent,length in [('x',w*.5+.009,d+.025),('z',d*.5+.0125,w+.018)]:
        for side in(-1,1):
            vertices=[];faces=[];along=16;down=6
            for layer in(0,1):
                for j in range(down+1):
                    t=j/down
                    for i in range(along+1):
                        a=(i/along-.5)*length
                        bulge=.006*sin(a*24+side)*t*t
                        transverse=side*(extent+bulge+(layer-.5)*.002)
                        y=.780-.12*t+.004*sin(a*31)*t*t
                        vertices.append((transverse,y,a)if axis=='x'else(a,y,transverse))
            count=(along+1)*(down+1)
            for layer in(0,1):
                for j in range(down):
                    for i in range(along):
                        k=layer*count+j*(along+1)+i;f=(k,k+1,k+along+2,k+along+1)
                        faces.append(f if not layer else tuple(reversed(f)))
            rim=list(range(along+1))+[j*(along+1)+along for j in range(1,down+1)]+[down*(along+1)+i for i in range(along-1,-1,-1)]+[j*(along+1)for j in range(down-1,0,-1)]
            for a,b in zip(rim,rim[1:]+rim[:1]):faces.append((a,b,b+count,a+count))
            g.add(vertices,faces,'Plastic',True)


def _cloth_trim(g,skin,nx,nz,mat='Fabric',fringe=False):
    vertices=g.meshes[skin]['v']
    rim=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,nz+1)]+[nz*(nx+1)+i for i in range(nx-1,-1,-1)]+[j*(nx+1)for j in range(nz-1,0,-1)]
    points=[vertices[i]for i in rim]
    g.path(points+[points[0]],.0016,mat,n=5,collider=False)
    if fringe:
        for row,sign in((0,-1),(nz,1)):
            for i in range(1,nx,2):
                p=vertices[row*(nx+1)+i]
                g.path([p,(p[0]+.003*sin(i),p[1]-.004,p[2]+sign*.047)],.0009,mat,n=4,collider=False)


def _legs(g, w, d, y, height, mat='Wood', r=.025, splay=.025):
    for sx in (-1, 1):
        for sz in (-1, 1):
            a=(sx*w*.5,y+height,sz*d*.5);b=(sx*(w*.5+splay),y,sz*(d*.5+splay))
            if mat in ('Wood','DarkWood','PaintedWood'):_profiled_timber_leg(g,a,b,r,mat,n=10)
            else:g.rod(a,b,r,mat,n=10)


def _feet(g, w, d, h=.10, mat='DarkWood'):
    for sx in (-1, 1):
        for sz in (-1, 1):
            g.box((sx*w*.41, h*.5, sz*d*.39), (.060, h, .060), mat, bevel=.007)


def _pull(g, x, y, z, length=.10, mat='Brass', vertical=False):
    if vertical:
        g.rod((x, y-length*.5, z), (x, y+length*.5, z), .006, mat, n=8)
        for s in (-1, 1):
            g.rod((x, y+s*length*.5, z-.033), (x, y+s*length*.5, z), .005, mat, n=8)
            _fastener(g,(x,y+s*length*.5,z-.030),r=.003,mat=mat)
    else:
        g.rod((x-length*.5, y, z), (x+length*.5, y, z), .006, mat, n=8)
        for s in (-1, 1):
            g.rod((x+s*length*.5, y, z-.033), (x+s*length*.5, y, z), .005, mat, n=8)
            _fastener(g,(x+s*length*.5,y,z-.030),r=.003,mat=mat)


def _knob(g, x, y, z, mat='Brass'):
    g.rod((x, y, z-.020), (x, y, z+.006), .006, mat, n=8)
    g.sphere((x, y, z+.008), (.027, .027, .020), mat, n=10, rings=5)


def _hinges(g, x, y, z, h, mat='Brass', side=-1):
    for sy in (-1, 1):
        yy = y+sy*h*.31
        g.rod((x, yy-.024, z), (x, yy+.024, z), .005, mat, n=8)
        g.box((x-side*.012, yy, z-.003), (.026, .032, .003), mat, bevel=.001)


def _carcass(g, w, h, d, bottom=.10, mat='Wood', thickness=.024,
             shelves=(), divider=None, top=True, back=True, zones=True):
    """Open front box: a back and separate side, base, shelf boards."""
    t = thickness
    g.static()
    g.box((-w*.5+t*.5, (h+bottom)*.5, 0), (t, h-bottom, d), mat, bevel=.004)
    g.box(( w*.5-t*.5, (h+bottom)*.5, 0), (t, h-bottom, d), mat, bevel=.004)
    g.box((0, bottom+t*.5, 0), (w-2*t, t, d-.012), mat, bevel=.004)
    if top:
        g.box((0, h-t*.5, 0), (w, t, d), mat, bevel=.005)
    if back:
        g.box((0, (h+bottom)*.5, -d*.5+t*.28), (w-2*t, h-bottom-t, t*.55), 'Plywood', bevel=.002)
        # Back-panel rebates are structural perimeter ledges, inside the existing
        # wall thickness and behind every authored item-placement volume.
        for sx in(-1,1):
            g.box((sx*(w*.5-t-.003),(h+bottom)*.5,-d*.5+t*.72),
                  (.008,h-bottom-.035,.010),'Wood',bevel=.001,collider=False)
        for yy in(bottom+t*.9,h-t*.9):
            g.box((0,yy,-d*.5+t*.72),(w-2*t-.01,.012,.010),'Wood',bevel=.001,collider=False)
    ordered_shelves=sorted(shelves)
    for yy in ordered_shelves:
        g.box((0, yy, -.008), (w-2*t, t*.70, d-.035), mat, bevel=.003)
        for sx in(-1,1):
            for zz in(-d*.29,d*.29):
                g.rod((sx*(w*.5-t*.6),yy-t*.35-.003,zz),
                      (sx*(w*.5-t-.009),yy-t*.35-.003,zz),.003,'Steel',n=8,collider=False)
        next_y=min([q for q in ordered_shelves if q>yy]+[h-t])
        zone_h=min(.20,next_y-yy-t-.024)
        if zones and zone_h>.035:
            if divider is None:
                g.zone('Shelf_%03d' % round(yy*100), (0, yy+t+.015+zone_h*.5, -.008),
                       (w-2*t-.028, zone_h, d-.09))
            else:
                for side,xx,ww in [('L',(-w*.5+t+divider-t*.5)*.5,divider-t*.5-(-w*.5+t)),
                                   ('R',(divider+t*.5+w*.5-t)*.5,w*.5-t-divider-t*.5)]:
                    g.zone('Shelf_%03d_%s'%(round(yy*100),side),
                           (xx,yy+t+.015+zone_h*.5,-.008),(ww-.025,zone_h,d-.09))
    if divider is not None:
        g.box((divider, (h+bottom)*.5, -.008), (t, h-bottom-2*t, d-.04), mat, bevel=.003)
    # The visible face of a board has a separate, thin edge strip; this closes the
    # laminated end grain without extending into any door's swept volume.
    for sx in(-1,1):
        g.box((sx*(w*.5-t*.5),(h+bottom)*.5,d*.5-.001),
              (t*.76,h-bottom-.018,.003),mat,bevel=.001,collider=False)
    # A placing zone occupies the usable lowest bay, never the shelf/divider boards.
    bay_top=min(ordered_shelves+[h-t])
    zone_h=max(.025,bay_top-bottom-t*2-.024)
    if zones:
        if divider is None:
            g.zone('Interior', (0,bottom+t+.012+zone_h*.5,-.008),
                   (w-2*t-.028,zone_h,d-.09))
        else:
            for side,xx,ww in [('L',(-w*.5+t+divider-t*.5)*.5,divider-t*.5-(-w*.5+t)),
                               ('R',(divider+t*.5+w*.5-t)*.5,w*.5-t-divider-t*.5)]:
                g.zone('Interior_'+side,(xx,bottom+t+.012+zone_h*.5,-.008),
                       (ww-.025,zone_h,d-.09))


def _door(g, name, x, y, z, w, h, mat='Wood', side=-1, glass=False,
          inset=False, mirror=False, damaged=False, amount=105):
    """Separate hinge at the actual outside edge; handles move with the leaf."""
    px = x+side*w*.5
    g.part(name, pivot=(px, y, z), kind='hinge', axis=(0, 1, 0),
           amount=side*amount, label='Дверца')
    core = 'Mirror' if mirror else ('Glass' if glass else mat)
    if glass or mirror:
        rail=.040 if glass else .030
        g.box((x,y,z), (w-rail*2,h-rail*2,.010), core, bevel=.001)
        for sx in (-1,1):
            g.box((x+sx*(w-rail)*.5,y,z), (rail,h,.026), mat, bevel=.004)
        for sy in (-1,1):
            g.box((x,y+sy*(h-rail)*.5,z), (w-2*rail,rail,.026), mat, bevel=.004)
    else:
        g.box((x, y, z), (w-.004, h-.004, .024), mat, bevel=.005,
              rot=(0, 0, 0))
        if inset:
            rw=min(.040,w*.12)
            for sx in (-1,1):
                g.box((x+sx*(w*.5-rw),y,z+.016), (rw*.65,h-.08,.011), 'DarkWood', bevel=.004)
            for sy in (-1,1):
                g.box((x,y+sy*(h*.5-.048),z+.016), (w-.08,rw*.65,.011), 'DarkWood', bevel=.004)
    if not damaged:
        _pull(g,x-side*(w*.5-.060),y,z+.041, .14 if h>.75 else .085,vertical=h>.75)
    else:
        # Missing handle leaves two mounting studs rather than a decorative new handle.
        for yy in (y-.045,y+.045):
            g.rod((x-side*(w*.5-.060),yy,z+.012),(x-side*(w*.5-.060),yy,z+.022),.003,'Rust',n=6)
    _hinges(g, px, y, z-.015, h,side=side)
    if inset and not(glass or mirror):
        # Two inset back rails are part of the leaf; their ends stop well clear of
        # the pivot edge. They are joinery, rather than a second full door slab.
        for sy in(-1,1):
            g.box((x,y+sy*h*.29,z-.011),(w*.69,.042,.008),'Wood',bevel=.002,collider=False)
            for sx in(-1,1):_fastener(g,(x+sx*w*.27,y+sy*h*.29,z-.016),normal=(0,0,-1),r=.0035)
    g.static()


def _drawer(g, name, x, y, z, w, h, d, mat='Wood', missing=False,
            travel=None, pull='bar', front_mat=None):
    """A five-board open tray, with a separate face; front is centred at z."""
    g.static()
    for sx in (-1,1):
        g.box((x+sx*(w*.5-.019),y-h*.35,z-d*.52),(.012,.018,d*.89),'Steel',bevel=.002)
    g.part(name,pivot=(x,y,z),kind='slide',axis=(0,0,1),
           amount=(d*.70 if travel is None else travel),label='Ящик')
    g.box((x,y,z),(w-.006,h-.008,.024),front_mat or mat,bevel=.005)
    g.box((x,y-h*.40,z-d*.46),(w-.055,.012,d-.028),'Plywood',bevel=.002)
    for sx in (-1,1):
        g.box((x+sx*(w*.5-.031),y-h*.055,z-d*.46),(.014,h*.69,d-.03),'Plywood',bevel=.002)
    g.box((x,y-h*.055,z-d+.035),(w-.05,h*.69,.014),'Plywood',bevel=.002)
    for sx in(-1,1):
        # A nested runner travels with the drawer, whereas its existing outer
        # guide stays in the case. The fine steel race does not add a blocking
        # collider to an intentionally sliding bearing interface.
        g.box((x+sx*(w*.5-.024),y-h*.25,z-d*.43),(.004,.014,d*.72),'Steel',bevel=.001,collider=False)
        for zz in(z-d*.18,z-d*.70):
            _fastener(g,(x+sx*(w*.5-.021),y-h*.25,zz),normal=(sx,0,0),r=.0026)
    if mat in('Wood','DarkWood') and pull=='knob':
        inset_w=w-.10;inset_h=max(.035,h-.070)
        g.path([(x-inset_w*.5,y-inset_h*.5,z+.015),(x+inset_w*.5,y-inset_h*.5,z+.015),
                (x+inset_w*.5,y+inset_h*.5,z+.015),(x-inset_w*.5,y+inset_h*.5,z+.015),
                (x-inset_w*.5,y-inset_h*.5,z+.015)],.0022,mat,n=6,collider=False)
    if not missing:
        if pull=='knob':
            if w>.66:
                for sx in (-1,1): _knob(g,x+sx*w*.28,y,z+.029)
            else: _knob(g,x,y,z+.029)
        else: _pull(g,x,y,z+.043,min(.20,w*.34),mat='Brass')
    else:
        for sx in (-1,1):
            g.sphere((x+sx*.048,y,z+.014),(.007,.007,.004),'Rust',n=6,rings=4)
    g.zone(name+'_Contents',(x,y,z-d*.46),(w-.085,h*.58,d-.08))
    g.static()


def _beading(g,w,d,y,mat='DarkWood',z=0):
    for sx in (-1,1):
        g.box((sx*w*.5,y,z),(.018,.024,d-.018),mat,bevel=.004)
    for sz in (-1,1):
        g.box((0,y,z+sz*d*.5),(w+.018,.024,.018),mat,bevel=.004)


def _welt(g,p,s,mat='DarkWood'):
    x,y,z=p;w,h,d=s
    g.path([(x-w*.45,y,z-d*.43),(x+w*.45,y,z-d*.43),
            (x+w*.48,y,z+d*.38),(x+w*.43,y,z+d*.45),
            (x-w*.43,y,z+d*.45),(x-w*.48,y,z+d*.38),
            (x-w*.45,y,z-d*.43)],.0022,mat,n=5)


def _seat(g,x,y,z,w,d,mat='Fabric',sag=.025,name=None,seed=0):
    if name:
        g.part(name,pivot=(x,y-.07,z),kind='slide',axis=(0,0,1),amount=.20,label='Подушка')
    g.cushion((x,y,z),(w,.16,d),mat,sag=sag,seed=seed,segments=10)
    _welt(g,(x,y-.034,z),(w,.16,d),mat)
    if name:g.static()


def _sofa(row):
    g=Geo(row);v=row['variant'];seed=row['id']
    w=[1.94,2.12,2.42,2.12,1.48,1.97,2.24,1.88,2.55,2.08][v]
    d=[.86,.92,1.46,.91,.78,.86,.94,.85,1.12,.93][v]
    mat=['RedFabric','Fabric','GreenFabric','Leather','Fabric','GreenFabric','RedFabric','Fabric','Fabric','RedFabric'][v]
    if v==9:
        # The exposed rails sit higher than the upholstered plinths. Their feet
        # terminate on the rail centre lines, including the offset front rail.
        for sx in (-1,1):
            for zz in (-.34,.30):
                g.box((sx*w*.41,.13,zz),(.060,.26,.060),'DarkWood',bevel=.007)
    elif v!=8:
        # Keep the foot top beneath the flat underside, clear of the edge bevel.
        _feet(g,w,min(d,.91)*.87,h=.13)
    if v==8:
        # A low modular sectional consists of visibly independent boxes and a chaise.
        for j in range(3):
            x=(j-1)*.82
            g.box((x,.23,-.04),(.77,.25,.96),'DarkWood',bevel=.025)
            for sx in (-1,1):
                for zz in (-.37,.29):
                    g.box((x+sx*.25,.065,zz),(.055,.13,.055),'DarkWood',bevel=.006)
            _seat(g,x,.44,.03,.76,.78,mat,sag=.025+j*.008,name='LooseSeat_%d'%j,seed=seed+j)
            g.cushion((x,.73,-.41),(.77,.53,.19),mat,sag=.025,segments=9)
        g.box((.82,.23,.77),(.77,.25,.58),'DarkWood',bevel=.025)
        for sx in (-1,1):
            for zz in (.56,.99):
                g.box((.82+sx*.25,.065,zz),(.055,.13,.055),'DarkWood',bevel=.006)
        _seat(g,.82,.44,.75,.76,.64,mat,sag=.035,name='ChaiseCushion',seed=seed+5)
        g.cushion((-1.23,.53,-.02),(.15,.45,.98),mat,segments=8)
        return g
    if v==9:
        # Exposed frame, split upholstery and lost front rail are actual construction.
        for z in (-.34,.30):g.box((0,.29,z),(w-.22,.065,.055),'DarkWood',bevel=.008)
        for x in (-.83,-.31,.32,.82):
            g.box((x,.30,-.02),(.055,.052,.75),'Plywood',bevel=.005,rot=(0,0,3 if x>0 else -2))
        g.box((.58,.235,.34),(.40,.035,.095),'Plywood',bevel=.003,rot=(0,-9,-7))
        g.cushion((-.45,.44,.01),(.82,.18,.73),mat,sag=.08,seed=seed,segments=10)
        g.cushion((.48,.42,-.03),(.84,.13,.70),'Foam',sag=.08,seed=seed+2,segments=10)
        g.cloth((.57,.50,-.22),(.88,.025,.34),mat,fold=.045,nx=9,nz=7)
        for k in range(6):
            x=.15+k*.115
            pts=[(x+.025*cos(a),.30+.025*sin(a),-.10+a*.018) for a in [i*pi/3 for i in range(18)]]
            g.path(pts,.002,'Rust',n=5)
        g.cushion((-.52,.74,-.36),(.91,.61,.17),mat,sag=.07,segments=10)
        g.box((.53,.69,-.40),(.88,.59,.040),'Plywood',bevel=.004,rot=(0,0,4))
        g.cushion((-.99,.59,0),(.14,.39,.78),mat,segments=8)
        g.box((.96,.43,.02),(.070,.12,.78),'DarkWood',bevel=.008)
        return g
    # Base is an actual open linen chest for the folding variants.
    if v in (0,1,5,7):
        _carcass(g,w-.20,.36,.70,bottom=.13,mat='DarkWood',top=False,back=True)
        # The linen chest remains open at the top only. A thin upholstered-base
        # fascia closes its front without filling the storage cavity or its zone.
        g.box((0,.245,.35),(w-.22,.23,.024),'DarkWood',bevel=.005)
        # The high rear seam is the lid's actual hinge line. This clears both the
        # back upholstery and fixed armrests throughout the storage-opening arc.
        g.part('LinenSeat',pivot=(0,.54,-.19),kind='hinge',axis=(1,0,0),amount=-37,label='Бельевой ящик')
        g.box((0,.375,.005),(w-.38,.033,.72),'Plywood',bevel=.004)
        # Seating upholstery is on this lift platform, independent from back-rest folding.
        for j in range(2):
            xx=(j-.5)*(w-.32)*.5
            g.cushion((xx,.46,.02),((w-.35)*.5,.17,.72),mat,sag=.03,seed=seed+j,segments=10)
            _welt(g,(xx,.425,.02),((w-.35)*.5,.17,.72),mat)
        g.static()
    else:
        g.box((0,.25,-.03),(w-.16,.25,min(d,.90)-.10),'DarkWood',bevel=.018)
        count=3 if v==6 else 2
        for j in range(count):
            xx=(j-(count-1)*.5)*(w-.32)/count
            _seat(g,xx,.46,.02,(w-.34)/count,.73,mat,
                  sag=.073 if v==6 else .03,name='LooseSeat_%d'%j,seed=seed+j)
    if v in (0,1):
        g.part('FoldBack',pivot=(0,.44,-.40),kind='hinge',axis=(1,0,0),amount=-90,label='Спинка дивана')
    back_clearance=.40 if v in (0,1) else .18
    g.box((0,.69,-.37),(w-back_clearance,.55,.060),'Plywood',bevel=.008)
    nback=3 if v in (3,6) else 2
    for j in range(nback):
        back_span=w-.42 if v in (0,1) else w-.28
        xx=(j-(nback-1)*.5)*back_span/nback
        back_skin=len(g.meshes)
        g.cushion((xx,.75,-.30),((back_span-.02)/nback,.57,.18),mat,sag=.035,seed=seed+j,segments=10)
        if v in (0,3,4):
            for bx in (-1,1):
                for yy in (.67,.84):
                    _fabric_button(g,xx+bx*(w-.30)/nback*.22,yy,back_skin,mat)
    g.static()
    for sx in (-1,1):
        xx=sx*(w*.5-.075)
        if v==5:
            for zz in (-.28,.27):g.rod((xx,.28,zz),(xx,.64,zz),.025,'Wood',n=10)
            g.box((xx,.66,-.015),(.14,.050,.81),'Wood',bevel=.018)
        elif v==7 and sx==1:
            g.cushion((xx,.55,-.01),(.16,.37,.76),'Foam',sag=.05,segments=8)
            g.cloth((xx,.715,-.18),(.15,.025,.38),mat,fold=.045,nx=6,nz=7)
        else:
            g.cushion((xx,.57,-.02),(.16,.40,.80),mat,sag=.020,seed=seed,segments=9)
    if v==2:
        # Chaise produces a true L footprint, with matching independent seating.
        g.box((-.73,.24,.74),(.79,.25,.69),'DarkWood',bevel=.020)
        for sx in (-1,1):
            g.box((-.73+sx*.26,.065,.99),(.060,.13,.060),'DarkWood',bevel=.007)
        _seat(g,-.73,.46,.72,.79,.73,mat,sag=.055,name='ChaiseCushion',seed=seed+10)
        g.cushion((-.31,.48,.94),(.15,.24,.27),'Foam',sag=.035,segments=7)
        g.cloth((-.34,.59,.76),(.22,.020,.29),mat,fold=.035,nx=5,nz=6)
    if v==1:
        g.part('PulloutBed',pivot=(0,.24,.37),kind='slide',axis=(0,0,1),amount=.60,label='Спальное место')
        g.box((0,.22,.20),(w-.34,.08,.57),'Plywood',bevel=.004)
        g.cushion((0,.31,.23),(w-.34,.13,.57),mat,sag=.025,segments=10)
        for sx in (-1,1):g.rod((sx*(w*.5-.24),.05,.46),(sx*(w*.5-.24),.21,.46),.020,'Steel',n=10)
        g.static()
    return g


def _bed(row):
    g=Geo(row);v=row['variant'];seed=row['id']
    w=[.90,.96,1.62,1.67,1.54,.77,.94,.94,.78,.88][v]
    d=[2.01,2.06,2.10,2.16,2.04,1.60,2.04,2.04,1.91,1.98][v]
    if v==8:
        # A steel folding camp bed with a two-part fabric deck and U-shaped trestles.
        for sx in (-1,1):g.rod((sx*.365,.37,-.89),(sx*.365,.37,0),.018,'Steel',n=12)
        g.cloth((0,.372,-.46),(.70,.025,.86),'GreenFabric',fold=.020,nx=10,nz=13)
        for zz in (-.63,-.10):
            g.path([(-.33,.36,zz),(-.28,.035,zz),(.28,.035,zz),(.33,.36,zz)],.016,'Steel',n=10)
        g.part('FoldingDeck',pivot=(0,.41,0),kind='hinge',axis=(1,0,0),amount=-158,label='Сложить раскладушку')
        for sx in (-1,1):g.rod((sx*.365,.37,0),(sx*.365,.37,.89),.018,'Steel',n=12)
        g.rod((-.365,.37,.89),(.365,.37,.89),.018,'Steel',n=12)
        g.cloth((0,.372,.46),(.70,.025,.86),'GreenFabric',fold=.021,nx=10,nz=13)
        g.path([(-.33,.36,.64),(-.28,.035,.64),(.28,.035,.64),(.33,.36,.64)],.016,'Steel',n=10)
        g.static()
        return g
    metal=v in (0,6,7,9)
    deck=.35 if metal else .32
    mat='Rust' if v in (7,9) else 'PaintedWood' if v==5 else 'Steel' if metal else 'DarkWood'
    if metal:
        for sx in (-1,1):
            if v==7 and sx==1:
                g.path([(sx*w*.46,.31,-d*.46),(sx*w*.48,.28,-.18),(sx*w*.44,.34,d*.46)],.024,mat,n=10)
            else:g.rod((sx*w*.46,deck,-d*.46),(sx*w*.46,deck,d*.46),.022,mat,n=12)
        for sz in (-1,1):
            endh=1.07 if sz<0 else .70
            if v==6:endh=1.85 if sz<0 else 1.71
            for sx in (-1,1):
                g.rod((sx*w*.46,.015,sz*d*.46),(sx*w*.46,endh,sz*d*.46),.025,mat,n=12)
                g.sphere((sx*w*.46,endh+.012,sz*d*.46),(.064,.052,.064),'Brass' if v==0 else mat,n=12,rings=6)
            if v in (0,9):
                top=endh-.06
                g.rod((-w*.46,top,sz*d*.46),(w*.46,top,sz*d*.46),.021,mat,n=12)
                g.rod((-w*.46,deck+.15,sz*d*.46),(w*.46,deck+.15,sz*d*.46),.016,mat,n=10)
                for j in range(7):
                    xx=(j-3)*w*.115
                    g.rod((xx,deck+.15,sz*d*.46),(xx,top,sz*d*.46),.008,mat,n=8)
            elif v==7:
                g.path([(-w*.46,endh-.10,sz*d*.46),(0,endh+.015,sz*d*.44),(w*.46,endh-.15,sz*d*.46)],.020,mat,n=12)
                for xx in (-w*.23,0,w*.23):g.rod((xx,deck+.12,sz*d*.46),(xx,endh-.14,sz*d*.46),.009,mat,n=8)
        if v in (0,7):
            for j in range(13):
                zz=-d*.42+j*d*.84/12
                g.box((0,deck-.008,zz),(w*.89,.025,.045),'Plywood',bevel=.003)
        elif v==9:
            # Separate closed diamond links form the visible chain mesh. The
            # slight alternating height is a woven overlap, not a flat rod grid.
            columns,rows=7,15;cw=w*.86/columns;cz=d*.84/rows
            for j in range(rows):
                for i in range(columns):
                    xx=-w*.43+(i+.5)*cw;zz=-d*.42+(j+.5)*cz
                    yy=deck-.020*sin((i+.5)*pi/columns)*sin((j+.5)*pi/rows)
                    yy+=.0018*((i+j)%2)
                    g.path([(xx-cw*.50,yy,zz),(xx,yy+.0018,zz+cz*.50),
                            (xx+cw*.50,yy,zz),(xx,yy-.0018,zz-cz*.50),
                            (xx-cw*.50,yy,zz)],.0021,mat,n=5,collider=False)
            for sx in(-1,1):
                for j in range(8):
                    zz=-d*.37+j*d*.74/7
                    g.path([(sx*w*.46,deck,zz),(sx*w*.43,deck,zz+.015),
                            (sx*w*.40,deck-.005,zz),(sx*w*.43,deck,zz-.015),
                            (sx*w*.46,deck,zz)],.0022,'Steel',n=5,collider=False)
        else:
            # Open spring mesh, not a solid inaccessible collision slab.
            for j in range(16):
                zz=-d*.43+j*d*.86/15
                pts=[(-w*.44+k*w*.88/10,deck-.026*sin(k*pi/10),zz+.012*sin(k*pi*2)) for k in range(11)]
                g.path(pts,.0027,mat,n=5)
            for j in range(8):
                xx=-w*.42+j*w*.84/7
                g.path([(xx,deck-.025*sin(k*pi/16),-d*.43+k*d*.86/16) for k in range(17)],.0027,mat,n=5)
    else:
        # Feet must sit under the side rails, not inside the open bed cavity.
        # The generic cabinet offsets leave wide beds' feet clear of every rail.
        for sx in (-1,1):
            for sz in (-1,1):
                g.box((sx*(w*.5-.045),.115,sz*(d*.5-.105)),
                      (.060,.23,.060),mat,bevel=.007)
        for sx in (-1,1):g.box((sx*(w*.5-.025),deck-.025,0),(.045,.16,d-.09),mat,bevel=.007)
        for sz in (-1,1):g.box((0,deck-.025,sz*(d*.5-.024)),(w,.16,.045),mat,bevel=.007)
        for j in range(15):g.box((0,deck-.013,-d*.43+j*d*.86/14),(w-.11,.020,.055),'Plywood',bevel=.003)
        hh=.85 if v!=5 else .72
        if v==3:
            head_skin=len(g.meshes)
            g.cushion((0,.68,-d*.48),(w,.79,.14),'Fabric',sag=.025,segments=12)
            for j in range(6):
                xx=(j-2.5)*w*.14
                for yy in (.57,.80):_fabric_button(g,xx,yy,head_skin,'Fabric')
        elif v==5:
            for sx in (-1,1):
                g.box((sx*w*.46,.43,-d*.46),(.045,.83,.045),'PaintedWood',bevel=.009)
            g.box((0,.63,-d*.46),(w*.88,.23,.040),'PaintedWood',bevel=.018)
            for j in range(4):g.rod(((j-1.5)*w*.20,.35,-d*.46),((j-1.5)*w*.20,.53,-d*.46),.018,'PaintedWood',n=10)
            g.box((0,.49,d*.46),(w*.95,.30,.040),'PaintedWood',bevel=.015)
        else:
            g.box((0,.57,-d*.48),(w,.76 if v==2 else .65,.040),mat,bevel=.010)
            g.box((0,.39,d*.48),(w,.35,.040),mat,bevel=.010)
            for yy in (.32,.80 if v==2 else .73):
                g.box((0,yy,-d*.456),(w-.08,.025,.018),'Wood',bevel=.005)
    if v==4:
        for side in (-1,1):
            # Drawers open on the long sides of the bed, preserving the central support.
            x=side*(w*.5-.02)
            g.part('BedDrawer_%s'%side,pivot=(x,.15,.22),kind='slide',axis=(side,0,0),amount=.48,label='Ящик кровати')
            g.box((x,.15,.22),(.025,.22,.81),'DarkWood',bevel=.005)
            g.box((x-side*.28,.065,.22),(.54,.016,.76),'Plywood',bevel=.003)
            for zz in (-.155,.595):g.box((x-side*.28,.14,zz),(.54,.15,.014),'Plywood',bevel=.003)
            g.box((x-side*.55,.14,.22),(.016,.15,.76),'Plywood',bevel=.003)
            g.rod((x+side*.024,.17,.12),(x+side*.024,.17,.32),.007,'Brass',n=8)
            g.zone('BedDrawer_Contents_%s'%side,(x-side*.28,.16,.22),(.50,.14,.70))
            g.static()
    if v!=9:
        my=deck+.115
        g.cushion((0,my,0),(w-.12,.20,d-.13),'Fabric' if v!=7 else 'GreenFabric',sag=.035 if v==7 else .022,seed=seed,segments=13)
        _welt(g,(0,my-.045,0),(w-.12,.20,d-.13),'Fabric')
        if v in (2,3,4):
            for sx in (-1,1):
                g.part('Pillow_%s'%sx,pivot=(sx*w*.23,my+.13,-d*.29),kind='lift',axis=(0,1,0),amount=.17,label='Подушка')
                g.cushion((sx*w*.23,my+.15,-d*.29),(w*.39,.15,.48),'Fabric',sag=.035,seed=seed+sx,segments=9)
                g.static()
        elif v==5:
            g.cushion((0,my+.12,-d*.28),(.47,.12,.34),'RedFabric',sag=.02,segments=9)
    else:
        # A torn cotton pad exposes the historic chain-link bed base.
        g.cloth((-.15,deck+.035,-.30),(.43,.06,.91),'Fabric',fold=.034,nx=10,nz=15)
    if v==6:
        upper=1.43
        for sx in (-1,1):g.rod((sx*w*.46,upper,-d*.46),(sx*w*.46,upper,d*.46),.022,'Steel',n=12)
        for j in range(12):g.box((0,upper-.015,-d*.43+j*d*.86/11),(w*.88,.024,.048),'Plywood',bevel=.003)
        g.cushion((0,upper+.10,0),(w-.12,.18,d-.13),'GreenFabric',sag=.022,segments=12)
        for sz in (-1,1):
            for yy in (upper+.10,upper+.34):g.rod((-w*.46,yy,sz*d*.46),(w*.46,yy,sz*d*.46),.015,'Steel',n=10)
        g.rod((-w*.46,upper+.32,-d*.46),(-w*.46,upper+.32,d*.46),.016,'Steel',n=10)
        # Right-hand safety guard leaves a real entrance adjacent to the ladder.
        g.rod((w*.46,upper+.32,-d*.46),(w*.46,upper+.32,d*.16),.016,'Steel',n=10)
        for zz in (-.70,.10):g.rod((w*.46,upper,zz),(w*.46,upper+.32,zz),.014,'Steel',n=10)
        for zz in (.51,.84):g.rod((w*.52,.025,zz),(w*.47,upper+.15,zz),.017,'Steel',n=10)
        for j in range(5):g.rod((w*.51,.22+j*.265,.51),(w*.51,.22+j*.265,.84),.014,'Steel',n=10)
    return g


def _chair_bed(row):
    """Three full-height sleeping cushions, nested carriers and deployable U-leg.

    Closed layers occupy separate heights in an open-bottom cabinet. The two
    slides first leave that cabinet; only then do the two lifts align the bed.
    The forward support folds underneath its own section, rather than passing
    below the floor while stowed. Parent metadata is consumed by the Unity build.
    """
    g=Geo(row);w=.82;mat='GreenFabric'
    # A clear U-shaped case admits the rolling carrier; a front bottom plank
    # would obstruct the wheels during extraction, so none is placed there.
    for sx in(-1,1):
        for zz in(-.32,.32):g.box((sx*.37,.03,zz),(.045,.06,.045),'DarkWood',bevel=.005)
        g.box((sx*.379,.245,0),(.022,.37,.76),'Wood',bevel=.004)
        g.box((sx*.366,.075,0),(.028,.030,.74),'DarkWood',bevel=.003)
    g.box((0,.245,-.371),(.736,.37,.018),'Plywood',bevel=.003)
    g.box((0,.075,-.355),(.736,.030,.032),'DarkWood',bevel=.003)
    g.box((0,.436,-.005),(.756,.016,.53),'Plywood',bevel=.003)
    _seat(g,0,.51,0,.63,.57,mat,sag=.025,name='LooseSeat',seed=row['id'])
    g.cushion((0,.80,-.29),(.68,.58,.17),mat,sag=.025,segments=11)
    for sx in(-1,1):
        g.cushion((sx*.35,.64,-.04),(.14,.36,.60),mat,sag=.018,segments=10)
        for yy in(.22,.38):_fastener(g,(sx*.391,yy,.21),normal=(sx,0,0),r=.004)

    g.part('BedExtension',pivot=(0,.245,.29),kind='slide',axis=(0,0,1),amount=.60,label='Выкатить основание')
    for sx in(-1,1):
        g.box((sx*.335,.183,0),(.018,.022,.62),'Steel',bevel=.002)
        # Real hollow outer stanchions expose their inner shafts when raised.
        g.lathe((sx*.32,.035,.25),[(.016,0),(.016,.235),(.011,.235),(.011,0)],'Steel',axis='y',n=12)
        g.colliders.append(dict(part=g.current,type='capsule',a=[sx*.32,.035,.25],b=[sx*.32,.27,.25],radius=.014))
        g.lathe((sx*.32,0,.25),[(0,0),(.022,0),(.022,.016),(.016,.035),(0,.035)],'Rubber',axis='y',n=12,collider=True)
    g.box((0,.235,.335),(.70,.32,.020),'Wood',bevel=.006)
    _pull(g,0,.24,.366,.14,'Steel')

    def platform(y,width,depth):
        for sx in(-1,1):g.box((sx*(width*.5-.012),y,0),(.024,.028,depth),'Plywood',bevel=.003)
        for zz in(-depth*.5+.012,depth*.5-.012):g.box((0,y,zz),(width-.048,.028,.024),'Plywood',bevel=.003)
        for zz in(-depth*.27,0,depth*.27):g.box((0,y,zz),(width-.048,.018,.045),'Wood',bevel=.003)

    g.part('BedRaise',pivot=(0,.27,0),kind='slide',axis=(0,1,0),amount=.17,
           label='Поднять среднюю секцию',parent='BedExtension')
    platform(.270,.62,.62)
    for sx in(-1,1):
        g.rod((sx*.32,.08,.25),(sx*.32,.27,.25),.010,'Chrome',n=12)
        g.box((sx*.31,.270,.25),(.04,.025,.035),'Steel',bevel=.002,collider=False)
    g.cushion((0,.350,0),(.62,.14,.62),mat,sag=.018,segments=11)

    g.part('BedFootExtension',pivot=(0,.10,0),kind='slide',axis=(0,0,1),amount=.64,
           label='Выдвинуть ножную секцию',parent='BedExtension')
    for sx in(-1,1):g.box((sx*.325,.080,0),(.018,.012,.66),'Steel',bevel=.002)
    for zz in(-.31,.31):g.box((0,.080,zz),(.65,.012,.018),'Steel',bevel=.002)
    _pull(g,0,.12,.355,.12,'Steel')

    g.part('BedFootRaise',pivot=(0,.10,0),kind='slide',axis=(0,1,0),amount=.343,
           label='Поднять ножную секцию',parent='BedFootExtension')
    platform(.100,.66,.66)
    g.cushion((0,.177,0),(.62,.14,.66),mat,sag=.018,segments=11)
    for sx in(-1,1):
        g.box((sx*.27,.074,.25),(.022,.026,.030),'Steel',bevel=.002,collider=False)
        _fastener(g,(sx*.282,.060,.25),normal=(sx,0,0),r=.004)
    g.rod((-.285,.060,.25),(.285,.060,.25),.006,'Steel',n=10,collider=False)

    g.part('BedFootLegs',pivot=(0,.060,.25),kind='hinge',axis=(1,0,0),amount=-90,
           label='Развернуть переднюю опору',parent='BedFootRaise')
    length=.383
    for sx in(-1,1):
        g.rod((sx*.27,.060,.25),(sx*.27,.060,.25-length),.010,'Steel',n=12)
        g.sphere((sx*.27,.060,.25-length),(.040,.040,.040),'Rubber',n=12,rings=6)
        # A rotating cube around a round foot sinks at intermediate angles.
        # This short capsule is a contained sphere, not a box around the ball.
        g.colliders.pop()
        g.colliders.append(dict(part=g.current,type='capsule',a=[sx*.27,.059,.25-length],
                                b=[sx*.27,.061,.25-length],radius=.0185))
    g.rod((-.27,.060,.25-length),(.27,.060,.25-length),.010,'Steel',n=12)
    g.static()
    return g


def _localized_torn_mattress(g,seed):
    """An intact mattress perimeter with a local torn ticking/foam opening."""
    g.cushion((0,.100,0),(.88,.20,1.90),'Fabric',sag=.015,seed=seed,segments=14)
    g.cushion((.205,.195,.38),(.34,.11,.57),'Foam',sag=.018,seed=seed+7,segments=10)
    nx,nz=16,30;vertices=[];faces=[];base_faces=[]
    def surface(x,z):
        dx=(x-.205)/.17;dz=(z-.38)/.29
        distance=(dx*dx+dz*dz)**.5
        angle=atan2(dz,dx)
        wrinkle=.010*sin(angle*7+seed*.3)*exp(-abs(distance-1)*3.2)
        return .215+.004*sin(x*11+z*7)+wrinkle
    for layer in(0,1):
        for j in range(nz+1):
            zz=(j/nz-.5)*1.86
            for i in range(nx+1):
                xx=(i/nx-.5)*.85
                vertices.append((xx,surface(xx,zz)-layer*.004,zz))
    for j in range(nz):
        for i in range(nx):
            x=(i/nx+.5/nx-.5)*.85;z=(j/nz+.5/nz-.5)*1.86
            dx=(x-.205)/.17;dz=(z-.38)/.29;angle=atan2(dz,dx)
            # An asymmetric serrated boundary, not a rectangular missing block.
            if dx*dx+dz*dz<(1+.16*sin(angle*5)+.09*cos(angle*9))**2:continue
            a=j*(nx+1)+i;base_faces.append((a,a+1,a+nx+2,a+nx+1))
    count=(nx+1)*(nz+1);edge_counts={}
    for face in base_faces:
        faces.append(face);faces.append(tuple(v+count for v in reversed(face)))
        for a,b in zip(face,face[1:]+face[:1]):
            key=tuple(sorted((a,b)));edge_counts[key]=edge_counts.get(key,0)+1
    for (a,b),count_edge in edge_counts.items():
        if count_edge!=1:continue
        faces.append((a,b,b+count,a+count))
    g.add(vertices,faces,'Fabric',True)
    # A few fibres remain attached to the actual cut boundary.
    for k,((a,b),number) in enumerate(edge_counts.items()):
        if number!=1 or k%7:continue
        p=vertices[a];q=vertices[b]
        if abs(p[0])>.40 or abs(p[2])>.88:continue
        c=tuple((p[i]+q[i])*.5 for i in range(3))
        g.path([c,(c[0]+.012*sin(k),c[1]+.006,c[2]-.013)],.0008,'Fabric',n=4,collider=False)
    # Spring ends emerge through foam along the tear; the main body stays whole.
    for j in range(2):
        pts=[]
        for k in range(19):
            angle=k*2*pi/6
            pts.append((.322+.016*cos(angle),.222+.016*sin(angle),.28+j*.15+k*.004))
        g.path(pts,.0018,'Rust',n=5,collider=False)
    _welt(g,(0,.065,0),(.88,.20,1.90),'Fabric')


def _bedding(row):
    g=Geo(row);v=row['variant'];seed=row['id']
    if v==0:
        g.cushion((0,.13,0),(.90,.25,1.90),'Fabric',sag=.045,seed=seed,segments=14)
        _welt(g,(0,.04,0),(.90,.25,1.90),'DarkWood')
        _welt(g,(0,.21,0),(.90,.25,1.90),'Fabric')
        for x in (-.25,0,.25):
            for z in (-.65,-.24,.24,.65):g.sphere((x,.255,z),(.024,.009,.024),'DarkWood',n=8,rings=4)
        for j in range(4):
            z=-.50+j*.26
            pts=[(.43+.018*cos(k*pi/3),.10+.018*sin(k*pi/3),z+k*.005) for k in range(20)]
            g.path(pts,.0018,'Rust',n=5)
    elif v==1:
        g.cushion((0,.16,0),(1.57,.30,2.0),'Fabric',sag=.014,seed=seed,segments=16)
        for yy in (.055,.265):_welt(g,(0,yy,0),(1.57,.30,2.0),'Fabric')
        # Quilting channels and side lifting handles are scaled to a spring mattress.
        for j in range(7):
            xx=(j-3)*.205
            g.path([(xx,.304-.003*cos(k*pi/3),-.88+k*.11) for k in range(17)],.0018,'Fabric',n=5)
        for zz in (-.52,.52):
            for sx in (-1,1):g.path([(sx*.768,.12,zz-.075),(sx*.792,.15,zz-.06),(sx*.792,.15,zz+.06),(sx*.768,.12,zz+.075)],.006,'Fabric',n=6)
    elif v==2:
        g.cushion((0,.10,0),(.85,.19,1.88),'GreenFabric',sag=.072,seed=seed,segments=15)
        g.cloth((-.06,.186,.12),(.76,.016,1.61),'Fabric',fold=.025,nx=13,nz=18)
        _welt(g,(0,.028,0),(.85,.19,1.88),'GreenFabric')
    elif v==3:
        _localized_torn_mattress(g,seed)
    elif v==4:
        g.cushion((0,.055,0),(.81,.105,1.84),'GreenFabric',sag=.038,seed=seed,segments=16)
        for zz in (-.65,-.32,0,.32,.65):
            g.path([(-.37,.08,zz),(-.18,.086,zz+.02),(0,.070,zz),(.18,.085,zz-.02),(.37,.08,zz)],.0024,'Fabric',n=5)
        _welt(g,(0,.04,0),(.81,.105,1.84),'Fabric')
    elif v==5:
        # The fold has separate compressed halves joined by a thick curved fabric nose.
        g.cushion((0,.10,.06),(.88,.19,.96),'RedFabric',sag=.045,seed=seed,segments=12)
        g.cushion((.025,.28,-.03),(.86,.17,.93),'RedFabric',sag=.055,seed=seed+1,segments=12)
        g.lathe((0,.19,-.47),[(0,-.43),(.10,-.43),(.10,.43),(0,.43)],'RedFabric',axis='x',n=20)
        for yy in (.04,.33):_welt(g,(0,yy,0),(.88,.19,.92),'Fabric')
    elif v==6:
        g.lathe((0,.23,0),[(0,-.42),(.205,-.42),(.23,-.35),(.235,.34),(.20,.43),(0,.43)],'GreenFabric',axis='x',n=36)
        for xx in (-.27,.27):g.ring((xx,.23,0),.237,.009,'DarkWood',axis='x',n=28)
        g.cloth((0,.445,.025),(.67,.018,.26),'Fabric',fold=.026,nx=12,nz=7)
        # End roll coils are low-poly cloth tubes, not hundreds of circles.
        for end in (-1,1):
            pts=[]
            for j in range(39):
                a=j*pi*.18;r=.018+j*.0045
                pts.append((end*.434,.23+r*sin(a),r*cos(a)))
            g.path(pts,.005,'Fabric',n=5)
    elif v==7:
        blanket_skin=len(g.meshes)
        g.cloth((0,.025,0),(1.42,.008,1.92),'RedFabric',fold=.052,nx=24,nz=28)
        # An irregular folded corner gives the blanket thickness and a human silhouette.
        g.cloth((.48,.055,.59),(.48,.008,.58),'RedFabric',fold=.032,nx=10,nz=12)
        _cloth_trim(g,blanket_skin,24,28,'RedFabric',fringe=True)
    elif v==8:
        g.cloth((0,.025,0),(1.35,.004,1.83),'Fabric',fold=.045,nx=25,nz=30)
        g.cloth((-.34,.055,-.38),(.73,.004,.58),'Fabric',fold=.032,nx=16,nz=14)
        g.cloth((.40,.048,.51),(.52,.004,.76),'Fabric',fold=.029,nx=13,nz=17)
        g.cushion((-.22,.105,-.57),(.60,.15,.44),'Fabric',sag=.025,seed=seed,segments=10)
    else:
        for j,(x,y,z,w,d) in enumerate([(-.26,.10,-.16,.60,.47),(.28,.11,-.08,.56,.45),(-.03,.29,.06,.66,.49)]):
            g.part('Pillow_%d'%j,pivot=(x,y-.07,z),kind='lift',axis=(0,1,0),amount=.18,label='Подушка')
            g.cushion((x,y,z),(w,.19,d),'Fabric' if j!=1 else 'GreenFabric',sag=.045,seed=seed+j,segments=11)
            _welt(g,(x,y-.05,z),(w,.19,d),'Fabric')
            g.static()
    return g


def _wardrobe(row):
    g=Geo(row);v=row['variant']
    w=[1.22,1.08,1.65,1.66,1.17,1.23,.53,1.44,1.14,1.82][v]
    h=[1.91,1.92,2.02,2.11,1.96,2.08,2.03,2.12,1.91,2.02][v]
    d=[.57,.55,.59,.62,.58,.56,.45,.64,.56,.58][v]
    mat=['DarkWood','Wood','DarkWood','Chipboard','Wood','WhiteLaminate','PaintedWood','DarkWood','Chipboard','Wood'][v]
    bottom=.13 if v in (0,7) else .08
    _feet(g,w,d,h=bottom,mat='DarkWood' if v!=5 else 'Plastic')
    shelves=(.53,1.64) if v==6 else (1.62,) if v in (0,1,2,4,7,8) else (.64,1.29,1.81) if v==5 else (1.68,)
    _carcass(g,w,h,d,bottom=bottom,mat=mat,shelves=shelves,
             divider=0 if v in (0,2,4,7) else None,zones=v not in (6,9))
    if v in (0,1,2,4,7,8):
        g.rod((-w*.42,1.50,-.025),(w*.42,1.50,-.025),.012,'Chrome',n=12)
        # Rail brackets terminate on the side boards.
        for sx in (-1,1):g.box((sx*(w*.5-.025),1.50,-.025),(.014,.034,.038),'Chrome',bevel=.003)
    if v==3:
        # Sliding leaves occupy separate tracks; no swinging leaf is hidden inside.
        for j in range(2):
            x=(-.25 if j==0 else .25)*w
            zz=d*.5+.015+j*.035
            door_y=h*.51+.014
            g.part('SlidingDoor_%d'%j,pivot=(x,door_y,zz),kind='slide',axis=(1 if j==0 else -1,0,0),amount=w*.43,label='Раздвижная дверь')
            g.box((x,door_y,zz),(w*.51,h-.14,.024),mat,bevel=.006)
            for sx in (-1,1):g.box((x+sx*w*.245,door_y,zz+.016),(.013,h-.14,.014),'Chrome',bevel=.002)
            _pull(g,x+(.24 if j==0 else -.24)*w,door_y,zz+.040,.20,'Chrome',vertical=True)
            g.static()
        for yy in (.09,h-.025):g.box((0,yy,d*.5+.025),(w-.03,.018,.072),'Chrome',bevel=.003)
    elif v==6:
        _drawer(g,'PencilDrawer',0,.31,d*.5,w-.045,.34,d-.06,mat)
        _door(g,'TallDoor',0,1.26,d*.5+.015,w-.045,1.47,mat,side=-1)
        g.zone('UpperPenalBay',(0,.90,-.008),(w-.08,.65,d-.09))
        g.zone('TopPenalBay',(0,1.82,-.008),(w-.08,.29,d-.09))
    elif v==9:
        # Combined wall unit: left long clothes bay, central display shelves, right drawers.
        for xx in (-.30,.34):g.box((xx,1.06,-.01),(.024,1.82,d-.04),mat,bevel=.003)
        for yy in (.60,1.05,1.50):
            g.box((.02,yy,-.015),(.60,.020,d-.04),mat,bevel=.003)
            display_height=.105 if yy>1.4 else .28
            g.zone('Display_%d'%round(yy*100),(.02,yy+.025+display_height*.5,0),(.56,display_height,d-.08))
        _door(g,'LeftWardrobe',-.604,1.06,d*.5+.014,.57,1.85,mat,side=-1,inset=True)
        for j in range(3):_drawer(g,'RightDrawer_%d'%j,.60,.22+j*.21,d*.5,.56,.20,d-.07,mat)
        _door(g,'RightUpper',.60,1.35,d*.5+.014,.56,1.23,mat,side=1,glass=True)
    else:
        count=3 if v==2 else 2
        usable=w-.04;dh=h-bottom-.025
        for j in range(count):
            x=(j-(count-1)*.5)*usable/count
            _door(g,'Door_%d'%j,x,bottom+dh*.5,d*.5+.014,usable/count-.005,dh,
                  mat,side=-1 if j%2==0 else 1,mirror=v==4 and j==0,
                  inset=v in (0,7),damaged=v==8 and j==1)
        if v==7:
            # Heavy cornice, plinth and carved-style columns distinguish the armoire.
            for yy,extra in ((h+.025,.08),(h-.025,.045),(.12,.045)):
                g.box((0,yy,0),(w+extra,.035,d+.05),mat,bevel=.010)
            for sx in (-1,1):
                g.lathe((sx*(w*.5-.04),.13,d*.5+.025),[(.025,0),(.033,.05),(.023,.10),(.023,1.63),(.034,1.69),(.025,1.74)],'Wood',n=12)
        if v==0:
            g.box((0,h+.025,0),(w+.045,.050,d+.025),mat,bevel=.009)
            _beading(g,w+.018,d+.012,.14,mat)
        if v==8:
            g.box((w*.46,.18,d*.35),(.040,.17,.020),'Plywood',bevel=.002,rot=(0,0,-7))
    return g


def _nightstand(row):
    g=Geo(row);v=row['variant']
    w=[.46,.50,.48,.48,.46,1.13,.53,.48,.48,.52][v]
    h=[.54,.62,.49,.57,.58,.46,.61,.64,.62,.57][v]
    d=[.38,.43,.38,.40,.41,.43,.43,.43,.42,.43][v]
    mat=['Wood','DarkWood','WhiteLaminate','Chipboard','PaintedWood','DarkWood','Wood','Chipboard','DarkWood','Wood'][v]
    base=.09 if v!=7 else .12
    if v==7:
        for sx in (-1,1):
            for sz in (-1,1):
                g.box((sx*w*.39,.0875,sz*d*.35),(.040,.085,.030),'Steel',bevel=.004)
                g.rod((sx*w*.39-.025,.037,sz*d*.35),(sx*w*.39+.025,.037,sz*d*.35),.027,'Rubber',n=12)
    else:_feet(g,w,d,h=base,mat='Plastic' if v==2 else 'DarkWood')
    _carcass(g,w,h,d,bottom=base,mat=mat,shelves=(h*.49,) if v in (1,4,8) else (),zones=v in (1,4,8))
    # All ten designs contain authored moving parts: no drawer-shaped static boxes.
    if v==0:
        _drawer(g,'TopDrawer',0,.43,d*.5,w-.04,.16,d-.05,mat,pull='knob')
        g.box((0,.25,0),(w-.05,.018,d-.035),mat,bevel=.003)
        g.zone('OpenBooks',(0,.17,0),(w-.08,.11,d-.07))
    elif v in (1,4,8):
        _door(g,'CabinetDoor',0,(h+base)*.5,d*.5+.012,w-.04,h-base-.035,mat,side=-1,inset=v==8)
        if v==1:
            g.box((0,h+.015,0),(w+.025,.035,d+.02),'DarkWood',bevel=.008)
            g.box((0,base+.015,d*.5+.010),(w-.03,.030,.020),'Brass',bevel=.003)
    elif v==5:
        # Television console has two side cabinets and a central open equipment bay.
        for sx in (-1,1):
            x=sx*.395
            g.box((sx*.23,.26,-.01),(.020,.30,d-.04),mat,bevel=.003)
            _door(g,'TVSide_%s'%sx,x,.275,d*.5+.012,.31,.31,mat,side=sx)
        g.box((0,.25,-.015),(.43,.018,d-.05),mat,bevel=.003)
        g.zone('TVEquipment',(0,.32,0),(.40,.12,d-.07))
    else:
        count=3 if v in (2,7) else 2
        usable=h-base-.04;dh=usable/count
        for j in range(count):
            _drawer(g,'Drawer_%d'%j,0,base+.02+dh*(j+.5),d*.5,w-.045,dh-.008,d-.06,mat,
                    missing=v==9 and j==1,pull='bar',front_mat='WhiteLaminate' if v==2 else None)
        if v==6:
            for yy in (h*.35,h*.72):
                for sx in (-1,1):g.box((sx*(w*.5-.004),yy,d*.5+.002),(.008,.021,.006),'Steel',bevel=.001)
    return g


def _dresser(row):
    g=Geo(row);v=row['variant']
    w=[.58,1.38,1.06,1.18,.66,1.08,1.12,.96,.88,1.28][v]
    h=[.91,.80,.88,.84,1.31,.96,.86,.88,.78,1.02][v]
    d=[.43,.47,.47,.44,.46,.49,.47,.45,.43,.54][v]
    mat=['Chipboard','Wood','DarkWood','WhiteLaminate','Wood','Wood','DarkWood','Chipboard','PaintedWood','DarkWood'][v]
    base=.12 if v in (2,6,9) else .07
    _feet(g,w,d,h=base,mat='DarkWood' if v!=3 else 'Plastic')
    _carcass(g,w,h,d,bottom=base,mat=mat,zones=False)
    if v==1:
        # Two stacks of broad drawers share a structural centre divider.
        g.box((0,(h+base)*.5,0),(.025,h-base-.03,d-.035),mat,bevel=.003)
        for sx in (-1,1):
            for j in range(3):_drawer(g,'Wide_%s_%d'%(sx,j),sx*w*.25,base+.035+(h-base-.06)*(j+.5)/3,d*.5,w*.5-.04,(h-base-.06)/3-.010,d-.06,mat)
    elif v==2:
        # Soviet chest has two shallow upper drawers and three full-width lower trays.
        bh=(h-base-.07)*.75
        for j in range(3):_drawer(g,'Lower_%d'%j,0,base+.025+bh*(j+.5)/3,d*.5,w-.04,bh/3-.008,d-.06,mat,pull='knob')
        ty=base+.035+bh+(h-base-.07-bh)*.5
        for sx in (-1,1):_drawer(g,'Upper_%s'%sx,sx*w*.25,ty,d*.5,w*.5-.03,(h-base-.07-bh)-.008,d-.06,mat,pull='knob')
        _beading(g,w+.025,d+.012,h+.004,mat)
    elif v==9:
        # Heavy antique chest: serpentine-style profiles, round feet, split upper row.
        for j in range(3):_drawer(g,'Antique_%d'%j,0,.22+j*.22,d*.5,w-.095,.208,d-.085,mat,pull='knob')
        for sx in (-1,1):
            _drawer(g,'AntiqueTop_%s'%sx,sx*w*.235,.885,d*.5,w*.46,.160,d-.085,mat,pull='knob')
            g.lathe((sx*w*.43,.01,d*.34),[(.045,0),(.055,.035),(.041,.08),(.035,.115)],mat,n=14)
            g.lathe((sx*w*.43,.01,-d*.34),[(.045,0),(.055,.035),(.041,.08),(.035,.115)],mat,n=14)
        for yy,ww,dd in ((.125,w+.02,d+.02),(h+.015,w+.075,d+.065),(h-.018,w+.025,d+.025)):
            g.box((0,yy,0),(ww,.030,dd),mat,bevel=.012)
        for sx in (-1,1):g.rod((sx*(w*.5-.015),.14,d*.5+.025),(sx*(w*.5-.015),h-.025,d*.5+.025),.023,'Wood',n=12)
    else:
        count=6 if v==4 else 4 if v in (0,5,7) else 3
        dh=(h-base-.055)/count
        for j in range(count):
            fm='WhiteLaminate' if v==3 else ('PaintedWood' if v==8 and j%2==0 else mat)
            _drawer(g,'Drawer_%d'%j,0,base+.026+dh*(j+.5),d*.5,w-.05,dh-.009,d-.065,mat,
                    missing=v==7 and j in (1,3),pull='knob' if v in (5,8) else 'bar',front_mat=fm)
        if v==5:
            # Solid timber chest has a projecting crown and side panel bead frames.
            g.box((0,h+.016,0),(w+.055,.040,d+.040),'Wood',bevel=.012)
            for sx in (-1,1):
                for zz in (-d*.36,d*.36):g.box((sx*(w*.5+.002),(h+base)*.5,zz),(.009,h-base-.11,.024),'DarkWood',bevel=.003)
    if v==6:
        # Mirror is supported by two rear uprights; it is separate from all drawers.
        for sx in (-1,1):g.box((sx*w*.36,h+.36,-d*.42),(.038,.81,.038),'DarkWood',bevel=.006)
        mw,mh=w*.82,.73
        g.box((0,h+.46,-d*.42),(mw-.055,mh-.055,.011),'Mirror',bevel=.002)
        for sx in (-1,1):g.box((sx*(mw*.5-.015),h+.46,-d*.42),(.030,mh,.036),'DarkWood',bevel=.006)
        for sy in (-1,1):g.box((0,h+.46+sy*(mh*.5-.015),-d*.42),(mw-.060,.030,.036),'DarkWood',bevel=.006)
    return g


def _professional_finish(g,row):
    """Category-specific construction that remains meaningful with doors open."""
    aid=row['id'];v=row['variant'];g.static()
    if aid<=10:
        widths=[1.94,2.12,2.42,2.12,1.48,1.97,2.24,1.88,2.55,2.08]
        depths=[.86,.92,1.46,.91,.78,.86,.94,.85,1.12,.93]
        mats=['RedFabric','Fabric','GreenFabric','Leather','Fabric','GreenFabric','RedFabric','Fabric','Fabric','RedFabric']
        w,d,mat=widths[v],depths[v],mats[v]
        if v not in(8,9):
            front=.375 if v in(0,1,5,7)else -.03+(min(d,.90)-.10)*.5+.010
            # The plinth has a padded, sewn facing; the real chest/frame remains
            # behind it and is exposed when its mechanism is operated.
            g.cushion((0,.245,front),(w-.30,.205,.030),mat,sag=0,seed=aid+311,segments=8)
            for yy in(.175,.315):g.rod((-(w-.38)*.5,yy,front+.015),((w-.38)*.5,yy,front+.015),.0015,mat,n=5,collider=False)
            if v==2:g.cushion((-.73,.24,1.091),(.72,.205,.028),mat,sag=0,seed=aid,segments=8)
        elif v==8:
            for j in range(3):
                g.cushion(((j-1)*.82,.23,.448),(.72,.21,.026),mat,sag=0,seed=aid+j,segments=8)
            g.cushion((.82,.23,1.067),(.72,.21,.026),mat,sag=0,seed=aid+3,segments=8)
        if v in(0,1):
            for sx in(-1,1):
                x=sx*w*.30
                g.rod((x,.33,-.343),(x,.44,-.40),.006,'Steel',n=8,collider=False)
                _fastener(g,(x,.33,-.355),normal=(0,0,-1),r=.004)
                g.part('FoldBack')
                g.rod((x-.022,.44,-.40),(x+.022,.44,-.40),.006,'Steel',n=10,collider=False)
                g.ring((x,.44,-.40),.012,.002,'Steel',axis='x',n=12)
                g.static()
    elif aid<=20:
        if v==2:
            g.part('RockingFrame')
            g.rod((-.25,.99,-.34),(.25,.99,-.34),.022,'Wood',n=12)
            for sx in(-1,1):_fastener(g,(sx*.29,.715,-.24),normal=(0,1,0),r=.004)
            g.static()
        elif v in(3,4):
            g.part('SwivelSeat')
            g.box((0,.425,0),(.38,.018,.38),'Plastic',bevel=.007,collider=False)
            g.lathe((0,.386,0),[(.034,0),(.047,.012),(.047,.038),(.034,.045)],'Steel',axis='y',n=16)
            for sx in(-1,1):_fastener(g,(sx*.14,.414,.13),normal=(0,-1,0),r=.004)
            g.static()
        elif v!=8:
            w=.58 if v==9 else [.76,.90,.66,.64,.65,.72,.76,1.01,.82,.58][v]
            sy=.36 if v==9 else .46
            mat=['GreenFabric','Leather','Wood','Fabric','Fabric','RedFabric','Fabric','RedFabric','GreenFabric','RedFabric'][v]
            g.cushion((0,sy-.165,.335),(w-.14,.13,.026),mat,sag=0,seed=aid,segments=8)
    elif aid<=30:
        if v in(0,2,3,4,8,9):
            scale=.73 if v==8 else 1
            sy=.32 if v==8 else .455
            base=sy-.116 if v==4 else sy-.045
            material='Steel'if v==3 else'PaintedWood'if v in(0,8)else'Wood'
            if v==4:g.box((0,sy-.085,0),(.40,.022,.38),'Plywood',bevel=.004)
            for sz in(-1,1):g.box((0,base,sz*.145*scale),(.34*scale,.060,.022),material,bevel=.003)
            for sx in(-1,1):g.box((sx*.16*scale,base,0),(.022,.060,.29*scale),material,bevel=.003)
            for sx in(-1,1):
                for sz in(-1,1):
                    _gusset(g,sx*.15*scale,base,sz*.135*scale,sx,sz,arm=.045*scale,height=.047,mat='Wood'if v!=3 else'Steel')
                    _fastener(g,(sx*.167*scale,base,sz*.13*scale),normal=(sx,0,0),r=.0033)
        if v==1:
            g.lathe((0,.412,0),[(.115,0),(.145,.008),(.145,.018),(.115,.022)],'Wood',axis='y',n=24)
    elif aid<=40:
        dims={1:(1.18,.78),3:(.76,.76),4:(1.30,.78),5:(1.55,.83),7:(1.18,.72),8:(1.12,.76),9:(1.36,.77)}
        if v in dims:
            w,d=dims[v]
            for sx in(-1,1):
                for sz in(-1,1):
                    _gusset(g,sx*w*.395,.695,sz*d*.36,sx,sz,arm=.075,height=.080,
                            mat='Steel'if v==7 else'Wood')
                    _fastener(g,(sx*w*.395,.695,sz*(d*.36+.017)),normal=(0,0,sz),r=.004)
        if v==0:
            for side,sx in(('L',-1),('R',1)):
                g.part('DropLeaf_'+side)
                g.rod((sx*.145,.73,-.34),(sx*.145,.73,.34),.004,'Steel',n=8,collider=False)
                for zz in(-.27,.27):
                    g.box((sx*.169,.733,zz),(.035,.004,.044),'Steel',bevel=.001,collider=False)
                g.static()
    elif aid<=50:
        if v in(4,6):
            w,d,y=(1.0,.53,.43)if v==4 else(.94,.52,.34)
            for sx in(-1,1):
                for sz in(-1,1):_gusset(g,sx*w*.36,y-.053,sz*d*.31,sx,sz,arm=.06,height=.065)
        elif v==7:
            for sx in(-1,1):
                for zz in(-.18,.19):
                    g.box((sx*.38,.70,zz),(.050,.065,.040),'Steel',bevel=.004,collider=False)
                    _fastener(g,(sx*.40,.70,zz),normal=(sx,0,0),r=.004)
    elif aid<=60:
        w=[.90,.96,1.62,1.67,1.54,.77,.94,.94,.78,.88][v]
        d=[2.01,2.06,2.10,2.16,2.04,1.60,2.04,2.04,1.91,1.98][v]
        if v in(0,6,7,9):
            deck=.35
            for sx in(-1,1):
                for sz in(-1,1):
                    z=sz*d*.46
                    g.box((sx*w*.46,deck-.015,z+sz*.023),(.056,.080,.006),'Steel',bevel=.002,collider=False)
                    for yy in(deck-.036,deck+.008):
                        _fastener(g,(sx*w*.46,yy,z+sz*.028),normal=(0,0,sz),r=.0042)
        elif v in(1,2,4):
            hh=.76 if v==2 else .65;yy=.57;zz=-d*.48+.023
            g.path([(-w*.5+.05,yy-hh*.5+.045,zz),(w*.5-.05,yy-hh*.5+.045,zz),
                    (w*.5-.05,yy+hh*.5-.045,zz),(-w*.5+.05,yy+hh*.5-.045,zz),
                    (-w*.5+.05,yy-hh*.5+.045,zz)],.003,'Wood',n=6,collider=False)
            for sx in(-1,1):
                for sz in(-1,1):
                    _fastener(g,(sx*(w*.5-.045),.31,sz*(d*.5-.003)),normal=(0,0,sz),r=.005)
    g.static()


def build(row):
    """Return the model recipe for exact required IDs 001--100."""
    asset_id=int(row['id'])
    if not 1 <= asset_id <= 100:
        raise ValueError('models_01_10 only owns asset IDs 001--100')
    local=dict(row)
    local['variant']=(asset_id-1)%10
    category=(asset_id-1)//10
    g=(_sofa,_armchair,_chair,_table,_desk,_bed,_bedding,
       _wardrobe,_nightstand,_dresser)[category](local)
    _professional_finish(g,local)
    # Optional wear-controlled fragments supplement the authored damaged structure;
    # switching these off never repairs a frame or hides a required moving panel.
    if asset_id in (8,10,17,30,64,79,98):
        g.damage('WearFragments_%03d'%asset_id)
        if asset_id in (8,10,17,64):
            for j in range(3):
                first_mesh=len(g.meshes);first_collider=len(g.colliders)
                x=.11+j*.055;z=.30+j*.035;h=.025+j*.003
                vertices=[(x-.024,0,z-.017),(x+.020,0,z-.020),(x+.026,0,z+.014),(x-.018,0,z+.021),
                          (x-.016,h*.87,z-.013),(x+.013,h,z-.012),(x+.017,h*.71,z+.010),(x-.013,h*.82,z+.015)]
                g.add(vertices,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],'Foam',False)
                # Each crumb rests on the floor independently of the furniture's
                # pivot; its tiny asymmetric bottom must not float above it.
                bottom=min(v[1]for m in g.meshes[first_mesh:]for v in m['v'])
                for m in g.meshes[first_mesh:]:m['v']=[(v[0],v[1]-bottom,v[2])for v in m['v']]
                for c in g.colliders[first_collider:]:
                    for key in ('center','a','b'):
                        if key in c:c[key][1]-=bottom
        else:
            for j in range(3):
                g.box((.07+j*.045,.0045,.22+j*.027),(.083,.009,.018),
                      'Plywood',bevel=.001,rot=(0,21*j-14,0),collider=False)
        g.static()
    # Curved foot caps and rumpled bedding can otherwise extend a few millimetres
    # below the authoring plane. Preserve all relative pivots/zones while placing
    # the actual lowest visible vertex exactly on the root's floor plane.
    minimum=min(v[1] for m in g.meshes if g.parts[m['part']]['kind']!='damage' for v in m['v'])
    delta=-minimum
    if abs(delta)>1e-7:
        for mesh in g.meshes:
            mesh['v']=[(v[0],v[1]+delta,v[2])for v in mesh['v']]
        for name,part in g.parts.items():
            if name!='Body':part['pivot'][1]+=delta
        for collider in g.colliders:
            for key in ('center','a','b'):
                if key in collider:collider[key][1]+=delta
        for zone in g.zones:zone['center'][1]+=delta
        for light in g.lights:light['position'][1]+=delta
    # Optional loose crumbs cannot determine the furniture's floor plane. Place
    # each independent fragment after the main construction has been grounded.
    for mesh in g.meshes:
        if g.parts[mesh['part']]['kind']!='damage':continue
        bottom=min(v[1]for v in mesh['v'])
        mesh['v']=[(v[0],v[1]-bottom,v[2])for v in mesh['v']]
    return g


def _armchair(row):
    g=Geo(row);v=row['variant'];seed=row['id']
    if v==8:return _chair_bed(row)
    mat=['GreenFabric','Leather','Wood','Fabric','Fabric','RedFabric','Fabric','RedFabric','GreenFabric','RedFabric'][v]
    w=.58 if v==9 else [.76,.90,.66,.64,.65,.72,.76,1.01,.82,.58][v]
    if v in (3,4):
        # Star base and rotating upper body, wheels have real forks and axles.
        g.rod((0,.13,0),(0,.46,0),.039,'Chrome',n=16)
        for j in range(5):
            a=j*2*pi/5
            q=(.31*cos(a),.115,.31*sin(a))
            g.rod((0,.15,0),q,.025,'Plastic' if v==3 else 'Chrome',n=10)
            g.box((q[0],.070,q[2]),(.060,.070,.047),'Plastic',bevel=.006)
            for s in (-1,1):g.sphere((q[0]+s*.032,.046,q[2]),(.025,.075,.075),'Rubber',n=10,rings=5)
        g.part('SwivelSeat',pivot=(0,.45,0),kind='rotate',axis=(0,1,0),amount=80,label='Повернуть кресло')
        g.cushion((0,.49,.01),(.54,.12,.51),mat,sag=.028,segments=10)
        g.rod((0,.45,-.19),(0,.83,-.23),.026,'Steel',n=10)
        if v==3:
            g.cushion((0,.84,-.22),(.46,.44,.12),mat,sag=.02,segments=10)
        else:
            g.box((0,.83,-.26),(.44,.44,.028),'Plastic',bevel=.03)
            for j in range(5):g.rod((-.20,.66+j*.067,-.235),(.20,.66+j*.067,-.235),.006,'Rubber',n=6)
        for sx in (-1,1):
            g.rod((sx*.24,.45,-.03),(sx*.29,.68,-.02),.018,'Plastic',n=8)
            g.box((sx*.29,.69,-.01),(.065,.040,.31),'Plastic',bevel=.014)
        g.static();return g
    if v==2:
        # Both curved runners and the complete upper frame rock as one assembly.
        # A regular circular centreline keeps every tube vertex inside a common
        # cylinder around the hinge: rotation therefore cannot push a runner
        # through the floor. Phase the arc so a furthest tube vertex is down at
        # rest instead of placing a chord midpoint below the intended circle.
        radius=.96;runner_tube=.023;segments=30
        step=(54*pi/180)/segments
        radial_tube=runner_tube*cos(pi/10)
        phase=-atan2(radial_tube*sin(step*.5),radius+radial_tube*cos(step*.5))
        centre_y=(radius*radius+radial_tube*radial_tube+
                  2*radius*radial_tube*cos(step*.5))**.5
        g.part('RockingFrame',pivot=(0,centre_y,0),kind='hinge',axis=(1,0,0),amount=10,label='Качнуть кресло')
        for sx in (-1,1):
            pts=[]
            for j in range(segments+1):
                angle=(j-segments*.5)*step+phase
                pts.append((sx*.285,centre_y-radius*cos(angle),radius*sin(angle)))
            g.path(pts,runner_tube,'Wood',n=10)
        for sx in (-1,1):
            for sz in (-1,1):g.rod((sx*.285,.08,sz*.29),(sx*.25,.43,sz*.23),.023,'Wood',n=10)
        for j in range(7):g.box(((j-3)*.075,.43,.01),(.062,.032,.53),'Wood',bevel=.009)
        for j in range(6):
            xx=(j-2.5)*.083
            g.rod((xx,.45,-.23),(xx,.99,-.34),.020,'Wood',n=10)
        for sx in (-1,1):
            g.rod((sx*.27,.45,.17),(sx*.29,.68,.17),.019,'Wood',n=10)
            g.rod((sx*.29,.68,.20),(sx*.29,.71,-.30),.026,'Wood',n=10)
        g.cushion((0,.49,.01),(.48,.09,.45),'Fabric',sag=.02,segments=8)
        g.static();return g
    sy=.36 if v==9 else .46
    _feet(g,w,.70,h=.14 if v==8 else sy-.25)
    if v!=8:
        g.box((0,sy-.16,0),(w-.08,.18,.65),'DarkWood',bevel=.015)
    if v==8:
        _carcass(g,w-.12,sy-.08,.62,bottom=.14,mat='Wood',top=False)
        g.part('BedExtension',pivot=(0,sy-.08,.29),kind='slide',axis=(0,0,1),amount=.64,label='Разложить кресло')
        g.box((0,sy-.10,.23),(w-.20,.06,.60),'Plywood',bevel=.004)
        g.cushion((0,sy,.25),(w-.18,.16,.59),mat,sag=.025,segments=9)
        for sx in (-1,1):g.rod((sx*(w*.5-.14),.08,.48),(sx*(w*.5-.14),sy-.10,.48),.020,'Steel',n=10)
        g.static()
    _seat(g,0,sy,0,w-.19,.57,mat,sag=.05 if v==6 else .025,name='LooseSeat',seed=seed)
    bh=.44 if v==9 else (.72 if v==7 else .58)
    back_skin=len(g.meshes)
    g.cushion((0,sy+bh*.50,-.26),(w-.14,bh,.17),mat,sag=.03,segments=10)
    for sx in (-1,1):
        if v==5:
            for zz in (-.23,.22):g.rod((sx*(w*.5-.05),.20,zz),(sx*(w*.5-.05),sy+.20,zz),.023,'Wood',n=10)
            g.box((sx*(w*.5-.05),sy+.23,0),(.10,.040,.65),'Wood',bevel=.018)
        elif v==6 and sx==1:
            g.cushion((sx*(w*.5-.06),sy+.12,0),(.14,.35,.60),'Foam',sag=.04,segments=8)
            g.cloth((sx*(w*.5-.06),sy+.28,-.15),(.17,.020,.31),mat,fold=.025,nx=6,nz=6)
        else:
            g.cushion((sx*(w*.5-.06),sy+.12,0),(.14,.35,.60),mat,sag=.02,segments=9)
    if v in (1,7):
        for xx in (-.17,0,.17):
            for yy in (sy+.18,sy+.38,sy+.56):
                _fabric_button(g,xx,yy,back_skin,mat)
    return g


def _chair(row):
    g=Geo(row);v=row['variant']
    if v==0:
        g.box((0,.455,0),(.34,.036,.34),'PaintedWood',bevel=.012)
        _legs(g,.25,.25,0,.435,'PaintedWood',r=.024,splay=.025)
        for zz in (-.125,.125):g.rod((-.13,.17,zz),(.13,.17,zz),.012,'PaintedWood',n=8)
        for xx in (-.125,.125):g.rod((xx,.19,-.13),(xx,.19,.13),.012,'PaintedWood',n=8)
    elif v==1:
        g.lathe((0,.43,0),[(0,0),(.18,0),(.18,.034),(0,.034)],'Wood',n=28,collider=True)
        for j in range(3):
            a=j*2*pi/3
            _profiled_timber_leg(g,(.11*cos(a),.43,.11*sin(a)),(.16*cos(a),0,.16*sin(a)),.025,'Wood',n=10)
        g.ring((0,.16,0),.126,.009,'Wood',axis='y',n=24)
    elif v==5:
        # Stackable moulded patio chair with open slatted back and flared legs.
        _curve_shell(g,(0,.43,.01),(.43,.009,.41),'WhitePlastic')
        for sx in (-1,1):
            for sz in (-1,1):
                g.box((sx*.18,.228,sz*.16),(.035,.436,.045),'WhitePlastic',bevel=.006,rot=(0,0,sx*5))
        for sx in (-1,1):g.path([(sx*.195,.44,-.16),(sx*.218,.67,-.235),(sx*.20,.89,-.27)],.021,'WhitePlastic',n=10)
        g.path([(-.21,.872,-.255),(-.14,.887,-.267),(0,.891,-.272),
                (.14,.887,-.267),(.21,.872,-.255)],.022,'WhitePlastic',n=10)
        for xx in (-.13,-.065,0,.065,.13):
            g.path([(xx,.445,-.17),(xx,.66,-.226),(xx,.875,-.269)],.014,'WhitePlastic',n=8)
        for sx in (-1,1):g.path([(sx*.20,.44,.15),(sx*.235,.64,.10),(sx*.23,.65,-.22)],.018,'WhitePlastic',n=10)
        for sx in(-1,1):g.box((sx*.235,.659,-.045),(.055,.018,.31),'WhitePlastic',bevel=.007)
    elif v==6:
        for sx in (-1,1):
            g.path([(sx*.22,0,-.22),(sx*.22,.45,.18),(sx*.22,.84,-.16)],.014,'Steel',n=10)
            # Side rails carry the seat hinge without entering the folding seat.
            g.rod((sx*.22,.43,.162),(sx*.22,.43,-.16),.012,'Steel',n=10)
            g.rod((sx*.22,.81,-.134),(sx*.22,.81,-.205),.010,'Steel',n=10)
        g.part('FoldingRearFrame',pivot=(0,.43,.015),kind='hinge',axis=(1,0,0),amount=-56,label='Сложить стул')
        for sx in (-1,1):g.rod((sx*.17,0,.25),(sx*.17,.46,-.16),.014,'Steel',n=10)
        g.rod((-.17,.035,.23),(.17,.035,.23),.012,'Steel',n=10)
        g.static()
        g.part('FoldingSeat',pivot=(0,.43,-.16),kind='hinge',axis=(1,0,0),amount=-73,label='Сиденье')
        g.box((0,.455,.015),(.40,.035,.37),'Wood',bevel=.008)
        g.static();g.box((0,.77,-.205),(.44,.19,.030),'Wood',bevel=.013)
        g.rod((-.22,.43,-.16),(.22,.43,-.16),.006,'Steel',n=10,collider=False)
        for sx in (-1,1):g.sphere((sx*.22,.43,-.16),(.018,.018,.018),'Chrome',n=8,rings=5)
    elif v==7:
        g.lathe((0,.73,0),[(0,0),(.19,0),(.19,.045),(0,.045)],'DarkWood',n=28,collider=True)
        for sx in (-1,1):
            for sz in (-1,1):g.rod((sx*.12,.73,sz*.12),(sx*.19,0,sz*.19),.020,'Steel',n=10)
        g.ring((0,.26,0),.205,.012,'Chrome',axis='y',n=28)
        for sx in (-1,1):g.rod((sx*.14,.75,-.13),(sx*.15,.99,-.16),.015,'Steel',n=10)
        g.cushion((0,.99,-.16),(.34,.19,.060),'Leather',sag=.01,segments=8)
    else:
        child=v==8;scale=.73 if child else 1
        sy=.32 if child else .455
        mat='Steel' if v==3 else ('PaintedWood' if child else 'Wood')
        _legs(g,.32*scale,.30*scale,0,sy-.007,mat,r=.020 if v==3 else .025,splay=.02)
        if v==4:_seat(g,0,sy,.01,.44,.42,'RedFabric',sag=.025,seed=row['id'])
        else:
            if v==9:
                for xx in (-.14,-.045,.052):g.box((xx,.46,0),(.08,.032,.40),'Wood',bevel=.006)
                g.box((.139,.444,.026),(.067,.030,.32),'Plywood',bevel=.004,rot=(0,4,-5))
            else:g.box((0,sy,0),(.43*scale,.035,.41*scale),'Wood' if v!=3 else 'Plywood',bevel=.011)
        for sx in (-1,1):g.rod((sx*.17*scale,sy-.04,-.16*scale),(sx*.18*scale,sy+.43*scale,-.21*scale),.018 if v==3 else .023,mat,n=10)
        if v==2:
            for j in range(3):g.box((0,sy+.16+j*.10,-.185-j*.008),(.34,.053,.026),'Wood',bevel=.008)
        elif v==4:g.cushion((0,.77,-.21),(.37,.29,.090),'RedFabric',sag=.01,segments=8)
        elif v==9:
            g.box((-.10,.79,-.20),(.18,.13,.030),'Wood',bevel=.006,rot=(0,0,-3))
            g.rod((.08,.455,-.17),(.08,.77,-.20),.012,'Wood',n=8)
        else:g.box((0,sy+.33*scale,-.20*scale),(.36*scale,.18*scale,.035),'Plywood' if v==3 else 'PaintedWood' if child else 'Wood',bevel=.015)
        for zz in (-.15*scale,.15*scale):g.rod((-.16*scale,.17,zz),(.16*scale,.17,zz),.011,mat,n=8)
    return g


def _table(row):
    g=Geo(row);v=row['variant']
    if v==0:
        # A Soviet gateleg book table has two drop leaves and independent swing gates.
        g.box((0,.747,0),(.28,.038,.80),'DarkWood',bevel=.008)
        for sx in (-1,1):
            g.box((sx*.105,.38,0),(.025,.72,.65),'DarkWood',bevel=.004)
            g.part('DropLeaf_%s'%('L' if sx<0 else 'R'),pivot=(sx*.145,.73,0),kind='hinge',axis=(0,0,1),amount=-sx*89,label='Крыло стола')
            g.box((sx*.47,.747,0),(.64,.038,.80),'DarkWood',bevel=.008)
            g.static()
            g.part('GateLeg_%s'%('L' if sx<0 else 'R'),pivot=(sx*.15,.36,-.27),kind='hinge',axis=(0,1,0),amount=-sx*88,label='Опора стола')
            for xx in (sx*.17,sx*.61):g.box((xx,.3645,-.27),(.040,.729,.040),'DarkWood',bevel=.005)
            for yy in (.16,.64):g.box((sx*.39,yy,-.27),(.44,.035,.035),'DarkWood',bevel=.004)
            g.static()
    elif v==2:
        g.lathe((0,.73,0),[(0,0),(.49,0),(.50,.022),(.49,.045),(0,.045)],'Wood',n=48,collider=True)
        g.rod((0,.11,0),(0,.73,0),.055,'DarkWood',n=18);g.meshes.pop()
        g.lathe((0,.11,0),[(.085,0),(.092,.045),(.065,.11),(.059,.42),(.075,.56),(.092,.62)],'DarkWood',axis='y',n=18)
        for j in range(4):
            a=j*pi*.5
            g.path([(0,.18,0),(.20*cos(a),.065,.20*sin(a)),(.40*cos(a),.036,.40*sin(a))],.044,'DarkWood',n=10)
    elif v==4:
        _legs(g,1.15,.63,0,.742,'Wood',r=.028,splay=.01)
        for sx in (-1,1):
            g.part('SlidingTop_%s'%sx,pivot=(sx*.32,.745,0),kind='slide',axis=(sx,0,0),amount=.27,label='Раздвинуть стол')
            g.box((sx*.325,.751,0),(.645,.040,.78),'Wood',bevel=.009)
            g.static()
        # A separate stored leaf lifts into place, accessible below the split top.
        g.part('ExtensionLeaf',pivot=(0,.64,0),kind='lift',axis=(0,1,0),amount=.121,label='Вкладка стола')
        g.box((0,.635,0),(.53,.030,.72),'Wood',bevel=.006)
        g.static()
        # The insert rises between the guides, never through them. Four brackets
        # connect the guides to the undersides of both sliding top halves.
        for zz in (-.378,.378):
            g.box((0,.70,zz),(1.30,.025,.022),'Steel',bevel=.003)
            for xx in (-.42,.42):
                g.box((xx,.724,zz),(.035,.032,.022),'Steel',bevel=.002)
    else:
        w=[0,1.18,0,.76,0,1.55,1.42,1.18,1.12,1.36][v]
        d=[0,.78,0,.76,0,.83,.83,.72,.76,.77][v]
        mat='Glass' if v==6 else 'Wood'
        if v==9:
            for j in range(5):
                xx=(j-2)*.267
                length=d if j!=3 else d*.68
                g.box((xx,.752,-.10 if j==3 else 0),(.255,.038,length),'Wood',bevel=.007,
                      rot=(0,2 if j==3 else 0,-1 if j==0 else 0))
            g.box((.44,.745,.25),(.18,.023,.20),'Plywood',bevel=.004,rot=(0,13,-4))
        else:g.box((0,.755,0),(w,.045 if v!=6 else .016,d),mat,bevel=.010 if v!=6 else .004)
        if v==5:
            for sx in (-1,1):
                for sz in (-1,1):g.box((sx*w*.40,.373,sz*d*.36),(.085,.746,.085),'DarkWood',bevel=.012)
            for zz in (-d*.36,d*.36):g.box((0,.65,zz),(w*.83,.11,.030),'DarkWood',bevel=.005)
            g.box((0,.17,0),(w*.84,.060,.050),'Wood',bevel=.006)
        elif v==7:
            for sx in (-1,1):
                g.path([(sx*w*.40,0,-d*.38),(sx*w*.40,.725,-d*.38),(sx*w*.40,.725,d*.38),(sx*w*.40,0,d*.38)],.021,'Chrome',n=12)
            g.rod((-w*.40,.65,0),(w*.40,.65,0),.015,'Steel',n=10)
        elif v==6:
            for sx in (-1,1):
                for sz in (-1,1):
                    g.rod((sx*w*.35,.01,sz*d*.30),(sx*w*.35,.749,sz*d*.30),.024,'Chrome',n=12)
                    g.rod((sx*w*.35,.73,sz*d*.30),(sx*w*.35,.752,sz*d*.30),.055,'Rubber',n=16)
        else:
            _legs(g,w*.79,d*.72,0,.745,'PaintedWood' if v==8 else 'Wood',r=.026,splay=.015)
            for zz in (-d*.36,d*.36):g.box((0,.68,zz),(w*.80,.090,.028),'Wood',bevel=.004)
            for xx in (-w*.395,w*.395):g.box((xx,.68,0),(.028,.090,d*.72),'Wood',bevel=.004)
        if v==8:
            g.cloth((0,.780,0),(w+.018,.004,d+.025),'Plastic',fold=.003,nx=14,nz=12)
            _oilcloth_skirt(g,w,d)
    return g


def _desk(row):
    g=Geo(row);v=row['variant'];h=.75
    if v in (4,5,6):
        if v==4:
            w,d=1.00,.53
            g.box((0,.43,0),(w,.030,d),'Chipboard',bevel=.005)
            g.box((0,.16,0),(.86,.022,.41),'Chipboard',bevel=.004)
            for sx in (-1,1):g.box((sx*.39,.285,0),(.024,.27,.42),'Chipboard',bevel=.003)
            _feet(g,w,d,h=.15,mat='Plastic')
        elif v==5:
            g.lathe((0,.42,0),[(0,0),(.48,0),(.48,.012),(0,.012)],'Glass',axis='y',n=44,collider=True)
            g.lathe((0,.13,0),[(0,0),(.37,0),(.37,.010),(0,.010)],'Glass',axis='y',n=36,collider=True)
            for j in range(3):
                a=j*2*pi/3
                g.rod((.32*cos(a),.01,.32*sin(a)),(.32*cos(a),.42,.32*sin(a)),.018,'Chrome',n=12)
        else:
            g.box((0,.34,0),(.94,.050,.52),'Wood',bevel=.014)
            _legs(g,.70,.32,0,.326,'DarkWood',r=.029,splay=.03)
            g.box((0,.16,0),(.76,.025,.26),'Wood',bevel=.006)
        return g
    if v==2:
        # L desk from two joined but non-overlapping tops and an inner corner support.
        g.box((-.33,h,0),(1.24,.035,.64),'WhiteLaminate',bevel=.006)
        g.box((.62,h,.36),(.64,.035,1.36),'WhiteLaminate',bevel=.006)
        for x,z in [(-.82,-.23),(-.82,.23),(.82,-.23),(.82,.91),(.38,.91)]:
            g.rod((x,0,z),(x,h-.012,z),.025,'Steel',n=12)
        g.box((.62,.43,.85),(.53,.020,.16),'WhiteLaminate',bevel=.004)
        _drawer(g,'CornerDrawer',.62,.65,.99,.54,.13,.43,'WhiteLaminate',travel=.27)
        return g
    w=[1.18,1.18,0,1.14,0,0,0,.98,1.30,1.42][v]
    d=.62 if v!=7 else .55
    mat='DarkWood' if v in (0,9) else 'Wood' if v in (7,8) else 'Chipboard'
    g.box((0,h,0),(w,.040,d),mat,bevel=.008)
    if v==3:
        for sx in (-1,1):g.box((sx*(w*.5-.04),.37,-.015),(.026,.71,d-.06),mat,bevel=.004)
        g.box((0,.39,-.27),(w-.08,.24,.020),mat,bevel=.003)
        g.part('KeyboardTray',pivot=(0,.66,.19),kind='slide',axis=(0,0,1),amount=.25,label='Полка клавиатуры')
        g.box((0,.66,.025),(.76,.018,.36),mat,bevel=.005)
        g.static()
        for sx in (-1,1):g.box((sx*.40,.66,-.045),(.015,.020,.42),'Steel',bevel=.002)
    elif v==1:
        # Computer desk with tower alcove, monitor shelf and accessible drawer.
        # A dedicated tower alcove occupies the right-hand side, leaving the knee bay open.
        g.box((-.51,.37,0),(.028,.71,.54),mat,bevel=.004)
        g.box((.50,.37,0),(.028,.71,.54),mat,bevel=.004)
        g.box((.33,.10,0),(.31,.028,.51),mat,bevel=.004)
        g.box((.17,.37,-.015),(.020,.55,.54),mat,bevel=.003)
        g.box((.26,.91,-.17),(.60,.025,.26),mat,bevel=.005)
        for sx in (-1,1):g.box((.26+sx*.25,.83,-.17),(.022,.13,.20),mat,bevel=.003)
        g.part('KeyboardTray',pivot=(-.24,.66,.19),kind='slide',axis=(0,0,1),amount=.27,label='Клавиатурная полка')
        g.box((-.24,.66,.02),(.64,.018,.36),mat,bevel=.004)
        g.static()
    elif v==7:
        for sx in (-1,1):
            g.path([(sx*.38,0,.19),(sx*.38,.69,.19),(sx*.38,.69,-.18),(sx*.38,0,-.18)],.018,'Steel',n=10)
        g.box((0,.60,-.23),(.79,.13,.025),'Wood',bevel=.004)
        g.box((0,.62,.04),(.73,.016,.35),'Plywood',bevel=.003)
        g.zone('SchoolBooks',(0,.665,.02),(.69,.07,.31))
    else:
        # Pedestals retain open cavities and working drawers, with a clear knee bay.
        sides=(-1,1) if v in (0,9) else (1,)
        cw=.32 if v!=9 else .38
        for sx in sides:
            x=sx*(w*.5-cw*.5-.035)
            # Board positions are offset explicitly so every front maps to a real void.
            for s2 in (-1,1):g.box((x+s2*(cw*.5-.012),.40,-.015),(.024,.66,d-.06),mat,bevel=.004)
            g.box((x,.075,-.015),(cw-.04,.024,d-.06),mat,bevel=.003)
            g.box((x,.40,-d*.5+.024),(cw-.04,.64,.012),'Plywood',bevel=.002)
            count=4 if v==9 else 3
            dh=.60/count
            for j in range(count):
                yy=.09+dh*(j+.5)
                _drawer(g,'Pedestal_%s_Drawer_%d'%(sx,j),x,yy,d*.5,cw-.025,dh-.010,d-.07,mat,pull='knob' if v==9 else 'bar')
        if len(sides)==1:
            g.box((-w*.5+.07,.38,0),(.050,.72,d-.07),mat,bevel=.005)
        g.box((0,.47,-d*.5+.025),(w*.63,.22,.020),mat,bevel=.004)
        if v==8:
            # A narrow lift-up stationery bay under the writing surface.
            _drawer(g,'Stationery',-.17,.66,d*.5,.46,.105,d-.11,mat,travel=.31)
    return g
