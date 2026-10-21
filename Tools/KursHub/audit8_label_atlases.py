"""Original Cyrillic equipment/personal labels, registered shared PBR atlases."""
from pathlib import Path
import json, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

R=Path(__file__).resolve().parents[2]; W=R/'work/KursHub/audit8'
A=W/'authoring'; T=A/'Assets/Environment/KursHub/Textures'
entries=json.loads((W/'label_atlas_entries.json').read_text(encoding='utf8'))
rng=np.random.default_rng(80917)
families=[]
for page in range(1+max(e['index'] for e in entries)//32):
    family='Hub8Labels'+chr(65+page)
    pixels=np.zeros((2048,2048,4),np.uint8)
    grain=rng.normal(0,1.25,(2048,2048))
    for c,value in enumerate((208,200,178)):pixels[:,:,c]=np.clip(value+grain,0,255)
    pixels[:,:,3]=255
    atlas=Image.fromarray(pixels,'RGBA')
    for e in [e for e in entries if e['index']//32==page]:
        slot=e['index']%32; x,y=(slot%4)*512,(slot//4)*256
        lines=e['text'].split('\n'); n=max(len(s) for s in lines)
        # The sticker geometry uses text-derived physical aspect; create that
        # aspect before fitting the fixed atlas slot to prevent fat letters.
        cw=max(64,round(n*.55*96));ch=round(len(lines)*1.4*96)
        tile=Image.new('RGBA',(cw,ch),(224,217,193,255));d=ImageDraw.Draw(tile)
        font=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',80)
        for row,line in enumerate(lines):
            box=d.textbbox((0,0),line,font=font)
            width=box[2]-box[0]
            if width>cw*.94:
                font=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',max(12,round(80*cw*.94/width)))
            d.text((cw*.025, row*134+15),line,font=font,fill=(36,36,33,255),stroke_width=0)
        tile=tile.resize((496,240),Image.Resampling.LANCZOS)
        td=ImageDraw.Draw(tile)
        # Small edge abrasions; text remains readable, with no painted shadows.
        for _ in range(42):
            ex=int(rng.integers(0,496));ey=int(rng.choice([1,2,237,238]))
            td.line((ex,ey,ex+int(rng.integers(1,9)),ey),fill=(178,168,141,255),width=1)
        atlas.alpha_composite(tile,(x+8,y+8))
    atlas.save(T/(family+'_BaseColor.png'))
    for suffix,value in [('Roughness',205),('Metallic',0),('Occlusion',255)]:
        Image.new('L',(2048,2048),value).save(T/(family+'_'+suffix+'.png'))
    Image.new('RGB',(2048,2048),(128,128,255)).save(T/(family+'_Normal.png'))
    Image.new('RGBA',(2048,2048),(0,0,0,50)).save(T/(family+'_MetallicSmoothness.png'))
    Image.new('I;16',(2048,2048),32768).save(T/(family+'_Height.png'))
    families.append(dict(name=family,resolution=2048,periodMetres=1,periodYMetres=1,tiling=1,normalStrength=1,heightMinMetres=0,heightMaxMetres=0,alpha=False,heightUsage='exchange only; printed label, no relief/displacement',uniqueUV=True))
manifest_path=T/'Audit7PBRManifest.json'
manifest=json.loads(manifest_path.read_text(encoding='utf8'))
newnames={f['name'] for f in families}
manifest['families']=[f for f in manifest['families'] if f['name'] not in newnames]+families
manifest['revision8Labels']=dict(entries=len(entries),families=sorted(newnames),provenance='Original label typography and paper wear; Arial Bold; no photographs or downloaded graphics')
manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
for relative in ('KursHubManifest.json','Props/KursHubPropsManifest.json'):
    path=A/'Assets/Environment/KursHub'/relative
    obj=json.loads(path.read_text(encoding='utf8'))
    known={p['name'] for p in obj['palette']}
    obj['palette'].extend(dict(name=f['name'],rgba=[1,1,1,1]) for f in families if f['name'] not in known)
    path.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf8')
(W/'label_atlas_report.json').write_text(json.dumps(dict(status='ORIGINAL_PRINT_ATLASES_GENERATED',labels=entries,families=families),ensure_ascii=False,indent=2),encoding='utf8')
print('AUDIT8_LABEL_ATLASES_OK',len(entries),'labels;',len(families),'2K families')
