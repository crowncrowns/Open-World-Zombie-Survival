using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

<<<<<<< Updated upstream
/// <summary>Builds reusable static props, then nests their prefabs in the hub.</summary>
=======
/// <summary>Builds reusable static props; the hub contains linked instances of this library.</summary>
>>>>>>> Stashed changes
public static class KursHubPropSetup
{
    public const string Folder = "Assets/Environment/KursHub/Props";
    public const string ManifestPath = Folder + "/KursHubPropsManifest.json";
<<<<<<< Updated upstream
    public const string PrefabsFolder = Folder + "/Prefabs";
=======
    public const string PrefabFolder = Folder + "/Prefabs";
    public const string PlacementGroup = "Exterior props";

    [Serializable] public sealed class Placement
    {
        public string id;
        public string prop;
        public float[] position;
        public float[] rotation;
        public float[] scale;
    }
>>>>>>> Stashed changes

    [Serializable] private sealed class Chunk
    {
        public string name;
        public string material;
        public bool collider;
    }

    [Serializable] private sealed class Prop
    {
        public string id;
        public string model;
<<<<<<< Updated upstream
        public string collection;
        public float[] sourceOffset;
        public Chunk[] chunks;
    }

    [Serializable] private sealed class Manifest { public Prop[] props; }

    [Serializable] public sealed class Placement
    {
        public string id;
        public string prop;
        public float[] position;
        public float[] rotation;
        public float[] scale;
    }

    private static Prop[] Read()
    {
        if (!File.Exists(ManifestPath))
            throw new InvalidOperationException("Нет каталога props: " + ManifestPath);
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
        if (manifest == null || manifest.props == null || manifest.props.Length == 0)
            throw new InvalidOperationException("Каталог props пуст или повреждён.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in manifest.props)
        {
            if (prop == null || !AssetName(prop.id) || !ids.Add(prop.id)
                || string.IsNullOrEmpty(prop.model) || prop.chunks == null || prop.chunks.Length == 0)
                throw new InvalidOperationException("Некорректное или повторное описание prop в manifest.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var chunk in prop.chunks)
                if (chunk == null || string.IsNullOrEmpty(chunk.name) || string.IsNullOrEmpty(chunk.material) || !names.Add(chunk.name))
                    throw new InvalidOperationException("Некорректная или повторная часть prop: " + prop.id);
        }
        return manifest.props;
    }

    private static bool AssetName(string name) => !string.IsNullOrWhiteSpace(name)
        && name != "." && name != ".." && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && !name.Contains("/") && !name.Contains("\\");

    private static string ModelPath(Prop prop)
    {
        var path = prop.model.Replace('\\', '/');
        if (!path.StartsWith("Assets/", StringComparison.Ordinal)) path = Folder + "/" + path;
        if (!path.StartsWith(Folder + "/Models/", StringComparison.Ordinal)
            || path.Split('/').Any(part => part == ".." || part == ".")
            || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Модель prop должна находиться в " + Folder + "/Models: " + path);
        return path;
    }

    private static string PrefabPath(string id) => PrefabsFolder + "/" + id + ".prefab";

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var split = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }

    private static Transform DirectChild(Transform parent, string name)
    {
        Transform result = null;
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name != name) continue;
            if (result != null) throw new InvalidOperationException("Повторное имя дочернего объекта: " + name);
            result = child;
        }
        return result;
    }

    private static void RequireSource(GameObject instance, string assetPath)
    {
        var actual = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
        if (!PrefabUtility.IsAnyPrefabInstanceRoot(instance) || actual != assetPath)
            throw new InvalidOperationException("У " + instance.name + " другой источник (" + actual
                + "). Авторский объект оставлен без замены; ожидается " + assetPath);
    }

    public static Dictionary<string, GameObject> BuildLibrary(Dictionary<string, Material> materials)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Сборка props доступна только вне Play.");
        if (materials == null) throw new ArgumentNullException(nameof(materials));
        var props = Read();
        // Check the complete palette before modifying any existing prefab.
        foreach (var prop in props)
            foreach (var chunk in prop.chunks)
                if (!materials.TryGetValue(chunk.material, out var material) || material == null)
                    throw new InvalidOperationException("Нет материала " + chunk.material + " для " + prop.id);
        EnsureFolder(PrefabsFolder);
        var library = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        foreach (var prop in props)
        {
            var path = PrefabPath(prop.id);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var identities = existing == null ? null : Identities(existing);
            var previousGuid = existing == null ? null : AssetDatabase.AssetPathToGUID(path);
            var modelPath = ModelPath(prop);
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Нет FBX prop: " + modelPath);
            importer.globalScale = 1;
            // Single-mesh props retain their named chunk below the model root;
            // renaming the nested model to Shell must not rename that mesh node.
            importer.preserveHierarchy = true;
=======
        public Chunk[] chunks;
    }

    [Serializable] private sealed class Manifest
    {
        public int version;
        public Prop[] props;
    }

    private static Manifest ReadManifest()
    {
        if (!File.Exists(ManifestPath))
            throw new InvalidOperationException("Нет manifest библиотеки предметов ТЦ Курс: " + ManifestPath);
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
        if (manifest == null || manifest.version != 1 || manifest.props == null || manifest.props.Length == 0)
            throw new InvalidOperationException("Неизвестный или пустой manifest библиотеки предметов ТЦ Курс.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in manifest.props)
        {
            if (prop == null || string.IsNullOrWhiteSpace(prop.id) || !prop.id.StartsWith("KH_", StringComparison.Ordinal)
                || prop.id.IndexOfAny(new[] { '/', '\\', ':', '.' }) >= 0 || !ids.Add(prop.id))
                throw new InvalidOperationException("Некорректный или повторный ID предмета в manifest.");
            if (string.IsNullOrWhiteSpace(prop.model) || !prop.model.StartsWith(Folder + "/Models/", StringComparison.Ordinal)
                || prop.model.Contains("..") || !prop.model.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                || prop.chunks == null || prop.chunks.Length == 0)
                throw new InvalidOperationException("Некорректная модель предмета: " + prop.id);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var chunk in prop.chunks)
                if (chunk == null || string.IsNullOrWhiteSpace(chunk.name) || string.IsNullOrWhiteSpace(chunk.material)
                    || !names.Add(chunk.name))
                    throw new InvalidOperationException("Некорректная или повторная часть предмета: " + prop.id);
        }
        return manifest;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Некорректный каталог Assets: " + path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static string PrefabPath(string id) => PrefabFolder + "/" + id + ".prefab";

    public static Dictionary<string, GameObject> BuildLibrary(Dictionary<string, Material> materials)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Сборка предметов доступна только вне Play.");
        if (materials == null) throw new ArgumentNullException(nameof(materials));
        var manifest = ReadManifest();
        foreach (var prop in manifest.props)
            foreach (var chunk in prop.chunks)
                if (!materials.TryGetValue(chunk.material, out var material) || material == null)
                    throw new InvalidOperationException("Отсутствует общий материал предмета: " + prop.id + "/" + chunk.material);
        EnsureFolder(PrefabFolder);
        var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        foreach (var prop in manifest.props)
        {
            var importer = AssetImporter.GetAtPath(prop.model) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Нет FBX предмета: " + prop.model);
            importer.globalScale = 1;
>>>>>>> Stashed changes
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.SaveAndReimport();
<<<<<<< Updated upstream
            var root = existing != null ? PrefabUtility.LoadPrefabContents(path) : new GameObject(prop.id);
            try
            {
                root.name = prop.id;
                root.isStatic = true;
                root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                var shell = DirectChild(root.transform, "Shell");
                if (shell == null)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    if (model == null) throw new InvalidOperationException("Не загружена модель " + modelPath);
                    shell = ((GameObject)PrefabUtility.InstantiatePrefab(model)).transform;
                    shell.name = "Shell";
                    shell.SetParent(root.transform, false);
                }
                else RequireSource(shell.gameObject, modelPath);
                shell.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                shell.localScale = Vector3.one;
                var chunks = prop.chunks.ToDictionary(chunk => chunk.name, StringComparer.Ordinal);
=======
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prop.model);
            if (modelAsset == null) throw new InvalidOperationException("FBX не импортирован: " + prop.model);
            var path = PrefabPath(prop.id);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            var oldGuid = existing ? AssetDatabase.AssetPathToGUID(path) : null;
            // Editing the original contents retains IDs in every referencing hub instance.
            var root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject(prop.id);
            try
            {
                root.name = prop.id;
                var shell = root.transform.Find("Shell")?.gameObject;
                if (shell == null)
                {
                    shell = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                    shell.name = "Shell";
                    shell.transform.SetParent(root.transform, false);
                }
                else if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(shell) != prop.model)
                    throw new InvalidOperationException("Shell ссылается на другую модель; требуется явное согласование: " + prop.id);

                var chunks = prop.chunks.ToDictionary(c => c.name, StringComparer.Ordinal);
>>>>>>> Stashed changes
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var renderer in shell.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!chunks.TryGetValue(renderer.name, out var chunk) || !seen.Add(renderer.name))
<<<<<<< Updated upstream
                        throw new InvalidOperationException("FBX/manifest prop не совпадают: " + prop.id + "/" + renderer.name);
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                        throw new InvalidOperationException("Пустая геометрия prop: " + renderer.name);
                    renderer.sharedMaterials = new[] { materials[chunk.material] };
                    renderer.enabled = true;
                    renderer.gameObject.isStatic = true;
                    renderer.shadowCastingMode = chunk.material == "LampGlass" || chunk.material == "DirtStain" || chunk.material == "Glass"
                        ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    var colliders = renderer.GetComponents<Collider>();
                    if (colliders.Any(collider => !(collider is MeshCollider)) || colliders.Length > 1)
                        throw new InvalidOperationException("Неизвестный или повторный Collider prop: " + renderer.name);
                    if (chunk.collider)
                    {
                        var collider = renderer.GetComponent<MeshCollider>();
                        if (collider == null) collider = renderer.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = filter.sharedMesh;
                        collider.convex = false;
                        collider.isTrigger = false;
                        collider.enabled = true;
=======
                        throw new InvalidOperationException("FBX/manifest предмета не совпадают: " + prop.id + "/" + renderer.name);
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                        throw new InvalidOperationException("Пустая геометрия предмета: " + renderer.name);
                    renderer.sharedMaterials = new[] { materials[chunk.material] };
                    renderer.enabled = true;
                    renderer.shadowCastingMode = chunk.material == "DirtStain" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    var colliders = renderer.GetComponents<MeshCollider>();
                    if (colliders.Length > 1 || renderer.GetComponents<Collider>().Length != colliders.Length)
                        throw new InvalidOperationException("Неожиданные авторские коллайдеры предмета: " + renderer.name);
                    if (chunk.collider)
                    {
                        var collider = colliders.Length == 0 ? renderer.gameObject.AddComponent<MeshCollider>() : colliders[0];
                        collider.sharedMesh = filter.sharedMesh;
                        collider.enabled = true;
                        collider.convex = false;
                        collider.isTrigger = false;
>>>>>>> Stashed changes
                    }
                    else
                        foreach (var collider in colliders) UnityEngine.Object.DestroyImmediate(collider);
                }
                if (seen.Count != chunks.Count)
<<<<<<< Updated upstream
                    throw new InvalidOperationException("FBX prop не содержит все части manifest: " + prop.id);
                if (prop.id == "KH_Floodlight") AddWorkLight(root);
                ValidateProp(root, prop, materials);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException("Не сохранён prop " + path);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Не загружен префаб prop: " + path);
            if (identities != null)
            {
                if (AssetDatabase.AssetPathToGUID(path) != previousGuid)
                    throw new InvalidOperationException("Изменился GUID prop " + prop.id);
                var updated = Identities(asset);
                foreach (var identity in identities)
                    if (!updated.TryGetValue(identity.Key, out var id) || id != identity.Value)
                        throw new InvalidOperationException("Изменился fileID существующего prop: " + prop.id + "/" + identity.Key);
            }
            library.Add(prop.id, asset);
        }
        AssetDatabase.SaveAssets();
        ValidateLibrary();
        return library;
    }

    private static void AddWorkLight(GameObject root)
    {
        var child = DirectChild(root.transform, "Work light");
        if (child == null)
        {
            child = new GameObject("Work light").transform;
            child.SetParent(root.transform, false);
            child.localPosition = new Vector3(0, 0, -.18f);
            child.localRotation = Quaternion.Euler(35, 180, 0);
            var light = child.gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1, .73f, .43f);
            light.intensity = 3;
            light.range = 14;
            light.spotAngle = 100;
            light.shadows = LightShadows.None;
        }
        // Existing authored light settings and optional URP data survive regeneration.
        var existing = child.GetComponent<Light>();
        if (existing == null || existing.type != LightType.Spot)
            throw new InvalidOperationException("Work light должен содержать Spot Light: " + root.name);
        child.gameObject.isStatic = true;
    }

    public static void Place(GameObject root, Dictionary<string, GameObject> library, Placement[] placements, string groupName = "Exterior props")
    {
        if (Application.isPlaying) throw new InvalidOperationException("Расстановка props доступна только вне Play.");
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (library == null) throw new ArgumentNullException(nameof(library));
        CheckPlacementData(placements);
        var group = DirectChild(root.transform, groupName);
        var expected = placements.ToDictionary(placement => placement.id, StringComparer.Ordinal);
        foreach (var placement in placements)
            if (!library.TryGetValue(placement.prop, out var prefab) || prefab == null
                || AssetDatabase.GetAssetPath(prefab) != PrefabPath(placement.prop))
                throw new InvalidOperationException("Неизвестный префаб для размещения: " + placement.prop);
        if (group != null)
            for (var i = 0; i < group.childCount; i++)
            {
                var child = group.GetChild(i);
                if (!expected.TryGetValue(child.name, out var placement))
                    throw new InvalidOperationException("В " + groupName + " есть авторский объект вне manifest: " + child.name + ". Он оставлен без удаления.");
                RequireSource(child.gameObject, PrefabPath(placement.prop));
            }
        if (group == null)
        {
            group = new GameObject(groupName).transform;
            group.SetParent(root.transform, false);
        }
        group.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        group.localScale = Vector3.one;
        group.gameObject.isStatic = true;
        foreach (var placement in placements)
        {
            var child = DirectChild(group, placement.id);
            if (child == null)
            {
                child = ((GameObject)PrefabUtility.InstantiatePrefab(library[placement.prop])).transform;
                child.SetParent(group, false);
                child.name = placement.id;
            }
            child.SetLocalPositionAndRotation(V(placement.position), Quaternion.Euler(V(placement.rotation)));
            child.localScale = V(placement.scale);
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
            PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
        }
        ValidatePlacements(root, placements, groupName);
    }

    public static void ValidateLibrary()
    {
        foreach (var prop in Read())
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(prop.id));
            if (asset == null) throw new InvalidOperationException("Нет префаба prop: " + prop.id);
            ValidateProp(asset, prop, null);
            var importer = AssetImporter.GetAtPath(ModelPath(prop)) as ModelImporter;
            if (importer == null || importer.globalScale != 1 || !importer.bakeAxisConversion
                || importer.importCameras || importer.importLights || importer.importAnimation
                || importer.addCollider || importer.isReadable || importer.materialImportMode != ModelImporterMaterialImportMode.None)
                throw new InvalidOperationException("Неверные параметры импорта prop: " + prop.id);
        }
    }

    private static void ValidateProp(GameObject root, Prop prop, Dictionary<string, Material> materials)
    {
        if (root.transform.localScale != Vector3.one || root.transform.localPosition != Vector3.zero
            || Quaternion.Angle(root.transform.localRotation, Quaternion.identity) > .001f)
            throw new InvalidOperationException("Корень prop должен иметь нулевую позу и единичный Scale: " + prop.id);
        if (root.GetComponentsInChildren<Rigidbody>(true).Length != 0
            || root.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component == null
                || component.GetType().FullName != "UnityEngine.Rendering.Universal.UniversalAdditionalLightData"))
            throw new InvalidOperationException("Статический prop содержит Rigidbody, игровую механику или Missing Script: " + prop.id);
        var shell = DirectChild(root.transform, "Shell");
        if (shell == null) throw new InvalidOperationException("Нет Shell в prop: " + prop.id);
        RequireSource(shell.gameObject, ModelPath(prop));
        if (shell.localPosition != Vector3.zero || shell.localScale != Vector3.one
            || Quaternion.Angle(shell.localRotation, Quaternion.identity) > .001f)
            throw new InvalidOperationException("Изменена импортная поза Shell: " + prop.id);
        var renderers = shell.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length != prop.chunks.Length)
            throw new InvalidOperationException("Неверное число частей prop: " + prop.id);
        foreach (var chunk in prop.chunks)
        {
            var matching = renderers.Where(renderer => renderer.name == chunk.name).ToArray();
            if (matching.Length != 1) throw new InvalidOperationException("Не найдена единственная часть prop: " + chunk.name);
            var renderer = matching[0];
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0 || !renderer.enabled
                || renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial == null
                || (materials != null && renderer.sharedMaterial != materials[chunk.material])
                || (materials == null && renderer.sharedMaterial.name != chunk.material))
                throw new InvalidOperationException("Неверная геометрия или material slot prop: " + chunk.name);
            var colliders = renderer.GetComponents<Collider>();
            if (colliders.Length != (chunk.collider ? 1 : 0))
                throw new InvalidOperationException("Неверное число коллайдеров prop: " + chunk.name);
            if (chunk.collider)
            {
                var collider = colliders[0] as MeshCollider;
                if (collider == null || !collider.enabled || collider.isTrigger || collider.convex || collider.sharedMesh != filter.sharedMesh)
                    throw new InvalidOperationException("Неверный MeshCollider prop: " + chunk.name);
            }
        }
        var lights = root.GetComponentsInChildren<Light>(true);
        if (prop.id == "KH_Floodlight")
        {
            var light = DirectChild(root.transform, "Work light");
            if (lights.Length != 1 || light == null || light.GetComponent<Light>() != lights[0]
                || lights[0].type != LightType.Spot || lights[0].shadows != LightShadows.None)
                throw new InvalidOperationException("Floodlight должен иметь один Spot Light без теней.");
        }
        else if (lights.Length != 0) throw new InvalidOperationException("Неожиданный источник света prop: " + prop.id);
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            if (!FinitePositive(transform.localScale))
                throw new InvalidOperationException("Отрицательный/нулевой Scale prop: " + prop.id + "/" + transform.name);
    }

    public static void ValidatePlacements(GameObject root, Placement[] placements, string groupName = "Exterior props")
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        CheckPlacementData(placements);
        var known = new HashSet<string>(Read().Select(prop => prop.id), StringComparer.Ordinal);
        var group = DirectChild(root.transform, groupName);
        if (group == null || group.childCount != placements.Length || group.localPosition != Vector3.zero
            || group.localScale != Vector3.one || Quaternion.Angle(group.localRotation, Quaternion.identity) > .001f)
            throw new InvalidOperationException("Группа " + groupName + " не соответствует manifest.");
        foreach (var placement in placements)
        {
            if (!known.Contains(placement.prop)) throw new InvalidOperationException("Неизвестный prop: " + placement.prop);
            var child = DirectChild(group, placement.id);
            if (child == null) throw new InvalidOperationException("Нет размещения prop: " + placement.id);
            RequireSource(child.gameObject, PrefabPath(placement.prop));
            if (Vector3.Distance(child.localPosition, V(placement.position)) > .001f
                || Vector3.Distance(child.localScale, V(placement.scale)) > .001f
                || Quaternion.Angle(child.localRotation, Quaternion.Euler(V(placement.rotation))) > .001f)
                throw new InvalidOperationException("Поза prop не соответствует manifest: " + placement.id);
        }
    }

    private static void CheckPlacementData(Placement[] placements)
    {
        if (placements == null) throw new ArgumentNullException(nameof(placements));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var placement in placements)
        {
            if (placement == null || !AssetName(placement.id) || !ids.Add(placement.id) || !AssetName(placement.prop))
                throw new InvalidOperationException("Некорректный или повторный ID размещения prop.");
            CheckVector(placement.position, placement.id);
            CheckVector(placement.rotation, placement.id);
            CheckVector(placement.scale, placement.id);
            if (!FinitePositive(V(placement.scale)))
                throw new InvalidOperationException("Scale prop должен быть положительным: " + placement.id);
        }
    }

    private static void CheckVector(float[] values, string id)
    {
        if (values == null || values.Length != 3 || values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
            throw new InvalidOperationException("Неверный вектор размещения prop: " + id);
    }

    private static bool FinitePositive(Vector3 value) => value.x > 0 && value.y > 0 && value.z > 0
        && !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
        && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);

    private static Vector3 V(float[] values) => new Vector3(values[0], values[1], values[2]);

    private static Dictionary<string, long> Identities(GameObject asset)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var transform in asset.GetComponentsInChildren<Transform>(true))
        {
            var path = AnimationUtility.CalculateTransformPath(transform, asset.transform);
            Capture(transform.gameObject, path + "/GameObject", result);
            var counts = new Dictionary<Type, int>();
            foreach (var component in transform.GetComponents<Component>())
            {
                if (component == null) continue;
                var type = component.GetType();
                counts.TryGetValue(type, out var index);
                counts[type] = index + 1;
                Capture(component, path + "/" + type.FullName + "[" + index + "]", result);
            }
        }
        return result;
    }

    private static void Capture(UnityEngine.Object asset, string key, Dictionary<string, long> result)
    {
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id)) result.Add(key, id);
    }
=======
                    throw new InvalidOperationException("FBX предмета не содержит все части manifest: " + prop.id);
                ConfigureLight(root, prop.id);
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.isStatic = true;
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException("Не удалось сохранить префаб предмета: " + path);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
            if (existing && AssetDatabase.AssetPathToGUID(path) != oldGuid)
                throw new InvalidOperationException("Изменился GUID существующего предмета: " + path);
            result.Add(prop.id, AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
        ValidateLibrary(result, materials);
        return result;
    }

    private static void ConfigureLight(GameObject root, string id)
    {
        if (id != "KH_Floodlight") return;
        var emitter = root.transform.Find("Warm emitter")?.gameObject;
        if (emitter == null)
        {
            emitter = new GameObject("Warm emitter");
            emitter.transform.SetParent(root.transform, false);
        }
        emitter.transform.localRotation = Quaternion.Euler(35, 180, 0);
        // The authored lamp body shares this optical axis; keep the emitter at its lens.
        emitter.transform.localPosition = emitter.transform.localRotation * Vector3.forward * .18f;
        emitter.transform.localScale = Vector3.one;
        var light = emitter.GetComponent<Light>();
        if (light == null) light = emitter.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1, .73f, .43f);
        light.intensity = 3;
        light.range = 14;
        light.spotAngle = 100;
        light.innerSpotAngle = 65;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        light.enabled = true;
    }

    private static Vector3 ReadVector(float[] values, Vector3 fallback, string field, string id, bool positive = false)
    {
        if (values == null || values.Length == 0) return fallback;
        if (values.Length != 3 || values.Any(v => float.IsNaN(v) || float.IsInfinity(v))
            || (positive && values.Any(v => v <= 0)))
            throw new InvalidOperationException("Некорректное размещение предмета: " + id + "/" + field);
        return new Vector3(values[0], values[1], values[2]);
    }

    public static void Place(GameObject root, Dictionary<string, GameObject> library, Placement[] placements)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Размещение предметов доступно только вне Play.");
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (library == null) throw new ArgumentNullException(nameof(library));
        if (placements == null) throw new ArgumentNullException(nameof(placements));
        var group = root.transform.Find(PlacementGroup);
        var existing = new Dictionary<string, Transform>(StringComparer.Ordinal);
        if (group != null)
            foreach (Transform child in group)
            {
                if (existing.ContainsKey(child.name))
                    throw new InvalidOperationException("Повторное имя размещённого предмета: " + child.name);
                existing.Add(child.name, child);
            }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        // Validate every requested link and pose before adding any new instance.
        foreach (var placement in placements)
        {
            if (placement == null || string.IsNullOrWhiteSpace(placement.id) || !ids.Add(placement.id)
                || string.IsNullOrWhiteSpace(placement.prop) || !library.TryGetValue(placement.prop, out var asset) || asset == null)
                throw new InvalidOperationException("Неизвестный или повторный предмет в списке размещения.");
            ReadVector(placement.position, Vector3.zero, "position", placement.id);
            ReadVector(placement.rotation, Vector3.zero, "rotation", placement.id);
            ReadVector(placement.scale, Vector3.one, "scale", placement.id, true);
            if (existing.TryGetValue(placement.id, out var child)
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) != AssetDatabase.GetAssetPath(asset))
                throw new InvalidOperationException("Предмет с этим ID уже имеет другой prefab; замена не выполняется: " + placement.id);
        }
        if (group == null)
        {
            group = new GameObject(PlacementGroup).transform;
            group.SetParent(root.transform, false);
            group.gameObject.isStatic = true;
        }
        foreach (var placement in placements)
        {
            if (!existing.TryGetValue(placement.id, out var child))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(library[placement.prop], group);
                instance.name = placement.id;
                child = instance.transform;
            }
            child.localPosition = ReadVector(placement.position, Vector3.zero, "position", placement.id);
            child.localRotation = Quaternion.Euler(ReadVector(placement.rotation, Vector3.zero, "rotation", placement.id));
            child.localScale = ReadVector(placement.scale, Vector3.one, "scale", placement.id, true);
            child.gameObject.isStatic = true;
        }
        ValidatePlacements(root, library, placements);
    }

    public static void ValidatePlacements(GameObject root, Dictionary<string, GameObject> library, Placement[] placements)
    {
        var group = root.transform.Find(PlacementGroup);
        if (group == null) throw new InvalidOperationException("Отсутствует группа размещённых предметов ТЦ Курс.");
        foreach (var placement in placements)
        {
            var children = group.Cast<Transform>().Where(t => t.name == placement.id).ToArray();
            if (children.Length != 1 || !library.TryGetValue(placement.prop, out var asset) || asset == null
                || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(children[0].gameObject) != AssetDatabase.GetAssetPath(asset))
                throw new InvalidOperationException("Потеряна связь размещённого предмета с библиотекой: " + placement.id);
            var child = children[0];
            if (Vector3.Distance(child.localPosition, ReadVector(placement.position, Vector3.zero, "position", placement.id)) > .001f
                || Quaternion.Angle(child.localRotation, Quaternion.Euler(ReadVector(placement.rotation, Vector3.zero, "rotation", placement.id))) > .01f
                || Vector3.Distance(child.localScale, ReadVector(placement.scale, Vector3.one, "scale", placement.id, true)) > .001f)
                throw new InvalidOperationException("Поза предмета не совпадает со списком размещения: " + placement.id);
        }
    }

    public static void ValidateLibrary(Dictionary<string, GameObject> library = null, Dictionary<string, Material> materials = null)
    {
        var manifest = ReadManifest();
        if (library == null)
            library = manifest.props.ToDictionary(p => p.id, p => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(p.id)), StringComparer.Ordinal);
        foreach (var prop in manifest.props)
        {
            var path = PrefabPath(prop.id);
            if (!library.TryGetValue(prop.id, out var prefab) || prefab == null || AssetDatabase.GetAssetPath(prefab) != path
                || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                throw new InvalidOperationException("Нет отдельного префаба предмета: " + prop.id);
            var shell = prefab.transform.Find("Shell");
            if (shell == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(shell.gameObject) != prop.model)
                throw new InvalidOperationException("Потеряна вложенная модель предмета: " + prop.id);
            if (prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component == null
                    || component.GetType().FullName != "UnityEngine.Rendering.Universal.UniversalAdditionalLightData"))
                throw new InvalidOperationException("Статический предмет содержит Rigidbody или игровые компоненты: " + prop.id);
            var chunks = prop.chunks.ToDictionary(c => c.name, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!chunks.TryGetValue(renderer.name, out var chunk) || !seen.Add(renderer.name))
                    throw new InvalidOperationException("Неизвестная или повторная часть префаба предмета: " + prop.id + "/" + renderer.name);
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || mesh.vertexCount == 0 || mesh.subMeshCount != 1 || !renderer.enabled || renderer.sharedMaterials.Length != 1
                    || renderer.sharedMaterials[0] == null || renderer.sharedMaterials[0].shader == null
                    || (materials != null && renderer.sharedMaterials[0] != materials[chunk.material]))
                    throw new InvalidOperationException("Пустая геометрия или материал предмета: " + renderer.name);
                var colliders = renderer.GetComponents<MeshCollider>();
                if (renderer.GetComponents<Collider>().Length != colliders.Length || colliders.Length != (chunk.collider ? 1 : 0)
                    || (chunk.collider && (!colliders[0].enabled || colliders[0].sharedMesh != mesh || colliders[0].convex || colliders[0].isTrigger)))
                    throw new InvalidOperationException("Коллизия предмета не совпадает с manifest: " + renderer.name);
                if (chunk.material == "DirtStain" && renderer.shadowCastingMode != ShadowCastingMode.Off)
                    throw new InvalidOperationException("Грязевая накладка отбрасывает тень: " + renderer.name);
            }
            if (seen.Count != chunks.Count) throw new InvalidOperationException("Не все части предмета присутствуют в префабе: " + prop.id);
            if (prefab.GetComponentsInChildren<Renderer>(true).Length != chunks.Count
                || prefab.GetComponentsInChildren<Collider>(true).Length != prop.chunks.Count(c => c.collider))
                throw new InvalidOperationException("У предмета есть неучтённые Renderer или Collider: " + prop.id);
            var lights = prefab.GetComponentsInChildren<Light>(true);
            if (lights.Length != (prop.id == "KH_Floodlight" ? 1 : 0))
                throw new InvalidOperationException("Неожиданное число источников света предмета: " + prop.id);
            if (lights.Length == 1 && (lights[0].type != LightType.Spot || lights[0].shadows != LightShadows.None || !lights[0].enabled))
                throw new InvalidOperationException("Некорректный источник прожектора: " + prop.id);
        }
        Debug.Log("KURS_PROP_LIBRARY_OK: " + manifest.props.Length + " отдельных связанных префабов предметов.");
    }

    [MenuItem("Tools/Character Lab/Check Kurs Hub Prop Library")]
    public static void CheckLibrary() => ValidateLibrary();
>>>>>>> Stashed changes
}
