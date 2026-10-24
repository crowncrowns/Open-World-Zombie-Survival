from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
import gc
ROOT=Path(__file__).resolve().parents[2]/'work/PotbellyStove/UnityProject/Assets/Environment/Props/PotbellyStove/Textures'
ROOT.mkdir(parents=True,exist_ok=True)
N=4096
rng=np.random.default_rng(231026)
def noise(s):
    return np.asarray(Image.fromarray(rng.random((s,s)).astype('float32')).resize((N,N),Image.Resampling.BICUBIC)).copy()
def save(a,n):
    Image.fromarray(np.uint8(np.clip(a,0,1)*255)).save(ROOT/(n+'.png'),compress_level=3)
macro=noise(19)*.50+noise(83)*.30+noise(410)*.20
fine=noise(1800)
rust=np.clip((macro-.40)*6,0,1)
streak=np.asarray(Image.fromarray(rng.random((128,1024)).astype('float32')).resize((N,N),Image.Resampling.BICUBIC))
scratch=Image.new('L',(N,N));d=ImageDraw.Draw(scratch)
for _ in range(2200):
    x,y=rng.integers(0,N,2);d.line((int(x),int(y),int(x+rng.integers(5,110)),int(y+rng.integers(-5,6))),fill=int(rng.integers(20,170)),width=1)
scr=np.asarray(scratch,dtype=np.float32)/255
u=np.arange(N,dtype=np.float32)[None,:]/N;v=np.arange(N,dtype=np.float32)[:,None]/N
# Blender cylindrical UV: front seam centred at u=.5; PNG top corresponds to highest V.
soot=np.exp(-((u-.5)/.16)**2-((v-.60)/.26)**2)*.7
col=np.empty((N,N,3),np.float32)
for c,(metal,oxide) in enumerate(zip((.085,.093,.088),(.32,.135,.048))):
    col[:,:,c]=(metal*(1-rust)+oxide*rust)*( .65+macro*.7)*(1-soot)+scr*.09+(streak-.5)*.026
save(col,'Stove_BaseColor');del col
height=rust*.12+(fine-.5)*.11-scr*.15+streak*.02
dx=(np.roll(height,1,1)-np.roll(height,-1,1))*3;dy=(np.roll(height,1,0)-np.roll(height,-1,0))*3
normal=np.stack((dx,-dy,np.ones_like(dx)),axis=-1);normal/=np.linalg.norm(normal,axis=-1)[:,:,None]
save(normal*.5+.5,'Stove_Normal');del normal,dx,dy,height
rough=np.clip(.48+rust*.43+fine*.06-scr*.2,.25,.98);metal=(1-rust)*.82
ao=np.clip(.90+macro*.18-soot*.16,.45,1)
save(rough,'Stove_Roughness');save(metal,'Stove_Metallic');save(ao,'Stove_AO');save(np.zeros((N,N),np.float32),'Stove_Emission')
save(np.stack((metal,ao,np.zeros_like(metal),1-rough),axis=-1),'Stove_UnityMask')
del rough,metal,ao,soot,streak,scr,macro,rust;gc.collect()
# Carbonised wood with glowing fissures, rather than solid orange logs.
grain=noise(75)*.4+noise(700)*.6
cracks=np.maximum(0, .17-np.abs(np.sin(u*157+np.sin(v*83)*.8+grain*3))) / .17
cracks*=np.clip((grain-.38)*5,0,1)
base=np.stack((.035+grain*.042,.022+grain*.032,.016+grain*.026),axis=-1)
save(base,'Embers_BaseColor');save(np.stack((cracks,cracks*.2,cracks*.015),axis=-1),'Embers_Emission')
# Particle masks have irregular, tapered silhouettes and no opaque card edges.
S=512;y,x=np.mgrid[0:S,0:S].astype('float32')/(S-1);xx=x*2-1
flame=np.clip(1-np.abs(xx+np.sin(y*16)*.12)/( .06+(1-y)*.62),0,1)**1.3*np.sin(np.pi*y)**.65
smoke=np.exp(-(xx**2+(y*2-1)**2)*4.5)*( .70+.15*np.sin(x*27+y*16)+.1*np.cos(y*39-x*18))
for name,alpha in [('Flame_Opacity',flame),('Smoke_Opacity',smoke)]:
    rgba=np.ones((S,S,4),np.float32);rgba[:,:,3]=alpha
    save(rgba,name)
print('STOVE_TEXTURES_READY')
