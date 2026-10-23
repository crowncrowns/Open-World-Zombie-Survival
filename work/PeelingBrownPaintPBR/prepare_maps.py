from pathlib import Path
import sys,json
import numpy as np
from PIL import Image
source,hs,target=map(Path,sys.argv[1:4]);size=2048
if target.exists() and any(target.iterdir()):raise RuntimeError('Refuse overwrite')
target.mkdir(parents=True,exist_ok=True)
im=Image.open(source).convert('RGB');a=np.asarray(im.resize((size,size),Image.Resampling.LANCZOS),dtype=np.float32)/255
hi=Image.open(hs).convert('L').resize((size,size),Image.Resampling.LANCZOS);height=np.asarray(hi,dtype=np.float32)/255
def periodic_x(x,band=48):
 x=x.copy();d=(x[:,0]-x[:,-1]).copy()
 for i in range(band):
  w=.5*(1-i/(band-1))**2;x[:,i]-=d*w;x[:,-1-i]+=d*w
 return np.clip(x,0,1)
def blur(x,r):
 from PIL import ImageFilter
 return np.asarray(Image.fromarray(np.uint8(np.clip(x,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),dtype=np.float32)/255
a=periodic_x(periodic_x(a).swapaxes(0,1)).swapaxes(0,1);height=periodic_x(periodic_x(blur(height,1.1)).swapaxes(0,1)).swapaxes(0,1);height=np.clip((height-np.percentile(height,1))/max(float(np.percentile(height,99)-np.percentile(height,1)),.01),0,1)
green=np.clip((a[:,:,1]-np.maximum(a[:,:,0],a[:,:,2])-.025)/.10,0,1)
lum=a.mean(axis=2);dirt=np.clip((.39-lum)/.28,0,1)
paint=np.clip((a[:,:,0]-a[:,:,1]-.035)/.18,0,1)
rough=np.clip(.9-.28*paint+.08*dirt,.58,.96)
ao=np.clip(1-.28*np.maximum(blur(height,10)-height,0),.85,1)
dy=np.gradient(height*.0008,axis=0)*size/2
dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*.5*.0008*size/2
n=np.stack([-dx,dy,np.ones_like(dx)],axis=2);n/=np.linalg.norm(n,axis=2,keepdims=True)
zero=np.zeros_like(height)
def u8(x):return np.uint8(np.rint(np.clip(x,0,1)*255))
def save(name,x):Image.fromarray(u8(x)).save(target/('PBP01_'+name+'.png'))
save('BaseColor',a);save('Normal_OpenGL',n*.5+.5);Image.fromarray(np.uint16(np.rint(height*65535))).save(target/'PBP01_Height16.png')
save('AO',ao);save('Roughness',rough);save('Metallic',zero);save('MetallicSmoothness',np.stack([zero,zero,zero,1-rough],axis=2));save('ORM',np.stack([ao,rough,zero],axis=2))
check={'resolution':[size,size],'source_resolution':list(im.size),'mapping':'Repeat U/V; world triplanar','tile_metres_assumption':[2,2],'height_range_m':.0008,'basecolor_horizontal_edge_error':int(np.abs(u8(a)[:,0].astype(int)-u8(a)[:,-1].astype(int)).max()),'normal_max_unit_error':float(np.abs(np.linalg.norm(n,axis=2)-1).max()),'note':'AI color/height; computed normals and estimated AO/roughness; brown aged paint semi-matte, exposed plaster matte. Not measured PBR scan.'}
(target/'Validation.json').write_text(json.dumps(check,indent=2),encoding='utf-8');print(json.dumps(check,indent=2))
p=Image.fromarray(u8(a)).resize((600,600));out=Image.new('RGB',(1200,600));out.paste(p,(0,0));out.paste(p,(600,0));out.save('work/PeelingBrownPaintPBR/repeat-preview.jpg',quality=94)

