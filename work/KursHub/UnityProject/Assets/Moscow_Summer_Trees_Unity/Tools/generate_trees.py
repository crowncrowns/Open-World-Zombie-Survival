#!/usr/bin/env python3
"""Moscow-region summer trees: 32 procedural fully modeled and wind-rigged GLBs."""
from __future__ import annotations
import math, random, json, os, sys
from pathlib import Path
import numpy as np
import trimesh
from PIL import Image, ImageDraw, ImageFilter
from trimesh.visual.material import PBRMaterial
from trimesh.visual.texture import TextureVisuals
ROOT = Path(__file__).resolve().parent.parent
SPECIES={
 'Birch': {'ru':'Берёза повислая', 'latin':'Betula pendula', 'height':18, 'radius':4.0, 'bark':(205,203,185), 'leaf':(83,141,55), 'branch_count':20},
 'Oak': {'ru':'Дуб черешчатый','latin':'Quercus robur','height':16,'radius':6.1,'bark':(92,77,63),'leaf':(58,115,46),'branch_count':23},
 'Pine': {'ru':'Сосна обыкновенная','latin':'Pinus sylvestris','height':21,'radius':4.7,'bark':(133,87,57),'leaf':(66,113,62),'branch_count':22},
 'Spruce': {'ru':'Ель европейская','latin':'Picea abies','height':19,'radius':4.4,'bark':(95,82,70),'leaf':(43,93,57),'branch_count':25},
 'Linden': {'ru':'Липа мелколистная','latin':'Tilia cordata','height':15,'radius':5.0,'bark':(105,93,78),'leaf':(89,143,58),'branch_count':22},
 'Maple': {'ru':'Клён остролистный','latin':'Acer platanoides','height':16,'radius':5.0,'bark':(100,93,81),'leaf':(76,137,52),'branch_count':22},
 'Aspen': {'ru':'Осина','latin':'Populus tremula','height':19,'radius':3.9,'bark':(178,174,153),'leaf':(102,154,66),'branch_count':21},
 'Rowan': {'ru':'Рябина обыкновенная','latin':'Sorbus aucuparia','height':9,'radius':3.4,'bark':(129,114,98),'leaf':(76,136,62),'branch_count':18},
}
VARIANTS=['TallMature','Young','Weathered','Dense']
LEVELS=[0,1,2]

def recolor(rgb, scale):
 return tuple(max(0,min(255,round(v*scale))) for v in rgb)

def lerp(a,b,t): return a*(1-t)+b*t

def make_bark_image(name, spec):
 N=512
 rng=random.Random(name+'bark')
 img=Image.new('RGB',(N,N)); pix=img.load(); base=spec['bark']
 vertical=np.array([rng.gauss(0,1) for i in range(N)], dtype=float)
 for _ in range(3): vertical=(vertical + np.roll(vertical,1)+np.roll(vertical,-1))/3
 for y in range(N):
  for x in range(N):
   v=vertical[x]*19 + 7*math.sin(x*0.065+5*math.sin(y*0.019)) + 6*math.sin(x*0.21+y*0.006)
   if name=='Birch':
    if (abs(math.sin(y*.1+x*.037)+.75*math.sin(y*.017+x*.065))>1.35): v-=85
   if name=='Pine': v+=12*math.sin(x*.065+y*.045)
   if name=='Aspen': v+=6*math.sin(x*.045+y*.007)
   noise=rng.uniform(-10,10)
   pix[x,y]=tuple(int(max(0,min(255,b+v+noise))) for b in base)
 d=ImageDraw.Draw(img,'RGBA')
 if name in ('Oak','Spruce','Linden','Maple','Rowan'):
  for i in range(215):
   x=rng.randrange(N); y=rng.randrange(N); h=rng.randrange(15,72)
   d.line((x,y,x+rng.randrange(-7,8),y+h),fill=(24,20,16,rng.randrange(40,110)),width=rng.randrange(1,4))
 if name in ('Birch','Aspen'):
  for i in range(120):
   x=rng.randrange(N); y=rng.randrange(N)
   d.ellipse((x,y,x+rng.randrange(4,23),y+rng.randrange(2,7)),fill=(35,29,27,rng.randrange(80,200)))
 return img

def leaf_polygon(name,cx,cy,r,ang=0):
 # Point cloud centered around (cx,cy) with species-specific silhouette
 pts=[]
 if name == 'Oak':
  for k in range(20):
   t=k*2*math.pi/20
   rr=r*(.82+.19*math.cos(8*t))
   pts.append((cx+rr*math.cos(t)*.69,cy+rr*math.sin(t)))
 elif name == 'Maple':
  for k in range(20):
   t=k*2*math.pi/20
   rr=r*(.58+.43*max(0,math.cos(5*t-math.pi/2))**2)
   pts.append((cx+rr*math.cos(t),cy+rr*math.sin(t)))
 elif name == 'Linden':
  for k in range(25):
   t=k*2*math.pi/25
   rr=r*(.93+.09*math.sin(2*t))
   pts.append((cx+rr*math.cos(t)*(1-.18*math.sin(t)),cy+rr*math.sin(t)))
 elif name == 'Birch':
  for k in range(20):
   t=k*2*math.pi/20
   rr=r*(.85+.10*math.cos(14*t))
   pts.append((cx+rr*math.cos(t)*.67,cy+rr*math.sin(t)*1.18))
 elif name == 'Rowan':
  for k in range(23):
   t=k*2*math.pi/23
   rr=r*(.82+.10*math.cos(12*t))
   pts.append((cx+rr*math.cos(t)*.42,cy+rr*math.sin(t)))
 else:
  for k in range(24):
   t=k*2*math.pi/24
   rr=r*(.95+.05*math.cos(10*t))
   pts.append((cx+rr*math.cos(t),cy+rr*math.sin(t)))
 return pts

def leaf_atlas(name,spec):
 S=512; scale=2; N=S*scale
 img=Image.new('RGBA',(N,N),(0,0,0,0)); d=ImageDraw.Draw(img,'RGBA')
 rnd=random.Random('leaf'+name)
 base=spec['leaf']
 # Full image leaf spray with silhouettes and transparent cutouts, no billboard black border
 if name in ('Pine','Spruce'):
  for cluster in range(13):
   xx=rnd.uniform(.15,.85)*N; yy=rnd.uniform(.14,.86)*N
   thick=4 if name=='Spruce' else 3
   for k in range(15 if name=='Pine' else 23):
    angle=rnd.uniform(0,math.tau)
    rr=rnd.uniform(.07,.27)*N if name=='Pine' else rnd.uniform(.06,.19)*N
    x=xx+math.cos(angle)*rr; y=yy+math.sin(angle)*rr
    col=(*recolor(base,rnd.uniform(.7,1.3)),rnd.randrange(185,255))
    d.line((xx,yy,x,y),fill=col,width=thick*scale)
   d.ellipse((xx-4*scale,yy-4*scale,xx+4*scale,yy+4*scale),fill=(*recolor(base,.55),230))
 else:
  for k in range(18 if name=='Rowan' else 12):
   cx=rnd.uniform(.13,.87)*N; cy=rnd.uniform(.14,.86)*N
   r=rnd.uniform(.05,.10)*N
   pts=leaf_polygon(name,cx,cy,r)
   col=(*recolor(base,rnd.uniform(.78,1.3)),255)
   d.polygon(pts,fill=col)
   d.line((cx,cy, cx+rnd.uniform(-.05,.05)*N,cy+r*.85),fill=(*recolor(base,.55),215),width=2*scale)
   if name=='Rowan' and k%3==0:
    for z in range(3):
     x=cx+rnd.uniform(-r,r);y=cy+rnd.uniform(-r,r)
     d.ellipse((x-5*scale,y-5*scale,x+5*scale,y+5*scale),fill=(182,54,25,255))
 img=img.resize((S,S), Image.Resampling.LANCZOS)
 return img

class Builder:
 def __init__(self,material):
  self.vertices=[];self.faces=[];self.uv=[];self.mat=material
 def tube(self, pts, radii, sides=7):
  ps=[np.array(p,float) for p in pts]
  if len(ps)<2: return
  offs=len(self.vertices); length=0
  for j,p in enumerate(ps):
   if j==0: direction=ps[1]-ps[0]
   elif j==len(ps)-1: direction=ps[-1]-ps[-2]
   else: direction=ps[j+1]-ps[j-1]
   direction=direction/(np.linalg.norm(direction)+1e-12)
   axis=np.array([0,0,1.])
   if abs(float(direction@axis))>.95: axis=np.array([1.,0,0])
   right=np.cross(direction,axis);right/=np.linalg.norm(right)
   front=np.cross(direction,right)
   if j>0: length+=float(np.linalg.norm(ps[j]-ps[j-1]))
   for i in range(sides+1):
    t=i/sides*math.tau
    vert=p+radii[j]*(right*math.cos(t)+front*math.sin(t))
    self.vertices.append(vert)
    self.uv.append((i/sides, length*.8))
  for j in range(len(ps)-1):
   for i in range(sides):
    a=offs+j*(sides+1)+i; b=a+sides+1
    self.faces.append((a,b,a+1));self.faces.append((a+1,b,b+1))
 def card(self,center,w,h,side,up=None,uvrect=(0,0,1,1)):
  c=np.array(center,float); side=np.array(side,float)
  side=side/(np.linalg.norm(side)+1e-9)
  if up is None: up=np.array((0.,1.,0.))
  up=np.array(up,float); up=up/(np.linalg.norm(up)+1e-9)
  # make orthogonal
  up=up-np.dot(up,side)*side
  up=up/(np.linalg.norm(up)+1e-9)
  offset=len(self.vertices)
  self.vertices.extend([c-side*w/2-up*h/2,c+side*w/2-up*h/2,c+side*w/2+up*h/2,c-side*w/2+up*h/2])
  u,v,uu,vv=uvrect
  self.uv.extend([(u,vv),(uu,vv),(uu,v),(u,v)])
  self.faces.extend([(offset,offset+1,offset+2),(offset,offset+2,offset+3)])
 def mesh(self):
  if not self.faces:return None
  m=trimesh.Trimesh(vertices=np.asarray(self.vertices),faces=np.asarray(self.faces),process=False)
  m.visual=TextureVisuals(uv=np.array(self.uv),material=self.mat)
  return m


def tm_translate(vec):
 arr=np.eye(4);arr[:3,3]=vec;return arr

def branch_details(species, spec, b, rng, level, height, r, leafmat, barkmat):
 # b=(y_base, angle, radial_len, rise, branch index) ; local branch pivot at (0,y_base,0)
 y,angle,L,rise,idx=b
 mid=(.57*L*math.cos(angle),rise*.63,.57*L*math.sin(angle))
 tip=(L*math.cos(angle),rise,L*math.sin(angle))
 P=np.array([0.,0.,0.]); Q=np.array(mid); R=np.array(tip)
 br=Builder(barkmat); fol=Builder(leafmat)
 thick=min(.23,max(.028, height*.009 * (1.3-.7*y/height)))*rng.uniform(.7,1.35)
 br.tube((P,Q,R),(.76*thick,.4*thick,.015),sides=7 if level==0 else 5)
 # secondary branches from branch shoulder, varying species droop/elevation
 twigcount={0:5,1:3,2:0}[level]
 leaf_clusters={0:28,1:12,2:0}[level]
 if species in ['Birch','Aspen']:leaf_clusters=int(leaf_clusters*.76)
 if species=='Oak':leaf_clusters=int(leaf_clusters*1.4)
 if species in ('Spruce','Pine'):leaf_clusters=int(leaf_clusters*1.4)
 for j in range(twigcount):
  t=rng.uniform(.37,.90); S=Q*(1-t)+R*t if t>.55 else Q*t/.55
  q=angle+rng.uniform(-1.25,1.25)
  l=L*rng.uniform(.15,.38)
  tip2=S+np.array((math.cos(q)*l,rng.uniform(-.28,.5)*l,math.sin(q)*l))
  if species=='Birch':tip2[1]-=l*.3
  br.tube([S,(S+tip2)*.5+np.array((0,.04,0)),tip2],[thick*.24,thick*.12,.005],sides=5)
 for k in range(leaf_clusters):
  t=rng.uniform(.28,1.02)
  pos= (1-t)*Q+t*R if t>.5 else Q*(t/.5)
  offset=np.array([rng.uniform(-1,1),rng.uniform(-.95,1.05),rng.uniform(-1,1)])
  offset*= max(.18,L*.19)*rng.uniform(.22,.97)
  if species=='Birch': offset[1]-=rng.uniform(0,L*.2)
  if species=='Spruce':offset[1]-=rng.uniform(0,L*.13)
  pos=pos+offset
  size=(.78 if species in ('Birch','Rowan','Aspen') else 1.18)*rng.uniform(.75,1.55)*(height/16)**.25
  if species in ('Pine','Spruce'):size*=1.08
  for j in range(2 if level==0 else 1):
   theta=rng.uniform(0,math.tau)
   side=np.array([math.cos(theta),rng.uniform(-.25,.25),math.sin(theta)])
   up=np.array([rng.uniform(-.24,.24),1,rng.uniform(-.24,.24)])
   fol.card(pos,size*rng.uniform(.8,1.2),size*rng.uniform(.85,1.25),side,up)
 return br.mesh(),fol.mesh()

def tree_layout(species, spec, variant, level, rng):
 height=spec['height']*[1.0,.57,.9,.81][variant]*rng.uniform(.93,1.08)
 R=spec['radius']*[1,.68,1.06,1.05][variant]
 if variant==2:R*=.96
 n=int(spec['branch_count']*[1,.67,.82,1.24][variant]*[1,.70,.42][level])
 n=max(8,n)
 branches=[]
 for i in range(n):
  t=i/max(1,n-1)
  if species=='Spruce':
   y=height*(.16+.78*t); L=R*max(.095,1-t)**.87*rng.uniform(.73,1.15);rise=-(.10+.4*t)*L
  elif species=='Pine':
   y=height*(.42+.49*t);L=R*(1-.60*t)*rng.uniform(.8,1.15);rise=rng.uniform(-.15,.15)*L
  else:
   begin={'Birch':.38,'Oak':.29,'Linden':.34,'Maple':.36,'Aspen':.45,'Rowan':.32}[species]
   y=height*(begin+(.88-begin)*t)+rng.uniform(-.12,.12)
   L=R*(.52+.48*math.sin(t*math.pi)) * rng.uniform(.68,1.25)
   if species in ('Birch','Aspen'):L*=.85
   rise=L*rng.uniform(.22,.85)
   if species=='Birch':rise=L*.25
  angle=i*2.399963+ rng.uniform(-.2,.2)
  if variant==2 and .15<((angle%(2*math.pi))/math.tau)<.44: L*=.42
  branches.append((y,angle,L,rise,i))
 return height,R,branches

def make_tree(species,variant,level,materials):
 spec=SPECIES[species]; rng=random.Random(f'{species}-{variant}-{level}')
 h,R,branches=tree_layout(species,spec,variant,level,rng)
 scene=trimesh.Scene()
 root=scene.graph.base_frame
 # Wind trunk rotating from ground; all branches parented to this pivot
 scene.graph.update(frame_to='Wind_Trunk',frame_from=root,matrix=np.eye(4))
 trunk=Builder(materials['bark'])
 root_radius=h*(.013 if species in ('Birch','Aspen','Pine','Spruce') else .024)
 if species=='Rowan':root_radius=h*.028
 ys=np.linspace(0,h*.96,10 if level==0 else 6)
 pts=[];radii=[]
 sx=rng.uniform(-.04,.04);sz=rng.uniform(-.04,.04)
 for y in ys:
  yf=y/h
  pts.append((sx*y*.15+.12*math.sin(yf*math.pi*1.35),y,sz*y*.15+.10*math.cos(yf*math.pi*.9)))
  radii.append(max(.012,root_radius*(1-y/(h*1.03))**1.22))
 trunk.tube(pts,radii,10 if level==0 else 7)
 mesh=trunk.mesh()
 scene.add_geometry(mesh,node_name='Trunk_Mesh',geom_name='Trunk_Mesh',parent_node_name='Wind_Trunk')
 if level==2:
  # highly simplified 3D mesh LOD2: several planes in cross pattern with foliage alpha, static branches
  lower=h*(.35 if species not in ('Spruce','Pine') else .21)
  canopy_h=h*.88-lower
  for j in range(3):
   angle=j*math.pi/3
   fc=Builder(materials['leaf'])
   fc.card((0,lower+canopy_h*.55,0),2*R,canopy_h*1.25,(math.cos(angle),0,math.sin(angle)))
   scene.add_geometry(fc.mesh(),node_name=f'Foliage_Cross_{j}',parent_node_name='Wind_Trunk')
 else:
  for y,angle,L,rise,i in branches:
   pivot=f'Wind_Branch_{i:02d}'
   scene.graph.update(frame_to=pivot,frame_from='Wind_Trunk',matrix=tm_translate((0,y,0)))
   br,fol=branch_details(species,spec,(y,angle,L,rise,i),rng,level,h,R,materials['leaf'],materials['bark'])
   scene.add_geometry(br,node_name=f'Bark_{i:02d}',geom_name=f'Bark_{i:02d}',parent_node_name=pivot)
   scene.add_geometry(fol,node_name=f'Leaves_{i:02d}',geom_name=f'Leaves_{i:02d}',parent_node_name=pivot)
 name=f'{species}_{VARIANTS[variant]}_LOD{level}'
 metadata={'species':species,'variant':VARIANTS[variant],'level':level,'height_m':round(h,2),'canopy_radius_m':round(R,2)}
 p=ROOT/'Models'/f'LOD{level}'/(name+'.glb')
 glb=trimesh.exchange.gltf.export_glb(scene)
 p.write_bytes(glb)
 metrics={'name':name,'file':str(p.relative_to(ROOT)),'height_m':round(h,2),'radius_m':round(R,2),'triangles':sum(len(m.faces) for m in scene.geometry.values()),'nodes':len(scene.graph.nodes),'bytes':len(glb)}
 return metrics

def main():
 allstats=[]
 for name,spec in SPECIES.items():
  bark=make_bark_image(name,spec); leaf=leaf_atlas(name,spec)
  bark.save(ROOT/'Textures'/f'{name}_Bark.png')
  leaf.save(ROOT/'Textures'/f'{name}_Leaves.png')
  barkmat=PBRMaterial(name=f'{name}_Bark',baseColorTexture=bark,metallicFactor=0,roughnessFactor=.96,doubleSided=False)
  leafmat=PBRMaterial(name=f'{name}_LeavesCutout',baseColorTexture=leaf,alphaMode='MASK',alphaCutoff=.35,metallicFactor=0,roughnessFactor=.92,doubleSided=True)
  for variant in range(4):
   for level in LEVELS:
    x=make_tree(name,variant,level,{'bark':barkmat,'leaf':leafmat})
    allstats.append(x)
    print(f"{x['name']} {x['triangles']} tris {x['bytes']/1e6:.2f}MB",flush=True)
 (ROOT/'TreeCatalog.json').write_text(json.dumps({'species':SPECIES,'variants':VARIANTS,'models':allstats},ensure_ascii=False,indent=2),encoding='utf8')
 print('TOTAL',len(allstats),'assets',sum(x['triangles'] for x in allstats),'tris',flush=True)

if __name__=='__main__':main()
