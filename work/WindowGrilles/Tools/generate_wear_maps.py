from pathlib import Path
import json
import numpy as np
from PIL import Image
from generate_textures import noise_field, OUT, PAINTS, SIZE

rust=np.asarray(Image.open(OUT/'T_Rust_Common_BaseColor.png').convert('RGB'),dtype=np.float32)
rust_mask=np.asarray(Image.open(OUT/'T_Rust_Common_MetallicSmoothness.png').convert('RGBA'),dtype=np.float32)
records=[]
for index,name in enumerate(PAINTS):
    paint=np.asarray(Image.open(OUT/f'T_{name}_BaseColor.png').convert('RGB'),dtype=np.float32)
    packed=np.asarray(Image.open(OUT/f'T_{name}_MetallicSmoothness.png').convert('RGBA'),dtype=np.float32)
    field=.60*noise_field(7200+index,42)+.28*noise_field(7300+index,180)+.12*noise_field(7400+index,760)
    for state,target in [('B',.34),('C',.64)]:
        threshold=float(np.quantile(field,1-target))
        alpha=np.clip((field-threshold)/.043+.5,0,1)
        color=paint*(1-alpha[...,None])+rust*alpha[...,None]
        edge=np.clip(1-np.abs(alpha-.25)*7.5,0,1)*.22
        color=color*(1-edge[...,None])+np.array([93,91,80],dtype=np.float32)*edge[...,None]
        Image.fromarray(np.clip(color,0,255).astype(np.uint8),'RGB').save(OUT/f'T_{name}_{state}_BaseColor.png',compress_level=6)
        mask=packed*(1-alpha[...,None])+rust_mask*alpha[...,None]
        Image.fromarray(np.clip(mask,0,255).astype(np.uint8),'RGBA').save(OUT/f'T_{name}_{state}_MetallicSmoothness.png',compress_level=6)
        records.append({'set':f'{name}_{state}','corrodedFraction':float(np.mean(alpha>.5)),'transitionFraction':float(np.mean((alpha>.05)&(alpha<.95)))})
root=Path(__file__).resolve().parents[1]
(root/'Verification'/'wear_texture_stats.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
print('WEAR_MAPS_OK extraMaps=20 resolution=4096x4096')
