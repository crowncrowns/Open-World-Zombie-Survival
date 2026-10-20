"""A unique 1K leaf PBR set: veins in physical height, UV across/along leaf."""
from pathlib import Path
import json,sys
import numpy as np
R=Path(__file__).resolve().parents[2];A=R/'work/KursHub/audit7/authoring';folder=A/'Assets/Environment/KursHub/Textures'
sys.path.insert(0,str(Path(__file__).parent))
from audit7_surfaces import normal_map,write_png,record
n=1024;v,u=np.meshgrid(np.arange(n,dtype=np.float32)/(n-1),np.arange(n,dtype=np.float32)/(n-1),indexing='ij');rng=np.random.default_rng(71025)
centre=np.exp(-((u-.5)/.008)**2)
veins=np.zeros_like(u)
for row in (.15,.29,.43,.57,.71,.85):
    line=row+abs(u-.5)*.65
    veins=np.maximum(veins,np.exp(-((v-line)/.006)**2)*np.clip(1-abs(u-.5)*2,0,1))
grain=rng.random((n,n),dtype=np.float32)-.5
height=centre*.00018+veins*.000065+grain*.000003
variation=(np.sin(v*8)*.006+np.cos(u*14)*.004+grain*.012+centre*.014+veins*.008)
base=np.clip(np.array((.16,.32,.073),np.float32)[None,None,:]+variation[...,None],0,1)
rough=np.clip(.66+grain*.045-centre*.08-veins*.025,0,1)
# Raised veins receive their lighting from the physical normal. Do not paint
# dark occlusion onto their crests; a thin unobstructed blade has neutral AO.
metal=np.zeros_like(u);ao=np.ones_like(u)
packed=np.zeros((n,n,4),np.float32);packed[...,0]=metal;packed[...,3]=1-rough
data=dict(BaseColor=base,Normal=normal_map(height,.32,.13),Roughness=rough,Metallic=metal,MetallicSmoothness=packed,Occlusion=ao,Height=(height-height.min())/(height.max()-height.min()))
for suffix,array in data.items():write_png(array,folder/('LeafGreen_'+suffix+'.png'),height=suffix=='Height')
path=folder/'Audit7PBRManifest.json';manifest=json.loads(path.read_text(encoding='utf8'))
manifest['families']=[f for f in manifest['families'] if f['name']!='LeafGreen']
manifest['families'].append(dict(name='LeafGreen',resolution=n,periodMetres=.13,periodYMetres=.32,tiling=1,normalStrength=1,heightMinMetres=float(height.min()),heightMaxMetres=float(height.max()),alpha=False,uv='Unique leaf U across width; V along stem to tip'))
manifest['maps']=[m for m in manifest['maps'] if not m['file'].startswith('LeafGreen_')]+[record(folder/('LeafGreen_'+suffix+'.png')) for suffix in data]
manifest['additionalGenerators']=sorted(set(manifest.get('additionalGenerators',[])+['Tools/KursHub/audit7_leaf_surface.py']))
manifest['physicalScale']='Mesh UV = metres/2; manifest tiling converts to world period; rug UV is unique 0..1 on 2.8 x 1.8 m; leaf UV is unique 0..1 on nominal 0.13 x 0.32 m.'
path.write_text(json.dumps(manifest,indent=2),encoding='utf8')
(R/'work/KursHub/audit7/reports/PBR_After_Manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
print('LEAF_PBR_OK',len(manifest['families']),len(manifest['maps']))
