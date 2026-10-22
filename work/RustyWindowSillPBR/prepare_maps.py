from pathlib import Path
import sys,json
import numpy as np
from PIL import Image
source,height_source,target=map(Path,sys.argv[1:4])
if target.exists() and any(target.iterdir()):raise RuntimeError('Refuse to overwrite existing pack')
target.mkdir(parents=True,exist_ok=True)
im=Image.open(source).convert('RGB');hi=Image.open(height_source).convert('L').resize(im.size,Image.Resampling.LANCZOS)
size=2048
def resize(a):
 if a.ndim==2:return np.asarray(Image.fromarray(a.astype('float32')).resize((size,size),Image.Resampling.BICUBIC),dtype=np.float32)
 return np.stack([resize(a[:,:,i]) for i in range(a.shape[2])],axis=2)
def periodic(a,band=48):
 a=a.copy()
 for axis in (1,0):
  b=np.swapaxes(a,0,axis);d=(b[0]-b[-1]).copy()
  for i in range(band):
   w=.5*(1-i/(band-1))**2;b[i]-=w*d;b[-1-i]+=w*d
 return np.clip(a,0,1)
def blur(a,sigma):
 fy=np.fft.fftfreq(a.shape[0])[:,None];fx=np.fft.rfftfreq(a.shape[1])[None,:]
 return np.fft.irfft2(np.fft.rfft2(a)*np.exp(-2*np.pi**2*sigma**2*(fx**2+fy**2)),s=a.shape).real.astype(np.float32)
a=periodic(resize(np.asarray(im,dtype=np.float32)/255));h=periodic(resize(np.asarray(hi,dtype=np.float32)/255));h=blur(h,1.4)
h=np.clip((h-np.percentile(h,1))/max(float(np.percentile(h,99)-np.percentile(h,1)),.01),0,1);h=periodic(h)
lum=a.mean(axis=2);warm=a[:,:,0]-(a[:,:,1]+a[:,:,2])*.5
rust=np.clip((warm-.012)/.075,0,1);rust=blur(rust,1.3)
steel=np.clip((lum-.105)/.13,0,1)*(1-rust)**2
metallic=np.clip(steel*.96,0,1)
rough=np.clip(.69+.22*rust-.30*steel+.04*(blur(h,5)-h),.34,.96)
ao=np.clip(1-.22*np.maximum(blur(h,12)-h,0),.88,1)
dx=(np.roll(h,-1,axis=1)-np.roll(h,1,axis=1))*.5;dy=(np.roll(h,-1,axis=0)-np.roll(h,1,axis=0))*.5
tile=.5;relief=.0006
n=np.stack([-dx*size*relief/tile,dy*size*relief/tile,np.ones_like(h)],axis=2);n/=np.linalg.norm(n,axis=2,keepdims=True)
def u8(x):return np.uint8(np.rint(np.clip(x,0,1)*255))
def save(name,x):Image.fromarray(u8(x)).save(target/('RWS01_'+name+'.png'))
save('BaseColor',a);save('Normal_OpenGL',n*.5+.5);Image.fromarray(np.uint16(np.rint(h*65535))).save(target/'RWS01_Height16.png')
save('AO',ao);save('Roughness',rough);save('Metallic',metallic)
save('MetallicSmoothness',np.stack([metallic,metallic,metallic,1-rough],axis=2));save('ORM',np.stack([ao,rough,metallic],axis=2))
validation={'resolution':[size,size],'generated_source_resolution':list(im.size),'basecolor_horizontal_edge_error':int(np.abs(u8(a)[:,0].astype(int)-u8(a)[:,-1].astype(int)).max()),'basecolor_vertical_edge_error':int(np.abs(u8(a)[0].astype(int)-u8(a)[-1].astype(int)).max()),'normal_max_unit_error':float(np.abs(np.linalg.norm(n,axis=2)-1).max()),'physical_tile_m':tile,'relief_m':relief,'note':'AI-authored appearance and height; 2K resampling; derived tangent normals; estimated roughness, AO and exposed-metal mask, not measured scans.'}
(target/'Validation.json').write_text(json.dumps(validation,indent=2),encoding='utf-8');print(json.dumps(validation,indent=2))
preview=Image.fromarray(u8(a)).resize((600,600));sheet=Image.new('RGB',(1200,1200))
for y in range(2):
 for x in range(2):sheet.paste(preview,(x*600,y*600))
sheet.save('work/RustyWindowSillPBR/tiling-preview.jpg',quality=94)
