using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Inventory;

public static class InventoryT2Setup
{
    public const string Root = "Assets/CharacterTesting/Items/T2";
    public const string GearPath = Root + "/TestBackpack.asset";
    private static void Set(UnityEngine.Object target, string field, object value)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(field);
        if (value is string s) p.stringValue = s;
        else if (value is int i) p.intValue = i;
        else if (value is bool b) p.boolValue = b;
        else if (value is float f) p.floatValue = f;
        else p.objectReferenceValue = value as UnityEngine.Object;
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
    }
    private static void Classify(string path, ItemCategory category, ItemSubcategory subcategory, string profile = "")
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null) throw new InvalidOperationException("Missing existing type: " + path);
        Set(item,"category",(int)category); Set(item,"subcategory",(int)subcategory);
        Set(item,"repairable",profile.Length > 0); Set(item,"repairProfileId",profile);
        if (profile.Length > 0) Set(item,"maximumDurability",100f);
    }
    [MenuItem("Tools/Character Lab/Set Up T2 Item State Tests")]
    public static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/CharacterTesting/CharacterTest.unity") throw new InvalidOperationException("Open CharacterTest in Edit Mode first; scene is never replaced.");
        Classify("Assets/Weapons/Definitions/ArmyPistolItem.asset", ItemCategory.Weapon, ItemSubcategory.Firearm,"weapon");
        Classify("Assets/Weapons/Definitions/CampingAxeItem.asset", ItemCategory.Weapon, ItemSubcategory.Melee,"weapon");
        Classify("Assets/Weapons/Definitions/Ammo9mmItem.asset", ItemCategory.Ammunition, ItemSubcategory.Ammunition);
        Classify("Assets/CharacterTesting/Items/CannedFood.asset", ItemCategory.Food, ItemSubcategory.Food);
        Classify("Assets/CharacterTesting/Items/Cloth.asset", ItemCategory.Material, ItemSubcategory.Textile);
        System.IO.Directory.CreateDirectory(Root); AssetDatabase.Refresh();
        var gear = AssetDatabase.LoadAssetAtPath<ItemDefinition>(GearPath);
        if (gear == null)
        {
            gear = ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(gear,GearPath);
            Set(gear,"itemId","TestBackpack"); Set(gear,"displayName","Тестовый рюкзак");
            Set(gear,"description","Т2: прочность экземпляра, без экипировки и бонусов вместимости.");
            Set(gear,"category",(int)ItemCategory.Equipment); Set(gear,"subcategory",(int)ItemSubcategory.Backpack);
            Set(gear,"maximumDurability",100f); Set(gear,"repairable",true); Set(gear,"repairProfileId","textile");
            var root = new GameObject("Test backpack pickup");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.name="Prototype backpack body"; body.transform.SetParent(root.transform,false); body.transform.localScale=new Vector3(.22f,.27f,.12f);
            // No bag/pack assets were present in Search: an explicit prototype, not a vendor model.
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=new Color(.23f,.34f,.24f);
            AssetDatabase.CreateAsset(material,Root+"/TestBackpack.mat"); body.GetComponent<Renderer>().sharedMaterial=material;
            var world = root.AddComponent<WorldItem>(); world.Initialize(gear,1);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root,Root+"/TestBackpack.prefab"); UnityEngine.Object.DestroyImmediate(root); Set(gear,"worldPrefab",prefab);
        }
        if (GameObject.Find("T2 - Состояние предметов") == null)
        {
            var zone = new GameObject("T2 - Состояние предметов");
            for (int i=0;i<2;i++)
            {
                var obj=(GameObject)PrefabUtility.InstantiatePrefab(gear.WorldPrefab,zone.transform);
                obj.name="Рюкзак — прочность " + (i==0?35:80);
                obj.transform.position=new Vector3(6.8f+i*1.1f,.17f,-21.7f);
                obj.GetComponent<WorldItem>().Initialize(new ItemStack(gear,1,i==0?35f:80f));
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj.GetComponent<WorldItem>());
            }
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
}
