"""Physical 0.5 m red/cream ceramic; dirt collects at joints and chips."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit8'
T=W/'authoring/Assets/Environment/KursHub/Textures'
N=4096;S=256;tiles=16;period=8.;rng=np.random.default_rng(83391)
rgb=np.zeros((N,N,3),np.float32);h=np.zeros((N,N),np.float32);rough=np.zeros((N,N),np.float32);ao=np.ones((N,N),np.float32)
yy,xx=np.mgrid[0:S,0:S].astype(np.float32)
edge=np.minimum(np.minimum(xx+.5,S-.5-xx),np.minimum(yy+.5,S-.5-yy))
grout=edge<2.2;bevel=np.clip((edge-2.2)/2.2,0,1)
joint_dirt=np.exp(-edge/13.)
noise=rng.normal(0,1,(S,S))
for row in range(tiles):
 for col in range(tiles):
    sl=np.s_[row*S:(row+1)*S,col*S:(col+1)*S]
    # Original factory layout, occasional salvaged replacement; no wall noise.
    red=(row+2*col)%5==0 or (row%4==2 and col%5==3)
    base=np.array((121,64,47) if red else (192,185,165),np.float32)
    base+=rng.normal(0,2.5,3)
    surface=base[None,None,:]+noise[:,:,None]*.75
    surface-=joint_dirt[:,:,None]*rng.uniform(9,19)
    # A rubbed tile centre and dirty rim share the same causal height/roughness.
    tileheight=-.0015*(1-bevel)
    tilerough=.67+joint_dirt*.19+noise*.002
    # Fine glaze scratches and small edge chips are material-scale, not gouges.
    mask=Image.new('L',(S,S),0);d=ImageDraw.Draw(mask)
    for _ in range(int(rng.integers(1,5))):
        sx,sy=rng.integers(10,S-10,2);length=int(rng.integers(8,43))
        d.line((int(sx),int(sy),int(sx+length),int(sy+rng.integers(-7,8))),fill=int(rng.integers(50,100)),width=1)
    scratch=np.asarray(mask,dtype=np.float32)/255
    surface-=scratch[:,:,None]*14;tileheight-=scratch*.00014;tilerough+=scratch*.15
    if rng.random()<.3:
        cx=float(rng.choice([3,S-4]));cy=float(rng.uniform(10,S-10))
        chip=np.clip(1-((xx-cx)**2/13**2+(yy-cy)**2/8**2),0,1)
        surface-=chip[:,:,None]*33;tileheight-=chip*.001;tilerough+=chip*.12
    surface[grout]=(57,53,45);tileheight[grout]=-.0017;tilerough[grout]=.94
    rgb[sl]=surface;h[sl]=tileheight;rough[sl]=np.clip(tilerough,.45,.98);ao[sl]=1-joint_dirt*.09
dx=period/N
gx=(np.roll(h,-1,1)-np.roll(h,1,1))/(2*dx);gy=(np.roll(h,-1,0)-np.roll(h,1,0))/(2*dx)
normal=np.dstack((-gx,gy,np.ones_like(h)));normal/=np.linalg.norm(normal,axis=2,keepdims=True)
Image.fromarray(np.uint8(np.clip(rgb,0,255)),'RGB').save(T/'CeramicTile_BaseColor.png')
Image.fromarray(np.uint8(np.clip((normal+1)*127.5,0,255)),'RGB').save(T/'CeramicTile_Normal.png')
Image.fromarray(np.uint8(rough*255),'L').save(T/'CeramicTile_Roughness.png')
Image.fromarray(np.uint8(ao*255),'L').save(T/'CeramicTile_Occlusion.png')
Image.new('L',(N,N),0).save(T/'CeramicTile_Metallic.png')
packed=np.zeros((N,N,4),np.uint8);packed[:,:,3]=np.uint8((1-rough)*255)
Image.fromarray(packed,'RGBA').save(T/'CeramicTile_MetallicSmoothness.png')
lo,hi=float(h.min()),float(h.max())
Image.fromarray(np.uint16(np.clip((h-lo)/max(hi-lo,1e-9),0,1)*65535),'I;16').save(T/'CeramicTile_Height.png')
path=T/'Audit7PBRManifest.json';manifest=json.loads(path.read_text(encoding='utf8'))
spec=next(f for f in manifest['families'] if f['name']=='CeramicTile')
spec.update(resolution=N,periodMetres=period,periodYMetres=period,tiling=2/period,heightMinMetres=lo,heightMaxMetres=hi)
manifest['revision8Ceramic']=dict(tileMetres=.5,jointWidthMetres=.0086,description='Cream/red fired ceramic, localized joint dirt, glaze scratches and sparse edge chips; original procedural texture, not a scan',unchangedUVPeriod=True)
path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
(W/'ceramic_report.json').write_text(json.dumps(manifest['revision8Ceramic'],indent=2),encoding='utf8')
print('AUDIT8_CERAMIC_4K_OK',lo,hi,'metres; unchanged UV scale .25')
