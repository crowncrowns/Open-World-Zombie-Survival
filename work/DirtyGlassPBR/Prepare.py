from PIL import Image,ImageFilter
from pathlib import Path
import numpy as np,sys,json
p=Path('Assets/Maps/Buildings/Textures/DirtyHazyGlass_01');p.mkdir(exist_ok=False);s=1024
im=Image.open(sys.argv[1]).convert('RGB');a=np.asarray(im.resize((s,s),Image.Resampling.LANCZOS),dtype=np.float32)/255
for axis in (0,1):
 a=a.swapaxes(axis,1);d=(a[:,0]-a[:,-1]).copy()
 for i in range(32):
  t=.5*(1-i/31)**2;a[:,i]-=d*t;a[:,-1-i]+=d*t
 a=a.swapaxes(axis,1)
lum=a.mean(2);lo,hi=np.percentile(lum,[2,98]);dirt=np.clip((hi-lum)/max(hi-lo,.05),0,1)
alpha=.23+.42*dirt;rgb=np.empty_like(a);rgb[:]=[.68,.70,.68];rgb-=dirt[:,:,None]*np.array([.10,.11,.12]);rough=.52+.34*dirt
# Extremely weak fine surface relief; dirt has no large bulges.
bl=np.asarray(Image.fromarray(np.uint8(lum*255)).filter(ImageFilter.GaussianBlur(4)),dtype=np.float32)/255
height=.5+(lum-bl)*.08;dy,dx=np.gradient(height*.00002);dx*=s;dy*=s;n=np.stack([-dx,dy,np.ones_like(dx)],2);n/=np.linalg.norm(n,axis=2,keepdims=True);z=np.zeros_like(lum);one=np.ones_like(lum)
def save(k,v):Image.fromarray(np.uint8(np.rint(np.clip(v,0,1)*255))).save(p/('DHG01_'+k+'.png'))
save('BaseColor',np.dstack([rgb,alpha]));save('Opacity',alpha);save('DirtMask',dirt);save('Roughness',rough);save('Normal_OpenGL',n*.5+.5);save('Metallic',z);save('MetallicSmoothness',np.dstack([z,z,z,1-rough]));save('AO',one);save('ORM',np.dstack([one,rough,z]));Image.fromarray(np.uint16(np.rint(np.clip(height,0,1)*65535))).save(p/'DHG01_Height16.png')
(p/'Validation.json').write_text(json.dumps({'resolution':[s,s],'source_size':list(im.size),'alpha_range':[float(alpha.min()),float(alpha.max())],'roughness_range':[float(rough.min()),float(rough.max())],'metallic':0,'normal_error':float(abs(np.linalg.norm(n,axis=2)-1).max()),'note':'Estimated dirt opacity and roughness, visual haze only, no refraction blur'},indent=2))
