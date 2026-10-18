# Т4: общие сессии сервисов — выполнен

## Объём и пути

Один `PlayerServiceSession` на игроке открывает одну сессию через существующий `PlayerInteractor` / E. `TraderServicePoint` и `WorkbenchServicePoint` наследуют общий `ServicePoint : Interactable`. Это тестовая оболочка доступа: каталогов, сделок, ремонта, улучшений, наград или сохранения экономики здесь нет. Wallet guard F5/F9 сохранён.

Код: `Assets/Scripts/Services/`. Игрок: `Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab`. Обёртки: `Assets/CharacterTesting/Prefabs/T4TraderSession.prefab` и `T4WorkbenchSession.prefab`. Сцена: `Assets/CharacterTesting/CharacterTest.unity`, корень `T4 - Сессии сервисов`. Исходная SampleScene не менялась этим этапом.

Настройка: `Tools/Character Lab/Set Up T4 Service Sessions`. Повторный запуск не создаёт дубликаты и не переставляет существующий ряд: авторские изменения сохраняются. Только открытая CharacterTest в Edit Mode. Проверка физического доступа: `Tools/Character Lab/Check T4 Service Access (Play)` / `ServiceSessionsT4Checks.Run()`; временные объекты всегда удаляются.

## Полигон и модели

Тестовый прилавок торговой сессии: **X28, Z−29**. Верстак: **X23, Z−29**. Таблички: соответствующие X, **Y0.035, Z−27.4**, лицом к подходу из центра. Прилавок перенесён с X18, чтобы не накладывать новую табличку на старую инструкцию этапа 10; прежняя зона оружия сохранена.

Unity Search нашёл `Assets/Polygon-Lite Survival Collection/Prefabs/SM_Workbench.prefab` и `Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Crafting Animations/Prefabs/Scenario/Human_Table.prefab`. Поиск trader-prefab ничего не вернул: прилавок не объявляется готовым торговым NPC. Модели вложены в наши обёртки; их legacy-материалы заменены renderer overrides на существующий `Assets/CharacterTesting/Materials/Concrete.mat` (URP/Lit). Vendor-ассеты не редактировались. У прилавка собственный BoxCollider; у верстака сохранён исходный non-convex MeshCollider.

## API и доступ

- `PlayerServiceSession.TryOpen(ServicePoint)` требует живой активный мотор, effective `AcceptingGameplayInput`, `PlayerInteractor.IsAvailable` (включая ограничения воздуха/позы), фокус при включённой настройке, доступную точку и отсутствие собственной открытой сессии.
- `ServicePoint.CanAccess(PlayerInteractor, out reason)` независимо проверяет разрешение Interaction Enabled, жизнь/активность игрока и необязательного Health владельца сервиса, доступную камеру/Collider, дистанцию и препятствия. Во время меню он намеренно не требует `AcceptingGameplayInput`: ввод заблокирован самой сессией.
- `ValidateAccess(out reason)` повторяет текущую физическую проверку. Для будущих операций использовать **`ValidateAccess(expectedPoint, out reason)`**: устаревший callback от Trader A не получает разрешение в новой сессии Trader B. Версии котировок и атомарные сделки — будущий Т6, не часть Т4.
- `Close()` идемпотентен и освобождает только lock этой сессии. Удаление/выключение точки или коллайдера, смерть/выключение игрока, препятствие, отход, пауза, потеря фокуса закрывают окно; Escape / Gamepad buttonEast и кнопка «Закрыть» также закрывают его. Мир и гравитация не останавливаются.

Дистанция по умолчанию 2.5 м до поверхности, а не центра модели. Открытие через E ограничено **обоими** параметрами: Interaction Distance игрока и Access Distance точки; увеличение только Access Distance не удлиняет луч E. Продолжение сессии/операции используют Access Distance точки.

ClosestPoint вызывается только для Box/Sphere/Capsule/convex Mesh. Для non-convex Mesh, Terrain и прочих Collider выполняется Collider.Raycast к центру bounds, затем к ближайшей границе bounds: только реальное попадание в поверхность разрешает доступ. Если оба луча промахнулись на сложной форме, отказ консервативный; назначьте простой авторский accessCollider в обёртке. Bounds сами по себе не дают разрешения. Луч препятствий исключает собственные коллайдеры игрока и точки, игнорирует триггеры; при полном буфере попаданий доступ отклоняется.

Настройки находятся в существующем `Tools/Zombie Survival/Настройки персонажа и зомби`, вкладка **Инвентарь**: Общая сессия сервиса / Доступ к торговой сессии / Доступ к сессии верстака. Настройки не копируются. Прилавок без ActorIdentity можно назначить в поле «Актёр». Новые serialized-поля имеют русские Tooltip/InspectorName.

## Владение вводом

FPC хранит независимый legacy base gate и множество владельцев `SetGameplayInputLock(Object owner, bool locked)`. Inventory, Progression и ServiceSession снимают только свой lock. Уничтоженные владельцы очищаются; снятие одного не включает ввод поверх другого или при base=false. Меню не вызывают legacy SetGameplayInputEnabled для восстановления старого bool.

После последнего unlock кнопки Player map должны быть отпущены: Attack/Interact/Jump/Aim и другие Button actions не превращаются в отложенное действие. WASD Value отпускать не нужно. UI close action независим от gameplay map; пользовательские bindings клонируются, fallback buttonEast добавляется один раз. При новом Play, включая отключённые Domain/Scene Reload, сессия закрывается и UI input переинициализируется.

Для Т5/Т10 развивать существующие точки (или композицию с ними), не добавлять второй IInteractable на тот же корень. Общая PlayerServiceSession остаётся владельцем доступа/закрытия, будущие каталоги и ремонт — отдельными сервисами.

## Подтверждённые проверки 18.10.2026

- Два Play с DisableDomainReload + DisableSceneReload. E открыл прилавок в первом и реальный non-convex верстак после recompile во втором; Escape закрыл, новое начало — без открытой сессии/старого lock.
- Семь проверок физического доступа PASS: близкая видимая точка, дальность, стена, disabled component, inactive object, disabled Collider, Interaction Enabled=false. Прямой API не обходит стену/дальность.
- Во время открытого окна ValidateAccess=true при GameplayInputEnabled=false; курсор свободен, время мира растёт. Inventory.Open отказал. Неверный expectedPoint отклонён.
- Два owner-lock и снятие одного оставляют ввод заблокированным. Base=false + Close также не включает его.
- Held-тест в Dynamic Input System context зафиксировал E=true и Attack=true до Close, blocked=true (кадр 29334). До явного release ввод оставался выключен. После release и реальных кадров он возобновился, сессия не открылась снова, сериализованные слоты/магазины не изменились: автоматического использования не было. Editor eval читает отдельное Editor input state, поэтому его нулевой readback не считался доказательством отпускания. Одноразовый callback удалён; ручной InputSystem.Update не вызывался.
- Открытые сессии закрылись после отключения точки, отхода на 7 м, появления стены, удаления временной точки и реального смертельного урона игроку. Focus-loss callback с requireFocus=true тоже закрыл окно (автоматическая имитация callback, не физическое переключение ОС).
- HP/Alive, положение/поворот, флаги фокуса/захвата, runInBackground и ракурс SceneView восстановлены. Временные объекты/устройства/callback удалены. Reload-настройки не менялись.

Кадры: `Temp/pipeline-screenshots/t4-service-ui.png` — настоящее меню GameView, просмотрено координатором; `Temp/pipeline-screenshots/screenshot_scene_20261018_163003_832.png` — финальные URP-модели и разнесённые таблички, без розовых материалов. Первый кадр сделан до исправления legacy-материалов; окончательный внешний вид — второй.

Ручная регрессия: подойти к каждой станции с севера, E; проверить свободный курсор и отсутствие движения/боя, Escape; удержать E/ЛКМ при закрытии и отпустить перед новым действием. Через Inspector во время Play выключить точку, вставить стену или переместить игрока: окно должно закрыться. Открыть Tab/K, проверить взаимное исключение и возврат управления. Для сложных Mesh проверять реальные подходы с нужной стороны.

Финал: CharacterTest и префаб игрока сохранены; Editor оставлен в Edit Mode, scene dirty=false. Компиляция completed/failed=false; groundTruth Console: 0 ошибок, 0 предупреждений. Обычный git diff --check для кода/Docs и проверка сериализованных файлов с core.whitespace=-blank-at-eol прошли. Штатный YAML Unity вручную не редактировался. Подход X28 проверен: свободный объём, опора Floor 64 x 64 m. Коммит/пуш не выполнялись; пользовательские WorldText/SampleScene/Maps/Textures сохранены без вмешательства.

## Если Handyman не применяется к торговцу

После обновления проекта были нарушены GUID/importer-зависимости Handyman. 19.10.2026 восстановлены 33 метаданных из рабочего `99fe3c7`, повторный импорт и изолированная замена подтверждены 13 проверками. Точные пути, резервная копия, результаты и ограничения: [HandymanRepair.md](HandymanRepair.md).

Выберите корень **T4TraderSession → ServiceVisual → Префаб модели**, назначьте **Assets/Bubba_The_Handyman/URP/Prefabs/Handyman_ver_1.prefab**, оставьте **Material Override=None** и при необходимости нажмите **Применить модель**. Живой экземпляр торговца в сцене пользователя этим ремонтом не заменялся. Исходные prefab не содержат Animator Controller: наличие клипов само по себе не включает idle-анимацию. HDRP-варианты не подходят текущему PC_RPAsset/URP.

Сцены не сохранялись/не закрывались. Assets/1.unity осталась dirty; CharacterTest после импорта/проверок также стала dirty, без изменения файла на диске. Причина конкретных in-memory отличий не установлена, флаг не сбрасывался ради сохранения возможных пользовательских изменений.

## Расстановка модели без сброса Transform

19.10.2026 исправлена безусловная перезапись дочернего Transform. Поля ServiceVisual теперь абсолютные локальные, как в Transform; существующая фактическая поза захватывается до миграции/Configure, явное изменение полей применяется, неизменные поля не затирают ручную посадку. Корень свободно размещается в мире, смена prefab/материала сохраняет выбранную позу. При прямой замене nested prefab и несовпадении VisualPrefab Inspector предупреждает без автоматической подмены источника. Контракт, тесты 9+13 и отдельный Play 5, сохранность пользовательских сцен: [ServiceVisualPlacement.md](ServiceVisualPlacement.md).
