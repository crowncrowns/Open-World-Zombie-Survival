# Aged PVC Window 01 — оконная рама после трёх лет без ухода

Готовый материал M_AgedPVCWindow_01_URP.mat предназначен для белых пластиковых профилей окна. Перетащите его только на раму или в соответствующий слот Materials. Для стекла, уплотнителя, фурнитуры и металлического отлива используются отдельные материалы.

Внешний вид: слегка пожелтевший белый ПВХ, мелкие потёртости, пыль, сдержанные потёки и редкие следы плесени. Это художественная интерпретация трёх лет без ухода, не физическая симуляция старения в заданном климате.

Карты 2048 × 2048:
- PVC01_BaseColor.png — цвет, sRGB.
- PVC01_Normal_OpenGL.png — слабый микрорельеф, tangent-space +Y, импорт Normal Map.
- PVC01_Height16.png — 16-битная высота; очень слабый рельеф, приложена отдельно, параллакс выключен.
- PVC01_Roughness.png — матовость: пыльные участки более шероховатые, чистый пластик сохраняет мягкий сатиновый блеск.
- PVC01_Metallic.png — чёрная карта, ПВХ не металл.
- PVC01_AO.png — почти белая карта: грязные пятна не превращены в глубокие отверстия.
- PVC01_MetallicSmoothness.png — упаковка для URP Lit, RGB = 0, Alpha = 1 − Roughness.
- PVC01_ORM.png — дополнительная упаковка R = AO, G = Roughness, B = Metallic; не заменяет MetallicSmoothness в URP.

BaseColor, Normal, MetallicSmoothness и AO подключены к материалу. Все карты Repeat/Trilinear, mipmaps, anisotropy 8, 2048 px, импорт без потерь. Карты данных линейные, BaseColor sRGB. Normal strength 0,4. Эмиссия, прозрачность, параллакс и металлическость не нужны для профиля рамы.

Примерный масштаб: один повтор 0,25 × 0,25 м. Tiling зависит от UV конкретной модели; не ставьте универсальное число без проверки на раме. Текстура не содержит самой геометрии профиля или его края.

Создано встроенным image_gen. Исходные изображения 1254 × 1254, затем комплект пересчитан в 2048 × 2048; это не нативный 2K-скан. Нормали вычислены из согласованной AI-высоты (диапазон порядка 0,035 мм), roughness и AO оценены. Проверены согласованное разрешение, повторение и импорт. Параметры проверки — Validation.json.

Существующие материалы/текстуры и сцены не изменялись; новый материал автоматически на построенный дом не назначался.

Промпты:
Built-in image_gen; artistic three-year exposure interpretation.

BASE COLOR
Use case: photorealistic-natural. Asset type: seamless PBR BASE COLOR / ALBEDO material texture for white PVC plastic window frames, three years abandoned outdoors after an apocalypse, realistic Unity game. Generate only a flat square edge-to-edge surface texture, 2048x2048 if possible. Off-white ivory slightly yellowed uPVC, still recognizably white plastic, subtle sun-weathering, ingrained grey dust, restrained fine dirty rain streaks, tiny scuffs and hairline scratches, a few subtle grey/green mildew speckles. Three years of neglect, NOT decades of severe deterioration. Mostly intact smooth satin plastic. Dirt distributed subtly and irregularly, avoid large dramatic hero stains. Absolutely no rust, metal, wood grain, flaking paint, deep cracks, concrete, bricks, glass, window outlines, frame edges, rubber seals or hardware. This is a reusable material sample for the frame mesh, not an image of a whole window. Uniform neutral DELIT diffuse albedo, no directional lighting, specular highlights, shadows, AO, perspective or vignette. Seamless repeat on all four sides. No text, labels, logos, borders or watermark.

HEIGHT
Use case: precise-object-edit. Edit target: supplied off-white weathered PVC plastic texture. Generate a perfectly registered technical GRAYSCALE HEIGHT MAP of this exact surface with unchanged framing and pixel positions. The plastic is FLAT smooth PVC: almost uniform 50 percent mid-gray, with only extremely faint micro-scratches slightly darker and microscopic dust texture. Preserve scratch locations precisely. Dirt, yellow staining and mildew color spots are flat discoloration and must remain the same mid-gray height, NOT raised islands or holes. No pores like stone, no cracks, plaster, metal, dramatic relief or flaking. No lighting, shadows, reflections, text, panels or border. A clean near-flat grayscale data map for tiny plastic roughness, same size as input, seamless all edges.
