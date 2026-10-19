using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Equipment;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;
using ZombieSurvival.Services;

public static class FlashlightSetup
{
    public const string Folder = "Assets/Equipment/Flashlight";
    public const string ItemPath = Folder + "/FlashlightItem.asset";
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
    private static Texture2D Cookie()
    {
        string path = Folder + "/BeamCookie.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing != null) return existing;
        var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
        for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
        {
            float dx = (x - 127.5f) / 127.5f, dy = (y - 127.5f) / 127.5f, r2 = dx * dx + dy * dy;
            float value = Mathf.Pow(Mathf.Clamp01(1 - r2), 1.15f) * (.48f + .52f * Mathf.Exp(-r2 * 11));
            texture.SetPixel(x, y, new Color(value, value, value, value));
        }
        texture.Apply(); System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true; importer.alphaIsTransparency = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static Material MaterialFor(Material source)
    {
        string path = Folder + "/Materials/" + source.name + "_URP.mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path); if (result != null) return result;
        result = new Material(Shader.Find("Universal Render Pipeline/Lit")); result.name = source.name + "_URP";
        result.SetTexture("_BaseMap", source.mainTexture); result.SetColor("_BaseColor", source.color);
        result.SetFloat("_Smoothness", source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : .35f);
        result.SetFloat("_Metallic", source.name == "Reflector" ? 1 : source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0);
        if (source.name == "Lense")
        {
            result.SetFloat("_Surface", 1); result.SetFloat("_Blend", 0); result.SetFloat("_ZWrite", 0);
            result.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            result.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            result.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            result.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); result.SetOverrideTag("RenderType", "Transparent");
            result.renderQueue = 3000; result.SetShaderPassEnabled("ShadowCaster", false);
        }
        AssetDatabase.CreateAsset(result, path); return result;
    }
    private static GameObject View()
    {
        string path = Folder + "/FlashlightView.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Free-FlashLight/Prefabs/FlashLight.prefab");
        if (source == null) throw new InvalidOperationException("Не найден Free-FlashLight/Prefabs/FlashLight.prefab.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("FlashlightView"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene); model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one * 1.35f;
            foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterials = renderer.sharedMaterials.Select(MaterialFor).ToArray();
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static bool AddCatalog(PlayerSaveSystem save, ItemDefinition item)
    {
        if (save == null) throw new InvalidOperationException("Нет общего PlayerSaveSystem.");
        var data = new SerializedObject(save); var list = data.FindProperty("itemCatalog");
        if (Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == item)) return false;
        int index = list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = item; data.ApplyModifiedProperties(); return true;
    }
    private static bool EnsurePlayer(GameObject player, ItemDefinition item)
    {
        bool changed = AddCatalog(player.GetComponent<PlayerSaveSystem>(), item);
        var handler = player.GetComponent<PlayerFlashlight>();
        if (handler == null) { handler = player.AddComponent<PlayerFlashlight>(); changed = true; }
        var data = new SerializedObject(handler); var camera = data.FindProperty("viewCamera");
        if (camera.objectReferenceValue == null) { camera.objectReferenceValue = player.GetComponentInChildren<Camera>(true); data.ApplyModifiedProperties(); changed = true; }
        return changed;
    }
    [MenuItem("Tools/Character Lab/Set Up Flashlight (No Scene Save)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Настраивайте фонарик вне Play.");
        EnsureFolder(Folder + "/Materials");
        var view = View(); var definition = AssetDatabase.LoadAssetAtPath<FlashlightDefinition>(Folder + "/Flashlight.asset");
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<FlashlightDefinition>(); definition.viewPrefab = view;
            definition.holdPosition = new Vector3(.25f, -.23f, .45f); definition.holdEuler = Vector3.zero; definition.viewScale = Vector3.one;
            definition.lightLocalPosition = new Vector3(0, 0, .04f); definition.lightLocalEuler = new Vector3(-1.5f, -1.7f, 0);
            definition.range = 25; definition.intensity = 20; definition.spotAngle = 44; definition.innerSpotAngle = 22;
            definition.color = new Color(1, .94f, .82f); definition.shadows = LightShadows.Soft;
            definition.shadowBias = .02f; definition.shadowNormalBias = .04f; definition.cookie = Cookie();
            AssetDatabase.CreateAsset(definition, Folder + "/Flashlight.asset");
        }
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>(); var data = new SerializedObject(item);
            data.FindProperty("itemId").stringValue = "HandheldFlashlight"; data.FindProperty("displayName").stringValue = "Фонарик";
            data.FindProperty("description").stringValue = "Ручной фонарик. Экипировать или назначить в хотбар; ЛКМ — включить / выключить. Без батареек и износа в этой версии.";
            data.FindProperty("category").intValue = (int)ItemCategory.Equipment; data.FindProperty("subcategory").intValue = (int)ItemSubcategory.Flashlight;
            data.FindProperty("flashlight").objectReferenceValue = definition; data.FindProperty("allowInHotbar").boolValue = true;
            data.FindProperty("maxStack").intValue = 1; data.FindProperty("maximumDurability").floatValue = 100;
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(item, ItemPath);
        }
        if (item.WorldPrefab == null)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("FlashlightPickup"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(view, scene); visual.transform.SetParent(root.transform, false);
                root.AddComponent<BoxCollider>().size = new Vector3(.08f, .08f, .23f); root.GetComponent<BoxCollider>().center = new Vector3(0, 0, -.08f);
                root.AddComponent<WorldItem>().Initialize(item, 1);
                var pickup = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/FlashlightPickup.prefab");
                var data = new SerializedObject(item); data.FindProperty("worldPrefab").objectReferenceValue = pickup; data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item); AssetDatabase.SaveAssetIfDirty(item);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        var profile = Resources.Load<TraderProfile>("Trading/weapons_ammo"); var config = new SerializedObject(profile); var catalog = config.FindProperty("catalog");
        if (!Enumerable.Range(0, catalog.arraySize).Any(i => catalog.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item))
        {
            int index = catalog.arraySize++; var entry = catalog.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("item").objectReferenceValue = item; entry.FindPropertyRelative("quantity").intValue = 2; entry.FindPropertyRelative("basePrice").longValue = 500;
        }
        var categories = config.FindProperty("acceptedCategories");
        if (!Enumerable.Range(0, categories.arraySize).Any(i => categories.GetArrayElementAtIndex(i).intValue == (int)ItemCategory.Equipment))
        { int index = categories.arraySize++; categories.GetArrayElementAtIndex(index).intValue = (int)ItemCategory.Equipment; }
        config.ApplyModifiedProperties(); profile.Validate(); AssetDatabase.SaveAssetIfDirty(profile);
        const string playerPath = "Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
        var contents = PrefabUtility.LoadPrefabContents(playerPath);
        try { if (EnsurePlayer(contents, item)) PrefabUtility.SaveAsPrefabAsset(contents, playerPath); }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        foreach (var player in UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include))
            if (player.gameObject.scene.path == "Assets/CharacterTesting/CharacterTest.unity" && EnsurePlayer(player.gameObject, item))
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Debug.Log("FLASHLIGHT_SETUP_OK: 500 руб., два фонарика в начальном оружейном ассортименте; сцена не сохранялась.");
    }
}
