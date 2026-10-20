"""Evidence-bound report drafts; integrated target checks remain explicitly pending."""
import collections
import csv
import json
import pathlib
import re
import shutil

ROOT = pathlib.Path(r'E:/git/Open-World-Zombie-Survival')
WORK = ROOT/'work/KursHub/audit7'
OUT = ROOT/'FINAL_ASSETS/REPORTS'
OUT.mkdir(parents=True,exist_ok=True)
def read(path): return json.loads((WORK/path).read_text(encoding='utf-8-sig'))
before=read('geometry/props_before.json')['props']
after=read('geometry/props_after.json')['props']
props0={p['id']:p for p in before}
props1={p['id']:p for p in after}
source={p['id']:p for p in read('geometry/prop_source_properties.json')['props']}
lods={p['id']:{l['level']:l['triangles'] for l in p['levels']} for p in read('geometry/lods.json')}
shell0=read('architecture_before.json')
shellfix=read('architecture_fixes.json')
shellfbx=read('architecture_roundtrip.json')
bevelnames={o['object'] for o in shellfix['bevels']}
pbr=read('reports/PBR_After_Manifest.json')
pbrcheck=read('reports/PBR_Readback.json')

refs={
 'I1':('02_Внутри/01_Общий_зал_и_торговцы.png','Густые лавки из досок и труб, товар, тёплые кабельные лампы, грязная светлая/красная керамика, синий центральный баннер.'),
 'I2':('02_Внутри/02_Контрольный_пункт_у_входа.png','Две рамки досмотра, сетчатые ограждения, мешки и ящики, отдельный стол; чёрный тканевый заголовок и правила.'),
 'I3':('02_Внутри/03_Второй_этаж_вид_на_холл.png','Плотный ночлег около перил, ткани на ограждениях, центральный атриум и стеклянная крыша.'),
 'I4':('02_Внутри/04_Жилые_зоны_и_мастерские.png','Обжитые смешанные жилые/ремонтные зоны, развешенные ткани и бельё, ковры, вещи, общий очаг.'),
 'E1':('01_Снаружи/01_Главный_фасад_и_вход.png','Закруглённый двухъярусный витраж, синие крылья, кремовая терраса слева, фронтальный купол и высокая вывеска.'),
 'E2':('01_Снаружи/02_Левый_боковой_фасад.png','Ступенчатые закрытые синие объёмы, отдельные оконные секции, панели, терраса и крышные укрепления.'),
 'E3':('01_Снаружи/03_Задний_двор_и_мастерские.png','Служебный фасад, коммуникации и вентиляция, синие/кремовые панели, мастерские и ограждения.'),
 'E4':('01_Снаружи/04_Общий_вид_сверху.png','Общий силуэт, фронтальный купол, вывеска на раме, техническая крыша, посты, наружный обжитой периметр.')}

proprefs={
 'KH_Sandbag':'E1, E2, E3, E4, I2','KH_Barricade':'E1, E2, E4','KH_WatchPost':'E1, E2, E3, E4',
 'KH_TarpWide':'E1, E2, E3, E4','KH_TarpNarrow':'E1, E2, E3, E4','KH_WindowBoards':'E1, E2, E3',
 'KH_CorrugatedPatch':'E1, E2, E3','KH_CableSag':'E1, E2, I1, I4','KH_Floodlight':'E1, E2, E3, E4',
 'KH_GrimePatch':'E1, E2, E3, I1, I2','KH_Railing':'I1, I3, I4, E1',
 'KH_Partition4':'I3, I4 (интерпретация перегородок)','KH_Shopfront6':'I1, I4 (интерпретация модуля)',
 'KH_DoorPartition4':'I3, I4 (интерпретация проёма)','KH_MeshPartition4':'I2, I4',
 'KH_DirectionBoard':'I1, I2, I3, I4','KH_MedicalSign':'I1, I2, I4',
 'KH_StallTimber':'I1, I4','KH_StallPipe':'I1, I4','KH_StallCounter':'I1, I2, I4',
 'KH_GoodsFood':'I1, I4 (тип товара)','KH_GoodsTools':'I1, I4 (тип товара)',
 'KH_GoodsGear':'I1, I3, I4 (тип товара)','KH_PalletCrates':'I1, I2, I3, E3',
 'KH_SupplySackStack':'I1, I2, I4','KH_LowDisplay':'I1, I3, I4 (тип раскладки)',
 'KH_WorkbenchStocked':'I4, E3','KH_Stool':'I1, I4','KH_Bench':'I1, I4',
 'KH_BunkMakeshift':'I3, I4','KH_Bedroll':'I3, I4','KH_LivingScreen':'I3, I4',
 'KH_RugRed':'I1, I3, I4','KH_RugBlue':'I1, I3, I4','KH_RugWoven':'I1, I3, I4',
 'KH_RailDrape':'I1, I3, I4','KH_RailDrapeBlue':'I1, I3, I4',
 'KH_RestTable':'I1, I4','KH_FireBarrel':'I1, I4','KH_CampChair':'I1, I4 (тип места отдыха)',
 'KH_ResidentBundle':'I3, I4 (тип вещей)','KH_PottedPlant':'I1, I3, I4 (тип растения)',
 'KH_FloorDebris':'I1, I3, I4 (тип мусора)','KH_KursAtriumBanner':'I1, I2, I3, I4',
 'KH_SignWeapons':'I1','KH_SignMedicalTrade':'I1, I3','KH_SignGear':'I1, I3','KH_SignRepair':'I4',
 'KH_CheckpointHeader':'I2','KH_CheckpointRules':'I2','KH_MetalDetector':'I2','KH_SecurityFence':'I2, E3',
 'KH_StringLights':'I1, I2, I3, I4','KH_WeaponsDisplay':'I1 (оружейная лавка; типы силуэтов интерпретированы)'}
assert set(proprefs)==set(props1)

def clean(value): return str(value).replace('|','/').replace('\n',' ')
def mdtable(headers,rows):
 return '| '+' | '.join(headers)+' |\n|'+ '|'.join('---' for _ in headers)+'|\n'+''.join('| '+' | '.join(clean(x) for x in row)+' |\n' for row in rows)

inventory=[]
prop_rows=[]
fix_rows=[]
counts=[]
for id,p in props1.items():
 old=props0.get(id)
 own=source[id]
 bevel=len(own['beveledObjects']);wood=len(own['woodUVObjects'])
 added=sum(bool(o['added']) for o in p['objects'])
 cloth=any(any(k in m for k in ('Canvas','Tarp','SandCanvas')) for o in p['objects'] for m in o['materialSlots'])
 corrected=[o['correction'] for o in p['objects'] if o['correction']]
 if old is None:
  problem='Под оружейной вывеской отсутствовала самостоятельная предметная выкладка оружия.';severity='HIGH';required='Добавить узнаваемые объёмные силуэты, стойку и крепления; сохранить существующие прилавки.'
 elif id=='KH_GrimePatch':
  problem='Малый плоский alpha-слой: проверить наложение и сортировку; геометрический дефект не обнаружен.';severity='LOW';required='Сохранить прозрачный декор; не выдавать его за объём/PBR всей поверхности.'
 elif cloth and bevel==0:
  problem='Ткань в BEFORE имеет слишком крупную контрастную сетку; исходная геометрия закрыта.';severity='HIGH';required='Мелкое тканое normal/roughness вместо печатной клетки; проверить складки, письмо и прозрачность в сцене.'
 elif bevel:
  problem='Жёсткие конструкционные кромки без физической фаски'+('; направление древесной UV не связано с длиной части.' if wood else '.')
  severity='MEDIUM';required='Фаски и нормали крупных граней'+('; UV по продольной оси древесины.' if wood else '.')
 else:
  problem='Исходная геометрия закрыта; крупные планы материалов и дистанционное упрощение не были подтверждены.';severity='MEDIUM';required='Сохранить полезный силуэт, проверить поверхность и добавить безопасные дистанционные LOD.'
 if any('Fabric repair' in x for x in corrected):required+=' Пришить ремонтные заплаты к реальным складкам, убрать плоское пересечение.'
 status='SOURCE/FBX PASS; целевой Unity и AFTER-оценка ожидаются'
 prop_rows.append((id,proprefs[id]+'; размеры/скрытые стороны интерпретированы',problem,severity,required,status))
 fix_rows.append((id,old['triangles'] if old else 'новый',lods[id][0],lods[id].get(1,'—'),lods[id].get(2,'—'),bevel,wood,added,'SOURCE/FBX PASS; Unity PENDING'))
 counts.append(dict(id=id,beforeTriangles=old['triangles'] if old else 0,**{'lod'+str(k):v for k,v in lods[id].items()},beveledObjects=bevel,grainOrientedUVObjects=wood,addedObjects=added,reference=proprefs[id]))
 for o in (old or {'objects':[]})['objects']:
  issues=[]
  if o['nonManifoldEdges']:issues.append('nonmanifold')
  if o['degenerateFaces']:issues.append('degenerate')
  if not o['uvLayers']:issues.append('missing UV')
  if o['name'] in own['beveledObjects']:issues.append('hard construction edge lacked physical bevel')
  if o['name'] in own['woodUVObjects']:issues.append('wood grain axis not authored')
  inventory.append(dict(scope=id,name=o['name'],type='MESH',reference=proprefs[id],materials=','.join(o['materialSlots']),triangles=o['triangles'],uv=','.join(o['uvLayers']),nonmanifold=o['nonManifoldEdges'],degenerate=o['degenerateFaces'],signedVolume=o['signedVolume'],scale=','.join(map(str,o['scale'])),collider=o['collider'],initialProblem='; '.join(issues) or 'No geometry defect detected in initial strict source check; material/reference review required',severity='MEDIUM' if issues else 'LOW',status='Preserved; staged fixes and roundtrip verified; target/visual pending'))

def archref(o):
 group=o['collections'][0]
 if group in ['Facade','FacadeDetails','RaisedWings','Terraces','TerraceRails','TenantSigns']:return 'E1, E2, E3, E4; right side inferred'
 if group in ['FrontDome','DomeDetails','DomeGuards','RoofSign','RoofSignText']:return 'E1, E2, E4'
 if group in ['RearSign','ServiceRisers','RoofPlant']:return 'E3, E4'
 if group in ['RoofDeck','RoofAccess','RoofStairs']:return 'E4; roof access layout inferred'
 if group in ['Vestibule','VestibuleDetails']:return 'E1, I2; exact vestibule dimensions inferred'
 return 'I1, I2, I3, I4; two floors explicitly required by user'

for o in shell0['objects']:
 if o['type']!='MESH':continue
 issues=[]
 if o['nonManifoldEdges']:issues.append('open/nonmanifold text mesh')
 if o['degenerateFaces']:issues.append('degenerate faces')
 if not o['uvLayers']:issues.append('missing UV')
 if o['name'] in bevelnames:issues.append('hard architecture edge lacks physical bevel')
 legacy=o['nonManifoldEdges']>0 or o['degenerateFaces']>0
 inventory.append(dict(scope='KursHub shell/'+','.join(o['collections']),name=o['name'],type='MESH',reference=archref(o),materials=','.join(o['materials']),triangles=o['triangles'],uv=','.join(o['uvLayers']),nonmanifold=o['nonManifoldEdges'],degenerate=o['degenerateFaces'],signedVolume=o['signedVolume'],scale=','.join(map(str,o['scale'])),collider=o['custom'].get('unity_collision',False),initialProblem='; '.join(issues) or 'No basic source geometry defect detected; context/reference review required',severity='HIGH' if legacy else ('MEDIUM' if issues else 'LOW'),status='REPAIRED_UNITY_PENDING: text welded/caps repaired; strict source and FBX PASS' if legacy else ('Physical bevel staged; target/visual pending' if o['name'] in bevelnames else 'Original object preserved; target/visual pending')))
with (OUT/'Initial_Object_Inventory.tsv').open('w',encoding='utf8',newline='') as file:
 writer=csv.DictWriter(file,fieldnames=list(inventory[0]),delimiter='\t');writer.writeheader();writer.writerows(inventory)
(OUT/'Per_Prop_Fixes.json').write_text(json.dumps(counts,indent=2,ensure_ascii=False),encoding='utf8')

overview=[
 ('Корпус / силуэт','E1, E2, E4','Объём уже приближен на этапе 3; не хватает глубины окон и строительных сопряжений.','HIGH','Сохранить 93 × 130 м/закругление/террасу; добавить откосы и швы.','94 оконные секции, 1166 деталей staged; AFTER PENDING'),
 ('Правая сторона','E4 частично; отдельного изображения нет','Точный обмер и правый ортогональный ракурс отсутствуют.','LOW','Согласованная интерпретация языка левого/заднего фасада.','ASSUMPTION; right_assumed.png подписан'),
 ('Оконные секции','E1, E2, E3','Остекление читается как плоский лист в тонкой раме.','HIGH','Откосы, наружные/внутренние элементы, нижний слив и контакт с облицовкой.','Геометрия staged; target/AFTER PENDING'),
 ('Крыша / парапеты / вентиляция','E3, E4','Недостаточно сопряжений и небольших деталей; масштаб крупного узла должен сохраняться.','MEDIUM','Накладки, стыки и физические радиусы без новых грузовых проёмов.','Геометрия staged; target/AFTER PENDING'),
 ('Наружное окружение входа','E1, E2, E4','BEFORE значительно свободнее и регулярнее укреплённого обжитого периметра в изображениях.','HIGH','Сопоставить плотность существующих отдельных props; не возвращать удалённый козырёк.','144 внешних instances вместо63; свободный центральный вход16м; AFTER PENDING'),
 ('Рынок / расстановка','I1, I4','В BEFORE видны регулярные одинаковые ряды и повторяющийся товар, большие незанятые плоскости.','HIGH','Различать занятия/товар, асимметрично уплотнить микросцены при сохранении проходов.','31 лавка с небольшим разворотом ±2,4°;9 оружейных стоек+12 debris;563 instances; AFTER PENDING'),
 ('КПП / контекст','I2','BEFORE рамки и сетки изолированы в широком пустом пространстве; свет слишком равномерно оранжевый.','HIGH','Сопоставить плотность стола, мешков, проверяемых вещей, ткани и локального света.','Детектор и материалы переработаны; художественная оценка PENDING'),
 ('Ночлеги / галерея','I3, I4','Ритм кроватей/ширм слишком однообразен; крупная клетка ткани.','HIGH','Материал меньшей частоты, крепления/заплаты по складкам, разнообразие личных вещей.','Ткань/узлы исправлены в source; AFTER PENDING'),
 ('Керамический пол','I1, I2, I4','Сильный повтор короткого тайла и нехватка близкого физического рельефа.','HIGH','Светлая/красно-коричневая плитка 0,5 м, грязь, затирка, сколы и roughness.','4096² / период 8 м / 256 плиток: bitmap PASS; target PENDING'),
 ('Ткань, письмо, подвесы','I1–I4','В BEFORE клетка похожа на печатный узор; буквы выглядят чрезмерно светящимися.','HIGH','Тонкая тканая normal, мягкое цветовое изменение; убрать только старую generated-эмиссию краски.','168-карточный PBR-набор readback PASS; target PENDING'),
 ('Архитектурные буквы','E1–E4','В baseline41 текстовый mesh имел nonmanifold-грани,7 содержали degenerate faces; дефекты группировались в11 FBX chunks.','HIGH','Сварить совпадающие вершины, восстановить caps; строго проверить каждый solid после экспорта.','REPAIRED_UNITY_PENDING:41 исходный объект исправлен; strict source+3 FBX PASS; legacy exceptions=0'),
 ('LOD-экспорт мелких деталей','Технический аудит','24 первые LOD-FBX после reduction получили открытые/инвертированные мелкие solids.','CRITICAL','Отбросить дефектные chunks, взять безопасный предыдущий LOD, повторно импортировать каждый FBX.','RESOLVED в staging: 160 повторных импортов PASS; rejected сохранены отдельно'),
 ('Коллизии','I2, I3 и проходы пользователя','Сгруппированные MC там, где возможно аналитическое разложение; замена целым bounds закрыла бы проёмы.','MEDIUM','Только доказанные группы ≤12 отдельных OBB; остальное точный MeshCollider; старые IDs сохранить.','37 chunks / 143 OBB schema PASS; Unity containment и проходы PENDING'),
 ('UV2 / bake','Технический аудит Unity','В baseline отсутствует вторичная UV части мешей; качество запекания не подтверждено.','MEDIUM','Генерация UV2 в ModelImporter, проверка каналов; bake отдельно.','Код подготовлен; target PENDING; bake NOT TESTED'),
 ('Стабильный FPS с населением','Пожелание пользователя','Отсутствуют нагрузочная сцена с целевыми NPC и измерение среднего ПК.','HIGH','Отдельный измеримый performance pass после населённой игровой сцены.','NOT TESTED; число треугольников/LOD не является доказательством FPS')]

reference_table=mdtable(['ID','Файл (относительно Documents)','Просмотренные признаки'],[(k,*v) for k,v in refs.items()])
overview_table=mdtable(['Object','Reference','Problem','Severity','Required Fix','Status'],overview)
prop_table=mdtable(['Object','Reference','Problem','Severity','Required Fix','Status'],prop_rows)
count_table=mdtable(['Prop','Before tris','LOD0','LOD1','LOD2','Beveled solids','Wood UV solids','Added solids','Status'],fix_rows)

audit = '''# Аудит ТЦ «Курс» и повторно используемых props — ревизия 7

Дата отчётного среза: 20.10.2026. Статус: **промежуточный доказательный отчёт; целевая Unity-сборка, новые AFTER-ракурсы и итоговый перенос ожидаются**. Этот документ не является заявлением о полном завершении, профессиональной визуальной приёмке или стабильном FPS.

Область — созданные в этом чате ТЦ «Курс», собственные архитектурные модули и 53 прежних props плюс новый KH_WeaponsDisplay. Сторонние наборы, деревья Moscow Trees и модели второго разработчика не пересобираются. Исходный проект, scene overrides и GUID сохраняются; текущее состояние зафиксировано в work/KursHub/audit7/baseline. Внешние референсы просмотрены как изображения; надписи на изображениях не являются инструкциями к выполнению.

## Основание и допущения

Все восемь исходных изображений просмотрены повторно. Видны несовместимые между собой детали/число ярусов: это концептуальные изображения, а не согласованный обмерный план. Приоритет имеют подтверждённые пользователем решения: наружный контур 93 × 130 м; два полных этажа на Y=0/5,5 м и выход на крышу Y=11 м; главный фасад под крышной вывеской; закруглённый вход. В I3/I4 читаются дополнительные ярусы, но третий полный этаж не добавляется. Прежнее решение убрать козырёк сохраняется. Грузовой вход не прорезается. Правая сторона и скрытая служебная планировка — интерпретации, без утверждения о точном воспроизведении существующего здания.

'''+reference_table+'''
## Метод сравнения и пределы доказательств

BEFORE зафиксирован через реальную Unity-камеру: front, left, rear, right_assumed, top45, market, checkpoint, gallery, detail_floor, detail_stall. Камеры, цели и FOV записаны в work/KursHub/audit7/before/cameras.tsv и должны оставаться прежними для пары BEFORE/AFTER. Референс/BEFORE могут иметь различную перспективу и постобработку: точная фото-калибровка без исходной камеры невозможна. В таблице указаны наблюдаемые признаки и оставшиеся вопросы, а не вымышленный процент сходства. AFTER-виды, дневной/пасмурный/скользящий свет и близкие материалы нужно оценить отдельно после интеграции.

## Первоначальная инвентаризация

Снимок архитектурного Blender: 3480 mesh-объектов, 735 экземпляров коллекций, 54 источника света, 3 камеры, 36 материалов и 92 image datablocks. Последнее число включает datablocks и не означает 92 разных PNG. Текстурный baseline — 71 PNG (62 × 1024² и 9 × 512²). Библиотека прежних props — 1535 замкнутых solids в 53 прототипах, 147578 треугольников уникального LOD0.

Полная индивидуальная начальная ведомость **5015 исходных мешей** (3480 оболочка +1535 props) находится в Initial_Object_Inventory.tsv: имя, группа, материал, UV, треугольники, объём, scale, collider, проблема/важность/состояние и связь с изображением. Машинные оригиналы architecture_before.json и geometry/props_before.json сохраняют дополнительные поля, bounds и модификаторы. Таблица не выдаёт автоматически найденный корректный mesh за художественно принятый объект. Для неизвестных маленьких товаров соответствие означает тип/материал и назначение, а не точное повторение отдельной вещи по невидимому крупному плану.

В исходной оболочке обнаружены 41 nonmanifold текстовый объект, 7 объектов с degenerate faces, 0 mesh без UV и 0 объектов с неположительным суммарным объёмом. Суммарный положительный объём не доказывает правильность каждого связного тела: отдельная roundtrip-проверка выявляет это отдельно. Все41 текстовый объект затем исправлены сваркой совпадающих вершин и восстановлением37 вырожденных крышечных треугольников. Свежий строгий architecture_roundtrip.json проверяет4646 итоговых source mesh и три реальных FBX: ноль ошибок и ноль legacy-исключений. Это устранённые начальные дефекты, а не открытая текущая ошибка; Unity-проверка ожидается.

## Основные проблемы и фактическое состояние

'''+overview_table+'''
## Все самостоятельные props: соответствие и необходимые исправления

Статус SOURCE/FBX PASS означает проверку исходника и обменной геометрии. Он не означает PASS материала в целевом движке или визуальное равенство референсу.

'''+prop_table+'''
## Что остаётся подтвердить в целевом проекте

1. Реальный импорт и сборку в отдельной копии Unity 6000.6.3f1 / URP 17.6.0; пустые material slots, Missing Script, геометрию UV0/UV2, положительный scale и ссылки на все уровни LOD.
2. Сохранение прежних GUID/fileID/scene targets при двух сборках, затем перенос без изменения CharacterTest и чужих станций.
3. Проход настоящим FirstPersonController через обе рамки КПП, проходы у лавок, лестницы, ночлеги и выход на крышу; отдельные порталы и защиту краёв. Компиляция и наличие коллайдера не доказывают проходимость.
4. Контрольные AFTER-виды по прежним камерам и REFERENCE/BEFORE/AFTER-доски; свет день/пасмурно/вдоль поверхности и близкие материалы. Видимые регулярные ряды и бедное окружение нельзя считать исправленными только по количеству новых mesh.
5. Экспорт .unitypackage через Editor API, чтение зависимостей и проверку настоящего повторного импорта. Упорядоченные loose-копии не заменяют проверку переносимых ссылок.

NPC, логика торговли/ремонта/безопасной зоны, NavMesh, население, окончательный bake, occlusion culling и FPS среднего ПК этим аудитом не подтверждены. Прежние ошибки WorldSaveRegistry не исправляются статической архитектурой; валидатор не ослабляется. Итоговая художественная приёмка остаётся открытой.
'''
(OUT/'Audit_Report.md').write_text(audit,encoding='utf8')

fixes='''# Исправления ТЦ «Курс» — ревизия 7

Дата среза: 20.10.2026. **Staging/source/FBX/bitmap результаты приведены ниже; интеграционная проверка Unity и окончательная визуальная оценка ожидаются.** Формулировки PASS относятся только к названному испытанию.

## Архитектура

Сохранены все 3480 прежних source mesh и их имена. Добавлены 1166 редактируемых деталей, 1211 физических фасок, обработаны 94 оконные секции. Имеются углублённые элементы оконного примыкания, подоконные/сливные профили, рамные стыки и элементы крышных сопряжений. Контур опорной геометрии доказан отдельно: 93 × 130 м. Полный FBX bounds шире контура стен из-за выступающих рам/отливов/подхода; масштаб здания ради экспорта не меняется.

Треугольники оболочки LOD0/LOD1/LOD2: **350372 /198244 /108188**, частей 389/389/299. Все три FBX действительно экспортированы и повторно импортированы Blender; сохранены старые chunk names и базовые размеры. Все41 прежний дефектный текстовый mesh исправлены сваркой с допуском0,00001м и восстановлением37 вырожденных треугольников крышек. Повторная строгая проверка4646 source mesh и389/389/299 экспортных chunks не содержит ошибок или legacy-исключений; состояние REPAIRED_UNITY_PENDING. Отдельный architecture_text_fixes.json фиксирует исходные/исправленные счётчики каждого текстового объекта.

Наружное наполнение увеличено с63 до144 отдельных reusable instances: добавлены81 ограждение/баррикада/доски/вещи, центральный проход у входа оставлен шириной16м. Поселение увеличено с542 до563 instances: сохранены прежние ID,31 лавка получила небольшое изменение положения/разворота в пределах±2,4°, добавлены9 оружейных стоек и12 групп мусора. Это реально изменённые данные расстановки; художественное сходство и проходимость должны подтвердить AFTER/Unity-проверки.

## Библиотека props

Сохранены прежние 53 ID и все 1535 original source solids. Добавлены 469 деталей, всего 2004 solids в 54 нативных коллекциях. Новый KH_WeaponsDisplay содержит стойку, отдельные профили прикладов/корпусов/стволов и крепления; это статическая выкладка без оружейной механики. Другие добавления — столярные крепления, гвозди/болты, узлы стоек, перемычки табурета, вентиляционные элементы детектора и радиаторы прожектора. 639 элементов имеют редактируемую физическую фаску; **416 деревянных частей имеют подтверждённую Blender custom property Audit7WoodUV** с UV вдоль продольной оси. Ведомость prop_source_properties.json получена отдельным фактическим открытием сохранённого .blend, не оценкой по имени материала.

LOD0/1/2 библиотеки: **219190 /131480 /69482** треугольника. KH_GrimePatch имеет только 12 треугольников и сознательно не получает бессмысленные LOD. Все 160 FBX (54 LOD0 +106 сниженных) повторно импортированы. Найденный дефект reduction мелких несвязанных solids исправлен в 24 файлах/38 chunks: только негодные chunks возвращены к предыдущему безопасному уровню. Проверка повторена после исправления, rejected-файлы сохраняются отдельно и не входят в поставку. У всех проверялись chunks/root, UV, материалы, bounds, manifold, нормали и объём каждого связного тела.

'''+count_table+'''
## PBR

Пересобраны **24 семейства ×7 карт =168 PNG**, 288223231 байт до платформенной компрессии; прежний прозрачный DirtStreaks_BaseColor сохранён отдельно. Это авторские процедурные поверхности, не сканы. CeramicTile 4096²/8×8 м: 16×16 плиток по0,5 м, 83 красно-коричневые и173 светлые. Основные архитектурные, деревянные и тканевые поверхности —2048²; небольшие однородные второстепенные материалы —1024². Убрана крупная печатная клетка ткани; древесный рисунок согласован с ориентированными UV.

Файлы: BaseColor(sRGB), Normal(linear tangent+Y), Roughness, Metallic, Occlusion, Height(16 bit) и MetallicSmoothness(linear R=metallic,A=1−roughness,G=B=0). Отдельные scalar карты прочитаны повторно; packed channels проверены с погрешностью 1/255. Нормаль заново вычислена из сохранённой Height, знак каналов подтверждён двумя аналитическими наклонами. Диэлектрики имеют metallic=0. AO — консервативная процедурная окклюзия микрорельефа/швов, не геометрический bake здания. Height — реальный обменный микрорельеф; runtime displacement/parallax не включён.

Проверка verify_pbr7.py: PASS, но не доказательство реалистичности материала в игровом освещении. Импортные пределы 1K/2K/4K, mipmaps, фильтрация и назначение реальных карт должен подтвердить целевой Unity отчёт. Старое известное значение эмиссии ClothInk отключается узкой миграцией; произвольные авторские изменения материалов не сбрасываются.

## Коллизии, LOD и переносимые ссылки

QualitySetup использует доказанные аналитические OBB только для целиком cuboid частей и не более12 boxes на chunk: 37 подходящих chunks/143 OBB для уникальных типов. MC сохраняется отключённым вместе с fileID, примитивы находятся отдельно под Quality collision/chunk/source. Остальные части сохраняют точную mesh collision. Доказательство геометрии исходника не означает пройденный игровой маршрут; при импорте дополнительно проверяется покрытие FBX vertices этими OBB с допуском2 мм. Визуальные LOD не содержат коллайдеров и не выключают рабочую коллизию.

Подготовлены команды Editor API для независимого аудита, демонстрационной сцены и .unitypackage со всеми KursHub-зависимостями. На этом срезе их новые результаты **PENDING**. Canonical Assets/Environment/KursHub и GUID сохраняются. В ordered FINAL_ASSETS экспортные папки служат обмену/просмотру; авторитетный перенос настроенных Unity материалов/LOD/коллайдеров — проверенный native package.

## Неподтверждённое и остающееся

Целевой импорт/Play, BEFORE/AFTER и визуальная приёмка, FPS с населением, baked lighting, NavMesh и игровые сервисы пока не заявлены. Начальные текстовые mesh-проблемы раскрыты и исправлены, строгий повторный source/FBX тест проходит без исключений. Отчёт не разрешает игнорировать оставшиеся художественные HIGH-замечания только потому, что source/FBX/bitmap проверки пройдены.

Источники доказательств: architecture_fixes.json, architecture_text_fixes.json, architecture_roundtrip.json, source_assembly.json, geometry/props_before.json, props_after.json, source_roundtrip.json, fbx_roundtrip.json, lod_export_repairs.json, prop_source_properties.json, colliders.json, unity_collider_contract_check.json, reports/PBR_After_Manifest.json, PBR_Readback.json, PBR_Report.md. Ссылки следует сопоставлять с хешами итоговой поставки после интеграции.
'''
(OUT/'Fixes_Report.md').write_text(fixes,encoding='utf8')

doc='''# ТЦ «Курс»: контракт пересборки и аудит качества 7

Срез 20.10.2026: source/FBX/PBR bitmap проверки выполнены в staging, **целевая интеграция и итоговая художественная оценка ожидаются**. Подробный реестр Object/Reference/Problem/Severity/Required Fix/Status — FINAL_ASSETS/REPORTS/Audit_Report.md; изменения и счётчики всех54 props — Fixes_Report.md. Индивидуальные начальные состояния5015 meshes — Initial_Object_Inventory.tsv. Не переписывать этот статус как «полностью проверено», пока не добавлены реальные Unity/Play/перенос/AFTER результаты.

## Стабильные пути и границы

- Единственный переносимый корень здания: Assets/Environment/KursHub/KursHub.prefab. Геометрия оболочки: Models/KursHubShell.fbx, дистанционные модели Models/LOD/KursHubShell_LOD1.fbx и LOD2.fbx.
- Отдельные предметы: Props/Models/<ID>.fbx и Props/Prefabs/<ID>.prefab; сниженные модели Props/Models/LOD/<ID>_LOD1/2.fbx. Прежние53 ID/префаба сохраняются, новыйID — KH_WeaponsDisplay. Все модели — настоящий Blender FBX export, не переименованный иной формат.
- Редактируемые Sources/KursHub/KursHub.blend и KursHubProps.blend остаются вне Assets; промежуточные working/baseline/rejected/export-probe файлы не импортируются в продукт.
- CharacterTest, SampleScene, gameplay, настройки ввода, vendor-папки и активный редактор пользователя не перестраивать. Unitybatch — только отдельная копия с Unity6000.6.3f1 и тем же URP17.6.0.

Контур стен93×130 м, два полноценных этажа0/5,5 м, крыша11 м, атриум30×50 м, закруглённый вход и прежний тестовый маршрут сохраняются. Референсы с дополнительными ярусами не отменяют явное число этажей пользователя. Отдельного правого референса/обмерного плана нет: right_assumed и скрытая планировка интерпретируются. Козырёк остаётся удалённым; грузовой проём не добавляется.

## Регенерация

Tools/KursHub/audit7_geometry.py читает архивный stage6 baseline и добавляет реальные фаски/узлы/UV в отдельном --root. Нельзя применять его к уже исправленному source, удваивая детали. Одноразовый экспорт сам по себе не закрывает проверку LOD: его первая reduction дала24 дефектных FBX. Проверки verify_exports.py/repair_lod_exports.py из work/KursHub/audit7/geometry требуются для guarded workflow; повторная запись модели без этих guards способна вернуть дефект. Сначала сохранять ручные изменения, GUID и исходники.

Tools/KursHub/audit7_architecture.py сохраняет3480 прежних объектов, добавляет фаски/оконные сопряжения и исходные LOD. Найденные41 дефектный текстовый source mesh (11 сгруппированных FBX chunks) исправлены сваркой совпадающих вершин с допуском0,00001м и восстановлением37 треугольников caps. architecture_text_fixes.json содержит изменения каждого объекта; новый architecture_roundtrip.json строго проверяет4646 source mesh и три FBX без legacy-исключений. При регенерации не возвращать старую открытую топологию текста и не обходить проверки двусторонним материалом. Предельный статус на этом срезе — REPAIRED_UNITY_PENDING.

Внешние props:144 instances вместо63, центральный проход16м; settlement563 вместо542, включая9 оружейных стоек/12 debris и небольшой разворот31 лавки±2,4°. Прежние735 native collection instances сохранены. Считать исправленной плотность и проходимость можно только после AFTER и реального маршрута, а не по счётчикуinstances.

audit7_surfaces.py + verify_pbr7.py создают и повторно читают168 карт; audit7_materials.py подключает их к Blender сохраняя неизвестные авторские image inputs. Физическая UV базовой архитектуры метры/2, material tiling=2/период; деревяннаяV ось вдоль детали, rug UV0..1. Нормали tangent+Y; Rmetallic/Asmoothness; Height16bit с min/max в метрах. AO процедурный локальный, не геометрическое запекание. Height не включать в активный parallax без отдельного решения/проверки.

## Unity Editor API

KursHubSetup и KursHubPropSetup вызывают KursHubQualitySetup. PropSetup создаёт UV2 через ModelImporter.generateSecondaryUV. LOD хранится отдельными native child models: Quality LOD1/2 у props и Quality Building LOD1/2 у здания. Root LODGroup здания включает только Shell: вложенные предметы, свет и постоянные защитные коллайдеры им не владеются. В каждой группе строго3 уровня с уменьшением треугольников; 12-треугольный KH_GrimePatch оставлен без бессмысленной пары.

Коллизии независимы от визуального LOD. Props/KursHubColliderManifest.json содержит chunks[].name/allBoxes/remainingSolids/boxes; Unity metres prop-local, quaternionx/y/z/w. Только allBoxes=true,remainingSolids=0 и1..12 boxes заменяют активный MC отдельными BoxCollider. Путь Quality collision/<chunk>/<source> стабилен, исходный MC остаётся отключённым, старые IDs сохраняются. Родитель/дочерние poses проверяются, импортированные vertices должны находиться внутри доказанных OBB. Legacy manifest без per-chunk mapping безопасно оставляет MC и пишет предупреждение. Нельзя заменить портал одним общим bounds.

Команды меню: Tools/Character Lab/Audit Kurs Hub Quality; Create Kurs Hub Standalone Demo; Export Kurs Hub Quality Package. Вывод задаётся KURS_FINAL_OUTPUT. Demo сохраняется Assets/Environment/KursHub/Scenes/KursHubDemo.unity. Пакет сохраняет canonicalpaths и проверяет зависимости: KursHub+URP/ShaderGraph+builtinUnity resources; не включает CharacterTest/player/gameplay. Отдельные arranged папки FINAL_ASSETS не должны приводить к потере GUID-связей; настроенный native package нужно реально импортировать.

## Обязательный ручной/автоматический контроль

Проверить source retention и FBXroundtrip; реальные UV0/UV2, материалы и текстуры в Unity; повторную сборку с сохранением ID; OBBcoverage; проход действующим FirstPersonController через обе рамки КПП, галереи/лавки/лестницы/крышу и отдельные порталы. Сравнить BEFORE/AFTER по cameras.tsv, отдельно просмотреть день, пасмурно и скользящий свет, рамы/дерево/керамику/ткань вблизи. Не выдавать source metrics за визуальную приёмку. Не объявлять FPS сNPC, AI/NavMesh, safezone, окончательный bake, торговлю и сохранения проверенными моделированием. Общий WorldSaveRegistry не ослаблять ради хаба; прежние ошибки сцены фиксировать отдельно.
'''
(ROOT/'Docs/KursHubQualityAudit.md').write_text(doc,encoding='utf8')
for p in [OUT/'Audit_Report.md',OUT/'Fixes_Report.md',ROOT/'Docs/KursHubQualityAudit.md']:
    content=p.read_text(encoding='utf8')
    content=re.sub(r'(?<=[А-Яа-яёЁ])(?=\d)', ' ', content)
    content=re.sub(r'(?<=\d)(?=[А-Яа-яёЁ])', ' ', content)
    content=content.replace('Unity6000','Unity 6000').replace('URP17.6','URP 17.6').replace('NPC,','NPC,')
    p.write_text(content,encoding='utf8')
print('AUDIT_REPORT_DRAFTS_OK',len(props1),'props',len(inventory),'initial meshes',len(bevelnames),'shell bevels',sum(len(p['woodUVObjects']) for p in source.values()),'wood UV')
