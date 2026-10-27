import json,shutil
from catalog import *
load=lambda n:json.loads((VERIFY/n).read_text(encoding='utf-8'))
fbx=load('blender_reimport.json');imp=load('unity_import.json');play=load('unity_play_checks.json');cold=load('unity_cold.json');edit=load('unity_edit_checks.json')
manifest=json.loads((ASSET/'manifest.json').read_text(encoding='utf-8'))
assert fbx['count']==100 and imp['fixtures']==90 and play['passed'] and cold['passed'] and edit['passed']
render=[c for c in play['checks'] if 'native' in c or 'opaque mounting wall' in c]
text=f'''# Lighting Pack — отчёт о готовности

Дата завершения: 25.10.2026 (создание начато 24.10). Выполнен отдельный процедурный пакет для **Unity 6000.6.3f1 / URP 17.6.0**. Созданы файлы моделей, материалов, скриптов, независимых префабов и демонстрационной сцены. Художественная AAA/фотореалистичная приёмка не подтверждена.

## Результат

| Элемент | Фактический результат |
|---|---|
| Типы светильников | 30; уникальность геометрии Used проверена по координатам после повторного FBX-импорта |
| Состояния | 90: Used / Worn / Damaged для каждого типа |
| Геометрическое разрушение | Все 30 Damaged отличаются геометрией от Used; Used/Worn сохраняют форму и отличаются PBR-картами |
| Декор | 10 отдельных FBX и префабов |
| Формат | 100 настоящих FBX, экспорт Blender, не переименованные GLB |
| LOD | 3 уровня во всех 90 светильниках; LOD0 {min(f['triangles'][0] for f in manifest['fixtures'])}–{max(f['triangles'][0] for f in manifest['fixtures'])} треугольников |
| Материалы | 24 общих URP Lit материала, индивидуальные Emission через MaterialPropertyBlock |
| Текстуры | 169 PNG 2048²; metallic/smoothness упакованы для URP |
| Источники света | Каждый светильник имеет настоящий Spot/Point Light; {sum(len(f['lights']) for f in manifest['fixtures'])} компонентов во всех 90 префабах суммарно |
| Source | LightingPack_Master.blend с 90 high-poly заготовками и 270 LOD-сборками из отдельных частей; Manufacturing_NormalBake.blend |
| Demo | Пять упрощённых архитектурных зон, галерея 90 образцов, два независимых экземпляра, панель просмотра в Play |

Файлы установлены в `Assets/PostApocalypse/LightingPack`. Основные игровые сцены, Packages, ProjectSettings, игрок, экономика и форматы сохранений не редактировались этим заданием. Коммит и push не выполнялись.

## Фактически выполненные проверки

1. **Чистый Blender re-import: {fbx['count']}/100 PASS.** Геометрия, конечные координаты, UV, материалы, уменьшение числа треугольников во всех трёх LOD. Уникальные формы Used: {fbx['uniqueUsedGeometries']}/30. Файл: `Verification/LightingPack/blender_reimport.json`.
2. **Unity import: {imp['fixtures']}/90 светильников + {imp['decor']} декора PASS.** Префабы загружаются, нет Missing Script/Material в проверяемых светильниках, URP Lit, три убывающих LOD, источники и Emission bindings, размер в метрах, все 2K-текстуры. Файл: `unity_import.json`.
3. **Изолированный Play: {len(play['checks'])} адресных проверок PASS.** По всем 90: Normal, Intensity, Emission, питание/Off, восстановление заданной яркости, Weak, Failing с паузами/вспышками, Flickering, неизменность shared materials, disable/enable. По Spot: центральный луч выходит из собственного непрозрачного корпуса. Дополнительно: два экземпляра с разными цветами и яркостью, разные фазы, температура, переключение теней, distance culling и возврат, гул и выключение, фактическое продвижение мерцания через Play Update. Файл: `unity_play_checks.json`.
4. **Edit Mode: {len(edit['checks'])} проверок PASS.** Скриптовые изменения, синхронизация Light/Emission, независимость двух экземпляров, выключение/восстановление, Undo/Redo. Это адресные вызовы Editor API, не проверка OS-мыши. Файл: `unity_edit_checks.json`.
5. **Холодное повторное открытие: PASS.** Отдельный процесс той же Unity повторно загрузил 90 префабов и references после сохранения/импорта. Файл: `unity_cold.json`.
6. **Native URP render:**
'''+ '\n'.join('   - '+c for c in render)+'''

7. Сняты 90 кадров галереи в Unity, выключенный свет для оценки геометрии/материалов. Выборочные кадры и сводные листы визуально просмотрены. Это не подтверждение всех ракурсов и всех пересечений.
8. Архивы проверяются отдельно после экспорта; точные размеры, целостность ZIP и состав UnityPackage — `Verification/LightingPack/delivery.json`. Установленные файлы сверяются SHA-256 со staging и перечислены в `installed_hashes.json`.

В изолированном Play журнал содержит отдельное `ArgumentOutOfRangeException` из `UnityEditor.Search.SearchDatabase` при запуске индексирования. Стек не проходит через код пакета; проверки продолжились и завершились PASS. Также при автоматическом UV2 unwrap повреждённой подвесной лампы Unity выдавала предупреждение `Could not pack uvs`; после уменьшения отступов повторно проверены материалы и наличие UV2 во всех мешах. Качество упаковки UV2 и настоящий GI bake не подтверждены. Проверочная пустая сцена до создания камеры выдавала предупреждения об отсутствии AudioListener; в самой Showcase есть Camera/AudioListener. Это не полностью чистый журнал Editor, хотя ошибок компиляции C# в итоговых прогонах нет.

## Просмотр

![30 типов](../Verification/LightingPack/Preview_30_Types.jpg)

![Сравнение света](../Verification/LightingPack/Preview_Lighting_Comparison.jpg)

- [Три состояния, выборка](../Verification/LightingPack/Preview_Wear_States.jpg)
- [Демонстрационная сцена](../Verification/LightingPack/Unity_Showcase.png)
- [Пример Failing](../Verification/LightingPack/Unity_Failing.gif) — последовательность native-кадров с принудительно заданными отметками времени эффекта; не запись FPS реальной игровой сцены. Фактический Update отдельно проверен в Play.
- [Руководство](../Assets/PostApocalypse/LightingPack/Documentation/README_RU.md).

## Ограничения и художественный статус

Это процедурная интерпретация российских светильников с общими тайловыми PBR-поверхностями. Размеры НПП и линейных ДСП ориентированы на каталоги IEK; остальные габариты авторские. Нет точной реконструкции конкретных изделий, индивидуального фото-референса или фотосканов. Общий high→low normal bake описан в README; отдельный bake каждой из 90 моделей не выполнялся. Высокополигональные фаски сохранены в source для дальнейшей ручной доработки.

Разрушение статическое, схемы повреждений параметрически варьируются; полноценного симулируемого разрушения нет. Стекло URP упрощено, без физического преломления. Крепёж, кабельные вводы и геометрические повреждения присутствуют в зависимости от конструкции; отсутствие заметных пересечений по всем поверхностям/углам не доказано. Декор снятого светильника — тонкая геометрическая накладка старого покрытия с остатками крепежа/проводов, не native Decal Projector.

Центральные лучи Spot и один пример стены с включёнными тенями проверены. Это не полная матрица углов/всех стен. При выключенных тенях Unity lights могут проходить сквозь стены. Световые профили приблизительные, без IES/фотометрической калибровки. Bloom и baked GI не подменяются Emission.

Не проверены: main-project Play, standalone build, FPS большого мира, baked/Mixed light bake, HDRP/Built-in, OS-ввод в панели Demo, интеграция с электросетью/Interact/уроном/player-save/world-save. Baked/Mixed — доступная настройка источника; уже запечённый свет не регулируется автоматически в Play. Общие непрозрачные материалы допускают instancing; MPB Emission может исключать отдельные Renderer из URP batching, массовые draw calls не замерялись.

Проверочные сборщики ограничены `work/LightingPack/UnityProject` и не должны запускаться на основном checkout. Собственный pipeline Demo назначался только в изолированном проекте. Пользовательские сцены не открывались для проверки этого пакета.
'''
(ROOT/'Docs/LightingPack.md').write_text(text,encoding='utf-8')
preview=ASSET/'Documentation'/'Previews';preview.mkdir(exist_ok=True)
for name in ['Preview_30_Types.jpg','Preview_Wear_States.jpg','Preview_Lighting_Comparison.jpg','Unity_Showcase.png','Unity_Failing.gif']:
    shutil.copy2(VERIFY/name,preview/name)
local=text.replace('../Verification/LightingPack/','Previews/').replace('../Assets/PostApocalypse/LightingPack/Documentation/README_RU.md','README_RU.md')
(ASSET/'Documentation'/'Readiness_Report_RU.md').write_text(local,encoding='utf-8')
print('REPORT_PASS')
