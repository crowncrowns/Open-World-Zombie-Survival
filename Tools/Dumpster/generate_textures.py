from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import random

ROOT=Path(__file__).resolve().parents[2]/'work/Dumpster/UnityProject/Assets/Environment/Props/Dumpster'
N=4096
rng=np.random.default_rng(9157)
random.seed(9157)
def noise(scale):
    a=rng.random((scale,scale)).astype('float32')
    return np.asarray(Image.fromarray(a,mode='F').resize((N,N),Image.Resampling.BICUBIC),dtype=np.float32)-.5
def save(a,p):
    p.parent.mkdir(parents=True,exist_ok=True)
    Image.fromarray(np.uint8(np.clip(a,0,1)*255)).save(p,compress_level=3)
def textures(folder,color,metal,rough,strength):
    p=ROOT/'Textures'/folder
    macro=noise(15)*.45+noise(80)*.20+noise(370)*.07
    grain=noise(1300)
    scratches=Image.new('L',(N,N)); d=ImageDraw.Draw(scratches)
    for i in range(1700):
        x=random.randrange(N); y=random.randrange(N)
        d.line((x,y,x+random.randint(3,95),y+random.randint(-6,6)),fill=random.randint(30,160),width=random.choice([1,1,2]))
    scratch=np.asarray(scratches,dtype=np.float32)/255
    base=np.zeros((N,N,3),np.float32)
    for c in range(3): base[:,:,c]=color[c]*(1+macro*.65)+grain*.012+scratch*.025
    save(base,p/(folder+'_BaseColor.png')); del base
    height=(macro*.15+grain*.03-scratch*.24)*strength
    dx=np.roll(height,1,1)-np.roll(height,-1,1); dy=np.roll(height,1,0)-np.roll(height,-1,0)
    norm=np.stack((dx*2,-dy*2,np.ones_like(dx)),axis=-1); norm/=np.linalg.norm(norm,axis=2)[:,:,None]
    save(norm*.5+.5,p/(folder+'_Normal.png')); del norm,dx,dy,height
    r=np.clip(rough+macro*.15+grain*.045-scratch*.09,.05,.98)
    save(r,p/(folder+'_Roughness.png'))
    save(np.full((N,N),metal,np.float32),p/(folder+'_Metallic.png'))
    ao=np.clip(1-np.maximum(0,-macro)*.22,.4,1)
    save(ao,p/(folder+'_AO.png'))
    mask=np.empty((N,N,4),np.float32); mask[:,:,0]=metal;mask[:,:,1]=ao;mask[:,:,2]=0;mask[:,:,3]=1-r
    save(mask,p/(folder+'_UnityMask.png'))
    del mask,ao,r,macro,grain,scratch
for args in [('Body',(.075,.18,.125),0,.71,1.8),('Wheels',(.030,.032,.034),0,.82,2),('Metal',(.43,.46,.46),.87,.39,1.5),('Garbage',(.055,.06,.065),0,.38,3)]: textures(*args)
# Separate reusable grime mask: R sediment, G vertical leaks, B faded abrasions.
p=ROOT/'Textures/Body'
m=np.clip(.5+noise(22)*.6+noise(90)*.3+noise(520)*.10,0,1)
drips=Image.new('L',(N,N));d=ImageDraw.Draw(drips)
for i in range(140):
    x=random.randrange(N); y=random.randrange(N)
    d.line((x,y,x+random.randint(-18,18),y+random.randint(100,1700)),fill=random.randint(30,180),width=random.randint(2,18))
drips=drips.filter(ImageFilter.GaussianBlur(5))
mask=np.stack((m,np.asarray(drips,dtype=np.float32)/255,np.clip(.2+noise(55),0,1)),axis=-1)
save(mask,p/'Body_GrimeMasks.png');del mask,m
# Eight authentic typeset announcements; no real phone numbers or personal data.
atlas=Image.new('RGBA',(N,N),(0,0,0,0))
titles=[('ВЫВОЗ','МУСОРА'),('ГРУЗЧИКИ','24 / 7'),('СКУПКА','ТЕХНИКИ'),('РЕМОНТ','КВАРТИР'),('МЕТАЛЛОЛОМ',),('УСЛУГИ','ГАЗЕЛИ'),('УБОРКА','ПОМЕЩЕНИЙ'),('ОБЪЯВЛЕНИЕ','СОРВАНО')]
fontpath='C:/Windows/Fonts/arialbd.ttf'
for i,lines in enumerate(titles):
    tile=Image.new('RGBA',(1024,2048),(0,0,0,0)); d=ImageDraw.Draw(tile)
    outline=[(random.randint(36,80),y) for y in range(70,1970,45)]
    outline += [(x,random.randint(1900,1980)) for x in range(70,960,45)]
    outline += [(random.randint(945,990),y) for y in range(1970,70,-45)]
    outline += [(x,random.randint(40,95)) for x in range(960,70,-45)]
    d.polygon(outline,fill=(205+i*3,199+i*3,169+i*4,255))
    for j,line in enumerate(lines):
        size=125
        while ImageFont.truetype(fontpath,size).getlength(line)>830:size-=2
        font=ImageFont.truetype(fontpath,size)
        d.text((512,370+j*185),line,font=font,anchor='mm',fill=(35,43,41,255))
    font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',57)
    for j,line in enumerate(['БЫСТРО • НЕДОРОГО','ЕЖЕДНЕВНО','РАБОТАЕМ В РАЙОНЕ']):
        d.text((512,1000+j*110),line,font=font,anchor='mm',fill=(65,65,54,255))
    d.line((100,1280,925,1280),fill=(90,85,68,255),width=4)
    for x in range(110,920,100):
        d.line((x,1340,x,1940),fill=(100,97,80,255),width=2)
        d.text((x+40,1500),'24/7',font=font,anchor='mm',fill=(90,90,74,255))
    for k in range(65):
        x=random.randrange(90,950);y=random.randrange(120,1940)
        d.line((x,y,x+random.randrange(8,170),y+random.randrange(-3,6)),fill=(170,166,143,160),width=random.randrange(1,7))
    # Missing corners and tears cut through the printed paper.
    d.polygon([(0,0),(300,0),(0,440)],fill=(0,0,0,0))
    d.polygon([(800,2048),(1024,1700),(1024,2048)],fill=(0,0,0,0))
    d.polygon([(1024,780),(820,825),(1024,870)],fill=(0,0,0,0))
    if i==7:d.polygon([(0,350),(1024,200),(1024,1700),(0,1500)],fill=(0,0,0,0))
    atlas.alpha_composite(tile,((i%4)*1024,(i//4)*2048))
out=ROOT/'Textures/Stickers';out.mkdir(parents=True,exist_ok=True);atlas.save(out/'Stickers_Atlas.png')
print('TEXTURES_COMPLETE',ROOT)
