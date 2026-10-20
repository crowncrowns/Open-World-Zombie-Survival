using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Authoring only. The static shell has no actor, economy or persistence state.</summary>
public static class KursHubSetup
{
    public const string Folder = "Assets/Environment/KursHub";
    public const string PrefabPath = Folder + "/KursHub.prefab";
    public const string ScenePath = "Assets/CharacterTesting/CharacterTest.unity";
    public static readonly Vector3 ScenePosition = new Vector3(0, 0, -155);

    [Serializable] private sealed class Chunk { public string name; public string material; public bool collider; public bool hidden; }
    [Serializable] private sealed class Guard { public string name; public float[] center; public float[] size; public float angle; }
    [Serializable] private sealed class Palette
    {
        public string name; public float[] rgba;
        public string baseMap; public string normalMap; public string maskMap;
        public float tile = 1; public float smoothness = .35f; public float metallic;
        public float[] tint; public bool transparent; public bool emission;
    }
    [Serializable] private sealed class Manifest
    {
        public int stage; public float width; public float depth; public float floorHeight;
        public Chunk[] chunks; public Guard[] guardColliders; public Palette[] palette;
        public KursHubPropSetup.Placement[] propInstances;
<<<<<<< Updated upstream
        public KursHubPropSetup.Placement[] interiorInstances;
        public KursHubPropSetup.Placement[] settlementInstances;
        public KursHubInteriorFinishSetup.LightPlacement[] settlementLights;
=======
>>>>>>> Stashed changes
    }
    private static Vector3 V(float[] values) => new Vector3(values[0], values[1], values[2]);
    private static Manifest Read() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "/KursHubManifest.json"));

    [MenuItem("Tools/Character Lab/Set Up Kurs Hub Stage 1")]
    public static void Run()
    {
        if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Откройте CharacterTest вне Play.");
        BuildPrefab();
        var scene = EditorSceneManager.GetActiveScene();
        var instance = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "KursHub");
        if (instance == null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            Undo.RegisterCreatedObjectUndo(instance, "Добавить ТЦ Курс");
            instance.transform.SetPositionAndRotation(ScenePosition, Quaternion.Euler(0, 180, 0));
        }
        AddApproach(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        // Interactive use leaves the user's other pending scene edits under their control.
        if (Application.isBatchMode) EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = instance;
        Debug.Log("KURS_SETUP_OK: префаб ТЦ Курс, два этажа и выход на крышу; вход (0, 0, -90). Сохраните сцену после проверки.");
    }

    [MenuItem("Tools/Character Lab/Set Up Kurs Hub Stage 2")]
    public static void RunStage2() => Run();

    [MenuItem("Tools/Character Lab/Set Up Kurs Hub Stage 3")]
    public static void RunStage3() => Run();

    [MenuItem("Tools/Character Lab/Set Up Kurs Hub Stage 4")]
    public static void RunStage4() => Run();

<<<<<<< Updated upstream
    [MenuItem("Tools/Character Lab/Set Up Kurs Hub Stage 5")]
    public static void RunStage5() => Run();

    [MenuItem("Tools/Character Lab/Set Up Kurs Hub Stage 6")]
    public static void RunStage6() => Run();

=======
>>>>>>> Stashed changes
    public static void BuildPrefab()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Создание префаба доступно только вне Play.");
        var manifest = Read();
        var modelPath = Folder + "/Models/KursHubShell.fbx";
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Сначала выполните Tools/KursHub/build_kurs_hub.py в Blender.");
        importer.globalScale = 1;
        importer.bakeAxisConversion = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.addCollider = false;
        importer.isReadable = manifest.stage >= 6;
        importer.SaveAndReimport();
        if (!AssetDatabase.IsValidFolder(Folder + "/Materials")) AssetDatabase.CreateFolder(Folder, "Materials");
        var materials = manifest.palette.ToDictionary(p => p.name, MaterialFor);
<<<<<<< Updated upstream
        if (manifest.stage >= 4) KursHubSurfaceSetup.Apply(materials);
=======
>>>>>>> Stashed changes
        var propLibrary = manifest.stage >= 4 ? KursHubPropSetup.BuildLibrary(materials) : null;
        var chunks = manifest.chunks.ToDictionary(c => c.name);
        // The scene already has overrides on this prefab's children. Editing the
        // existing contents preserves their local IDs, including the working lights.
        var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        var root = loaded ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("KursHub");
        try
        {
            var model = root.transform.Find("Shell")?.gameObject;
            if (model == null)
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                model.name = "Shell";
                model.transform.SetParent(root.transform, false);
            }
            var seen = new HashSet<string>();
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!chunks.TryGetValue(renderer.name, out var chunk) || !seen.Add(renderer.name))
                    throw new InvalidOperationException("FBX/manifest не совпадают: " + renderer.name);
                renderer.sharedMaterials = new[] { materials[chunk.material] };
                renderer.enabled = !chunk.hidden;
                renderer.gameObject.isStatic = true;
                renderer.shadowCastingMode = chunk.material == "Glass" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                var colliders = renderer.GetComponents<MeshCollider>();
                if (chunk.collider)
                {
                    if (colliders.Length > 1)
                        throw new InvalidOperationException("Дублирующий MeshCollider: " + renderer.name);
                    var collider = colliders.Length == 0 ? renderer.gameObject.AddComponent<MeshCollider>() : colliders[0];
                    collider.sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    collider.enabled = true;
                }
                else
                    foreach (var collider in colliders) UnityEngine.Object.DestroyImmediate(collider);
            }
            if (seen.Count != chunks.Count)
                throw new InvalidOperationException("В FBX отсутствуют части manifest: " + string.Join(", ", chunks.Keys.Where(name => !seen.Contains(name))));

            var guards = root.transform.Find("Continuous guards")?.gameObject;
            if (guards == null)
            {
                guards = new GameObject("Continuous guards");
                guards.transform.SetParent(root.transform, false);
            }
            if (guards.transform.childCount > manifest.guardColliders.Length)
                throw new InvalidOperationException("Число существующих ограждений больше manifest: требуется явное согласование вместо удаления авторских объектов.");
            for (var i = 0; i < manifest.guardColliders.Length; i++)
            {
                var guard = manifest.guardColliders[i];
                var go = i < guards.transform.childCount ? guards.transform.GetChild(i).gameObject : null;
                if (go == null)
                {
                    go = new GameObject(guard.name);
                    go.transform.SetParent(guards.transform, false);
                }
                go.transform.localPosition = V(guard.center);
                go.transform.localRotation = Quaternion.Euler(0, guard.angle, 0);
                var collider = go.GetComponent<BoxCollider>();
                // Unity may return a missing native component wrapper: use its
                // native null comparison before adding new stage-3 guard objects.
                if (collider == null) collider = go.AddComponent<BoxCollider>();
                collider.size = V(guard.size);
                go.isStatic = true;
            }
            // Temporary working light, no shadows. Replace with authored/baked lighting later.
            // Existing lights and their components/settings are retained verbatim.
            if (root.transform.Find("Stage 1 working lights") == null)
            {
                var lights = new GameObject("Stage 1 working lights");
                lights.transform.SetParent(root.transform, false);
                foreach (float y in new[] { 4.3f, 9.8f })
                    foreach (float x in new[] { -29f, 29f })
                        foreach (float z in new[] { -38f, -8f, 24f, 52f })
                        {
                            var go = new GameObject("Working light"); go.transform.SetParent(lights.transform, false);
                            go.transform.localPosition = new Vector3(x, y, z);
                            var light = go.AddComponent<Light>(); light.type = LightType.Point;
                            light.color = new Color(1, .83f, .64f); light.intensity = 2; light.range = 19;
                            light.shadows = LightShadows.None;
                        }
            }
            if (propLibrary != null) KursHubPropSetup.Place(root, propLibrary, manifest.propInstances);
<<<<<<< Updated upstream
            if (manifest.stage >= 5) KursHubPropSetup.Place(root, propLibrary, manifest.interiorInstances, "Interior architecture");
            if (manifest.stage >= 6)
            {
                KursHubPropSetup.Place(root, propLibrary, manifest.settlementInstances, "Interior settlement");
                KursHubInteriorFinishSetup.Apply(root, materials, manifest.settlementLights);
            }
=======
>>>>>>> Stashed changes
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            if (loaded) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
        if (manifest.stage >= 6) { importer.isReadable = false; importer.SaveAndReimport(); }
    }

    private static Material MaterialFor(Palette entry)
    {
        var path = Folder + "/Materials/" + entry.name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            // Stage 3 replaces only our original dark blue default. Authored
            // colours and all other material settings/GUIDs remain intact.
            if ((entry.name == "BlueCladding" && Vector4.Distance(material.GetColor("_BaseColor"), new Color(.035f,.16f,.27f,1)) < .0001f)
                || (entry.name == "MallBlue" && Vector4.Distance(material.GetColor("_BaseColor"), new Color(.025f,.19f,.36f,1)) < .0001f))
            {
                material.SetColor("_BaseColor", new Color(entry.rgba[0],entry.rgba[1],entry.rgba[2],entry.rgba[3]));
                EditorUtility.SetDirty(material);
            }
            ApplyFinish(material, entry);
            return material;
        }
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Не найден URP/Lit.");
        material = new Material(shader) { name = entry.name };
        material.SetColor("_BaseColor", new Color(entry.rgba[0], entry.rgba[1], entry.rgba[2], entry.rgba[3]));
        material.SetFloat("_Smoothness", entry.name == "Glass" ? .65f : .12f);
        if (entry.name == "Glass")
        {
            material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        ApplyFinish(material, entry);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Texture2D SurfaceTexture(string path, bool normal = false, bool linear = false)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Нет карты поверхности: " + path);
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        var dirty = importer.textureType != type || importer.sRGBTexture == (normal || linear)
            || importer.maxTextureSize != 1024 || importer.wrapModeU != TextureWrapMode.Repeat
            || importer.wrapModeV != (path.Contains("DirtStreaks") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat)
            || importer.alphaIsTransparency != (!normal && !linear && (path.Contains("GlassGrime") || path.Contains("DirtStreaks")));
        if (dirty)
        {
            importer.textureType = type;
            importer.sRGBTexture = !(normal || linear);
            importer.maxTextureSize = 1024;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = path.Contains("DirtStreaks") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.alphaIsTransparency = !normal && !linear && (path.Contains("GlassGrime") || path.Contains("DirtStreaks"));
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void ApplyFinish(Material material, Palette entry)
    {
        if (entry.emission && !material.IsKeywordEnabled("_EMISSION"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1,.58f,.23f) * 2);
            EditorUtility.SetDirty(material);
        }
        if (string.IsNullOrEmpty(entry.baseMap)) return;
        var albedo = SurfaceTexture(entry.baseMap);
        var normal = SurfaceTexture(entry.normalMap, true);
        var mask = SurfaceTexture(entry.maskMap, false, true);
        // Assign authored surfaces once. Subsequent rebuilds keep artists' tint,
        // tiling, roughness and alternate maps on the same material/GUID.
        var assigned = material.GetTexture("_BaseMap");
        if (assigned != null && AssetDatabase.GetAssetPath(assigned).StartsWith("Assets/", StringComparison.Ordinal)) return;
        material.SetTexture("_BaseMap", albedo);
        material.SetColor("_BaseColor", entry.tint != null && entry.tint.Length == 4
            ? new Color(entry.tint[0],entry.tint[1],entry.tint[2],entry.tint[3]) : Color.white);
        material.SetTextureScale("_BaseMap", Vector2.one * entry.tile);
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", .35f);
            material.EnableKeyword("_NORMALMAP");
        }
        material.SetFloat("_Smoothness", entry.smoothness);
        material.SetFloat("_Metallic", entry.metallic);
        if (mask != null)
        {
            material.SetTexture("_MetallicGlossMap", mask);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        if (entry.transparent)
        {
            material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        if (entry.emission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1,.58f,.23f) * 2);
        }
        EditorUtility.SetDirty(material);
    }

    private static GameObject Cube(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.position = center; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat; go.isStatic = true;
        return go;
    }

    private static void AddApproach(Scene scene)
    {
        if (scene.GetRootGameObjects().Any(g => g.name == "KursHub - access path")) return;
        var boundary = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "Boundary south");
        if (boundary == null || Vector3.Distance(boundary.position, new Vector3(0, 1.5f, -32)) > .01f
            || Vector3.Distance(boundary.localScale, new Vector3(65, 3, 1)) > .01f)
            throw new InvalidOperationException("Южная граница изменилась: разместите подход вручную, не перезаписывая чужую планировку. "
                + (boundary == null ? "Boundary south не найдена в " + scene.path + "; roots=" + string.Join(",", scene.GetRootGameObjects().Select(g => g.name)) : $"position={boundary.position}, scale={boundary.localScale}"));
        Undo.RecordObject(boundary, "Проход к ТЦ Курс");
        boundary.position = new Vector3(-4, 1.5f, -32); boundary.localScale = new Vector3(57, 3, 1);
        var path = new GameObject("KursHub - access path"); Undo.RegisterCreatedObjectUndo(path, "Подход к ТЦ Курс");
        var floor = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Floor.mat");
        Cube("South boundary end", path.transform, new Vector3(31, 1.5f, -32), new Vector3(3, 3, 1), boundary.GetComponent<Renderer>().sharedMaterial);
        Cube("Path from lab", path.transform, new Vector3(27, -.2f, -45), new Vector3(6, .4f, 30), floor);
        Cube("Path turn", path.transform, new Vector3(13.5f, -.2f, -59), new Vector3(33, .4f, 6), floor);
        Cube("Path to entrance apron", path.transform, new Vector3(0, -.2f, -65), new Vector3(6, .4f, 16), floor);
        var sign = new GameObject("ТЦ Курс — проход"); sign.transform.SetParent(path.transform);
        sign.transform.position = new Vector3(29.4f, 2.2f, -30.5f); sign.transform.rotation = Quaternion.Euler(0, 180, 0);
        var text = sign.AddComponent<TextMesh>(); text.text = "ТЦ КУРС\nПРОХОД"; text.fontSize = 64;
        text.characterSize = .05f; text.anchor = TextAnchor.MiddleCenter; text.color = new Color(.9f, .8f, .4f);
    }

    [MenuItem("Tools/Character Lab/Check Kurs Hub Stage 1")]
    public static void Check()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Нет префаба ТЦ Курс.");
        var manifest = Read();
        var chunks = manifest.chunks.ToDictionary(c => c.name);
        var renderers = prefab.transform.Find("Shell").GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length != manifest.chunks.Length) throw new InvalidOperationException("Число частей не совпадает с FBX.");
        var seen = new HashSet<string>();
        foreach (var renderer in renderers)
        {
            if (!chunks.TryGetValue(renderer.name, out var chunk) || !seen.Add(renderer.name))
                throw new InvalidOperationException("FBX/manifest не совпадают: " + renderer.name);
            if (renderer.sharedMaterials.Length != (manifest.stage >= 6 && renderer.name.StartsWith("Floors_", StringComparison.Ordinal) ? 2 : 1)
                || renderer.sharedMaterials.Any(m => m == null || m.shader.name != "Universal Render Pipeline/Lit"))
                throw new InvalidOperationException("Материалы: " + renderer.name);
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || mesh.vertexCount == 0) throw new InvalidOperationException("Пустая геометрия: " + renderer.name);
            if (renderer.enabled == chunk.hidden)
                throw new InvalidOperationException("Видимость части не совпадает с manifest: " + renderer.name);
            var colliders = renderer.GetComponents<MeshCollider>();
            if (colliders.Length != (chunk.collider ? 1 : 0)
                || (chunk.collider && (!colliders[0].enabled || colliders[0].sharedMesh != mesh)))
                throw new InvalidOperationException("MeshCollider не совпадает с manifest: " + renderer.name);
        }
        if (seen.Count != chunks.Count) throw new InvalidOperationException("Не все части manifest найдены в префабе.");
<<<<<<< Updated upstream
        if (manifest.stage >= 4) { KursHubPropSetup.ValidateLibrary(); KursHubPropSetup.ValidatePlacements(prefab, manifest.propInstances); }
        if (manifest.stage >= 5) KursHubPropSetup.ValidatePlacements(prefab, manifest.interiorInstances, "Interior architecture");
        if (manifest.stage >= 6) KursHubPropSetup.ValidatePlacements(prefab, manifest.settlementInstances, "Interior settlement");
=======
        if (manifest.stage >= 4) KursHubPropSetup.ValidateLibrary();
>>>>>>> Stashed changes
        var instances = SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == "KursHub").ToArray();
        if (instances.Length != 1 || !PrefabUtility.IsPartOfPrefabInstance(instances[0]))
            throw new InvalidOperationException("В сцене нужен один связанный экземпляр KursHub.");
        var allColliders = prefab.GetComponentsInChildren<Collider>(true);
        Debug.Log($"KURS_VALIDATION_OK: {renderers.Length} render chunks; {allColliders.Length} colliders; 93 x 130 m; Y=0/5.5/11. Play-проход проверяется отдельно.");
    }

    [MenuItem("Tools/Character Lab/Check Kurs Hub Stage 2")]
    public static void CheckStage2() => Check();

    [MenuItem("Tools/Character Lab/Check Kurs Hub Stage 3")]
    public static void CheckStage3() => Check();

    [MenuItem("Tools/Character Lab/Check Kurs Hub Stage 4")]
    public static void CheckStage4() => Check();

<<<<<<< Updated upstream
    [MenuItem("Tools/Character Lab/Check Kurs Hub Stage 5")]
    public static void CheckStage5() => Check();

    [MenuItem("Tools/Character Lab/Check Kurs Hub Stage 6")]
    public static void CheckStage6() => Check();

=======
>>>>>>> Stashed changes
    public static void BatchBuild()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Run(); Check();
    }
}
