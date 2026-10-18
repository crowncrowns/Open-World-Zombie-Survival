# Диагностика Input System после обновления проекта — 18.10.2026

## Что обнаружено и что исправлено

Проверен checkout Open-World-Zombie-Survivalgit после обновления до 4004181, Unity 6000.6.3f1, Input System 1.20.0. Ошибок C# компиляции не было: исходный compilationFailed=false. Две записи Console были assertion Input System, а не CS:

- Action count in old and new state must be the same;
- Binding count in old and new state must be the same.

Обе появились в 07:49:04 UTC при стартовом OnNativeDeviceDiscovered. Editor.log показывает импорт Assets/InputSystem_Actions.inputactions на initial refresh, завершение загрузки проекта и затем эти assertion в RestoreActionStatesAfterReResolvingBindings. Это подтверждённая граница возникновения — старт/импорт/обнаружение устройства; точный внутренний триггер пакета не воспроизведён. Нельзя объявлять их доказанным багом хотбара, FirstPersonController или исправленным runtime-дефектом.

Исходные stacktrace сохранены в Temp/InputDiagnostics/startup-assertions-20261018.log. До проверок текущий project-wide asset и его resolved state уже совпадали: 31 actions, 89 bindings. Runtime структурного добавления действий/привязок в работающие карты не найдено; respawn template получает gamepad binding при создании выключенного действия.

Реальная правка: 15 вызовов устаревшего FindObjectsByType(..., FindObjectsSortMode.None) заменены поддерживаемыми overload без SortMode в восьми наших Editor-файлах. Все прежние вызовы были None, зависимости от сортировки не было. Устранены 30 диагностик CS0618, предупреждения не подавлялись.

Файлы: ActorAudioValidation.cs, ActorContractValidation.cs, ActorIntegrationSetup.cs, InventoryAndCrawlValidation.cs, PlayerHealthSetup.cs, PlayerInteractionSetup.cs, PlayerStageFiveSetup.cs, WorldSpacePresentationSetup.cs (все в Assets/Editor).

## Выполненные проверки и границы вывода

После перекомпиляции compilationFailed=false, фактическая Console показала 0 errors / 0 warnings. ClearConsole и перезапуск Editor не применялись. Pipeline retained buffer продолжал хранить две исторические assertion и 30 старых предупреждений; это история, а не текущий groundTruth.

Проверены подключение/удаление временных Keyboard/Mouse в Edit Mode, Gamepad во время Play, два последовательных Play с исходными отключёнными Domain Reload и Scene Reload. Новых assertion не зарегистрировано.

В первом Play QueueStateEvent подтвердил движение W, выбор Digit2 и колесо, открытие K и закрытие Escape с переключением gameplay map. Некоторым последующим пробам не удалось доставить клавиши: control state сам по себе не доказывал performed. Поэтому провели контроль доставки, а не стали менять игровую архитектуру.

Во втором Play новая virtual Keyboard, созданная уже в Play, без ручного InputSystem.Update, дала performed-события Digit2 и Tab в одной временной диагностической карте: счётчики 1/1, хотбар выбрал индекс 1, рюкзак открылся и выключил gameplay. После Dispose диагностической карты возвращены обычные исходные template.Clone: Tab открыл рюкзак, Escape закрыл и восстановил gameplay; исходный PlayerProgressionMenu открылся на K и закрылся Escape. Временные constructor/Clone/map сравнения не переносились в продукт.

Попытки OS keypress через Computer Use не дали подтверждённых performed-событий в контрольном map, включая Digit2. Поэтому они не являются доказательством неисправности реальной пользовательской клавиатуры. Успешно проверен путь Input System events → исходные menu/hotbar компоненты; не заявляется полная проверка аппаратного ввода пользователя. Runtime-код ввода оставлен без спекулятивных изменений.

Все probe-устройства удалены, callbacks диагностических карт сняты через Dispose, оригинальные clones возвращены. Исходные флаги фокуса/захвата курсора восстановлены, Play остановлен. Позиция игрока вернулась к (0; 0,05; −24), пользовательские сцены и InputActions GUID не менялись.

## Памятка второму разработчику и ИИ

1. Перед внешней сменой checkout/InputActions выйти из Play. Дождаться завершения импорта и перекомпиляции, затем проверять Console groundTruth. Не менять GUID существующих actions/bindings.
2. Не перестраивать включённую runtime-карту и не редактировать общий asset как управление отдельным игроком. Изменения структуры выполняются на выключенной собственной карте до Enable; компоненты получают актуальные ссылки из её владельца.
3. Меню должны работать при выключенной movement map. Не переносить их toggle на карту, которую это же меню выключает.
4. При диагностике различать историю retained log и текущую Console, C# errors и runtime assertion. Сначала доказать доставку события через performed/control, затем проверять WasPressedThisFrame и Update/gates. Не подменять product-код ради неработающей доставки теста.
5. Для no-domain/no-scene reload проверять минимум два Play, повторный enable, меню и hotbar. После проб удалять virtual devices, Dispose временные карты/actions, возвращать overrides и фокусные настройки. Не сохранять диагностическое состояние сцены.
6. Если стартовый assert повторится, сохранить полный stacktrace, время, последовательность import/device events и counts до повторного разрешения. Не править vendor/package cache или вводить глобальный postprocessor без воспроизводимого случая.

Код Т3 (Wallet/TraderBudget) уже присутствует в checkout, но завершение этапа и его интеграционные проверки этой диагностикой не подтверждаются. Не создавать второй кошелёк и не объявлять Т3 готовым. Дальнейшая работа Т3 остановлена по смене приоритета пользователя. Wallet guard сохранения сохранён: частичный откат предметов без согласованной экономики остаётся запрещённым. Source InputActions, runtime-инвентарь, хотбар, развитие и сохранения этой правкой не менялись.
