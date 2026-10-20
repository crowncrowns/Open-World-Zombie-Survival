"""Deterministic ceramic, woven rugs and salvage finishes for the Kurs interior."""
import argparse, hashlib, json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from stage6_palette import PALETTE6

def noise(rng,n,cells):
    a=rng.random((cells,cells)).astype('float32')
    return np.asarray(Image.fromarray(a).resize((n,n),Image.Resampling.BICUBIC))

def surface(name,n=1024):
    rng=np.random.default_rng(6100+list(PALETTE6).index(name))
    yy,xx=np.mgrid[:n,:n];u=xx/n;v=yy/n
    fine=rng.random((n,n)).astype('float32');broad=noise(rng,n,12);stain=noise(rng,n,35)
    base=np.array(PALETTE6[name][:3],dtype='float32')
    rgb=np.broadcast_to(base,(n,n,3)).copy();height=(fine-.5)*.0006;metal=np.zeros((n,n));smooth=np.ones((n,n))*.16
    if name=='CeramicTile':
        tx,ty=xx//256,yy//256;ix,iy=xx%256,yy%256
        red=((tx+ty)%3==0)|((tx==2)&(ty==2))
        tile_noise=rng.uniform(.91,1.07,(4,4))[ty,tx]
        rgb=np.where(red[...,None],np.array([.43,.19,.115]),np.array([.72,.69,.59]))*tile_noise[...,None]
        edge=np.minimum.reduce([ix,iy,255-ix,255-iy]);grout=edge<3
        dirt=np.clip((.55-broad)*1.15,0,.37)+np.clip((.43-stain)*.3,0,.12)
        scratches=((np.sin(xx*.13+yy*.012)>.995)&(stain<.48))*.18
        rgb*=((1-dirt)*(1-scratches))[...,None]
        rgb+=((fine-.5)*.023)[...,None]
        rgb[grout]=np.array([.19,.175,.14])
        # Fine bevels, occasional chipped ceramic and dirt-packed grout.
        height=np.clip(edge/6,0,1)*.006+(fine-.5)*.0003
        chips=(noise(rng,n,100)>.75)&(edge<11)
        rgb[chips]*=.62;height[chips]-=.002
        smooth=np.clip(.42-dirt*.8+((fine-.5)*.06),.09,.47);smooth[grout]=.06
    elif name.startswith('Rug'):
        im=Image.new('RGB',(n,n),tuple((base*255).astype('uint8')));draw=ImageDraw.Draw(im)
        ochre=(156,126,72);cream=(169,153,112);dark=(37,34,27)
        for margin,width,col in [(12,8,dark),(28,5,cream),(45,25,ochre),(76,5,dark),(91,4,cream),(113,9,ochre)]:
            draw.rectangle((margin,margin,n-margin-1,n-margin-1),outline=col,width=width)
        if name=='RugWoven':
            for y in range(128,n-128,47):
                draw.rectangle((128,y,n-128,y+14),fill=ochre if y%2 else cream)
                for x in range(145,n-128,69):draw.polygon([(x,y-14),(x+13,y+4),(x,y+24),(x-13,y+4)],fill=dark)
        else:
            for y in range(166,n-140,124):
                for x in range(166,n-140,124):
                    draw.polygon([(x,y-41),(x+37,y),(x,y+41),(x-37,y)],fill=ochre,outline=dark)
                    draw.polygon([(x,y-23),(x+20,y),(x,y+23),(x-20,y)],fill=cream)
                    draw.rectangle((x-5,y-5,x+5,y+5),fill=dark)
            for x in range(60,n-50,51):
                for y in (61,n-62):draw.polygon([(x-10,y),(x,y-12),(x+10,y),(x,y+12)],fill=dark)
            for y in range(60,n-50,51):
                for x in (61,n-62):draw.polygon([(x-10,y),(x,y-12),(x+10,y),(x,y+12)],fill=dark)
        rgb=np.asarray(im).astype('float32')/255
        weave=((xx%4==0)|(yy%5==0))*.065
        wear=np.clip((broad-.58)*.75,0,.22)
        rgb=rgb*(1-weave[...,None])*(.82+.23*stain[...,None])
        rgb=rgb*(1-wear[...,None])+np.array([.39,.33,.22])*wear[...,None]
        height=(np.sin(xx*np.pi/2)+np.sin(yy*np.pi/2.5))*.0005;smooth*=.2
    elif name.startswith('Canvas'):
        weave=(np.sin(xx*np.pi/3)+np.sin(yy*np.pi/4))*.035
        fade=.75+.30*broad+.12*stain+weave
        rgb*=fade[...,None]
        height=(np.sin(xx*np.pi/3)+np.sin(yy*np.pi/4))*.0006
        rgb[np.logical_or(xx%237<3,yy%301<3)]*=.77
    elif name=='TimberRaw':
        grain=np.sin(yy*.032+np.sin(xx*.009)*2+noise(rng,n,24)*4)
        rgb*=((.67+.27*broad+.18*grain)*(.94+.1*fine))[...,None]
        cracks=(np.sin(yy*.055+noise(rng,n,16)*3)>.993)&(stain<.55)
        rgb[cracks]*=.35;height=grain*.001
    elif name=='Paper':
        rgb*=(.78+.32*broad)[...,None]
        lines=(yy%38<2)&(xx>70)&(xx<n-70)
        rgb[lines]*=.66
    else:
        rgb*=(.72+.31*broad+.09*fine)[...,None]
        if name=='Copper':metal[:]=.65;smooth[:]=.3
        if name=='Enamel':smooth[:]=.38;rgb[stain<.18]*=.35
        if name=='PlasticDark':smooth[:]=.22
    gx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*n/4
    gy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*n/4
    normal=np.stack([-gx,-gy,np.ones_like(gx)],-1);normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
    mask=np.zeros((n,n,4),dtype='float32');mask[...,0]=metal;mask[...,3]=smooth
    return {'BaseColor':np.clip(rgb,0,1),'Normal':normal*.5+.5,'MetallicSmoothness':mask}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--root',type=Path,required=True);args=parser.parse_args()
    dest=args.root/'Assets/Environment/KursHub/Textures';dest.mkdir(parents=True,exist_ok=True)
    report=[];thumbs=[]
    for name in PALETTE6:
        if name in ('Ember','LeafGreen'):continue
        for kind,a in surface(name).items():
            p=dest/f'{name}_{kind}.png';Image.fromarray(np.rint(a*255).astype('uint8')).save(p,optimize=True)
            report.append(dict(file=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
            if kind=='BaseColor':thumbs.append((name,Image.open(p).resize((230,230))))
    sheet=Image.new('RGB',(1000,1000),(25,25,25));draw=ImageDraw.Draw(sheet)
    for i,(name,im) in enumerate(thumbs):
        x,y=(i%4)*250,(i//4)*250;sheet.paste(im,(x,y));draw.text((x,y+232),name,fill='white')
    report_dir=args.root/'work/KursHub';report_dir.mkdir(parents=True,exist_ok=True)
    sheet.save(report_dir/'InteriorTextureContactSheet.png');(report_dir/'InteriorTextures.json').write_text(json.dumps(report,indent=2))
    print('KURS_INTERIOR_TEXTURES_OK',len(report),'PBR maps')

if __name__=='__main__':main()
