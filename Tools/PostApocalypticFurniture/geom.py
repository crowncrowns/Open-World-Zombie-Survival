"""Editable furniture geometry. Author coordinates: X right, Y up, Z front, metres."""
import math, random
from math import sin, cos, pi

MATERIALS = ['Wood','DarkWood','Plywood','Chipboard','PaintedWood','WhiteLaminate',
 'Fabric','RedFabric','GreenFabric','Leather','Foam','Steel','Rust','Chrome','Enamel',
 'Plastic','WhitePlastic','Glass','Mirror','Rubber','Paper','Carpet','Screen','Emission','Ceramic','Brass','Labels'] + ['Carpet'+str(i) for i in range(10)]

def rotate(v,angles):
    x,y,z=v; a,b,c=[math.radians(q) for q in angles]
    y,z=y*cos(a)-z*sin(a),y*sin(a)+z*cos(a)
    x,z=x*cos(b)+z*sin(b),-x*sin(b)+z*cos(b)
    x,y=x*cos(c)-y*sin(c),x*sin(c)+y*cos(c)
    return x,y,z

class Geo:
    def __init__(self,row):
        self.row=row; self.meshes=[]; self.parts={'Body':dict(name='Body',pivot=[0,0,0],kind='static',axis=[0,1,0],amount=0,label='Корпус')}
        self.current='Body'; self.colliders=[]; self.zones=[]; self.lights=[]; self.devices=[]; self.damage_groups=[]
    def add(self,v,f,mat='Wood',smooth=False,collider=None):
        if mat not in MATERIALS: raise ValueError('Unknown material '+mat)
        self.meshes.append(dict(v=v,f=f,mat=mat,part=self.current,smooth=smooth))
        if collider:self.colliders.append(dict(part=self.current,**collider))
    def part(self,name,pivot=(0,0,0),kind='hinge',axis=(0,1,0),amount=105,label=None,**kwargs):
        if name not in self.parts:self.parts[name]=dict(name=name,pivot=list(pivot),kind=kind,axis=list(axis),amount=amount,label=label or name,**kwargs)
        self.current=name; return self
    def static(self):self.current='Body'; return self
    def damage(self,name):
        self.part(name,kind='damage'); self.damage_groups.append(name); return self
    def zone(self,name,center,size):self.zones.append(dict(name=name,center=list(center),size=list(size),part=self.current))
    def light(self,position,intensity=1,range=2,part=None,**kwargs):self.lights.append(dict(position=list(position),intensity=intensity,range=range,part=part or self.current,**kwargs))
    def device(self,kind='device',**kwargs):self.devices.append(dict(kind=kind,**kwargs))
    def box(self,p,s,mat='Wood',bevel=.006,rot=(0,0,0),collider=True,**kwargs):
        if isinstance(rot,(int,float)):rot=(0,rot,0)
        a,b,c=[q*.5 for q in s]; e=max(0,min(bevel,a*.4,b*.4,c*.4)); v=[]
        if e<.00005:
            v=[(x,y,z) for x,y,z in [(-a,-b,-c),(-a,-b,c),(-a,b,-c),(-a,b,c),(a,-b,-c),(a,-b,c),(a,b,-c),(a,b,c)]]
            f=[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)]
        else:
            for y,inset in [(-b,e),(-b+e,0),(b-e,0),(b,e)]:
                aa,cc=a-inset,c-inset
                for x,z in [(-aa+e,-cc),(aa-e,-cc),(aa,-cc+e),(aa,cc-e),(aa-e,cc),(-aa+e,cc),(-aa,cc-e),(-aa,-cc+e)]:v.append((x,y,z))
            f=[tuple(range(7,-1,-1)),tuple(range(24,32))]
            for j in range(3):
                for i in range(8):f.append((j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i))
        local_v=list(v)
        # Face charts use the primitive frame, so rotating a board also rotates
        # its grain. Long axes run along V; all charts retain metre density.
        charts=[]
        for face in f:
            q0,q1,q2=[local_v[i] for i in face[:3]]
            e1=[q1[k]-q0[k] for k in range(3)];e2=[q2[k]-q0[k] for k in range(3)]
            normal=(e1[1]*e2[2]-e1[2]*e2[1],e1[2]*e2[0]-e1[0]*e2[2],e1[0]*e2[1]-e1[1]*e2[0])
            dominant=max(range(3),key=lambda k:abs(normal[k]));axes=[k for k in range(3) if k!=dominant]
            axes.sort(key=lambda k:s[k])
            charts.append([(local_v[i][axes[0]]*2,local_v[i][axes[1]]*2) for i in face])
        v=[tuple(rotate(q,rot)[i]+p[i] for i in range(3)) for q in v]
        col=dict(center=list(p),size=list(s),rotation=list(rot)) if collider else None
        self.add(v,f,mat,False,col)
        self.meshes[-1]['face_uv']=charts
    def rod(self,a,b,r,mat='Steel',n=12,collider=None):
        import mathutils
        av,bv=mathutils.Vector(a),mathutils.Vector(b); axis=bv-av
        if axis.length<1e-7:return
        length=axis.length;axis.normalize();u=axis.cross(mathutils.Vector((0,1,0)))
        if u.length<.01:u=axis.cross(mathutils.Vector((1,0,0)))
        u.normalize();w=axis.cross(u);vs=[]
        for p in [av,bv]:vs.extend([tuple(p+r*(u*cos(i*2*pi/n)+w*sin(i*2*pi/n))) for i in range(n)])
        fs=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        # Rod collision follows the actual cylinder axis; thin seams and wires do not obstruct placement.
        col=None
        if collider is True or (collider is None and r>=.009 and length>=.05):col=dict(type='capsule',a=list(a),b=list(b),radius=r*cos(pi/n))
        self.add(vs,fs,mat,True,col)
        circumference=2*pi*r
        charts=[]
        for face in fs:
            if all(i<n for i in face) or all(i>=n for i in face):
                charts.append([(float((mathutils.Vector(vs[i])-av).dot(u))*2,float((mathutils.Vector(vs[i])-av).dot(w))*2) for i in face])
            else:
                ks=[i%n for i in face];seam=0 in ks and n-1 in ks
                charts.append([(((n if seam and i%n==0 else i%n)/n)*circumference*2,(length if i>=n else 0)*2) for i in face])
        self.meshes[-1]['face_uv']=charts
    def path(self,pts,r,mat='Steel',n=8,collider=None):
        for a,b in zip(pts,pts[1:]):self.rod(a,b,r,mat,n,collider)
    def lathe(self,center,profile,mat='Steel',axis='y',n=24,collider=False,**kwargs):
        vs=[];fs=[]
        for radius,h in profile:
            for k in range(n):
                a=2*pi*k/n;v=(radius*cos(a),h,radius*sin(a))
                if axis=='z':v=(v[0],-v[2],v[1])
                elif axis=='x':v=(v[1],v[0],v[2])
                vs.append(tuple(v[i]+center[i] for i in range(3)))
        for j in range(len(profile)-1):
            for k in range(n):fs.append((j*n+k,j*n+(k+1)%n,(j+1)*n+(k+1)%n,(j+1)*n+k))
        closed=kwargs.get('closed',profile[0][0]>0 and profile[-1][0]>0 and abs(profile[0][1]-profile[-1][1])<.00001)
        if closed:
            j=len(profile)-1
            for k in range(n):fs.append((j*n+k,j*n+(k+1)%n,(k+1)%n,k))
        else:fs.extend([tuple(range(n-1,-1,-1)),tuple((len(profile)-1)*n+i for i in range(n))])
        self.add(vs,fs,mat,True)
        # Continuous cylindrical charts with a single longitudinal seam.
        arc=[0.0]
        for a,b in zip(profile,profile[1:]):arc.append(arc[-1]+math.hypot(b[0]-a[0],b[1]-a[1]))
        charts=[];circumference=2*pi*max(q[0] for q in profile)
        for face in fs:
            if len(face)==n:
                # End caps have real planar metric UVs, not a collapsed UV line.
                charts.append([(profile[i//n][0]*cos((i%n)*2*pi/n)*2,profile[i//n][0]*sin((i%n)*2*pi/n)*2) for i in face])
            else:
                ks=[i%n for i in face];seam=0 in ks and n-1 in ks
                charts.append([((n if seam and i%n==0 else i%n)/n*circumference*2,arc[i//n]*2) for i in face])
        self.meshes[-1]['face_uv']=charts
        if collider:
            lo=[min(v[i] for v in vs) for i in range(3)];hi=[max(v[i] for v in vs) for i in range(3)]
            center=[(lo[i]+hi[i])*.5 for i in range(3)];size=[hi[i]-lo[i] for i in range(3)];ax={'x':0,'y':1,'z':2}[axis]
            for k in range(3):
                if k!=ax:size[k]/=math.sqrt(2)
            for angle in [0,45]:
                rotation=[0,0,0];rotation[ax]=angle
                self.colliders.append(dict(part=self.current,center=center,size=list(size),rotation=rotation))
    def ring(self,c,r,tube=.005,mat='Steel',axis='y',n=24,**kwargs):
        vs=[];fs=[];m=6
        for k in range(n):
            a=k*2*pi/n
            for j in range(m):
                b=j*2*pi/m;v=((r+tube*cos(b))*cos(a),tube*sin(b),(r+tube*cos(b))*sin(a))
                if axis=='z':v=(v[0],-v[2],v[1])
                elif axis=='x':v=(v[1],v[0],v[2])
                vs.append(tuple(v[i]+c[i] for i in range(3)))
        for k in range(n):
            for j in range(m):fs.append((k*m+j,k*m+(j+1)%m,((k+1)%n)*m+(j+1)%m,((k+1)%n)*m+j))
        self.add(vs,fs,mat,True)
    def sphere(self,c,size,mat='Fabric',n=16,rings=8):
        vs=[];fs=[]
        for j in range(rings+1):
            b=pi*j/rings
            for i in range(n):
                a=2*pi*i/n;vs.append((c[0]+size[0]*.5*sin(b)*cos(a),c[1]+size[1]*.5*cos(b),c[2]+size[2]*.5*sin(b)*sin(a)))
        for j in range(rings):
            for i in range(n):fs.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
        self.add(vs,fs,mat,True,dict(center=list(c),size=list(size),rotation=[0,0,0]))
    def cushion(self,c,size,mat='Fabric',sag=.03,seed=0,segments=12,**kwargs):
        # Rounded superellipse volume, locally depressed top, asymmetric soft creases; welded on export.
        # A backrest/arm pad has a different thin axis from a seat. Keeping Y
        # as the thickness axis for every pad put piping across backrest faces.
        thin=min(range(3),key=lambda k:size[k])
        axes=(0,1,2) if thin==1 else (0,2,1) if thin==2 else (2,0,1)
        vs=[];fs=[];n=max(32,segments*2);rings=12;a,b,d=[size[k]/2 for k in axes];phase=(seed+self.row['id'])*.719
        def signedpow(q,p):return math.copysign(abs(q)**p,q)
        for j in range(rings+1):
            beta=-pi/2+pi*j/rings;rad=0 if j in [0,rings] else abs(cos(beta))**.28
            for k in range(n):
                angle=2*pi*k/n;x=a*rad*signedpow(cos(angle),.30);z=d*rad*signedpow(sin(angle),.30)
                y=b*signedpow(sin(beta),.40)
                # Small tension folds collect at corners; the whole cushion
                # no longer oscillates as an evenly spaced radial waveform.
                crease=.0022*(abs(x/max(a,.001))**6*sin(z/max(d,.001)*13+phase)+abs(z/max(d,.001))**6*sin(x/max(a,.001)*11-phase))*abs(cos(beta))
                if j>rings/2:y-=sag*math.exp(-((x/(a*.62))**2+(z/(d*.68))**2))*sin(beta)
                local=(x,y+crease,z);point=list(c)
                for index,axis in enumerate(axes):point[axis]+=local[index]
                vs.append(tuple(point))
        for j in range(rings):
            for k in range(n):fs.append((j*n+k,j*n+(k+1)%n,(j+1)*n+(k+1)%n,(j+1)*n+k))
        collision_size=[size[k]*(.85 if k==thin else .94) for k in range(3)]
        self.add(vs,fs,mat,True,dict(center=list(c),size=collision_size,rotation=[0,0,0]))
        pts=[vs[(rings//2)*n+k%n] for k in range(n+1)]
        if mat!='Screen':self.path(pts,.0014,mat,6,False)
    def cloth(self,c,size,mat='Fabric',fold=.03,nx=16,nz=16,**kwargs):
        vs=[];fs=[];thick=max(.004,size[1]);phase=self.row['id']*.49
        for layer in [0,1]:
            for j in range(nz+1):
                for i in range(nx+1):
                    x=(i/nx-.5)*size[0];z=(j/nz-.5)*size[2]
                    h=fold*(sin(x*17+z*6+phase)*.45+sin(z*29-x*4)*.18+cos(x*7-z*11)*.22)
                    edge=abs(i/nx-.5)*2;h+=fold*edge**8*sin(z*12+phase)
                    vs.append((c[0]+x,c[1]+h+(thick*.5 if layer==0 else -thick*.5),c[2]+z))
        N=(nx+1)*(nz+1)
        for layer in [0,1]:
            for j in range(nz):
                for i in range(nx):
                    q=layer*N+j*(nx+1)+i;f=(q,q+nx+1,q+nx+2,q+1);fs.append(f if layer==0 else tuple(reversed(f)))
        rim=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,nz+1)]+[nz*(nx+1)+i for i in range(nx-1,-1,-1)]+[j*(nx+1) for j in range(nz-1,0,-1)]
        for a,b in zip(rim,rim[1:]+rim[:1]):fs.append((a,b,b+N,a+N))
        rot=kwargs.get('rot',(0,0,0))
        if rot!=(0,0,0):vs=[tuple(rotate(tuple(p[i]-c[i] for i in range(3)),rot)[i]+c[i] for i in range(3)) for p in vs]
        self.add(vs,fs,mat,True,dict(center=list(c),size=[size[0],thick+fold*.4,size[2]],rotation=list(rot)))
    def label(self,c,size,index=0,rot=(0,0,0)):
        w,h=size;v=[(-w/2,-h/2,0),(w/2,-h/2,0),(w/2,h/2,0),(-w/2,h/2,0)]
        v=[tuple(rotate(q,rot)[i]+c[i] for i in range(3)) for q in v]
        self.add(v,[(0,1,2,3)],'Labels')
        widths=[.085938,.162094,.172760,.245056,.106445,.302719,.133774,.200195,.217743,.215775,.199219,.440353,.172775,.248001,.238251,.417938]
        y0=1-(index*64+54)/1024;y1=1-(index*64+15)/1024
        u0,u1=.009,widths[index]
        # The exported front chart is viewed from +Z in Unity. Reverse U here,
        # rather than mirroring the atlas (which would break other consumers).
        self.meshes[-1]['uv']=[(u1,y0),(u0,y0),(u0,y1),(u1,y1)]
