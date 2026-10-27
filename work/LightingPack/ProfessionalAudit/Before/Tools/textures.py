import numpy as np, json, wave, math
from PIL import Image,ImageFilter
from catalog import *
ASSET.mkdir(parents=True,exist_ok=True); VERIFY.mkdir(parents=True,exist_ok=True)
N=2048
rng=np.random.default_rng(8417)
def noise(scale):
    a=Image.fromarray((rng.random((scale,scale))*255).astype('uint8'))
    return np.asarray(a.resize((N,N),Image.Resampling.BICUBIC),dtype=np.float32)/255
coarse=noise(24); middle=noise(180); fine=noise(1000)
y,x=np.mgrid[0:N,0:N].astype(np.float32)/N
colors={'Steel':(.27,.31,.28),'Aluminium':(.57,.59,.57),'Plastic':(.76,.75,.66),'Glass':(.79,.80,.72),'Bulb':(.83,.82,.74),'Rubber':(.045,.042,.035),'Copper':(.38,.19,.075),'ClearGlass':(.49,.56,.51)}
for wi,wear in enumerate(['Used','Worn','Damaged']):
    for mi,name in enumerate(MATERIALS):
        dest=ASSET/'Textures'/wear/name;dest.mkdir(parents=True,exist_ok=True)
        base=np.broadcast_to(np.array(colors[name],np.float32),(N,N,3)).copy()
        dirt=np.clip((coarse*.6+middle*.3+fine*.1)-.38,0,1)*[.45,.9,1.35][wi]
        drip=(np.sin(x*690+np.sin(y*6)*.12)> .95)*np.clip(coarse-.4,0,1)*(.15+wi*.12)
        base *= (1-dirt-drip)[...,None]
        height=(fine-.5)*.012+(middle-.5)*.018
        metal=name in ['Steel','Aluminium','Copper']
        rust=np.clip((coarse+middle*.22 - [.82,.69,.57][wi])*7,0,1) if metal else np.zeros((N,N),np.float32)
        if name=='Aluminium': rust*=.20 # oxidised aluminium, not red iron corrosion
        rustcol=np.stack([.19+middle*.11,.063+middle*.06,.018+middle*.03],axis=-1)
        if name=='Aluminium':rustcol[:]=(.32,.33,.30)
        base=base*(1-rust[...,None])+rustcol*rust[...,None]
        scratches=(np.abs(np.sin(x*1380+y*17))>.998)*(middle>.55)
        base += scratches[...,None]*.10*(1-rust[...,None])
        if name in ['Plastic','Glass','Bulb']:
            base[:,:,2] *= 1-wi*.085
        height += rust*.09 - scratches*.035
        gy,gx=np.gradient(height);norm=np.stack([-gx*160,-gy*160,np.ones_like(gx)],axis=-1);norm/=np.linalg.norm(norm,axis=-1,keepdims=True)
        rough=np.clip((.47 if metal else .35)+dirt*.5+rust*.4+(middle-.5)*.12,.05,.98)
        metallic=np.clip((.08 if name=='Steel' else (.8 if metal else 0))*(1-rust),0,1)
        ao=np.clip(1-dirt*.35-rust*.2,.3,1)
        def save(a,fn):Image.fromarray((np.clip(a,0,1)*255).astype('uint8')).save(dest/(fn+'.png'),compress_level=4)
        save(base,'BaseColor');save(norm*.5+.5,'Normal');save(rough,'Roughness');save(ao,'AO');save(metallic,'Metallic')
        packed=np.zeros((N,N,4),np.float32);packed[:,:,0]=metallic;packed[:,:,3]=1-rough;save(packed,'MetallicSmoothness')
        emission=np.repeat((np.clip(1-dirt*.65,0,1) if name in ['Glass','Bulb','ClearGlass'] else np.zeros_like(dirt))[...,None],3,axis=-1);save(emission,'Emission')
        print(wear,name,flush=True)
# Original quiet electrical hum, not a third-party audio sample.
audio=ASSET/'Audio';audio.mkdir(exist_ok=True)
sr=22050;t=np.arange(sr*2)/sr
v=(np.sin(t*2*math.pi*100)*.42+np.sin(t*2*math.pi*200)*.12+np.sin(t*2*math.pi*50)*.08)*(1+.08*np.sin(t*2*math.pi*7))
with wave.open(str(audio/'ElectricalHum.wav'),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(sr);f.writeframes((v*8000).astype('<i2').tobytes())
(ASSET/'catalog.json').write_text(json.dumps(catalog(),ensure_ascii=False,indent=2),encoding='utf-8')
print('TEXTURES_PASS',flush=True)
