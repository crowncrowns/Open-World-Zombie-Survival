from pathlib import Path
import json,sys
import numpy as np
from PIL import Image

source=Path(sys.argv[1]); height_source=Path(sys.argv[2]); target=Path(sys.argv[3])
if target.exists() and any(target.iterdir()): raise RuntimeError('Refuse to overwrite existing texture pack')
target.mkdir(parents=True,exist_ok=True)
img=Image.open(source).convert('RGB'); raw=np.asarray(img,dtype=np.float32)/255
hh=Image.open(height_source).convert('L').resize(img.size,Image.Resampling.LANCZOS)
hraw=np.asarray(hh,dtype=np.float32)/255
# Tile bounds end in mortar, preserving the source brick geometry in every map.
redness=raw[:,:,0]-(raw[:,:,1]+raw[:,:,2])*.5
profile=redness.mean(axis=1)
lo=int(len(profile)*.94); bottom=lo+int(np.argmin(profile[lo:-4]))
top=int(np.argmin(profile[:max(8,int(len(profile)*.012))]))
if bottom-top < len(profile)*.9: raise RuntimeError('Invalid tile crop')
raw=raw[top:bottom]; hraw=hraw[top:bottom]
size=2048
def resize(a):
    if a.ndim==2: return np.asarray(Image.fromarray(a.astype('float32'),'F').resize((size,size),Image.Resampling.BICUBIC),dtype=np.float32)
    return np.stack([resize(a[:,:,i]) for i in range(a.shape[2])],axis=2)
def periodic(a,band=40):
    a=a.copy(); n=a.shape[1]; d=(a[:,0]-a[:,-1]).copy()
    for j in range(band):
        w=.5*(1-j/(band-1))**2
        a[:,j]-=d*w; a[:,n-1-j]+=d*w
    n=a.shape[0];d=(a[0]-a[-1]).copy()
    for j in range(band):
        w=.5*(1-j/(band-1))**2
        a[j]-=d*w;a[n-1-j]+=d*w
    return np.clip(a,0,1)
def blur_wrap(a,sigma):
    f=np.fft.fftfreq(a.shape[0])[:,None];g=np.fft.rfftfreq(a.shape[1])[None,:]
    return np.fft.irfft2(np.fft.rfft2(a)*np.exp(-2*np.pi**2*sigma*sigma*(f*f+g*g)),s=a.shape).real.astype(np.float32)
albedo=periodic(resize(raw))
height=periodic(resize(hraw))
height=blur_wrap(height,1.0)
# Verify that the AI height interpretation agrees with mortar positions, not brightness.
red=albedo[:,:,0]-(albedo[:,:,1]+albedo[:,:,2])*.5
brick=np.clip((blur_wrap(red,2)-.025)/.09,0,1)
corr=float(np.corrcoef(brick.ravel(),height.ravel())[0,1])
if corr < .25: raise RuntimeError(f'Height map failed registration/semantics check: {corr:.3f}')
height=np.clip((height-np.percentile(height,1))/(np.percentile(height,99)-np.percentile(height,1)),0,1)
height=periodic(height)
# Tangent-space +Y normal: image rows grow downward, so Ny uses +dH/dRow.
dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*.5
dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*.5
physical_tile=1.85; relief_metres=.012
normal=np.stack([-dx*size*relief_metres/physical_tile,dy*size*relief_metres/physical_tile,np.ones_like(height)],axis=2)
normal/=np.linalg.norm(normal,axis=2,keepdims=True)
rough=np.clip(.78+.14*(1-brick)+.05*(blur_wrap(height,3)-height),.70,.96)
# Height-derived cavity AO: a game-ready approximation, not measured illumination.
ao=np.clip(1-1.1*np.maximum(blur_wrap(height,16)-height,0)-.10*(1-brick),.48,1)
def u8(a):return np.uint8(np.rint(np.clip(a,0,1)*255))
def save(name,a):Image.fromarray(u8(a)).save(target/('WBR01_'+name+'.png'))
save('BaseColor',albedo);save('Normal_OpenGL',normal*.5+.5)
Image.fromarray(np.uint16(np.rint(height*65535))).save(target/'WBR01_Height16.png')
save('Roughness',rough);save('AO',ao)
save('MetallicSmoothness',np.stack([np.zeros_like(rough),np.zeros_like(rough),np.zeros_like(rough),1-rough],axis=2))
save('ORM',np.stack([ao,rough,np.zeros_like(rough)],axis=2))
check={"resolution":[size,size],"generated_source_resolution":list(img.size),"source_crop_y":[top,bottom],"height_albedo_semantic_correlation":corr,"normal_max_unit_length_error":float(np.max(np.abs(np.linalg.norm(normal,axis=2)-1))),"basecolor_edge_error_horizontal":int(np.abs(u8(albedo)[:,0].astype(int)-u8(albedo)[:,-1].astype(int)).max()),"basecolor_edge_error_vertical":int(np.abs(u8(albedo)[0].astype(int)-u8(albedo)[-1].astype(int)).max()),"physical_tile_metres":physical_tile,"height_relief_metres":relief_metres,"note":"AI source; 2K resampling; normals numerically derived from AI height; AO/roughness estimated, not measured."}
(target/'Validation.json').write_text(json.dumps(check,indent=2),encoding='utf-8')
print(json.dumps(check,indent=2))
