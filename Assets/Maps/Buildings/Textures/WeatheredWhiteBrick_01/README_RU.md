# Weathered White Brick 01 — белый кирпич на основе красного

Папка: Assets/Maps/Buildings/Textures/WeatheredWhiteBrick_01.
Готовый материал для назначения на стены: M_WhiteBrick_World.mat.
Отдельная копия графа: SG_Brick_World_White.shadergraph.

Основой послужили текущие WBR01_BaseColor и SG_Brick_World из WeatheredRedBrick_01. Цвет кирпича изменён встроенным image_gen на состаренный светлый серо-кремовый по фото пользователя; зелёный оттенок от листвы/освещения референса не переносился. Сохраняются ряды кладки, размеры, расположение швов и общий износ исходной текстуры. Цвет пересчитан из результата 1254 × 1254 в 2048 × 2048; это не нативный 2K-скан. Совпадение профилей кладки с прежней высотой: корреляция строк 0,94, столбцов 0,97. Это проверка соответствия рисунка, не доказательство точного совпадения каждого пикселя после AI-перекраски.

Семь карт 2048 × 2048:
- WWB01_BaseColor.png — новый светлый кирпич, sRGB.
- WWB01_Normal_OpenGL.png — точная копия текущей карты нормалей красного кирпича.
- WWB01_Height16.png — точная копия текущей 16-битной высоты.
- WWB01_AO.png — копия прежнего AO.
- WWB01_Roughness.png — копия прежней шероховатости.
- WWB01_MetallicSmoothness.png — прежняя упаковка URP: RGB = металлическость, A = smoothness.
- WWB01_ORM.png — прежняя упаковка R = AO, G = roughness, B = metallic.

Настройки импорта всех карт перенесены через Unity AssetDatabase.CopyAsset с новыми GUID; файлы .meta оригиналов не копировались поверх. Все карты кроме BaseColor побайтно скопированы. Цвет подготовлен к повторению, просмотрен повтор 2 × 2.

SG_Brick_World_White — независимая копия графа. Сохранены узлы, связи, параметры трипланарной мировой проекции, обработка нормалей и остальные настройки. В тексте графа заменены только GUID текстур на новые копии; обратная замена GUID даёт исходный граф без отличий. У нового материала нет зависимостей от красных текстур в тех слотах, где они стояли у оригинала.

M_WhiteBrick_World создан как копия встроенного материала исходного SG_Brick_World, затем переключён на отдельный белый граф и соответствующие копии текстур. Сохранены все числовые/цветовые свойства и трансформации текстур; несовпадений при проверке нет. Brick_Tile = 0,6, Brick_Blend = 1, Brick_Smoothness_Mult = 0,5. Brick_Tile регулирует ту же мировую проекцию, что у красного кирпича. Не переносите сюда рекомендации Tiling 1,62 от старого обычного URP Lit автоматически: используется ваш текущий Shader Graph.

Для точного наследования оставлены и текущие назначения исходного графа: в Brick_AO стоит карта Roughness, Brick_Roughness не назначена. Это состояние существующего графа, а не новая настройка белого материала; оно намеренно не исправлялось без изменения заданного пользователем поведения. Все отдельные технические карты доступны в папке, если позднее потребуется перенастройка.

Проверено: граф импортирован без shader errors/messages; scalar/vector/texture-property comparison — без расхождений с исходным материалом, кроме ожидаемой замены текстур и шейдера. Проверены контрольные суммы 22 файлов папки красного кирпича: ни один не изменён. Сцены, префабы, Player и существующая расстановка не менялись. Материал автоматически на стены не назначался. Коммит и push не выполнялись. Прежнее изменение настроек ProBuilder оставлено без изменений.

Отчёты проверки рядом: ColorValidation.json, ShaderValidation.json, SourcePreservation.json.

Промпт встроенной генерации (изображение 1 — текущий красный BaseColor; изображение 2 — фото пользователя):
Use case: precise-object-edit. Image 1 is the EDIT TARGET: our existing square red-brick PBR albedo texture. Image 2 is only a COLOR/MATERIAL reference: pale white/light cream silicate bricks on a building. Change ONLY the brick material color of image 1 to realistic weathered pale off-white/light gray-cream silicate masonry matching image 2, without copying the green cast from foliage/sunlight. Crucially KEEP EVERY brick outline, row, mortar joint, chip, crack, pore and stain at EXACTLY THE SAME image coordinates as image 1. Do not redraw or move the masonry: this recolor must remain registered to existing normal/height maps. Preserve existing complete framing, same square layout, original mortar positions and original brick dimensions. Keep worn mineral surface and small chipped edges, muted dust and faint dirty grey weathering; pale bricks should still have slight natural color variation, not pure flat white paint, no flaking paint film. Mortar is slightly darker warm gray, discernible but not black. No red/orange bricks remain. No new large stains, moss patches, cracks, features, greenery, window, objects or shadows from image 2. DELIT neutral evenly illuminated albedo only, no directional highlights, reflections, ambient shading, vignette, perspective, border, text or labels. Preserve seamless left/right/top/bottom character and exact original grid. Output square 2048x2048 if possible.
