using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Combat;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;
using ZombieSurvival.Services;
using ZombieSurvival.Weapons;

/// <summary>Real PlayerWeapons checks; call coroutine only in the isolated recovery project's Play Mode.</summary>
public static class WeaponWearT8Checks
{
    public static bool Completed { get; private set; }
    public static string LastReport { get; private set; }
    public static IEnumerator RunPlay()
    {
        Completed=false; LastReport="Running";
        if(!Application.isPlaying || !Application.dataPath.Contains("_CodexRecovery")) { LastReport="FAIL: isolated Play required";Completed=true;yield break; }
        var root=new GameObject("T8 weapon checks");root.SetActive(false);var assets=new List<UnityEngine.Object>();var results=new List<string>();
        var routine=Scenario(root,assets,results);
        while(true)
        {
            bool next;
            try { next=routine.MoveNext(); } catch(Exception error) { results.Add("FAIL "+error);break; }
            if(!next)break;yield return routine.Current;
        }
        (routine as IDisposable)?.Dispose();UnityEngine.Object.Destroy(root);foreach(var asset in assets)UnityEngine.Object.Destroy(asset);
        LastReport=string.Join("\n",results);Completed=true;
    }
    private static void Set(UnityEngine.Object target,string field,object value)
    {
        var so=new SerializedObject(target);var p=so.FindProperty(field);
        if(value is bool b)p.boolValue=b;else if(value is float f)p.floatValue=f;else if(value is int n)p.intValue=n;else if(value is string s)p.stringValue=s;else p.objectReferenceValue=value as UnityEngine.Object;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static IEnumerator Scenario(GameObject root,List<UnityEngine.Object> assets,List<string> results)
    {
        void Check(bool pass,string name){if(!pass)throw new InvalidOperationException(name);results.Add("PASS "+name);}
        var profile=Resources.Load<TraderProfile>("Trading/weapons_ammo");
        ItemDefinition Clone(WeaponKind kind)
        {
            var source=profile.Catalog.First(e=>e.item.Weapon!=null&&e.item.Weapon.kind==kind).item;
            var item=UnityEngine.Object.Instantiate(source);var weapon=UnityEngine.Object.Instantiate(source.Weapon);assets.Add(item);assets.Add(weapon);Set(item,"weapon",weapon);
            weapon.viewPrefab=null;weapon.attacks=Array.Empty<AudioClip>();weapon.impactSound=weapon.reloadSound=null;
            weapon.durabilityCostPerUse=kind==WeaponKind.Firearm?.5f:1f;weapon.cooldown=.12f;weapon.windup=.06f;weapon.staminaCost=0;weapon.hipSpread=weapon.aimSpread=weapon.recoil=0;weapon.muzzleOffset=weapon.aimMuzzleOffset=Vector3.zero;weapon.damage=20;return item;
        }
        var pistolType=Clone(WeaponKind.Firearm);var axeType=Clone(WeaponKind.Melee);
        var player=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab"),root.transform);
        player.transform.SetPositionAndRotation(new Vector3(10000,0,10000),Quaternion.identity);
        var motor=player.GetComponent<FirstPersonController>();Set(motor,"requireApplicationFocus",false);Set(motor,"requireCursorCapture",false);Set(motor,"captureCursorOnEnable",false);
        var scheduler=player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSlots>();if(scheduler!=null)scheduler.enabled=false;
        Set(player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSystem>(),"fileName","weapon-wear-t8-isolated.json");
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=player.transform.position+Vector3.down*.5f;floor.transform.localScale=new Vector3(12,1,12);
        root.SetActive(true);yield return null;yield return null;yield return null;
        var storage=player.GetComponent<ItemStorage>();var weapons=player.GetComponent<PlayerWeapons>();var hotbar=player.GetComponent<PlayerHotbar>();
        var chest=new GameObject("T8 chest");chest.transform.SetParent(root.transform);var destination=chest.AddComponent<ItemStorage>();
        var save=player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSystem>();
        var pistol=new ItemStack(pistolType,1,1);Check(storage.TryAddStack(pistol)&&storage.TryAddAll(pistolType.Weapon.Ammo,30),"original pistol and ammo inserted");storage.Reload(pistol);
        int Index(ItemStack item)=>Enumerable.Range(0,storage.SlotCount).First(i=>storage.GetSlot(i)==item);
        bool Equip(ItemStack item)=>hotbar.Assign(0,Index(item))&&hotbar.Select(0)&&weapons.Equip(Index(item));
        Check(Equip(pistol),"real player prefab equips pistol");yield return new WaitForSeconds(.23f);
        var camera=player.GetComponentInChildren<Camera>();
        var target=new GameObject("T8 damage target");target.SetActive(false);target.transform.SetParent(root.transform);target.transform.position=camera.transform.position+camera.transform.forward*1.25f;
        var box=target.AddComponent<BoxCollider>();box.size=new Vector3(1.4f,1.4f,.25f);var identity=target.AddComponent<ActorIdentity>();Set(identity,"faction",(int)ActorFaction.Infected);
        var health=target.AddComponent<Health>();Set(health,"maximum",1000f);target.AddComponent<CombatTarget>();target.SetActive(true);Physics.SyncTransforms();
        int rounds=pistol.LoadedRounds,notices=0;bool final=true,reentry=true;
        Action observer=()=>
        {
            notices++;final &= pistol.LoadedRounds==rounds-1&&pistol.Durability==.5f;
            reentry &= !weapons.TryAttack()&&!weapons.TryReload()&&!weapons.Equip(Index(pistol));weapons.Unequip();reentry &= weapons.Equipped==pistol;
            reentry &= !storage.TrySetDurability(Index(pistol),90)&&!storage.SpendRound(pistol)&&!storage.TryUseWeapon(pistol,1,true,out _);
            reentry &= storage.TransferTo(destination,Index(pistol),1)==0&&storage.Reload(pistol)==0;
            reentry &= PlayerWeapons.IsBusy&&ItemStorage.IsWeaponUseBusy&&!save.CanSaveNow(out _)&&!save.TryLoad();
        };
        storage.Changed+=observer;float hp=health.Current;
        Check(weapons.TryAttack(),"real firearm confirmed shot");storage.Changed-=observer;
        Check(notices==1&&final&&reentry,"one Changed sees atomic ammo+wear and rejects reentry/unequip/mutations");
        Check(health.Current==hp-20,"shot applies full damage");
        Check(!weapons.TryAttack()&&pistol.LoadedRounds==rounds-1&&pistol.Durability==.5f,"cooldown refusal has no wear/ammo");
        yield return new WaitForSeconds(.15f);hp=health.Current;
        Check(weapons.TryAttack()&&pistol.Durability==0&&health.Current==hp-20,"last firearm shot deals full damage then remains broken");
        yield return new WaitForSeconds(.15f);rounds=pistol.LoadedRounds;
        Check(!weapons.TryAttack()&&pistol.Durability==0&&pistol.LoadedRounds==rounds,"broken firearm refuses without ammo consumption");
        storage.TrySetDurability(Index(pistol),10);
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f})
        {pistolType.Weapon.durabilityCostPerUse=invalid;Check(!weapons.TryAttack()&&pistol.Durability==10&&pistol.LoadedRounds==rounds,"invalid wear refuses before ammo: "+invalid);}
        pistolType.Weapon.durabilityCostPerUse=0;Check(weapons.TryAttack()&&pistol.Durability==10,"explicit zero wear is permitted");
        yield return new WaitForSeconds(.15f);storage.TrySetDurability(Index(pistol),0);Check(!weapons.TryAttack(),"zero wear does not bypass broken predicate");
        var axe=new ItemStack(axeType,1,1);Check(storage.TryAddStack(axe)&&Equip(axe),"real player equips melee instance");yield return new WaitForSeconds(.23f);
        Check(weapons.TryAttack()&&axe.Durability==1,"melee windup does not charge wear early");weapons.Unequip();yield return new WaitForSeconds(.15f);
        Check(axe.Durability==1,"cancelled melee swing has no wear");Check(Equip(axe),"melee re-equip");yield return new WaitForSeconds(.23f);
        target.transform.position=camera.transform.position+camera.transform.forward*1.1f;Physics.SyncTransforms();hp=health.Current;
        Check(weapons.TryAttack(),"last melee strike started");yield return new WaitForSeconds(.09f);
        Check(axe.Durability==0&&health.Current==hp-20,"last melee completion wears to zero and still deals full damage");
        yield return new WaitForSeconds(.08f);Check(!weapons.TryAttack(),"broken melee cannot start swing");
        storage.TrySetDurability(Index(axe),5);target.SetActive(false);Check(weapons.TryAttack(),"melee miss started");yield return new WaitForSeconds(.09f);
        Check(axe.Durability==4,"completed melee miss charges exactly once");yield return new WaitForSeconds(.1f);Check(axe.Durability==4,"no repeated wear after completion");
        weapons.Unequip();string id=axe.InstanceId;
        Check(storage.TransferTo(destination,Index(axe),1)==1&&destination.Contains(axe)&&axe.InstanceId==id&&axe.Durability==4,"transfer preserves original ID and worn condition");
    }
}
