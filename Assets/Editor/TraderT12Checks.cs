using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Services;
using ZombieSurvival.Inventory;
using ZombieSurvival.Economy;
using ZombieSurvival.Player;

public static class TraderT12Checks
{
    public static bool Completed{get;private set;}
    public static string LastReport{get;private set;}
    private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static bool Advance(Trader trader,double seconds)=>(bool)typeof(Trader).GetMethod("AdvanceRestock",Flags).Invoke(trader,new object[]{seconds});
    private static double Elapsed(Trader.Stock row)=>(double)typeof(Trader.Stock).GetField("restockElapsed",Flags).GetValue(row);
    private static void ResetClocks(Trader trader){foreach(var row in trader.Stocks)typeof(Trader.Stock).GetField("restockElapsed",Flags).SetValue(row,0d);}
    private static void Set(UnityEngine.Object obj,string field,object value){var so=new SerializedObject(obj);var p=so.FindProperty(field);if(value is bool b)p.boolValue=b;else if(value is int i)p.intValue=i;else if(value is float f)p.floatValue=f;else if(value is string s)p.stringValue=s;else p.objectReferenceValue=value as UnityEngine.Object;so.ApplyModifiedPropertiesWithoutUndo();}
    public static IEnumerator RunPlay()
    {
        Completed=false;LastReport="Running";
        if(!Application.isPlaying||!Application.dataPath.Contains("_CodexRecovery")){LastReport="FAIL: isolated Play required";Completed=true;yield break;}
        var root=new GameObject("T12 isolated checks");root.SetActive(false);var assets=new List<UnityEngine.Object>();var results=new List<string>();var routine=Scenario(root,assets,results);
        while(true){bool next;try{next=routine.MoveNext();}catch(Exception error){results.Add("FAIL "+error);break;}if(!next)break;yield return routine.Current;}
        (routine as IDisposable)?.Dispose();UnityEngine.Object.Destroy(root);foreach(var asset in assets)UnityEngine.Object.Destroy(asset);LastReport=string.Join("\n",results);Completed=true;
    }
    private static IEnumerator Scenario(GameObject root,List<UnityEngine.Object> assets,List<string> results)
    {
        void Check(bool pass,string name){if(!pass)throw new InvalidOperationException(name);results.Add("PASS "+name);}
        var npc=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TraderT5Setup.PrefabPath),root.transform);npc.transform.position=new Vector3(13000,0,13002);var trader=npc.GetComponent<Trader>();
        foreach(var row in trader.Stocks){row.profile=UnityEngine.Object.Instantiate(row.profile);assets.Add(row.profile);}
        var guns=trader.Stocks.Single(r=>r.profile.Id=="weapons_ammo");var gear=trader.Stocks.Single(r=>r.profile.Id=="equipment");
        var pistolEntry=guns.profile.Catalog.First(e=>e.item.Weapon!=null&&e.item.Weapon.kind==ZombieSurvival.Weapons.WeaponKind.Firearm);pistolEntry.quantity=2;
        var ammoEntry=guns.profile.Catalog.First(e=>e.item==pistolEntry.item.Weapon.Ammo);ammoEntry.quantity=60;
        Set(trader,"activeProfile",guns.profile);Set(trader,"restockEnabled",true);Set(trader,"restockIntervalSeconds",600f);Set(trader,"saleMarkupPercent",0);Set(trader,"saleDiscountPercent",0);
        var player=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab"),root.transform);player.transform.SetPositionAndRotation(new Vector3(13000,0,13000),Quaternion.identity);
        var motor=player.GetComponent<FirstPersonController>();Set(motor,"requireApplicationFocus",false);Set(motor,"requireCursorCapture",false);Set(motor,"captureCursorOnEnable",false);
        var session=player.GetComponent<PlayerServiceSession>();Set(session,"requireApplicationFocus",false);Set(player.GetComponent<ZombieSurvival.Interaction.PlayerInteractor>(),"allowWhileAirborne",true);
        var save=player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSystem>();Set(save,"fileName","trader-t12-isolated.json");var scheduler=player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSlots>();if(scheduler!=null)scheduler.enabled=false;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=player.transform.position+Vector3.down*.5f;floor.transform.localScale=new Vector3(12,1,12);
        root.SetActive(true);yield return null;yield return null;yield return null;ResetClocks(trader);
        var bag=player.GetComponent<ItemStorage>();var money=player.GetComponent<Wallet>();var budget=npc.GetComponent<TraderBudget>();Check(money.TryCredit(100000)&&budget.Funds.TryCredit(111),"fixture wallets explicitly funded");
        Check(session.TryOpen(npc.GetComponent<TraderServicePoint>()),"real player opens existing trader session");
        int Index(ItemStorage store,ItemStack item)=>Enumerable.Range(0,store.SlotCount).First(i=>store.GetSlot(i)==item);
        List<ItemStack> Items(ItemStorage store,ItemDefinition type)=>Enumerable.Range(0,store.SlotCount).Select(store.GetSlot).Where(i=>i!=null&&!i.IsEmpty&&i.Definition==type).ToList();
        var original=Items(guns.storage,pistolEntry.item)[0];guns.storage.TrySetDurability(Index(guns.storage,original),47);guns.storage.Reload(original);string originalId=original.InstanceId;int rounds=original.LoadedRounds;
        var removed=Items(guns.storage,pistolEntry.item)[1];Check(guns.storage.TryExtract(Index(guns.storage,removed),1,out _),"one firearm removed to create deficit");
        var other=Enumerable.Range(0,gear.storage.SlotCount).Select(gear.storage.GetSlot).First(i=>i!=null&&!i.IsEmpty&&i.Definition.Category==ItemCategory.Equipment);var gearType=other.Definition;int gearBefore=gear.storage.Count(gearType);gear.storage.TryExtract(Index(gear.storage,other),1,out _);
        Check(Advance(trader,599)&&guns.storage.Count(pistolEntry.item)==1&&Elapsed(guns)==599&&Elapsed(gear)==599,"all initialized profiles tick without early restock");
        string initialSettings=trader.GetRestockRemainingSeconds(guns.profile).ToString();Check(initialSettings=="1","remaining countdown reads without mutation");
        int changed=0;bool final=true,reentry=true;long reserve=budget.Funds.Balance,cash=money.Balance;
        Action observer=()=>{changed++;final &= guns.storage.Count(pistolEntry.item)==2&&guns.storage.Count(ammoEntry.item)==60&&gear.storage.Count(gearType)==gearBefore;
            reentry &= Trader.RestockIsBusy&&!Advance(trader,600)&&!trader.TrySelectProfile(gear.profile)&&!guns.storage.TryAddAll(ammoEntry.item,1)
                &&!TradeService.TryQuote(session,trader,TradeDirection.Buy,original,1,out _,out _)&&!save.CanSaveNow(out _)&&!save.TryLoad();};
        guns.storage.Changed+=observer;gear.storage.Changed+=observer;bool replenished=Advance(trader,1);guns.storage.Changed-=observer;gear.storage.Changed-=observer;
        Check(replenished&&changed==2&&final&&reentry,"one atomic top-up per changed profile; all profiles committed before Changed; trade/save/load/reentry blocked");
        var fresh=Items(guns.storage,pistolEntry.item).Single(i=>i!=original);
        Check(guns.storage.Contains(original)&&original.InstanceId==originalId&&original.Durability==47&&original.LoadedRounds==rounds&&fresh.InstanceId!=originalId&&fresh.Durability==fresh.Definition.MaximumDurability&&fresh.LoadedRounds==0,"sold/old instance unchanged; fresh weapon has new ID full condition empty magazine");
        Check(budget.Funds.Balance==reserve&&money.Balance==cash,"restock never refills either wallet");
        Check(trader.TrySelectProfile(gear.profile)&&trader.TrySelectProfile(guns.profile)&&guns.storage.Contains(original)&&original.InstanceId==originalId,"profile switch keeps stock and IDs");
        ResetClocks(trader);Check(Advance(trader,25),"clock advanced25");double savedClock=Elapsed(guns);trader.enabled=false;Check(!Advance(trader,100)&&Elapsed(guns)==savedClock,"disabled NPC clock stands");trader.enabled=true;Check(Elapsed(guns)==savedClock,"enable does not reset elapsed");
        Set(trader,"restockEnabled",false);Check(!Advance(trader,100)&&Elapsed(guns)==savedClock,"disabled restock preserves progress");Set(trader,"restockEnabled",true);
        float timeScale=Time.timeScale;try{Time.timeScale=0;Check(!Advance(trader,100)&&Elapsed(guns)==savedClock,"pause does not advance clocks");}finally{Time.timeScale=timeScale;}
        foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,-1d,0d})Check(!Advance(trader,invalid)&&Elapsed(guns)==savedClock,"invalid/zero simulated delta refused: "+invalid);
        foreach(var interval in new[]{float.NaN,float.PositiveInfinity,-1f,0f}){Set(trader,"restockIntervalSeconds",interval);Check(!Advance(trader,100)&&Elapsed(guns)==savedClock,"invalid interval refuses without changing timer: "+interval);}Set(trader,"restockIntervalSeconds",600f);
        var working=typeof(ZombieSurvival.Persistence.PlayerSaveSystem).GetField("working",Flags);working.SetValue(save,true);
        try{Check(!Advance(trader,100)&&Elapsed(guns)==savedClock,"save/load callback working guard prevents restock clock mutation");}finally{working.SetValue(save,false);}
        var sourcePrice=TradeService.TryPreviewPrice(guns.profile,TradeDirection.Buy,original,1,out var baseBuy,out _,out _);Check(sourcePrice,"legacy profile-only preview remains supported");
        Set(trader,"saleMarkupPercent",25);Set(trader,"saleDiscountPercent",20);
        Check(TradeService.TryPreviewPrice(trader,TradeDirection.Buy,original,1,out var adjusted,out _,out _)&&adjusted==baseBuy,"25percent markup then20percent discount yields original sell coefficient");
        Set(trader,"saleDiscountPercent",0);Check(TradeService.TryPreviewPrice(trader,TradeDirection.Buy,original,1,out adjusted,out _,out _)&&adjusted>=baseBuy,"markup raises buy price with existing rounding");
        Check(TradeService.TryPreviewPrice(trader,TradeDirection.Sell,original,1,out var traderSell,out _,out _)&&TradeService.TryPreviewPrice(guns.profile,TradeDirection.Sell,original,1,out var profileSell,out _,out _)&&traderSell==profileSell,"NPC buyback percent unchanged by markup");
        Check(TradeService.TryQuote(session,trader,TradeDirection.Buy,original,1,out var stale,out _),"real quote with NPC price settings");Set(trader,"saleMarkupPercent",26);
        Check(!TradeService.TryExecute(stale,out _)&&money.Balance==cash&&guns.storage.Contains(original),"same-frame settings edit rejects old quote before Update");
        Set(trader,"saleMarkupPercent",0);Set(trader,"saleDiscountPercent",100);Check(!TradeService.TryPreviewPrice(trader,TradeDirection.Buy,original,1,out _,out _,out _)&&!Advance(trader,600),"discount under buyback and invalid config reject trading/restock");Set(trader,"saleDiscountPercent",0);
        Check(TradeService.TryQuote(session,trader,TradeDirection.Buy,original,1,out stale,out _),"quote before timed restock");ResetClocks(trader);Check(Advance(trader,600)&&!TradeService.TryExecute(stale,out _),"timed cycle invalidates stale quote even at full stock");
        // Fill every slot with unrelated, player-sold gear. Only a merge gap can accept ammo; weapons must wait.
        var extra=Items(guns.storage,pistolEntry.item).Single(i=>i!=original);guns.storage.TryExtract(Index(guns.storage,extra),1,out _);guns.storage.SpendRound(original);guns.storage.Reload(original);
        int targetAmmo=ammoEntry.quantity;int currentAmmo=guns.storage.Count(ammoEntry.item);if(currentAmmo>=targetAmmo){var ammo=Items(guns.storage,ammoEntry.item)[0];guns.storage.TryExtract(Index(guns.storage,ammo),1,out _);}
        while(guns.storage.FreeCapacity(gearType)>0)Check(guns.storage.TryAddAll(gearType,1),"capacity fixture fills free slot with foreign item");
        int foreign=guns.storage.Count(gearType);ResetClocks(trader);Check(Advance(trader,600)&&guns.storage.Count(pistolEntry.item)==1&&guns.storage.Count(ammoEntry.item)==targetAmmo&&guns.storage.Count(gearType)==foreign,"full capacity only merges fitting resource deficit; foreign items never removed");
        var blocker=Items(guns.storage,gearType)[0];guns.storage.TryExtract(Index(guns.storage,blocker),1,out _);ResetClocks(trader);Check(Advance(trader,599)&&guns.storage.Count(pistolEntry.item)==1,"capacity freed does not trigger immediate missed-cycle issuance");
        Check(Advance(trader,6001)&&guns.storage.Count(pistolEntry.item)==2&&original.InstanceId==originalId&&original.Durability==47,"huge missed time grants only one top-up, preserves old original");
    }
}
