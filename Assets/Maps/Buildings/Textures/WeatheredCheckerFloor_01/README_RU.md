# Weathered Checker Floor 01 — грязная потёртая напольная плитка

Готовый материал M_WeatheredCheckerFloor_01_URP.mat находится в Assets/Maps/Buildings/Textures/WeatheredCheckerFloor_01. Назначьте его на поверхность пола или нужный слот Materials. Шейдер Universal Render Pipeline/Lit.

Рисунок по референсу: квадратные красно-коричневые и кремовые плитки, объединённые в одноцветные блоки 2 × 2; блоки чередуются шахматно. Один повтор содержит 8 × 8 отдельных плиток. Добавлены въевшаяся грязь, затёртая глазурь, пыль, небольшие трещины и сколы по краям. Крупных провалов и мусора нет.

Восемь PNG-карт 2048 × 2048:
- WCF01_BaseColor — цвет, sRGB.
- WCF01_Normal_OpenGL — tangent-space +Y нормали: швы, фаски, сколы и мелкая фактура; импорт Normal Map.
- WCF01_Height16 — 16-битная высота: плитки выше, швы ниже. Цвет плитки не определяет высоту.
- WCF01_AO — мягкое затенение швов и сколов.
- WCF01_Roughness — шероховатость загрязнённой керамики/затирки.
- WCF01_Metallic — чёрная карта, керамика неметаллическая.
- WCF01_MetallicSmoothness — URP: RGB = 0, A = 1 − Roughness.
- WCF01_ORM — дополнительная упаковка R = AO, G = Roughness, B = Metallic. Не заменяет MetallicSmoothness в URP Lit.

BaseColor, Normal, MetallicSmoothness, AO и Height подключены. Normal strength 1, параллакс 0,005. Repeat, mipmaps, Trilinear, anisotropy 8, импорт без потерь. Только цвет sRGB, карты данных linear.

Условный физический размер отдельной плитки — 15 × 15 см; один повтор около 1,2 × 1,2 м. Это допущение, не обмер фото. Если UV 0–1 покрывают весь блок пола 3 × 3 м, начальный Tiling = 2,5 × 2,5; для 3,4 × 3,4 м — примерно 2,83 × 2,83. При уже масштабированных UV подбирайте с учётом развёртки.

Рельеф швов и сколов около 3 мм — шейдерный Normal/Height, без изменения геометрии или коллайдеров. Для выбитых плиток и реальных провалов нужен отдельный меш. Сцены, префабы и существующие материалы не изменялись; материал на пол автоматически не назначался.

Происхождение: встроенный image_gen, цвет и согласованная высота по пользовательскому референсу. Исходные изображения 1254 × 1254, подготовлены пересчётом в 2048 × 2048. Нормали вычислены из высоты, roughness/AO оценены. Это художественный PBR-набор, не измеренный скан. Параметры и результаты проверки — Validation.json.

Промпты:
Built-in image_gen; floor photograph supplied by user as reference.

BASE COLOR
Use case: photorealistic-natural. Asset type: seamless PBR BASE COLOR / ALBEDO floor tile texture for realistic post-apocalyptic apartment stairwell. User reference defines layout/colors: small square muted terracotta reddish-brown and aged pale cream ceramic tiles. Colors form a checkerboard of TWO-BY-TWO same-color tile blocks, NOT individual alternating tile colors. Produce an exact orthographic square material tile, approximately 2048x2048, with exactly 8 by 8 equal square individual tiles and narrow dark gray-brown grout joints. Pattern rule: tile color = terracotta if (floor(column/2)+floor(row/2)) is even, otherwise cream. Thus each block is 2x2 individual tiles, blocks alternate red/cream across and down. Show visible grout between all individual tiles. Align grid perfectly horizontally and vertically with no perspective. Texture starts/ends halfway through the exterior grout lines, so it tiles seamlessly on all four edges. Photorealistic worn ceramic: faded patchy glazing, ingrained gray dust, dirty dark mortar, fine scratches, scuffed foot traffic, small edge chips, a few restrained hairline cracks, subtle irregular dried muddy staining and pale rubbed patches. Abandoned for several years, dirty and used but structurally intact; no big missing tiles, grass, debris, puddles, footprints shaped like shoes, objects, extreme destruction or centered hero stains. Keep original terracotta and ivory colors legible beneath wear. Neutral uniform DELIT diffuse albedo ONLY, absolutely no specular glare, directional lighting, cast shadows, baked AO, vignette or perspective. No text, labels, frames or watermark. One seamless tileable square texture, no montage.

HEIGHT
undefined
