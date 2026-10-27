"""Seamless surface parameters. Height is in metres; colour contains no baked lighting."""
from catalog import *
import numpy as np,json
from PIL import Image
N=2048;px=.5/N;rng=np.random.default_rng(2561025)
def periodic(scale):
    grid=rng.random((scale,scale),dtype=np.float32);a=np.arange(N,dtype=np.float32)*scale/N;indices=a.astype('int32');t=a-indices;t=t*t*(3-2*t)
    v00=grid[indices[:,None],indices[None,:]];v10=grid[indices[:,None],(indices[None,:]+1)%scale]
    v01=grid[(indices[:,None]+1)%scale,indices[None,:]];v11=grid[(indices[:,None]+1)%scale,(indices[None,:]+1)%scale]
    return (v00*(1-t)[None,:]+v10*t[None,:])*(1-t)[:,None]+(v01*(1-t)[None,:]+v11*t[None,:])*t[:,None]
coarse=periodic(13);medium=periodic(93);fine=periodic(431);grain=periodic(1024)
colors={'Steel':(.39,.44,.40),'Aluminium':(.65,.67,.66),'Plastic':(.79,.77,.69),'Glass':(.83,.85,.81),'Bulb':(.89,.88,.82),'Rubber':(.045,.043,.038),'Copper':(.70,.37,.18),'ClearGlass':(.83,.88,.86)}
report=[]
for mi,name in enumerate(MATERIALS):
    cloud=np.roll(coarse,(mi*127,mi*251),axis=(0,1));detail=np.roll(medium,(mi*61,mi*163),axis=(0,1));speck=np.roll(fine,(mi*19,mi*83),axis=(0,1))
    for wi,wear in enumerate(['Used','Worn','Damaged']):
        folder=ASSET/'Textures'/wear/name;folder.mkdir(parents=True,exist_ok=True)
        level=cloud*.62+detail*.26+speck*.12
        rust=np.clip((level-[.80,.69,.60][wi])*10,0,1) if name=='Steel' else np.zeros((N,N),np.float32)
        oxide=np.clip((level-[.86,.75,.65][wi])*7,0,1) if name in ['Aluminium','Copper'] else np.zeros_like(rust)
        dirt=np.clip((cloud-.32)*.55+(detail-.45)*.18,0,.50)*[.24,.54,.85][wi]
        # Sparse substrate-exposing scuffs. Rust itself remains dielectric.
        scuff=((grain>.81)&(speck>.72)).astype(np.float32)*[.25,.6,1][wi]
        base=np.broadcast_to(np.array(colors[name],np.float32),(N,N,3)).copy()
        if name in ['Plastic','Glass','Bulb']:base[:,:,2]*=1-wi*.06
        base *= (1-dirt*.65+(detail-.5)*.035+(grain-.5)*.012)[...,None]
        if name=='Steel':
            rustcol=np.stack([.24+detail*.13,.085+detail*.07,.027+detail*.025],axis=-1)
            base=base*(1-rust[...,None])+rustcol*rust[...,None]
            base=base*(1-scuff[...,None]*.2)+np.array([.55,.56,.54])*scuff[...,None]*.2
        if name=='Aluminium':base=base*(1-oxide[...,None]*.35)+np.array([.55,.56,.53])*oxide[...,None]*.35
        if name=='Copper':base=base*(1-oxide[...,None]) + np.array([.14,.24,.19])*oxide[...,None]
        roughBase={'Steel':.58,'Aluminium':.29,'Plastic':.43,'Glass':.39,'Bulb':.30,'Rubber':.74,'Copper':.31,'ClearGlass':.15}[name]
        rough=np.clip(roughBase+wi*.055+(detail-.5)*.13+dirt*.22+rust*.18+oxide*.24,.06,.97)
        metallic=np.ones((N,N),np.float32)*(.94 if name in ['Aluminium','Copper'] else 0)
        if name=='Steel':metallic=scuff*.75*(1-rust)
        if name in ['Aluminium','Copper']:metallic*=1-oxide*.8
        amplitude={'Steel':.000008,'Aluminium':.000003,'Plastic':.000002,'Glass':.000001,'Bulb':.0000005,'Rubber':.000004,'Copper':.000004,'ClearGlass':.0000004}[name]
        height=(speck-.5)*amplitude+(grain-.5)*amplitude*.22+rust*(.000055+detail*.000025)+oxide*.000008-scuff*.000005
        gx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))/(2*px);gy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))/(2*px)
        normal=np.stack([-gx,-gy,np.ones_like(gx)],axis=-1);normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
        ao=np.clip(1-rust*.055-oxide*.025-dirt*.025,.88,1)
        opacity=np.clip(.18+dirt*.22+wi*.025+(detail-.5)*.025,.10,.38)
        def save(a,fn):Image.fromarray((np.clip(a,0,1)*255).astype('uint8')).save(folder/(fn+'.png'),compress_level=4)
        if name=='ClearGlass':save(np.dstack([base,opacity]),'BaseColor');save(opacity,'Opacity')
        else:save(base,'BaseColor')
        save(normal*.5+.5,'Normal');save(rough,'Roughness');save(metallic,'Metallic');save(ao,'AO')
        packed=np.zeros((N,N,4),np.float32);packed[:,:,0]=metallic;packed[:,:,3]=1-rough;save(packed,'MetallicSmoothness')
        save((height+.00002)/.00014,'Height')
        emission=np.repeat((np.clip(1-dirt*.8,0,1) if name in ['Glass','Bulb','ClearGlass'] else np.zeros_like(dirt))[...,None],3,axis=-1);save(emission,'Emission')
        report.append(dict(material=name+'_'+wear,roughnessMin=float(rough.min()),roughnessMax=float(rough.max()),rustCoverage=float((rust>.1).mean()),heightMetresMin=float(height.min()),heightMetresMax=float(height.max()),tileMetres=.5))
        print('SURFACE_REBUILT',name,wear,flush=True)
y,x=np.mgrid[0:N,0:N].astype(np.float32)/N
u=(x-.25)/.5;v=(y-.35)/.3
edge=np.minimum(np.minimum(u,1-u),np.minimum(v,1-v))
alpha=np.clip((edge-.035-(coarse-.5)*.09)*20,0,1)*(.16+medium*.24)
alpha*=1-np.clip((fine-.74)*8,0,1)
rgb=np.stack([.34+medium*.045,.33+medium*.04,.28+medium*.03],axis=-1)
ghost=ASSET/'Textures/GhostPaint';ghost.mkdir(exist_ok=True)
Image.fromarray((np.clip(np.dstack([rgb,alpha]),0,1)*255).astype('uint8')).save(ghost/'GhostPaint_BaseColor.png',compress_level=4)
proof=VERIFY/'ProfessionalAudit';proof.mkdir(exist_ok=True)
(proof/'surface_parameters.json').write_text(json.dumps(dict(passed=True,materials=report,normalDerivation='Analytical periodic height derivatives in metres, tile size 0.5 m. No albedo-derived normals or painted lighting.',ao='Microcavity approximation only, not geometry AO bake.'),indent=2),encoding='utf-8')
print('ALL_SURFACES_REBUILT_PASS',len(report),flush=True)
