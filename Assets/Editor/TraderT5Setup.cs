using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Services;
using ZombieSurvival.Persistence;

public static class TraderT5Setup
{
    public const string Folder = "Assets/CharacterTesting/Trading";
    public const string PrefabPath = Folder + "/T5Trader.prefab";
    private static void FolderAt(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/'); FolderAt(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
    private static void Text(UnityEngine.Object obj, string field, string value)
    { var s = new SerializedObject(obj); s.FindProperty(field).stringValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static TraderProfile Profile(string id, string label, ItemCategory[] accepted, string[] paths, int[] quantities, long[] prices)
    {
        string path = "Assets/Resources/Trading/" + id + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<TraderProfile>(path); if (existing != null) return existing;
        var profile = ScriptableObject.CreateInstance<TraderProfile>();
        var data = new SerializedObject(profile); data.FindProperty("profileId").stringValue = id; data.FindProperty("displayName").stringValue = label;
        var categories = data.FindProperty("acceptedCategories"); categories.arraySize = accepted.Length;
        for (int i = 0; i < accepted.Length; i++) categories.GetArrayElementAtIndex(i).intValue = (int)accepted[i];
        var catalog = data.FindProperty("catalog"); catalog.arraySize = paths.Length;
        for (int i = 0; i < paths.Length; i++)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(paths[i]); if (item == null) throw new InvalidOperationException("Нет существующего предмета: " + paths[i]);
            var entry = catalog.GetArrayElementAtIndex(i); entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("quantity").intValue = quantities[i]; entry.FindPropertyRelative("basePrice").longValue = prices[i];
        }
        data.ApplyModifiedPropertiesWithoutUndo(); profile.Validate(); AssetDatabase.CreateAsset(profile, path); return profile;
    }
    [MenuItem("Tools/Character Lab/Create T5 Trader Assets")]
    public static void CreateAssets()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Создавайте авторские ассеты вне Play.");
        ServiceSessionsT4Setup.EnsurePlayerPrefab();
        FolderAt(Folder); FolderAt("Assets/Resources/Trading");
        Profile("weapons_ammo", "Оружие и патроны", new[] { ItemCategory.Weapon, ItemCategory.Ammunition },
            new[] { "Assets/Weapons/Definitions/ArmyPistolItem.asset", "Assets/Weapons/Definitions/CampingAxeItem.asset", "Assets/Weapons/Definitions/Ammo9mmItem.asset" }, new[] { 2, 1, 120 }, new long[] { 8000, 1500, 12 });
        Profile("equipment", "Снаряжение", new[] { ItemCategory.Equipment, ItemCategory.Material },
            new[] { "Assets/CharacterTesting/Items/T2/TestBackpack.asset", "Assets/CharacterTesting/Items/Cloth.asset" }, new[] { 2, 20 }, new long[] { 2500, 40 });
        Profile("food_medicine", "Еда и медицина", new[] { ItemCategory.Food, ItemCategory.Medicine },
            new[] { "Assets/CharacterTesting/Items/CannedFood.asset", "Assets/CharacterTesting/Survival/Water.asset", "Assets/CharacterTesting/Progression/MedicalKit.asset" }, new[] { 10, 5, 3 }, new long[] { 180, 100, 600 });
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        var root = PrefabUtility.LoadPrefabContents("Assets/CharacterTesting/Prefabs/T4TraderSession.prefab");
        try
        {
            root.name = "T5Trader";
            root.GetComponent<ServiceVisual>().SetModel(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bubba_The_Handyman/URP/Prefabs/Handyman_ver_1.prefab"));
            var pose = new SerializedObject(root.GetComponent<ServiceVisual>()); pose.FindProperty("localEulerAngles").vector3Value = Vector3.zero; pose.ApplyModifiedPropertiesWithoutUndo(); root.GetComponent<ServiceVisual>().ApplyModel();
            var trader = root.AddComponent<Trader>();
            var save = root.AddComponent<WorldSaveObject>(); root.AddComponent<WorldWalletSaveAdapter>(); root.AddComponent<WorldTraderSaveAdapter>();
            Text(save.GetComponent<SaveIdentity>(), "persistentId", "t5-trader-authoring");
            foreach (var row in trader.Stocks)
            {
                var identity = row.storage.gameObject.AddComponent<SaveIdentity>(); Text(identity, "persistentId", "t5-stock-" + row.profile.Id);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    [MenuItem("Tools/Character Lab/Add T5 Traders To CharacterTest (No Save)")]
    public static void AddStations()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Добавляйте станцию вне Play.");
        var scene = Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(UnityEngine.SceneManagement.SceneManager.GetSceneAt)
            .FirstOrDefault(s => s.path == "Assets/CharacterTesting/CharacterTest.unity");
        if (!scene.IsValid()) throw new InvalidOperationException("CharacterTest должна быть уже открыта; другие сцены команда не открывает.");
        ServiceSessionsT4Setup.EnsurePlayerPrefab(); ServiceSessionsT4Setup.EnsureScenePlayers(scene);
        if (scene.GetRootGameObjects().Any(r => r.name == "T5 - Профили торговцев")) return;
        CreateAssets();
        var registry = UnityEngine.Object.FindObjectsByType<WorldSaveRegistry>(FindObjectsInactive.Include).Single(r => r.gameObject.scene == scene);
        var group = new GameObject("T5 - Профили торговцев"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(group, scene); Undo.RegisterCreatedObjectUndo(group, "Добавить тест Т5");
        var added = new System.Collections.Generic.List<WorldSaveObject>();
        string[] profiles = { "weapons_ammo", "food_medicine" };
        for (int i = 0; i < 2; i++)
        {
            var npc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), group.transform);
            npc.name = "T5 Trader " + (i + 1); npc.transform.position = new Vector3(28 - i * 4, 0, 19); npc.transform.rotation = Quaternion.Euler(0, 180, 0);
            foreach (var id in npc.GetComponentsInChildren<SaveIdentity>(true)) Text(id, "persistentId", "t5-" + Guid.NewGuid().ToString("N"));
            var trader = npc.GetComponent<Trader>(); trader.TrySelectProfile(trader.Stocks.Single(r => r.profile.Id == profiles[i]).profile);
            added.Add(npc.GetComponent<WorldSaveObject>());
            var plaque = new GameObject("Т5 / Инструкция " + (i + 1)); plaque.transform.SetParent(group.transform, false);
            plaque.transform.position = new Vector3(28 - i * 4, .035f, 17.2f);
            var text = plaque.AddComponent<TextMesh>(); text.text = "Т5 · " + trader.Profile.DisplayName + "\nE — посмотреть личные остатки\nТип торговца: Inspector → Trader\nСмена типа не пополняет товары и рубли\nПокупка и продажа пока недоступны";
            WorldSpacePresentationSetup.ConfigurePlaque(text);
            plaque.GetComponentInChildren<Canvas>().transform.position = new Vector3(28 - i * 4, .035f, 17.2f);
        }
        Undo.RecordObject(registry, "Зарегистрировать Т5 в общем мире");
        var data = new SerializedObject(registry); var objects = data.FindProperty("sceneObjects"); int start = objects.arraySize; objects.arraySize += added.Count;
        for (int i = 0; i < added.Count; i++) objects.GetArrayElementAtIndex(start + i).objectReferenceValue = added[i];
        data.ApplyModifiedProperties(); EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Т5: добавлены только два NPC X28/24,Z19 и записи реестра; сцена НЕ сохранена. E — просмотр профиля/остатков, сделки отсутствуют.");
    }
}
