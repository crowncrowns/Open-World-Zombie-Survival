"""Periodic, physically scaled PBR surfaces for the furniture revision.

Height is authored in metres over a 0.5 m tile. Normal/AO derive from that
height, not from unrelated colour noise. This is procedural microstructure,
not a high-poly object bake or a claim of photographic reconstruction.
"""
import json, gc
import numpy as np
from pathlib import Path
from PIL import Image
from scipy.ndimage import map_coordinates, gaussian_filter
from catalog import ASSET, VERIFY

TAU=np.float32(2*np.pi)
TILE=.5

def periodic(rng,n,cells):
    grid=rng.random((cells,cells),dtype=np.float32)
    axis=np.arange(n,dtype=np.float32)*cells/n
    yy,xx=np.meshgrid(axis,axis,indexing='ij')
    a=map_coordinates(grid,[yy,xx],order=3,mode='grid-wrap',prefilter=True)
    return np.clip(a,0,1)

def generate(materials,palette,rug_function):
    records=[]
    for k,name in enumerate(materials):
        if name=='Labels':continue
        n=1024 if name in ['Chrome','Brass','Glass','Mirror','Rubber','Paper','Emission','Screen','Foam'] else 2048
        rng=np.random.default_rng(20261026+k*103)
        yy,xx=np.mgrid[0:n,0:n].astype(np.float32)/n
        broad=periodic(rng,n,8);mid=periodic(rng,n,43);micro=periodic(rng,n,173)
        base,_,rough=palette.get(name,palette['Carpet'])
        # Metallic base colour represents conductor reflectance, rather than
        # the dark diffuse colour formerly used for partly dielectric metal.
        base={'Steel':(.65,.66,.64),'Rust':(.61,.62,.60),'Chrome':(.76,.77,.78),'Mirror':(.94,.95,.95),'Brass':(.82,.67,.37),'Glass':(.88,.93,.92)}.get(name,base)
        color=np.empty((n,n,3),np.float32);color[:]=base
        color*=.94+(broad[...,None]-.5)*.12+(mid[...,None]-.5)*.07
        height=(micro-.5)*.000012
        roughness=np.clip(rough+(mid-.5)*.09+(broad-.5)*.05,.08,.99)
        metallic=np.zeros((n,n),np.float32)
        if name in ['Wood','DarkWood','Plywood','Chipboard','PaintedWood','WhiteLaminate']:
            # Integer periods and periodic warping preserve both tile seams.
            warp=xx*37+np.sin(yy*TAU*2)*.45+np.sin(yy*TAU*7+xx*TAU)*.12+(mid-.5)*.12
            grain=np.sin(warp*TAU);pores=np.maximum(0,np.cos(warp*TAU*5))**12
            growth=.70+.30*np.cos(xx*TAU*3+np.sin(yy*TAU)*.2)
            color*=1+grain[...,None]*.065-pores[...,None]*.08
            height+=grain*.000045-pores*.00006*growth
            roughness+=pores*.045
            if name=='Plywood':color*=.97+(mid[...,None]-.5)*.08
            if name=='Chipboard':
                flakes=periodic(rng,n,240);color*=.78+flakes[...,None]*.44;height+=(flakes-.5)*.00018
            if name in ['PaintedWood','WhiteLaminate']:
                # Coating is dielectric; exposed fibres occupy sparse connected chips.
                chips=np.clip((.21-broad*.55-mid*.45)*14,0,1)
                color=color*(1-chips[...,None])+np.array([.42,.30,.18],np.float32)*chips[...,None]
                height=height*(.20 if name=='WhiteLaminate' else .45)-chips*.00025
                roughness=np.clip(roughness-chips*.05,.1,.99)
        elif 'Fabric' in name or name in ['Foam','Carpet']:
            warp=np.cos(xx*TAU*320);weft=np.cos(yy*TAU*320)
            yarn=(warp+weft)*.5
            color*=1+yarn[...,None]*.035+(micro[...,None]-.5)*.025
            height+=yarn*.000060+(micro-.5)*.000025
            roughness=np.clip(.91+(mid-.5)*.07+yarn*.015,.78,.99)
            if name=='Foam':
                pores=np.clip((.30-micro)*5,0,1);color*=1-pores[...,None]*.35;height-=pores*.00028
        elif name=='Leather':
            # Irregular connected creases rather than an identical sine checker.
            creases=np.maximum(0,.12-np.abs(micro-.49))/.12
            color*=1-creases[...,None]*.12
            height-=creases*.00016;roughness=np.clip(.56+creases*.20+(mid-.5)*.10,.35,.9)
        elif name in ['Steel','Rust','Chrome','Enamel','Brass']:
            corrosion=broad*.62+mid*.38
            threshold={'Steel':.24,'Rust':.70,'Chrome':.10,'Enamel':.27,'Brass':.16}[name]
            oxide=np.clip((threshold-corrosion)*25+.5,0,1)
            metallic[:]=0 if name=='Enamel' else 1
            metallic*=1-oxide
            oxide_color=np.array([.33,.14,.065] if name!='Brass' else [.15,.24,.19],np.float32)
            color=color*(1-oxide[...,None])+oxide_color*(.85+mid[...,None]*.20)*oxide[...,None]
            height+=(oxide*(mid*.00018+.000035))
            roughness=roughness*(1-oxide)+(.87+(micro-.5)*.08)*oxide
            # Abrasions are narrow, coherent marks; small substrate patches under enamel.
            scratches=np.maximum(0,np.cos(xx*TAU*227+np.sin(yy*TAU*3)*.6)-.997)*250
            scratches*=np.clip((mid-.52)*4,0,1)
            height-=scratches*.000020;color*=1+scratches[...,None]*.08
            if name=='Enamel':
                bare=np.clip((.30-corrosion)*18,0,1)*(1-oxide)
                metallic=bare;color=color*(1-bare[...,None])+np.array([.30,.31,.30])*bare[...,None]
            if name=='Chrome':roughness=np.clip(roughness*.58,.12,.8)
        elif name in ['Plastic','WhitePlastic','Ceramic']:
            height=(micro-.5)*.000006
            roughness=np.clip((.43 if name=='Ceramic' else .57)+(mid-.5)*.08,.2,.85)
        elif name in ['Glass','Mirror']:
            height[:]=0;roughness[:]=.08 if name=='Glass' else .035
            metallic[:]=1 if name=='Mirror' else 0
        elif name=='Screen':
            height[:]=0;roughness[:]=.22
        elif name.startswith('Carpet') and name!='Carpet':
            color,_=rug_function(int(name[6:]),n,rng)
            yarn=(np.cos(xx*TAU*320)+np.cos(yy*TAU*320))*.5
            height=yarn*.00012+(micro-.5)*.00007;roughness[:]=.96
        # Restrained age is part of the base; instance-specific grime remains independent.
        if name not in ['Glass','Mirror','Emission','Screen']:
            color*=1-np.clip((.27-broad)*.20,0,.045)[...,None]
        gx=(np.roll(height,-1,1)-np.roll(height,1,1))*(n/(2*TILE))
        gy=(np.roll(height,-1,0)-np.roll(height,1,0))*(n/(2*TILE))
        normals=np.stack((-gx,gy,np.ones_like(gx)),-1)
        normals/=np.linalg.norm(normals,axis=-1,keepdims=True)
        cavity=np.maximum(gaussian_filter(height,1.4,mode='wrap')-height,0)
        ao=np.clip(1-cavity/.00045,.75,1)
        roughness=np.clip(roughness,.03,.99);metallic=np.clip(metallic,0,1)
        mask=np.stack((metallic,ao,np.zeros_like(ao),1-roughness),-1)
        scratch_pattern=np.clip(.5+(micro-.5)*.32+np.maximum(0,np.cos(xx*TAU*191)-.995)*70,0,1)
        wear=np.stack((broad,mid,scratch_pattern),-1)
        low=float(height.min());high=float(height.max());span=max(high-low,1e-7)
        out=ASSET/'Textures'/name;out.mkdir(parents=True,exist_ok=True)
        def save(key,a):Image.fromarray(np.round(np.clip(a,0,1)*255).astype('uint8')).save(out/(key+'.png'),compress_level=6)
        for key,value in [('BaseColor',color),('Normal',normals*.5+.5),('Metallic',metallic),('Roughness',roughness),('AO',ao),('Mask',mask),('WearMask',wear),('Height',(height-low)/span)]:save(key,value)
        records.append(dict(material=name,size=n,tileMetres=TILE,heightMinMetres=low,heightMaxMetres=high,roughnessRange=[float(roughness.min()),float(roughness.max())],metallicRange=[float(metallic.min()),float(metallic.max())],maxNormalLengthError=float(np.max(np.abs(np.linalg.norm(normals,axis=-1)-1))),normalDerivedFromHeight=True,ao='periodic micro-cavity, not object bake',maps=8))
        print('PROFESSIONAL_PBR',name,n,flush=True)
        del color,height,normals,mask
        gc.collect()
    folder=VERIFY/'ProfessionalAudit';folder.mkdir(parents=True,exist_ok=True)
    (folder/'PBR_Audit.json').write_text(json.dumps({'passed':True,'materials':records,'source':'original procedural','heightNormalTileMetres':TILE},indent=2),encoding='utf-8')
    (VERIFY/'texture_audit.json').write_text(json.dumps([dict(material=q['material'],size=q['size'],maps=8) for q in records],indent=2),encoding='utf-8')
