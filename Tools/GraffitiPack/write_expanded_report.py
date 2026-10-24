from pathlib import Path
import hashlib,json

root=Path(__file__).resolve().parents[2]
pack=root/'Assets/PostApocalypse/GraffitiPack';v=root/'Verification/GraffitiPack'
def read(name):return json.loads((v/name).read_text(encoding='utf-8'))
items=json.loads((pack/'Documentation/catalog.json').read_text(encoding='utf-8'))['items']
integrity=read('package_integrity.json');cold=read('cold_import.json');military=read('military_checks.json');atlas=read('atlas_checks.json');textures=read('texture_validation.json');sceneview=read('sceneview_military_checks.json')
assert len(items)==cold['prefabsPassed']==integrity['sourcePNGs']==60
baseline=read('pre_military_baseline.json');changed=[p for p,h in baseline.items() if not (root/p).exists() or hashlib.sha256((root/p).read_bytes()).hexdigest()!=h]
assert not changed,changed
(v/'military_preservation.json').write_text(json.dumps(dict(baselineFilesChecked=len(baseline),base50AssetBytesAndMetasPreserved=True,changed=[]),indent=2),encoding='utf-8')
rel=lambda p:str(p.relative_to(root)).replace('\\','/')
catalog=[]
for d in items:
    mats=list((pack/'Materials').glob(d['file']+'_*.mat'));prefabs=list((pack/'Prefabs').glob(d['file']+'_*.prefab'));assert len(mats)==len(prefabs)==1
    catalog.append(dict(id=d['id'],title=d['title'],category=d['category'],width=d['width'],height=d['height'],texture=rel(pack/f"Textures/{d['category']}/{d['file']}.png"),material=rel(mats[0]),prefab=rel(prefabs[0]),entry=rel(pack/f"Catalog/{d['file']}.asset")))
(v/'asset_catalog.json').write_text(json.dumps(dict(items=catalog),ensure_ascii=False,indent=2),encoding='utf-8')
below=sum(max(i['sourceSize'])<2048 for i in textures['items'])
report=f'''# Graffiti Pack — 60 композиций, 24.10.2026

В `Assets/PostApocalypse/GraffitiPack` находятся 60 самостоятельных RGBA PNG, 60 URP-материалов, 60 независимых префабов и 60 записей каталога. К исходным 50 добавлены G51–G60 в категории **Military2022 / Военная тематика · 2022** по запросу пользователя. Исходные текстуры, материалы, префабы, записи каталога и их .meta сохранены побайтно; проверено {len(baseline)} файлов. Демонстрация и визуальный каталог расширены до 60 вариантов.

## Новое дополнение

Z кистью, V трафаретом, O с датой 2022, Z в квадрате, V в кольце, «СВО 2022», «КОЛОННА →», «ВОЕННАЯ ПОЛИЦИЯ», «БЛОКПОСТ 2022» и многослойная Z/V с датой. Z/V/O намеренно латинские обозначения; русские надписи сверены визуально. Это игровые художественные изображения по запросу, а не проверенный справочник исторических схем маркировки.

## Поставка и пути

- Архив: `outputs/GraffitiPack_URP.unitypackage`, {integrity['packageBytes']:,} байт; SHA-256 `{integrity['packageSha256']}`.
- Предыдущий архив на 50 вариантов: `outputs/GraffitiPack_URP_50_Backup.unitypackage`.
- Новые текстуры: `Assets/PostApocalypse/GraffitiPack/Textures/Military2022/G51.png…G60.png`.
- Префабы/материалы с номерами и читаемыми именами: `Prefabs/` и `Materials/` внутри пака; записи `Catalog/G51.asset…G60.asset`.
- Галерея: `Documentation/PreviewCatalog.html`; инструкция: `Documentation/README_RU.md`.
- Демонстрация: `Demo/GraffitiGallery.unity` — 60 стен 3 × 3 м и 60 экземпляров, камера охватывает шесть рядов.
- Пути PNG/материала/префаба/записи для всех вариантов: `Verification/GraffitiPack/asset_catalog.json`. Все файлы, размеры и хеши: `asset_manifest.json` рядом.
- Запросы встроенного imagegen: `Tools/GraffitiPack/generation_prompts.json` и `military2022_generation_prompts.json`; отдельные исходные выходы: `work/GraffitiPack/Source/G01.png…G60.png`.

## Технические результаты

Подтверждённая конфигурация: Unity 6000.6.3f1 / URP 17.6.0, desktop. В исходном PC_Renderer нет Decal Feature, поэтому основные префабы — URP Lit Transparent Quad, без emission/ShadowCaster/ZWrite. Исходные Packages и глобальные настройки рендера не менялись. Новая категория доступна в Painter и в случайном выборе по категории.

| Проверка | Результат |
|---|---|
| Самостоятельные PNG, уникальные SHA-256 | 60/60 |
| RGBA, длинная сторона 2048, alpha=0 на внешних краях | 60/60 |
| Префабы: текстура/материал/меш/скрипты без Missing References, sRGB/mipmaps | 60/60 |
| Новая категория Military2022 | 10/10 |
| Видимость новой краски: разница кадров с Renderer и без него | 10/10 |
| Scene View: выбран G51 из новой категории, стена без Collider/Static, Undo/Redo | PASS, скриптовые события редактора |
| Атласная команда | {atlas['alternativePrefabs']} альтернативных префабов, {atlas['pages']} страниц/общих материалов; ссылки/UV PASS |
| Импорт обновлённого архива в отдельный чистый проект | {cold['prefabsPassed']}/60 префабов, {cold['demoInstances']} экземпляров галереи |
| Сохранность исходного набора | Побайтно, включая .meta/GUID |
| Архив | {integrity['packagedAssets']} файловых ассетов; payload совпадают с checkout |

Результаты в `Verification/GraffitiPack`: `military_checks.json`, `sceneview_military_checks.json`, `military_preservation.json`, `unity_validation.json`, `texture_validation.json`, `atlas_checks.json`, `cold_import.json`, `package_integrity.json`. Протоколы в `work/GraffitiPack`: `build_military.log`, `verify_military.log`, `sceneview_military.log`, `cold_military.log` / `cold_military_verify.log`. Снимок текущих проверок включён в `Documentation/Validation.json`.

Просмотрены лист `Catalog_6.jpg` с десятью новыми изображениями и кадры Unity. Сохранены отдельные `Render_51.png…Render_60.png` и `Gallery_60_Daylight.png`. Генерация выполнена отдельными встроенными imagegen-запросами для каждого PNG. Старая приёмка базовых 50 (74 технических PASS, Play, углы/нулевой свет, исправление стен с 11 новыми проверками) сохранена в `Report_Base50_RU.md` и соответствующих JSON/логах; повторный Play основной игры не проводился.

## Размещение и ограничения

Открытие: `Tools → Graffiti Painter`; категория **Военная тематика · 2022**. Painter пересекает видимый MeshFilter/MeshRenderer: Collider и Static не обязательны, non-readable меши поддержаны. Крупный Collider корня здания не заменяет фасад. Terrain сохранён. Исключены Rigidbody/Animator/CharacterController и собственные граффити; действует LayerMask. Превью проверяет 49 точек опоры, при ошибках установка блокируется до явного разрешения частичной опоры. Это дискретная проверка, не точная обрезка проёмов. Сохраняются opacity, масштаб и метка Painter, Undo/Redo и групповое удаление только собственных размещений активной сцены.

Объекты — мировые корни и не следуют последующему перемещению стен. Quad не огибает кривые поверхности и не наследует normal/roughness стены на плотных участках пигмента; полноценный микрорельеф требует native decals. Их создание по уже включённому Feature проверено в базовом этапе, отдельная GPU-приёмка native/HDRP/Built-in не проводилась. Плоское превью и отступ 3 мм не доказывают отсутствие мерцания на любых дистанциях/камерах.

Все итоговые PNG имеют длинную сторону 2048 px. {below} исходных выходов imagegen были меньше и увеличены при экспорте; это не новая детализация. Исходные размеры раскрыты в texture_validation.json. Альфа-экспорт удаляет шум ≤2/255 и добавляет прозрачный запас. Импорт: sRGB, alphaIsTransparency, mipmaps, Clamp/Trilinear, Standalone BC7 HQ. Atlas PackTextures может уменьшать отдельные изображения; атласные альтернативы не включены в основной пакет и создаются явной командой, их GPU/FPS-приёмка не проводилась.

Пользовательские сцены и расстановка не менялись. Не подтверждены AAA/точное совпадение референсу, FPS большого мира, standalone build, основной Play, игровой свет/погода или world-save интеграция. Пересечение мешей опирается на Editor API Unity 6000.6.3f1; другие версии отдельно не проверены.

## Индекс композиций

| № | Название | Категория | Ширина × высота, м |
|---|---|---|---|
'''
for d in catalog:report+=f"| {d['id']:02d} | {d['title']} | {d['category']} | {d['width']:.2f} × {d['height']:.2f} |\n"
(root/'Docs/GraffitiPack.md').write_text(report,encoding='utf-8')
print('Report updated:',len(catalog),'entries;',len(baseline),'baseline files intact')
