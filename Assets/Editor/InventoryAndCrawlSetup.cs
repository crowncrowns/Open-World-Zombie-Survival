using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Unity.AI.Navigation;
using ZombieSurvival.AI;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;

/// <summary>Дополняет только личный полигон. Повторный запуск не удаляет предметы/настройки.
/// Для другого уровня запекайте две поверхности: обычную и низкую Humanoid Crawl.</summary>
public static class InventoryAndCrawlSetup
{
    private const string Lab = "Assets/CharacterTesting";
    private const string Items = Lab + "/Items";
    [MenuItem("Tools/Character Lab/Set Up Inventory and Crawl Pursuit")]
    public static void Run()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Сначала остановите Play.");
        if (!AssetDatabase.IsValidFolder(Items)) AssetDatabase.CreateFolder(Lab, "Items");
        int crawlType = FindCrawlType();
        ConfigurePrefab(Lab + "/Prefabs/CharacterTestPlayer.prefab", player =>
        {
            Ensure<ItemStorage>(player); Ensure<PlayerInventory>(player);
            if (player.GetComponentInChildren<PlayerNoiseHUD>(true) == null) AddNoiseHUD(player);
            var noiseHUD = player.GetComponentInChildren<PlayerNoiseHUD>(true);
            var panel = Ensure<Image>(noiseHUD.gameObject); panel.color = new Color(.04f,.07f,.09f,.92f); panel.raycastTarget = false;
            var label = noiseHUD.GetComponentInChildren<Text>(); label.fontSize = 20;
            label.rectTransform.offsetMin = new Vector2(10,14); label.rectTransform.offsetMax = new Vector2(-8,-3);
            // Значение прежнего прототипа (12 м) поднимаем один раз; пользовательскую настройку сохраняем.
            var noise = new SerializedObject(player.GetComponent<PlayerNoiseEmitter>());
            if (Mathf.Approximately(noise.FindProperty("walkRange").floatValue, 12f)) { noise.FindProperty("walkRange").floatValue = 18f; noise.ApplyModifiedPropertiesWithoutUndo(); }
        });
        ConfigurePrefab("Assets/Characters/Enemies/ZombieMaleActor.prefab", zombie =>
        {
            Set(Ensure<EnemyCrawlPursuit>(zombie), "crawlAgentType", crawlType);
            foreach (var animator in zombie.GetComponentsInChildren<Animator>(true))
                if (animator.isHuman) Ensure<HumanoidCrawlPose>(animator.gameObject);
        });
        var scene = EditorSceneManager.OpenScene(Lab + "/CharacterTest.unity");
        if (GameObject.Find("07 - Inventory and crawl pursuit") == null) CreateStations();
        Bake("Character lab navigation", 0, Lab + "/CharacterTestNavMesh.asset");
        Bake("Character lab crawl navigation", crawlType, Lab + "/CharacterTestCrawlNavMesh.asset");
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("INVENTORY_CRAWL_SETUP_OK: inventory, items, container, noise HUD and dual navigation.");
    }
    private static int FindCrawlType()
    {
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
            if (NavMesh.GetSettingsNameFromID(id) == "Humanoid Crawl") return id;
        }
        var settings = NavMesh.CreateSettings(); int type = settings.agentTypeID;
        
        // SetSettings API writes the named global profile; never edit the existing humanoid profile.
        var navSettings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings"));
        var agents = navSettings.FindProperty("m_Settings");
        for (int i = 0; i < agents.arraySize; i++)
        {
            var profile = agents.GetArrayElementAtIndex(i);
            if (profile.FindPropertyRelative("agentTypeID").intValue != type) continue;
            var names = navSettings.FindProperty("m_SettingNames");
            if (names.arraySize <= i) names.arraySize = i + 1;
            names.GetArrayElementAtIndex(i).stringValue = "Humanoid Crawl";
            profile.FindPropertyRelative("agentRadius").floatValue = .3f;
            profile.FindPropertyRelative("agentHeight").floatValue = .65f;
            profile.FindPropertyRelative("agentClimb").floatValue = .15f;
            profile.FindPropertyRelative("agentSlope").floatValue = 45f;
        }
        navSettings.ApplyModifiedPropertiesWithoutUndo();
        return type;
    }
    private static void Bake(string name, int type, string path)
    {
        var root = GameObject.Find(name); if (root == null) root = new GameObject(name);
        var surface = Ensure<NavMeshSurface>(root); surface.agentTypeID = type;
        surface.collectObjects = CollectObjects.Volume; surface.center = new Vector3(20,5,20); surface.size = new Vector3(130,30,125);
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.overrideVoxelSize = true; surface.voxelSize = .06f;
        surface.BuildNavMesh();
        var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData, path);
        else if (existing != surface.navMeshData) { EditorUtility.CopySerialized(surface.navMeshData, existing); surface.RemoveData(); surface.navMeshData = existing; surface.AddData(); }
    }
    private static void ConfigurePrefab(string path, System.Action<GameObject> configure)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { configure(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static T Ensure<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();
    private static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(field);
        if (value is int i) p.intValue = i; else if (value is string s) p.stringValue = s; else p.objectReferenceValue = value as Object;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void AddNoiseHUD(GameObject player)
    {
        var canvas = player.GetComponentInChildren<Canvas>(true);
        var rect = new GameObject("Noise HUD", typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(24,-24); rect.sizeDelta = new Vector2(620,52);
        var textRect = new GameObject("Noise level", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>(); textRect.SetParent(rect,false);
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = new Vector2(0,14); textRect.offsetMax = Vector2.zero;
        var text = textRect.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 18; text.color = Color.white; text.raycastTarget = false;
        var background = new GameObject("Noise bar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); background.SetParent(rect,false);
        background.anchorMin = background.anchorMax = Vector2.zero; background.pivot = Vector2.zero; background.sizeDelta = new Vector2(260,8); background.GetComponent<Image>().color = new Color(.08f,.1f,.12f,.8f); background.GetComponent<Image>().raycastTarget = false;
        var fill = new GameObject("Noise fill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); fill.SetParent(background,false);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero; fill.GetComponent<Image>().raycastTarget = false;
        var hud = rect.gameObject.AddComponent<PlayerNoiseHUD>(); Set(hud,"source",player.GetComponent<PlayerNoiseEmitter>()); Set(hud,"fill",fill.GetComponent<Image>()); Set(hud,"label",text);
    }
    private static void CreateStations()
    {
        var root = new GameObject("07 - Inventory and crawl pursuit").transform;
        var food = CreateItem("CannedFood", "Консервы", "Запас еды. Употребление появится на этапе потребностей.", 5, new Color(.85f,.6f,.18f));
        var cloth = CreateItem("Cloth", "Ткань", "Материал для будущих бинтов и крафта.", 10, new Color(.35f,.65f,.9f));
        for (int i = 0; i < 4; i++)
        {
            var item = i % 2 == 0 ? food : cloth;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(item.WorldPrefab, root);
            instance.name = item.DisplayName + " — тестовый подбор"; instance.transform.position = new Vector3(8 + i * 1.2f,.25f,-20);
            instance.GetComponent<WorldItem>().Initialize(item, i % 2 == 0 ? 3 : 7);
        }
        var box = Box(root, "Ящик для добычи [E]", new Vector3(13,.5f,-17), new Vector3(1.5f,1,1));
        Ensure<ItemStorage>(box); Set(Ensure<StorageContainer>(box), "displayName", "Ящик для добычи");
        // Реальная крыша: зомби должен пройти снизу, а не обойти низкую декоративную стенку.
        Box(root, "Crawl pursuit roof 0.90m clearance", new Vector3(66,1.1f,-20), new Vector3(6,.4f,4));
        Box(root, "Crawl pursuit left wall", new Vector3(62.7f,.7f,-20), new Vector3(.4f,1.4f,4));
        Box(root, "Crawl pursuit right wall", new Vector3(69.3f,.7f,-20), new Vector3(.4f,1.4f,4));
        Sign(root,"08 / INVENTORY\nE: pick up / open chest | Tab: backpack\nSelect slot: transfer / drop | Alt: quiet walk",new Vector3(11,3,-20));
        Sign(root,"CRAWL PURSUIT\nLet the zombie see you, then Z under the roof\nHidden before contact? You remain hidden",new Vector3(66,3.5f,-20));
    }
    private static ItemDefinition CreateItem(string file, string title, string description, int stack, Color color)
    {
        string assetPath = Items + "/" + file + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath); if (item != null) return item;
        item = ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(item,assetPath);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = color; AssetDatabase.CreateAsset(mat,Items + "/" + file + ".mat");
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = title; obj.transform.localScale = Vector3.one * .25f; obj.GetComponent<Renderer>().sharedMaterial = mat;
        var world = obj.AddComponent<WorldItem>(); Set(world,"item",item); Set(world,"displayName",title);
        var rb = obj.AddComponent<Rigidbody>(); rb.mass = .3f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var prefab = PrefabUtility.SaveAsPrefabAsset(obj,Items + "/" + file + ".prefab"); Object.DestroyImmediate(obj);
        Set(item,"itemId",file.ToLowerInvariant()); Set(item,"displayName",title); Set(item,"description",description); Set(item,"maxStack",stack); Set(item,"worldPrefab",prefab);
        return item;
    }
    private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.SetParent(parent,false); obj.transform.position = position; obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Lab + "/Materials/Concrete.mat"); return obj;
    }
    private static void Sign(Transform parent, string label, Vector3 position)
    {
        var obj = new GameObject(label.Split('\n')[0]); obj.transform.SetParent(parent,false); obj.transform.position = position;
        var text = obj.AddComponent<TextMesh>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = label; text.fontSize = 64; text.characterSize = .11f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; obj.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
    }
}
