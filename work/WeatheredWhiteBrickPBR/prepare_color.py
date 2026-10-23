from PIL import Image
from pathlib import Path
import numpy as np,json,sys
src=Path(sys.argv[1]);red=Path('Assets/Maps/Buildings/Textures/WeatheredRedBrick_01')
a=np.asarray(Image.open(src).convert('RGB').resize((2048,2048),Image.Resampling.LANCZOS),dtype=np.float32)/255
for axis in (1,0):
 b=np.swapaxes(a,0,axis);d=(b[0]-b[-1]).copy()
 for i in range(40):
  q=.5*(1-i/39)**2;b[i]-=q*d;b[-1-i]+=q*d
a=np.uint8(np.rint(np.clip(a,0,1)*255));Image.fromarray(a).save('work/WeatheredWhiteBrickPBR/WhiteBrick_BaseColor.png')
h=np.asarray(Image.open(red/'WBR01_Height16.png'),dtype=np.float32);gray=a.astype(np.float32).mean(axis=2)
# Profiles compare the shared mortar grid, ignoring individual brick color variation.
rx=float(np.corrcoef(gray.mean(axis=0),h.mean(axis=0))[0,1]);ry=float(np.corrcoef(gray.mean(axis=1),h.mean(axis=1))[0,1])
result={'resolution':[2048,2048],'generated_recolor_resolution':list(Image.open(src).size),'row_height_profile_correlation':ry,'column_height_profile_correlation':rx,'horizontal_edge_error':int(np.abs(a[:,0].astype(int)-a[:,-1].astype(int)).max()),'vertical_edge_error':int(np.abs(a[0].astype(int)-a[-1].astype(int)).max()),'note':'AI recolor of current red albedo. Existing structural PBR maps are copied unchanged to retain masonry scale and relief.'}
Path('work/WeatheredWhiteBrickPBR/color-validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
p=Image.fromarray(a).resize((600,600));out=Image.new('RGB',(1200,1200))
for yy in range(2):
 for xx in range(2):out.paste(p,(xx*600,yy*600))
out.save('work/WeatheredWhiteBrickPBR/repeat-preview.jpg',quality=94)
