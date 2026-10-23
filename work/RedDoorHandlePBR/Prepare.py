from pathlib import Path
import sys,json
import numpy as np
from PIL import Image,ImageFilter
p=Path('Assets/Maps/Buildings/Textures/RedDoorHandle_01');p.mkdir(exist_ok=False);s=1024
im=Image.open(sys.argv[1]).convert('RGB');a=np.asarray(im.resize((s,s),Image.Resampling.LANCZOS),dtype=np.float32)/255
for axis in (0,1):
 a=a.swapaxes(axis,1);d=(a[:,0]-a[:,-1]).copy()
 for i in range(32):
  w=.5*(1-i/31)**2;a[:,i]-=d*w;a[:,-1-i]+=d*w
 a=a.swapaxes(axis,1)
def blur(v,r):return np.asarray(Image.fromarray(np.uint8(np.clip(v,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),dtype=np.float32)/255
lum=a.mean(2);detail=lum-blur(lum,6);height=np.clip(.5+detail*.3,.4,.6);rough=np.clip(.66+(blur(lum,5)-lum)*.1,.60,.75);metal=np.full_like(lum,.65);ao=np.ones_like(lum)
dy,dx=np.gradient(height*.00008);dx*=s/.13;dy*=s/.13;n=np.stack([-dx,dy,np.ones_like(dx)],2);n/=np.linalg.norm(n,axis=2,keepdims=True)
def save(k,v):Image.fromarray(np.uint8(np.rint(np.clip(v,0,1)*255))).save(p/('RDH01_'+k+'.png'))
save('BaseColor',a);save('Normal_OpenGL',n*.5+.5);save('AO',ao);save('Roughness',rough);save('Metallic',metal);save('MetallicSmoothness',np.stack([metal,metal,metal,1-rough],2));save('ORM',np.stack([ao,rough,metal],2));Image.fromarray(np.uint16(np.rint(height*65535))).save(p/'RDH01_Height16.png')
(p/'Validation.json').write_text(json.dumps({'resolution':[s,s],'source_size':list(im.size),'metallic':.65,'roughness_range':[float(rough.min()),float(rough.max())],'height':'approximate fine scratches from high-pass albedo','normal_error':float(abs(np.linalg.norm(n,axis=2)-1).max())},indent=2))

