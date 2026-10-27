"""Original deterministic PBR surfaces, woven rugs, and copyright-clear procedural radio."""
import sys,json,math,wave
from math import pi
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont,ImageFilter
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import ASSET,VERIFY
from geom import MATERIALS
PALETTE={
'Wood':((.36,.21,.105),.0,.72),'DarkWood':((.17,.095,.052),0,.75),'Plywood':((.52,.38,.22),0,.84),
'Chipboard':((.34,.27,.17),0,.91),'PaintedWood':((.22,.32,.24),0,.80),'WhiteLaminate':((.66,.63,.53),0,.72),
'Fabric':((.35,.30,.22),0,.94),'RedFabric':((.32,.105,.095),0,.94),'GreenFabric':((.14,.22,.16),0,.96),
'Leather':((.17,.105,.065),0,.69),'Foam':((.59,.43,.18),0,.98),'Steel':((.22,.24,.22),.75,.64),
'Rust':((.33,.13,.045),.24,.90),'Chrome':((.48,.48,.43),.95,.30),'Enamel':((.65,.66,.56),.10,.61),
'Plastic':((.15,.17,.14),0,.73),'WhitePlastic':((.62,.59,.43),0,.76),'Glass':((.34,.44,.42),.1,.20),
'Mirror':((.56,.65,.64),.98,.13),'Rubber':((.038,.040,.033),0,.93),'Paper':((.66,.59,.40),0,.95),
'Carpet':((.26,.08,.065),0,.98),'Screen':((.035,.052,.042),0,.38),'Emission':((.81,.62,.32),0,.58),
'Ceramic':((.65,.65,.56),0,.50),'Brass':((.42,.30,.10),.76,.57)}

def smooth_noise(rng,n,scale):
    a=(rng.random((scale,scale))*255).astype('uint8');return np.asarray(Image.fromarray(a).resize((n,n),Image.Resampling.BICUBIC),dtype=np.float32)/255

def rugs(index,n,rng):
    y,x=np.mgrid[0:n,0:n].astype(float)/n;X=(x-.5)*2;Y=(y-.5)*2
    # Woven medallions, nested ornamental borders and repeated floral knot motifs.
    colors=[((.34,.07,.06),(.63,.46,.25),(.13,.18,.17)),((.32,.08,.08),(.62,.49,.29),(.14,.20,.18)),
            ((.16,.23,.27),(.62,.43,.27),(.39,.10,.11)),((.28,.21,.11),(.60,.48,.28),(.12,.20,.16)),
            ((.20,.29,.25),(.57,.47,.26),(.23,.09,.07)),((.37,.25,.19),(.52,.43,.31),(.22,.22,.18)),
            ((.30,.09,.06),(.56,.43,.23),(.14,.14,.12)),((.23,.13,.09),(.57,.44,.25),(.15,.20,.16)),
            ((.24,.09,.08),(.57,.43,.28),(.16,.21,.18)),((.14,.17,.12),(.32,.30,.19),(.09,.10,.08))][index]
    b=np.maximum(abs(X),abs(Y));band=np.minimum(abs((b-.79)*16)%2,1)
    central=(abs(X)*1.2+abs(Y)*.82);angle=np.arctan2(Y,X);r=np.sqrt(X*X+Y*Y)
    petals=np.cos(angle*(8 if index!=4 else 4)+np.sin(r*18))*np.sin(r*32)
    flower=np.cos(x*(24+index*2)*np.pi)*np.cos(y*(32-index)*np.pi)
    mask=((central<.62)&(petals>-.35))|((b>.73)&(b<.93)&(np.sin(x*140)*np.cos(y*120)>.1))
    tex=np.empty((n,n,3),np.float32);tex[:]=colors[0];tex[mask]=colors[1]
    tex[(flower>.55)&(b<.72)&(central>.63)]=colors[2]
    for edge in [.69,.72,.76,.94,.97]:tex[abs(b-edge)<.008]=colors[1]
    if index==4:tex[((np.floor(x*12)+np.floor(y*16))%2==0)&(b<.7)]=colors[2]
    if index==9:tex[:]=colors[0];tex+=(np.sin(x*320)*np.sin(y*320))[...,None]*.025
    grain=smooth_noise(rng,n,80);weave=(np.sin(x*n*pi)*.015+np.sin(y*n*pi)*.015)
    tex*=.75+grain[...,None]*.3;tex+=weave[...,None]
    wear=smooth_noise(rng,n,9)*smooth_noise(rng,n,29)
    tex=tex*(1-wear[...,None]*.32)+np.array([.37,.30,.22])*wear[...,None]*.32
    if index==5:tex=tex*.55+np.array([.37,.31,.22])*.45
    if index==6:
        burn=(np.sin(x*18+y*12)+np.cos(x*34-y*17)>1.5)&(grain>.25);tex[burn]*=.12
    height=grain*.25+np.sin(x*n*pi)*.03+np.sin(y*n*pi)*.03
    return tex,height

def surfaces():
    from professional_surfaces import generate
    generate(MATERIALS,PALETTE,rugs)
    # Cyrillic labels on an original reusable atlas, without copyrighted printed graphics.
    im=Image.new('RGB',(1024,1024),(176,163,124));dr=ImageDraw.Draw(im)
    font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',32)
    labels=['ЗИЛ','БИРЮСА','САРАТОВ','ОСТОРОЖНО!','220 В','СДЕЛАНО В СССР','РАДИО','ПРИЁМНИК','ГРОМКОСТЬ','НАСТРОЙКА','ВКЛ · ВЫКЛ','ХРАНИТЬ В СУХОМ МЕСТЕ','АРТ. 1978','НЕ ВКЛЮЧАТЬ','МАСТЕРСКАЯ','ТЕХНИЧЕСКИЙ ПАСПОРТ']
    for i,txt in enumerate(labels):
        yy=i*64;dr.rectangle((4,yy+3,1020,yy+60),outline=(65,54,36),width=2);dr.text((15,yy+12),txt,font=font,fill=(42,35,24))
    im.save(ASSET/'Textures/Labels_RU.png')
    VERIFY.mkdir(parents=True,exist_ok=True)

def write_wav(path,data,sr=24000):
    path.parent.mkdir(parents=True,exist_ok=True);data=np.asarray(data);data=np.clip(data,-.92,.92)
    with wave.open(str(path),'wb') as f:f.setparams((1,2,sr,0,'NONE','not compressed'));f.writeframes((data*32767).astype('<i2').tobytes())

def sounds():
    sr=24000;rng=np.random.default_rng(9531);root=ASSET/'Audio'
    for name,duration,kind in [('Hinge',.65,0),('Drawer',.7,1),('Switch',.13,2),('Static',1.2,3),('Button',.12,4)]:
        t=np.arange(int(sr*duration))/sr;noise=rng.normal(0,.09,len(t));env=np.sin(pi*t/duration)**1.2
        if kind==0:data=(np.sin(t*2*pi*(170+70*np.sin(t*18)))*.07+noise*.4)*env
        elif kind==1:data=(noise*.6+np.sin(t*2*pi*83)*.035)*env
        elif kind in [2,4]:data=noise*np.exp(-t*70)+np.sin(t*2*pi*680)*np.exp(-t*95)*.08
        else:data=noise*.8+np.sin(t*2*pi*50)*.012
        folder='Furniture' if kind<2 else 'Electronics';write_wav(root/folder/(name+'.wav'),data,sr)
    # Three original looping station beds: a minor-key instrumental, ambient pad and empty band.
    seconds=24;t=np.arange(seconds*sr)/sr
    for station in range(3):
        data=np.zeros(len(t));tempo=.50 if station==0 else 1.5
        notes=[57,60,64,67,64,60,55,59,62,65,62,59,53,57,60,64]
        for beat in range(int(seconds/tempo)):
            start=int(beat*tempo*sr);end=min(len(t),start+int(tempo*1.7*sr));u=np.arange(end-start)/sr
            midi=notes[(beat+station*3)%len(notes)];freq=440*2**((midi-69)/12)
            env=(1-np.exp(-u*25))*np.exp(-u*(3 if station==0 else .6))
            tone=np.sin(u*freq*2*pi)+.28*np.sin(u*freq*4*pi)+.10*np.sin(u*freq*6*pi)
            data[start:end]+=tone*env*(.07 if station==0 else .035)
        if station==1:data+=np.sin(t*2*pi*110)*.018+np.sin(t*2*pi*164.8138)*.012
        if station==2:data=rng.normal(0,.038,len(t))*(.4+.2*np.sin(t*.3))+np.sin(t*2*pi*50)*.008
        # Zero-ended loop buffer avoids hard clicks.
        fade=np.minimum(1,np.minimum(t,.001+seconds-t)/.08);data*=fade
        write_wav(root/'Radio'/['OriginalInstrumental.wav','QuietAtmosphere.wav','EmptyFrequency.wav'][station],data,sr)
    print('ORIGINAL_AUDIO_PASS',flush=True)
if __name__=='__main__':surfaces();sounds()
