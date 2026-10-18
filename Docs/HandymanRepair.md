# Восстановление зависимостей Handyman — 19.10.2026

## Причина отказа применения

В коммите ed85c6e были заново созданы 33 файла `.meta` пакета `Assets/Bubba_The_Handyman`. Сами FBX, prefab, материалы и контроллеры между рабочим `99fe3c7` и текущим HEAD не менялись. Поэтому префабы продолжали ссылаться на прежние GUID и fileID, а AssetDatabase зарегистрировала новые GUID. Editor сообщал Missing Prefab Variant parent, и модель не могла корректно примениться к торговцу.

Пример URP: `FBX_Mesh/Handyman_Full.fbx.meta`, GUID `db4637291be2f55449ebbeae91a6aadf` был заменён на `ffcba355a4baf2e4a945c7a3a8077467`. Ошибка затронула также анимационные FBX, текстуры, нормали и внешние material remaps. Одной замены GUID у двух FBX недостаточно для восстановления importer-настроек.

## Что исправлено

Восстановлены ровно 33 метаданных целиком из рабочего коммита `99fe3c7`, затем выполнен повторный импорт пакета в уже открытом Unity 6000.6.3f1. Перед восстановлением проверено: старые GUID не заняты другими Assets.meta, новые GUID не имеют ссылок вне пакета (включая meta в Assets/Packages/ProjectSettings/Docs). Сейчас git diff пакета относительно рабочего коммита не показывает отличий; изменены только указанные `.meta`, исходные модели, prefab и материалы не переписывались. Это ремонт зависимостей, не новая механика торговли и не изменение ServiceVisual.

Резервная копия 33 исходных метаданных, AGENTS и ServiceSessions с SHA256: `S:/Games/Unity Projects/_CodexRecovery/Handyman-20261019-012718`. Там же `target-paths.txt`, `manifest.json`, `checks.json`, `dependencies.json`, `state-after.json`, `console-before.json` и `console-after.json`. Не удалять без подтверждения пользователя.

## Как назначить модель торговцу

1. Выберите **корень T4TraderSession** в своей сцене или откройте его prefab в Prefab Mode.
2. В компоненте **ServiceVisual** найдите **Префаб модели**. Те же настройки доступны в существующем окне настроек актёра, вкладка «Инвентарь».
3. Назначьте `Assets/Bubba_The_Handyman/URP/Prefabs/Handyman_ver_1.prefab` из Project. При необходимости выберите другой из пяти URP-вариантов.
4. **Материал модели (Material Override) = None**: персонаж должен использовать свои материалы. Нажмите **Применить модель**, если автоматическое обновление ещё не произошло.
5. Проверьте положение/поворот/масштаб и корневой BoxCollider. ServiceVisual подгоняет его по Renderer bounds; собственные коллайдеры модели отключаются. Корень сервиса и настройки сессии сохраняются.

В этом ремонте модель живого торговца намеренно не заменялась. В момент проверки он находился в `Assets/1.unity`: VisualPrefab и Instance — T4TraderTableVisual, materialOverride=None, ValidationMessage=null. Это состояние оставлено пользователю для самостоятельного выбора модели.

## Проверено

- Все **шесть URP prefab** (`Handyman_Full`, `Handyman_ver_1`…`Handyman_ver_5`) загружаются: у каждого есть Renderer, все Mesh/SkinnedMeshRenderer sharedMesh не пусты, material slots заполнены и не содержат null. Все используемые URP/Lit shaders поддерживаются. Фактический pipeline — **PC_RPAsset**, quality **PC**; настройки рендера не изменялись.
- У всех шести — валидный Humanoid Avatar. `ServiceVisual.ValidateModel` возвращает true.
- `ServiceVisualChecks.Run()` в изолированном PrefabContents дал **13 PASS**: применение Handyman, A→B→A, nested prefab link, размер корневого collider, отключение collider модели, сохранение Avatar/controller, отсутствие копий при повторном назначении, безопасный отказ для scene/self/script-rich NPC, очистка None и сохранение идентичности корневой точки/коллайдера. Изолированные экземпляры выгружены, живой торговец не использовался для замены.
- В обеих ветках URP/HDRP импортируются четыре Humanoid-клипа: `Handyman_idle1_anim` (9.333 с), `Handyman_idle2_anim` (14.667 с), `Handyman_run_anim` (0.667 с), `Handyman_walk_anim` (1.167 с). Три URP-текстуры нормалей снова имеют TextureImporter.type=NormalMap. Для URP prefab/material/importer references не осталось неразрешённых GUID.
- Все шесть HDRP prefab вновь загружаются с мешами и валидным Humanoid Avatar, но HDRP материалы в этом URP-проекте используют ErrorShader/отсутствующие HDRP-ресурсы. Это не пригодные для текущего рендера варианты: выбирать папку **URP**, не HDRP. Конвертация HDRP и установка другого pipeline не выполнялись.

## Ограничения и сохранность

У исходных prefab **Animator Controller = None**. ServiceVisual сохраняет это значение, не создаёт автоматически idle controller и не обещает проигрывание анимации без его отдельного назначения. Клипы присутствуют отдельными ассетами. Торговля/каталоги/ремонт и старые вопросы Undo/Redo не входят в исправление зависимости.

Сцены не сохранялись и не закрывались, Play не запускался. `Assets/1.unity` была и осталась dirty=true; её торговец сохранил прежние ссылки. CharacterTest до импорта была clean, после импорта/изолированных проверок Editor пометил её dirty. Файл сцены на диске не изменён; источник конкретных in-memory отличий не установлен, возможные параллельные пользовательские правки не отбрасывались. Флаг намеренно не сбрасывался. Нельзя утверждать полную неизменность состояния открытых сцен.

Текущая Console содержит исторические ошибки импорта и предупреждения: после начала ремонта новыми были только два Pipeline main-thread timeout при длительном импорте. Повторно запускать импорт из-за такого timeout не потребовалось. Новых Missing Prefab Variant parent после ремонта не зарегистрировано; compilationFailed=false. Console не очищалась. Пользовательские `.vscode/settings.json` и три Wall/Window.meta не редактировались. Коммит/пуш не выполнялись.

## Точный список восстановленных файлов

- `Assets/Bubba_The_Handyman/HDRP/Animations/Handyman_idle_1_animation.fbx.meta`
- `Assets/Bubba_The_Handyman/HDRP/Animations/Handyman_idle_2_animation.fbx.meta`
- `Assets/Bubba_The_Handyman/HDRP/Animations/Handyman_run_animation.fbx.meta`
- `Assets/Bubba_The_Handyman/HDRP/Animations/Handyman_walk_animation.fbx.meta`
- `Assets/Bubba_The_Handyman/HDRP/Demo_scene/Sample_surface.fbx.meta`
- `Assets/Bubba_The_Handyman/HDRP/FBX_Mesh/Handyman_Full.fbx.meta`
- `Assets/Bubba_The_Handyman/HDRP/Textures/Handyman_Body_AlbedoTransparency.png.meta`
- `Assets/Bubba_The_Handyman/HDRP/Textures/Handyman_Body_MaskMap_Raw.png.meta`
- `Assets/Bubba_The_Handyman/HDRP/Textures/Handyman_Body_Normal.png.meta`
- `Assets/Bubba_The_Handyman/HDRP/Textures/Handyman_Cloth_Albedo_ver2.png.meta`
- `Assets/Bubba_The_Handyman/HDRP/Textures/Handyman_Cloth_Albedo_ver3.png.meta`
- `Assets/Bubba_The_Handyman/HDRP/Textures/Handyman_Cloth_MaskMap_Raw.png.meta`
- `Assets/Bubba_The_Handyman/HDRP/Textures/Handyman_Cloth_Normal.png.meta`
- `Assets/Bubba_The_Handyman/HDRP/Textures/Handyman_Hair.png.meta`
- `Assets/Bubba_The_Handyman/HDRP/Textures/Handyman_Hair_NormalMap.png.meta`
- `Assets/Bubba_The_Handyman/URP/Animations/Handyman_idle_1_animation.fbx.meta`
- `Assets/Bubba_The_Handyman/URP/Animations/Handyman_idle_2_animation.fbx.meta`
- `Assets/Bubba_The_Handyman/URP/Animations/Handyman_run_animation.fbx.meta`
- `Assets/Bubba_The_Handyman/URP/Animations/Handyman_walk_animation.fbx.meta`
- `Assets/Bubba_The_Handyman/URP/Demo_scene/Demo_scene/ReflectionProbe-0.exr.meta`
- `Assets/Bubba_The_Handyman/URP/Demo_scene/Sample_surface.fbx.meta`
- `Assets/Bubba_The_Handyman/URP/FBX_Mesh/Handyman_Full.fbx.meta`
- `Assets/Bubba_The_Handyman/URP/Textures/Handyman_Body_AO.png.meta`
- `Assets/Bubba_The_Handyman/URP/Textures/Handyman_Body_AlbedoTransparency.png.meta`
- `Assets/Bubba_The_Handyman/URP/Textures/Handyman_Body_MetallicSmoothness.png.meta`
- `Assets/Bubba_The_Handyman/URP/Textures/Handyman_Body_Normal.png.meta`
- `Assets/Bubba_The_Handyman/URP/Textures/Handyman_Cloth_AO.png.meta`
- `Assets/Bubba_The_Handyman/URP/Textures/Handyman_Cloth_Albedo_ver2.png.meta`
- `Assets/Bubba_The_Handyman/URP/Textures/Handyman_Cloth_Albedo_ver3.png.meta`
- `Assets/Bubba_The_Handyman/URP/Textures/Handyman_Cloth_MetallicSmoothness.png.meta`
- `Assets/Bubba_The_Handyman/URP/Textures/Handyman_Cloth_Normal.png.meta`
- `Assets/Bubba_The_Handyman/URP/Textures/Handyman_Hair.png.meta`
- `Assets/Bubba_The_Handyman/URP/Textures/Handyman_Hair_NormalMap.png.meta`

## Если после применения сбрасывался ручной поворот

Отдельная причина в ServiceVisual исправлена: [ServiceVisualPlacement.md](ServiceVisualPlacement.md). Теперь Transform управляемой модели и поля ServiceVisual — одна абсолютная локальная поза, без скрытого умножения на поворот стола. Для мира перемещайте корень торговца; ручные x/z и Y=180 модели также сохраняются при применении/смене внешности. Если nested prefab заменён напрямую, назначьте тот же asset в «Префаб модели», как подсказывает предупреждение Inspector. Утраченные до диагностики значения автоматически не восстанавливаются.
