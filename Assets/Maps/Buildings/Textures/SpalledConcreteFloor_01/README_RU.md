# Spalled Concrete Floor 01 — потёртый бетонный пол со сколами

Материал M_SpalledConcreteFloor_01_URP.mat находится в Assets/Maps/Buildings/Textures/SpalledConcreteFloor_01. Назначьте его на пол или нужный слот Materials. Universal Render Pipeline/Lit.

Серый старый бетон/цементная поверхность: затёртые участки, въевшаяся грязь, пыль, тонкие трещинки и местные неглубокие сколы верхнего слоя. В сколах видны песок и мелкий заполнитель. Арматуры, сетки, проволоки, глубоких дыр и торчащих обломков нет. Пол визуально остаётся пригодным для прохода.

Восемь PNG 2048 × 2048:
- SCF01_BaseColor — цвет, sRGB.
- SCF01_Normal_OpenGL — tangent-space +Y нормали для мелких сколов, пор и трещинок, импорт Normal Map.
- SCF01_Height16 — 16-битная высота; сколы ниже основной поверхности.
- SCF01_AO — мягкое затенение углублений.
- SCF01_Roughness — матовая шероховатость бетона.
- SCF01_Metallic — чёрная карта, бетон неметаллический.
- SCF01_MetallicSmoothness — упаковка URP: RGB = 0, Alpha = 1 − Roughness.
- SCF01_ORM — R = AO, G = Roughness, B = Metallic для других шейдеров, не заменяет MetallicSmoothness в URP.

Основные карты подключены, включая Height. Normal strength 1, параллакс 0,005. Repeat, mipmaps, Trilinear, anisotropy 8, импорт без потерь. Цвет sRGB, карты данных linear.

Один повтор соответствует примерно 2 × 2 м, диапазон неглубокого рельефа около 5 мм. Это авторские игровые параметры, не строительные измерения. Для блока пола 3 × 3 м с UV 0–1 начальный Tiling = 1,5 × 1,5; для 3,4 × 3,4 м — 1,7 × 1,7. Если UV уже масштабированы, учитывайте их масштаб.

Normal/Height создают видимый рельеф, а не реальные ямы и препятствия: геометрия, силуэт и коллайдеры пола не меняются. Материал автоматически на сцену не назначался; существующие сцены, префабы и материалы не изменялись.

Встроенный image_gen создал цвет и согласованную карту высоты; нормали вычислены из высоты, roughness/AO оценены. Исходные изображения 1254 × 1254, комплект подготовлен пересчётом в 2048 × 2048. Это художественная PBR-текстура, не измеренный скан. Проверки краёв, нормалей и параметры — Validation.json.

Промпты:
Built-in image_gen; no input image. Intended use: interior stairwell concrete floor, no reinforcement.

BASE COLOR
Use case: photorealistic-natural. Asset type: seamless PBR BASE COLOR / ALBEDO texture for a worn concrete FLOOR inside a post-apocalyptic apartment stairwell, square 2048x2048 if possible. Straight orthographic top-down surface-only view. A continuous old gray cement screed/concrete floor, worn smooth in places by foot traffic, dusty charcoal-gray dirt, restrained dried muddy staining, fine hairline cracks, small irregular shallow spalled patches where the top cement skin has flaked off and exposes fine sand and small aggregate beneath. About 15 percent shallow spalling, distributed irregularly, not a grid. The surface remains walkable and broadly flat: shallow millimetre-scale damage, NO large holes, deep craters, big broken slabs, loose rubble piles, protruding rocks or obstacles. ABSOLUTELY NO REBAR, metal mesh, wires, rods, screws, pipes or exposed reinforcement of any kind. No ceramic tile grid, decorative pattern, floor joints or architectural edges. Neutral cool-to-warm medium gray, realistic mineral detail, not bright white or almost black, not glossy wet, not new clean concrete. All four edges seamlessly tileable, no conspicuous centered hero damage. DELIT neutral uniform base color: no directional lighting, cast shadows, baked AO, highlights, perspective or vignette. Surface depth and shading will be represented by separate technical maps. No text, labels, frame, objects, vegetation or watermark.

HEIGHT
Use case: precise-object-edit. Edit target: supplied worn concrete floor surface. Generate ONLY a precisely pixel-registered GRAYSCALE HEIGHT map, same full framing and same locations of shallow spalled patches, cracks and aggregate. Intact cement floor is an evenly flat raised plateau at 70 percent gray, the shallow spalled patches are slightly lower at 35-45 percent gray with fine small aggregate embedded in them, never protruding above intact floor. Tiny cracks slightly recessed, fine pores very subtle. Total depth only a few millimetres. Color stains, dust and bright aggregate colors must not cause hills or deep pits. No major craters, broken slabs, holes, rebar, metal mesh, wires or objects. Uniform flat height plateaus with shallow graded broken edges. No lighting, highlights, directional shadows, embossed-render shading, text, border or vignette. Seamless height data on all four edges.

## Мировые координаты
Назначайте M_SpalledConcreteFloor_World.mat. Отдельный SG_ConcreteFloor_World.shadergraph основан на SG_Brick_World с прежней мировой трипланарной проекцией; исходный граф не изменён. Подключены существующие SCF01_BaseColor/Normal_OpenGL/AO/Roughness/Metallic, без дублирования текстур.
Brick_Tile=0.5: один повтор занимает примерно 2×2 метра независимо от UV и масштаба объекта. Увеличение значения уменьшает рисунок. Brick_Blend=1, Brick_Smoothness_Mult=1. Рисунок остаётся привязанным к миру при перемещении объекта.
В исходном графе нет Height/Parallax: мировая версия использует Normal/AO для сколов. Старый URP Lit материал с параллаксом сохранён. Все 5 карт проверены, ошибок шейдера нет; сцены и назначения материалов не изменялись, Play не запускался. Внешний вид на конкретных плитах следует проверить в сцене.
