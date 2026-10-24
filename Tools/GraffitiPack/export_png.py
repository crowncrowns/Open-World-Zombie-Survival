"""Non-semantic export: preserve generated RGBA artwork, pad, size and catalogue."""
from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
import json,hashlib,html
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
PACK=ROOT/'Assets/PostApocalypse/GraffitiPack'
items=json.loads((ROOT/'Tools/GraffitiPack/catalog.json').read_text(encoding='utf-8'))['items']
out=ROOT/'Verification/GraffitiPack';out.mkdir(parents=True,exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',21)
report=[]
for d in items:
    src=ROOT/f"work/GraffitiPack/Source/{d['file']}.png"
    if not src.exists():continue
    im=Image.open(src).convert('RGBA');source_size=im.size
    pixels=np.array(im);pixels[:,:,3][pixels[:,:,3]<=2]=0;im=Image.fromarray(pixels)
    alpha=np.asarray(im.getchannel('A'))
    box=im.getchannel('A').getbbox()
    if not box: raise ValueError(f"Empty artwork {src}")
    # Keep all nonzero-alpha pigment. Padding protects soft spray and mip transitions.
    im=im.crop(box)
    pad=max(32,round(max(im.size)*.055));canvas=Image.new('RGBA',(im.width+2*pad,im.height+2*pad))
    canvas.paste(im,(pad,pad))
    scale=2048/max(canvas.size);size=tuple(max(1,round(s*scale)) for s in canvas.size)
    im=canvas.resize(size,Image.Resampling.LANCZOS)
    target=PACK/f"Textures/{d['category']}/{d['file']}.png";target.parent.mkdir(parents=True,exist_ok=True);im.save(target)
    a=np.asarray(im.getchannel('A'))
    report.append(dict(id=d['id'],file=str(target.relative_to(ROOT)),sourceSize=source_size,size=im.size,mode=im.mode,alphaMin=int(a.min()),alphaMax=int(a.max()),transparentPixels=int((a==0).sum()),softPixels=int(((a>0)&(a<255)).sum()),sha256=hashlib.sha256(target.read_bytes()).hexdigest(),sourceTouchesBorder=bool(alpha[0,:].any() or alpha[-1,:].any() or alpha[:,0].any() or alpha[:,-1].any())))
    d['height']=round(d['width']*im.height/im.width,3)
    d['pixels']=im.size

(out/'texture_validation.json').write_text(json.dumps(dict(count=len(report),uniqueHashes=len(set(r['sha256'] for r in report)),items=report),ensure_ascii=False,indent=2),encoding='utf-8')
doc=PACK/'Documentation';doc.mkdir(parents=True,exist_ok=True)
(doc/'catalog.json').write_text(json.dumps(dict(items=items),ensure_ascii=False,indent=2),encoding='utf-8')
cards=[]
for d in items:
    cards.append(f'''<article><img loading="lazy" src="../Textures/{d['category']}/{d['file']}.png"><h2>{d['id']:02d}. {html.escape(d['title'])}</h2><p>{d['category']} · {d['width']:.2f} × {d.get('height',0):.2f} м</p></article>''')
(doc/'PreviewCatalog.html').write_text('''<!doctype html><html lang="ru"><meta charset="utf-8"><title>Граффити · Подмосковье</title><style>body{background:#191b1b;color:#ddd;font:16px system-ui;margin:32px}h1{font-size:30px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:20px}article{background:#292c2b;padding:16px;border-radius:8px}img{width:100%;height:220px;object-fit:contain;background:repeating-conic-gradient(#989b94 0% 25%,#a8aaa3 0% 50%) 0/24px 24px}h2{font-size:18px}p{color:#adb5ae}</style><h1>Граффити · Подмосковье</h1><p>'''+str(len(items))+''' отдельных композиций · размеры в метрах · RGBA 2048</p><main>'''+''.join(cards)+'</main></html>',encoding='utf-8')
for start in range(0,len(items),10):
    sheet=Image.new('RGB',(1500,1250),(142,145,139));draw=ImageDraw.Draw(sheet)
    for n,d in enumerate(items[start:start+10]):
        p=PACK/f"Textures/{d['category']}/{d['file']}.png"
        if not p.exists():continue
        im=Image.open(p);im.thumbnail((710,190),Image.Resampling.LANCZOS)
        x=(n%2)*750+20;y=(n//2)*250+10
        sheet.paste(im,(x+(710-im.width)//2,y+(190-im.height)//2),im)
        draw.text((x,y+194),f"{d['id']:02d} {d['title']}",font=font,fill=(15,20,17))
    sheet.save(out/f'Catalog_{start//10+1}.jpg',quality=94)
print(json.dumps(dict(exported=len(report),unique=len(set(r['sha256'] for r in report))),ensure_ascii=False))
