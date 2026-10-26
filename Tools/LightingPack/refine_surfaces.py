from catalog import *
import numpy as np
from PIL import Image
for wear in ['Used','Worn','Damaged']:
    folder=ASSET/'Textures'/wear/'Steel'
    for name in ['Metallic','MetallicSmoothness']:
        p=folder/(name+'.png');im=np.array(Image.open(p))
        channel=im if im.ndim==2 else im[:,:,0]
        if channel.max()<=30:continue
        if im.ndim==2:im=(im.astype('float32')*.1).astype('uint8')
        else:im[:,:,0]=(im[:,:,0].astype('float32')*.1).astype('uint8')
        Image.fromarray(im).save(p,compress_level=4)
print('PAINTED_STEEL_SURFACE_REFINED')
