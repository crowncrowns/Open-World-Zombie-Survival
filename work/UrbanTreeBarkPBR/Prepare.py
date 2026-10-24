from pathlib import Path
from PIL import Image,ImageFilter
import numpy as np,json
src=Path('C:/Users/Григорий/.codex/generated_images/01a16020-cae0-78d2-8c66-4d7f34319e8d');root=Path('Assets/Maps/Buildings/Textures');size=2048
items=[('Birch','exec-f071c692-f515-40fa-a9e9-98441cc7f20a.png',.002),('Poplar','exec-cf397477-2563-4a15-861f-6713acba74cc.png',.014),('Linden','exec-1d7be0fb-16b4-4e74-9b56-8582b579531e.png',.006),('Maple','exec-a4eaf521-c2d5-4771-8396-efe879b5e110.png',.005)]
def blur(v,r):return np.asarray(Image.fromarray(np.uint8(np.clip(v,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),dtype=np.float32)/255
for name,file,depth in items:
 p=root/('UrbanBark_'+name+'_01');p.mkdir(exist_ok=False);im=Image.open(src/file).convert('RGB');a=np.asarray(im.resize((size,size),Image.Resampling.LANCZOS),dtype=np.float32)/255
 for axis in (0,1):
  a=a.swapaxes(axis,1);d=(a[:,0]-a[:,-1]).copy()
  for i in range(48):
   w=.5*(1-i/47)**2;a[:,i]-=d*w;a[:,-1-i]+=d*w
  a=a.swapaxes(axis,1)
 lum=a.mean(2)
 if name=='Birch':height=np.clip(.55+(lum-blur(lum,14))*.35,.3,.8)
 else:
  lo,hi=np.percentile(lum,[3,97]);height=blur(np.clip((lum-lo)/max(hi-lo,.05),0,1),1.2)
 # Make reconstructed data periodic too.
 for axis in (0,1):
  height=height.swapaxes(axis,1);d=(height[:,0]-height[:,-1]).copy()
  for i in range(32):
   w=.5*(1-i/31)**2;height[:,i]-=d*w;height[:,-1-i]+=d*w
  height=height.swapaxes(axis,1)
 height=np.clip(height,0,1);dx=(np.roll(height,-1,1)-np.roll(height,1,1))*.5*depth*size;dy=(np.roll(height,-1,0)-np.roll(height,1,0))*.5*depth*size
 n=np.stack([-dx,dy,np.ones_like(dx)],2);n/=np.linalg.norm(n,axis=2,keepdims=True);z=np.zeros_like(lum);rough=np.clip(.86+.1*(1-height),.84,.98);ao=np.clip(1-.6*np.maximum(blur(height,12)-height,0),.7,1)
 def save(k,v):Image.fromarray(np.uint8(np.rint(np.clip(v,0,1)*255))).save(p/('Bark_'+k+'.png'))
 save('BaseColor',a);save('Normal_OpenGL',n*.5+.5);save('AO',ao);save('Roughness',rough);save('Metallic',z);save('MetallicSmoothness',np.dstack([z,z,z,1-rough]));save('ORM',np.dstack([ao,rough,z]));Image.fromarray(np.uint16(np.rint(height*65535))).save(p/'Bark_Height16.png')
 report={'species':name,'resolution':[size,size],'source_size':list(im.size),'tile_metres':[1,1],'relief_range_m':depth,'height_source':'approximate reconstruction from color, NOT scanned height; birch high-pass avoids treating broad black markings as pits','normal_error':float(abs(np.linalg.norm(n,axis=2)-1).max()),'finite':bool(np.isfinite(n).all())};(p/'Validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
