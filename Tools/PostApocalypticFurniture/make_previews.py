from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
from catalog import VERIFY,ROWS,GROUPS
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',20)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
out=VERIFY/'Catalog';out.mkdir(exist_ok=True)
for category,(name,ru,_) in enumerate(GROUPS):
    page=Image.new('RGB',(1600,780),(29,32,34));draw=ImageDraw.Draw(page)
    draw.text((20,10),f'{category+1:02d} · {ru} · Unity LOD0',font=font,fill=(231,222,199))
    for k in range(10):
        row=ROWS[category*10+k];p=VERIFY/'UnityPreviews'/f'{row["id"]:03d}.png'
        thumb=Image.open(p).convert('RGB').resize((312,312),Image.Resampling.LANCZOS)
        x=(k%5)*320+4;y=(k//5)*365+42;page.paste(thumb,(x,y))
        title=f'{row["id"]:03d} · {row["name"]}'
        words=title.split();lines=['']
        for w in words:
            if draw.textlength(lines[-1]+' '+w,font=small)>303:lines.append(w)
            else:lines[-1]+=(' ' if lines[-1] else '')+w
        draw.multiline_text((x+4,y+316),'\n'.join(lines[:2]),font=small,fill=(224,216,196),spacing=3)
    page.save(out/f'Catalog_{category+1:02d}_{name}.jpg',quality=92)
ids=[1,13,25,31,49,57,69,73,84,95,110,118,126,131,148,154,163,171,188,194]
sheet=Image.new('RGB',(1600,1440),(29,32,34));draw=ImageDraw.Draw(sheet)
for i,aid in enumerate(ids):
    row=ROWS[aid-1];p=VERIFY/'UnityPreviews'/f'{aid:03d}.png';x=(i%5)*320;y=(i//5)*360
    sheet.paste(Image.open(p).convert('RGB').resize((316,316),Image.Resampling.LANCZOS),(x+2,y))
    draw.text((x+8,y+321),f'{aid:03d} · {row["categoryRu"]}',font=small,fill=(230,221,202))
sheet.save(VERIFY/'Preview_20_Types.jpg',quality=93)
print('NATIVE_CATALOG_SHEETS_PASS 20')
