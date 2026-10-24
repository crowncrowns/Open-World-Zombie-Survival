import json,re
from pathlib import Path
import numpy as np
from PIL import Image,ImageFilter
src=Path('C:/Users/Григорий/.codex/generated_images/01a16020-cae0-78d2-8c66-4d7f34319e8d');root=Path('Assets/Maps/Buildings/Textures');size=2048
for folder,file,repair in [('WeatheredPanelFacade_01','exec-d3302750-e7ea-47ec-bc66-79a473bf2f70.png',False),('WeatheredPanelFacade_Repairs3x3_01','exec-47568509-5ffc-456e-8b9e-0b86896b10c8.png',True)]:
 p=root/folder;p.mkdir(exist_ok=False);im=Image.open(src/file).convert('RGB')
 if repair:im=im.crop((25,25,im.width-25,im.height-25))
 a=np.asarray(im.resize((size,size),Image.Resampling.LANCZOS),dtype=np.float32)/255
 for axis in (0,1):
  a=a.swapaxes(axis,1);d=(a[:,0]-a[:,-1]).copy()
  for i in range(32):
   t=.5*(1-i/31)**2;a[:,i]-=d*t;a[:,-1-i]+=d*t
  a=a.swapaxes(axis,1)
 def blur(v,r):return np.asarray(Image.fromarray(np.uint8(np.clip(v,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),dtype=np.float32)/255
 lum=a.mean(2);height=.6+(lum-blur(lum,12))*.5
 if repair:height-=np.clip((.62-blur(lum,2))/.25,0,1)*.12
 height=np.clip(height,0,1);dy,dx=np.gradient(height*.003);dx*=size/3;dy*=size/3;n=np.stack([-dx,dy,np.ones_like(dx)],2);n/=np.linalg.norm(n,axis=2,keepdims=True);z=np.zeros_like(lum);rough=np.full_like(lum,.94);ao=np.clip(1-.3*np.maximum(blur(height,10)-height,0),.9,1)
 def save(k,v):Image.fromarray(np.uint8(np.rint(np.clip(v,0,1)*255))).save(p/('WPF01_'+k+'.png'))
 for k,v in [('BaseColor',a),('Normal_OpenGL',n*.5+.5),('AO',ao),('Roughness',rough),('Metallic',z),('MetallicSmoothness',np.dstack([z,z,z,1-rough])),('ORM',np.dstack([ao,rough,z]))]:save(k,v)
 Image.fromarray(np.uint16(np.rint(height*65535))).save(p/'WPF01_Height16.png')
 (p/'Validation.json').write_text(json.dumps({'resolution':[size,size],'tile_m':[3,3],'height':'approximate from fine albedo detail; repair bands slightly recessed','normal_error':float(abs(np.linalg.norm(n,axis=2)-1).max())},indent=2))
