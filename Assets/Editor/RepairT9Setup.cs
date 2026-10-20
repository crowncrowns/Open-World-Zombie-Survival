using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;
using ZombieSurvival.Services;

/// <summary>Creates repair data and upgrades the existing workbench. Does not save user scenes.</summary>
public static class RepairT9Setup
{
    public const string MaterialFolder = "Assets/CharacterTesting/Items/Repair";
    public const string PolicyPath = "Assets/Resources/RepairPolicies/Default.asset";
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/'); Folder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
    private static ItemDefinition MaterialItem(string name, string id, string title, ItemSubcategory subcategory)
    {
        string path = MaterialFolder + "/" + name + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item != null) return item;
        item = ScriptableObject.CreateInstance<ItemDefinition>(); var data = new SerializedObject(item);
        data.FindProperty("itemId").stringValue = id; data.FindProperty("displayName").stringValue = title;
        data.FindProperty("description").stringValue = "Материал для ремонта на верстаке. Расход зависит от повреждения вещи.";
        data.FindProperty("category").intValue = (int)ItemCategory.Material; data.FindProperty("subcategory").intValue = (int)subcategory;
        data.FindProperty("maxStack").intValue = 20; data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(item, path);
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject(name + "Pickup"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(box, scene);
            box.transform.SetParent(root.transform, false); box.transform.localScale = new Vector3(.18f, .08f, .13f);
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            box.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Weapons/RangeFurniture.mat");
            root.AddComponent<BoxCollider>().size = new Vector3(.2f, .1f, .15f); root.AddComponent<WorldItem>().Initialize(item, 1);
            var pickup = PrefabUtility.SaveAsPrefabAsset(root, MaterialFolder + "/" + name + "Pickup.prefab");
            data = new SerializedObject(item); data.FindProperty("worldPrefab").objectReferenceValue = pickup; data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item); AssetDatabase.SaveAssetIfDirty(item);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        return item;
    }
    private static RepairRecipe Recipe(string id, params RepairRecipe.MaterialRow[] rows)
    {
        string path = "Assets/Resources/RepairRecipes/" + id + ".asset";
        var recipe = AssetDatabase.LoadAssetAtPath<RepairRecipe>(path); if (recipe != null) return recipe;
        recipe = ScriptableObject.CreateInstance<RepairRecipe>(); recipe.profileId = id; recipe.materials = rows;
        AssetDatabase.CreateAsset(recipe, path); return recipe;
    }
    private static void AddToTrader(string profileId, ItemDefinition material, int quantity, long price)
    {
        var profile = Resources.Load<TraderProfile>("Trading/" + profileId); var data = new SerializedObject(profile);
        var list = data.FindProperty("catalog");
        if (Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == material)) return;
        int index = list.arraySize++; var row = list.GetArrayElementAtIndex(index);
        row.FindPropertyRelative("item").objectReferenceValue = material; row.FindPropertyRelative("quantity").intValue = quantity;
        row.FindPropertyRelative("basePrice").longValue = price;
        var categories = data.FindProperty("acceptedCategories");
        if (!Enumerable.Range(0, categories.arraySize).Any(i => categories.GetArrayElementAtIndex(i).intValue == (int)ItemCategory.Material))
        { int categoryIndex = categories.arraySize++; categories.GetArrayElementAtIndex(categoryIndex).intValue = (int)ItemCategory.Material; }
        data.ApplyModifiedProperties(); profile.Validate(); AssetDatabase.SaveAssetIfDirty(profile);
    }
    private static bool Catalog(PlayerSaveSystem save, params ItemDefinition[] materials)
    {
        var data = new SerializedObject(save); var list = data.FindProperty("itemCatalog"); bool changed = false;
        foreach (var material in materials)
        {
            if (Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == material)) continue;
            int index = list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = material; changed = true;
        }
        if (changed) data.ApplyModifiedProperties(); return changed;
    }
    [MenuItem("Tools/Character Lab/Set Up T9 Repair (No Scene Save)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Настраивайте ремонт вне Play.");
        Folder(MaterialFolder); Folder("Assets/Resources/RepairPolicies"); Folder("Assets/Resources/RepairRecipes");
        var cloth = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/CharacterTesting/Items/Cloth.asset");
        if (cloth == null) throw new InvalidOperationException("Не найдена существующая ткань.");
        var parts = MaterialItem("WeaponParts", "WeaponParts", "Оружейные детали", ItemSubcategory.WeaponParts);
        var armor = MaterialItem("ArmorMaterials", "ArmorMaterials", "Бронематериалы", ItemSubcategory.ArmorMaterial);
        var weaponRecipe = Recipe("weapon", new RepairRecipe.MaterialRow { material = parts, fullRepairAmount = 4 });
        var textileRecipe = Recipe("textile", new RepairRecipe.MaterialRow { material = cloth, fullRepairAmount = 4 });
        var armorRecipe = Recipe("armor", new RepairRecipe.MaterialRow { material = armor, fullRepairAmount = 4 }, new RepairRecipe.MaterialRow { material = cloth, fullRepairAmount = 2 });
        if (AssetDatabase.LoadAssetAtPath<RepairPolicy>(PolicyPath) == null)
        {
            var policy = ScriptableObject.CreateInstance<RepairPolicy>(); policy.recipes = new[] { weaponRecipe, textileRecipe, armorRecipe };
            AssetDatabase.CreateAsset(policy, PolicyPath);
        }
        AddToTrader("weapons_ammo", parts, 20, 80); AddToTrader("equipment", armor, 12, 120);
        const string playerPath = "Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
        var player = PrefabUtility.LoadPrefabContents(playerPath);
        try { if (Catalog(player.GetComponent<PlayerSaveSystem>(), parts, armor)) PrefabUtility.SaveAsPrefabAsset(player, playerPath); }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        const string benchPath = "Assets/CharacterTesting/Prefabs/T4WorkbenchSession.prefab";
        var bench = PrefabUtility.LoadPrefabContents(benchPath);
        try
        {
            if (bench.GetComponent<RepairWorkbench>() == null) { bench.AddComponent<RepairWorkbench>(); PrefabUtility.SaveAsPrefabAsset(bench, benchPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(bench); }
        foreach (var actor in UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include))
            if (actor.gameObject.scene.path == "Assets/CharacterTesting/CharacterTest.unity" && Catalog(actor.GetComponent<PlayerSaveSystem>(), parts, armor))
                EditorSceneManager.MarkSceneDirty(actor.gameObject.scene);
        foreach (var service in UnityEngine.Object.FindObjectsByType<WorkbenchServicePoint>(FindObjectsInactive.Include))
            if (service.gameObject.scene.path == "Assets/CharacterTesting/CharacterTest.unity" && service.GetComponent<RepairWorkbench>() == null)
            { Undo.AddComponent<RepairWorkbench>(service.gameObject); EditorSceneManager.MarkSceneDirty(service.gameObject.scene); }
        Debug.Log("T9_REPAIR_SETUP_OK: общие рецепты weapon/textile/armor, материалы у торговцев и в общем каталоге, существующий верстак подключён. Сцены не сохранялись.");
    }
}
