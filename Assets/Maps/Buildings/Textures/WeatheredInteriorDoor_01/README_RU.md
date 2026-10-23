# Weathered Interior Door 01 — обшарпанная филёнчатая дверь

Материал M_WeatheredInteriorDoor_01_URP.mat находится в Assets/Maps/Buildings/Textures/WeatheredInteriorDoor_01. Шейдер Universal Render Pipeline/Lit, готов к назначению на дверное полотно.

По фото сохранены четыре прямоугольные филёнки: две высокие сверху и две короткие снизу, центральная вертикальная стойка и широкая поперечина. Перспектива выпрямлена. Белая краска потускнела, слегка пожелтела; добавлены пыль в стыках, потёртости, небольшие сколы с видимой древесиной. Ручка и отверстие под неё отсутствуют в изображении. Окружающие наличники, стены и пол исключены.

Восемь карт 1024 × 2048:
- WID01_BaseColor.png — цвет, sRGB.
- WID01_Normal_OpenGL.png — tangent-space +Y нормали с рельефом филёнок и слабой микрофактурой, импорт Normal Map.
- WID01_Height16.png — 16-битная высота: светлые стойки/перемычки приподняты, тёмные центры панелей утоплены, фаски имеют плавные переходы.
- WID01_AO.png — мягкое затемнение углублений, linear.
- WID01_Roughness.png — матовость старой краски/пыли.
- WID01_Metallic.png — чёрная карта: дерево и краска неметаллические.
- WID01_MetallicSmoothness.png — упаковка URP: RGB = 0, A = 1 − Roughness.
- WID01_ORM.png — дополнительная упаковка R = AO, G = Roughness, B = Metallic; не заменяет MetallicSmoothness в URP Lit.

Все основные карты, включая Height, подключены. Normal strength 1, параллакс 0,015; для меньшей глубины можно снизить Height/Parallax до 0,005–0,01. Рабочий диапазон высоты около 12 мм при условном размере двери 0,9 × 2,1 м. Это авторское приближение, не обмер референса.

Изображение покрывает одно полное дверное полотно, не повторяется плиткой. Передняя сторона меша должна иметь вертикальную UV-развёртку 0–1; Tiling 1 × 1, Offset 0 × 0. Импорт Clamp, mipmaps, Trilinear, anisotropy 8, без сжатия. Технические карты linear, только BaseColor sRGB. При UV-атласе развёртку потребуется адаптировать. Для узких боковых граней подходит отдельный материал.

Объём в этом комплекте — шейдерный рельеф от Normal/Height: он реагирует на свет и угол взгляда, но не меняет силуэт и коллайдеры. Для настоящих филёнок на торцах/при очень близком боковом обзоре нужен соответствующий меш. Существующие сцены, префабы, геометрия и 3D-ручки в этой работе не менялись.

Сгенерировано встроенным image_gen; цвет и высота созданы ИИ по референсу, нормали вычислены из высоты, roughness/AO оценены. Итог пересчитан в 1024 × 2048, не измеренный PBR-скан. Исходное разрешение и проверка нормалей записаны в Validation.json.

Промпты:
Built-in image_gen; user interior door photograph as reference/edit target.

BASE COLOR
Use case: precise-object-edit. Edit target: supplied photograph of an old white four-panel interior wooden door. Create a production PBR BASE COLOR / ALBEDO texture for ONE complete door leaf, orthographic front view, tall portrait aspect 1:2 approximately 1024x2048. Preserve the reference's traditional four recessed rectangular panels: two tall narrow upper panels and two shorter lower panels, a broad middle cross rail and vertical central stile, narrow molded/beveled panel edges. Straighten perspective and make all rails/stiles and panels symmetric and square. Crop out the surrounding jamb, wallpaper, floor, hinges and background; only the door leaf fills the entire canvas. REMOVE the round knob, handle, escutcheon, screws and all hardware completely, leaving painted wood without a handle hole. Keep the original ivory/off-white paint but make it faded, dull and worn from a few years in an abandoned post-apocalyptic apartment: dusty grey grime in panel crevices, mild yellowing, small paint chips and scuffs at edges and near the bottom exposing subdued old wood, restrained hairline paint cracks and faint rub marks. Structurally intact, still mostly white, no catastrophic rot, no large peeled areas, no holes, graffiti, lettering or new decorative shapes. Neutral diffuse DELIT albedo without baked directional highlights or shadows, no vignette, no perspective or cast shadows. Panel shapes legible only through modest natural grime and paint variation; dimensional molding will be represented by a separate height/normal map. Photorealistic material detail. No frame around the image, labels or collage. Nonrepeating whole-door texture.

HEIGHT
Use case: precise-object-edit. Edit target: provided white four-panel door texture. Produce ONLY a precisely pixel-registered GRAYSCALE HEIGHT / DISPLACEMENT map of the same full door leaf, same portrait dimensions, all four panel boundaries and molding profiles unchanged. This is technical geometry data, NOT a lit embossed render. All outer stiles, central stile and horizontal rails are one uniform raised height, 75 percent light gray. All four inset panel centers are one uniform recessed height, 25 percent dark gray. The narrow perimeter molded/beveled bands around each panel interpolate smoothly between these heights with small raised molding lips. Preserve the TWO tall upper and TWO shorter lower rectangles exactly in place. Total panel recess is shallow about 12 mm. Add only very faint micro-height for chipped paint; dirt, wood grain coloration and yellow stains must not change broad surface height. REMOVE ALL LIGHTING: no bright-edge highlights, directional shadows, reflections, vignette, color, labels or text. No handle, hole, hardware, new shapes or frame. Uniform flat gray plateaus with smooth graded bevel heights, grayscale height data only.
