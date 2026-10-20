using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Services;
using ZombieSurvival.Economy;
using ZombieSurvival.Player;

public static class RepairT9Checks
{
    public static bool Completed{get;private set;}
    public static string LastReport{get;private set;}
    private static void Set(UnityEngine.Object obj,string field,object value){var so=new SerializedObject(obj);var p=so.FindProperty(field);if(value is bool b)p.boolValue=b;else if(value is float f)p.floatValue=f;else if(value is int n)p.intValue=n;else if(value is string s)p.stringValue=s;else p.objectReferenceValue=value as UnityEngine.Object;so.ApplyModifiedPropertiesWithoutUndo();}
    public static IEnumerator RunPlay()
    {
        Completed=false;LastReport="Running";
        if(!Application.isPlaying||!Application.dataPath.Contains("_CodexRecovery")){LastReport="FAIL: isolated Play required";Completed=true;yield break;}
        var root=new GameObject("T9 isolated checks");root.SetActive(false);var assets=new List<UnityEngine.Object>();var results=new List<string>();var routine=Scenario(root,assets,results);
        while(true){bool next;try{next=routine.MoveNext();}catch(Exception e){results.Add("FAIL "+e);break;}if(!next)break;yield return routine.Current;}
        (routine as IDisposable)?.Dispose();UnityEngine.Object.Destroy(root);foreach(var asset in assets)UnityEngine.Object.Destroy(asset);LastReport=string.Join("\n",results);Completed=true;
    }
    private static IEnumerator Scenario(GameObject root,List<UnityEngine.Object> assets,List<string> results)
    {
        void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);results.Add("PASS "+name);}
        var policy=UnityEngine.Object.Instantiate(Resources.Load<RepairPolicy>("RepairPolicies/Default"));assets.Add(policy);
        policy.recipes=policy.recipes.Select(r=>{var c=UnityEngine.Object.Instantiate(r);assets.Add(c);return c;}).ToArray();
        var recipe=policy.recipes.Single(r=>r.profileId=="weapon");var material=recipe.materials.Single().material;
        var source=Resources.Load<TraderProfile>("Trading/weapons_ammo").Catalog.First(e=>e.item.Weapon!=null&&e.item.Weapon.kind==ZombieSurvival.Weapons.WeaponKind.Firearm).item;
        var type=UnityEngine.Object.Instantiate(source);assets.Add(type);Set(type,"maximumDurability",100f);Set(type,"repairable",true);Set(type,"repairProfileId","weapon");
        foreach(float condition in new[]{0f,1f,25f,50f,99f,100f})
        {
            var previewItem=new ItemStack(type,1,condition);bool valid=RepairService.TryPreviewCost(policy,previewItem,out var costs,out _);int expected=(int)decimal.Ceiling(4m*(100m-(decimal)condition)/100m);
            Check(condition==100?!valid:valid&&costs.Count==1&&costs[0].Amount==expected,"ceil cost at durability "+condition);
        }
        Check(RepairService.TryPreviewCost(policy,new ItemStack(type,1,99.99999f),out var tinyCosts,out _)&&tinyCosts[0].Amount==1,"tiny positive damage still costs one material");
        foreach(var profileId in new[]{"textile","armor"})
        {
            var gear=UnityEngine.Object.Instantiate(type);assets.Add(gear);Set(gear,"weapon",null);Set(gear,"category",(int)ItemCategory.Equipment);Set(gear,"repairProfileId",profileId);
            var expectedRecipe=policy.recipes.Single(r=>r.profileId==profileId);
            Check(RepairService.TryPreviewCost(policy,new ItemStack(gear,1,50),out var gearCosts,out _)&&gearCosts.Count==expectedRecipe.materials.Length
                &&gearCosts.Select((cost,i)=>cost.Amount==(int)decimal.Ceiling(expectedRecipe.materials[i].fullRepairAmount*.5m)).All(x=>x),"generic "+profileId+" material-only recipe preview");
        }
        var player=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab"),root.transform);player.transform.SetPositionAndRotation(new Vector3(12000,0,12000),Quaternion.identity);
        var motor=player.GetComponent<FirstPersonController>();Set(motor,"requireApplicationFocus",false);Set(motor,"requireCursorCapture",false);Set(motor,"captureCursorOnEnable",false);
        var session=player.GetComponent<PlayerServiceSession>();Set(session,"requireApplicationFocus",false);Set(player.GetComponent<ZombieSurvival.Interaction.PlayerInteractor>(),"allowWhileAirborne",true);
        var save=player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSystem>();Set(save,"fileName","repair-t9-isolated.json");var slots=player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSlots>();if(slots!=null)slots.enabled=false;
        var bench=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/T4WorkbenchSession.prefab"),root.transform);bench.transform.position=player.transform.position+Vector3.forward*2;
        var workbench=bench.GetComponent<RepairWorkbench>();Check(workbench!=null,"existing authored workbench contains RepairWorkbench");Set(workbench,"policyOverride",policy);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=player.transform.position+Vector3.down*.5f;floor.transform.localScale=new Vector3(10,1,10);
        root.SetActive(true);yield return null;yield return null;yield return null;
        Check(session.TryOpen(bench.GetComponent<WorkbenchServicePoint>()),"real player opens existing workbench session");
        var storage=player.GetComponent<ItemStorage>();var wallet=player.GetComponent<Wallet>();long cash=wallet.Balance;var item=new ItemStack(type,1,0);Check(storage.TryAddStack(item),"broken original weapon inserted");
        int Index(ItemStack stack)=>Enumerable.Range(0,storage.SlotCount).First(i=>storage.GetSlot(i)==stack);
        string Snapshot()=>string.Join(";",Enumerable.Range(0,storage.SlotCount).Select(i=>{var s=storage.GetSlot(i);return s==null?"-":s.InstanceId+":"+s.Quantity+":"+s.Durability+":"+s.LoadedRounds;}))+"|"+wallet.Balance;
        RepairQuote Quote(){if(!RepairService.TryQuote(session,workbench,item,out var q,out var reason))throw new InvalidOperationException(reason);return q;}
        void Reject(Func<bool> action,string name){var before=Snapshot();Check(!action()&&before==Snapshot(),name);}
        Reject(()=>RepairService.TryQuote(session,workbench,item,out _,out _),"missing materials refusal changes nothing");
        Check(storage.TryAddAll(type.Weapon.Ammo,12),"ammo added");storage.Reload(item);int rounds=item.LoadedRounds;string id=item.InstanceId;
        Check(material.MaxStack>=4&&storage.TryAddAll(material,material.MaxStack+2),"split-material fixture added");
        var firstMaterial=Enumerable.Range(0,storage.SlotCount).Select(storage.GetSlot).First(s=>s!=null&&s.Definition==material);
        Check(storage.TryExtract(Index(firstMaterial),material.MaxStack-2,out _),"material fixture split into two 2-unit slots");
        var first=Quote();int notices=0;bool final=true,reentry=true;
        Action observer=()=>{notices++;final &= item.Durability==100&&storage.Count(material)==0&&item.InstanceId==id&&item.LoadedRounds==rounds;
            reentry &= RepairService.IsBusy&&!RepairService.TryExecute(first,out _)&&!RepairService.TryQuote(session,workbench,item,out _,out _)&&!storage.TrySetDurability(Index(item),1)
                &&!storage.TryAddAll(material,1)&&storage.Reload(item)==0&&!save.CanSaveNow(out _)&&!save.TryLoad();};
        storage.Changed+=observer;bool repaired=RepairService.TryExecute(first,out var reason);storage.Changed-=observer;
        Check(repaired&&notices==1&&final&&reentry&&wallet.Balance==cash,"broken full repair atomically consumes split stacks before one Changed; callbacks/reentry/save/load blocked; Wallet unchanged");
        Check(storage.Contains(item)&&item.InstanceId==id&&item.LoadedRounds==rounds,"same original reference/ID/magazine retained");
        Reject(()=>RepairService.TryExecute(first,out _),"used quote cannot execute twice");Reject(()=>RepairService.TryQuote(session,workbench,item,out _,out _),"full condition refuses without charge");
        storage.TrySetDurability(Index(item),50);storage.TryAddAll(material,20);
        var stale=Quote();storage.TrySetDurability(Index(item),49);Reject(()=>RepairService.TryExecute(stale,out _),"durability change rejects stale quote");
        stale=Quote();storage.TryAddAll(material,1);Reject(()=>RepairService.TryExecute(stale,out _),"material quantity change rejects stale quote");
        stale=Quote();int materialTotal=storage.Count(material);var materialStack=Enumerable.Range(0,storage.SlotCount).Select(storage.GetSlot).First(s=>s!=null&&s.Definition==material);
        Check(storage.TryExtract(Index(materialStack),materialStack.Quantity,out var regrouped)&&storage.TryAddStack(regrouped)&&storage.Count(material)==materialTotal,"material layout changed without changing total");
        Reject(()=>RepairService.TryExecute(stale,out _),"same-total material slot redistribution rejects stale quote");
        stale=Quote();Set(type,"maximumDurability",101f);Reject(()=>RepairService.TryExecute(stale,out _),"type maximum change rejects stale quote");Set(type,"maximumDurability",100f);
        stale=Quote();recipe.materials[0].fullRepairAmount++;Reject(()=>RepairService.TryExecute(stale,out _),"recipe cost change rejects stale quote");recipe.materials[0].fullRepairAmount--;
        stale=Quote();session.Close();yield return null;yield return null;Check(session.TryOpen(bench.GetComponent<WorkbenchServicePoint>()),"session reopened");Reject(()=>RepairService.TryExecute(stale,out _),"session reopen rejects prior quote");
        Reject(()=>RepairService.TryQuote(session,workbench,item.Copy(1),out _,out _),"snapshot cannot be repaired");
        var chest=new GameObject("T9 chest");chest.transform.SetParent(root.transform);var other=chest.AddComponent<ItemStorage>();stale=Quote();Check(storage.TransferTo(other,Index(item),1)==1,"original transferred to foreign storage");Reject(()=>RepairService.TryExecute(stale,out _),"foreign ownership rejects old quote");
        Reject(()=>RepairService.TryQuote(session,workbench,item,out _,out _),"foreign original cannot be quoted");
        var freeRecipe=UnityEngine.Object.Instantiate(recipe);assets.Add(freeRecipe);freeRecipe.materials=Array.Empty<RepairRecipe.MaterialRow>();policy.recipes=new[]{freeRecipe};
        Check(!RepairService.TryPreviewCost(policy,new ItemStack(type,1,0),out _,out _),"empty/free recipe rejects damaged item");
        Check(wallet.Balance==cash,"all checks leave Wallet unchanged");
    }
}
