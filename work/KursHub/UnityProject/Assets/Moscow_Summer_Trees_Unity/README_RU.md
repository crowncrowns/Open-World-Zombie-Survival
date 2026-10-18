# Moscow Summer Trees | Unity / Blender

**32 уникальных дерева:** 8 видов × 4 разных формы. 96 GLB: LOD0, LOD1, LOD2 для каждого дерева.

Виды: Birch (берёза), Oak (дуб), Pine (сосна), Spruce (ель), Linden (липа), Maple (клён), Aspen (осина), Rowan (рябина).
Варианты: TallMature (высокое взрослое), Young (молодое), Weathered (старое/асимметричное), Dense (густое).
Все размеры в метрах, Y — вверх, основание на Y=0. Среда — реалистичный летний лес Московской области.

## Где файлы
- `Models/LOD0`: 32 полноценных 3D-модели (ветви отдельными подвижными узлами)
- `Models/LOD1`: 32 облегчённых модели
- `Models/LOD2`: 32 упрощённых модели с перекрёстными плоскостями листвы
- `Textures`: PNG коры и листвы по 8 видам (модели GLB содержат встроенные текстуры)
- `UnityScripts/MoscowTreeWind.cs`: анимация деревьев от ветра, настройки в Unity Inspector
- `UnityScripts/MoscowTreePrefabBuilder.cs`: редакторский помощник для создания префабов
- `UnityScripts/MoscowTreeLODBuilder.cs`: автосборка LODGroup (3 уровня + ветер + коллайдер)
- `Tools/Blender_Convert_GLB_to_FBX.py`: перевод GLB→FBX через Blender
- `TreeCatalog.json`: перечень всех ассетов, высоты, радиусы крон, треугольники

## Быстро открыть в Blender
File → Import → glTF 2.0 (.glb/.gltf) → выбери дерево из Models/LOD0.
Дерево состоит из настоящих 3D-мешей, а не только концепт-изображения. Узлы `Wind_Trunk` и `Wind_Branch_XX` являются точками вращения анимации и должны сохраниться при экспорте.

## Unity: рекомендуемый путь через FBX
1. Открой Blender и выполни Tools/Blender_Convert_GLB_to_FBX.py. В скрипте укажи путь к этой распакованной папке (описание в начале скрипта). Появится `Models_FBX`.
2. Перетащи нужные FBX в папку `Assets/MoscowSummerTrees/Models` Unity.
3. Скопируй `MoscowTreeWind.cs` в `Assets/MoscowSummerTrees/Scripts`.
4. Скопируй `MoscowTreePrefabBuilder.cs` строго в `Assets/Editor`.
5. Выдели деревья FBX во вкладке Project и выбери `Tools → Moscow Trees → Create Wind Prefabs From Selection`.
6. Готовые префабы окажутся в `Assets/MoscowTreePrefabs`. Перетащи их в сцену.
7. Для автоматического LODGroup положи FBX в `Assets/MoscowSummerTrees/Models/LOD0`, `LOD1`, `LOD2` с исходными названиями, скопируй `MoscowTreeLODBuilder.cs` в `Assets/Editor`, выдели в Unity все 32 модели из папки LOD0 и выбери `Tools → Moscow Trees → Create LOD Prefabs From Selected LOD0 Models`. Префабы появятся в `Assets/MoscowTreeLODPrefabs`. В компоненте `MoscowTreeWind` в Inspector включай ветер и меняй его направление, скорость, силу и порывы.

## Unity: без Blender
В Unity установи пакет glTFast, затем импортируй модели GLB через его редакторский импортёр. Способ зависит от версии glTFast. Проверь, что узлы `Wind_Trunk` / `Wind_Branch_*` сохранились как Transform; затем добавь `MoscowTreeWind` на корневой объект.

## Направление и настройки анимации
- `Enable Wind` — включить/выключить движение веток и ствола
- `Wind From` — **ОТКУДА** дует ветер: North/NorthEast/East/SouthEast/South/SouthWest/West/NorthWest/Custom.
- `Custom Travel Direction` — собственный мировой вектор **КУДА** дует ветер (работает при `Custom`); Y игнорируется
- `Wind Strength` — сила (0..3); `Wind Speed` — скорость циклов
- `Gustiness`, `Gust Frequency` — амплитуда и частота порывов
- `Trunk Bend Degrees`, `Branch Bend Degrees` — углы качания в градусах
- `Variation Seed` — различие фаз одинаковых деревьев
- `Animate In Edit Mode` — опциональный предпросмотр вне Play; обычно выключен

По умолчанию +Z считается севером, +X востоком. North значит ветер идёт с +Z в сторону -Z.
Анимация работает за счёт преобразования Transform-узлов, а не шейдера, совместима с Built-in/URP/HDRP.

## LOD и производительность
LOD0/LOD1/LOD2 — разные модели. Скрипт `MoscowTreeLODBuilder.cs` может автоматически собрать три уровня внутрь одного префаба с LODGroup: 0.55, 0.20, 0.035. Для сотен деревьев переходи на GPU-wind shader и GPU-instancing, так как анимация трансформов для множества деревьев требует CPU.
Используй CapsuleCollider только на стволе. У листвы нет необходимости включать MeshCollider.

## Ограничения первой версии
Это процедурный **игровой ассет (mid-poly)** с карточками листвы, а не ботаническое фотосканирование. Фолиаж на карточках использует alpha mask; сохранение альфа-канала и two-sided зависит от настроек материала при импорте в конкретную Unity render pipeline. Если листья стали квадратами, включи Alpha Clipping и Double-sided для материала листвы. Для FBX после импорта может понадобиться ручная привязка PNG и альфы.
Движение — процедурная анимация в Unity при запуске сцены, а НЕ baked animation clips внутри GLB/FBX. Для игрового ветра это удобнее: параметры сериализуются в префабе/Inspector.
