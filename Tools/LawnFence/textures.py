from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import random, json
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'work/LawnFence/UnityProject/Assets/Environment/Fences/Textures'
N=4096
rng=np.random.default_rng(9431)
random.seed(9431)
def noise(s):
    return np.asarray(Image.fromarray(rng.random((s,s)).astype('float32')).resize((N,N),Image.Resampling.BICUBIC)).astype('float32')
def save(a,folder,name):
    p=OUT/folder/(name+'.png');p.parent.mkdir(parents=True,exist_ok=True)
    Image.fromarray((np.clip(a,0,1)*255).astype('uint8')).save(p,compress_level=3)
for name,threshold in [('Old',.64),('Heavy_Rust',.39)]:
    macro=noise(23)*.30+noise(95)*.32+noise(340)*.26+noise(1100)*.12
    grain=noise(2200)-.5
    # Non-identical patches across strips; fine chips occupy separate frequency bands.
    rust=np.clip((macro-threshold)*35+.45,0,1)
    chip=np.clip((noise(840)-.64)*15,0,1)
    if name=='Old':rust=np.maximum(rust,chip*.42)
    drips=Image.new('L',(N,N));d=ImageDraw.Draw(drips)
    for _ in range(290):
        x=random.randrange(N);y=random.randrange(100,N-700)
        d.line((x,y,x+random.randrange(-8,9),y+random.randrange(70,650)),fill=random.randrange(25,130),width=random.randrange(2,12))
    streak=np.asarray(drips.filter(ImageFilter.GaussianBlur(4)),dtype='float32')/255
    dirt=np.clip((np.linspace(0,1,N,dtype='float32')[:,None]-.87)*10+noise(42)*.20,0,1)
    bare=np.clip((macro-threshold+.025)*35,0,1)*(1-rust)
    base=np.empty((N,N,3),dtype='float32')
    for c,(paint,oxide,earth) in enumerate(zip((.30,.345,.27),(.30,.145,.062),(.105,.088,.058))):
        base[:,:,c]=(paint+(macro-.5)*.095)*(1-rust)+(oxide+(macro-.5)*.15+grain*.04)*rust
        base[:,:,c]=base[:,:,c]*(1-bare*.5)+(.31+c*.012)*bare*.5
        base[:,:,c]*=1-streak*.40
        base[:,:,c]=base[:,:,c]*(1-dirt*.8)+earth*dirt*.8
    save(base,'Albedo',name+'_Albedo');del base
    rough=np.clip(.69+rust*.19+grain*.08+dirt*.09-bare*.28,.3,.98)
    metal=np.clip(bare*.8*(1-dirt),0,1)
    # Cavity AO describes micro-pits; joint occlusion is actual welded geometry.
    ao=np.clip(1-rust*np.maximum(0,-grain)*.35-dirt*.10,.65,1)
    height=(macro-.5)*.16+grain*(.06+rust*.20)-chip*.018
    dx=(np.roll(height,1,1)-np.roll(height,-1,1))*2
    dy=(np.roll(height,1,0)-np.roll(height,-1,0))*2
    norm=np.stack((dx,-dy,np.ones_like(dx)),axis=-1);norm/=np.linalg.norm(norm,axis=2)[:,:,None]
    save(norm*.5+.5,'Normal',name+'_Normal');del norm,dx,dy
    for folder,a in [('Metallic',metal),('Roughness',rough),('AO',ao),('Height',height+.5)]:save(a,folder,name+'_'+folder)
    mask=np.empty((N,N,4),dtype='float32');mask[:,:,0]=metal;mask[:,:,1]=ao;mask[:,:,2]=0;mask[:,:,3]=1-rough
    save(mask,'URP',name+'_MetallicSmoothness')
    del macro,grain,rust,chip,streak,dirt,bare,rough,metal,ao,height,mask
print('LAWN_FENCE_4K_TEXTURES_READY')
