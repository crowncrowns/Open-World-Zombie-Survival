"""Deterministic 4K trim atlases: aged coating, failed coating, exposed corrosion.
Rust follows trim ends (joints), lower drainage bands and elongated leak paths.
Maps describe surface relief; AO is procedural micro-occlusion, not a cavity bake.
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'work/ModularRoofKit/UnityProject/Assets/Environment/ModularRoofKit/Textures'
N=4096
rng=np.random.default_rng(20461024)
def noise(n):
    return np.array(Image.fromarray(rng.random((n,n),dtype=np.float32)).resize((N,N),Image.Resampling.BICUBIC),dtype=np.float32)
def save(a,p):
    Image.fromarray(np.uint8(np.clip(a,0,1)*255)).save(p,compress_level=6)
for folder,color in [('Roof',(.67,.69,.66)),('Gutters',(.31,.30,.26)),('Fences',(.76,.75,.68))]:
    p=OUT/folder;p.mkdir(parents=True,exist_ok=True)
    macro=noise(25);mid=noise(180);fine=noise(1100)
    yy,xx=np.mgrid[0:N,0:N].astype(np.float32)/N
    # U carries circumference / panel width. V carries length; joints use V ends.
    joint=np.exp(-np.minimum(yy,1-yy)*35)
    basin=np.exp(-((xx-.5)/.17)**2) if folder=='Gutters' else np.exp(-((xx-.04)/.05)**2)
    drips=Image.new('L',(N,N));d=ImageDraw.Draw(drips)
    for j in range(115):
        x=int(rng.integers(N));y=int(rng.choice([0,N-1]));end=int(rng.integers(130,1500))*(1 if y==0 else -1)
        d.line((x,y,x+int(rng.integers(-20,20)),y+end),fill=int(rng.integers(45,185)),width=int(rng.integers(2,12)))
    leak=np.asarray(drips.filter(ImageFilter.GaussianBlur(5)),dtype=np.float32)/255
    abrasion=np.clip((mid-.56)*5,0,1)
    rust=np.clip((macro-.73)*4+joint*(.1+.7*mid)+basin*.18+leak*.5,0,1)
    peel=np.clip((macro*.55+mid*.45-.52)*8,0,1)
    h=(mid-.5)*.025+(fine-.5)*.010-rust*.035-abrasion*.009
    dx=np.roll(h,1,1)-np.roll(h,-1,1);dy=np.roll(h,1,0)-np.roll(h,-1,0)
    normal=np.stack((dx*4,-dy*4,np.ones_like(dx)),axis=-1);normal/=np.linalg.norm(normal,axis=2)[:,:,None]
    save(normal*.5+.5,p/(folder+'_Normal.png'));del normal,dx,dy
    save(.5+h,p/(folder+'_Height.png'))
    ao=np.clip(1-rust*.22-joint*.06-abrasion*.12,.5,1);save(ao,p/(folder+'_AO.png'))
    for state in range(3):
        seed=np.clip((mid-.54)*5,0,1)
        if state==0:r=np.clip(seed*(joint*.22+basin*.10)+leak*.10+np.maximum(macro-.86,0)*.9,0,.35)
        elif state==1:r=np.clip(seed*(joint*.55+basin*.22)+np.maximum(macro-.74,0)*1.5+leak*.28,0,.65)
        else:r=np.clip(seed*(joint*.95+basin*.6)+np.clip((macro-.63)*4,0,.75)+leak*.5,0,1)
        base=np.empty((N,N,3),np.float32)
        for c in range(3):
            base[:,:,c]=color[c]*(.88+macro*.19+fine*.035)*(1-r)+[.24,.105,.035][c]*(.65+mid*.6)*r
        save(base,p/(folder+f'_{state}_BaseColor.png'));del base
        rough=np.clip(.62+state*.06+r*.24+(fine-.5)*.06+leak*.05,.32,.97)
        coated=.12 if folder=='Roof' else (.82 if state==0 else .04) if folder=='Gutters' else .04
        metallic=np.clip(coated*(1-r)+peel*.24*(1-r),0,1)
        save(rough,p/(folder+f'_{state}_Roughness.png'));save(metallic,p/(folder+f'_{state}_Metallic.png'))
        mask=np.stack((metallic,ao,np.zeros_like(ao),1-rough),axis=-1)
        save(mask,p/(folder+f'_{state}_UnityMask.png'));del mask,r,rough,metallic
    print('TEXTURE_ATLAS_COMPLETE',folder,flush=True)
