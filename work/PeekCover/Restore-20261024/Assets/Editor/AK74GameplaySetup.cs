using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Weapons;
using AK74Reference;

/// <summary>Адресный авторинг нового оружия. Не пересоздаёт игрока, не пишет сцены и не запускает vendor builder.</summary>
public static class AK74GameplaySetup
{
    public const string Folder = "Assets/Weapons/AK74Gameplay";
    public const string Native = "Assets/Weapons/AK74_Reference/Prefabs/AK74_Reference.prefab";
    public const string Player = "Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
    public const string ItemPath = Folder + "/AK74Item.asset";
    public const string AmmoPath = Folder + "/Ammo545Item.asset";

    private static void Set(UnityEngine.Object obj,string field,object value)
    {
        var data=new SerializedObject(obj); var p=data.FindProperty(field);
        if(p==null)throw new InvalidOperationException(field);
        if(value is string text)p.stringValue=text;
        else if(value is int n)p.intValue=n;
        else if(value is bool b)p.boolValue=b;
        else if(value is float f)p.floatValue=f;
        else p.objectReferenceValue=value as UnityEngine.Object;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static T Create<T>(string path,Action<T> setup) where T:ScriptableObject
    {
        var result=AssetDatabase.LoadAssetAtPath<T>(path); if(result!=null)return result;
        result=ScriptableObject.CreateInstance<T>(); setup(result); AssetDatabase.CreateAsset(result,path); return result;
    }
    private static ItemDefinition Item(string path,string id,string title,string description,int stack,ItemCategory category)
        => Create<ItemDefinition>(path,item=>
        {
            Set(item,"itemId",id); Set(item,"displayName",title); Set(item,"description",description);
            Set(item,"maxStack",stack); Set(item,"category",(int)category);
            Set(item,"subcategory",category==ItemCategory.Weapon?(int)ItemSubcategory.Firearm:(int)ItemSubcategory.Ammunition);
            Set(item,"allowInHotbar",category==ItemCategory.Weapon);
            if(category==ItemCategory.Weapon) { Set(item,"repairable",true); Set(item,"repairProfileId","weapon"); }
        });
    private static GameObject View()
    {
        string path=Folder+"/AK74View.prefab"; var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(existing!=null)return existing;
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Native); if(source==null)throw new InvalidOperationException("Нет AK74_Reference.prefab");
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("AK74View"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source,scene); model.transform.SetParent(root.transform,false);
            foreach(var c in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
            var receiver=model.GetComponentInChildren<AK74Presentation>(true);
            var bridge=root.AddComponent<WeaponModelAnimation>(); Set(bridge,"modelAnimator",model.GetComponentInChildren<Animator>(true));
            var data=new SerializedObject(bridge);
            foreach(var pair in new[]{("installedMagazine",receiver.magazineRenderers),("spareMagazine",receiver.spareMagazineRenderers)})
            {
                var array=data.FindProperty(pair.Item1); array.arraySize=pair.Item2.Length;
                for(int i=0;i<array.arraySize;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=pair.Item2[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
    }
    private static GameObject Pickup(ItemDefinition item,bool rifle)
    {
        string path=Folder+(rifle?"/AK74Pickup.prefab":"/Ammo545Pickup.prefab");
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(existing!=null){if(item.WorldPrefab==null){Set(item,"worldPrefab",existing);AssetDatabase.SaveAssetIfDirty(item);}return existing;}
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject(rifle?"АК-74 · подобрать":"Патроны 5.45×39 · подобрать");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            if(rifle)
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Native),scene);
                model.transform.SetParent(root.transform,false); model.transform.localRotation=Quaternion.Euler(0,90,90);
                foreach(var c in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
                // В мире оружие лежит в rest pose. В руках отдельный wrapper включает Animator/bridge.
                foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            }
            else
            {
                var box=GameObject.CreatePrimitive(PrimitiveType.Cube); box.name="Коробка 5.45×39";
                box.transform.SetParent(root.transform,false); box.transform.localScale=new Vector3(.16f,.075f,.11f);
                UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
                box.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Weapons/RangeFurniture.mat");
            }
            var bounds=new Bounds(); bool first=true;
            foreach(var r in root.GetComponentsInChildren<Renderer>()) {if(!r.enabled)continue;if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            var collider=root.AddComponent<BoxCollider>(); collider.center=bounds.center; collider.size=bounds.size;
            var body=root.AddComponent<Rigidbody>(); body.mass=rifle?3.3f:.25f; body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            root.AddComponent<WorldItem>().Initialize(item,rifle?1:60);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path); Set(item,"worldPrefab",prefab); AssetDatabase.SaveAssetIfDirty(item); return prefab;
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
    }
    [MenuItem("Tools/Zombie Survival/АК-74/Создать и подключить ассеты")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Остановите Play.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Weapons","AK74Gameplay");
        var ammo=Item(AmmoPath,"Ammo545x39","Патроны 5.45×39","Боезапас АК-74. R — перезарядка. В хотбар не назначаются.",60,ItemCategory.Ammunition);
        var item=Item(ItemPath,"AK74","АК-74","Автоматический огонь — удерживать ЛКМ. ПКМ — прицел, R — перезарядка. Использует патроны 5.45×39; подбирается с пустым магазином.",1,ItemCategory.Weapon);
        var view=View();
        var hand=Create<WeaponHandPose>(Folder+"/AK74HandPose.asset",pose=>
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Native);
            var reference=source.GetComponentInChildren<AK74Presentation>(true);
            Vector3 right=source.transform.InverseTransformPoint(reference.rightHand.position);
            pose.leftGrip=source.transform.InverseTransformPoint(reference.leftHand.position);
            // Дальний сокет цевья вытягивал короткий Humanoid-аватар и уводил общий прицел назад/влево.
            // Для этого игрока опора у магазина; собственный профиль позволяет перенастроить её без правок мотора.
            pose.leftGrip=new Vector3(pose.leftGrip.x,-.015f,.05f);
            pose.aimPosition=new Vector3(.025f,-.078f,.43f);
            pose.cameraEuler=new Vector3(1.28f,.75f,-1.72f);
            pose.elbowHint=new Vector3(.45f,-.2f,-.1f);
            // Сохраняем ориентацию кисти существующего пистолета, компенсируя новую ось ствола +Z.
            Quaternion mount=Quaternion.Euler(90,0,0)*Quaternion.Euler(0,90,0);
            pose.handEuler=mount.eulerAngles; pose.handPosition=-(mount*right);
        });
        var weapon=Create<WeaponDefinition>(Folder+"/AK74.asset",w=>
        {
            w.kind=WeaponKind.Firearm; w.damage=32; w.range=120; w.cooldown=.1f; w.automatic=true; w.staminaCost=0;
            w.durabilityCostPerUse=.2f; w.reloadSeconds=2.2f; w.hipSpread=2; w.aimSpread=.25f; w.recoil=.8f; w.recoilRecovery=6;
            w.noise=new Vector2(16,85); w.volume=.45f; w.viewPrefab=view; w.handPose=hand;
            w.holdPosition=hand.hipPosition; w.aimPosition=hand.aimPosition; w.holdEuler=Vector3.zero;
            w.muzzleOffset=new Vector3(.16f,-.19f,.99f); w.aimMuzzleOffset=new Vector3(0,-.055f,.93f);
            Set(w,"ammo",ammo); Set(w,"magazineSize",30);
            w.attacks=Enumerable.Range(1,3).Select(i=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Props/FreeWeaponSFX_v1.1/AssaultRifle/Gunshots/sfx_wpn_ar_gunshot_0"+i+".wav")).ToArray();
            w.reloadSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/AK74/AK74_Reload.wav");
            if(w.attacks.Any(c=>c==null)||w.reloadSound==null)throw new InvalidOperationException("Не найдены звуки АК-74.");
        });
        AK74BodyAnimationSetup.Ensure(weapon);
        if(item.Weapon==null)Set(item,"weapon",weapon);
        Pickup(item,true); Pickup(ammo,false);
        // Каталог расширяется адресно; чужие настройки игрока/камера/ID/хват и сцены не перезаписываются.
        var player=PrefabUtility.LoadPrefabContents(Player);
        try
        {
            var data=new SerializedObject(player.GetComponent<PlayerSaveSystem>()); var catalog=data.FindProperty("itemCatalog"); bool changed=false;
            foreach(var entry in new[]{item,ammo})if(!Enumerable.Range(0,catalog.arraySize).Any(i=>catalog.GetArrayElementAtIndex(i).objectReferenceValue==entry))
            {int index=catalog.arraySize++;catalog.GetArrayElementAtIndex(index).objectReferenceValue=entry;changed=true;}
            if(changed){data.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(player,Player);}
        }
        finally{PrefabUtility.UnloadPrefabContents(player);}
        foreach(var obj in new UnityEngine.Object[]{item,ammo,weapon,hand})AssetDatabase.SaveAssetIfDirty(obj);
        Debug.Log("AK74_GAMEPLAY_SETUP_OK: AK74Pickup и Ammo545Pickup готовы; сцены не сохранялись.");
    }
    [MenuItem("Tools/Zombie Survival/АК-74/Поставить оружие и патроны перед игроком")]
    public static void PlaceInOpenScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Остановите Play.");
        var player=UnityEngine.Object.FindObjectsByType<ZombieSurvival.Player.FirstPersonController>(FindObjectsInactive.Include)
            .FirstOrDefault(p=>p.gameObject.scene==UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        if(player==null)throw new InvalidOperationException("В активной сцене нет игрока.");
        var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemPath);var ammo=AssetDatabase.LoadAssetAtPath<ItemDefinition>(AmmoPath);
        if(item==null||ammo==null)throw new InvalidOperationException("Сначала создайте ассеты АК-74.");
        Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Поставить АК-74 и патроны");
        int index=0;
        foreach(var type in new[]{item,ammo})
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(type.WorldPrefab,player.gameObject.scene);Undo.RegisterCreatedObjectUndo(go,"АК-74");
            Vector3 point=player.transform.position+player.transform.forward*1.5f+player.transform.right*(index++==0?-.3f:.3f);
            if(Physics.Raycast(point+Vector3.up*3,Vector3.down,out var hit,20,~0,QueryTriggerInteraction.Ignore))point=hit.point;
            go.transform.position=point+Vector3.up*.25f;
        }
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
    }
}
