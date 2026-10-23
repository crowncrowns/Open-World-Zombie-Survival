# Исправление 28 ошибок основного проекта — 23.10.2026

Проверен именно открытый пользовательский Unity6000.6.3f1, процесс27176, CharacterTest. Главный журнал находится в `Logs/Editor.log`, обычный AppData Editor.log лишь перенаправляет к нему. Play не запускался, редактор не завершался, сцену не сохраняли.

До исправления фактически наблюдались28 красных сообщений Console:

- 26 CS0101/CS0579/CS0111: runtime AK74Presentation/AK74ResetPresentationOnExit включены в Assembly-CSharp одновременно из Assets/AK74_Reference/Unity/Assets/Weapons/AK74_Reference и Assets/Weapons/AK74_Reference. Две пары Editor-классов также дублировались и создали бы следующий конфликт после runtime-сборки.
- 2 CS0246: новый WeaponHandPose отсутствовал во входном ScriptCompilationData/SourceFiles; WeaponModelAnimation и два новых Editor Setup также отсутствовали в своих source lists. Существование файла/MonoScript в Project и импорт QuickSearch не доказывали включение в компиляцию.

Исправление:

1. Проверены154 GUID всей исходной папки, внешних ссылок из Assets нет. Gameplay использует установленный prefab GUID9fab17b1302f7a745a482b3cc5d4ae35.
2. Assets/AK74_Reference и sibling .meta перенесены в work/AK74Gameplay/SourcePackage-20261023. Хеши всех284 файлов совпали, ничего из исходного комплекта не утрачено. Манифест: SourcePackageManifest.json.
3. Четыре новых .cs.meta получили стандартный MonoImporter с прежними GUID. Refresh и обычный Reimport по отдельности не исправили script source list; временный перенос4 .cs+.meta вне Assets, Refresh, точный возврат и ещё один Refresh восстановили регистрацию. Переносы/хеши сохранены в work/AK74Gameplay/ReimportScripts.json, исходные meta — CompileFixMetaBefore. Функциональный код не менялся.
4. Main .rsp теперь содержит WeaponHandPose/WeaponModelAnimation и оба Editor Setup; архивные пути в .rsp отсутствуют. Обе Csc сборки завершились с exit0: MainCompilationAfter.json. До исправления: MainErrorsBefore.txt.

В открытой Console после завершения импорта визуально подтверждены **0 errors,38 warnings,1 log**. Clear не нажимался. Существующие предупреждения импорта/шрифтов не объявлены исправленными. Все112 файлов прежнего игрового/ассетного прогона остались с прежними хешами; повторять Play ради перемещения дубля и регистрации файлов не потребовалось.

Не возвращать исходную распакованную папку в Assets при последующих передачах. Проверка отдельной копии должна учитывать полный набор компилируемых main-скриптов, включая вложенные папки пакетов; минимальная проверочная копия сама по себе не доказывает чистую сборку основного проекта.
