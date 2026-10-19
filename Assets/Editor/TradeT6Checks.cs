using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Services;
using ZombieSurvival.Inventory;
using ZombieSurvival.Economy;
using ZombieSurvival.Player;
using ZombieSurvival.Weapons;

/// <summary>Coroutine checks: real player prefab and normal frames, only in the isolated test checkout.</summary>
public static class TradeT6Checks
{
    public static string LastReport { get; private set; }
    public static bool Completed { get; private set; }
    private static void Set(UnityEngine.Object obj, string field, object value)
    {
        var data = new SerializedObject(obj); var p = data.FindProperty(field);
        if (value is bool flag) p.boolValue = flag; else if (value is long number) p.longValue = number; else p.objectReferenceValue = value as UnityEngine.Object;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static string Snapshot(ItemStorage a, ItemStorage b, Wallet x, Wallet y)
    {
        string Store(ItemStorage s) => string.Join(";", Enumerable.Range(0,s.SlotCount).Select(i=> { var v=s.GetSlot(i);return v==null||v.IsEmpty?"-":v.InstanceId+":"+v.Quantity+":"+v.Durability+":"+v.LoadedRounds; }));
        return Store(a)+"|"+Store(b)+"|"+x.Balance+"|"+y.Balance+"|"+x.IsInfinite+"|"+y.IsInfinite;
    }
    public static IEnumerator RunPlay()
    {
        Completed = false; LastReport = "Running";
        if (!Application.isPlaying || !Application.dataPath.Contains("_CodexRecovery")) { LastReport="FAIL: isolated Play required"; Completed=true; yield break; }
        var root = new GameObject("T6 isolated checks"); root.SetActive(false);
        var profiles = new List<TraderProfile>(); var checks = new List<string>();
        var routine = Scenario(root, profiles, checks);
        while (true)
        {
            bool next;
            try { next=routine.MoveNext(); }
            catch (Exception error) { checks.Add("FAIL "+error); break; }
            if (!next) break;
            yield return routine.Current;
        }
        (routine as IDisposable)?.Dispose(); UnityEngine.Object.Destroy(root);
        foreach (var p in profiles) UnityEngine.Object.Destroy(p);
        LastReport=string.Join("\n",checks); Completed=true;
    }
    private static IEnumerator Scenario(GameObject root, List<TraderProfile> profiles, List<string> checks)
    {
        void Check(bool pass,string name) { if(!pass)throw new InvalidOperationException(name);checks.Add("PASS "+name); }
        var player=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab"),root.transform);
        player.transform.position=new Vector3(10000,0,10000); player.transform.rotation=Quaternion.identity;
        var session=player.GetComponent<PlayerServiceSession>(); Check(session!=null,"реальный prefab игрока содержит session");
        var motor=player.GetComponent<FirstPersonController>(); Set(motor,"requireApplicationFocus",false);Set(motor,"requireCursorCapture",false);Set(motor,"captureCursorOnEnable",false);
        Set(session,"requireApplicationFocus",false); Set(player.GetComponent<ZombieSurvival.Interaction.PlayerInteractor>(),"allowWhileAirborne",true);
        // The source's long-running auto-save scheduler is irrelevant to a short isolated transaction test.
        var autosave=player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSlots>(); if(autosave!=null)autosave.enabled=false;
        var npc=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TraderT5Setup.PrefabPath),root.transform);npc.transform.position=player.transform.position+Vector3.forward*2;
        var trader=npc.GetComponent<Trader>();
        foreach(var row in trader.Stocks){row.profile=UnityEngine.Object.Instantiate(row.profile);profiles.Add(row.profile);}
        var guns=trader.Stocks.Single(s=>s.profile.Id=="weapons_ammo");var gear=trader.Stocks.Single(s=>s.profile.Id=="equipment");
        Set(trader,"activeProfile",guns.profile);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=player.transform.position+Vector3.down*.5f;floor.transform.localScale=new Vector3(10,1,10);
        root.SetActive(true); Physics.SyncTransforms(); yield return null; yield return null; yield return null;
        var bag=player.GetComponent<ItemStorage>();var money=player.GetComponent<Wallet>();var budget=npc.GetComponent<TraderBudget>();var funds=budget.Funds;
        Check(money.TryCredit(100000),"тестовый бюджет игрока явно начислен");
        Check(session.TryOpen(npc.GetComponent<TraderServicePoint>()),"сессия реального игрока открылась по обычному доступу");
        var ammo=guns.profile.Catalog.Single(e=>e.item.Category==ItemCategory.Ammunition).item;
        ItemStack StockAmmo()=>Enumerable.Range(0,guns.storage.SlotCount).Select(guns.storage.GetSlot).First(s=>s!=null&&!s.IsEmpty&&s.Definition==ammo);
        ItemStack BagAmmo()=>Enumerable.Range(0,bag.SlotCount).Select(bag.GetSlot).First(s=>s!=null&&!s.IsEmpty&&s.Definition==ammo);
        TradeQuote Quote(TradeDirection direction,ItemStack item,int count){if(!TradeService.TryQuote(session,trader,direction,item,count,out var q,out var why))throw new InvalidOperationException(why);return q;}
        void Reject(Func<bool> action,string name){var before=Snapshot(guns.storage,bag,money,funds);Check(!action()&&before==Snapshot(guns.storage,bag,money,funds),name);}
        var first=Quote(TradeDirection.Buy,StockAmmo(),5);int stockBefore=guns.storage.Count(ammo),bagBefore=bag.Count(ammo);long cashBefore=money.Balance,reserveBefore=funds.Balance;int observed=0;
        bool finalState=true,reentry=true;
        Action observer=()=>{
            observed++;finalState &= guns.storage.Count(ammo)==stockBefore-5 && bag.Count(ammo)==bagBefore+5 && money.Balance==cashBefore-first.TotalPrice && funds.Balance==reserveBefore+first.TotalPrice;
            reentry &= !TradeService.TryExecute(first,out _)&&!money.TryCredit(1)&&!bag.TryAddAll(ammo,1)&&!guns.storage.TrySetDurability(0,1)&&guns.storage.Reload(guns.storage.GetSlot(0))==0;
            reentry &= !player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSystem>().CanSaveNow(out _);
        };
        guns.storage.Changed+=observer;bag.Changed+=observer;money.Changed+=observer;funds.Changed+=observer;
        bool bought=TradeService.TryExecute(first,out var reason);
        guns.storage.Changed-=observer;bag.Changed-=observer;money.Changed-=observer;funds.Changed-=observer;
        Check(bought&&observed==4&&finalState&&reentry,"покупка атомарна до всех4 Changed; reentry/мутации/save в observer запрещены");
        Reject(()=>TradeService.TryExecute(first,out _),"повторное исполнение того же quote не меняет состояние");
        string mergeId=BagAmmo().InstanceId;var next=Quote(TradeDirection.Buy,StockAmmo(),3);Check(TradeService.TryExecute(next,out _)&&BagAmmo().InstanceId==mergeId,"partial resource buy merge сохраняет ID принимающей стопки");
        var sell=Quote(TradeDirection.Sell,BagAmmo(),2);Check(sell.UnitPrice<=first.UnitPrice&&TradeService.TryExecute(sell,out _),"продажа части ресурса без прибыльного roundtrip");
        Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Buy,StockAmmo(),0,out _,out _),"нулевое количество отказ");
        Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Buy,StockAmmo(),int.MaxValue,out _,out _),"нехватка товара отказ без partial");
        var stale=Quote(TradeDirection.Buy,StockAmmo(),1);trader.TrySelectProfile(gear.profile);Reject(()=>TradeService.TryExecute(stale,out _),"смена профиля инвалидирует quote");trader.TrySelectProfile(guns.profile);
        var pistol=guns.storage.GetSlot(0);guns.storage.TrySetDurability(0,47);guns.storage.Reload(pistol);string pistolId=pistol.InstanceId;int rounds=pistol.LoadedRounds;
        var pistolBuy=Quote(TradeDirection.Buy,pistol,1);Check(TradeService.TryExecute(pistolBuy,out _)&&bag.Contains(pistol)&&pistol.InstanceId==pistolId&&pistol.Durability==47&&pistol.LoadedRounds==rounds,"заряженное оружие47 с магазином и ID переносится оригиналом");
        int weaponIndex=Enumerable.Range(0,bag.SlotCount).First(i=>bag.GetSlot(i)==pistol);Check(player.GetComponent<PlayerWeapons>().Equip(weaponIndex),"реальное оружие экипировано");
        Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Sell,pistol,1,out _,out _),"продажа экипированного оружия запрещена");player.GetComponent<PlayerWeapons>().Unequip();
        var pistolSell=Quote(TradeDirection.Sell,pistol,1);Check(pistolSell.UnitPrice<=pistolBuy.UnitPrice&&TradeService.TryExecute(pistolSell,out _)&&guns.storage.Contains(pistol)&&pistol.LoadedRounds==rounds,"обратная продажа сохраняет магазин и не приносит прибыль");
        var insufficient=money.Balance;if(insufficient>0)money.TryDebit(insufficient);Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Buy,StockAmmo(),1,out _,out _),"недостаток денег игрока без изменений");money.TryCredit(100000);
        if(funds.Balance>0)funds.TryDebit(funds.Balance);Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Sell,BagAmmo(),1,out _,out _),"недостаток конечного бюджета NPC");funds.TryCredit(500000);
        money.TryCredit(long.MaxValue-money.Balance);Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Sell,BagAmmo(),1,out _,out _),"overflow кошелька получателя");money.TryDebit(money.Balance-100000);
        var blocked=Quote(TradeDirection.Buy,StockAmmo(),1);player.transform.position+=Vector3.right*10;Physics.SyncTransforms();Reject(()=>TradeService.TryExecute(blocked,out _),"отход от сервиса отменяет сделку");player.transform.position-=Vector3.right*10;Physics.SyncTransforms();
        var reopen=Quote(TradeDirection.Buy,StockAmmo(),1);session.Close();yield return null;yield return null;
        Check(session.TryOpen(npc.GetComponent<TraderServicePoint>()),"повторное открытие через реальные кадры");Reject(()=>TradeService.TryExecute(reopen,out _),"новая сессия не оживляет старую котировку");
        long reserve=funds.Balance;budget.SetInfinite(true);var infinite=Quote(TradeDirection.Sell,BagAmmo(),1);Check(TradeService.TryExecute(infinite,out _)&&funds.Balance==reserve,"infinite платит без изменения конечного резерва");budget.SetInfinite(false);
        var changed=Quote(TradeDirection.Buy,pistol,1);guns.storage.TrySetDurability(0,10);Reject(()=>TradeService.TryExecute(changed,out _),"изменение состояния/каталога инвалидирует quote");
        var food=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/CharacterTesting/Items/CannedFood.asset");bag.TryAddAll(food,1);
        var foodStack=Enumerable.Range(0,bag.SlotCount).Select(bag.GetSlot).First(s=>s!=null&&!s.IsEmpty&&s.Definition==food);
        Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Sell,foodStack,1,out _,out _),"неподдерживаемая категория выкупа");
        var categoriesData=new SerializedObject(guns.profile);var categories=categoriesData.FindProperty("acceptedCategories");int categoryIndex=categories.arraySize++;categories.GetArrayElementAtIndex(categoryIndex).intValue=(int)ItemCategory.Food;categoriesData.ApplyModifiedPropertiesWithoutUndo();
        Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Sell,foodStack,1,out _,out _),"разрешённая категория без цены типа отказывает");
        bag.TryAddAll(ammo,bag.FreeCapacity(ammo));Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Buy,StockAmmo(),1,out _,out _),"полный рюкзак не получает partial покупку");
        for(int i=0;i<bag.SlotCount;i++){var stack=bag.GetSlot(i);if(stack!=null&&!stack.IsEmpty)bag.TryExtract(i,stack.Quantity,out _);}
        bag.TryAddAll(ammo,2);guns.storage.TryAddAll(ammo,guns.storage.FreeCapacity(ammo));
        Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Sell,BagAmmo(),1,out _,out _),"полный склад не получает partial продажу");
        var priceEntry=guns.profile.Catalog.Single(e=>e.item==ammo);long originalPrice=priceEntry.basePrice;priceEntry.basePrice=long.MaxValue;
        Reject(()=>TradeService.TryQuote(session,trader,TradeDirection.Buy,StockAmmo(),2,out _,out _),"long total price overflow отказывает");
        priceEntry.basePrice=0;long cash=money.Balance,reserveZero=funds.Balance;var free=Quote(TradeDirection.Buy,StockAmmo(),1);
        Check(free.TotalPrice==0&&TradeService.TryExecute(free,out _)&&money.Balance==cash&&funds.Balance==reserveZero,"явная нулевая цена не создаёт/списывает рубли");priceEntry.basePrice=originalPrice;
        var dead=Quote(TradeDirection.Buy,StockAmmo(),1);var health=player.GetComponent<ZombieSurvival.Combat.Health>();health.Revive(1,0);health.ApplyDamage(new ZombieSurvival.Combat.DamageInfo(health.Current+1000,null,player.transform.position,ZombieSurvival.Combat.DamageKind.Environment));
        Reject(()=>TradeService.TryExecute(dead,out _),"смерть отменяет котировку без предметов/платежа");
        session.Close();checks.Add("DONE: реальный prefab, normal frames, временные объекты будут уничтожены");
    }
}
