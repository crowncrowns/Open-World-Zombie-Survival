# ТЦ «Курс»: качество и перенос, ревизия 7

Проверки 20.10.2026: реальные Blender source/FBX, целевой Unity 6000.6.3f1 / URP 17.6.0, проход FirstPersonController и native prefab. Итоговые доказательства/хеши — Verification/KursHub/QualityAudit7 и FINAL_ASSETS/REPORTS. Художественное сходство показано на REFERENCE | BEFORE | AFTER; технический PASS не означает фототождественность или оценку AAA.

## Пути и сохранённые контракты

Canonical Assets/Environment/KursHub/KursHub.prefab остаётся единым переносимым зданием. Shell — Models/KursHubShell.fbx, дистанционные модели Models/LOD/KursHubShell_LOD1/2.fbx. Props/Prefabs содержит 55 отдельных reusable prefab: сохранены все прежние 53 ID, добавлены KH_WeaponsDisplay и KH_AtriumFir. Props/Models/LOD содержит независимые FBX уровней 1/2. Настоящие FBX экспортированы Blender, native prefab созданы Unity Editor API.

Sources/KursHub/KursHub.blend и KursHubProps.blend остаются вне Assets. В комплекте FINAL_ASSETS/BLENDER они продублированы как Buildings_Final.blend и Props_Final.blend с упакованными картами. 3480 прежних mesh корпуса и 1535 прежних solids предметов сохранены; итог — 4646/2848 mesh. Все 735 прежних ID экземпляров сохранены; всего 838 (144 наружных +130 архитектурных +564 поселения). 55 источников света поселения, без отдельного света на каждую лампочку.

Наружные стены 93×130 м; 93 м по главному фасаду остаётся рабочим допущением. Два этажа 0/5,5 м, крыша 11 м, атриум 30×50 м, закруглённый вход. Третий этаж из противоречивых концептов не добавлялся. Козырёк удалён по решению пользователя, грузовой проём отложен. Правая сторона без отдельного референса помечена right_assumed. CharacterTest/SampleScene/gameplay/vendor и глобальный свет не перестраивать ради preview.

## Геометрия и поверхности

94 оконных секции дополнены настоящими откосами, прижимами/переплётами, отливами и капельниками; добавлены парапетные накладки, водосточные узлы и ручки. 1211 selective bevel элементов; ходовые плоскости не сглаживаются фаской. 41 открытый текстовый mesh исправлен сваркой 0,01 мм и восстановлением 37 вырожденных cap triangles. Не возвращать старую топологию текста и не обходить её двухсторонним материалом.

163 props FBX +3 shell FBX проходят строгий независимый импорт: замкнутость, нормали, положительный объём каждого связного тела, UV, размер, материал, имена и уменьшающиеся LOD. Корпус 350372/198244/108188 tris; библиотека предметов 242870/148802/84638. KH_GrimePatch (12 tris) оставлен без бессмысленного LOD. Исходные имена FBX частей и старые native IDs сохраняются.

26 PBR-семейств/182 PNG плюс прежняя alpha-декаль. BaseColor sRGB; tangent+Y Normal; scalar Roughness/Metallic/AO; Height16; packed R=metallic/A=smoothness. Это авторские процедурные карты, не фотосканы. AO локальный, не bake здания; Height не включает runtime parallax. Керамика 4K: 8×8 м, 16×16 плиток по 0,5 м; 83 красно-коричневые/173 светлые. Верх полов отдельный CeramicTile, потолки/торцы нейтральные Floor; определять по TransformPoint Y=0/5,5, не по сырым FBX осям. Пять InteriorMeshes и их .meta сохранять. Деревянная V UV вдоль детали. FirNeedles — alpha-test 0,4, ZWrite, coverage mipmaps; не переводить в прозрачное стекло.

Ткань/мешки/ковры получили мягкие нормали, крупная печатная клетка убрана. Краска на семи вывесках повторяет реальные складки, толщина 0,3 мм; номинальный зазор 1 мм увеличивается по измеренным пересечениям с грубыми гранями ткани (до 17 мм у отдельных букв). Небезопасные reduced glyph сохраняют предыдущую проверенную пару ткани/краски, остальные крепления продолжают упрощаться. Не возвращать толстые парящие буквы. Ёлка 3,35 м — статический отдельный prop, не NPC/скриптовая система.

## Регенерация

Сначала сохранять ручные изменения, source и GUID. Старый полный build_kurs_hub.py генерирует прежние этапы и не является безопасной командой поверх исправленного source. Quality scripts запускаются в отдельном staging --root, а не поверх принятой модели.

Tools/KursHub/audit7_architecture.py +audit7_text_geometry.py выполняют архитектурный pass. audit7_geometry.py строит props из архивного baseline, вызывает guarded LOD export. audit7_verify_props.py и audit7_repair_lods.py сохраняют только корректные reduced chunks; отсутствие ошибок source не доказывает безопасную reduction. audit7_soft_shading.py сохраняет геометрию/UV/ID при изменении нормалей. audit7_printed_cloth.py имеет собственный before snapshot и повторяет проверенный paint pass; audit7_verify_sources.py заново читает оба итоговых blend.

audit7_surfaces.py /leaf_surface/fir_surface создают семейства; audit7_materials.py подключает их. audit7_layout/exterior/atmosphere/extra_merge/integrate.py обновляют native расстановку с сохранением ID. Для нового выпуска обязательны повторный geometry/source/PBR readback, целевой импорт/маршрут и fresh package import. Скрипты проверки игры создают временные C# только в work/KursHub/UnityProject — не переносить их в продукт.

## Unity Editor API и коллизии

KursHubSetup/PropSetup/SurfaceSetup/InteriorFinishSetup/QualitySetup — авторские Editor скрипты. UV2 importer: generateSecondaryUV=true, secondaryUVMarginMethod=Manual, secondaryUVPackMargin=1. Calculate игнорировал поле margin и мог импортировать пустой frame mesh. Не менять без проверки реального vertex count.

Quality LOD1/2 у props и Quality Building LOD1/2 у здания содержат визуальные модели. Корневой LODGroup владеет только Shell, не props/светом/постоянными коллайдерами. Props/KursHubColliderManifest.json содержит per-chunk OBB доказательство. Только allBoxes=true, remainingSolids=0, 1..12 boxes допускают замену активного MC; импортированные вершины проверяются внутри OBB с допуском 2 мм. Исходный MC остаётся отключённым с прежним fileID. Остальные части сохраняют точную коллизию; не закрывать портал общим bounds.

Материалы/свет мигрируются только с распознанных прежних generated значений; пользовательские material edits и сценовые overrides не сбрасывать. Текущая CharacterTest сохранена без перезаписи, все 80 внешних targets проверены фактическим AssetDatabase localID. Среди них есть virtual nested-prefab IDs: отсутствие отдельного YAML документа не означает потерю native объекта. Не заменять проверку blanket ignore.

Меню Tools/Character Lab: Audit Kurs Hub Quality, Create Kurs Hub Standalone Demo, Export Kurs Hub Quality Package. KURS_FINAL_OUTPUT задаёт каталог выпуска. Demo — Assets/Environment/KursHub/Scenes/KursHubDemo.unity, без игрового контроллера. Package включает только KursHub и разрешённые URP/ShaderGraph/builtin зависимости. Для переноса использовать проверенный package либо loose UNITY с .meta; не импортировать одновременно дубли одинаковых GUID.

## Проверка и ограничения

Реальный FirstPersonController/Input System прошёл подход/вход в обе стороны, обе рамки КПП (после сдвига на 13,5 м к атриуму), рынок/очаг, обе главные лестницы, ночлеги у перил/оба задних переулка, U-лестницу/крышу. Выполнены push проверки стен/стекла/краёв и standalone проходов. Две сборки подряд сохраняют старые IDs. Повторный импорт финального package проверяет 56 prefab, модели/LOD/коллизии/карты/материалы/освещение demo. Нет Missing Mesh/Material/Shader/Texture в проверенной поставке.

Ручная проверка: открыть текущую CharacterTest, пройти тем же маршрутом, осмотреть керамику, складки и краску вблизи, дерево/рамы при дневном и скользящем свете; проверить независимое размещение props. Сравнительные листы — реальные Unity изображения, перспективы референсов приблизительны. Повторяемость части модульного ассортимента и интерпретированные скрытые стороны отмечены в отчёте.

NPC, торговля, досмотр, safezone, NavMesh, населённый FPS и финальный bake не реализованы/не проверены этим ассетом. Две прежние ошибки общего WorldSaveRegistry на ZombieMaleActor(1)/(2) с дублирующимся SaveIdentity не исправлялись и не скрывались; нельзя считать общий реестр прошедшим. Резервная копия до ревизии — work/KursHub/audit7/baseline; не восстанавливать оттуда сцену поверх новых правок пользователя. Git commit/push без запроса не выполняется.
