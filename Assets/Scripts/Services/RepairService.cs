using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Weapons;
namespace ZombieSurvival.Services
{
    public sealed class RepairCost
    {
        public ItemDefinition Material{get;}
        public int Amount{get;}
        internal RepairCost(ItemDefinition material,int amount){Material=material;Amount=amount;}
    }
    public sealed class RepairQuote
    {
        internal RepairQuote(){}
        public RepairWorkbench Workbench{get;internal set;}
        public ItemStack Item{get;internal set;}
        public float CurrentDurability{get;internal set;}
        public float MaximumDurability{get;internal set;}
        public IReadOnlyList<RepairCost> Costs{get;internal set;}
        internal PlayerServiceSession session;
        internal ItemStorage storage;
        internal RepairPolicy policy;
        internal RepairRecipe recipe;
        internal string configuration,id,itemProfile;
        internal ItemDefinition itemType;
        internal long sessionVersion;
        internal int quantity,rounds;
        internal bool consumed;
        internal ItemStack[] slots;
        internal string[] slotIds;
        internal int[] slotQuantities;
    }
    public static class RepairService
    {
        public static bool IsBusy{get;private set;}
        private static bool Fail(string text,out string reason){reason=text;return false;}
        public static bool TryPreviewCost(RepairPolicy policy,ItemStack item,out IReadOnlyList<RepairCost> costs,out string reason)
            => Calculate(policy,item,out _,out costs,out reason);
        private static bool Calculate(RepairPolicy policy,ItemStack item,out RepairRecipe recipe,out IReadOnlyList<RepairCost> costs,out string reason)
        {
            recipe=null;costs=Array.Empty<RepairCost>();
            if(policy==null||item==null||item.IsEmpty||!item.Definition.HasDurability||!item.Definition.Repairable)return Fail("Этот тип нельзя ремонтировать",out reason);
            float max=item.Definition.MaximumDurability,current=item.Durability;
            if(float.IsNaN(max)||float.IsInfinity(max)||max<=0||float.IsNaN(current)||float.IsInfinity(current)||current<0||current>max)return Fail("Некорректная прочность предмета",out reason);
            if(current>=max)return Fail("Предмет полностью исправен",out reason);
            if(!policy.TryResolve(item.Definition,out recipe,out reason))return false;
            if(recipe.materials==null||recipe.materials.Length==0)return Fail("Рецепт ремонта пуст",out reason);
            var materials=new HashSet<ItemDefinition>();var rows=new List<RepairCost>();
            try
            {
                decimal missing=(decimal)(max-current)/(decimal)max;
                foreach(var row in recipe.materials)
                {
                    if(row==null||row.material==null||row.material.Category!=ItemCategory.Material||row.material.IsIndividual||row.fullRepairAmount<=0||!materials.Add(row.material))return Fail("Некорректный материал или расход рецепта",out reason);
                    decimal amount=decimal.Ceiling(row.fullRepairAmount*missing);
                    if(amount<1||amount>int.MaxValue)return Fail("Расход материала вне допустимого диапазона",out reason);
                    rows.Add(new RepairCost(row.material,(int)amount));
                }
            }
            catch(OverflowException){return Fail("Числа ремонта вне допустимого диапазона",out reason);}
            costs=rows.AsReadOnly();reason="";return true;
        }
        public static bool TryQuote(PlayerServiceSession session,RepairWorkbench workbench,ItemStack item,out RepairQuote quote,out string reason)
        {
            quote=null;if(IsBusy||TradeService.IsBusy||PlayerWeapons.IsBusy||ItemStorage.IsWeaponUseBusy)return Fail("Предыдущее действие ещё завершается",out reason);
            return Build(session,workbench,item,out quote,out reason);
        }
        private static bool Build(PlayerServiceSession session,RepairWorkbench workbench,ItemStack item,out RepairQuote quote,out string reason)
        {
            quote=null;
            if(session==null||workbench==null||!workbench.isActiveAndEnabled)return Fail("Верстак недоступен",out reason);
            if(!session.ValidateAccess(workbench.GetComponent<WorkbenchServicePoint>(),out reason))return false;
            var storage=session.GetComponent<ItemStorage>();var weapons=session.GetComponent<PlayerWeapons>();
            if(storage==null||!storage.isActiveAndEnabled||item==null||item.IsSnapshot||!storage.Contains(item)||item.Quantity!=1)return Fail("Выберите оригинал вещи из своего рюкзака",out reason);
            if(weapons!=null&&(weapons.IsAttacking||weapons.IsReloading))return Fail("Завершите атаку или перезарядку",out reason);
            var policy=workbench.Policy;
            if(!Calculate(policy,item,out var recipe,out var costs,out reason))return false;
            if(!storage.TryPrepareRepair(item,costs,out _,out reason))return false;
            int count=storage.SlotCount;var slots=new ItemStack[count];var ids=new string[count];var quantities=new int[count];
            for(int i=0;i<count;i++){var slot=storage.GetSlot(i);slots[i]=slot;ids[i]=slot?.InstanceId;quantities[i]=slot!=null&&!slot.IsEmpty?slot.Quantity:0;}
            quote=new RepairQuote{Workbench=workbench,Item=item,CurrentDurability=item.Durability,MaximumDurability=item.Definition.MaximumDurability,Costs=costs,
                session=session,storage=storage,policy=policy,recipe=recipe,configuration=policy.ConfigurationStamp(),id=item.InstanceId,itemType=item.Definition,itemProfile=item.Definition.RepairProfileId,sessionVersion=session.SessionVersion,
                quantity=item.Quantity,rounds=item.LoadedRounds,slots=slots,slotIds=ids,slotQuantities=quantities};reason="";return true;
        }
        public static bool TryExecute(RepairQuote quote,out string reason)
        {
            if(IsBusy||TradeService.IsBusy||PlayerWeapons.IsBusy||ItemStorage.IsWeaponUseBusy||quote==null||quote.consumed)return Fail("Предложение уже исполнено или действие занято",out reason);
            IsBusy=true;bool locked=false;
            try
            {
                if(quote.session==null||quote.session.SessionVersion!=quote.sessionVersion)return Fail("Ремонтная сессия уже изменилась",out reason);
                if(!Build(quote.session,quote.Workbench,quote.Item,out var current,out reason))return false;
                if(current.storage!=quote.storage||current.policy!=quote.policy||current.recipe!=quote.recipe||current.configuration!=quote.configuration||current.id!=quote.id||current.itemType!=quote.itemType||current.itemProfile!=quote.itemProfile
                    ||current.quantity!=quote.quantity||current.rounds!=quote.rounds||current.CurrentDurability!=quote.CurrentDurability||current.MaximumDurability!=quote.MaximumDurability||current.slots.Length!=quote.slots.Length)
                    return Fail("Предмет или правила изменились. Получите новое предложение",out reason);
                for(int i=0;i<current.slots.Length;i++)if(current.slots[i]!=quote.slots[i]||current.slotIds[i]!=quote.slotIds[i]||current.slotQuantities[i]!=quote.slotQuantities[i])return Fail("Содержимое рюкзака изменилось. Получите новое предложение",out reason);
                if(!quote.storage.TryPrepareRepair(quote.Item,current.Costs,out var prepared,out reason))return false;
                locked=quote.storage.TryLockTrade();if(!locked)return Fail("Инвентарь занят",out reason);
                prepared.Commit();quote.consumed=true;quote.storage.PublishTrade();reason="";return true;
            }
            finally{if(locked)quote.storage.UnlockTrade();IsBusy=false;}
        }
    }
}
