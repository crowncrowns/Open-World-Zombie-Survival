# Spalled Cornice Blocks 01 — разрушающаяся бетонная полоса у крыши

Материал M_SpalledCorniceBlocks_01_URP.mat находится в Assets/Maps/Buildings/Textures/SpalledCorniceBlocks_01. Шейдер Universal Render Pipeline/Lit. Назначается на бетонный пояс/лицевую часть карниза, не на кирпич или кровлю.

По референсу создана горизонтальная полоса восьми светлых бетонных блоков с вертикальными стыками. Износ усилен для постапокалиптического здания: глубокие сколы, трещины, повреждённые углы и местами выкрошившийся бетон с видимым крупным заполнителем. Арматуры, проволоки и металлической сетки нет. Повреждения заполнены более глубоким бетоном, материал непрозрачный.

Восемь PNG-карт 2048 × 256, формат узкой горизонтальной полосы:
- SCB01_BaseColor — светлый бетон, грязь, сколы; sRGB.
- SCB01_Normal_OpenGL — tangent-space +Y нормали, глубокие повреждения, импорт Normal Map.
- SCB01_Height16 — 16-битная высота: целые грани выше, сколы/стыки ниже.
- SCB01_AO — затемнение углублений и грубого заполнителя.
- SCB01_Roughness — высокая матовость бетона.
- SCB01_Metallic — чёрная карта, бетон неметаллический.
- SCB01_MetallicSmoothness — упаковка URP: RGB = 0, A = 1 − Roughness.
- SCB01_ORM — R = AO, G = Roughness, B = Metallic для других шейдеров; не заменяет MetallicSmoothness в URP.

Основные карты, включая Height, подключены. Normal strength 1, параллакс 0,04. Если на острых углах взгляда параллакс слишком заметно искажает поверхность, уменьшите его до 0,015–0,025. Карты данных linear, только цвет sRGB; mipmaps, Trilinear, anisotropy 8, без сжатия.

Wrap U = Repeat, Wrap V = Clamp: повтор по горизонтали, не по вертикали. Рабочий масштаб полосы — около 4 м в длину и 0,5 м в высоту, восемь блоков. Это условные игровые параметры, не обмер фото. Для лицевой полосы 4 × 0,5 м с UV 0–1 начальный Tiling = 1 × 1, для длины 8 м — 2 × 1. Под свою геометрию и ширину блоков скорректируйте X. По Y обычно оставляют 1. Если UV уже масштабированы, числа нужно адаптировать.

Диапазон сколов около 40 мм передаётся картами Normal/Height и параллаксом. Это не геометрическое разрушение: силуэт края, коллайдер и настоящие тени от обломанного блока не меняются. Для свисающих или отсутствующих кусков на краю и реалистичного бокового силуэта нужен отдельный повреждённый меш. Исходная форма карниза задаётся моделью; текстура не содержит ступенчатого профиля, кровли, водостока и тени от свеса.

Создано встроенным image_gen по пользовательскому фото. Цвет и высота художественные, нормали вычислены из высоты, AO/roughness оценены, не измеренный скан. Изображения исходно содержали пустые поля над и под полосой: обе карты обрезаны одинаково до рабочей области, затем подготовлены в 2048 × 256. Исходное разрешение и границы обрезки — Validation.json. Проверены повторение по X, нормали, подключения и настройки импорта.

Сцены, исходные меши, Player и прежние материалы не изменялись. Материал автоматически на здание не назначался.

Промпты:
Built-in image_gen; pale roofline concrete band in user photo used as reference.

BASE COLOR
Use case: photorealistic-natural. Asset type: PBR BASE COLOR / ALBEDO strip texture for old pale precast CONCRETE CORNICE/COPING BLOCKS just below the roof of an abandoned apartment building. User photo is only a design/material reference for the pale segmented concrete band, not its lighting or background. Generate an orthographic front-on horizontal material strip, landscape aspect 4:1 approximately 2048x512. One continuous row of eight chunky pale gray/off-white rectangular precast concrete blocks with straight narrow vertical dark mortar joints. Each block face is approximately 0.5m wide and 0.25m tall, strip represents 4m x0.25m but image is intentionally 4:1 and will map onto the narrow band. Blocks have intact broad flat faces interrupted by irregular deep spalling: chipped/receding corners, crumbling cement skin, open fractures, missing chunks exposing coarse gray aggregate, some dark dirt in cracks, weathered old chalky white surface remaining on most areas. Major damage on about 25 percent of the strip, asymmetrically distributed. Make convincingly post-apocalyptic deterioration, substantially deeper than floor wear, with stone aggregate embedded not floating. NO exposed rebar, steel mesh, wires, vegetation, loose rubble, complete gaps through the strip or transparent background. Damage must remain filled with rough deeper concrete so the whole image is usable as an opaque texture. DO NOT include the roof, brick wall, rain gutter, black eaves shadow, sky or architectural silhouettes. No decorative horizontal molding steps: one flat band of segmented concrete faces; actual cornice shape comes from the mesh. DELIT neutral even albedo, no directional shadows, cast eaves shadow, highlights, perspective or vignette. Seamlessly tileable LEFT/RIGHT with matching vertical joints at edges. Top/bottom are band edges, do not need to tile vertically. No text, labels, border or watermark.

HEIGHT
Use case: precise-object-edit. Edit target: supplied horizontal row of damaged pale concrete blocks with white empty padding above and below. Output a precisely registered GRAYSCALE HEIGHT MAP, same overall full framing, same exact position and dimensions of the central concrete strip and the eight blocks, every vertical joint, crack and damaged patch exactly aligned. Leave the empty top/bottom padding uniformly white; it will be cropped identically from both images later. Intact broad block faces are flat 75 percent gray, narrow vertical mortar joints are recessed 25 percent gray, deeper spalled patches 15-35 percent gray showing embedded coarse aggregate at intermediate heights BELOW the intact face. Broken edges have convincing depth, total recess about 40 mm. Do not invent holes through the strip, rebar, wire, hanging metal or floating stones. Color stains do not cause extra heights. These are technical geometry values, NOT an embossed-lit grayscale render: no directional shading, no cast shadows, highlights, perspective, vignette or text. Keep correct block outlines, flat face plateaus and graded broken concrete edges. Same left/right repeatable strip. Do not add any features to the white padding.
