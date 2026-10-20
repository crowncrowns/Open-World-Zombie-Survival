"""Original cutout fir spray atlas; explicit alpha, physical roughness and normals."""
from pathlib import Path
import numpy as np,json,sys,random
from PIL import Image,ImageDraw,ImageFilter
R=Path(__file__).resolve().parents[2];A=R/'work/KursHub/audit7/authoring';D=A/'Assets/Environment/KursHub/Textures'
sys.path.insert(0,str(Path(__file__).parent));from audit7_surfaces import write_png,normal_map,record
n=1024;hi=2048;im=Image.new('RGBA',(hi,hi),(22,53,14,0));dr=ImageDraw.Draw(im);rng=random.Random(227)
dr.line([(hi*.50,hi*.96),(hi*.50,hi*.08)],fill=(48,65,24,255),width=10)
for j in range(46):
    t=j/46;y=hi*(.91-.79*t);spread=hi*(.31*(1-t*.72))
    for side in (-1,1):
        for k in range(2):
            end=(hi*.5+side*spread*rng.uniform(.65,1),y-hi*rng.uniform(.045,.12))
            dr.line([(hi*.5+side*k*4,y+rng.uniform(-5,5)),end],fill=(rng.randint(38,53),rng.randint(70,103),rng.randint(18,33),255),width=rng.randint(7,12))
im=im.resize((n,n),Image.Resampling.LANCZOS);base=np.asarray(im).astype(np.float32)/255
height=np.asarray(im.getchannel('A').filter(ImageFilter.GaussianBlur(.65)),dtype=np.float32)/255*.00018
rough=np.full((n,n),.72,np.float32);metal=np.zeros_like(rough);ao=np.ones_like(rough);packed=np.zeros((n,n,4),np.float32);packed[...,3]=1-rough
data=dict(BaseColor=base,Normal=normal_map(height,.28,.22),Roughness=rough,Metallic=metal,MetallicSmoothness=packed,Occlusion=ao,Height=height/.00018)
for s,array in data.items():write_png(array,D/('FirNeedles_'+s+'.png'),height=s=='Height')
p=D/'Audit7PBRManifest.json';d=json.loads(p.read_text(encoding='utf8'));d['families']=[f for f in d['families'] if f['name']!='FirNeedles']
d['families'].append(dict(name='FirNeedles',resolution=n,periodMetres=.22,periodYMetres=.28,tiling=1,normalStrength=1,heightMinMetres=0,heightMaxMetres=.00018,alpha=True,uv='Unique cutout needle spray; alpha clip .4, crossed volumetric fronds'))
d['maps']=[m for m in d['maps'] if not m['file'].startswith('FirNeedles_')]+[record(D/('FirNeedles_'+s+'.png')) for s in data]
p.write_text(json.dumps(d,indent=2),encoding='utf8');(R/'work/KursHub/audit7/reports/PBR_After_Manifest.json').write_text(json.dumps(d,indent=2),encoding='utf8')
mp=A/'Assets/Environment/KursHub/KursHubManifest.json';md=json.loads(mp.read_text(encoding='utf8'))
if not any(p['name']=='FirNeedles' for p in md['palette']):md['palette'].append(dict(name='FirNeedles',rgba=[.13,.28,.06,1]))
mp.write_text(json.dumps(md,indent=2),encoding='utf8');print('FIR_ATLAS_OK',len(d['families']),len(d['maps']))
