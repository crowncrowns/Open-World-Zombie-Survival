import json,sys,hashlib,shutil,zipfile
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import ROOT,ASSET,VERIFY,GROUPS

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def prepare():
    # Folder GUIDs can already be used by the user's Project-window bookmarks. They do
    # not identify a mesh/script dependency, so keep the main checkout's folder metadata.
    main=ROOT/'Assets/PostApocalypticFurniture'
    top=Path(str(main)+'.meta');stage_top=Path(str(ASSET)+'.meta')
    if top.exists() and stage_top.exists():shutil.copyfile(top,stage_top)
    for meta in main.rglob('*.meta'):
        if 'folderAsset: yes' in meta.read_text(encoding='utf-8-sig',errors='replace'):
            target=ASSET/meta.relative_to(main)
            if target.exists():shutil.copyfile(meta,target)
    catalog=json.loads((ASSET/'Documentation/catalog.json').read_text(encoding='utf-8'))['assets']
    audit=json.loads((VERIFY/'unity_prefab_audit.json').read_text(encoding='utf-8'))
    runtime=json.loads((VERIFY/'unity_runtime_checks.json').read_text(encoding='utf-8'))
    stress=json.loads((VERIFY/'unity_stress_checks.json').read_text(encoding='utf-8'))
    reimport=json.loads((VERIFY/'blender_clean_reimport.json').read_text(encoding='utf-8'))
    if not all(d['passed'] for d in [audit,runtime,stress,reimport]):raise RuntimeError('A required acceptance check has not passed')
    if len(list((ASSET/'Prefabs').rglob('*.prefab')))!=200:raise RuntimeError('Expected exactly 200 furniture prefabs')
    if len(list((VERIFY/'UnityPreviews').glob('*.png')))!=200:raise RuntimeError('Expected 200 final native model frames')
    if not (VERIFY/'unity_cold_check.json').exists():raise RuntimeError('Cold reopen proof missing')
    cold=json.loads((VERIFY/'unity_cold_restore.json').read_text(encoding='utf-8'))
    if not cold['passed']:raise RuntimeError('Cold-process furniture state restore has not passed')
    own_contacts=json.loads((VERIFY/'native_body_contacts.json').read_text(encoding='utf-8'))
    if own_contacts['contacts']:raise RuntimeError('Own-body contact diagnostic still needs clearance review')
    low=min(a['triangles'][0] for a in audit['assets']);high=max(a['triangles'][0] for a in audit['assets'])
    interactions=sum(a['interactions'] for a in audit['assets']);zones=sum(a['zones'] for a in audit['assets'])
    docs=ASSET/'Documentation';docs.mkdir(exist_ok=True)
    (docs/'README_RU.md').write_text(f'''# Постапокалиптическая мебель · 200 предметов

Набор создан для Open-World-Zombie-Survival, Unity **6000.6.3f1**, URP **17.6.0**, Input System проекта. В нём ровно **200 отдельных FBX и 200 префабов**, по десять в каждой из двадцати категорий. Это процедурно смоделированные игровые конструкции, не сканы и не копии измеренных музейных предметов.

## Начать работу

Откройте `DemoScenes/Furniture_Showcase.unity`: все 200 предметов, подписи, пол и перегородки. WASD — ходьба, мышь — обзор, **F — единственное действие взаимодействия**, Esc — курсор. Наведитесь именно на дверцу, ящик, кнопку или ручку, которую хотите использовать. Подсказка поступает из прежнего `PlayerInteractor`; мебель не читает клавиатуру самостоятельно. Перенос в свою сцену — обычное размещение префаба из одной из двадцати папок `Prefabs`.

`Furniture_StressTest.unity` содержит **12 квартир и 288 экземпляров мебели**, с разной степенью износа. Сцены созданы отдельно; сцены, Packages и Render Pipeline основного проекта сборщик не меняет. Изолированный сборщик намеренно отказывается работать в основном checkout.

## Содержимое

- Настоящие FBX, три LOD на отдельный предмет, собственные оси подвижных деталей.
- LOD0: **{low}–{high} треугольников** по фактическому импорту Unity; жёсткий предел 18 000 соблюдён.
- **{interactions} интерактивных элементов**, **{zones} зон размещения**, полые ящики и корпуса. Полости свободны от примитивных коллайдеров в проверенном закрытом положении.
- Десять радио с питанием, шестью вымышленными станциями, настройкой, громкостью, помехами и пространственным затуханием.
- Десять холодильников с полками, карманами дверей и независимыми отделениями; лампы управляются открытием дверцы.
- Светильники с выключателем и регулятором яркости; кнопки электроники переключают питание/режимы.
- PBR: BaseColor, tangent-space Normal, Metallic, Roughness, AO, Height, упакованная Mask (R=metallic, G=AO, A=smoothness), WearMask. Основные поверхности — 2K, небольшие второстепенные материалы — 1K. Height согласован с normal; геометрическое displacement в игровом шейдере не включено.
- Двадцать редактируемых `.blend`: `SourceBlender/Source~/`. Папка `Source~` исключена из автоматического Blender-импорта Unity; это полноценные исходники, доступные в ZIP. Текстуры связаны относительными путями. В Blender Z — вверх, лицевая сторона +Y; в Unity Y — вверх, лицевая сторона +Z, метры.

## Износ экземпляров

`FurnitureWear` на корне хранит seed, грязь, выцветание, оттенок и повреждение 0–1. Выделите экземпляры и используйте меню `Tools/Environment/Furniture/Варьировать износ выбранной мебели` либо настройте компонент. Меняются MaterialPropertyBlock и доступные дополнительные повреждённые детали; общие меши и материалы не клонируются. Seed и значения сохраняются вместе с мебелью. Урон поверхности не вырезает произвольные новые отверстия и не запускает физическое разрушение. Для восьми конструкций подготовлены дополнительные авторские повреждённые элементы; другие состояния разрушения являются отдельными моделями каталога.

Поля износа объявлены instanced properties в URP-шейдере. Итоговое объединение вызовов зависит от LOD, материалов, света и остальных настроек сцены; само наличие общего материала не является гарантией числа draw calls.

## Кресло-кровать 019

Для раскладывания наведитесь на нужную деталь и нажимайте F по порядку: выкатить основание → выдвинуть ножную секцию → поднять среднюю секцию → поднять ножную секцию → развернуть опору. Сложение — в обратном порядке. Подсказки и блокировки защищают последовательность. Длина разложенного спального места — 1,855 м.

После этой пересборки у 019 добавлены четыре механизма. Старый снимок мира с прежней конструкцией 019 несовместим с её новым составом: после обновления такой сцены создайте новое сохранение F5. Строгая проверка общего снимка мира сохранена; автоматического пропуска отсутствующих состояний нет.

## Сохранение

На каждом префабе есть `FurnitureWorldSaveAdapter` и прежние `WorldSaveObject`/`SaveIdentity`. Сохранение дверец, ящиков, направления незавершённого движения, устройств, радио и износа проходит через **существующий атомарный снимок мира**. Второго файла экономики/предметов или самостоятельной системы сохранения мебели нет.

После размещения выделите корни и выполните `Tools/Environment/Furniture/Зарегистрировать выбранную мебель в сохранении мира`, затем сохраните свою сцену обычным способом. Команда использует единственный реестр своей сцены, назначает только пустые IDs, сохраняет существующие и поддерживает Undo; повторный ID требует явного ремонта прежним инструментом. Префабы не содержат общего сценового ID. После изменения состава мира нужен новый совместимый снимок F5, как и для других мировых объектов проекта.

Уход из квартиры внутри загруженной сцены и disable/enable не сбрасывают состояние. Для выгрузки сцены необходим штатный снимок до выгрузки и восстановление той же области. Streaming большого мира данным набором не реализован.

## Механизмы и порядок

Движение выполняется скриптом с плавным разгоном/торможением, повторное F разворачивает цель. Вращается отдельная ось, не корень предмета. Коллайдеры следуют деталям. Для стола-книжки сначала сложите опоры, затем крылья; при раскладывании сначала крылья, затем опоры. У раздвижного стола сначала раздвигаются обе половины, затем поднимается вкладка. У складного стула сначала сиденье, затем ножки. Корзины посудомойки доступны после открытия дверцы. Внутренняя морозильная заслонка требует открытой внешней дверцы. Небезопасный порядок отклоняется с русской подсказкой, включая проверку будущего состояния при Load.

Защита от внешних препятствий позиционная и использует промежуточные Box/Capsule-overlap проверки. Это не система динамического разрушения и не симуляция сложных шарнирных сил. Произвольные изначальные пересечения и очень тонкие/быстрые препятствия не гарантированы. `SetOpen(..., immediate:true)` — авторский/API-переход, пропускающий плавный физический маршрут; игровое F использует обычный путь.

Зоны `FurniturePlacementZone` обозначают свободный геометрический объём и движутся с ящиками. Они не создают предметы и не заменяют `ItemStorage`/`WorldItem`; содержимое размещается существующим игровым механизмом.

## Проверки и исходники

Меню `Tools/Environment/Furniture/Проверить 200 префабов` создаёт отчёт с треугольниками трёх LOD, числом материалов, интерактивных элементов, зон, коллайдеров и проблем. Полный каталог — `CATALOG_RU.md`, данные — `catalog.json`. Подробный отчёт — `Verification/PostApocalypticFurniture/Report_RU.md` в поставке.

Производственные скрипты находятся в `Tools/PostApocalypticFurniture`; временные Play-проверки не устанавливаются в Assets. Все проверки выполнены в отдельном проекте. Основной игровой Play/build, установленная пользователем застройка, OS-мышь, все игровые ракурсы и целевой FPS большого мира не приняты этим прогоном. Материалы процедурные; отдельного high-poly bake нет. Зеркала используют отражающий материал, экраны — индикацию питания/режима, без телевизионного видеоконтента и planar reflections.
''',encoding='utf-8')
    names={a['name']:a for a in audit['assets']}
    lines=['# Каталог · 200 моделей','', '| ID | Предмет | Категория | LOD0 / LOD1 / LOD2 | Габариты, м | Взаимодействия | Зоны |','|---|---|---|---|---|---|---|']
    for row in catalog:
        e=names[row['slug']];dim=[row['boundsMax'][i]-row['boundsMin'][i] for i in range(3)]
        lines.append(f"| {row['id']:03d} | {row['name']} | {row['categoryRu']} | {' / '.join(map(str,e['triangles']))} | {' × '.join(f'{x:.2f}' for x in dim)} | {e['interactions']} | {e['zones']} |")
    (docs/'CATALOG_RU.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    (docs/'Audio_Provenance_RU.md').write_text('''# Звук и происхождение материалов

Геометрия, рисунки ковров, поверхности, маски и шумы созданы процедурно для этого набора. Изображения сторонних производителей и защищённые песни не использованы. Торговые названия холодильников — маркировка типов из ТЗ, без скопированных логотипов.

`make_surfaces.py` создаёт оригинальную инструментальную мелодию, атмосферную подложку, пустую частоту, скрип, скольжение, щелчки и радиопомехи. `make_speech.ps1` синтезирует три оригинальных вымышленных русскоязычных текста установленным системным голосом Microsoft Irina: ведущий «Тихой волны», сообщение станции «Север», экстренная передача. Содержимое не является реальными экстренными указаниями.

Все каналы вынесены в `FurnitureRadioPlaylist` ScriptableObject; AudioClip можно заменить собственным разрешённым контентом. Радиостанция, канал, громкость, настройка и позиция воспроизведения сохраняются в блоке мебели. Источники 3D, с логарифмическим затуханием; не создают AudioListener и не транслируются на всю карту. Они не подменяют игровой `GameplayNoise` и не меняют правила привлечения заражённых.
''',encoding='utf-8')
    summary=f'''# Проверка набора мебели · 26.10.2026

Созданы и импортированы **200 FBX / 200 префабов**, 20 категорий, 20 редактируемых Blender-исходников. LOD0 в Unity: **{low}–{high} треугольников**, все ниже 18 000. **{interactions} интерактивных элементов**, **{zones} зон размещения**.

| Проверка | Подтверждённый результат |
|---|---|
| Чистый Blender re-import | {reimport['count']}/200, геометрия, UV, оси, материалы, три LOD, конечные координаты, без нулевых треугольников |
| Unity 6000.6.3f1 / URP17.6 | 200/200, ссылки, материалы, коллайдеры, LOD и блок состояния |
| Play | {len(runtime['checks'])} успешных утверждений; механизмы, обратное движение, повторная активация, радио, износ, interlocks |
| Ввод | Виртуальная клавиатура Input System → существующий PlayerInteractor → F; удержание не повторяет действие |
| Сохранение | Реальный WorldSaveRegistry JSON round trip, повторный Load без дублирования, отказ повреждённого блока до изменения мира |
| Нативные кадры | 200 отдельных Unity Camera.Render LOD0; 20 вариантов износа, кадры LOD и нагрузочной сцены |
| Нагрузочная сцена | {stress['apartments']} квартир / {stress['furnitureInstances']} экземпляров; измерения в unity_stress_checks.json |
| Холодное открытие | Повторный процесс Unity: сцены/IDs/реестры; открытая дверца, радио и износ восстановлены из сохранённого на диск JSON |

До окончательного экспорта удалены совпадающие треугольники, возникшие при упрощении дальних швов/пружин. Предварительное расследование сохранено отдельно в `fbx_lod_loss.json`; это снимок дефекта до исправления. В итоговом `blender_clean_reimport.json` количество треугольников экспорта и чистого импорта совпадает для всех трёх LOD. Отчёт префабов и каталог приводят фактические игровые меши. Проверка исходников и чистого FBX не подменяет визуальную приёмку всех ракурсов.

Исходные проверки деталей 001–100: все 100 геометрий различны без учёта материала, зоны без пересечения с досками, критические траектории по 20 положениям и все механизмы по 41 положению относительно пола с необходимыми позами родителей. Проверка видимой геометрии опор: source_support_contacts_01_10.json. Для 101–200 повторно выполнены OBB/capsule проверки зон; действительные нативные контакты всех механизмов проверены отдельным проходом ниже. Блокировки порядка защищают специально связанные механизмы. Старые дополнительные source-проходы находятся в DevelopmentHistory и не считаются новой приёмкой.

Материалы — процедурная реконструкция, AO не получен отдельным high-poly bake. Износ меняет маски/оттенок и предусмотренные дополнительные фрагменты; произвольного разрушения мешей нет. Измерения производительности относятся к указанным камерам и изолированному Editor Play, а не к FPS основного мира/standalone build. Не подтверждены AAA/точность реальным заводским образцам, все игровые ракурсы/переходы LOD, OS-мышь, основной Play/build и streaming. Обычные источники света мебели не являются UV; радио не меняет экономику, предметы или правила AI.

Основные пользовательские сцены, Packages и настройки pipeline не пересохранялись. Все rebuild/test команды защищены путём work/PostApocalypticFurniture. Пакет использует уже существующие игровые контракты; его C#-часть предназначена для данного проекта.
'''
    summary+='\n## Проверка корпуса и нагрузки\n\n'
    summary+=f"Нативный диагностический проход: {own_contacts['mechanisms']} механизмов, {own_contacts['poses']} положений, **0 новых контактов с собственным статическим корпусом** сверх контакта закрытого состояния и допуска 2 мм. Проверены реальные Box/Capsule Unity, отдельно от исходного аудита. Это не проверка всех движущихся предметов мира. В полном Play-проходе дополнительно проверялась каждая движущаяся деталь относительно настоящего пола на каждом шаге открытия/закрытия; допуск численной погрешности касания — 10 мкм.\n\n"
    summary+='Нагрузочный прогон: Unity Editor Play, Direct3D11, NVIDIA GeForce RTX 3070, 1280×720, исходные закрытые механизмы и выключенные устройства. 120 кадров прогрева на ракурс, 90 измерений; результат зависит от сцены и редактора.\n\n| Ракурс | Camera.Render CPU, среднее / p95, мс | Draw calls | SetPass | Треугольники |\n|---|---|---|---|---|\n'
    for view in stress['views']:
        def counter(name):
            c=next((x for x in view['counters'] if x['name']==name),None)
            return str(round(c['values']['mean'])) if c and c['available'] else 'недоступно'
        timing=view['cameraRenderCpuMs'];summary+=f"| {view['name']} | {timing['mean']:.3f} / {timing['p95']:.3f} | {counter('Draw Calls Count')} | {counter('SetPass Calls Count')} | {counter('Triangles Count')} |\n"
    summary+='\nСчётчики Draw calls/SetPass используют проверенный резервный источник UnityStats, когда ProfilerRecorder возвращает только нули. Batches Count в данном batch Editor недоступен и не объявлен нулевым. CPU-время Camera.Render не является временем завершения GPU или FPS игры. Массовое одновременное движение дверец, звук всех радио и все включённые светильники этим прогоном не измерялись.\n'
    summary+='\nСлужебные сообщения редактора: в принятых batch-запусках повторяется ArgumentOutOfRangeException внутри UnityEditor.Search.SearchDatabase при стартовой индексации. Стек находится в Editor Search; целевые проверки завершились PASS. Встречались таймауты UnityConnect при завершении. Поэтому весь журнал редактора не объявляется полностью чистым, несмотря на пройденную компиляцию C# и проверки набора.\n'
    (VERIFY/'Report_RU.md').write_text(summary,encoding='utf-8')
    (ROOT/'Docs/PostApocalypticFurniture.md').write_text('# Мебель квартир · 200 предметов\n\nОсновная инструкция: [README](../Assets/PostApocalypticFurniture/Documentation/README_RU.md). Полный [каталог](../Assets/PostApocalypticFurniture/Documentation/CATALOG_RU.md), [отчёт проверки](../Verification/PostApocalypticFurniture/Report_RU.md).\n\nВзаимодействие исключительно через прежний PlayerInteractor на **F**. Переносимые экземпляры регистрируются в существующем реестре мира командой Tools/Environment/Furniture/Зарегистрировать выбранную мебель в сохранении мира. Износ хранится на FurnitureWear. Изолированные Showcase и StressTest находятся в DemoScenes набора.\n',encoding='utf-8')
    print('DELIVERY_DOCS_PREPARED',flush=True)

def package():
    dest=ROOT/'Assets/PostApocalypticFurniture'
    # Only the newly created furniture subtree is installed; unrelated checkout paths are never synchronized.
    shutil.copytree(ASSET,dest,dirs_exist_ok=True,ignore=shutil.ignore_patterns('*.blend1','*.blend1.meta','__pycache__'))
    folder_meta=Path(str(ASSET)+'.meta')
    if folder_meta.exists():shutil.copyfile(folder_meta,Path(str(dest)+'.meta'))
    files=[p for p in ASSET.rglob('*') if p.is_file() and not p.name.endswith('.blend1') and not p.name.endswith('.blend1.meta')]
    mismatch=[str(p.relative_to(ASSET)) for p in files if sha(p)!=sha(dest/p.relative_to(ASSET))]
    if mismatch:raise RuntimeError('Installed hash mismatch '+repr(mismatch[:10]))
    output=ROOT/'outputs/PostApocalypticFurniture_Unity.zip';output.parent.mkdir(exist_ok=True)
    records=[]
    with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        if folder_meta.exists():z.write(folder_meta,'Assets/PostApocalypticFurniture.meta');records.append(dict(path='Assets/PostApocalypticFurniture.meta',size=folder_meta.stat().st_size,sha256=sha(folder_meta)))
        for p in sorted(files):
            rel=Path('Assets/PostApocalypticFurniture')/p.relative_to(ASSET);z.write(p,str(rel));records.append(dict(path=str(rel).replace('\\','/'),size=p.stat().st_size,sha256=sha(p)))
        for base in [ROOT/'Tools/PostApocalypticFurniture',VERIFY]:
            for p in sorted(base.rglob('*')):
                if p.is_file() and '__pycache__' not in p.parts and p.suffix not in ['.log'] and p.name not in ['package_integrity.json']:
                    rel=p.relative_to(ROOT).as_posix();z.write(p,rel);records.append(dict(path=rel,size=p.stat().st_size,sha256=sha(p)))
        z.write(ROOT/'Docs/PostApocalypticFurniture.md','Docs/PostApocalypticFurniture.md');records.append(dict(path='Docs/PostApocalypticFurniture.md',size=(ROOT/'Docs/PostApocalypticFurniture.md').stat().st_size,sha256=sha(ROOT/'Docs/PostApocalypticFurniture.md')))
        z.writestr('SHA256_MANIFEST.json',json.dumps(records,ensure_ascii=False,indent=2))
    with zipfile.ZipFile(output) as z:
        bad=z.testzip()
        if bad:raise RuntimeError('Archive corruption '+bad)
        names=z.namelist();counts={ext:len([n for n in names if n.startswith('Assets/PostApocalypticFurniture/') and n.endswith(ext)]) for ext in ['.fbx','.prefab','.blend','.png','.wav']}
        if counts['.fbx']!=200 or counts['.prefab']!=200 or counts['.blend']!=20:raise RuntimeError('Incomplete archive '+str(counts))
    report=dict(passed=True,installedFiles=len(files),hashMismatches=0,archive=str(output),archiveSHA256=sha(output),archiveBytes=output.stat().st_size,counts=counts)
    (VERIFY/'package_integrity.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,ensure_ascii=False),flush=True)

if __name__=='__main__':
    if '--package' in sys.argv:package()
    else:prepare()
