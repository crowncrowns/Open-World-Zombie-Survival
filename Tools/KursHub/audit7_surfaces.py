"""Audit and rebuild project-owned Kurs PBR maps; procedural, not scans.

Run under the runtime Python with NumPy/Pillow. No source Assets are mutated:
--root is a staging project root. Existing names are retained for GUID-safe
replacement. Heights are metres, normals use +Y (OpenGL), PNG rows point -V.
"""
from __future__ import annotations

import argparse
import gc
import hashlib
import json
import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

FAMILIES = {
    'BluePaint': (2048, 4), 'CreamConcrete': (2048, 4),
    'RustedSteel': (2048, 4), 'RoofMembrane': (2048, 4),
    'FloorConcrete': (2048, 4), 'WornWood': (2048, 1),
    'TarpGreen': (2048, 4), 'SandCanvas': (2048, 4),
    'CeramicTile': (4096, 8), 'TimberRaw': (2048, 1),
    'CanvasBlue': (2048, 4), 'CanvasBeige': (2048, 4),
    'CanvasRed': (2048, 4), 'CanvasDark': (2048, 4),
    'RugRed': (2048, 2.8), 'RugBlue': (2048, 2.8),
    'RugWoven': (2048, 2.8), 'PlasticDark': (1024, 4),
    'Enamel': (1024, 4), 'Copper': (1024, 4),
    'Paper': (1024, 4), 'ClothInk': (1024, 4), 'InkDark': (1024, 4),
    'GlassGrime': (2048, 4),
}
COLORS = {
    'BluePaint': (.055,.245,.425), 'CreamConcrete': (.70,.675,.605),
    'RustedSteel': (.19,.205,.20), 'RoofMembrane': (.265,.27,.275),
    'FloorConcrete': (.43,.44,.425), 'WornWood': (.37,.30,.21),
    'TarpGreen': (.20,.265,.185), 'SandCanvas': (.53,.48,.35),
    'TimberRaw': (.39,.285,.175), 'CanvasBlue': (.075,.14,.23),
    'CanvasBeige': (.49,.42,.29), 'CanvasRed': (.32,.085,.055),
    'CanvasDark': (.07,.075,.065), 'PlasticDark': (.105,.13,.075),
    'Enamel': (.66,.63,.49), 'Copper': (.67,.37,.18), 'Paper': (.64,.59,.43),
    'ClothInk': (.78,.75,.64), 'InkDark': (.028,.025,.020),
    'RugRed': (.34,.075,.045), 'RugBlue': (.065,.13,.18),
    'RugWoven': (.42,.29,.15), 'GlassGrime': (.56,.65,.66),
}
MATERIALS = {
    'BlueCladding':'BluePaint','MallBlue':'BluePaint','Concrete':'CreamConcrete',
    'CreamPanel':'CreamConcrete','Floor':'FloorConcrete','Frame':'RustedSteel',
    'SteelOxide':'RustedSteel','Roof':'RoofMembrane','Glass':'GlassGrime',
    **{n:n for n in FAMILIES if n not in ('BluePaint','CreamConcrete','RustedSteel','RoofMembrane','FloorConcrete','GlassGrime')},
}


def noise(n, cells, rng):
    """Periodic value noise, cubic interpolation, bounded intermediate memory."""
    gy, gx = (cells,cells) if isinstance(cells,int) else cells
    a = rng.random((gy,gx),dtype=np.float32)
    tx = np.arange(n,dtype=np.float32)*(gx/n)
    ix = np.floor(tx).astype(np.int32)
    fx = (tx-ix).astype(np.float32);fx=fx*fx*(3-2*fx)
    out = np.empty((n,n),dtype=np.float32)
    for start in range(0,n,128):
        ty = np.arange(start,min(n,start+128),dtype=np.float32)*(gy/n)
        iy = np.floor(ty).astype(np.int32)
        fy = (ty-iy).astype(np.float32);fy=fy*fy*(3-2*fy)
        lo = a[iy[:,None]%gy,ix[None,:]%gx]*(1-fx)[None,:]+a[iy[:,None]%gy,(ix[None,:]+1)%gx]*fx[None,:]
        hi = a[(iy[:,None]+1)%gy,ix[None,:]%gx]*(1-fx)[None,:]+a[(iy[:,None]+1)%gy,(ix[None,:]+1)%gx]*fx[None,:]
        out[start:start+len(ty)] = lo*(1-fy)[:,None]+hi*fy[:,None]
    return out


def rgb(base, variation):
    return np.clip(np.asarray(base,dtype=np.float32)[None,None,:]+variation[...,None],0,1)


def surface(name,n,period):
    seed=int.from_bytes(hashlib.sha256(('KursAudit7:'+name).encode('ascii')).digest()[:4],'big')
    rng=np.random.default_rng(seed)
    broad=noise(n,7,rng);medium=noise(n,37,rng);fine=noise(n,213,rng)
    grain=rng.random((n,n),dtype=np.float32)
    # Pixel grids remain float32, including the 4K hero floor.
    y,x=np.meshgrid(np.arange(n,dtype=np.float32)/n,np.arange(n,dtype=np.float32)/n,indexing='ij')
    var=(broad-.5)*.035+(medium-.5)*.028+(fine-.5)*.011
    color=rgb(COLORS.get(name,(.7,.6,.5)),var)
    height=(fine-.5)*.00015+(grain-.5)*.00005
    rough=np.clip(.76+(medium-.5)*.1,0,1)
    metal=np.zeros((n,n),np.float32)
    ao=np.ones((n,n),np.float32)
    if name=='CeramicTile':
        tiles=int(period/.5);tx=np.floor(x*tiles).astype(np.int32);ty=np.floor(y*tiles).astype(np.int32)
        ix=(x*tiles-tx).astype(np.float32);iy=(y*tiles-ty).astype(np.float32)
        edge=np.minimum.reduce((ix,iy,1-ix,1-iy))*.5
        layout=rng.random((tiles,tiles),dtype=np.float32)
        red=layout[ty,tx]<.34
        tile_color=rng.uniform(.92,1.06,(tiles,tiles)).astype(np.float32)[ty,tx]
        color=np.where(red[...,None],np.array((.465,.205,.13),np.float32),np.array((.78,.755,.685),np.float32))*tile_color[...,None]
        # Multiple scales of wear, not a dark border painted around every tile.
        dirt=np.clip((.56-broad)*.7,0,.27)+np.clip((.45-medium)*.45,0,.13)
        scuff=np.clip((noise(n,(41,9),rng)-.58)*.40,0,.10)*np.clip((fine-.40)*2,0,1)
        color*=((1-dirt)*(1-scuff))[...,None]
        color+=((grain-.5)*.009)[...,None]
        grout=edge<.0032
        bevel=np.clip((edge-.0032)/.0025,0,1)
        color[grout]=np.array((.205,.19,.16),np.float32)
        color[grout]*=(.84+.22*medium[grout,None])
        height=bevel*.0035+(fine-.5)*.00014+(grain-.5)*.00004
        chip=(fine>.83)&(edge<.012)&(medium>.60)
        color[chip]*=.66;height[chip]-=.0014
        rough=np.clip(.32+dirt*1.5+scuff+(medium-.5)*.12,.23,.84)
        damp=(broad>.66)&(medium>.38);rough[damp]*=.74
        rough[grout]=.94
        ao=1-(1-bevel)*.19;ao[chip]*=.92
    elif name in ('WornWood','TimberRaw'):
        # Longitudinal grain with nonuniform growth lines and periodic knots.
        warp=noise(n,(11,3),rng)
        phase=x*2*np.pi*197+(warp-.5)*13+(broad-.5)*6
        growth=(np.sin(phase)*.5+.5)**7
        pores=np.clip((noise(n,(9,901),rng)-.74)*3.4,0,1)
        knots=np.zeros((n,n),np.float32)
        for cx,cy,rx,ry in ((.24,.31,.055,.16),(.74,.82,.035,.11)):
            dx=np.minimum(abs(x-cx),1-abs(x-cx))/rx;dy=np.minimum(abs(y-cy),1-abs(y-cy))/ry
            radius=np.sqrt(dx*dx+dy*dy)
            knots+=np.exp(-radius*radius*1.7)*(.65+.35*np.sin(radius*29))
        color=rgb(COLORS[name],var*1.8-growth*.043-pores*.058-knots*.075)
        height=(medium-.5)*.00035-growth*.0004-pores*.0011-knots*.0005
        rough=np.clip(.73+(medium-.5)*.15+pores*.14,.55,.96)
        ao=1-pores*.14-knots*.045
    elif name.startswith('Canvas') or name in ('TarpGreen','SandCanvas'):
        # At 4 m the yarn spacing is ~2 mm. Albedo contribution is deliberately
        # weak: yarn relief belongs in normals, not a large checker pattern.
        frequency=round(period/.0022)
        # Two-millimetre yarn is below Nyquist in a 4 m / 2K map. Do not sample
        # it into a moire/checker. Subpixel yarn is represented by roughness and
        # a weak stochastic fibre normal, as it is after physical minification.
        weave=(grain-.5)*.4 if frequency>n/3 else (np.sin(x*np.pi*2*frequency)+np.sin(y*np.pi*2*frequency))*.5
        # Weathered cloth has low-frequency fading, not a thresholded value-
        # noise camouflage print. Geometry supplies the visible folds/shadows;
        # albedo only supplies subtle dye variation and non-directional grime.
        stain=np.clip((.44-broad)*.16,0,.05)+np.clip((.44-medium)*.10,0,.02)
        fade=(broad-.5)*.028+(medium-.5)*.009+(fine-.5)*.004
        color=rgb(COLORS[name],fade-stain*.24+weave*.0018)
        height=weave*.00009+(fine-.5)*.00012
        rough=np.clip(.86+(medium-.5)*.1+stain*.2,.72,.98)
        ao=1-np.maximum(0,-weave)*.035
    elif name.startswith('Rug'):
        # A unique 2.8 x 1.8 m rug UV; small woven motifs, faded medium-scale dye.
        im=Image.new('RGB',(n,n),tuple(round(v*255) for v in COLORS[name]));draw=ImageDraw.Draw(im)
        s=n/1024
        cream=(161,146,111);ochre=(139,106,61);dark=(39,34,28)
        for margin,width,col in ((18,8,dark),(34,5,cream),(52,20,ochre),(80,6,dark),(98,3,cream),(114,6,ochre)):
            m=round(margin*s);draw.rectangle((m,m,n-m-1,n-m-1),outline=col,width=max(1,round(width*s)))
        for yy in range(154,900,62):
            for xx in range(154,900,62):
                r=17 if name=='RugWoven' else 23
                pts=[(round((xx+dx)*s),round((yy+dy)*s)) for dx,dy in ((0,-r),(r*.75,0),(0,r),(-r*.75,0))]
                draw.polygon(pts,fill=ochre,outline=dark)
                draw.line((round((xx-7)*s),round(yy*s),round((xx+7)*s),round(yy*s)),fill=cream,width=round(3*s))
        color=np.asarray(im,dtype=np.float32)/255
        wear=np.clip((broad-.52)*.6,0,.21)
        color*= (.83+.22*medium)[...,None]
        color=color*(1-wear[...,None])+np.array((.36,.31,.235),np.float32)*wear[...,None]
        # UV axes have different physical widths, so count yarns independently.
        weave=(grain-.5)*.4
        color*=1+weave[...,None]*.006
        height=weave*.00013+(fine-.5)*.00013
        rough=np.clip(.92+(medium-.5)*.05,.85,.99)
        ao=1-np.maximum(0,-weave)*.045
    elif name=='BluePaint':
        chips=np.clip((fine-.88)*5,0,.5)*(medium>.57)
        dirty=np.clip((noise(n,(7,149),rng)-.57)*.12,0,.06)
        color=rgb(COLORS[name],var-dirty)
        color=color*(1-chips[...,None])+np.array((.27,.145,.075),np.float32)*chips[...,None]
        height=(fine-.5)*.00014-chips*.00025
        rough=np.clip(.58+(medium-.5)*.16+chips*.5+dirty,.4,.95)
        # Both paint and iron oxide are dielectrics; only exposed steel is metal.
    elif name=='RustedSteel':
        rust=np.clip((broad*.48+medium*.42+fine*.1-.34)*2.4,0,1)
        oxide=rgb((.40,.225,.115),var*1.6)
        color=color*(1-rust[...,None])+oxide*rust[...,None]
        height=(fine-.5)*.00025+rust*.00045+(grain-.5)*.0001
        metal=.94*(1-rust)**3
        rough=np.clip(.35+rust*.54+(medium-.5)*.08,.26,.96)
        ao=1-np.maximum(0,fine-.65)*.18*rust
    elif name in ('CreamConcrete','FloorConcrete'):
        pits=np.clip((grain-.976)*30,0,.68)
        # Rain streaks belong on facade concrete. A horizontal walking surface
        # keeps isotropic wear instead of inheriting vertical wall drips.
        stain_noise=noise(n,(5,119) if name=='CreamConcrete' else 19,rng)
        streak=np.clip((stain_noise-.55)*(.12 if name=='CreamConcrete' else .08),0,.045)
        color=rgb(COLORS[name],var-streak-pits*.035)
        height=(fine-.5)*.0005-pits*.0007+(grain-.5)*.00008
        rough=np.clip(.86+(medium-.5)*.08,.76,.97)
        ao=1-pits*.10
    elif name=='RoofMembrane':
        height=(fine-.5)*.0006+(grain-.5)*.00025
        rough=np.clip(.9+(medium-.5)*.09,.78,.98)
    elif name=='Copper':
        # Combining unrelated lattice frequencies avoids the previous square
        # stamp-like islands, especially visible on the small copper map.
        patina_field=broad*.48+noise(n,19,rng)*.28+noise(n,61,rng)*.16+fine*.08
        patina=np.clip((patina_field-.46)*2.4,0,.76)
        color=color*(1-patina[...,None])+np.array((.10,.25,.19),np.float32)*patina[...,None]
        metal=.97*(1-patina)**2
        rough=np.clip(.28+patina*.6+(medium-.5)*.12,.2,.9)
        height=(fine-.5)*.00012+patina*.00015
    elif name=='Enamel':
        chips=np.clip((fine-.88)*5,0,.5)*(medium>.57)
        color=color*(1-chips[...,None])+np.array((.15,.115,.085),np.float32)*chips[...,None]
        rough=np.clip(.29+(medium-.5)*.10+chips*.6,.21,.87)
        height=(fine-.5)*.000045-chips*.00017
    elif name=='PlasticDark':
        rough=np.clip(.57+(medium-.5)*.17,.43,.77)
        height=(fine-.5)*.00008
    elif name=='Paper':
        color=rgb(COLORS[name],var*1.1)
        # No fake ruled lines on every package: papers have distinct object shapes.
        height=(fine-.5)*.000065;rough[:]=.91
    elif name in ('ClothInk','InkDark'):
        color=rgb(COLORS[name],var*.14)
        height=(fine-.5)*.000018;rough[:]=.91
    elif name=='GlassGrime':
        streak=noise(n,(5,103),rng)
        grime=np.clip((streak-.48)*.43+(medium-.5)*.07,0,.24)
        c=rgb(COLORS[name],(broad-.5)*.018-grime*.12)
        color=np.concatenate((c,(.25+grime)[...,None]),axis=-1)
        rough=np.clip(.11+grime*1.9+(fine-.5)*.014,.075,.66)
        height=(fine-.5)*.000008*grime;ao[:]=1
    else:
        raise ValueError(name)
    return dict(BaseColor=np.clip(color,0,1).astype(np.float32), Height=height.astype(np.float32),
                Roughness=np.clip(rough,0,1).astype(np.float32), Metallic=np.clip(metal,0,1).astype(np.float32),
                Occlusion=np.clip(ao,0,1).astype(np.float32))


def normal_map(height,period_y,period_x):
    n=height.shape[0]
    dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*(n/(2*period_x))
    dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*(n/(2*period_y))
    v=np.stack((-dx,dy,np.ones_like(height)),axis=-1)
    v/=np.linalg.norm(v,axis=-1,keepdims=True)
    return v*.5+.5


def write_png(array,path,height=False):
    if height:
        pixels=np.rint(np.clip(array,0,1)*65535).astype(np.uint16)
    else:
        pixels=np.rint(np.clip(array,0,1)*255).astype(np.uint8)
    Image.fromarray(pixels).save(path,optimize=True)


def record(path):
    with Image.open(path) as im:
        a=np.asarray(im);size=im.size;mode=im.mode
    return dict(file=path.name,resolution=list(size),mode=mode,
                min=np.min(a,axis=(0,1)).tolist(),max=np.max(a,axis=(0,1)).tolist(),
                sha256=hashlib.sha256(path.read_bytes()).hexdigest(),bytes=path.stat().st_size)


def audit_before(source,out):
    folder=source/'Assets/Environment/KursHub'
    guid_to_path={}
    for p in folder.rglob('*.meta'):
        match=re.search(r'^guid: ([a-f0-9]+)$',p.read_text(encoding='utf8'),re.M)
        if match:guid_to_path[match[1]]=str(p.relative_to(source))[:-5].replace('\\','/')
    maps=[record(p) for p in sorted((folder/'Textures').glob('*.png')) if 'ContactSheet' not in p.name]
    materials=[]
    for p in sorted((folder/'Materials').glob('*.mat')):
        text=p.read_text(encoding='utf8');textures={}
        for m in re.finditer(r'- (\w+):\s*\n\s+m_Texture: \{fileID: (\d+)(?:, guid: ([a-f0-9]+), type: \d+)?\}',text):
            textures[m[1]]=guid_to_path.get(m[3],None) if m[2]!='0' else None
        family=MATERIALS.get(p.stem)
        materials.append(dict(name=p.stem,family=family,version=re.search(r'KursSurfaceVersion: (\S+)',text).group(1) if 'KursSurfaceVersion:' in text else None,textures=textures))
    report=dict(scope='Kurs project-owned materials only, not vendor assets',maps=maps,materials=materials,issues=[
        dict(id='PBR-01',severity='HIGH',problem='Interior normal generator negates PNG row derivative in green; OpenGL +Y requires positive row derivative.',fix='Regenerate physical +Y normals and verify analytic ramp.'),
        dict(id='PBR-02',severity='HIGH',problem='Visible coarse weave/checker at close range, mixed scale conventions and non-periodic interior resize noise.',fix='2 mm subtle yarn relief; periodic multiscale fields; one physical UV specification.'),
        dict(id='PBR-03',severity='MEDIUM',problem='Opaque PBR has only Base/Normal/packed mask; no exchange roughness/metallic/cavity/height maps.',fix='Independent named scalar maps and physical 16-bit height; conservative cavity AO.'),
        dict(id='PBR-04',severity='HIGH',problem='Ceramic pattern repeats 4x4 tiles every 2 m; old 1K detail is lost near the player.',fix='4K 8 m hero pattern with 16x16 0.5 m tiles and per-tile color/grime.'),
        dict(id='PBR-05',severity='MEDIUM',problem='Old painted blue uses metallic .05 plus rust .48; oxide and paint are dielectrics.',fix='Separate bare metal from corrosion/coating; metallic zero for paint, cloth, tile, wood.'),
        dict(id='PBR-06',severity='MEDIUM',problem='Importer hard-caps all textures at 1024; most prop textures only 512.',fix='Manifest-controlled 1K/2K/4K cap with mipmaps; preserve all existing texture GUIDs.'),
    ])
    out.mkdir(parents=True,exist_ok=True)
    (out/'PBR_Before_Audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    return report


def contact_sheet(folder,out):
    font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
    manifest_path=folder/'Audit7PBRManifest.json'
    specs=json.loads(manifest_path.read_text(encoding='utf8'))['families'] if manifest_path.exists() else [dict(name=name,resolution=spec[0]) for name,spec in FAMILIES.items()]
    rows=(len(specs)+3)//4
    image=Image.new('RGB',(1200,rows*330),(27,29,30));draw=ImageDraw.Draw(image)
    for i,spec in enumerate(specs):
        name=spec['name']
        with Image.open(folder/(name+'_BaseColor.png')) as im:
            preview=im.convert('RGBA').resize((282,282),Image.Resampling.LANCZOS)
        bg=Image.new('RGBA',preview.size,(90,103,110,255));bg.alpha_composite(preview)
        x,y=(i%4)*300+8,(i//4)*330+8;image.paste(bg.convert('RGB'),(x,y))
        draw.text((x,y+286),f'{name} {spec["resolution"]}',font=font,fill=(220,222,220))
    image.save(out/'PBR_After_ContactSheet.png',optimize=True)


def generate(root,source,only=None):
    folder=root/'Assets/Environment/KursHub/Textures';folder.mkdir(parents=True,exist_ok=True)
    reports=root/'work/KursHub/audit7/pbr';reports.mkdir(parents=True,exist_ok=True)
    audit_before(source,reports)
    families=[];maps=[]
    for name,(n,period) in FAMILIES.items():
        if only and name not in only:continue
        data=surface(name,n,period)
        hmin,hmax=float(data['Height'].min()),float(data['Height'].max())
        scale_y=1.8 if name.startswith('Rug') else period
        data['Normal']=normal_map(data['Height'],scale_y,period)
        mask=np.zeros((n,n,4),np.float32);mask[...,0]=data['Metallic'];mask[...,3]=1-data['Roughness'];data['MetallicSmoothness']=mask
        data['Height']=(data['Height']-hmin)/max(1e-12,hmax-hmin)
        for suffix,array in data.items():
            p=folder/f'{name}_{suffix}.png';write_png(array,p,height=suffix=='Height');maps.append(record(p))
        families.append(dict(name=name,resolution=n,periodMetres=period,periodYMetres=scale_y,
                             tiling=1 if name.startswith('Rug') else 2/period,
                             normalStrength=1,heightMinMetres=hmin,heightMaxMetres=hmax,
                             alpha=name=='GlassGrime',heightUsage='exchange only; no displacement/parallax in Unity'))
        print('PBR_FAMILY_OK',name,n,flush=True)
        del data,mask;gc.collect()
    # Analytic ramp proves the tangent basis, independent of a plausible bitmap.
    ramp=np.repeat(np.arange(64,dtype=np.float32)[:,None],64,axis=1)*.00001
    ramp_n=normal_map(ramp,1,1)
    assert float(ramp_n[1:-1,:,1].mean())>.5,'PNG row +dh must yield tangent +Y green > 0.5'
    existing=folder/'Audit7PBRManifest.json'
    if only and existing.exists():
        previous=json.loads(existing.read_text(encoding='utf8'))
        replace_families={r['name'] for r in families};replace_maps={r['file'] for r in maps}
        families=[r for r in previous['families'] if r['name'] not in replace_families]+families
        maps=[r for r in previous['maps'] if r['file'] not in replace_maps]+maps
        families.sort(key=lambda r:list(FAMILIES).index(r['name']))
        maps.sort(key=lambda r:r['file'])
    report=dict(version=7,generator='Tools/KursHub/audit7_surfaces.py',provenance='Original deterministic procedural materials; not photographic scans.',
                physicalScale='Mesh UV = metres/2; manifest tiling converts to world period; rug UV is unique 0..1.',
                normalConvention='Tangent +Y / OpenGL, PNG rows run opposite UV V; no green flip.',
                channels={'BaseColor':'sRGB; alpha only for glass','Normal':'linear tangent +Y','MetallicSmoothness':'linear R=metallic A=1-roughness G=B=0','Metallic':'linear scalar','Roughness':'linear scalar','Occlusion':'linear local cavity only, not directional baked lighting','Height':'16 bit normalized; physical min/max given for each family'},
                families=families,maps=maps,validation={'analyticRampGreenPositive':True,'sourceAssetsWritten':False})
    (folder/'Audit7PBRManifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    (reports/'PBR_After_Manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    if len(families)==len(FAMILIES):contact_sheet(folder,reports)
    print('KURS_AUDIT7_PBR_OK',len(families),len(maps),flush=True)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root',type=Path,required=True);p.add_argument('--source-root',type=Path,required=True)
    p.add_argument('--only',nargs='*');args=p.parse_args();generate(args.root.resolve(),args.source_root.resolve(),args.only)


if __name__=='__main__':main()
