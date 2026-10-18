using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Combat;
using ZombieSurvival.Economy;
using ZombieSurvival.Interaction;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;

public static class WorldSavingSetup
{
    public const string Folder = "Assets/CharacterTesting/WorldSaving";
    private static T Add<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();
    private static void Set(UnityEngine.Object target, string field, object value)
    {
        var data = new SerializedObject(target); var p = data.FindProperty(field);
        if (value is string text) p.stringValue = text; else if (value is Vector3 point) p.vector3Value = point;
        else if (value is int number) p.intValue = number; else p.objectReferenceValue = value as UnityEngine.Object;
        data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
    }
    [MenuItem("Tools/Character Lab/Set Up CM04 World Saving")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/CharacterTesting/CharacterTest.unity") throw new InvalidOperationException("Откройте CharacterTest вне Play.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/CharacterTesting", "WorldSaving");
        foreach (var path in new[] { "Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab", "Assets/Characters/Enemies/ZombieMaleActor.prefab" })
        {
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Add<WorldSaveObject>(prefab);
                if(prefab.GetComponent<ActorIdentity>()!=null)Add<WorldIdentitySaveAdapter>(prefab);
                if (prefab.GetComponent<FirstPersonController>() == null) Add<WorldActorSaveAdapter>(prefab);
                if (prefab.GetComponent<Wallet>() != null) Add<WorldWalletSaveAdapter>(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        var barrel = Barrel(); var zombie = Definition("ZombieSpawn", "zombie-male", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Enemies/ZombieMaleActor.prefab"));
        var registryObject = GameObject.Find("CM04 - World save registry") ?? new GameObject("CM04 - World save registry");
        var registry = Add<WorldSaveRegistry>(registryObject);
        var data = new SerializedObject(registry); var catalog = data.FindProperty("prefabCatalog");
        foreach (var type in new[] { barrel, zombie })
            if (!Enumerable.Range(0, catalog.arraySize).Any(i => catalog.GetArrayElementAtIndex(i).objectReferenceValue == type))
            { int i = catalog.arraySize++; catalog.GetArrayElementAtIndex(i).objectReferenceValue = type; }
        data.ApplyModifiedPropertiesWithoutUndo();
        if (GameObject.Find("CM04 - World save tests") == null)
        {
            var station = new GameObject("CM04 - World save tests");
            string[] labels = { "Создать контейнер", "Создать зомби (восток)", "Удалить контейнеры" };
            for (int i = 0; i < 3; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = labels[i]; go.transform.SetParent(station.transform);
                go.transform.position = new Vector3(17+i*3, .45f, -31); go.transform.localScale = new Vector3(.55f, .9f, .55f);
                var console = go.AddComponent<WorldSaveTestConsole>(); Set(console, "action", i); Set(console, "registry", registry); Set(console, "definition", i == 1 ? zombie : barrel);
                Set(console, "spawnPoint", i == 1 ? new Vector3(56, .03f, -24) : new Vector3(16, .03f, -27));
                Set(console, "supplies", AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/CharacterTesting/Items/CannedFood.asset"));
                var sign = new GameObject("CM04 instructions"); sign.transform.SetParent(station.transform); sign.transform.position = new Vector3(17+i*3, 2, -29.4f);
                var text = sign.AddComponent<TextMesh>(); text.text = "CM-04 / СОХРАНЕНИЕ МИРА\nE: " + labels[i] + "\nF5 / F9: мир + вещи + рубли\nДверь, выключатель и NPC также сохраняются";
                WorldSpacePresentationSetup.ConfigurePlaque(text);
            }
        }
        // Настраиваем реальные компоненты по контрактам, а не по названиям моделей/объектов сцены.
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(c => c != null && c.gameObject.scene == scene).ToArray())
        {
            var go = component.gameObject;
            bool actor = component is Health && go.GetComponent<FirstPersonController>() == null;
            if (!(component is Wallet || component is ActorIdentity || component is ItemStorage || component is DoorInteractable || component is ToggleInteractable || actor)) continue;
            if(component is ItemStorage&&go.GetComponentInParent<WorldSaveObject>(true)!=null&&go.GetComponent<WorldSaveObject>()==null)
            {
                var childId=Add<SaveIdentity>(go);if(string.IsNullOrWhiteSpace(childId.Id))Set(childId,"persistentId",Guid.NewGuid().ToString("N"));
                continue;
            }
            Add<WorldSaveObject>(go);
            if(component is ActorIdentity)Add<WorldIdentitySaveAdapter>(go);
            if (actor) Add<WorldActorSaveAdapter>(go);
            if (component is Wallet) Add<WorldWalletSaveAdapter>(go);
            if (component is DoorInteractable) Add<WorldDoorSaveAdapter>(go);
            if (component is ToggleInteractable) Add<WorldToggleSaveAdapter>(go);
            var identity = Add<SaveIdentity>(go);
            if (string.IsNullOrWhiteSpace(identity.Id)) Set(identity, "persistentId", Guid.NewGuid().ToString("N"));
        }
        var roots = UnityEngine.Object.FindObjectsByType<WorldSaveObject>(FindObjectsInactive.Include).Where(r => r.gameObject.scene == scene).OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
        data.Update(); var objects = data.FindProperty("sceneObjects"); objects.arraySize = roots.Length;
        for (int i = 0; i < roots.Length; i++) objects.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach (var save in UnityEngine.Object.FindObjectsByType<PlayerSaveSystem>().Where(s => s.gameObject.scene == scene)) Set(save, "worldRegistry", registry);
        registry.ValidateCoverage(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("CM04_SETUP_OK: " + roots.Length + " объектов мира.");
    }
    private static WorldPrefabDefinition Definition(string file, string id, GameObject prefab)
    {
        var path = Folder + "/" + file + ".asset"; var type = AssetDatabase.LoadAssetAtPath<WorldPrefabDefinition>(path); if (type != null) return type;
        type = ScriptableObject.CreateInstance<WorldPrefabDefinition>(); AssetDatabase.CreateAsset(type, path); Set(type, "prefabId", id); Set(type, "prefab", prefab); return type;
    }
    private static WorldPrefabDefinition Barrel()
    {
        var type = AssetDatabase.LoadAssetAtPath<WorldPrefabDefinition>(Folder + "/SupplyBarrel.asset"); if (type != null) return type;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Polygon-Lite Survival Collection/Prefabs/SM_Barrel_Open_01.prefab");
        if (source == null) throw new InvalidOperationException("Не найдена подтверждённая модель SM_Barrel_Open_01.");
        var root = new GameObject("SupplyBarrel"); var collider = root.AddComponent<BoxCollider>(); collider.center = Vector3.up*.4f; collider.size = new Vector3(.65f,.8f,.65f);
        root.AddComponent<WorldSaveObject>(); root.AddComponent<ItemStorage>(); root.AddComponent<StorageContainer>();
        var model = (GameObject)PrefabUtility.InstantiatePrefab(source); model.transform.SetParent(root.transform, false);
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        var renderers = model.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Weapons/SurvivalWeaponsURP.mat");
        foreach (var r in renderers) { bounds.Encapsulate(r.bounds); r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray(); }
        float scale = .75f/Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y,bounds.size.z)); model.transform.localScale *= scale;
        model.transform.localPosition = Vector3.up*.4f-bounds.center*scale;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/SupplyBarrel.prefab"); UnityEngine.Object.DestroyImmediate(root);
        return Definition("SupplyBarrel", "supply-barrel", prefab);
    }
}
