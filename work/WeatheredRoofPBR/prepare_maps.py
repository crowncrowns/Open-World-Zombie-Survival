from pathlib import Path
import sys,json
import numpy as np
from PIL import Image
source,target=map(Path,sys.argv[1:3]);size=2048
if target.exists() and any(target.iterdir()):raise RuntimeError('Refuse to overwrite existing pack')
target.mkdir(parents=True,exist_ok=True)
im=Image.open(source).convert('RGB');a=np.asarray(im.resize((size,size),Image.Resampling.LANCZOS),dtype=np.float32)/255
def periodic(a,band=48):
 a=a.copy()
 for axis in (1,0):
  b=np.swapaxes(a,0,axis);d=(b[0]-b[-1]).copy()
  for i in range(band):
   w=.5*(1-i/(band-1))**2;b[i]-=w*d;b[-1-i]+=w*d
 return a
def blur(a,sigma):
 fy=np.fft.fftfreq(size)[:,None];fx=np.fft.rfftfreq(size)[None,:]
 return np.fft.irfft2(np.fft.rfft2(a)*np.exp(-2*np.pi**2*sigma*sigma*(fx*fx+fy*fy)),s=a.shape).real.astype(np.float32)
a=np.clip(periodic(a),0,1);lum=a.mean(axis=2)
# A controlled physical profile gives coherent corrugations in all technical maps.
u=np.arange(size,dtype=np.float32)[None,:]/size
wave=np.broadcast_to(.5+.5*np.cos(2*np.pi*8*u),(size,size)).copy()
micro=blur(lum,1)-blur(lum,6);micro=np.clip(micro,-.1,.1)
height_m=wave*.04+micro*.003
h=np.clip(height_m/.04,0,1)
dx=(np.roll(height_m,-1,axis=1)-np.roll(height_m,1,axis=1))*.5*size/1.2
dy=(np.roll(height_m,-1,axis=0)-np.roll(height_m,1,axis=0))*.5*size/1.2
n=np.stack([-dx,dy,np.ones_like(dx)],axis=2);n/=np.linalg.norm(n,axis=2,keepdims=True)
rough=np.clip(.84+.12*np.clip((.53-lum)/.4,0,1),.80,.97)
ao=np.clip(.80+.20*wave-.08*np.maximum(blur(lum,10)-lum,0),.75,1)
metal=np.zeros_like(lum)
def u8(x):return np.uint8(np.rint(np.clip(x,0,1)*255))
def save(name,x):Image.fromarray(u8(x)).save(target/('WCR01_'+name+'.png'))
save('BaseColor',a);save('Normal_OpenGL',n*.5+.5);Image.fromarray(np.uint16(np.rint(h*65535))).save(target/'WCR01_Height16.png')
save('AO',ao);save('Roughness',rough);save('Metallic',metal)
save('MetallicSmoothness',np.stack([metal,metal,metal,1-rough],axis=2));save('ORM',np.stack([ao,rough,metal],axis=2))
validation={'resolution':[size,size],'generated_source_resolution':list(im.size),'tile_metres':[1.2,1.2],'corrugations':8,'wave_pitch_m':.15,'crest_to_trough_m':.04,'basecolor_horizontal_edge_error':int(np.abs(u8(a)[:,0].astype(int)-u8(a)[:,-1].astype(int)).max()),'basecolor_vertical_edge_error':int(np.abs(u8(a)[0].astype(int)-u8(a)[-1].astype(int)).max()),'normal_max_unit_error':float(np.abs(np.linalg.norm(n,axis=2)-1).max()),'note':'AI albedo; analytic corrugation height/normal profile; roughness and AO estimated, not a scan or a measurement of the pictured roof.'}
(target/'Validation.json').write_text(json.dumps(validation,indent=2),encoding='utf-8');print(json.dumps(validation,indent=2))
# A 2x2 technical relief preview, directional shading for inspection only, not albedo.
light=np.array([-.6,.3,.74],dtype=np.float32);light/=np.linalg.norm(light)
shade=np.clip((n*light).sum(axis=2),0,1)*.65+.35
preview=Image.fromarray(u8(a*shade[:,:,None])).resize((600,600));out=Image.new('RGB',(1200,1200))
for y in range(2):
 for x in range(2):out.paste(preview,(x*600,y*600))
out.save('work/WeatheredRoofPBR/relief-preview.jpg',quality=95)
