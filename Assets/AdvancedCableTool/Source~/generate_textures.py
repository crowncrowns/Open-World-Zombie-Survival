"""Deterministic periodic 2K PBR source; no downloaded images or flat swatches."""
from pathlib import Path
import numpy as np
from scipy.ndimage import gaussian_filter
from PIL import Image
import argparse

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output',type=Path,help='Output Textures folder (optional)')
args=parser.parse_args()
script=Path(__file__).resolve()
default=script.parents[1]/'Textures' if script.parent.name=='Source~' else script.parents[2]/'work/AdvancedCableTool/UnityProject/Assets/AdvancedCableTool/Textures'
ROOT=args.output.resolve() if args.output else default
N = 2048
rng = np.random.default_rng(57126)
y, x = np.mgrid[0:N, 0:N].astype(np.float32) / (N-1)

def noise(scale):
    a = rng.random((N,N), dtype=np.float32)
    a = gaussian_filter(a, scale, mode='wrap')
    return (a-a.min())/(a.max()-a.min())

macro, mid, fine = noise(60), noise(6), noise(.65)
for kind, base in [('Electric',(.046,.049,.053)),('Power',(.32,.34,.35)),('Soviet',(.21,.16,.105)),
                   ('Steel',(.22,.235,.24)),('Industrial',(.057,.063,.068)),('Damaged',(.17,.115,.065))]:
    for wi, wear in enumerate(['New','Used','Damaged']):
        name=f'{kind}_{wear}'
        # Integer-frequency fields provide periodic grain, fissures, stranding and scratches.
        fissure = np.exp(-np.abs(np.sin(2*np.pi*(13*x+3*y+.06*np.sin(2*np.pi*7*y))))*180)
        fissure *= np.clip((mid-.47)*6,0,1) * (.1+wi*.4)
        scratches = np.exp(-np.abs(np.sin(2*np.pi*(49*x+y)))*220) * np.clip((macro-.45)*4,0,1)
        ribs = (.5+.5*np.cos(2*np.pi*32*y))**6 if kind=='Industrial' else np.zeros_like(x)
        rope = (.5+.5*np.cos(2*np.pi*(7*x-4*y)))**7 if kind=='Steel' else np.zeros_like(x)
        rust = np.clip((macro-.45)*4,0,1) * (.12+wi*.38) if kind=='Steel' else np.zeros_like(x)
        exposed = np.clip((np.cos(2*np.pi*(2*x+3*y))-.85)*10,0,1) * np.clip((macro-.4)*6,0,1) if kind=='Damaged' else np.zeros_like(x)
        dirt = np.clip((macro-.55)*3,0,1) * wi*.12
        height = fine*.018+mid*.025-fissure*.055 + ribs*.1 + rope*.12 - scratches*.012 - rust*.035-exposed*.08
        color = np.array(base)[None,None,:]*(.7+macro[...,None]*.5+fine[...,None]*.12)
        color *= (1-fissure[...,None]*.7-dirt[...,None])
        color += scratches[...,None]*(.015+wi*.018)+ribs[...,None]*.022
        color = color*(1-rust[...,None])+np.array([.32,.105,.035])*rust[...,None]
        color = color*(1-exposed[...,None])+np.array([.58,.25,.09])*exposed[...,None]
        metallic = np.clip((.87-rust*.8) if kind=='Steel' else exposed*.9,0,1)
        rough = np.clip(.68+mid*.16+fissure*.12+wi*.025-rust*.1-exposed*.35, .2,.98)
        if kind=='Steel': rough=np.clip(.32+mid*.2+rust*.42,.2,.98)
        ao=np.clip(1-fissure*.6-rope*.18-ribs*.15-exposed*.28,.25,1)
        dx=(np.roll(height,-1,1)-np.roll(height,1,1))*12
        dy=(np.roll(height,-1,0)-np.roll(height,1,0))*12
        norm=np.stack([-dx,-dy,np.ones_like(x)],-1); norm/=np.linalg.norm(norm,axis=-1)[...,None]
        norm=norm*.5+.5
        packed=np.stack([np.broadcast_to(metallic,x.shape),np.zeros_like(x),np.zeros_like(x),1-rough],-1)
        maps={'BaseColor':color,'Normal':norm,'Metallic':np.broadcast_to(metallic,x.shape),'Roughness':rough,'AO':ao,'MetallicSmoothness':packed}
        for channel, data in maps.items():
            folder=ROOT/channel; folder.mkdir(parents=True,exist_ok=True)
            data=np.clip(data*255,0,255).astype(np.uint8)
            # Exact wrap seam in every delivered map, including normal and packed masks.
            data[-1]=data[0]; data[:,-1]=data[:,0]
            Image.fromarray(data).save(folder/f'{name}.png',compress_level=3)
        print(name,flush=True)
