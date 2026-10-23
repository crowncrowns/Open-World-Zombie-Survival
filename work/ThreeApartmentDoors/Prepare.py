from pathlib import Path
import numpy as np,json
from PIL import Image,ImageFilter
root=Path('Assets/Maps/Buildings/Textures');src=Path('C:/Users/Григорий/.codex/generated_images/01a16020-cae0-78d2-8c66-4d7f34319e8d')
w,h=1024,2400
items=[('ApartmentDoor_RedTufted_01','ADR01','exec-b66129d8-7e31-42fd-b0ae-4abac07a1838.png','exec-fe09261a-141b-49e2-b7f1-9c2ede2ff693.png',.012),('ApartmentDoor_WoodSlats_01','ADW01','exec-2e6f34a9-8123-4cb5-ac6c-a3027ba88f30.png',None,.006),('ApartmentDoor_DarkUpholstery_01','ADD01','exec-7ca352b2-ccd3-4553-b622-bee8d72bc938.png',None,.003)]
def blur(a,r):return np.asarray(Image.fromarray(np.uint8(np.clip(a,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),dtype=np.float32)/255
for idx,(folder,pre,base,hs,depth) in enumerate(items):
 p=root/folder
 if p.exists():raise RuntimeError('Refuse existing '+str(p))
 p.mkdir();im=Image.open(src/base).convert('RGB');a=np.asarray(im.resize((w,h),Image.Resampling.LANCZOS),dtype=np.float32)/255;lum=a.mean(2);yy,xx=np.mgrid[:h,:w];x=xx/w;y=yy/h
 metal=np.zeros((h,w),np.float32)
 def ellipse(cx,cy,rx,ry):return ((x-cx)/rx)**2+((y-cy)/ry)**2<1
 def rect(x0,x1,y0,y1):return (x>x0)&(x<x1)&(y>y0)&(y<y1)
 if idx==0:
  height=np.asarray(Image.open(src/hs).convert('L').resize((w,h),Image.Resampling.LANCZOS),dtype=np.float32)/255
  metal[ellipse(.507,.14,.022,.013)|ellipse(.076,.283,.038,.019)|rect(.045,.108,.401,.511)]=.65
 elif idx==1:
  profile=np.median(lum[int(h*.06):int(h*.9)],axis=0);smooth=np.convolve(np.pad(profile,12,mode='edge'),np.ones(25)/25,mode='valid');groove=np.clip((smooth-profile-.008)/.075,0,1)
  height=np.broadcast_to(.55-.45*groove,(h,w)).copy();mask=rect(.04,.19,.425,.469)|ellipse(.545,.287,.018,.012);height[mask]=.98;metal[mask]=.55
 else:
  height=np.clip(.5+(lum-blur(lum,14))*.18,.43,.57);mask=ellipse(.5,.232,.015,.008)|ellipse(.925,.268,.04,.019)|rect(.904,.962,.439,.54)
  height[mask]=.95;metal[mask]=.7
 height=blur(height,1.2);rough=np.where(metal>.1,.54,.83 if idx==1 else .85).astype(np.float32);ao=np.clip(1-.45*np.maximum(blur(height,12)-height,0),.73,1)
 dy,dx=np.gradient(height*depth);dx*=w/.87;dy*=h/2.035;n=np.stack([-dx,dy,np.ones_like(dx)],2);n/=np.linalg.norm(n,axis=2,keepdims=True)
 def save(name,v):Image.fromarray(np.uint8(np.rint(np.clip(v,0,1)*255))).save(p/(pre+'_'+name+'.png'))
 save('BaseColor',a);save('Normal_OpenGL',n*.5+.5);save('Roughness',rough);save('AO',ao);save('Metallic',metal);save('MetallicSmoothness',np.stack([metal,metal,metal,1-rough],2));save('ORM',np.stack([ao,rough,metal],2));Image.fromarray(np.uint16(np.rint(height*65535))).save(p/(pre+'_Height16.png'))
 report={'folder':folder,'resolution':[w,h],'leaf_m':[.87,2.035],'source_resolution':list(im.size),'height_source':'saved AI height' if hs else 'approximate technical reconstruction of grooves/fittings from saved color','relief_m':depth,'normal_unit_error':float(abs(np.linalg.norm(n,axis=2)-1).max()),'maps':8,'finite':bool(np.isfinite(n).all())};(p/'Validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
