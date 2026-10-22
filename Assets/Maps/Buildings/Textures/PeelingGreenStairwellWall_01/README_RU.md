# Peeling Green Stairwell Wall 01 — зелёная краска и белая шпаклёвка

Материал: M_PeelingGreenStairwellWall_01_URP.mat, Universal Render Pipeline/Lit.
Папка: Assets/Maps/Buildings/Textures/PeelingGreenStairwellWall_01.

Белая верхняя половина — грязная матовая шпаклёвка/тонкий минеральный слой. Зелёная нижняя половина — старая краска, местами отслоившаяся с обнажением той же белой основы. Добавлены мелкие трещинки, потёртости, пыль и следы стока. Полноценных дыр в стене и прозрачных участков нет: отслаивается только тонкое покрытие.

Восемь PNG 2048 × 2048:
- PGW01_BaseColor — цвет, sRGB.
- PGW01_Normal_OpenGL — tangent-space +Y нормали: края краски, сколы и фактура шпаклёвки; импорт Normal Map.
- PGW01_Height16 — 16-битная высота, тонкий слой краски слегка выше основы.
- PGW01_AO — мягкое затенение края отслоений и мелких углублений.
- PGW01_Roughness — зелёная краска немного более гладкая, белая шпаклёвка матовая; грязь повышает шероховатость.
- PGW01_Metallic — чёрная карта, покрытие неметаллическое.
- PGW01_MetallicSmoothness — URP: RGB = 0, A = 1 − Roughness.
- PGW01_ORM — R = AO, G = Roughness, B = Metallic для других шейдеров; не заменяет MetallicSmoothness в URP Lit.

Основные карты, включая Height, подключены. Normal strength 0,8, параллакс 0,005. Рельеф порядка 0,8 мм — игровое художественное приближение. Карты данных linear, BaseColor sRGB, mipmaps, Trilinear, anisotropy 8, без сжатия.

ВАЖНО: повтор только по горизонтали. Текстуры импортированы Wrap U = Repeat, Wrap V = Clamp. Верх и низ не должны совпадать, иначе двухцветная стена превратится в повторяющиеся полосы. Граница цветов проходит посередине изображения. При UV 0–1 на стене 3 × 3 м начальный Tiling 1 × 1 даёт зелёную краску до 1,5 м. Для стены 6 × 3 м при UV 0–1 — Tiling 2 × 1. Высота и ширина здесь — рабочее допущение, не обмер фото. По Y оставляйте 1; для изменения уровня границы лучше адаптировать UV/раздельные материалы.

Материал рассчитан на вертикальную развёртку: белое сверху, зелёное снизу. Если ваш меш имеет повёрнутые UV, ориентацию нужно исправить на меше. Автоматически сцены, префабы, исходники мешей и старые материалы не изменялись; материал на дом не назначался.

Normal/Height меняют отображение поверхности, а не силуэт и коллайдер стены. Для настоящих свисающих крупных хлопьев краски нужна отдельная геометрия.

Встроенный image_gen создал цвет и согласованную AI-высоту, нормали вычислены из высоты, AO и roughness оценены. Исходные изображения 1254 × 1254, итог подготовлен пересчётом в 2048 × 2048. Это не измеренный скан. Проверки горизонтальных краёв и нормалей — Validation.json.

Промпты:
Built-in image_gen; user stairwell photograph as reference, only wall finish used.

BASE COLOR
Use case: photorealistic-natural. Asset type: production PBR BASE COLOR / ALBEDO wall texture for a realistic post-apocalyptic Russian apartment stairwell. Reference image defines the traditional TWO-TONE wall finish only: green oil/enamel paint on the LOWER HALF, white/off-white plaster skim coat/spackling on the UPPER HALF. Generate a flat orthographic square material texture approximately 2048x2048, edge-to-edge wall surface only. A perfectly horizontal paint boundary exactly at 50 percent image height. Lower 50 percent is aged medium leafy emerald-green paint, muted by wear but still clearly green, NOT turquoise or pale mint. Upper 50 percent is dull warm off-white unpainted skim plaster/spackle. Lower green paint is locally peeling and chipped, irregular small and medium missing paint patches revealing the SAME off-white skim plaster beneath, fine lifted paint edges, minor blisters, scratches, wall rub marks, ingrained gray dust and subdued dirty drip streaks. About 15-20 percent of lower green area has lost paint, scattered naturally, mostly green remains. Upper white plaster has subtle hand-troweled mineral texture, a few fine hairline cracks and gray grime, no green flakes above the boundary. Realistic neglect for several years, no huge wall holes, exposed brick or structural damage. No mold takeover, no debris. Color/albedo must be neutral evenly lit and DELIT: no gloss highlights, cast shadows, directional lighting, AO shadows, vignette or perspective. Paint versus plaster gloss will be encoded separately. Horizontally seamless LEFT/RIGHT edges, identical paint boundary height at both sides. Do NOT make top and bottom match: white above and green below are intentional. No rails, stairs, windows, doors, skirting, floor, ceiling, objects, text, numbers, graffiti, border or watermark. Realistic material scan appearance, not an illustration.

HEIGHT
undefined
