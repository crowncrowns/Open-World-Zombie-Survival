"""Compose factual comparison sheets and final delivery documentation.

Original images are only letterboxed, never synthesized or retouched.
Run after target-Unity validation/export; --comparisons can run independently.
"""
from pathlib import Path
import argparse, collections, csv, hashlib, json, re, shutil
from PIL import Image, ImageDraw, ImageFont, ImageOps

R=Path(__file__).resolve().parents[2]
W=R/'work/KursHub/audit7'; F=R/'FINAL_ASSETS'; D=Path('Assets/Environment/KursHub')
T=R/'work/KursHub/UnityProject'
def js(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,s): p.parent.mkdir(parents=True,exist_ok=True);p.write_text(s,encoding='utf8')

REFS=[
 ('01_front','01_Снаружи/01_Главный_фасад_и_вход.png','front','front','Фасад и главный вход'),
 ('02_left','01_Снаружи/02_Левый_боковой_фасад.png','left','left','Левый фасад'),
 ('03_rear','01_Снаружи/03_Задний_двор_и_мастерские.png','rear','rear','Служебный фасад; грузовой вход отложен'),
 ('04_top45','01_Снаружи/04_Общий_вид_сверху.png','top45','top45','Общий силуэт сверху под углом'),
 ('05_market','02_Внутри/01_Общий_зал_и_торговцы.png','market','market','Рынок и общий очаг'),
 ('06_checkpoint','02_Внутри/02_Контрольный_пункт_у_входа.png','checkpoint','checkpoint','КПП; одна мировая камера до/после'),
 ('07_gallery','02_Внутри/03_Второй_этаж_вид_на_холл.png','gallery','gallery','Галерея и ночлеги; два этажа по заданию'),
 ('08_living','02_Внутри/04_Жилые_зоны_и_мастерские.png','gallery','gallery','Жилая зона; перспективы референса и сцены различаются')]

def comparisons():
 out=F/'PREVIEWS/Reference_Comparisons';out.mkdir(parents=True,exist_ok=True)
 font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',28)
 small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',21)
 rows=[]
 for name,ref,before,after,title in REFS:
  paths=[Path('C:/Users/Alex/Documents')/ref,W/'before'/f'{before}.png',W/'after'/f'{after}.png']
  sheet=Image.new('RGB',(3000,800),(25,28,33));draw=ImageDraw.Draw(sheet)
  draw.text((22,14),title,font=font,fill='white')
  for index,(path,label) in enumerate(zip(paths,('REFERENCE','BEFORE / Stage 6','AFTER / Quality 7'))):
   draw.text((index*1000+22,66),label,font=font,fill=(226,216,183))
   with Image.open(path) as source:
    frame=ImageOps.contain(source.convert('RGB'),(980,620),Image.Resampling.LANCZOS)
    sheet.paste(frame,(index*1000+10+(980-frame.width)//2,112+(620-frame.height)//2))
  note='BEFORE и AFTER: одинаковые позиция, цель и FOV камеры. Референс: приблизительный ракурс, без фотокалибровки.'
  draw.text((22,756),note,font=small,fill=(204,208,216))
  target=out/f'{name}.jpg';sheet.save(target,quality=94,subsampling=0)
  rows.append(dict(sheet=target.name,reference=str(paths[0]),before=str(paths[1]),after=str(paths[2]),sameWorldCamera=True))
 # Supplemental subject-aligned checkpoint frame: camera translated with the КПП.
 source=next(x for x in REFS if x[0]=='06_checkpoint')
 paths=[Path('C:/Users/Alex/Documents')/source[1],W/'before/checkpoint.png',W/'after/checkpoint_reference.png']
 sheet=Image.new('RGB',(3000,800),(25,28,33));draw=ImageDraw.Draw(sheet)
 draw.text((22,14),'КПП: сопоставление самого узла после переноса к атриуму',font=font,fill='white')
 for index,(path,label) in enumerate(zip(paths,('REFERENCE','BEFORE','AFTER / камера +13,5 м по Z'))):
  draw.text((index*1000+22,66),label,font=font,fill=(226,216,183))
  with Image.open(path) as im:
   frame=ImageOps.contain(im.convert('RGB'),(980,620),Image.Resampling.LANCZOS)
   sheet.paste(frame,(index*1000+10+(980-frame.width)//2,112+(620-frame.height)//2))
 draw.text((22,756),'В этом дополнительном листе AFTER-камера смещена вместе с КПП. Не выдаётся за неизменную мировую камеру.',font=small,fill=(204,208,216))
 sheet.save(out/'09_checkpoint_subject.jpg',quality=94,subsampling=0)
 rows.append(dict(sheet='09_checkpoint_subject.jpg',reference=str(paths[0]),before=str(paths[1]),after=str(paths[2]),sameWorldCamera=False,afterCameraTranslation=[0,0,13.5]))
 write(out/'comparisons.json',json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
 for p in (W/'before/cameras.tsv',W/'after/cameras.tsv'):
  shutil.copy2(p,out/(p.parent.name+'_cameras.tsv'))
 write(out/'README.md','# Сравнения\n\nВосемь исходных изображений показаны без дорисовки. Первые восемь листов используют фиксированные мировые BEFORE/AFTER камеры; точная камера концепт-референсов неизвестна. Лист 09 дополнительно выравнивает КПП после переноса на 13,5 м к атриуму. Отдельного правого референса нет: right_assumed.png — конструктивная интерпретация. Люди, автомобили и городской фон не добавлены в изображения модели. Численный процент сходства не вычислялся.\n')
 print('REFERENCE_COMPARISONS_OK',len(rows))

def reports():
 package=js(W/'package_import_validation.json')
 assert package['status']=='PASS' and package['packageSha256']==sha(F/'KursHub_Final.unitypackage')
 assert 'KURS_PLAY_OK:' in (W/'play_validation.txt').read_text(encoding='utf8')
 assert 'KURS_AUDIT7_SCENE_CHECK_OK' in (W/'scene_validation.txt').read_text(encoding='utf8')
 props=js(T/D/'Props/KursHubPropsManifest.json')['props']
 native=js(W/'release_sources.json'); manifest=js(T/D/'KursHubManifest.json')
 assert native['status']=='AUDIT7_NATIVE_SOURCES_OK'
 levels=[sum(next((q['triangles'] for q in p['lods'] if q['level']==i),0) for p in props) for i in range(3)]
 old={p['id']:p for p in js(W/'geometry/props_before.json')['props']}
 properties={p['id']:p for p in js(W/'geometry/prop_source_properties.json')['props']}
 pbr=js(W/'reports/PBR_Readback.json')
 # Keep the complete initial discrepancy table, replacing only obsolete statuses.
 audit_path=F/'REPORTS/Audit_Report.md';audit=audit_path.read_text(encoding='utf8')
 audit=re.sub(r'Дата отчётного среза:.*?\n\n',
  'Дата проверки: 20.10.2026. **Геометрия, native prefab, целевой импорт, проход игрока и повторный импорт пакета проверены.** Художественное сходство показано на реальных сравнительных листах; технический PASS не является утверждением о фототождественности или AAA-приёмке.\n\n',audit,count=1)
 audit=audit.replace('53 прежних props плюс новый KH_WeaponsDisplay','53 прежних props плюс новые KH_WeaponsDisplay и KH_AtriumFir')
 audit=audit.replace('и должны оставаться прежними для пары BEFORE/AFTER','; пары BEFORE/AFTER 10 исходных камер сохранены')
 audit=audit.replace('AFTER-виды, дневной/пасмурный/скользящий свет и близкие материалы нужно оценить отдельно после интеграции.','AFTER-виды и отдельные дневной/пасмурный/скользящий ракурсы получены в целевой Unity и просмотрены. Различие камеры референса не маскируется наложением или подменой изображения.')
 audit=audit.replace('Unity-проверка ожидается.','Целевой импорт и нативный аудит Unity пройдены.')
 row_status={
  'Корпус / силуэт':'94 секции с глубиной, 1166 деталей; source/3 FBX/Unity PASS; сравнительные листы 01–04',
  'Оконные секции':'Откосы, прижимы, подоконники/отливы; геометрия/Unity PASS; window_detail.png',
  'Крыша / парапеты / вентиляция':'Примыкания/радиусы/водостоки добавлены; крыша и ограждения проверены в Play',
  'Наружное окружение входа':'144 внешних экземпляра, центральный проход 16 м; вход и подход пройдены',
  'Рынок / расстановка':'564 экземпляра поселения; 31 разворот лавки, 9 стоек оружия, 12 debris, ёлка; проход PASS',
  'КПП / контекст':'Узел перенесён на 13,5 м к атриуму, обе рамки реально пройдены; листы 06/09',
  'Ночлеги / галерея':'Швы/складки/материалы/вещи обновлены; обе галереи и задние проходы пройдены',
  'Керамический пол':'4096², 0,5 м плитки, 8 м период; UV/каналы/Unity PASS; detail_floor/floor_grazing',
  'Ткань, письмо, подвесы':'182 PBR-карты; краска повторяет складки, толщина 0,3 мм, измеренный зазор от 1 мм; 21 FBX PASS'
 }
 lines=[]
 for line in audit.splitlines():
  if line.startswith('| ') and not line.startswith('| Object |'):
   cells=line.split('|')
   if len(cells)==8:
    key=cells[1].strip()
    if key in row_status:cells[6]=' '+row_status[key]+' ';line='|'.join(cells)
    elif key.startswith('KH_'):
     cells[6]=' Source/FBX/Unity PASS; материал/назначение сопоставлены в собранной сцене ';line='|'.join(cells)
    elif 'PENDING' in cells[6] or 'ожида' in cells[6]:
     cells[6]=' Native Unity/UV2/материалы/LOD PASS; bake отдельно не выполнялся ' if ('UV2' in key or 'свет' in key.lower()) else ' Source/FBX/Unity PASS; подробнее Fixes_Report.md '
     line='|'.join(cells)
  lines.append(line)
 audit='\n'.join(lines)+'\n'
 if '| KH_AtriumFir |' not in audit:
  audit+='\n| Object | Reference | Problem | Severity | Required Fix | Status |\n|---|---|---|---|---|---|\n| KH_AtriumFir | I1, I3; верхняя галерея | В прежнем наборе отсутствовал заметный ориентир — ёлка. | MEDIUM | Отдельный объёмный prop с хвоей и гирляндой. | Создан, source/3 FBX/native Unity PASS; галерея проверена |\n'
 audit=audit.replace('Статус SOURCE/FBX PASS означает проверку исходника и обменной геометрии. Он не означает PASS материала в целевом движке или визуальное равенство референсу.','Статус Source/FBX/Unity PASS означает реальные проверки исходника, обменной геометрии и нативных ссылок/материалов/LOD/коллизии в Unity. Он не означает точное воспроизведение каждого мелкого товара по недоступному крупному плану.')
 audit+='\n## Дополнительные исправления и границы\n\nKH_AtriumFir — новая отдельная ёлка 3,35 м с ветвями, замкнутой хвоей, alpha-test атласом, проводом и лампами на верхней задней галерее (I1/I3). KH_PottedPlant перемоделирован: изогнутые листья вместо ромбов. Грани ткани/мешков/ковров исправлены изменением нормалей без подмены силуэта. Для семи тканевых вывесок устранены толстые парящие буквы: краска повторяет реальную складку. Исходные 53 типа и все 5015 исходных mesh-объектов сохранены.\n\nСцена CharacterTest не перезаписывалась: сохранены текущие пользовательские правки света и все 80 фактических внешних ссылок на native объекты prefab. Две прежние ошибки WorldSaveRegistry на ZombieMaleActor(1)/(2) с повторяющимся SaveIdentity остаются отдельно от хаба; проверка реестра мира не объявляется успешной.\n\nМодульный рисунок повторяется на некоторых соседних лавках; мелкий ассортимент и скрытые стороны интерпретированы. Городской фон, транспорт, снег, толпа и тканевая симуляция не входят в этот комплект. Кадры показывают фактические игровые ассеты без дорисовки. Baked lighting, населённый FPS, NPC/NavMesh, безопасная зона и торговая логика не проверялись и не входят в технический PASS этой ревизии.\n'
 write(audit_path,audit)
 rows=[]; machine=[]
 for p in props:
  q=properties.get(p['id'],{}); lod={l['level']:l['triangles'] for l in p['lods']}
  base=old.get(p['id']);a=q.get('beveledObjects',[]);wood=q.get('woodUVObjects',[])
  rows.append('| '+ ' | '.join(map(str,(p['id'],base['triangles'] if base else 'новый',lod[0],lod.get(1,'—'),lod.get(2,'—'),len(a),len(wood),'PASS')))+' |')
  machine.append(dict(id=p['id'],beforeTriangles=base['triangles'] if base else None,lods=lod,boundsMin=p['boundsMin'],boundsMax=p['boundsMax'],beveledSourceObjects=len(a),woodUVSourceObjects=len(wood),sourceFBXUnity='PASS'))
 head=f'''# Фактические исправления — ТЦ «Курс», ревизия 7

Проверено в Unity {package['unityVersion']}, URP {package['urpVersion']}. Нативный package повторно импортирован API Unity в отдельный проект: **{package['prefabCount']} prefab, {package['guidChecks']} GUID-проверок, {package['independentCollisionChecks']} самостоятельных проверок коллизии**. Общий префаб содержит {package['nestedPropInstances']} вложенных экземпляров.

## Архитектура и расстановка

Все 3480 прежних mesh корпуса сохранены; добавлено 1166 деталей, всего 4646 редактируемых mesh. В 94 секциях появились настоящие откосы, прижимы/переплёты, отливы, капельники. Добавлены примыкания парапета, водосточные крепления/выпуски, дверные ручки. Фаски с физическим радиусом применены к 1211 выбранным частям; ходовые плоскости не искажены. Исправлен 41 открытый текстовый mesh: сварка 0,01 мм и восстановление 37 вырожденных cap-треугольников. Строгий повторный source/FBX импорт проходит без legacy-исключений.

Корпус LOD0/1/2: **350372 / 198244 / 108188 треугольников**, chunks 389/389/299. Старые 299 экспортных имён сохранены. Контур стен 93×130 м, два этажа, атриум, закругление и отсутствие удалённого козырька сохранены.

Наружных предметов 144 вместо 63, центральный проход 16 м. Поселение — 564 вместо 542 экземпляров: добавлены 9 выкладок оружия, 12 групп мусора и ёлка. 31 лавка получила небольшие различия положения/разворота. КПП сдвинут к атриуму на 13,5 м. Сохранены все 735 исходных ID экземпляров. Собственных источников света поселения 55; узкая миграция снижает оранжевый оттенок только у распознанных generated-значений, пользовательские overrides сохраняются.

## Все отдельные предметы

55 типов вместо 53. Все 1535 исходных solids сохранены. В нативной библиотеке теперь 2848 mesh и {levels[0]} треугольников. 639 физических фасок и 416 продольных деревянных UV проверены по source properties до дополнительных растительных объектов. Добавлены крепления столов/лавок/ящиков, болты/гвозди/пластины, элементы детектора и прожектора. Новая оружейная выкладка статическая, без игровой оружейной логики. Новая ёлка 3,35 м имеет смоделированные ветки/хвою, вырезной атлас, провод и лампы. У растения изогнутые объёмные листья с отдельным материалом жилок.

Сумма уникальных props LOD0/1/2: **{' / '.join(map(str,levels))}**. Проверены все 163 FBX предметов и 3 корпуса. Для KH_GrimePatch (12 tris) LOD не нужен. Небезопасные reduced части возвращены к предыдущему проверенному уровню; общая сложность каждого prop всё равно строго снижается. Тонкая краска повторяет поверхность складок: толщина 0,3 мм; зазор номинально 1 мм с измеренной компенсацией до 17 мм; на дистанционных glyph с несимметричной редукцией сохранена безопасная пара «ткань + краска». Остальные UV/имена/идентификаторы и support chunks сохранены.

| Prop | Before tris | LOD0 | LOD1 | LOD2 | Фаски (source) | Wood UV (source) | Source/FBX/Unity |
|---|---|---|---|---|---|---|---|
'''
 tail=f'''

## Материалы и свет

26 семейств ×7 карт =182 PBR PNG ({pbr['pngBytes']} байт исходных карт), плюс прежняя прозрачная DirtStreaks_BaseColor. CeramicTile 4K: 16×16 плиток по 0,5 м в периоде 8×8 м; 83 красно-коричневые и 173 светлые. Остальные поверхности 1K/2K. Продольная древесная UV, мелкий тканый рельеф вместо крупной печатной клетки, изменяемая roughness, корректные металлические маски и грязь стекла. Проверены реальные изображения при дневном, пасмурном и скользящем свете и близкие планы пола/окна/лавки.

Карты BaseColor(sRGB), tangent+Y Normal, Roughness, Metallic, Occlusion, Height16 и MetallicSmoothness(R=metallic,A=1−roughness). Численные каналы прочитаны обратно; diffuse цвет не содержит запечённого направленного света. AO процедурный локальный; это не bake всей геометрии. Height сохранён для обмена, runtime parallax/displacement не включён. FirNeedles использует alpha-test 0,4, покрытие mipmaps и depth write. Все поверхности авторские процедурные, не фотосканы.

## Unity, коллизии и проверенный маршрут

Настоящие prefabs созданы через Unity Editor API. UV2 генерируется импортёром с Manual secondary UV margin=1: автоматический Calculate игнорировал заданное поле и давал пустой frame mesh; этот дефект исправлен и повторно проверен. Визуальные LOD независимы от постоянных коллайдеров; корневой LODGroup здания управляет только Shell. Для 37 целиком прямоугольных chunks использованы 143 доказанных OBB; остальные точные коллизии сохранены. Исходные отключённые MC сохраняют fileID.

Действующий FirstPersonController с Input System прошёл подход, вход в обе стороны, обе рамки КПП, торговые проходы, очаг, обе главные лестницы, ближние к перилам галереи и задние переулки, U-лестницу и крышу с куполом. Выполнены push-проверки стен/стекла/краёв и отдельные проходы reusable перегородок. Две последовательные native сборки сохранили прежние IDs; все 80 внешних native ссылок текущей CharacterTest проверены через AssetDatabase, включая виртуальные вложенные prefab IDs. Сцена пользователя не перезаписана.

Повторный импорт финального package в изолированный проект проверяет зависимости, модели, материалы, карты, LOD, масштаб, коллизии и demo сцену. Отсутствующих Mesh/Material/Shader/Texture в проверенной поставке нет. Подробности и хеши: package_import_validation.json, play_validation.txt, scene_validation.txt, release_sources.json, architecture_roundtrip.json, fbx_roundtrip.json.

## Практические пределы

Сравнительные листы показывают текущий визуальный результат; технические проверки не присваивают ему AAA-оценку. Стороны/товары без точных референсов интерпретированы, часть модульного ассортимента повторяется. Городской фон, транспорт и люди отсутствуют. Финальный bake, NPC/NavMesh, населённый FPS, торговля и safezone не проверялись. Две прежние ошибки общего WorldSaveRegistry на существующих зомби остаются отдельно и не были скрыты или обойдены. Резервная копия исходного состояния сохранена в work/KursHub/audit7/baseline.
'''
 write(F/'REPORTS/Fixes_Report.md',head+'\n'.join(rows)+tail)
 write(F/'REPORTS/Per_Prop_Fixes.json',json.dumps(machine,ensure_ascii=False,indent=2)+'\n')
 # The initial inventory is historical evidence; its final column now links to the real completed per-type audit.
 inv=F/'REPORTS/Initial_Object_Inventory.tsv'
 if inv.exists():
  text=inv.read_text(encoding='utf8').replace('Preserved; staged fixes and roundtrip verified; target/visual pending','Preserved; source/FBX/Unity verified; final per-type state: Per_Prop_Fixes.json')
  write(inv,text)
 print('FINAL_REPORTS_OK',len(props),levels)

def manifest():
 pkg=js(W/'package_import_validation.json'); assert pkg['status']=='PASS'
 write(F/'README.md','''# ТЦ «Курс» — комплект игровых ассетов

Цель: Unity 6000.6.3f1 / URP 17.6.0. Основной переносимый файл — **KursHub_Final.unitypackage**. Он создан и проверен нативным Unity Editor API, включает 56 настоящих prefab: целое здание и 55 отдельных предметов.

Импортируйте package в URP-проект, затем откройте `Assets/Environment/KursHub/Scenes/KursHubDemo.unity` или перетащите `Assets/Environment/KursHub/KursHub.prefab` в сцену. Один Unity unit = 1 метр, Scale=1, вход локально −Z. Оболочка 93×130 м; два этажа 0/5,5 м; крыша 11 м. Для игры используется текущий CharacterTest исходного проекта; демонстрационная сцена содержит камеру/пол/свет и не добавляет контроллер или NPC.

BLENDER содержит редактируемые Buildings_Final.blend и Props_Final.blend с упакованными текстурами; исходная каноническая копия лежит Sources/KursHub вне Unity Assets. FBX содержит настоящие отдельные экспорты и LOD. TEXTURES — обменные PBR-карты, UNITY — настроенные loose ассеты с сохранёнными GUID. Выберите один способ переноса: package либо UNITY; одновременный импорт обеих копий создаст одинаковые GUID. FBX/.meta дублируются в обменной и UNITY-папках намеренно.

Откройте PREVIEWS/Reference_Comparisons для REFERENCE | BEFORE | AFTER и REPORTS для аудита, исправлений, маршрута и проверок. Правая сторона без отдельного референса помечена как интерпретация. Изображения модели получены в Unity без дорисовки; люди и городской фон не включены.

LOD и коллизии настроены; технический импорт и реальный проход игрока проверены. Финальный bake и FPS с населением не измерены; NPC, торговля и safezone не реализуются этим ассетом. Часть ассортимента модульно повторяется, материалы процедурные. Генераторы не запускать поверх ручных доработок без отдельного staging и сохранения GUID/источников.
''')
 files=[]
 for p in sorted(F.rglob('*')):
  if p.is_file() and p.name not in ('SHA256SUMS.txt','Asset_Manifest.md','Asset_Manifest.json'):
   files.append(dict(path=p.relative_to(F).as_posix(),bytes=p.stat().st_size,sha256=sha(p)))
 write(F/'REPORTS/Asset_Manifest.json',json.dumps(dict(packageSha256=sha(F/'KursHub_Final.unitypackage'),files=files),ensure_ascii=False,indent=2)+'\n')
 write(F/'REPORTS/SHA256SUMS.txt',''.join(f"{p['sha256']}  {p['path']}\n" for p in files))
 totals=collections.Counter()
 for p in files:totals[p['path'].split('/')[0]]+=p['bytes']
 lines='\n'.join(f'| {key} | {count:,} |' for key,count in sorted(totals.items()))
 write(F/'REPORTS/Asset_Manifest.md',f'''# Состав и проверка поставки

Unity {pkg['unityVersion']} / URP {pkg['urpVersion']}. Package: {pkg['archiveEntries']} архивных записей, {pkg['prefabCount']} prefab, {pkg['materialCount']} материалов, {pkg['nestedPropInstances']} вложенных экземпляров; импорт и native GUID-проверки PASS.

SHA256 `KursHub_Final.unitypackage`: `{sha(F/'KursHub_Final.unitypackage')}`.

| Папка / файл верхнего уровня | Байт (включая дубли обменных копий) |
|---|---|
{lines}

Полные имена, размеры и SHA256 находятся в Asset_Manifest.json и SHA256SUMS.txt. Сам manifest и checksum index исключены из собственного набора хешей. Все 166 FBX прошли обратный импорт; 182 PBR-карты и дополнительная alpha-декаль сохранены отдельно; 56 native prefab созданы Unity API. В BLENDER два упакованных исходника. Внешние зависимости package ограничены URP/ShaderGraph и встроенными ресурсами Unity. Brick содержит пояснение: выраженная кирпичная кладка в этих референсах отсутствует, она не выдумывалась.

Canonical пути проекта и GUID сохранены внутри package. Loose UNITY и FBX имеют альтернативную раскладку; GUID-ссылки переносятся с .meta. Не импортируйте одновременно дублирующие варианты в один Assets.
''')
 print('DELIVERY_MANIFEST_OK',len(files),'files',sum(p['bytes'] for p in files),'bytes')

def verification():
 out=R/'Verification/KursHub/QualityAudit7';out.mkdir(parents=True,exist_ok=True)
 for p in (F/'REPORTS').glob('*'):
  if p.is_file() and p.suffix in ('.json','.txt','.md','.tsv'):shutil.copy2(p,out/p.name)
 for p in (W/'after').glob('*.png'):
  destination=out/'views'/p.name;destination.parent.mkdir(exist_ok=True);shutil.copy2(p,destination)
 for p in (W/'package_import_previews').glob('*.png'):
  destination=out/'package_import_views'/p.name;destination.parent.mkdir(exist_ok=True);shutil.copy2(p,destination)
 for relative in ('Assets/Editor/KursHubVerification.cs','Assets/KursHubProbe.cs','Assets/Editor/KursHubAuditAfter7.cs'):
  p=T/relative;destination=out/'verification_sources'/(p.name+'.txt');destination.parent.mkdir(exist_ok=True);shutil.copy2(p,destination)
 p=W/'PackageImportProject/Assets/Editor/KursHubPackageImportCheck.cs'
 shutil.copy2(p,out/'verification_sources/KursHubPackageImportCheck.cs.txt')
 shutil.copy2(W/'prepare_package_import.py',out/'verification_sources/prepare_package_import.py.txt')
 for name in ('before','after'):
  shutil.copy2(W/name/'cameras.tsv',out/(name+'_cameras.tsv'))
 write(out/'README.md','# Проверки ТЦ «Курс», ревизия 7\n\nЦелевой движок Unity 6000.6.3f1 / URP17.6.0, изолированный проект work/KursHub/UnityProject. Реальный контроллер и Input System: play_validation.txt. Сохранность текущей сцены/ID: scene_validation.txt и deployment.json. Чистый первоначальный импорт archive и повторный выпуск после визуальной коррекции: package_import_validation.json, native GUID/material/LOD/collider checks и изображения package_import_views. Ни один временный probe не перенесён в продукт. Тексты проверок сохранены как .txt и не являются runtime-компонентами. Их пути/настройки предназначены для этого checkout; не импортировать в основную Assets.\n\nТехнический PASS не объявляет фотографическую или AAA-приёмку. Реальные сравнительные листы и исходные render views находятся в FINAL_ASSETS/PREVIEWS. Городской фон/NPC/финальный bake/населённый FPS отсутствуют. Общий WorldSaveRegistry имеет прежние ошибки зомби; сцена пользователя не переписана.\n')
 print('VERIFICATION_PUBLISHED',out)

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--comparisons',action='store_true');parser.add_argument('--reports',action='store_true');parser.add_argument('--manifest',action='store_true');parser.add_argument('--verification',action='store_true');args=parser.parse_args()
 if args.comparisons:comparisons()
 if args.reports:reports()
 if args.manifest:manifest()
 if args.verification:verification()
