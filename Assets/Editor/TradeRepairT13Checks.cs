using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Economy;
using ZombieSurvival.Player;
using ZombieSurvival.Services;
using ZombieSurvival.Weapons;
using ZombieSurvival.Interaction;

/// <summary>Full existing-API chain on the separately authored T13Integration scene. No main scene or save file is touched.</summary>
public static class TradeRepairT13Checks
{
    public static bool Completed{get;private set;}
    public static string LastReport{get;private set;}
    private const string TestFile="trade-repair-t13-integration.json";
    public static IEnumerator RunPlay()
    {
        Completed=false;LastReport="Running";
        if(!Application.isPlaying||!Application.dataPath.Contains("_CodexRecovery")||SceneManager.GetActiveScene().name!="T13Integration")
        {Completed=true;LastReport="FAIL: isolated T13Integration Play required";yield break;}
        var results=new List<string>();var routine=Scenario(results);
        while(true){bool next;try{next=routine.MoveNext();}catch(Exception error){results.Add("FAIL "+error);break;}if(!next)break;yield return routine.Current;}
        (routine as IDisposable)?.Dispose();LastReport=string.Join("\n",results);Completed=true;
    }
    private static ItemStack WorldState(WorldItem item)=>(ItemStack)typeof(WorldItem).GetField("instance",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(item);
    private static IEnumerable<ItemStorage> Stores(Scene scene)=>UnityEngine.Object.FindObjectsByType<ItemStorage>(FindObjectsInactive.Include).Where(s=>s.gameObject.scene==scene);
    private static IEnumerable<WorldItem> Loot(Scene scene)=>UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include).Where(w=>w.gameObject.scene==scene&&w.Quantity>0);
    private static int Index(ItemStorage storage,ItemStack item)=>Enumerable.Range(0,storage.SlotCount).First(i=>storage.GetSlot(i)==item);
    private static ItemStack ById(ItemStorage storage,string id)=>Enumerable.Range(0,storage.SlotCount).Select(storage.GetSlot).FirstOrDefault(s=>s!=null&&!s.IsEmpty&&s.InstanceId==id);
    private static int Owners(Scene scene,string id)=>Stores(scene).Sum(s=>Enumerable.Range(0,s.SlotCount).Count(i=>s.GetSlot(i)!=null&&!s.GetSlot(i).IsEmpty&&s.GetSlot(i).InstanceId==id))+Loot(scene).Count(w=>WorldState(w)?.InstanceId==id);
    private static string Snapshot(Scene scene,Wallet money,Wallet funds)
    {
        string Item(ItemStack item)=>item==null||item.IsEmpty?"-":item.InstanceId+":"+item.Definition.Id+":"+item.Quantity+":"+item.Durability.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+item.LoadedRounds;
        return string.Join("|",Stores(scene).OrderBy(s=>s.GetComponent<SaveIdentity>()?.Id,StringComparer.Ordinal).Select(s=>(s.GetComponent<SaveIdentity>()?.Id??s.name)+"="+string.Join(";",Enumerable.Range(0,s.SlotCount).Select(i=>Item(s.GetSlot(i))))))
            +"|world="+string.Join(";",Loot(scene).Select(w=>Item(WorldState(w))).OrderBy(x=>x,StringComparer.Ordinal))+"|cash="+money.Balance+"|budget="+funds.Balance;
    }
    private static IEnumerator Approach(FirstPersonController motor,Transform service)
    {
        motor.GetComponent<PlayerServiceSession>().Close();motor.GetComponent<PlayerInventory>()?.Close();
        var controller=motor.GetComponent<CharacterController>();if(controller!=null)controller.enabled=false;
        motor.transform.SetPositionAndRotation(new Vector3(service.position.x,0,service.position.z-2),Quaternion.identity);
        if(controller!=null)controller.enabled=true;Physics.SyncTransforms();for(int i=0;i<8;i++)yield return null;
    }
    private static IEnumerator Scenario(List<string> results)
    {
        void Check(bool condition,string label){if(!condition)throw new InvalidOperationException(label);results.Add("PASS "+label);}
        var scene=SceneManager.GetActiveScene();var motor=UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Exclude).Single(p=>p.gameObject.scene==scene);
        var trader=UnityEngine.Object.FindObjectsByType<Trader>(FindObjectsInactive.Exclude).Single(t=>t.gameObject.scene==scene);
        var bench=UnityEngine.Object.FindObjectsByType<RepairWorkbench>(FindObjectsInactive.Exclude).Single(b=>b.gameObject.scene==scene);
        var chest=Stores(scene).Single(s=>s.name=="T13 Transfer Container");var bag=motor.GetComponent<ItemStorage>();var inventory=motor.GetComponent<PlayerInventory>();var hotbar=motor.GetComponent<PlayerHotbar>();
        var session=motor.GetComponent<PlayerServiceSession>();var save=motor.GetComponent<PlayerSaveSystem>();var wallet=motor.GetComponent<Wallet>();var funds=trader.GetComponent<TraderBudget>().Funds;
        Check(Path.GetFileName(save.SavePath)==TestFile,"strictly disposable T13 save filename");
        Check(session!=null&&save!=null&&inventory!=null&&trader.GetComponent<WorldTraderSaveAdapter>()!=null,"real source player/trader/workbench integrations present");
        for(int i=0;i<12;i++)yield return null;
        var guns=trader.Stocks.Single(r=>r.profile.Id=="weapons_ammo");Check(trader.TrySelectProfile(guns.profile),"existing weapons profile selected");
        var type=guns.profile.Catalog.First(e=>e.item.Weapon!=null&&e.item.Weapon.kind==WeaponKind.Firearm).item;
        var recipe=bench.Policy.recipes.Single(r=>r.profileId=="weapon");Check(recipe.materials.Length==1,"authored default weapon recipe");var parts=recipe.materials[0].material;
        if(wallet.Balance<10000)Check(wallet.TryCredit(10000-wallet.Balance),"fixture player money credited");else if(wallet.Balance>10000)Check(wallet.TryDebit(wallet.Balance-10000),"fixture player money normalized explicitly");
        trader.GetComponent<TraderBudget>().SetInfinite(false);if(funds.Balance<50000)Check(funds.TryCredit(50000-funds.Balance),"fixture finite trader funds credited");else if(funds.Balance>50000)Check(funds.TryDebit(funds.Balance-50000),"fixture trader funds normalized explicitly");
        var item=new ItemStack(type,1,47);Check(bag.TryAddStack(item)&&bag.TryAddAll(type.Weapon.Ammo,type.Weapon.MagazineSize),"one explicit worn original and ammo added for fixture");
        Check(bag.Reload(item)==type.Weapon.MagazineSize,"standard reload loads original weapon");string id=item.InstanceId;int rounds=item.LoadedRounds;float wear=item.Durability;long startCash=wallet.Balance,startFunds=funds.Balance;
        void Reject(Func<bool> action,string label){string before=Snapshot(scene,wallet,funds);Check(!action()&&Snapshot(scene,wallet,funds)==before,label);}
        TradeQuote Quote(TradeDirection direction,ItemStack target,int amount=1){if(!TradeService.TryQuote(session,trader,direction,target,amount,out var q,out var reason))throw new InvalidOperationException(reason);return q;}
        try
        {
            var approach=Approach(motor,trader.transform);while(approach.MoveNext())yield return approach.Current;
            Check(session.TryOpen(trader.GetComponent<TraderServicePoint>()),"real player service opens trader");
            Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Sell,item.Copy(1),1,out _,out _),"snapshot cannot be sold");
            Check(funds.TryDebit(funds.Balance),"finite budget temporarily emptied for refusal");Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Sell,item,1,out _,out _),"insufficient NPC budget is non-mutating");Check(funds.TryCredit(startFunds),"finite budget restored explicitly");
            var sale=Quote(TradeDirection.Sell,item);Check(TradeService.TryExecute(sale,out var why),"sell worn loaded original: "+why);
            long expectedCash=startCash+sale.TotalPrice,expectedFunds=startFunds-sale.TotalPrice;
            Check(wallet.Balance==expectedCash&&funds.Balance==expectedFunds&&guns.storage.Contains(item)&&!bag.Contains(item)&&item.InstanceId==id&&item.Durability==wear&&item.LoadedRounds==rounds&&Owners(scene,id)==1,"sale preserves exact original ID/condition/magazine and exact two balances");
            Reject(()=>TradeService.TryExecute(sale,out _),"sold quote cannot be replayed");
            Check(wallet.TryDebit(wallet.Balance),"player funds temporarily emptied for refusal");Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Buy,item,1,out _,out _),"insufficient player cash is non-mutating");Check(wallet.TryCredit(expectedCash),"player cash restored explicitly");
            var purchase=Quote(TradeDirection.Buy,item);Check(purchase.TotalPrice>=sale.TotalPrice&&TradeService.TryExecute(purchase,out why),"buy same original back without profitable roundtrip: "+why);
            expectedCash-=purchase.TotalPrice;expectedFunds+=purchase.TotalPrice;
            Check(bag.Contains(item)&&!guns.storage.Contains(item)&&item.InstanceId==id&&item.Durability==wear&&item.LoadedRounds==rounds&&wallet.Balance==expectedCash&&funds.Balance==expectedFunds&&Owners(scene,id)==1,"buy-back keeps reference/state and exact balances");
            var beforeTransfer=Quote(TradeDirection.Sell,item);session.Close();yield return null;yield return null;
            Check(bag.TransferTo(chest,Index(bag,item),1)==1&&chest.Contains(item)&&Owners(scene,id)==1,"transfer original to registered container");
            Reject(()=>TradeService.TryExecute(beforeTransfer,out _),"old offer cannot spend container-owned item");
            Check(chest.TransferTo(bag,Index(chest,item),1)==1&&bag.Contains(item)&&item.InstanceId==id&&item.Durability==wear&&item.LoadedRounds==rounds,"transfer back preserves exact reference and state");
            Check(inventory.Drop(Index(bag,item),1),"PlayerInventory.Drop uses standard extracted-original WorldItem path");
            var dropped=Loot(scene).Single(w=>WorldState(w)?.InstanceId==id);
            Check(WorldState(dropped)==item&&!bag.Contains(item)&&dropped.Quantity==1&&Owners(scene,id)==1,"world pickup owns original once, no duplicate storage owner");
            Check(save.TrySave(),"actual save includes dropped WorldItem: "+save.Status);
            Check(dropped.Interact(motor.GetComponent<PlayerInteractor>())&&bag.Contains(item)&&item.LoadedRounds==rounds&&item.Durability==wear,"standard WorldItem.Interact picks original back up");yield return null;
            var oldPicked=item;
            Check(save.TryLoad(),"actual load restores dropped-item checkpoint: "+save.Status);
            Check(oldPicked.IsEmpty&&!bag.Contains(oldPicked)&&Owners(scene,id)==1,"load retires old live reference and restores only one owner");
            var restoredDrop=Loot(scene).Single(w=>WorldState(w)?.InstanceId==id);item=WorldState(restoredDrop);
            Check(item!=oldPicked&&item.Durability==wear&&item.LoadedRounds==rounds&&restoredDrop.Interact(motor.GetComponent<PlayerInteractor>()),"restored world item picks up with same ID/magazine/condition");yield return null;yield return null;
            Check(bag.Contains(item)&&Owners(scene,id)==1,"pickup after load has one canonical storage owner");
            var toBench=Approach(motor,bench.transform);while(toBench.MoveNext())yield return toBench.Current;
            Check(session.TryOpen(bench.GetComponent<WorkbenchServicePoint>()),"real player opens existing workbench");
            Reject(()=>RepairService.TryQuote(session,bench,item,out _,out _),"missing repair materials do not change original or money");
            Check(RepairService.TryPreviewCost(bench.Policy,item,out var costs,out _)&&costs.Count==1,"repair cost uses authored recipe");int required=costs[0].Amount;
            Check(bag.TryAddAll(parts,required+2),"repair fixture materials explicitly added");
            Check(RepairService.TryQuote(session,bench,item,out var repair,out why),"real repair quote created: "+why);
            int materialsBefore=bag.Count(parts),notifications=0;string fileBefore=File.ReadAllText(save.SavePath);bool callbacksSafe=true;
            Action observer=()=>{notifications++;callbacksSafe &= RepairService.IsBusy&&item.Durability==type.MaximumDurability&&bag.Count(parts)==materialsBefore-required
                &&!RepairService.TryExecute(repair,out _)&&!save.TrySave()&&!save.TryLoad();};
            bag.Changed+=observer;bool repaired;
            try{repaired=RepairService.TryExecute(repair,out why);}finally{bag.Changed-=observer;}
            Check(repaired&&notifications==1&&callbacksSafe&&File.ReadAllText(save.SavePath)==fileBefore,"repair commit has one final-state event; nested execution/save/load rejected without changing checkpoint");
            Check(bag.Contains(item)&&item.InstanceId==id&&item.LoadedRounds==rounds&&item.Durability==type.MaximumDurability&&bag.Count(parts)==2&&wallet.Balance==expectedCash&&funds.Balance==expectedFunds,"repair preserves canonical ID/magazine and exact money; only required materials consumed");
            Reject(()=>RepairService.TryExecute(repair,out _),"used repair quote cannot consume materials again");Reject(()=>RepairService.TryQuote(session,bench,item,out _,out _),"full condition cannot be charged");
            Check(save.TrySave(),"actual save after trade/drop/transfer/repair: "+save.Status);string repairedState=Snapshot(scene,wallet,funds);
            Check(bag.TrySetDurability(Index(bag,item),75),"condition changed after saved checkpoint");
            Check(RepairService.TryQuote(session,bench,item,out var staleRepair,out why),"repair quote created against state newer than saved checkpoint");
            var beforeRepairLoad=item;Check(save.TryLoad(),"load while repair service is open: "+save.Status);
            Check(!session.IsOpen,"load closes repair session and invalidates its owner lock");
            Reject(()=>RepairService.TryExecute(staleRepair,out _),"pre-load repair quote cannot act on restored state");
            item=ById(bag,id);Check(beforeRepairLoad.IsEmpty&&item!=beforeRepairLoad&&item!=null&&Snapshot(scene,wallet,funds)==repairedState&&Owners(scene,id)==1,"load restores exact repaired snapshot, retires prior reference, no duplicate");
            yield return null;yield return null;Check(motor.GameplayInputEnabled,"repair load releases menu lock after normal frames");
            var toTrader=Approach(motor,trader.transform);while(toTrader.MoveNext())yield return toTrader.Current;
            Check(session.TryOpen(trader.GetComponent<TraderServicePoint>()),"trader reopened for trade-load boundary");
            var stockAmmo=Enumerable.Range(0,guns.storage.SlotCount).Select(guns.storage.GetSlot).First(s=>s!=null&&!s.IsEmpty&&s.Definition==type.Weapon.Ammo);
            Check(save.TrySave(),"repaired snapshot saved with trade session open: "+save.Status);string finalState=Snapshot(scene,wallet,funds);
            var staleTrade=Quote(TradeDirection.Buy,stockAmmo);Check(save.TryLoad(),"load while trader service is open: "+save.Status);
            Check(!session.IsOpen,"load closes trade session and invalidates its owner lock");
            Check(stockAmmo.IsEmpty,"load retires old stock ItemStack references instead of duplicating them");
            Reject(()=>TradeService.TryExecute(staleTrade,out _),"pre-load trade quote cannot act on restored stock");
            Check(Snapshot(scene,wallet,funds)==finalState&&Owners(scene,id)==1&&ById(bag,id).LoadedRounds==rounds&&ById(bag,id).Durability==type.MaximumDurability,"trade-open load preserves exact goods/money/materials/magazine");
            Check(save.TryLoad()&&Snapshot(scene,wallet,funds)==finalState&&Owners(scene,id)==1&&bag.Count(parts)==2,"second immediate load creates no duplicates, repeated payment or material consumption");
            yield return null;yield return null;Check(motor.GameplayInputEnabled&&!session.IsOpen,"repeat load leaves gameplay unlocked and service closed");
            Check(wallet.Balance==expectedCash&&funds.Balance==expectedFunds,"full chain exact final player/NPC rubles");
        }
        finally
        {
            session.Close();inventory.Close();foreach(string suffix in new[]{"",".bak",".tmp"})if(File.Exists(save.SavePath+suffix))File.Delete(save.SavePath+suffix);
        }
    }
}
