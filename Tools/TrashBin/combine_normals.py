from pathlib import Path
from PIL import Image
import numpy as np
p=Path(__file__).resolve().parents[2]/'work/TrashBin/UnityProject/Assets/Environment/Props/TrashBin/Textures'
a=np.asarray(Image.open(p/'TrashBin_Normal.png').convert('RGB'),dtype=np.float32)/127.5-1
b=np.asarray(Image.open(p/'TrashBin_HP_BakedNormal.png').convert('RGB'),dtype=np.float32)/127.5-1
b[b[:,:,2]<.1]=[0,0,1]
c=np.stack([a[:,:,0]+b[:,:,0],a[:,:,1]+b[:,:,1],a[:,:,2]*b[:,:,2]],axis=-1)
c/=np.maximum(np.linalg.norm(c,axis=-1,keepdims=True),.00001)
Image.fromarray(np.uint8(np.clip((c*.5+.5)*255,0,255))).save(p/'TrashBin_Normal.png')
print('Combined high-poly bake and fine surface relief')
