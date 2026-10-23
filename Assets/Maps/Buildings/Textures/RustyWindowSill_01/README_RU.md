# Rusty Window Sill 01 — тёмный ржавый оконный отлив

Готовый материал: M_RustyWindowSill_01_URP.mat, шейдер Universal Render Pipeline/Lit.
Перетащите его на металлическую часть отлива или в соответствующий слот Materials её Renderer. Если окно и отлив объединены в один меш, выберите именно слот отлива: назначение на весь объект может заменить материал окна/стены.

Комплект находится в Assets/Maps/Buildings/Textures/RustyWindowSill_01.
Восемь карт PNG, 2048 × 2048:
- RWS01_BaseColor — тёмная выцветшая краска, приглушённая ржавчина, потёртости; sRGB.
- RWS01_Normal_OpenGL — tangent-space +Y нормали, импорт Normal Map.
- RWS01_Height16 — 16-битная карта очень мелкого рельефа.
- RWS01_Roughness — шероховатость, белое = матовое.
- RWS01_Metallic — оценочная маска открытого металла; краска и ржавчина неметаллические.
- RWS01_AO — мягкое затенение мелких углублений.
- RWS01_MetallicSmoothness — готовая упаковка URP: RGB = Metallic, Alpha = 1 − Roughness.
- RWS01_ORM — дополнительная упаковка R = AO, G = Roughness, B = Metallic для других шейдеров. Не назначать вместо MetallicSmoothness в URP Lit.

Материал уже содержит BaseColor, Normal, Metallic/Smoothness и AO. Height приложена отдельно, параллакс выключен: у листового металла рельеф очень мелкий, его передают нормали. Форма отлива, загнутый край и уклон задаются мешем, не текстурой. Текстура не содержит окна, кирпича или геометрических кромок, поэтому пригодна для разных отливов.

Repeat, mipmaps, Trilinear, anisotropy 8; все карты данных linear, только BaseColor sRGB. Normal strength 0,55. Начальный физический размер повтора около 0,5 × 0,5 м; при UV 0–1 на отливе 1,2 × 0,2 м можно начать с Tiling 2,4 × 0,4. Фактический результат зависит от UV вашего меша.

Референс пользователя использован для характера материала: очень тёмный состаренный металлический отлив под белым окном. Степень коррозии интерпретирована художественно. Сгенерировано встроенным image_gen, без API/CLI генерации. Оригиналы 1254 × 1254, комплект подготовлен пересчётом в 2048 × 2048. Цвет и высота созданы ИИ; нормали вычислены из высоты, AO/roughness/metallic оценены, не измерены сканированием. Технический диапазон рельефа около 0,6 мм, без изменения геометрии и коллайдеров.

Проверено: размеры всех карт, совпадение краёв цвета, повторение 2 × 2, корректность единичных нормалей и настроек импорта, привязки материала. Подробности — Validation.json. Прежние текстуры и материалы не изменялись, на объекты сцены автоматически ничего не назначалось.

Промпты генерации:
Built-in image_gen. Reference: user-supplied photo, dark flashing below window only.

BASE COLOR
Use case: photorealistic-natural. Asset type: seamless PBR BASE COLOR / ALBEDO texture for a narrow exterior sheet-metal window sill flashing in a realistic post-apocalyptic Unity game. Reference image: use ONLY the dark aged metal sill immediately below the white window for material character; do not include the window, brick, background or geometry. Generate a flat square 2048x2048 material texture if possible, orthographic close surface view, uniform evenly diffuse DELIT illumination, no highlights, directional light, AO shadows, perspective or vignette. Very dark charcoal-black weathered steel with remnants of old dark paint, sparse exposed dull gunmetal, irregular subdued brown/russet rust patches and thin scuffed edges of paint chips, shallow corrosion pitting, fine lengthwise scratches, subtle old water/dirt marks. About 75 percent dark aged coating, 20 percent dark muted rust, 5 percent exposed steel. Keep rust restrained and believable, not bright orange, not a new clean metal, not thick crumbling bark or flaking chunks. Broad flat sheet without boards, bricks, seams, borders, fasteners, bent edges or manufactured object silhouettes. No baked reflection or shadow. Subtle horizontal grain, continuous seamless repeat on all four sides, no large memorable centered stain, no text, logos or watermark.

HEIGHT
undefined
