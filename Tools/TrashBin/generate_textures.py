from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import gaussian_filter, zoom
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'work/TrashBin/UnityProject/Assets/Environment/Props/TrashBin/Textures'
OUT.mkdir(parents=True,exist_ok=True)
N=4096
rng=np.random.default_rng(6123)
def noise(h,w,scale):
    a=rng.random((max(2,int(h/scale)),max(2,int(w/scale))),dtype=np.float32)
    return np.asarray(Image.fromarray(a).resize((w,h),Image.Resampling.BICUBIC))
al=np.zeros((N,N,3),np.uint8); ro=np.zeros((N,N),np.uint8);me=ro.copy();ao=np.full_like(ro,255);height=np.zeros((N,N),np.float32)
def panel(x0,y0,w,h,kind,seed):
    global al,ro,me,ao,height
    rr=np.random.default_rng(seed)
    yy,xx=np.mgrid[0:h,0:w].astype(np.float32);u=xx/w;v=yy/h
    n=noise(h,w,120); n2=noise(h,w,24); fine=noise(h,w,2)
    # Image rows go from the upper edge to the lower edge of each panel.
    bottom=np.exp(-((1-v)/.065)**2);edge=np.exp(-(np.minimum(u,1-u)/.018)**2)
    if kind=='outer':
        field=bottom*.27+edge*.18
        # Discrete corrosion centres: bottom seam and side pivot/weld locations.
        for cx,cy,sx,sy in [(rr.uniform(.05,.95),rr.uniform(.91,1.02),rr.uniform(.012,.055),rr.uniform(.015,.042)) for _ in range(8)]+([(.5,.48,.025,.025)] if seed in [92,93] else []):
            field+=np.exp(-(((u-cx)/sx)**2+((v-cy)/sy)**2))*rr.uniform(.5,1.1)
        base=np.array([21,79,39],np.float32)
    elif kind=='inner':
        field=bottom*.85+edge*.55+(n>.66)*.13
        base=np.array([27,64,34],np.float32)
    elif kind=='floor':
        field=(n>.55)*.6+edge*.4
        base=np.array([42,57,34],np.float32)
    else:
        field=(np.exp(-((v-.92)/.15)**2)+np.exp(-((v-.45)/.045)**2))*.8+edge*.35
        base=np.array([27,30,25],np.float32)
    rust=np.clip((field+(n-.5)*.36+(n2-.5)*.47+(fine-.5)*.28-.46)*10,0,1)
    # Sparse chips remain isolated from the broad weathering noise.
    chips=(noise(h,w,9)>.79)&((edge+bottom)>.12)
    rust=np.maximum(rust,chips*.8)
    dirt=(1-v)**0 # wet/dry streaks carry dust downward from isolated scratches
    streak=np.zeros((h,w),np.float32)
    for _ in range(18):
        cx=rr.uniform(.02,.98);start=rr.uniform(.03,.8);length=rr.uniform(.06,.36)
        streak+=np.exp(-((u-cx)/rr.uniform(.001,.005))**2)*np.clip((v-start)/.025,0,1)*np.clip((start+length-v)/.1,0,1)
    color=base[None,None,:]*(.83+n[...,None]*.30)+(fine[...,None]-.5)*7
    color*=1-np.clip(streak,0,1)[...,None]*.20
    if kind in ['outer','inner']:color=color*(1-v[...,None]*.09)
    rustcol=np.stack([62+n2*35+fine*22,29+n2*19+fine*11,15+n2*10+fine*5],axis=-1)
    color=color*(1-rust[...,None])+rustcol*rust[...,None]
    # Actual narrow scratches, variable direction, length and depth.
    scratch=Image.new('L',(w,h));d=ImageDraw.Draw(scratch)
    for _ in range(100 if kind=='outer' else 45):
        x=int(rr.uniform(.02,.98)*w);y=int(rr.uniform(.04,.96)*h)
        d.line((x,y,x+int(rr.normal(0,8)),y+int(rr.uniform(3,42))),fill=int(rr.uniform(60,210)),width=int(rr.choice([1,1,2])))
    sc=np.asarray(scratch,dtype=np.float32)/255
    color=color*(1-sc[...,None]*.6)+np.array([83,83,68])*sc[...,None]*.6
    r=np.clip(170+n2*34+rust*35+v*12-sc*40,0,255)
    metal=np.clip(sc*130+(1-rust)*5,0,255)
    if kind=='steel':metal=np.clip(175-rust*170,0,255);r-=30
    a=np.clip(255-bottom*20-edge*18-rust*13,0,255)
    al[y0:y0+h,x0:x0+w]=np.clip(color,0,255).astype('uint8')
    ro[y0:y0+h,x0:x0+w]=r.astype('uint8');me[y0:y0+h,x0:x0+w]=metal.astype('uint8');ao[y0:y0+h,x0:x0+w]=a.astype('uint8')
    height[y0:y0+h,x0:x0+w]=(fine-.5)*.06-rust*.18-sc*.3
for i in range(4):
    panel(i*1024,0,1024,1638,'outer',90+i)
    panel(i*1024,1638,1024,1638,'inner',100+i)
for i,k in enumerate(['floor','floor','steel','steel']):panel(i*1024,3276,1024,820,k,110+i)
Image.fromarray(al).save(OUT/'TrashBin_Albedo.png')
for a,n in [(ro,'Roughness'),(me,'Metallic'),(ao,'AO')]:Image.fromarray(a).save(OUT/f'TrashBin_{n}.png')
gy,gx=np.gradient(gaussian_filter(height,.7));normal=np.stack([-gx*2,gy*2,np.ones_like(gx)],axis=-1);normal/=np.linalg.norm(normal,axis=-1)[...,None]
Image.fromarray(np.uint8(np.clip((normal*.5+.5)*255,0,255))).save(OUT/'TrashBin_Normal.png')
packed=np.stack([me,np.zeros_like(me),np.zeros_like(me),255-ro],axis=-1)
Image.fromarray(packed).save(OUT/'TrashBin_MetallicSmoothness.png')
Image.fromarray(np.stack([me,ao,np.zeros_like(me),255-ro],axis=-1)).save(OUT/'TrashBin_HDRP_Mask.png')
# Shared household-waste atlas, including printed paper and packaging.
atlas=Image.new('RGBA',(4096,4096));draw=ImageDraw.Draw(atlas)
colors=[(186,181,158),(140,162,148),(33,39,37),(111,96,68),(178,170,143),(120,42,24),(214,205,170),(94,112,116),(32,51,58),(152,155,143),(94,100,66),(184,154,95),(198,196,178),(92,109,61),(155,150,132),(62,65,53)]
fontpath='C:/Windows/Fonts/arial.ttf';font=ImageFont.truetype(fontpath,48);small=ImageFont.truetype(fontpath,20)
for i,c in enumerate(colors):
    x=i%4*1024;y=i//4*1024
    n=noise(1024,1024,70);f=noise(1024,1024,3)
    a=np.clip(np.array(c)[None,None,:]*(.88+n[...,None]*.22)+(f[...,None]-.5)*14,0,255).astype('uint8')
    tile=Image.fromarray(a).convert('RGBA');dt=ImageDraw.Draw(tile)
    if i in [0,4,6,11,12]:
        dt.rectangle((70,180,950,280),fill=(c[0]//2, c[1]//2,c[2]//2,255))
        dt.text((100,190),['ГОРОДСКИЕ НОВОСТИ','ПИТЬЕВАЯ ВОДА','ПАКЕТ','КАРТОН'][i%4],font=font,fill=(210,204,174))
        for yy in range(325,850,28):
            for col in range(3):
                dt.text((70+col*300,yy),'Объявления  •  город' if i==0 else 'Состав и информация',font=small,fill=(70,68,54))
    if i in [1,5,7,9,13]:
        dt.rectangle((60,330,965,670),fill=(c[0]//2,c[1]//2,c[2]//2,255))
        dt.text((120,440),['ВОДА','ПРОДУКТ','НАПИТОК'][i%3],font=font,fill=(220,212,185))
        for k in range(70):dt.line((100+k*6,740,100+k*6,870),fill=(40,40,33),width=int(rng.integers(1,4)))
    atlas.paste(tile,(x,y))
atlas.save(OUT/'TrashContents_Atlas.png')
Image.new('RGB',(4096,4096),(128,128,255)).save(OUT/'TrashContents_Normal.png')
Image.new('RGBA',(4096,4096),(0,0,0,65)).save(OUT/'TrashContents_MetallicSmoothness.png')
print('TEXTURES_OK',OUT)
