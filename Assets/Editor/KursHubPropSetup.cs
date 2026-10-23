using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds reusable static props, then nests their prefabs in the hub.</summary>
public static class KursHubPropSetup
{
    public const string Folder = "Assets/Environment/KursHub/Props";
    public const string ManifestPath = Folder + "/KursHubPropsManifest.json";
    public const string PrefabsFolder = Folder + "/Prefabs";

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
        [Tooltip("Сила света конкретного прожектора в единицах Unity Light.intensity. 0 — сохранить настройку общего префаба.")]
        public float lightIntensity;
        [Tooltip("Дальность света конкретного прожектора в метрах. 0 — сохранить настройку общего префаба.")]
        public float lightRange;
        [Tooltip("Внутренний угол светового пучка конкретного прожектора, градусы. 0 — сохранить общий префаб.")]
        public float lightInnerAngle;
        [Tooltip("Точка наведения прожектора в локальных координатах здания, метры X/Y/Z. Пусто — сохранить направление общего префаба.")]
        public float[] lightTarget;
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
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.generateSecondaryUV = true;
            importer.secondaryUVPackMargin = 1;
            importer.secondaryUVMarginMethod = ModelImporterSecondaryUVMarginMethod.Manual;
            importer.SaveAndReimport();
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
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var renderer in shell.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!chunks.TryGetValue(renderer.name, out var chunk) || !seen.Add(renderer.name))
                        throw new InvalidOperationException("FBX/manifest prop не совпадают: " + prop.id + "/" + renderer.name);
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                        throw new InvalidOperationException("Пустая геометрия prop: " + renderer.name);
                    renderer.sharedMaterials = new[] { materials[chunk.material] };
                    renderer.enabled = true;
                    renderer.gameObject.isStatic = true;
                    renderer.shadowCastingMode = chunk.material == "LampGlass" || chunk.material == "DirtStain" || chunk.material == "Glass"
                        ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    KursHubQualitySetup.ConfigureCollider(renderer, chunk.collider, root, prop.id);
                }
                if (seen.Count != chunks.Count)
                    throw new InvalidOperationException("FBX prop не содержит все части manifest: " + prop.id);
                if (prop.id == "KH_Floodlight") AddWorkLight(root);
                KursHubQualitySetup.ConfigureProp(root, prop.id);
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
            if (HasLightOverride(placement))
            {
                var light = DirectChild(child, "Work light")?.GetComponent<Light>();
                if (light == null) throw new InvalidOperationException("Нет источника Work light: " + placement.id);
                if (placement.lightIntensity > 0) light.intensity = placement.lightIntensity;
                if (placement.lightRange > 0) light.range = placement.lightRange;
                if (placement.lightInnerAngle > 0) light.innerSpotAngle = placement.lightInnerAngle;
                if (placement.lightTarget != null)
                {
                    var direction = root.transform.TransformPoint(V(placement.lightTarget)) - light.transform.position;
                    if (direction.sqrMagnitude < .0001f) throw new InvalidOperationException("Точка наведения совпала со светильником: " + placement.id);
                    light.transform.rotation = Quaternion.LookRotation(direction, root.transform.up);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
                }
                // The shared reusable prefab keeps its defaults. Only this
                // authored fixture records the explicitly requested setting.
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            }
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
                || importer.addCollider || importer.isReadable || !importer.generateSecondaryUV || importer.materialImportMode != ModelImporterMaterialImportMode.None)
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
                throw new InvalidOperationException("Неверная геометрия или material slot prop: " + chunk.name
                    + "; mesh=" + (filter == null || filter.sharedMesh == null ? "null" : filter.sharedMesh.name + "/" + filter.sharedMesh.vertexCount)
                    + "; enabled=" + renderer.enabled + "; materials=" + string.Join(",", renderer.sharedMaterials.Select(m => m == null ? "null" : m.name))
                    + "; expected=" + chunk.material);
            KursHubQualitySetup.ValidateCollider(renderer, chunk.collider, root, prop.id);
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
        KursHubQualitySetup.ValidateProp(root, prop.id);
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
            if (HasLightOverride(placement))
            {
                var light = DirectChild(child, "Work light")?.GetComponent<Light>();
                if (light == null || (placement.lightIntensity > 0 && !Mathf.Approximately(light.intensity, placement.lightIntensity))
                    || (placement.lightRange > 0 && !Mathf.Approximately(light.range, placement.lightRange))
                    || (placement.lightInnerAngle > 0 && !Mathf.Approximately(light.innerSpotAngle, placement.lightInnerAngle))
                    || (placement.lightTarget != null && Vector3.Angle(light.transform.forward,
                        root.transform.TransformPoint(V(placement.lightTarget)) - light.transform.position) > .1f))
                    throw new InvalidOperationException("Свет prop не соответствует manifest: " + placement.id
                        + " intensity=" + (light == null ? -1 : light.intensity) + "/" + placement.lightIntensity
                        + " range=" + (light == null ? -1 : light.range) + "/" + placement.lightRange
                        + " inner=" + (light == null ? -1 : light.innerSpotAngle) + "/" + placement.lightInnerAngle
                        + " directionError=" + (light == null || placement.lightTarget == null ? -1 : Vector3.Angle(light.transform.forward,
                            root.transform.TransformPoint(V(placement.lightTarget)) - light.transform.position)));
            }
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
            if (float.IsNaN(placement.lightIntensity) || float.IsInfinity(placement.lightIntensity) || placement.lightIntensity < 0
                || float.IsNaN(placement.lightRange) || float.IsInfinity(placement.lightRange) || placement.lightRange < 0
                || float.IsNaN(placement.lightInnerAngle) || float.IsInfinity(placement.lightInnerAngle) || placement.lightInnerAngle < 0 || placement.lightInnerAngle > 100
                || (HasLightOverride(placement) && placement.prop != "KH_Floodlight"))
                throw new InvalidOperationException("Некорректная настройка конкретного прожектора: " + placement.id);
            if (placement.lightTarget != null) CheckVector(placement.lightTarget, placement.id);
        }
    }

    private static bool HasLightOverride(Placement placement) => placement.lightIntensity > 0 || placement.lightRange > 0
        || placement.lightInnerAngle > 0 || placement.lightTarget != null;

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
}
