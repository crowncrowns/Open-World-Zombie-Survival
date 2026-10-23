from pathlib import Path
import sys,json
import numpy as np
from PIL import Image,ImageFilter
p=Path('Assets/Maps/Buildings/Textures/WhiteStairwellCeiling_01');size=2048
if p.exists():raise RuntimeError('Refuse overwrite')
p.mkdir(parents=True)
im=Image.open(sys.argv[1]).convert('RGB');a=np.asarray(im.resize((size,size),Image.Resampling.LANCZOS),dtype=np.float32)/255
def seam(x):
 x=x.copy();d=(x[:,0]-x[:,-1]).copy()
 for i in range(48):
  w=.5*(1-i/47)**2;x[:,i]-=d*w;x[:,-1-i]+=d*w
 return np.clip(x,0,1)
a=seam(seam(a).swapaxes(0,1)).swapaxes(0,1)
def blur(x,r):return np.asarray(Image.fromarray(np.uint8(np.clip(x,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),dtype=np.float32)/255
lum=a.mean(axis=2);detail=lum-blur(lum,12);height=np.clip(.6+detail*.65,.4,.8);chip=np.zeros_like(height)
dx=(np.roll(height,-1,1)-np.roll(height,1,1))*.5*.0003*size/3
dy=(np.roll(height,-1,0)-np.roll(height,1,0))*.5*.0003*size/3
n=np.stack([-dx,dy,np.ones_like(dx)],2);n/=np.linalg.norm(n,axis=2,keepdims=True)
rough=np.full_like(height,.94);ao=np.clip(1-.25*np.maximum(blur(height,5)-height,0),.9,1);z=np.zeros_like(height)
def save(s,x):Image.fromarray(np.uint8(np.rint(np.clip(x,0,1)*255))).save(p/('WSC01_'+s+'.png'))
save('BaseColor',a);save('Normal_OpenGL',n*.5+.5);save('AO',ao);save('Roughness',rough);save('Metallic',z);save('MetallicSmoothness',np.stack([z,z,z,1-rough],2));save('ORM',np.stack([ao,rough,z],2));Image.fromarray(np.uint16(np.rint(height*65535))).save(p/'WSC01_Height16.png')
report={'revision':'white ceiling plaster','resolution':[size,size],'source_resolution':list(im.size),'estimated_chip_percent':float((chip>.15).mean()*100),'normal_unit_error':float(np.abs(np.linalg.norm(n,axis=2)-1).max()),'height':'approximate subtle plaster relief from albedo high-pass; not measured height'}
(p/'Validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))


