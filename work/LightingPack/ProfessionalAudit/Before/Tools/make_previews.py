from PIL import Image,ImageDraw,ImageFont
from catalog import *
import json
data=json.loads((ASSET/'manifest.json').read_text(encoding='utf-8'))
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
big=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',30)
used=[f for f in data['fixtures'] if f['wear']=='Used']
sheet=Image.new('RGB',(2160,2060),(14,18,23));draw=ImageDraw.Draw(sheet);draw.text((24,18),'LIGHTING PACK · 30 конструкций · Unity URP · Used / свет выключен',font=big,fill=(225,230,223))
for k,f in enumerate(used):
    x=(k%6)*360;y=(k//6)*400+60
    im=Image.open(VERIFY/'Gallery'/(f['name']+'.png')).convert('RGB');im=im.resize((350,350),Image.Resampling.LANCZOS);sheet.paste(im,(x+5,y))
    draw.text((x+10,y+352),f"{f['id']:02d} · {f['title']}",font=small,fill=(220,224,213))
sheet.save(VERIFY/'Preview_30_Types.jpg',quality=93)
ids=[1,9,12,17,21,30]
sheet=Image.new('RGB',(1560,3370),(14,18,23));draw=ImageDraw.Draw(sheet)
for j,wear in enumerate(['Used','Worn','Damaged']):draw.text((j*520+25,14),wear,font=big,fill=(229,225,206))
for row,id in enumerate(ids):
    for col,wear in enumerate(['Used','Worn','Damaged']):
        f=next(f for f in data['fixtures'] if f['id']==id and f['wear']==wear)
        im=Image.open(VERIFY/'Gallery'/(f['name']+'.png')).convert('RGB').resize((510,510),Image.Resampling.LANCZOS)
        x=col*520+5;y=row*550+60;sheet.paste(im,(x,y));draw.text((x+10,y+512),f['title'],font=font,fill=(220,224,213))
sheet.save(VERIFY/'Preview_Wear_States.jpg',quality=93)
sheet=Image.new('RGB',(1500,450),(14,18,23));draw=ImageDraw.Draw(sheet)
for i,(name,label) in enumerate([('Off','Выключен'),('Weak','Тусклый'),('Normal','Нормальный')]):
    im=Image.open(VERIFY/('Unity_Lighting_'+name+'.png')).convert('RGB').resize((500,400),Image.Resampling.LANCZOS);sheet.paste(im,(i*500,40));draw.text((i*500+20,8),label,font=font,fill=(229,225,206))
sheet.save(VERIFY/'Preview_Lighting_Comparison.jpg',quality=94)
frames=sorted((VERIFY/'FailingFrames').glob('*.png'))
if frames:
    images=[Image.open(p).convert('RGB').resize((750,600),Image.Resampling.LANCZOS) for p in frames]
    images[0].save(VERIFY/'Unity_Failing.gif',save_all=True,append_images=images[1:],duration=120,loop=0)
print('PREVIEWS_PASS')
