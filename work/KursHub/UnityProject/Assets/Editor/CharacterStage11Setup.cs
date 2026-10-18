using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;

public static class CharacterStage11Setup
{
    private const string Root="Assets/CharacterTesting/Survival";
    private static void Set(UnityEngine.Object o,string name,object value)
    {
        var so=new SerializedObject(o);var p=so.FindProperty(name);
        if(value is string s)p.stringValue=s;else if(value is float f)p.floatValue=f;else if(value is int i)p.intValue=i;else p.objectReferenceValue=value as UnityEngine.Object;
        so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(o);
    }
    private static T Add<T>(GameObject g) where T:Component=>g.GetComponent<T>()??g.AddComponent<T>();
    [MenuItem("Tools/Character Lab/Set Up Stage 11 Character")]
    public static void Run()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!="Assets/CharacterTesting/CharacterTest.unity")throw new InvalidOperationException("Откройте CharacterTest вне Play. Инструмент не заменяет сцену.");
        if(!AssetDatabase.IsValidFolder(Root))AssetDatabase.CreateFolder("Assets/CharacterTesting","Survival");
        var food=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/CharacterTesting/Items/CannedFood.asset");
        if(food.Description.Contains("Употребление появится"))Set(food,"description","Консервы. Восстанавливают сытость: Tab → выбрать предмет → Употребить 1.");
        var water=AssetDatabase.LoadAssetAtPath<ItemDefinition>(Root+"/Water.asset");
        if(water==null)
        {
            water=ScriptableObject.CreateInstance<ItemDefinition>();AssetDatabase.CreateAsset(water,Root+"/Water.asset");
            Set(water,"itemId","water_flask");Set(water,"displayName","Вода во фляге");Set(water,"description","Tab → Употребить 1. Восстанавливает воду; пустая тара пока не создаётся.");
            Set(water,"maxStack",5);Set(water,"category",(int)ItemCategory.Food);Set(water,"subcategory",(int)ItemSubcategory.Food);
            Pickup(water,"Flask");
        }
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/FoodPickup.prefab")==null)Pickup(food,"Canned_food_A_01","FoodPickup");
        var ration=Effect("Ration",food,35,0);var drink=Effect("Water",water,0,40);
        var items=AssetDatabase.FindAssets("t:ItemDefinition").Select(g=>AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(i=>i.Id).ToArray();
        const string path="Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";var player=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var needs=Add<PlayerNeeds>(player);var so=new SerializedObject(needs);var list=so.FindProperty("consumables");
            // Append missing adapters; preserve the designer's existing effects and settings.
            foreach(var effect in new[]{ration,drink})if(!Enumerable.Range(0,list.arraySize).Any(i=>list.GetArrayElementAtIndex(i).objectReferenceValue==effect)){int i=list.arraySize++;list.GetArrayElementAtIndex(i).objectReferenceValue=effect;}
            so.ApplyModifiedPropertiesWithoutUndo();
            var save=Add<PlayerSaveSystem>(player);var ss=new SerializedObject(save);var catalog=ss.FindProperty("itemCatalog");catalog.arraySize=items.Length;for(int i=0;i<items.Length;i++)catalog.GetArrayElementAtIndex(i).objectReferenceValue=items[i];ss.ApplyModifiedPropertiesWithoutUndo();
            var id=Add<SaveIdentity>(player);Set(id,"persistentId","player");
            var motion=Add<PlayerCameraMotion>(player);Set(motion,"viewCamera",player.GetComponentInChildren<Camera>(true));
            PrefabUtility.SaveAsPrefabAsset(player,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(player);}
        foreach(var storage in UnityEngine.Object.FindObjectsByType<ItemStorage>(FindObjectsInactive.Include).Where(s=>s.gameObject.scene==scene))
        {
            var id=Add<SaveIdentity>(storage.gameObject);var so=new SerializedObject(id);
            if(string.IsNullOrWhiteSpace(so.FindProperty("persistentId").stringValue))Set(id,"persistentId",Guid.NewGuid().ToString("N"));
        }
        if(GameObject.Find("Stage11_CharacterSurvival")==null)
        {
            var root=new GameObject("Stage11_CharacterSurvival");
            string[] labels={"Истощить до 10 / 10","Восстановить потребности","Сохранить персонажа","Загрузить персонажа"};
            for(int i=0;i<4;i++)
            {
                var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=labels[i];g.transform.SetParent(root.transform);g.transform.position=new Vector3(-23+i*2,.45f,-30);g.transform.localScale=new Vector3(.5f,.9f,.5f);
                Set(g.AddComponent<SurvivalTestConsole>(),"action",i);
                var sign=new GameObject("Stage11 instructions");sign.transform.SetParent(root.transform);sign.transform.position=new Vector3(-23+i*2,2,-28.4f);
                var text=sign.AddComponent<TextMesh>();text.text="ЭТАП 11\nE: "+labels[i]+"\nF5 — сохранить | F9 — загрузить";WorldSpacePresentationSetup.ConfigurePlaque(text);
            }
            var table=GameObject.CreatePrimitive(PrimitiveType.Cube);table.name="Food and water table";table.transform.SetParent(root.transform);table.transform.position=new Vector3(-20,.5f,-27);table.transform.localScale=new Vector3(3,.3f,1);
            var obstacle=table.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=new Vector3(1,5,1);obstacle.carving=true;
            Place(food,new Vector3(-20.7f,.83f,-27),root.transform);Place(water,new Vector3(-19.3f,.83f,-27),root.transform);
        }
        foreach(var label in GameObject.Find("Stage11_CharacterSurvival").GetComponentsInChildren<TextMesh>())
            label.text=label.text.Replace("Сохранить игру","Сохранить персонажа").Replace("Загрузить игру","Загрузить персонажа");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("STAGE11_SETUP_OK");
    }
    private static ConsumableDefinition Effect(string name,ItemDefinition item,float food,float water)
    {
        var path=Root+"/"+name+"Effect.asset";var value=AssetDatabase.LoadAssetAtPath<ConsumableDefinition>(path);if(value!=null)return value;
        value=ScriptableObject.CreateInstance<ConsumableDefinition>();AssetDatabase.CreateAsset(value,path);Set(value,"item",item);Set(value,"food",food);Set(value,"water",water);return value;
    }
    private static void Pickup(ItemDefinition item,string modelName,string name="WaterPickup")
    {
        var root=new GameObject(name);root.AddComponent<SphereCollider>().radius=.17f;root.AddComponent<WorldItem>().Initialize(item,1);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Polygon-Lite Survival Collection/Prefabs/"+modelName+".prefab");
        if(source==null)throw new InvalidOperationException("Нет проверенной модели "+modelName);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.transform.SetParent(root.transform,false);model.transform.localPosition=Vector3.zero;
        foreach(var c in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Weapons/SurvivalWeaponsURP.mat");var bounds=new Bounds();bool first=true;
        foreach(var r in model.GetComponentsInChildren<Renderer>()) {r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
        float factor=.27f/Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));model.transform.localScale*=factor;model.transform.localPosition=-bounds.center*factor;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);Set(item,"worldPrefab",prefab);
    }
    private static void Place(ItemDefinition item,Vector3 point,Transform parent)
    {var go=(GameObject)PrefabUtility.InstantiatePrefab(item.WorldPrefab);go.transform.SetParent(parent);go.transform.position=point;go.GetComponent<WorldItem>().Initialize(item,3);}
}
