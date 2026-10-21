"""Register clear roof glazing and the painted steel supporting it."""
from pathlib import Path
import json
import numpy as np
from PIL import Image
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit8';A=W/'authoring'
T=A/'Assets/Environment/KursHub/Textures';N=2048
rng=np.random.default_rng(814031);y,x=np.mgrid[0:N,0:N].astype(np.float32)/N
families=[]
for name in ('Hub8SkylightGlass','Hub8SkylightFrame'):
    glass=name.endswith('Glass');noise=rng.normal(0,.9,(N,N))
    if glass:
        # Clear pane body, fine retained wash marks; no opaque marbling.
        wash=np.clip(np.sin(x*73+y*3)*.5+.5,0,1)**14*.025
        alpha=np.clip(.115+wash+noise*.0006,.09,.17)
        base=np.stack([176+noise*.3,191+noise*.3,185+noise*.3],axis=2)
        rough=np.full((N,N),.14,np.float32)+wash
        height=np.zeros((N,N),np.float32);strength=.12
    else:
        speck=np.maximum(noise-1.8,0)
        base=np.stack([209+noise-speck*8,212+noise-speck*10,205+noise-speck*11],axis=2)
        alpha=np.ones((N,N));rough=np.clip(.39+noise*.004,.35,.48)
        height=noise*.0000015;strength=.35
    rgba=np.dstack((np.clip(base,0,255),alpha*255)).astype(np.uint8)
    Image.fromarray(rgba,'RGBA').save(T/(name+'_BaseColor.png'))
    Image.fromarray(np.uint8(rough*255),'L').save(T/(name+'_Roughness.png'))
    Image.new('L',(N,N),0).save(T/(name+'_Metallic.png'))
    Image.new('L',(N,N),255).save(T/(name+'_Occlusion.png'))
    Image.new('RGB',(N,N),(128,128,255)).save(T/(name+'_Normal.png'))
    packed=np.zeros((N,N,4),np.uint8);packed[:,:,3]=np.uint8((1-rough)*255)
    Image.fromarray(packed,'RGBA').save(T/(name+'_MetallicSmoothness.png'))
    lo,hi=float(height.min()),float(height.max())
    norm=(height-lo)/max(hi-lo,1e-9)
    Image.fromarray(np.uint16(norm*65535),'I;16').save(T/(name+'_Height.png'))
    families.append(dict(name=name,resolution=N,periodMetres=2,periodYMetres=2,tiling=1,normalStrength=strength,heightMinMetres=lo,heightMaxMetres=hi,alpha=glass,heightUsage='exchange only; no displacement/parallax in Unity'))
path=T/'Audit7PBRManifest.json';manifest=json.loads(path.read_text(encoding='utf8'))
names={f['name'] for f in families}
manifest['families']=[f for f in manifest['families'] if f['name'] not in names]+families
manifest['revision8Skylight']=dict(glassMeanAlpha=.115,shadowCaster=False,frame='Painted steel, dielectric coating',provenance='Original procedural surfaces; not scans')
path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
for relative in ('KursHubManifest.json','Props/KursHubPropsManifest.json'):
 path=A/'Assets/Environment/KursHub'/relative;obj=json.loads(path.read_text(encoding='utf8'))
 known={p['name'] for p in obj['palette']}
 obj['palette'].extend(dict(name=f['name'],rgba=[1,1,1,1]) for f in families if f['name'] not in known)
 path.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf8')
print('AUDIT8_SKYLIGHT_PBR_OK',sorted(names))
