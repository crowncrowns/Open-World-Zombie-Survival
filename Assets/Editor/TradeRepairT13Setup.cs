using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;
using ZombieSurvival.Services;

public static class TradeRepairT13Setup
{
    [MenuItem("Tools/Character Lab/T13/Create Isolated Integration Scene")]
    public static void CreateIsolatedScene()
    {
        if(Application.isPlaying||!Application.dataPath.Contains("_CodexRecovery"))throw new InvalidOperationException("Создавайте тестовую сцену только в отдельной recovery-копии, вне Play.");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="T13 Floor";floor.transform.position=Vector3.down*.5f;floor.transform.localScale=new Vector3(30,1,30);
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab"));
        player.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var motor=player.GetComponent<FirstPersonController>();Bool(motor,"requireApplicationFocus",false);Bool(motor,"requireCursorCapture",false);Bool(motor,"captureCursorOnEnable",false);
        Bool(player.GetComponent<PlayerServiceSession>(),"requireApplicationFocus",false);
        var scheduler=player.GetComponent<PlayerSaveSlots>();if(scheduler!=null)scheduler.enabled=false;
        var trader=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TraderT5Setup.PrefabPath));trader.transform.position=Vector3.forward*2;
        var t=trader.GetComponent<Trader>();var edit=new SerializedObject(t);edit.FindProperty("activeProfile").objectReferenceValue=t.Stocks.Single(r=>r.profile.Id=="weapons_ammo").profile;edit.ApplyModifiedPropertiesWithoutUndo();
        Identity(player,"player");Identity(trader,"t13-trader");int child=0;foreach(var stock in t.Stocks)Identity(stock.storage.gameObject,"t13-stock-"+child++);
        var bench=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/T4WorkbenchSession.prefab"));bench.transform.position=new Vector3(6,0,2);
        var chest=new GameObject("T13 Transfer Container");chest.AddComponent<ItemStorage>();chest.AddComponent<WorldSaveObject>();Identity(chest,"t13-transfer-container");
        var registry=new GameObject("T13 World Registry").AddComponent<WorldSaveRegistry>();
        edit=new SerializedObject(registry);var roots=UnityEngine.Object.FindObjectsByType<WorldSaveObject>(FindObjectsInactive.Include);var list=edit.FindProperty("sceneObjects");list.arraySize=roots.Length;
        for(int i=0;i<roots.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=roots[i];edit.ApplyModifiedPropertiesWithoutUndo();
        var save=player.GetComponent<PlayerSaveSystem>();edit=new SerializedObject(save);edit.FindProperty("fileName").stringValue="trade-repair-t13-integration.json";edit.FindProperty("worldRegistry").objectReferenceValue=registry;
        var types=AssetDatabase.FindAssets("t:ItemDefinition").Select(g=>AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();list=edit.FindProperty("itemCatalog");list.arraySize=types.Length;
        for(int i=0;i<types.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=types[i];edit.ApplyModifiedPropertiesWithoutUndo();
        registry.ValidateCoverage();EditorSceneManager.SaveScene(scene,"Assets/T13Integration.unity");
    }
    private static void Bool(UnityEngine.Object target,string field,bool value){var so=new SerializedObject(target);so.FindProperty(field).boolValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Identity(GameObject root,string id){var identity=root.GetComponent<SaveIdentity>()??root.AddComponent<SaveIdentity>();var so=new SerializedObject(identity);so.FindProperty("persistentId").stringValue=id;so.ApplyModifiedPropertiesWithoutUndo();}

    [MenuItem("Tools/Character Lab/T13/Register Uncovered Single Rigidbody Roots (No Scene Save)")]
    public static void RegisterPhysicsRoots()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Регистрация только вне Play.");
        var scene=SceneManager.GetActiveScene();var registry=UnityEngine.Object.FindObjectsByType<WorldSaveRegistry>(FindObjectsInactive.Include).Single(r=>r.gameObject.scene==scene);
        var bodies=UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include).Where(b=>b.gameObject.scene==scene&&b.GetComponentInParent<WorldItem>(true)==null
            &&b.GetComponentInParent<WorldSaveObject>(true)==null&&!(b.isKinematic&&b.GetComponentsInChildren<Collider>(true).All(c=>c.isTrigger))).ToArray();
        // Validate the whole authoring operation before adding anything. Never pretend a ragdoll is a single body.
        foreach(var b in bodies)if(b.GetComponentsInChildren<Rigidbody>(true).Length!=1||b.GetComponentsInParent<Rigidbody>(true).Length!=1)
            throw new InvalidOperationException("Составная физика требует своего адаптера: "+b.name);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("T13 Register Physical Roots");
        try
        {
            foreach(var b in bodies){Undo.AddComponent<WorldSaveObject>(b.gameObject);Undo.AddComponent<WorldRigidbodySaveAdapter>(b.gameObject);}
            var so=new SerializedObject(registry);var list=so.FindProperty("sceneObjects");
            foreach(var b in bodies){int index=list.arraySize++;list.GetArrayElementAtIndex(index).objectReferenceValue=b.GetComponent<WorldSaveObject>();}so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);Debug.Log("T13_PHYSICS_REGISTERED: "+bodies.Length+"; сцену сохраните отдельно после проверки.");
        }
        finally{Undo.CollapseUndoOperations(group);}
    }
}
