using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using ZombieSurvival.Combat;
using ZombieSurvival.Inventory;
using ZombieSurvival.Weapons;

/// <summary>Авторские обёртки моделей, данные и станция этапа 10. Пакеты Asset Store не меняются.</summary>
public static class WeaponLabSetup
{
    private const string Root="Assets/Weapons";
    [MenuItem("Tools/Character Lab/Set Up Stage 10 Weapons")]
    public static void Run()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Остановите Play.");
        Folder(Root); Folder(Root+"/Definitions"); Folder(Root+"/Prefabs");
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/SurvivalWeaponsURP.mat");
        if(mat==null)
        {
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Polygon-Lite Survival Collection/Textures/Polygon_Texture.png"));
            mat.SetFloat("_Smoothness",.2f); AssetDatabase.CreateAsset(mat,Root+"/SurvivalWeaponsURP.mat");
        }
        var ammo=Item("Ammo9mm","Патроны 9×19","Боезапас пистолета. R — перезарядить магазин.",60);
        var axe=Weapon("CampingAxe",WeaponKind.Melee,"SM_Camping_Axe",mat);
        var pistol=Weapon("ArmyPistol",WeaponKind.Firearm,"SM_Army_Pistol",mat);
        Set(pistol,"ammo",ammo);
        var axeItem=Item("CampingAxe","Туристический топор","Ближний бой. ЛКМ — один удар; расходует выносливость.",1);
        var pistolItem=Item("ArmyPistol","Пистолет 9×19","Полуавтоматический огонь. ПКМ — прицел, R — перезарядка. Подбирается пустым.",1);
        Set(axeItem,"weapon",axe); Set(pistolItem,"weapon",pistol);
        Pickup(axeItem,axe.viewPrefab); Pickup(pistolItem,pistol.viewPrefab); Pickup(ammo,null);
        const string playerPath="Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
        var player=PrefabUtility.LoadPrefabContents(playerPath);
        try { var w=player.GetComponent<PlayerWeapons>(); if(w==null) w=player.AddComponent<PlayerWeapons>(); Set(w,"viewCamera",player.GetComponentInChildren<Camera>(true)); PrefabUtility.SaveAsPrefabAsset(player,playerPath); }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        var scene=EditorSceneManager.OpenScene("Assets/CharacterTesting/CharacterTest.unity");
        if(GameObject.Find("Stage10_Weapons") == null)
        {
            var station=new GameObject("Stage10_Weapons");
            Sign(station.transform,"ЭТАП 10 · ОРУЖИЕ\nE — взять · Tab — экипировать\n1 / 2 — перебор · ЛКМ — атака\nПКМ — прицел · R — перезарядка",new Vector3(21,2.8f,-27),.085f);
            var table=GameObject.CreatePrimitive(PrimitiveType.Cube); table.name="Weapon table"; table.transform.SetParent(station.transform); table.transform.position=new Vector3(21,.5f,-25); table.transform.localScale=new Vector3(4,.3f,1);
            Place(axeItem,new Vector3(19.8f,.88f,-25),1,station.transform);
            Place(pistolItem,new Vector3(21,.88f,-25),1,station.transform);
            Place(ammo,new Vector3(22.2f,.88f,-25),48,station.transform);
            Target(station.transform,new Vector3(21,0,-17),"Мишень · 100 HP",ActorFaction.Infected);
            Target(station.transform,new Vector3(25,0,-17),"Союзник · урон запрещён",ActorFaction.Survivor);
        }
        foreach(var hp in GameObject.Find("Stage10_Weapons").GetComponentsInChildren<Health>())
        {
            if(hp.GetComponent<WeaponPracticeTarget>()!=null) continue;
            var label=GameObject.Find("Stage10_Weapons").GetComponentsInChildren<TextMesh>().FirstOrDefault(t=>t.text==hp.name);
            if(label!=null) label.transform.SetParent(hp.transform,true);
            hp.gameObject.AddComponent<WeaponPracticeTarget>();
        }
        StyleStation();
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("STAGE10_SETUP_OK");
    }
    private static Material Flat(string name,Color color)
    {
        string path=Root+"/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color=color; AssetDatabase.CreateAsset(mat,path); } return mat;
    }
    private static void StyleStation()
    {
        var station=GameObject.Find("Stage10_Weapons");
        foreach(var t in station.GetComponentsInChildren<TextMesh>())
        {
            t.transform.rotation=Quaternion.identity;
            if(t.text.StartsWith("ЭТАП")) { t.text="ЭТАП 10 · ОРУЖИЕ"; t.transform.position=new Vector3(18,1.4f,-25.07f); t.characterSize=.025f; }
            else t.characterSize=.045f;
        }
        var table=station.transform.Find("Weapon table"); table.GetComponent<Renderer>().sharedMaterial=Flat("RangeFurniture",new Color(.12f,.17f,.2f));
        var panel=station.transform.Find("Weapon instructions board");
        if(panel==null) { var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name="Weapon instructions board"; go.transform.SetParent(station.transform); UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); panel=go.transform; }
        panel.position=new Vector3(18,1.4f,-25); panel.localScale=new Vector3(2,.45f,.05f); panel.GetComponent<Renderer>().sharedMaterial=Flat("RangeFurniture",new Color(.12f,.17f,.2f));
        var obstacle=table.GetComponent<NavMeshObstacle>(); if(obstacle==null) obstacle=table.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape=NavMeshObstacleShape.Box; obstacle.center=new Vector3(0,-.5f,0); obstacle.size=new Vector3(1,5,1); obstacle.carving=true;
        foreach(var target in station.GetComponentsInChildren<Health>())
        {
            var blocker=target.GetComponent<NavMeshObstacle>(); if(blocker==null) blocker=target.gameObject.AddComponent<NavMeshObstacle>();
            blocker.shape=NavMeshObstacleShape.Capsule; blocker.radius=.5f; blocker.height=2; blocker.carving=true;
        }
        foreach(var hp in station.GetComponentsInChildren<Health>()) hp.GetComponent<Renderer>().sharedMaterial=Flat(hp.GetComponent<ActorIdentity>().Faction==ActorFaction.Infected?"HostileTarget":"FriendlyTarget",hp.GetComponent<ActorIdentity>().Faction==ActorFaction.Infected?new Color(.65f,.19f,.13f):new Color(.12f,.5f,.3f));
    }
    private static void Folder(string p) { if(!AssetDatabase.IsValidFolder(p)) { int i=p.LastIndexOf('/'); AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1)); } }
    private static void Set(UnityEngine.Object obj,string field,UnityEngine.Object value) { var so=new SerializedObject(obj); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(obj); }
    private static ItemDefinition Item(string id,string title,string description,int stack)
    {
        string path=Root+"/Definitions/"+id+"Item.asset"; var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>(path); if(item!=null) return item;
        item=ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(item,path); var so=new SerializedObject(item);
        so.FindProperty("itemId").stringValue=id; so.FindProperty("displayName").stringValue=title; so.FindProperty("description").stringValue=description; so.FindProperty("maxStack").intValue=stack; so.ApplyModifiedPropertiesWithoutUndo(); return item;
    }
    private static WeaponDefinition Weapon(string id,WeaponKind kind,string modelName,Material material)
    {
        string path=Root+"/Definitions/"+id+".asset"; var data=AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path); if(data!=null) return data;
        data=ScriptableObject.CreateInstance<WeaponDefinition>(); data.kind=kind;
        data.viewPrefab=View(id,modelName,material); data.holdPosition=new Vector3(.28f,-.28f,.5f);
        if(kind==WeaponKind.Firearm)
        {
            data.damage=28; data.range=70; data.cooldown=.25f; data.staminaCost=0; data.noise=new Vector2(12,65);
            data.aimPosition=new Vector3(0,-.075f,.5f); data.holdEuler=new Vector3(0,-90,0);
            data.attacks=Clips("Assets/FreeWeaponSFX_v1.1/Handgun/Gunshots","gunshot");
            data.reloadSound=Clip("Assets/Audio/Generated/PistolReload.wav");
        }
        else { data.damage=40; data.range=1.9f; data.cooldown=.8f; data.windup=.28f; data.staminaCost=16; data.holdPosition=new Vector3(.32f,-.35f,.55f); data.holdEuler=new Vector3(0,-15,-15); data.attacks=Clips("Assets/Audio/Generated","MeleeSwing"); }
        data.impactSound=AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Footsteps - Essentials/Footsteps_Rock/Footsteps_Rock_Jump"}).Select(g=>AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(c=>c.name.Contains("Land"));
        AssetDatabase.CreateAsset(data,path); return data;
    }
    private static AudioClip Clip(string p)=>AssetDatabase.LoadAssetAtPath<AudioClip>(p);
    private static AudioClip[] Clips(string p,string part)=>AssetDatabase.FindAssets("t:AudioClip",new[]{p}).Select(g=>Clip(AssetDatabase.GUIDToAssetPath(g))).Where(c=>c.name.Contains(part)).OrderBy(c=>c.name).ToArray();
    private static GameObject View(string id,string model,Material mat)
    {
        var root=new GameObject(id+"View");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Polygon-Lite Survival Collection/Prefabs/"+model+".prefab");
        var go=(GameObject)PrefabUtility.InstantiatePrefab(source); go.transform.SetParent(root.transform,false); go.transform.localPosition=Vector3.zero;
        foreach(var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach(var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
        // Сначала нормализуем размеры пакета; точная поза оружия задаётся в его Definition.
        var bounds=new Bounds(); bool first=true; foreach(var r in go.GetComponentsInChildren<Renderer>()) { if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds); }
        float size=model.Contains("Axe")?.65f:.26f;
        float factor=size/Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
        go.transform.localScale*=factor; go.transform.localPosition=-bounds.center*factor;
        var result=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+id+"View.prefab"); UnityEngine.Object.DestroyImmediate(root); return result;
    }
    private static void Pickup(ItemDefinition item,GameObject visual)
    {
        string path=Root+"/Prefabs/"+item.name+"Pickup.prefab"; if(item.WorldPrefab!=null) return;
        var root=new GameObject(item.name+"Pickup"); root.AddComponent<SphereCollider>().radius=.17f;
        var world=root.AddComponent<WorldItem>(); world.Initialize(item,1);
        GameObject model;
        if(visual!=null) { model=(GameObject)PrefabUtility.InstantiatePrefab(visual); model.transform.SetParent(root.transform,false); model.transform.localScale=Vector3.one*.42f; model.transform.localRotation=Quaternion.Euler(75,0,0); }
        else { model=GameObject.CreatePrimitive(PrimitiveType.Cube); model.transform.SetParent(root.transform,false); model.transform.localScale=new Vector3(.22f,.12f,.14f); UnityEngine.Object.DestroyImmediate(model.GetComponent<Collider>()); }
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,path); UnityEngine.Object.DestroyImmediate(root); Set(item,"worldPrefab",prefab);
    }
    private static void Place(ItemDefinition item,Vector3 position,int amount,Transform parent)
    { var go=(GameObject)PrefabUtility.InstantiatePrefab(item.WorldPrefab); go.transform.SetParent(parent); go.transform.position=position; go.GetComponent<WorldItem>().Initialize(item,amount); }
    private static void Target(Transform parent,Vector3 position,string title,ActorFaction faction)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Capsule); go.name=title; go.transform.SetParent(parent); go.transform.position=position+Vector3.up; go.transform.localScale=new Vector3(.65f,1,.65f);
        go.AddComponent<Health>(); go.AddComponent<CombatTarget>(); var identity=go.AddComponent<ActorIdentity>(); var so=new SerializedObject(identity); so.FindProperty("faction").enumValueIndex=(int)faction; so.ApplyModifiedPropertiesWithoutUndo();
        Sign(go.transform,title,position+new Vector3(0,2.4f,0),.065f); go.AddComponent<WeaponPracticeTarget>();
    }
    private static void Sign(Transform parent,string text,Vector3 point,float scale)
    { var go=new GameObject("Instructions"); go.transform.SetParent(parent); go.transform.position=point; go.transform.rotation=Quaternion.identity; var t=go.AddComponent<TextMesh>(); t.text=text; t.fontSize=36; t.characterSize=scale; t.anchor=TextAnchor.MiddleCenter; t.color=Color.white; }
}
