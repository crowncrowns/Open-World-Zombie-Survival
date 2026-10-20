# PBR: фактический итог ревизии 7

Численный повторный readback: **PASS**, 26 семейств, 182 карт, 293236567 байт PNG. Прежняя DirtStreaks_BaseColor сохранена отдельно. Хеш каждого изображения/каналы/Height min-max указаны в PBR_After_Manifest.json и PBR_Readback.json.

| Семейство | Разрешение | Физический период | Альфа |
|---|---|---|---|
| BluePaint | 2048² | 4 × 4 м | opaque |
| CreamConcrete | 2048² | 4 × 4 м | opaque |
| RustedSteel | 2048² | 4 × 4 м | opaque |
| RoofMembrane | 2048² | 4 × 4 м | opaque |
| FloorConcrete | 2048² | 4 × 4 м | opaque |
| WornWood | 2048² | 1 × 1 м | opaque |
| TarpGreen | 2048² | 4 × 4 м | opaque |
| SandCanvas | 2048² | 4 × 4 м | opaque |
| CeramicTile | 4096² | 8 × 8 м | opaque |
| TimberRaw | 2048² | 1 × 1 м | opaque |
| CanvasBlue | 2048² | 4 × 4 м | opaque |
| CanvasBeige | 2048² | 4 × 4 м | opaque |
| CanvasRed | 2048² | 4 × 4 м | opaque |
| CanvasDark | 2048² | 4 × 4 м | opaque |
| RugRed | 2048² | 2.8 × 1.8 м | opaque |
| RugBlue | 2048² | 2.8 × 1.8 м | opaque |
| RugWoven | 2048² | 2.8 × 1.8 м | opaque |
| PlasticDark | 1024² | 4 × 4 м | opaque |
| Enamel | 1024² | 4 × 4 м | opaque |
| Copper | 1024² | 4 × 4 м | opaque |
| Paper | 1024² | 4 × 4 м | opaque |
| ClothInk | 1024² | 4 × 4 м | opaque |
| InkDark | 1024² | 4 × 4 м | opaque |
| GlassGrime | 2048² | 4 × 4 м | alpha |
| LeafGreen | 1024² | 0.13 × 0.32 м | opaque |
| FirNeedles | 1024² | 0.22 × 0.28 м | alpha |

CeramicTile: 4K/8×8 м, 256 плиток по 0,5 м; 83 красно-коричневые и 173 светлые. Дерево ориентировано вдоль продольной UV V. Листья/хвоя имеют уникальную UV и отдельные карты жилок/игл, без случайного наложения старой ткани. FirNeedles использует вырезной BaseColor alpha, threshold 0,4, ZWrite и coverage mipmaps. Остальные ткани физически непрозрачны; отверстия представлены реальной геометрией/износом, а не смешением alpha на всём каркасе.

BaseColor — sRGB, остальные карты linear. Normal — tangent +Y. Roughness/Metallic/Occlusion отдельные scalar файлы. Packed MetallicSmoothness: R=metallic, A=1−roughness, G=B=0; readback погрешность не более 1/255. Height — 16-bit обменный микрорельеф в указанных физических пределах; активного runtime displacement/parallax нет. Направление normal вычислено из Height и проверено двумя аналитическими наклонами. Диэлектрики metal=0; ржавчина не выдаётся за голый проводящий металл. AO — локальные консервативные полости/швы, не bake геометрии здания.

Материалы процедурные, авторские; сканированная точность и AAA-фотореализм не заявляются. URP Lit alpha-стекло не имеет физической рефракции. Целевая Unity проверяет реальные texture slots/sRGB/import limits/mipmaps/нормали; дневные/пасмурные/скользящие ракурсы и крупные планы сохранены в PREVIEWS/Final_Renders. Финальный baked свет и populated FPS не проверены.

Генераторы: Tools/KursHub/audit7_surfaces.py, audit7_leaf_surface.py, audit7_fir_surface.py; проверка verify_pbr7.py. Подключение Blender — audit7_materials.py, Unity — KursHubSurfaceSetup.cs. Генерацию выполнять в staging, сохранять пользовательские edits и GUID; неизвестные авторские карты не сбрасывать.
