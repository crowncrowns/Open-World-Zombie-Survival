# Weathered Corrugated Roof 01 — серая волнистая кровля

Готовый материал: M_WeatheredCorrugatedRoof_01_URP.mat (Universal Render Pipeline/Lit).
Назначьте его только на кровельное покрытие или соответствующий слот Renderer → Materials. На существующую крышу автоматически ничего не назначалось.

По референсу создан серо-бежевый выветренный материал, визуально похожий на старую волнистую фиброцементную кровлю: мелкое минеральное зерно, грязь, следы стока воды, редкие оливковые вкрапления лишайника. Точный состав реального материала по фото не устанавливался.

Восемь PNG-карт 2048 × 2048:
- WCR01_BaseColor — цвет без намеренно запечённого освещения, sRGB.
- WCR01_Normal_OpenGL — tangent-space +Y нормали с волнистым профилем и слабой микрофактурой; импорт Normal Map.
- WCR01_Height16 — 16-битная высота: белое — гребень, чёрное — впадина.
- WCR01_AO — мягкое затенение впадин.
- WCR01_Roughness — высокая шероховатость минеральной поверхности.
- WCR01_Metallic — 0, покрытие неметаллическое.
- WCR01_MetallicSmoothness — упаковка URP: RGB = 0, Alpha = 1 − Roughness.
- WCR01_ORM — дополнительная упаковка R = AO, G = Roughness, B = Metallic для других шейдеров; не подставлять вместо MetallicSmoothness в URP Lit.

Все необходимые карты подключены к материалу. Repeat, mipmaps, Trilinear, anisotropy 8. Только цвет sRGB, технические карты linear. Исходный импорт без потерь, normal strength 1, параллакс 0,015 с умеренным эффектом.

Масштаб: один повтор около 1,2 × 1,2 м, восемь волн поперёк U, гребни идут вдоль V — от конька к карнизу. Шаг 150 мм и высота гребень-впадина 40 мм — выбранные рабочие параметры, не обмер кровли на фото. При UV 0–1 на скате 12 × 6 м начальный Tiling = 10 × 5. Если UV уже масштабированы, это число неприменимо напрямую. При поперечном направлении волн требуется повернуть UV ската на 90 градусов с корректными тангентами, а не саму крышу.

Нормали и параллакс имитируют волны на поверхности, но не меняют силуэт крыши, тени от геометрии и коллайдеры. Для видимого волнистого торца нужен отдельный волнистый меш. Крепёж, нахлёсты отдельных листов, конёк, дымоходы и водосток этим материалом не создаются.

Происхождение: встроенный image_gen, исходная цветовая фактура 1254 × 1254, подготовлена в 2048 × 2048. Цвет художественный, нормали/высота математически согласованы с периодическим волнистым профилем, AO/roughness оценочные. Не фотограмметрия и не точная копия реального материала. Края цвета, повторение, нормали и импорт проверены; параметры — Validation.json.

Промпты:
Built-in image_gen; reference: roof covering only from user photograph.

BASE COLOR
Use case: photorealistic-natural. Asset type: seamless PBR BASE COLOR / ALBEDO surface texture for aged gray corrugated fiber-cement roofing sheets in a realistic post-apocalyptic Unity environment. Reference photograph: use ONLY the gray weathered wavy roof covering for color and material character, no actual roof composition. Generate a square flat orthographic material tile 2048x2048 if possible. Eight equally spaced corrugation wavelengths across the tile, ridges running vertically from top to bottom, consistent straight parallel grooves with about 150 mm pitch, tile physical size 1.2 x 1.2 metres. Weathered gray-beige cementitious roof surface, porous fine mineral grain, dull charcoal dust in worn areas, sparse muted olive lichen flecks, subtle rain runoff patina. Intact aged sheets, no holes, no thick mold or severe cracking. IMPORTANT this is DELIT ALBEDO ONLY: do not bake alternating bright and dark lighting stripes into the corrugations; geometry and shading will come from the normal map separately. Mild natural color variation may follow runoff vertically. Seamless all four sides, no border, no panel seams unless exactly repeatable, no objects, fasteners, skylights, chimneys, sky, brick, gutters, perspective, shadows, directional lighting, specular reflections, text or watermarks.

DELIGHTING EDIT
Use case: precise-object-edit. Edit target: supplied weathered gray roofing texture. Change ONLY illumination and broad relief shading: remove ALL broad regular bright/dark corrugation stripes, highlights and shadows. Flatten the surface into uniform DELIT gray-beige fiber-cement albedo while preserving its exact fine grain, scattered olive lichen specks, weathering marks and framing. In the result there must be NO regularly shaded ribs or embossed appearance; only subtle irregular vertical rain grime is allowed. It should look like the same mineral material on a flat perfectly evenly illuminated surface. Corrugation geometry will be reconstructed mathematically by a separate normal map, so baking it into color is wrong. Keep it matte, neutral grey, subtly aged, tileable on all four edges. No new objects, panel borders, holes, text or gradients.

Technical maps: analytic sinusoidal eight-wave profile, 1.2m tile, 150mm pitch, 40mm crest-to-trough. Not inferred measurements from the photograph.
