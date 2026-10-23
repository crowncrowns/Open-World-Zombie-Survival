# АК-74 — проверка игрового подключения, 23.10.2026

Unity6000.6.3f1, отдельная копия `work/ContextHud/UnityProject`. Основной редактор не закрывался, Main Play не запускался. Инструкция/контракты: `Docs/AK74Gameplay.md`.

Последующая проверка именно основного редактора и исправление28 ошибок описаны в [MainConsoleFix.md](MainConsoleFix.md): дублирующий исходный пакет вынесен из Assets, новые скрипты зарегистрированы заново, обе main-сборки exit0, фактическая Console0errors/38warnings/1log.

- `Play.log`: итоговые36PASS,7успешных выстрелов, gameplay Errors/Exceptions=0. Содержит только относящиеся к проверке строки, без служебных данных лицензирования Unity. E/R/LMB/hold/RMB/Tab — виртуальные устройства InputSystem, не ОС-клавиатура/мышь. Drop — публичный API инвентаря, Q отдельно не проверялась.
- `Assets.log`:77PASS, отсутствие missing scripts/materials/PBR,3LOD, аудио/каталог, сохранность пистолетного mount, идемпотентный повтор Setup.
- `PlayChecks.cs.txt`, `AssetChecks.cs.txt`: временные проверки из Recovery; не копировать в Assets основного проекта.
- `Hip.png`, `ADS.png`, `Reload.png`: реальные кадры GameView1002×879, нейтральная стойка. Финальный Aim viewport(.4960,.5018), отклонение ствола около1.1°. Кадры просмотрены; крайние углы/движение/пальцы/все фазы reload и финальный аудиомикс требуют отдельной ручной приёмки.
- `SourceRecoveryHashes.json`:112файлов совпадают с переданным результатом. Изменение source CharacterTestPlayer — только2новые строки каталога; сцены CharacterTest/SampleScene совпали с началом работы, Interiors не менялась.
- `ImportRepair.json`:99устаревших внутренних ссылок установленной модели восстановлены по unitypackage, текущие GUIDheaders/fileID сохранены. Исходный пакет не изменён; backup в work/AK74Gameplay/ImportRepair/files.

В Recovery до начала сценария возникает прежняя ошибка индекса `UnityEditor.Search.SearchDatabase.ArgumentOutOfRangeException`. Она явно исключена из runtime-счётчика, не объявлена исправленной. Компиляция игрового кода прошла; временные probes дают обычные obsolete warnings Unity6 о старом overload поиска объектов. Старые пакеты также могут выдавать предупреждения импорта; утверждения «вся консоль чистая» нет.

Сценарий проверяет общие hitscan/factions/Health, магазин/износ, оба reload/отмену/скрытие, отдельный аудиоканал, смену на пистолет, Drop/E, Save/Load/repeat Load и защиту от stale handles. Новая торговля/баллистика/гильзы/streaming, холодная загрузка и полный населённый мир этим прогоном не проверяются.
