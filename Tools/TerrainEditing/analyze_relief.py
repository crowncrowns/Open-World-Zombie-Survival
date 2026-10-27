from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw

import argparse
parser=argparse.ArgumentParser(description="Analyze a world-metre Terrain heightmap export and produce broad smoothing candidates.")
parser.add_argument("input_directory", type=Path, help="Directory containing TerrainInfo.json, BeforeHeights.f32, Buildings.json")
p=parser.parse_args().input_directory
info=json.loads((p/'TerrainInfo.json').read_text(encoding='utf-8-sig'))
n=info['res']; step=info['size'][0]/(n-1)
h=np.fromfile(p/'BeforeHeights.f32',dtype='<f4').reshape(n,n).astype(np.float64)
buildings=json.loads((p/'Buildings.json').read_text(encoding='utf-8-sig'))
def sample(a,x,z):
    xx=np.clip((np.asarray(x)-info['pos'][0])/step,0,n-1); zz=np.clip((np.asarray(z)-info['pos'][2])/step,0,n-1)
    ix=np.floor(xx).astype(int); iz=np.floor(zz).astype(int); jx=np.minimum(ix+1,n-1); jz=np.minimum(iz+1,n-1)
    fx=xx-ix; fz=zz-iz
    return (a[iz,ix]*(1-fx)+a[iz,jx]*fx)*(1-fz)+(a[jz,ix]*(1-fx)+a[jz,jx]*fx)*fz
def blur(a,sigma_m):
    s=sigma_m/step; radius=int(np.ceil(s*3.5)); x=np.arange(-radius,radius+1); k=np.exp(-.5*(x/s)**2); k/=k.sum()
    for axis in [1,0]:
        pad=[(0,0),(0,0)]; pad[axis]=(radius,radius)
        a=np.apply_along_axis(lambda row:np.convolve(row,k,mode='valid'),axis,np.pad(a,pad,mode='reflect'))
    return a
def stats(a):
    dz,dx=np.gradient(a,step); grade=np.hypot(dx,dz)
    spreads=[]
    for b in buildings:
        xy=np.asarray(b['points']); vals=sample(a,xy[:,0],xy[:,2]); spreads.append({'name':b['name'],'range':float(np.ptp(vals)),'oldCenter':float(sample(h,b['center'][0],b['center'][2])),'center':float(sample(a,b['center'][0],b['center'][2])),'floor':b['center'][1]})
    return {'heights':np.percentile(a,[0,1,50,99,100]).tolist(),'grade_percent':np.percentile(grade*100,[50,90,99,100]).tolist(),'building_spreads':spreads,'max_building_range':max(b['range'] for b in spreads)}
out={'before':stats(h)}
for sigma in [100,150,200,250]:
    a=blur(h,sigma)
    a+=h.mean()-a.mean()
    a.astype('<f4').tofile(p/f'Candidate_Sigma{sigma}.f32')
    out[str(sigma)]=stats(a)
(p/'Candidates.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:{j:v[j] for j in ['heights','grade_percent','max_building_range']} for k,v in out.items()},indent=2))
# A fixed elevation scale makes the loss of short, sharp terrain features visible.
canvas=Image.new('RGB',(1200,660),'white'); draw=ImageDraw.Draw(canvas)
for i,(label,a) in enumerate([('Before',h),('Gaussian 150 m',np.fromfile(p/'Candidate_Sigma150.f32',dtype='<f4').reshape(n,n))]):
    f=np.clip((a-24)/65,0,1); color=np.stack([70+f*160,110+f*100,150-f*100],axis=-1).astype('uint8')
    img=Image.fromarray(color[::-1]).resize((560,560)); canvas.paste(img,(20+i*600,45)); draw.text((20+i*600,15),label,fill='black')
canvas.save(p/'HeightComparison.png')
