from pathlib import Path
import numpy as np,json
from PIL import Image,ImageFilter
p=Path('Assets/Maps/Buildings/Textures/WeatheredAsphalt_DoubleLine_01');p.mkdir(exist_ok=False);s=2048
im=Image.open('C:/Users/Григорий/.codex/generated_images/01a16020-cae0-78d2-8c66-4d7f34319e8d/exec-64b58f47-6476-4a55-a1c2-28c1df34b20e.png').convert('RGB');a=np.asarray(im.resize((s,s),Image.Resampling.LANCZOS),dtype=np.float32)/255
for axis in (0,1):
 a=a.swapaxes(axis,1);d=(a[:,0]-a[:,-1]).copy()
 for i in range(32):
  t=.5*(1-i/31)**2;a[:,i]-=d*t;a[:,-1-i]+=d*t
 a=a.swapaxes(axis,1)
def blur(v,r):return np.asarray(Image.fromarray(np.uint8(np.clip(v,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),dtype=np.float32)/255
lum=a.mean(2);yy,xx=np.mgrid[:s,:s];zone=abs(xx/s-.5)<.06;paint=np.clip((lum-.63)/.15,0,1)*zone
# Replace bright paint with local road mean before reconstructing shallow road relief.
road=lum*(1-paint)+.43*paint;detail=road-blur(road,8);height=np.clip(.55+detail*.75,.2,.85)+paint*.012
rough=np.clip(.94-.07*paint,.84,.98);ao=np.clip(1-.4*np.maximum(blur(height,10)-height,0),.9,1)
dy,dx=np.gradient(height*.008);dx*=s/6;dy*=s/6;n=np.stack([-dx,dy,np.ones_like(dx)],2);n/=np.linalg.norm(n,axis=2,keepdims=True);z=np.zeros_like(lum)
def save(k,v):Image.fromarray(np.uint8(np.rint(np.clip(v,0,1)*255))).save(p/('WAD01_'+k+'.png'))
for k,v in [('BaseColor',a),('Normal_OpenGL',n*.5+.5),('AO',ao),('Roughness',rough),('Metallic',z),('MetallicSmoothness',np.dstack([z,z,z,1-rough])),('ORM',np.dstack([ao,rough,z])),('PaintMask',paint)]:save(k,v)
Image.fromarray(np.uint16(np.rint(np.clip(height,0,1)*65535))).save(p/'WAD01_Height16.png');(p/'Validation.json').write_text(json.dumps({'resolution':[s,s],'source_resolution':list(im.size),'tile_m':[6,6],'height':'estimated from detail; bright painted line masked to prevent raised ridges','normal_error':float(abs(np.linalg.norm(n,axis=2)-1).max()),'maps':9},indent=2))
