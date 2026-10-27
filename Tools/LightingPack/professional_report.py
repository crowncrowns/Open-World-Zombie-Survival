from catalog import *
from pathlib import Path
import json,re,shutil,hashlib
proof=VERIFY/'ProfessionalAudit';load=lambda name:json.loads((proof/name).read_text(encoding='utf-8'))
original=load('initial_inventory.json');before=load('before_unity.json');after=load('after_unity.json');clean=load('clean_fbx_audit.json');ids=load('identity_preservation.json');cold=load('cold_import.json');edit=load('edit_checks.json')
play=json.loads((VERIFY/'unity_play_checks.json').read_text(encoding='utf-8'));assert all(d['passed'] for d in [after,clean,ids,cold,edit,play])
assert len(after['assets'])==100 and ids['fbxChanged']==100
badbefore=[a for a in before['assets'] if a.get('issues')];boundary=sum(m['boundaryEdges'] for a in clean['results'] for m in a['meshes']);nonmanifold=sum(m['nonmanifoldEdges'] for a in clean['results'] for m in a['meshes']);zero=sum(m['zeroArea'] for a in clean['results'] for m in a['meshes'])
assert boundary==nonmanifold==zero==0
meta=[]
for rel in original['files']:
    if not rel.endswith('.meta'):continue
    old=Path(original['backup'])/'LightingPack'/rel;new=ASSET/rel
    a=re.search(r'^guid: (\w+)',old.read_text(encoding='utf-8'),re.M);b=re.search(r'^guid: (\w+)',new.read_text(encoding='utf-8'),re.M)
    assert (a.group(1) if a else None)==(b.group(1) if b else None),rel
    meta.append(rel)
materials=len(list((ASSET/'Materials').glob('*.mat')));textures=len(list((ASSET/'Textures').rglob('*.png')))
manifest=json.loads((ASSET/'manifest.json').read_text(encoding='utf-8'));lod0=[f['triangles'][0] for f in manifest['fixtures']]
rows=load('discrepancies.json')
table='\n'.join('| '+r['Object']+' | '+r['Reference']+' | '+r['Problem']+' | '+r['Severity']+' | '+r['RequiredFix']+' | '+r['Status']+' |' for r in rows)
text=f'''# LightingPack — ревизия всех 100 префабов, 25.10.2026

В существующих файлах обновлены **все 100 префабов и все 100 FBX**: 90 светильников (30 типов × Used/Worn/Damaged) и 10 декоративных предметов. Это фактическая геометрическая и материальная ревизия, не только аудит. До установки исходный пакет целиком сохранён в `{original['backup']}`. Базовый commit при инвентаризации: `{original['head']}`.

## Что исправлено

- Строгая загрузка исходной версии обнаружила пустые импортированные меши в **{len(badbefore)} префабах**: крепёж, аварийные индикаторы, отдельные колбы и детали LOD. После пересборки каждый MeshFilter во всех 100 префабах имеет непустой Mesh.
- Заново построены плавные профили плафонов и колб, шар уличного фонаря; добавлены конструктивные уплотнения, резьба патронов, шайбы/винты, PG-вводы, колодки трубок, клипсы панелей, радиаторы и соединения кронштейнов. Советский консольный корпус вытянут по продольной оси. Повреждения остаются геометрическими.
- Прожекторы получили тонкостенные наклонные отражатели. Пересекавшиеся с ними прежние внутренние боковые вставки удалены во всех трёх LOD соответствующих девяти вариантов.
- Все 10 декоративных ассетов пересобраны отдельно: гибкие провода/гофра, монтажная коробка с швом крышки и крепежом, усиленный кронштейн, резьбовой патрон, лампа, плафон, болты/шайбы, выключатель с обрамлением и след снятой лампы. У последнего — полупрозрачная рваная маска старого покрытия, остатки отверстий/проводов, без непрозрачной наклеенной доски и без физического препятствия от краски.
- Убраны остаточные плоские фрагменты мелкого крепежа в дальнем LOD и закрыты экспортные микродефекты торцов. При финальном чистом FBX re-import: **{boundary} открытых рёбер, {nonmanifold} неманифолдных рёбер, {zero} граней с нулевой площадью**. Это топологическая проверка; она не доказывает отсутствие всех возможных взаимных пересечений сборных деталей.
- UV2 построены в Blender и импортируются готовыми. Автоматическое создание UV2 в Unity отключено у всех 100 FBX; причиной прежних пустых мешей был в том числе сбой этого этапа. UV2 присутствуют, конечны и лежат в 0…1; настоящий light bake отдельно не выполнялся.
- Пересобраны 24 комплекта PBR-карт. Окрашенная сталь диэлектрическая; металлическими остаются обнажённые сколы. Коррозия стали, оксид алюминия и окисление меди различаются. BaseColor не содержит нарисованного освещения. Normal рассчитан из периодического физического микрорельефа в метрах, не из яркости BaseColor. Разнообразие Roughness и Height сохранено в отдельных картах. Есть Opacity для прозрачного стекла. Старая общая baked-normal и её .blend сохранены как исторический источник, но новые поверхности используют свои height-derived Normal.
- Всего сейчас **{materials} материалов URP Lit и {textures} PNG 2048²**. Высота микрорельефа, диапазоны Roughness и доли коррозии перечислены в `surface_parameters.json`. Это авторские процедурные поверхности, не фотоскан и не индивидуальный high-poly AO bake. AO — слабая микрополостная аппроксимация; Height не включает автоматически displacement в стандартном URP Lit.
- Активные подробные MeshCollider заменены ограничивающими простыми BoxCollider на непрозрачных частях. Старые MeshCollider-компоненты сохранены выключенными ради совместимости ссылок. Коллизии небольших статичных приборов приблизительные; для Rigidbody/разрушения нужна отдельная настройка. Collider следа снятой лампы отключён.

## Сохранность проекта

Проверены все **100 прежних prefab GUID**, все **{ids['localIDsPreserved']} прежних внутренних YAML fileID**, существующие имена/иерархии и GUID **{len(meta)} старых .meta**. Идентификаторы не потеряны. Дополнительные компоненты коллизии не заменяли исходные объекты. Настройки питания, интенсивности, цвета, температуры, режима, теней, мерцания, гула и culling всех 90 контроллеров сравнивались до/после и сохранены. Источники Light и их относительные веса остались прежними; пересобраны привязки Emission к обновлённым материалам.

Игровые сцены основного проекта, Player, Packages и ProjectSettings этим заданием не редактировались. Проверки выполнялись только в `work/LightingPack/UnityProject`. Строители старой версии `build_models.py`/`LightingPackBuilder.Build` не следует запускать поверх этой ревизии: актуальная редактируемая модель — `Source~/LightingPack_Master.blend`, а воспроизводимые этапы ревизии — `Tools/LightingPack/professional_*` и защищённый `LightingPackProfessionalAudit`.

## Проверки

| Проверка | Результат |
|---|---|
| Чистый Blender FBX re-import | 100/100; Blender {clean['blender']}; геометрия/UV0/UV2/материалы/конечные координаты/топология |
| Типы/варианты | 30 уникальных типов Used; каждый Damaged геометрически отличается |
| LOD | По три убывающих LOD у всех 90 светильников; LOD0 {min(lod0)}–{max(lod0)} треугольников |
| Unity import и cold | Unity {after['unity']} / URP 17.6.0; 90 светильников + 10 декора; пустых Mesh и потерянных материалов/скриптов не обнаружено |
| GUID и fileID | 100/100 prefab GUID, {ids['localIDsPreserved']} старых внутренних fileID, старые .meta GUID сохранены |
| Play | {len(play['checks'])} адресных проверок: режимы, реальные Light, Emission, независимость экземпляров, питание/возврат, собственный корпус/Spot, тени, culling, гул и Update |
| Editor | {len(edit['checks'])} проверок: параметры, MPB всех LOD, независимость, питание, Undo/Redo, сохранение overrides после scene reload |
| Native изображения | 900 BEFORE + 900 AFTER: семь ракурсов при дневном свете, плюс 45° при пасмурном/скользящем свете для каждого из 100 |

Сводные листы всех 100 объектов и выборочные крупные сравнения просмотрены визуально. Изображения сохраняют реальную геометрию из Unity. Для `Decor_BrokenBulb`, ставшей физически более длинной после добавления патрона, AFTER снят с общим охватом двух габаритов; оригинальный полный BEFORE приведён к этому ортографическому охвату масштабированием и добавлением фона. Исходный BEFORE сохранён отдельно. Это корректировка кадрирования, не дорисовка модели.

В Play может присутствовать отдельный `ArgumentOutOfRangeException` из `UnityEditor.Search.SearchDatabase` при индексировании; он отмечается отдельно от результатов пакета. Ошибок компиляции C# в итоговых проверках нет. Проверочная пустая сцена до создания камеры может предупреждать об AudioListener. Нельзя называть весь журнал Editor полностью чистым на основании PASS тестов.

## Референсы и ограничения приёмки

В приложении пользователя — текстовое задание, **исходных фотографий светильников в нём и каталогах LightingPack не найдено**. Каталожные габариты НПП/ДСП и существующий дизайн использованы как ориентиры; остальные размеры/детали — авторские допущения. Поэтому колонка REFERENCE в сравнениях честно помечена «не предоставлен». Прежние рендеры не выданы за фотографии-референсы. Полная фото-ориентированная художественная приёмка и утверждение о точном сходстве с исходными изображениями невозможны без этих изображений.

Не заявляются: AAA/фотоскан/точная копия реальных приборов, индивидуальный high-poly normal/AO bake каждой модели, отсутствие всех пересечений во всех игровых ракурсах, native OS-мышь, main-project Play, standalone build, массовый FPS, baked/Mixed GI или player/world-save/электросеть/Interact-интеграция. Прозрачное стекло использует обычную прозрачность URP, без физического преломления. При выключенных тенях Point/Spot могут проходить через стены. MPB свечения может исключать Renderer из batching.

## Файлы

- `Assets/PostApocalypse/LightingPack` — обновлённые игровые ассеты и исходники.
- `Verification/LightingPack/ProfessionalAudit` — исходная инвентаризация, 1800 native кадров, 200 сравнительных листов, JSON проверок и таблица расхождений CSV.
- `work/LightingPack/ProfessionalAudit/Before` — полная резервная копия до ревизии.
- `outputs/LightingPack_URP.unitypackage`, `outputs/LightingPack_Unity.zip` — актуальные архивы после успешной упаковки.

![Used](After_Used.jpg)

![Декор](After_Decor.jpg)

## Таблица расхождений — все 100 объектов

| Object | Reference | Problem | Severity | Required Fix | Status |
|---|---|---|---|---|---|
{table}
'''
(proof/'Report_RU.md').write_text(text,encoding='utf-8')
doc=ASSET/'Documentation/ProfessionalAudit';doc.mkdir(exist_ok=True)
for name in ['After_Used.jpg','After_Worn.jpg','After_Damaged.jpg','After_Decor.jpg','Discrepancies.csv','identity_preservation.json','surface_parameters.json','clean_fbx_audit.json']:
    shutil.copy2(proof/name,doc/name)
shutil.copytree(proof/'Comparisons',doc/'Comparisons',dirs_exist_ok=True)
(doc/'Report_RU.md').write_text(text,encoding='utf-8')
summary=f'''# LightingPack — актуальная ревизия всех 100 префабов

25.10.2026: переработаны все 100 префабов и все 100 FBX, {materials} URP Lit материалов и {textures} PNG 2K. GUID 100 префабов и {ids['localIDsPreserved']} прежних внутренних fileID сохранены. Исходные файлы до изменения — в `work/LightingPack/ProfessionalAudit/Before`.

Исправлены пустые меши в {len(badbefore)} префабах, профили плафонов/колб, отражатели, крепёж, вводы и декор. UV2 поставляются из Blender. На финальном чистом FBX re-import: 0 открытых и неманифолдных рёбер, 0 вырожденных граней. Отдельный Unity import/cold всех 100, {len(play['checks'])} Play и {len(edit['checks'])} Editor проверок прошли. Снято 1800 native кадров до/после и подготовлены сравнения для каждого объекта.

Полный отчёт, таблица всех 100 объектов и ограничения: [ProfessionalAudit/Report_RU.md](../Verification/LightingPack/ProfessionalAudit/Report_RU.md).

![Обновлённые Used](../Verification/LightingPack/ProfessionalAudit/After_Used.jpg)

Исходные фото-референсы не предоставлены; точное фото-сходство и AAA не подтверждены. Main Play/build/FPS, baked GI и gameplay/save-интеграция не проверялись. В отчёте отдельно отражены SearchDatabase exception редактора, приближённость коллизий и границы процедурных PBR/стекла. Пользовательские сцены, Packages и ProjectSettings не изменялись.
'''
(proof/'Project_Doc_Update.md').write_text(summary,encoding='utf-8')
readme=ASSET/'Documentation/README_RU.md';old=readme.read_text(encoding='utf-8');marker='## Актуальная профессиональная ревизия 25.10.2026'
if marker not in old:
    readme.write_text('# LightingPack — 100 префабов\n\n'+marker+f'\n\nВсе 100 префабов/FBX обновлены. GUID и {ids["localIDsPreserved"]} старых внутренних fileID сохранены. {materials} материалов, {textures} PNG 2K; Blender UV2, новые профили/отражатели/крепёж/декор. [Актуальный отчёт и сравнения](ProfessionalAudit/Report_RU.md). Фото-референсы не предоставлены; точное сходство/AAA не подтверждены.\n\nНиже сохранено руководство исходной версии: его прежние числа текстур/материалов и описание коллизий заменены актуальным отчётом. Не запускайте исторические сборщики поверх текущего Master.blend.\n\n---\n\n'+old,encoding='utf-8')
(proof/'meta_preservation.json').write_text(json.dumps(dict(passed=True,existingMetaGuidsPreserved=len(meta),materials=materials,textures=textures),indent=2),encoding='utf-8')
shutil.copy2(ASSET/'Editor/LightingPackProfessionalAudit.cs',ROOT/'Tools/LightingPack/LightingPackProfessionalAudit.cs')
print('PROFESSIONAL_REPORT_PASS',materials,textures,len(badbefore),len(play['checks']),len(edit['checks']))
