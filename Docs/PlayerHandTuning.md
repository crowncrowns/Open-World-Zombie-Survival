# Настройка рук и пистолета на игроке

21.10.2026. Дополнение к PlayerPistolPresentation.md и PlayerUALAnimations.md.

23.10.2026: отдельные модели могут явно назначать `WeaponDefinition.handPose` (WeaponHandPose). АК-74 использует собственный профиль и винтовочные клипы тела: [AK74Gameplay.md](AK74Gameplay.md). Корневые параметры ниже остаются источником пистолета и другого оружия без профиля; runtime Animator bridge по-прежнему не становится источником авторских значений.

## Где редактировать

Выбрать корневой CharacterTestPlayer в сцене либо открыть Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab. Настройки доступны до Play, менять скрипты и искать кости не требуется.

- Player UAL Animation Driver → «Руки и пистолет — положение относительно камеры»: Firearm Hip Position, Firearm Aim Position, Left Firearm Grip, Aim Weight, Firearm Elbow Hint. Ниже — готовность руки с оружием ближнего боя.
- Player Weapons → «Полное тело — крепление оружия»: Firearm Hand Position и Firearm Hand Euler задают посадку пистолета в правой ладони. Hand Bound Visual включён, Hand Attachment пустой для текущего Survivalist.

Hip/Aim Position: X вправо, Y вверх, Z вперёд от камеры, в метрах. Left Firearm Grip задаётся в локальных осях пистолета (+X вдоль ствола), mount — в осях кисти. Elbow Hint: X наружу от корпуса для каждой руки, Y вверх, Z вперёд; исходное значение (0.6, 0.2, 0.2). Длина рук ограничивает фактическую достижимую позу. При крутом взгляде вверх HIP автоматически приближается к центру; это не переключение игрового ADS.

В Edit изменения сохраняются обычным сохранением сцены или prefab. Сценовые overrides могут перекрыть значения prefab. В Play поля обновляют хват сразу, включая освобождённый курсор. Для переноса Play-калибровки: меню нужного корневого компонента → Copy Component, остановить Play, тот же компонент → Paste Component Values, сохранить. Player Weapons и Player UAL Animation Driver копировать отдельно, если менялись оба. Автоматической записи Play-состояния в сцену нет.

## Контракт для второго разработчика и ИИ

Единственный авторский источник семи hand-pose полей — PlayerUALAnimationDriver на корне игрока. На каждом OnAnimatorIK он передаёт значения через ApplyHandSettings/SetHandSettings в transient PlayerUALAnimatorIK текущего Animator. Поля bridge сохранены для совместимости/диагностики и скрыты в Inspector; не предлагать редактировать их и не сохранять runtime bridge в модель ради калибровки. ResolveAnimator/SetModel сбрасывают transient solver, но не авторские параметры. Existing root PlayerWeapons остаётся единственным источником mount. PlayerUALSetup.ConfigurePlayer не должен сбрасывать новые пользовательские hand-pose значения.

Освобождение курсора Esc и потеря фокуса блокируют игровой ввод, но сохраняют ready-представление оружия и последнее HIP/ADS. PlayerWeapons.CanAct и motor.AcceptingGameplayInput по-прежнему обязательны для стрельбы/перезарядки; CanPresent лишь разрешает видимость. Pending-удар и reload при потере игрового ввода отменяются, armed=false защищает от выстрела при возврате. Owner/base locks (инвентарь/торговля/верстак), смерть, спринт и паркур сохраняют прежние ограничения представления. Не заменять gameplay-gate на presentation-gate в операциях с предметами.

Форматы сохранений, ID, экономика, damage/ammo и импортированные анимации не меняются. Настройки хвата — авторские свойства сцены/prefab, не часть сохранения мира. Не запускать Main Play ради тестов; использовать отдельную Recovery-копию. Эта задача не означает завершения прежней приёмки ткани при moving/up-ADS: известный near-plane случай отслеживается отдельно в PlayerPistolPresentation.md.

## Проверка

PlayerHandTuningChecks.RunPlay прошёл 30 PASS, без FAIL, в отдельном Recovery Play. Реальные кадры 33→988: семь root-полей перенесены в фактический IK, настоящий ввод Esc проверен для HIP и ADS, редактирование при свободном курсоре применено, TryAttack/TryReload отвергнуты без изменения магазина/прочности, owner-lock убирает представление, после отпускания кнопок и явного захвата курсора оно возвращается. Потеря фокуса проверена вызовом настоящего OnApplicationFocus(false), а не переключением окна ОС. Пистолет fixture был создан пустым: неизменность магазина сама по себе не доказывает отказ заряженного оружия; одновременно явно проверялся закрытый AcceptingGameplayInput и false от обоих игровых API. Полная прежняя игровая/cloth матрица в этом проходе не повторялась.

Отчёт: S:/Games/Unity Projects/_CodexRecovery/PlayerHandTuning-20261021/hand-tuning-checks.txt. Компиляция Main и Recovery без ошибок; фактическая консоль Main — 0 errors (14 прежних warnings), Recovery — 0 errors (11 warnings). Main Play не запускался, dirty-сцена осталась открытой; перед работой сохранена её нативная копия CharacterTest-before.unity вне Assets. В source CharacterTestPlayer.prefab нативно сохранены только семь новых сериализованных полей относительно резервной копии этого прохода; существующие mount и пользовательские значения не сбрасывались. Тестовая сцена остановлена, auto-tick Recovery выключен, временные объекты/устройства убраны. Ни коммита, ни push нет.
