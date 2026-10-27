from PIL import Image
import numpy as np
from catalog import *
normal=np.asarray(Image.open(ASSET/'Textures/Shared/Manufacturing_BakedNormal.png').convert('RGB'),dtype=np.float32)/127.5-1
for wear in ['Used','Worn','Damaged']:
    for name in MATERIALS:
        path=ASSET/'Textures'/wear/name/'Normal.png'
        micro=np.asarray(Image.open(path).convert('RGB'),dtype=np.float32)/127.5-1
        a=.6 if name in ['Steel','Aluminium','Copper'] else .14
        result=np.stack([normal[:,:,0]*a+micro[:,:,0],normal[:,:,1]*a+micro[:,:,1],normal[:,:,2]*micro[:,:,2]],axis=-1)
        result/=np.maximum(np.linalg.norm(result,axis=-1,keepdims=True),.001)
        Image.fromarray(np.clip((result+1)*127.5,0,255).astype('uint8')).save(path,compress_level=4)
print('BAKED_NORMALS_COMBINED_PASS')
