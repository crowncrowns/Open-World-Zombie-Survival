using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;
using ZombieSurvival.Progression;

/// <summary>Только авторская настройка полигона; существующее дерево и его баланс повторный запуск не сбрасывает.</summary>
public static class CharacterStage12Setup
{
    public const string TreePath = "Assets/CharacterTesting/Progression/CharacterSkillTree.asset";
    [MenuItem("Tools/Character Lab/Set Up Stage 12 and Hotbar")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/CharacterTesting/CharacterTest.unity")
            throw new InvalidOperationException("Откройте CharacterTest вне Play. Другие сцены этот setup не изменяет.");
        if (!AssetDatabase.IsValidFolder("Assets/CharacterTesting/Progression")) AssetDatabase.CreateFolder("Assets/CharacterTesting", "Progression");
        var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(TreePath);
        if (tree == null) tree = CreateTree();
        var medicine = CreateMedicine();
        const string path = "Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
        var player = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Add<PlayerStatModifiers>(player);
            var progression = Add<PlayerProgression>(player);
            var serialized = new SerializedObject(progression);
            if (serialized.FindProperty("tree").objectReferenceValue == null)
            { serialized.FindProperty("tree").objectReferenceValue = tree; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            Add<PlayerHotbar>(player);
            AppendReference(player.GetComponent<PlayerNeeds>(), "consumables", medicine);
            AppendReference(player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSystem>(), "itemCatalog", medicine.Item);
            var menu = Add<PlayerProgressionMenu>(player); var menuData = new SerializedObject(menu);
            if (menuData.FindProperty("background").objectReferenceValue == null)
            {
                menuData.FindProperty("background").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Постапокалиптическое дерево навыков индустриального бункера.png");
                menuData.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(player, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        if (GameObject.Find("Stage12_ProgressionAndHotbar") == null)
        {
            var root = new GameObject("Stage12_ProgressionAndHotbar");
            for (int i = 0; i < 2; i++)
            {
                var console = GameObject.CreatePrimitive(PrimitiveType.Cube);
                console.name = i == 0 ? "Опыт +100" : "Опыт +1000";
                console.transform.SetParent(root.transform); console.transform.position = new Vector3(-9 + i * 2, .45f, -30);
                console.transform.localScale = new Vector3(.6f, .9f, .6f);
                var data = new SerializedObject(console.AddComponent<ProgressionTestConsole>());
                data.FindProperty("experience").intValue = i == 0 ? 100 : 1000; data.ApplyModifiedPropertiesWithoutUndo();
                var label = new GameObject("Stage12 instructions"); label.transform.SetParent(root.transform); label.transform.position = new Vector3(-9 + i * 2, 2, -28.4f);
                var text = label.AddComponent<TextMesh>();
                text.text = "ЭТАП 12\nE: " + console.name + "\nK — дерево развития\nTab — назначить в хотбар\n1–5 / колесо | Q — выброс";
                WorldSpacePresentationSetup.ConfigurePlaque(text);
            }
        }
        var station = GameObject.Find("Stage12_ProgressionAndHotbar");
        if (station.transform.Find("Медицинский стол") == null)
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube); table.name = "Медицинский стол";
            table.transform.SetParent(station.transform); table.transform.position = new Vector3(-8, .5f, -27); table.transform.localScale = new Vector3(2, .3f, 1);
            var obstacle = table.AddComponent<UnityEngine.AI.NavMeshObstacle>(); obstacle.carving = true;
            var pickup = (GameObject)PrefabUtility.InstantiatePrefab(medicine.Item.WorldPrefab);
            pickup.transform.SetParent(station.transform); pickup.transform.position = new Vector3(-8, .85f, -27); pickup.GetComponent<WorldItem>().Initialize(medicine.Item, 3);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        if (!tree.Validate(out var error)) throw new InvalidOperationException(error);
        Debug.Log("STAGE12_SETUP_OK");
    }
    private static T Add<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
    private static void AppendReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        if (target == null) throw new InvalidOperationException("Этап 11 должен быть настроен до этапа 12.");
        var data = new SerializedObject(target); var list = data.FindProperty(field);
        if (!Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == value))
        { int index = list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    }
    private static ConsumableDefinition CreateMedicine()
    {
        const string folder = "Assets/CharacterTesting/Progression/";
        var effect = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>(folder + "MedicalKitEffect.asset");
        if (effect != null) return effect;
        // Подтверждённый ассет библиотеки; вся игровая обёртка создаётся вне vendor-папки.
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Polygon-Lite Survival Collection/Prefabs/Medical_Kit.prefab");
        if (source == null) throw new InvalidOperationException("Не найдена модель Medical_Kit.");
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(folder + "MedicalKit.asset");
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(item, folder + "MedicalKit.asset");
            var data = new SerializedObject(item); data.FindProperty("itemId").stringValue = "medical_kit";
            data.FindProperty("displayName").stringValue = "Аптечка"; data.FindProperty("description").stringValue = "Восстанавливает 20 HP; Полевая медицина усиливает лечение на 25%.";
            data.FindProperty("maxStack").intValue = 5; data.FindProperty("category").intValue = (int)ItemCategory.Medicine;
            data.FindProperty("subcategory").intValue = (int)ItemSubcategory.MedicalSupplies;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var root = new GameObject("MedicalKitPickup"); root.AddComponent<SphereCollider>().radius = .18f; root.AddComponent<WorldItem>().Initialize(item, 1);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(source); model.transform.SetParent(root.transform, false);
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Weapons/SurvivalWeaponsURP.mat");
        var renderers = model.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) { renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray(); bounds.Encapsulate(renderer.bounds); }
        float factor = .3f / Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        model.transform.localScale *= factor; model.transform.localPosition = -bounds.center * factor;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "MedicalKitPickup.prefab"); UnityEngine.Object.DestroyImmediate(root);
        var itemData = new SerializedObject(item); itemData.FindProperty("worldPrefab").objectReferenceValue = prefab; itemData.ApplyModifiedPropertiesWithoutUndo();
        effect = ScriptableObject.CreateInstance<ConsumableDefinition>(); AssetDatabase.CreateAsset(effect, folder + "MedicalKitEffect.asset");
        var effectData = new SerializedObject(effect); effectData.FindProperty("item").objectReferenceValue = item;
        effectData.FindProperty("food").floatValue = 0; effectData.FindProperty("water").floatValue = 0; effectData.FindProperty("healing").floatValue = 20;
        effectData.ApplyModifiedPropertiesWithoutUndo(); return effect;
    }
    private static SkillTreeDefinition CreateTree()
    {
        var tree = ScriptableObject.CreateInstance<SkillTreeDefinition>(); AssetDatabase.CreateAsset(tree, TreePath);
        var data = new SerializedObject(tree); var tabs = data.FindProperty("tabs"); tabs.arraySize = 3;
        string[] ids = { "stats", "abilities", "upgrades" }, titles = { "Характеристики", "Способности", "Улучшения" };
        Color[] colors = { new Color(.95f, .62f, .18f), new Color(.25f, .65f, 1), new Color(.75f, .4f, 1) };
        for (int i = 0; i < 3; i++)
        { var row = tabs.GetArrayElementAtIndex(i); row.FindPropertyRelative("id").stringValue = ids[i]; row.FindPropertyRelative("title").stringValue = titles[i]; row.FindPropertyRelative("color").colorValue = colors[i]; }
        var nodes = data.FindProperty("nodes"); nodes.arraySize = 0;
        Node(nodes, "health_5", "Здоровье +5", "stats", new Vector2(570, 100), StatId.HealthMaximum, 5, 0, 5);
        Node(nodes, "stamina_1", "Выносливость +1%", "stats", new Vector2(570, 260), StatId.StaminaCapacity, 0, 1, 5, "health_5");
        Node(nodes, "recovery_5", "Восстановление +5%", "stats", new Vector2(570, 420), StatId.StaminaRecovery, 0, 5, 3, "stamina_1");
        Node(nodes, "walk_2", "Ходьба +2%", "stats", new Vector2(220, 100), StatId.WalkSpeed, 0, 2, 3);
        Node(nodes, "run_3", "Бег +3%", "stats", new Vector2(220, 260), StatId.RunSpeed, 0, 3, 3, "walk_2");
        Node(nodes, "jump_5", "Прыжок +0,05 м", "stats", new Vector2(220, 420), StatId.JumpHeight, .05f, 0, 3, "run_3");
        Node(nodes, "melee_5", "Ближний урон +5%", "stats", new Vector2(800, 100), StatId.MeleeDamage, 0, 5, 3);
        Node(nodes, "firearm_3", "Огнестрел +3%", "stats", new Vector2(800, 260), StatId.FirearmDamage, 0, 3, 3, "melee_5");
        Node(nodes, "medicine", "Полевая медицина", "abilities", new Vector2(400, 170), StatId.HealingPower, 0, 25, 1, "health_5", "field_medicine");
        Node(nodes, "reserve", "Запас сил +10", "upgrades", new Vector2(400, 150), StatId.StaminaCapacity, 10, 0, 2);
        Node(nodes, "resilience", "Здоровье +2%", "upgrades", new Vector2(400, 330), StatId.HealthMaximum, 0, 2, 3, "reserve");
        data.ApplyModifiedPropertiesWithoutUndo(); return tree;
    }
    private static void Node(SerializedProperty nodes, string id, string title, string tab, Vector2 point, StatId stat, float flat, float percent, int ranks, string parent = "", string ability = "")
    {
        int index = nodes.arraySize++; var node = nodes.GetArrayElementAtIndex(index);
        node.FindPropertyRelative("id").stringValue = id; node.FindPropertyRelative("title").stringValue = title;
        node.FindPropertyRelative("description").stringValue = ability == "field_medicine"
            ? "Лечебные предметы восстанавливают на 25% больше HP. Не усиливает еду/воду и не воскрешает."
            : "Улучшает характеристику «" + PlayerProgressionMenu.StatLabel(stat) + "». Каждый ранг добавляет указанный эффект. Увеличение максимального запаса само по себе не восстанавливает потраченный запас.";
        node.FindPropertyRelative("tabId").stringValue = tab; node.FindPropertyRelative("position").vector2Value = point;
        node.FindPropertyRelative("pointCost").intValue = 1; node.FindPropertyRelative("maxRank").intValue = ranks;
        node.FindPropertyRelative("requireAllPrerequisites").boolValue = true;
        var parents = node.FindPropertyRelative("prerequisiteIds"); parents.arraySize = string.IsNullOrEmpty(parent) ? 0 : 1;
        if (parents.arraySize > 0) parents.GetArrayElementAtIndex(0).stringValue = parent;
        var effects = node.FindPropertyRelative("effects"); effects.arraySize = 1; var effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("stat").intValue = (int)stat; effect.FindPropertyRelative("flat").floatValue = flat;
        effect.FindPropertyRelative("percent").floatValue = percent; effect.FindPropertyRelative("abilityId").stringValue = ability;
    }
}
