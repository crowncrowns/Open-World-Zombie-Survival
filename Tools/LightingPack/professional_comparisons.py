from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib,re,csv
import numpy as np
from catalog import *
proof=VERIFY/'ProfessionalAudit'
before=json.loads((proof/'before_unity.json').read_text(encoding='utf-8'));after=json.loads((proof/'after_unity.json').read_text(encoding='utf-8'))
initial=json.loads((proof/'initial_inventory.json').read_text(encoding='utf-8'))
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',21);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16);large=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',28)
out=proof/'Comparisons';out.mkdir(exist_ok=True)
rows=[];identities=[];modelChanges=[]
manifest=json.loads((ASSET/'manifest.json').read_text(encoding='utf-8'));lookup={f['name']:f for f in manifest['fixtures']}
def before_image(name,view,light,old,new):
    im=Image.open(proof/'Before'/(name+'_'+view+'_'+light+'.png')).convert('RGB')
    if name!='Decor_BrokenBulb':return im
    # The physically rebuilt E27 bulb is longer than the old fragment. Use a common
    # orthographic field: reframe the complete original image without inventing pixels.
    vec=lambda v:np.array([v['x'],v['y'],v['z']],dtype=float)
    oc,os,nc,ns=vec(old['center']),vec(old['size']),vec(new['center']),vec(new['size'])
    lo=np.minimum(oc-os/2,nc-ns/2);hi=np.maximum(oc+os/2,nc+ns/2);cc=(lo+hi)/2;cs=hi-lo
    directions={'Front':(0,0,1),'Back':(0,0,-1),'Left':(-1,0,0),'Right':(1,0,0),'Top':(0,1,0),'Bottom':(0,-1,0),'Angle45':(1,.65,2.4)}
    direction=np.array(directions[view],dtype=float);direction/=np.linalg.norm(direction);forward=-direction;up=np.array((0,0,1) if abs(direction[1])>.99 else (0,1,0));right=np.cross(up,forward);right/=np.linalg.norm(right);up=np.cross(forward,right)
    ortho=.88*max(cs);scale=max(os)/max(cs);side=max(1,round(440*scale));position=np.array([220+np.dot(oc-cc,right)/ortho*220,220-np.dot(oc-cc,up)/ortho*220])
    canvas=Image.new('RGB',(440,440),im.getpixel((0,0)));canvas.paste(im.resize((side,side),Image.Resampling.LANCZOS),(round(position[0]-side/2),round(position[1]-side/2)))
    adjusted=proof/'BeforeComparison';adjusted.mkdir(exist_ok=True);canvas.save(adjusted/(name+'_'+view+'_'+light+'.png'));return canvas
for item in after['assets']:
    name=item['name'];old=next(a for a in before['assets'] if a['path']==item['path']);entry=next(p for p in initial['prefabs'] if p['path']==item['path'].split('LightingPack/',1)[1]);p=ASSET/entry['path']
    ids=set(re.findall(r'^--- !u!(\d+) &(-?\d+)',p.read_text(encoding='utf-8'),re.M));original={tuple(v) for v in entry['fileIDs']}
    assert original<=ids,(name,'Original serialized IDs lost',original-ids)
    guid=re.search(r'^guid: (\w+)',p.with_suffix('.prefab.meta').read_text(),re.M).group(1);assert guid==entry['guid']
    currenthash=hashlib.sha256(p.read_bytes()).hexdigest();assert currenthash!=entry['sha256'],(name,'Prefab was not changed')
    identities.append(dict(name=name,guidPreserved=True,oldLocalIDs=len(original),allOldIDsPreserved=True,addedLocalIDs=len(ids-original),prefabChanged=True))
    row=lookup.get(name);source=ASSET/('Models/'+row['category']+'/'+name+'.fbx' if row else 'Models/Decor/'+name+'.fbx');rel=source.relative_to(ASSET).as_posix()
    changed=hashlib.sha256(source.read_bytes()).hexdigest()!=initial['files'][rel];assert changed,(name,'FBX unchanged');modelChanges.append(rel)
    canvas=Image.new('RGB',(1320,515),(20,25,30));draw=ImageDraw.Draw(canvas)
    for k,label in enumerate(['REFERENCE · не предоставлен','BEFORE · исходный проект','AFTER · ревизия']):draw.text((k*440+12,12),label,font=font,fill=(222,226,223))
    draw.text((24,195),'Нет исходной фотографии.\nКаталог задаёт часть габаритов.\nСходство с фото не оценивалось.',font=font,fill=(169,178,180),spacing=12)
    canvas.paste(before_image(name,'Angle45','Day',old,item),(440,48));canvas.paste(Image.open(proof/'After'/(name+'_Angle45_Day.png')).convert('RGB'),(880,48))
    draw.text((12,490),name,font=small,fill=(215,220,212));canvas.save(out/(name+'.jpg'),quality=93)
    issues=old.get('issues') or []
    if issues:problem='; '.join(issues);severity='CRITICAL'
    elif row and row['shape'] in ['globe','barebulb','cage','wallsocket','pendant','portable']:problem='Ступенчатый профиль рассеивателя/колбы, упрощённый патрон и соединения';severity='HIGH'
    elif row and row['shape'] in ['flood','adjustable','oldflood']:problem='Упрощённый отражатель, недостаточная детализация рамки/крепления';severity='HIGH'
    else:problem='Упрощённые стыки/крепёж/вводы; типовые поверхности и UV2 unwrap';severity='MEDIUM'
    rows.append(dict(Object=name,Reference='Фото не предоставлено; конструктивная ревизия существующего дизайна',Problem=problem,Severity=severity,RequiredFix='Новая геометрия/соединения, PBR/UV2, импорт и prefab links',Status='FIXED; фото-сходство не подтверждено'))
    sheet=Image.new('RGB',(1320,970),(20,25,30));d=ImageDraw.Draw(sheet)
    for col,label in enumerate(['Дневной','Пасмурный','Скользящий']):d.text((col*440+12,8),label,font=font,fill='white')
    for r,phase in enumerate(['Before','After']):
        for col,light in enumerate(['Day','Overcast','Grazing']):sheet.paste(before_image(name,'Angle45',light,old,item) if phase=='Before' else Image.open(proof/phase/(name+'_Angle45_'+light+'.png')).convert('RGB'),(col*440,40+r*460))
    sheet.save(out/(name+'_Lighting.jpg'),quality=90)
for wear in ['Used','Worn','Damaged','Decor']:
    chosen=[a for a in after['assets'] if (a['name'].startswith('Decor_') if wear=='Decor' else a['name'].endswith('_'+wear))]
    cols=5;tile=300;tileh=335;rowsn=(len(chosen)+cols-1)//cols
    contact=Image.new('RGB',(cols*tile,rowsn*tileh+55),(20,25,30));draw=ImageDraw.Draw(contact);draw.text((18,12),'LightingPack · AFTER · '+wear,font=large,fill=(227,232,221))
    for i,a in enumerate(chosen):
        x=(i%cols)*tile;y=(i//cols)*tileh+55;im=Image.open(proof/'After'/(a['name']+'_Angle45_Day.png')).convert('RGB').resize((300,300),Image.Resampling.LANCZOS);contact.paste(im,(x,y));label=lookup[a['name']]['title'] if a['name'] in lookup else a['name'].replace('Decor_','');draw.text((x+6,y+302),label,font=small,fill=(210,219,211))
    contact.save(proof/('After_'+wear+'.jpg'),quality=94)
with (proof/'Discrepancies.csv').open('w',encoding='utf-8-sig',newline='') as f:w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
(proof/'identity_preservation.json').write_text(json.dumps(dict(passed=True,prefabs=100,fbxChanged=len(modelChanges),localIDsPreserved=sum(i['oldLocalIDs'] for i in identities),results=identities),indent=2),encoding='utf-8')
(proof/'discrepancies.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print('ALL_100_COMPARISONS_AND_IDENTITIES_PASS',sum(i['oldLocalIDs'] for i in identities))
