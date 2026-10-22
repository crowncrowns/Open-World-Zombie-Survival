# Weathered Red Brick 01 — постапокалиптический кирпич

Готовый материал: M_WeatheredRedBrick_01_URP.mat (Universal Render Pipeline/Lit).
Перетащите материал на нужный блок или в Renderer → Materials. Сцены и существующие материалы не изменялись.

Карты, 2048 × 2048:
- WBR01_BaseColor.png — цвет, sRGB.
- WBR01_Normal_OpenGL.png — tangent-space +Y нормали, импортирована как Normal Map.
- WBR01_Height16.png — 16-битный PNG высоты, светлое выше; подключён к Height Map.
- WBR01_AO.png — затенение углублений, linear.
- WBR01_Roughness.png — отдельная шероховатость, linear; белое = шероховатое.
- WBR01_MetallicSmoothness.png — готовая упаковка для URP: RGB = 0 (неметалл), A = 1 − Roughness. Подключена к Metallic Map.
- WBR01_ORM.png — дополнительная упаковка для других шейдеров: R = AO, G = Roughness, B = Metallic. Не подставлять вместо MetallicSmoothness в URP Lit.

Все карты Repeat, mipmaps, Trilinear, anisotropy 8. Импорт без потерь; сжатие для целевой платформы можно включить позднее. Материал уже содержит цвет, normal, metallic/smoothness, AO и height. Emission не нужен, поверхность непрозрачна.

Ориентировочный физический размер одного повтора — 1,85 × 1,85 м. Если UV 0–1 занимает всю стену 3 × 3 м, начните с Tiling 1,62 × 1,62. Для стены 3,4 × 3 м — 1,84 × 1,62. Если в меше уже масштабированы UV, подберите Tiling с учётом этого масштаба.

Normal strength = 0,7; AO strength = 0,65; parallax = 0,0065. Если рельеф выглядит чрезмерным, уменьшите Normal strength до 0,4–0,5. Height в URP даёт параллакс, не меняет силуэт/коллайдер и не заменяет настоящую геометрию.

Происхождение: встроенный image_gen, без API/CLI генерации изображений. Исходный цвет и согласованная AI-карта высоты имеют разрешение 1254 × 1254. После общей обрезки по шву и подготовки повторения комплект пересчитан в 2048 × 2048; это не нативная 2K-фотограмметрия. Нормали численно вычислены из высоты с единым масштабом, AO и roughness оценены. Карты художественные, не измеренные сканом. Валидация и параметры — Validation.json. Краевые пиксели BaseColor совпадают по обеим осям; повтор 2 × 2 просмотрен.

Промпты генерации:

1. Use case: photorealistic-natural. Asset type: production seamless tileable PBR BASE COLOR / ALBEDO texture for a Unity game, square 2048x2048 if possible. Generate ONLY the flat base color texture, edge to edge, no montage or labels. Subject: aged mid-20th-century red clay brick masonry on an abandoned post-apocalyptic Russian residential building. Running bond, regular horizontally laid individual bricks, about 8 brick lengths across and 26 courses vertically (physical tile approximately 2.08 metres square; bricks 250x65 mm with mortar joints). Photorealistic tactile variation: faded reddish brown and russet clay, chipped edges, occasional small spalls and hairline cracks, eroded pale grey gritty recessed mortar, subtle soot and mineral staining, slight moss specks distributed sparsely in damp mortar. Strong convincing aged material, structurally intact masonry, no large holes. Neutral evenly illuminated DELIT albedo: absolutely no directional lighting, specular highlights, cast shadows, ambient-occlusion shadows or gradients, no vignette, no perspective. Perfectly orthographic front-on. Texture must tile seamlessly on all four edges, consistent brick grid and weathering without conspicuous hero stains. No floor, sky, frame, text, graffiti, objects or watermark.

2. Use case: precise-object-edit. Edit target: provided brick wall albedo texture. Produce a single precisely registered GRAYSCALE HEIGHT / DISPLACEMENT MAP of this exact image. Keep all brick outlines, mortar boundaries, chips and cracks at the EXACT same pixel positions and preserve the image size and entire framing. No aesthetic changes, no shifted or added bricks. White/light gray is raised brick face (around 75% gray), dark gray is recessed mortar (around 25% gray), cracks and spalls slightly depressed. Different brick colors must NOT change their large-scale height: dark colored bricks are still raised, pale mortar is recessed despite being bright in the source. Only shallow realistic pores on brick faces. No lighting, shadows, directional gradients, metallic reflections, normal-map colors, text, legend or panels. Full image grayscale technical height data, no border.
