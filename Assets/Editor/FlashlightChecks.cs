using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Equipment;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;
using ZombieSurvival.Weapons;

public static class FlashlightChecks
{
    public static bool Completed{get;private set;}
    public static string LastReport{get;private set;}
    public static IEnumerator RunPlay()
    {
        Completed=false;LastReport="Running";
        if(!Application.isPlaying||!Application.dataPath.Contains("_CodexRecovery")){LastReport="FAIL: isolated Play required";Completed=true;yield break;}
        var root=new GameObject("Flashlight isolated checks");root.SetActive(false);var results=new List<string>();var routine=Scenario(root,results);
        while(true){bool next;try{next=routine.MoveNext();}catch(Exception error){results.Add("FAIL "+error);break;}if(!next)break;yield return routine.Current;}
        (routine as IDisposable)?.Dispose();UnityEngine.Object.Destroy(root);LastReport=string.Join("\n",results);Completed=true;
    }
    private static void Set(UnityEngine.Object target,string field,bool value){var so=new SerializedObject(target);so.FindProperty(field).boolValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static IEnumerator Scenario(GameObject root,List<string> results)
    {
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);results.Add("PASS "+label);}
        var itemType=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Equipment/Flashlight/FlashlightItem.asset");
        Check(itemType!=null&&itemType.Flashlight!=null&&itemType.Weapon==null&&itemType.MaxStack==1&&itemType.Category==ItemCategory.Equipment&&itemType.Subcategory==ItemSubcategory.Flashlight,"authored flashlight is Equipment individual, not weapon");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab");
        var player=UnityEngine.Object.Instantiate(prefab,root.transform);player.transform.position=new Vector3(11000,0,11000);
        var light=player.GetComponent<PlayerFlashlight>();Check(light!=null,"real player prefab contains flashlight adapter");
        var motor=player.GetComponent<FirstPersonController>();Set(motor,"requireApplicationFocus",false);Set(motor,"requireCursorCapture",false);Set(motor,"captureCursorOnEnable",false);
        var scheduler=player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSlots>();if(scheduler!=null)scheduler.enabled=false;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=player.transform.position+Vector3.down*.5f;floor.transform.localScale=new Vector3(10,1,10);
        root.SetActive(true);yield return null;yield return null;yield return null;
        var storage=player.GetComponent<ItemStorage>();var hotbar=player.GetComponent<PlayerHotbar>();var weapons=player.GetComponent<PlayerWeapons>();var item=new ItemStack(itemType,1);string id=item.InstanceId;
        Check(storage.TryAddStack(item),"owned flashlight instance inserted");int index=Enumerable.Range(0,storage.SlotCount).First(i=>storage.GetSlot(i)==item);
        Check(hotbar.Assign(0,index)&&hotbar.Select(0),"flashlight assigned to real hotbar");yield return null;
        Check(light.HeldItem==item&&light.Beam!=null&&light.Beam.type==LightType.Spot&&!light.IsOn&&weapons.Equipped==null,"held model creates real spot, starts off, no weapon equipped");
        float condition=item.Durability;int rounds=item.LoadedRounds;
        Check(hotbar.TryUsePrimary()&&light.IsOn,"primary hotbar route turns real spotlight on");
        Check(item.InstanceId==id&&item.Quantity==1&&item.Durability==condition&&item.LoadedRounds==rounds,"toggle does not consume item, ammo or durability");
        Check(light.GetComponentsInChildren<Light>(true).Count(l=>l.enabled)==1,"only one enabled runtime light");
        yield return new WaitForSeconds(.3f);Check(hotbar.TryUsePrimary()&&!light.IsOn,"second primary toggles off");
        yield return new WaitForSeconds(.3f);Check(hotbar.TryUsePrimary()&&light.IsOn,"third primary toggles on");
        motor.SetGameplayInputLock(light,true);yield return null;Check(!light.IsOn&&!hotbar.TryUsePrimary(),"menu/input owner lock extinguishes and blocks use");
        motor.SetGameplayInputLock(light,false);yield return null;yield return null;Check(!light.IsOn,"unlock never relights automatically");
        yield return new WaitForSeconds(.3f);Check(hotbar.TryUsePrimary()&&light.IsOn,"fresh use after unlock lights again");
        hotbar.Select(1);yield return null;yield return null;Check(!light.IsOn&&light.HeldItem==null,"switch to empty slot removes light and held model");
        hotbar.Select(0);yield return null;Check(light.Beam!=null&&!light.IsOn,"selecting flashlight again starts off");
        light.enabled=false;yield return null;Check(light.Beam==null&&!light.IsOn,"disable destroys managed presentation");
        light.enabled=true;yield return null;yield return null;Check(light.Beam!=null&&light.GetComponentsInChildren<Light>(true).Count(l=>l.name=="FlashlightBeam")==1,"re-enable restores one beam without duplicates");
        var chest=new GameObject("Flashlight chest");chest.transform.SetParent(root.transform);var destination=chest.AddComponent<ItemStorage>();
        Check(storage.TransferTo(destination,index,1)==1,"transfer original flashlight to chest");yield return null;Check(light.HeldItem==null&&!light.IsOn&&destination.Contains(item)&&item.InstanceId==id,"ownership loss removes beam, preserves original ID");
    }
}
