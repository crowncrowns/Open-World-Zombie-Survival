# Weathered Entrance Door 01 — пошарпанная дверь подъезда

Материал: M_WeatheredEntranceDoor_01_URP.mat, Universal Render Pipeline/Lit.
Папка: Assets/Maps/Buildings/Textures/WeatheredEntranceDoor_01.

По пользовательскому референсу сохранены простое плоское полотно и приглушённая красно-коричневая краска. Ручка и её видимая светлая накладка удалены из изображения. Добавлены потёртости, царапины, локальные сколы и умеренная коррозия, сильнее по краям и снизу. Внешний вид художественный, не обмер и не скан реальной двери.

Это изображение ОДНОГО ЦЕЛОГО дверного полотна, не бесшовная повторяющаяся поверхность. Разрешение готовых карт 1024 × 2048. Ориентируйте UV передней стороны двери вертикально в диапазоне 0–1. Tiling = 1 × 1, Offset = 0 × 0. Импорт Clamp предотвращает повторение изношенного нижнего края. Если модель использует UV-атлас или UV выходят за 0–1, потребуется отдельная корректировка развёртки полотна. На боковых гранях при Clamp может растягиваться край; для них подходит отдельный однотонный материал.

Назначьте материал на полотно, не на раму/стену. Сама существующая 3D-ручка от назначения текстуры НЕ исчезает: здесь ручка удалена только из изображения. Геометрия, сцены и префабы в этой работе не изменялись.

Карты:
- WED01_BaseColor.png — цвет, sRGB.
- WED01_Normal_OpenGL.png — tangent-space +Y нормали, импорт Normal Map.
- WED01_Height16.png — 16-битная высота, неглубокие сколы/царапины; приложена отдельно, параллакс отключён.
- WED01_AO.png — мягкие углубления, linear.
- WED01_Roughness.png — шероховатость; краска и ржавчина матовые, открытая сталь менее шероховатая.
- WED01_Metallic.png — оценочная маска открытой стали; окрашенные и ржавые области не металлические.
- WED01_MetallicSmoothness.png — URP: RGB = Metallic, A = 1 − Roughness; подключена к материалу.
- WED01_ORM.png — R = AO, G = Roughness, B = Metallic для других шейдеров; не заменяет MetallicSmoothness в URP.

Материал уже содержит цвет, normal, metallic/smoothness и AO. Normal strength 0,5. Mipmaps, Trilinear, anisotropy 8, без сжатия. Все технические карты linear; только BaseColor sRGB. Для целевой платформы сжатие можно включить отдельно.

Встроенный image_gen создал цвет и согласованную карту высоты; итог подготовлен в 1024 × 2048. Нормали численно вычислены из высоты с условным диапазоном рельефа 0,6 мм для полотна около 1 × 2,1 м, roughness/AO/metallic оценены. Это не измеренные физические карты. Проверки и исходное разрешение — Validation.json.

Промпты:
Built-in image_gen, user door photograph as edit target.

BASE COLOR
Use case: precise-object-edit. Edit target: user supplied photograph of a tall reddish-brown painted steel entrance door. Create a production BASE COLOR / ALBEDO texture of the exact door leaf, straight-on orthographic, completely filling a tall portrait canvas approximately 1024x2048, aspect ratio 1:2. Keep the recognizable deep muted burgundy/reddish brown paint and simple featureless steel door design. Make it convincingly worn after several years of post-apocalyptic abandonment: rubbed and faded paint, varied fine scratches, boot scuffs concentrated low down, irregular restrained paint chips exposing dull dark steel, subtle dark brown corrosion along edges and bottom, light dirty rain streaks, small worn patches around the former handle position. Keep it structurally intact, realistic, restrained, not a wreck. REMOVE THE HANDLE and ALL attached hardware, including the small silver plate visible on the right, lock escutcheon, keypad, screws, hinges or protrusions: leave only worn painted sheet steel, no remaining white rectangle, no fake handle-shaped drawing. No peephole, lettering, number, logo, bullet holes or graffiti. The original door's small silver object is hardware to remove, not a decorative sign to preserve. Crop away surrounding frame, masonry and background: entire image is door-leaf surface, no exterior border. Neutral evenly lit DELIT albedo, no cast shadows, highlights, perspective, vignette or baked directional lighting. This texture covers one complete door leaf, NOT a repeating square material, natural dirt distribution from top to bottom. High resolution photographic material detail. No collage or labels.

HEIGHT
Use case: precise-object-edit. Edit target: supplied portrait burgundy steel door-leaf albedo. Output only a precisely registered grayscale technical HEIGHT map, same full portrait framing, every scratch and paint-chip location preserved. Most painted metal is perfectly flat 50 percent gray. Fine scratches and paint chips subtly recessed, shallow rust scale slightly raised, microscopic scuffs. No door handle, hardware or new marks. Red paint darkness and dirt stains must NOT turn into deep dents or hills. No broad raised door border or bevel, no shadows, light, reflections, vignette or text. Extremely shallow submillimetre relief, realistic intact painted steel. Grayscale height data only.
