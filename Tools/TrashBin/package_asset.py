from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import zipfile,hashlib,json,shutil
R=Path(__file__).resolve().parents[2];V=R/'Verification/TrashBin';stage=R/'work/TrashBin/UnityProject/Assets/Environment/Props/TrashBin';target=R/'Assets/Environment/Props/TrashBin'
report=json.loads((V/'unity_validation.json').read_text(encoding='utf-8-sig'))
assert report['success'] and report['placedCopies']==16
for p in (stage/'Textures').glob('*.png'):assert Image.open(p).size==(4096,4096),p
for f in ['unity_build.log','unity_reopen.log']:
    text=(R/'work/TrashBin'/f).read_text(encoding='utf-8',errors='replace')
    assert ('TRASHBIN_BUILD_PASS' if f=='unity_build.log' else 'TRASHBIN_REOPEN_PASS') in text,f
    assert 'error CS' not in text,f
if (V/'unity_error.txt').exists():(V/'unity_error.txt').unlink()
shutil.copytree(stage,target,dirs_exist_ok=True)
shutil.copy2(stage.with_suffix('.meta'),target.with_suffix('.meta'))
files=[p for p in target.rglob('*') if p.is_file()]+[target.with_suffix('.meta')]
hashes={str(p.relative_to(R)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
for p in files:
    q=stage/ p.relative_to(target) if p!=target.with_suffix('.meta') else stage.with_suffix('.meta')
    assert hashlib.sha256(q.read_bytes()).hexdigest()==hashes[str(p.relative_to(R)).replace('\\','/')]
(V/'installed_sha256.json').write_text(json.dumps({'byteIdenticalToValidatedStage':True,'files':hashes},indent=2))
# Contact sheet uses actual Unity renders, never substitutes a generated concept image.
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25)
sheet=Image.new('RGB',(1800,910),(36,39,42));d=ImageDraw.Draw(sheet)
for row,var in enumerate(['Empty','Filled']):
    for col,suffix in enumerate(['0','1','2','3','Top']):
        im=Image.open(V/f'{var}_{suffix}.png').convert('RGB');im.thumbnail((354,410));x=col*360+(360-im.width)//2;y=row*455
        sheet.paste(im,(x,y));d.text((col*360+12,y+419),f'{var} / {suffix}',font=font,fill=(225,225,220))
sheet.save(V/'Unity_Overview.jpg',quality=94)
hero=Image.new('RGB',(1200,760),(245,245,244));hd=ImageDraw.Draw(hero)
for i,var in enumerate(['Empty','Filled']):
    im=Image.open(V/f'Blender_{var}.png').convert('RGB');im.thumbnail((590,721));hero.paste(im,(i*600+(600-im.width)//2,0));hd.text((i*600+20,727),var+' / Blender source',font=font,fill=(45,45,43))
hero.save(V/'Preview.jpg',quality=95)
lines=['# Проверка уличной урны','',f"Unity: {report['unity']}; URP 17.6.0. Проверки и рендеры сделаны в отдельном проекте.",'','Оба реальных FBX повторно импортированы в чистые сцены Blender. Префабы созданы и затем повторно открыты новым процессом Unity. Подтверждены UV, normals/tangents, ссылки материалов/текстур, pivot на земле, единичный масштаб корня, три LOD, 9 BoxCollider, луч через открытый верх до дна и луч в боковую стенку. Размещено 16 копий. Проверка этих коллайдеров выполнена через Physics.Raycast в редакторе, не игровым персонажем.','', '| Версия | LOD0 трис | LOD1 трис | LOD2 трис |','|---|---:|---:|---:|']
for p in report['prefabs']:lines.append('| '+p['variant']+' | '+' | '.join(str(x) for x in p['triangles'])+' |')
lines+=['','Все исходные PNG — 4096 × 4096. Источники, отдельные PBR-карты и .meta включены в архив. Установленные Assets побайтно совпадают с проверенной копией; см. installed_sha256.json.','', 'Это процедурная реконструкция по одному маленькому фото, без исходных измерений. На рендерах видны упрощения текстур и мусора; качество AAA и точное фотореалистичное совпадение не подтверждены. Проверка вершин внутри урны не равнозначна полному тесту пересечений или доказательству контакта каждого предмета. Общий AO является текстурным приближением. Не проверялись игровая сцена, FPS, освещение после baking, Unreal/HDRP и игровая интеграция. Основные сцены не изменялись.','', 'Unity_Overview.jpg — четыре стороны и верх обеих версий. Отдельные PNG сохранены с исходным разрешением. Полное описание: Docs/TrashBin.md.']
(V/'Report_RU.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
out=R/'outputs/TrashBin_Unity.zip';out.parent.mkdir(exist_ok=True)
extra=[R/'Docs/TrashBin.md']+[p for p in (R/'Sources/TrashBin').iterdir() if p.suffix in ['.blend','.fbx','.png']]+[p for p in (R/'Tools/TrashBin').iterdir() if p.is_file()]+[p for p in V.iterdir() if p.is_file()]
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=5) as z:
    for p in files+extra:z.write(p,str(p.relative_to(R)).replace('\\','/'))
with zipfile.ZipFile(out) as z:assert z.testzip() is None
result={'archive':str(out),'bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'assetFileCount':len(files),'archiveIntegrity':True,'stageMatchesInstallation':True}
(V/'package_validation.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
