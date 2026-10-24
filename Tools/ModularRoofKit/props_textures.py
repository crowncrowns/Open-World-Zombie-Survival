from pathlib import Path
import numpy as np
from PIL import Image
P=Path(__file__).resolve().parents[2]/'work/ModularRoofKit/UnityProject/Assets/Environment/ModularRoofKit/Textures/Props'
P.mkdir(parents=True,exist_ok=True);N=4096;rng=np.random.default_rng(107)
def noise(n):return np.array(Image.fromarray(rng.random((n,n),dtype=np.float32)).resize((N,N),Image.Resampling.BICUBIC))
def save(a,name):Image.fromarray(np.uint8(np.clip(a,0,1)*255)).save(P/(name+'.png'),compress_level=6)
fine=noise(1800);mid=noise(160);macro=noise(12)
for i,c in enumerate([(.44,.215,.125),(.19,.17,.075)]):
    base=np.stack([q*(.7+macro*.35)+(fine-.5)*.04+(mid-.5)*.06 for q in c],axis=-1);save(base,f'Props_{i}_BaseColor');del base
h=fine*.012+mid*.03;dx=np.roll(h,1,1)-np.roll(h,-1,1);dy=np.roll(h,1,0)-np.roll(h,-1,0)
norm=np.stack((dx*3,-dy*3,np.ones_like(dx)),axis=-1);norm/=np.linalg.norm(norm,axis=2)[:,:,None];save(norm*.5+.5,'Props_Normal');del norm
save(h+.5,'Props_Height');rough=.84+mid*.1;save(rough,'Props_Roughness');save(np.zeros_like(h),'Props_Metallic');ao=.94+fine*.06;save(ao,'Props_AO');mask=np.stack((np.zeros_like(h),ao,np.zeros_like(h),1-rough),axis=-1);save(mask,'Props_UnityMask')
print('PROPS_4K_TEXTURES_COMPLETE')
