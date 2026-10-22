from pathlib import Path
import sys,json
import numpy as np
from PIL import Image
source,hs,target=map(Path,sys.argv[1:4]);w,h=1024,2048
if target.exists() and any(target.iterdir()):raise RuntimeError('Refuse overwrite')
target.mkdir(parents=True,exist_ok=True)
im=Image.open(source).convert('RGB');a=np.asarray(im.resize((w,h),Image.Resampling.LANCZOS),dtype=np.float32)/255
hei=Image.open(hs).convert('L').resize((w,h),Image.Resampling.LANCZOS);height=np.asarray(hei,dtype=np.float32)/255
def blur(x,r):
 # Edge padding avoids wrapping this nonrepeating complete door leaf.
 from PIL import ImageFilter
 return np.asarray(Image.fromarray(np.uint8(np.clip(x,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),dtype=np.float32)/255
# Keep inset plateaus geometrically flat: remove AI lighting gradients inside panel centers.
for x0,x1,y0,y1 in [(0.13,0.42,0.065,0.485),(0.58,0.87,0.065,0.485),(0.13,0.42,0.635,0.885),(0.58,0.87,0.635,0.885)]:
 xx0,xx1,yy0,yy1=int(x0*w),int(x1*w),int(y0*h),int(y1*h)
 region=height[yy0:yy1,xx0:xx1];yy,xx=np.indices(region.shape)
 fade=np.clip(np.minimum.reduce([xx,region.shape[1]-1-xx,yy,region.shape[0]-1-yy])/6,0,1)
 height[yy0:yy1,xx0:xx1]=region*(1-fade)+.30*fade
height=blur(height,1.7);height=np.clip((height-np.percentile(height,1))/max(float(np.percentile(height,99)-np.percentile(height,1)),.01),0,1)
lum=a.mean(axis=2);r,g,b=a[:,:,0],a[:,:,1],a[:,:,2]
warm=np.clip((r-b-.035)/.15,0,1);rust=np.clip((g/np.maximum(r,.02)-.52)/.25,0,1)*warm
neutral=1-np.clip((np.max(a,axis=2)-np.min(a,axis=2))/.13,0,1)
steel=np.clip((lum-.10)/.23,0,1)*neutral;metallic=np.zeros_like(lum)
dirt=np.clip((.82-lum)/.5,0,1);rough=np.clip(.61+.25*dirt,.59,.92)
ao=np.clip(1-.65*np.maximum(blur(height,18)-height,0),.65,1)
dy,dx=np.gradient(height*.012);dx*=w/.9;dy*=h/2.1
n=np.stack([-dx,dy,np.ones_like(dx)],axis=2);n/=np.linalg.norm(n,axis=2,keepdims=True)
def u8(x):return np.uint8(np.rint(np.clip(x,0,1)*255))
def save(name,x):Image.fromarray(u8(x)).save(target/('WID01_'+name+'.png'))
save('BaseColor',a);save('Normal_OpenGL',n*.5+.5);Image.fromarray(np.uint16(np.rint(height*65535))).save(target/'WID01_Height16.png')
save('AO',ao);save('Roughness',rough);save('Metallic',metallic);save('MetallicSmoothness',np.stack([metallic,metallic,metallic,1-rough],axis=2));save('ORM',np.stack([ao,rough,metallic],axis=2))
check={'resolution':[w,h],'source_resolution':list(im.size),'mapping':'one complete portrait door leaf; Clamp; UV 0..1; no tiling','normal_max_unit_error':float(np.abs(np.linalg.norm(n,axis=2)-1).max()),'physical_size_assumption_m':[.9,2.1],'relief_m':.012,'note':'AI basecolor and height; computed normals; estimated AO/roughness/metallic; not a scan.'}
(target/'Validation.json').write_text(json.dumps(check,indent=2),encoding='utf-8');print(json.dumps(check,indent=2))


