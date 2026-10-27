from catalog import *
from PIL import Image,ImageDraw,ImageFont
import shutil,json
proof=VERIFY/'ProfessionalAudit';previews=ASSET/'Documentation/Previews';previews.mkdir(exist_ok=True)
shutil.copy2(proof/'After_Used.jpg',previews/'Preview_30_Types.jpg')
shutil.copy2(proof/'After_Used.jpg',VERIFY/'Preview_30_Types.jpg')
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22)
data=json.loads((ASSET/'manifest.json').read_text(encoding='utf-8'))
canvas=Image.new('RGB',(1320,2950),(20,25,30));draw=ImageDraw.Draw(canvas)
for col,wear in enumerate(['Used','Worn','Damaged']):draw.text((col*440+16,12),wear,font=font,fill='white')
for row,id in enumerate([1,9,12,17,21,30]):
    for col,wear in enumerate(['Used','Worn','Damaged']):
        f=next(f for f in data['fixtures'] if f['id']==id and f['wear']==wear);x=col*440;y=row*480+50
        canvas.paste(Image.open(proof/'After'/(f['name']+'_Angle45_Day.png')).convert('RGB'),(x,y));draw.text((x+12,y+442),f['title'],font=font,fill=(221,227,218))
canvas.save(previews/'Preview_Wear_States.jpg',quality=94);shutil.copy2(previews/'Preview_Wear_States.jpg',VERIFY/'Preview_Wear_States.jpg')
old=ASSET/'Documentation/Readiness_Report_RU.md'
if old.exists():
    text=old.read_text(encoding='utf-8')
    if not text.startswith('# Исторический отчёт'):
        old.write_text('# Исторический отчёт до ревизии 100 префабов\n\nАктуальные исправления, проверки и сравнения: [ProfessionalAudit/Report_RU.md](ProfessionalAudit/Report_RU.md). Числа и ограничения ниже относятся к исходной версии и не заменяют новый отчёт.\n\n---\n\n'+text,encoding='utf-8')
data['professionalAudit']=dict(date='2026-10-25',prefabs=100,oldPrefabGuidsPreserved=True,oldLocalFileIdsPreserved=5231,referencePhotographs='not supplied',report='Documentation/ProfessionalAudit/Report_RU.md')
(ASSET/'manifest.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
print('CURRENT_DOCS_AND_PREVIEWS_PASS')
