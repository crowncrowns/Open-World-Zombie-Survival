using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Economy;

namespace ZombieSurvival.Services
{
    /// <summary>Read-only ordering/comparison of live instances. Never changes storage, selection or quotes.</summary>
    internal sealed class ItemListPresentation
    {
        private ItemCategory? category;
        private int sort;
        internal void Reset(){category=null;sort=0;}
        private sealed class Entry
        {
            internal ItemStack item;internal int slot;internal bool hasPrice;internal long price;
        }
        internal void DrawControls(ItemStorage storage,bool prices,GUIStyle button,ref Vector2 scroll,bool categories=true)
        {
            if(categories)
            {
                var values=new List<ItemCategory>();if(storage!=null)for(int i=0;i<storage.SlotCount;i++){var item=storage.GetSlot(i);if(item!=null&&!item.IsEmpty&&!values.Contains(item.Definition.Category))values.Add(item.Definition.Category);}
                values.Sort((a,b)=>((int)a).CompareTo((int)b));
                if(category.HasValue&&!values.Contains(category.Value))category=null;
                string caption=category.HasValue?CategoryName(category.Value):"Все";
                if(GUILayout.Button("Категория: "+caption+"  ›",button,GUILayout.Height(30)))
                {int index=category.HasValue?values.IndexOf(category.Value):-1;category=index+1<values.Count?(ItemCategory?)values[index+1]:null;scroll=Vector2.zero;}
            }
            string[] labels=prices?new[]{"Имя А—Я","Цена ↑","Прочность ↓"}:new[]{"Имя А—Я","Прочность ↓"};
            if(GUILayout.Button("Сортировка: "+labels[sort%labels.Length]+"  ›",button,GUILayout.Height(30))){sort=(sort+1)%labels.Length;scroll=Vector2.zero;}
            GUILayout.Space(7);
        }
        internal List<ItemStack> Items(ItemStorage storage,TraderProfile profile,TradeDirection direction,bool prices,Predicate<ItemStack> include=null,Trader trader=null)
        {
            var rows=new List<Entry>();
            if(storage!=null)for(int i=0;i<storage.SlotCount;i++)
            {
                var item=storage.GetSlot(i);if(item==null||item.IsEmpty||category.HasValue&&item.Definition.Category!=category.Value||include!=null&&!include(item))continue;
                long unit=0;bool available=prices&&profile!=null&&(direction!=TradeDirection.Sell||profile.Accepts(item.Definition))&&Preview(trader,profile,direction,item,out unit);
                rows.Add(new Entry{item=item,slot=i,hasPrice=available,price=unit});
            }
            rows.Sort((a,b)=>
            {
                int compare=0;
                if(prices&&sort==1){compare=b.hasPrice.CompareTo(a.hasPrice);if(compare==0&&a.hasPrice)compare=a.price.CompareTo(b.price);}
                else if(prices&&sort==2||!prices&&sort==1)
                {bool x=a.item.Definition.HasDurability,y=b.item.Definition.HasDurability;compare=y.CompareTo(x);if(compare==0&&x)compare=(b.item.Durability/b.item.Definition.MaximumDurability).CompareTo(a.item.Durability/a.item.Definition.MaximumDurability);}
                if(compare==0)compare=StringComparer.CurrentCultureIgnoreCase.Compare(a.item.Definition.DisplayName,b.item.Definition.DisplayName);
                if(compare==0)compare=StringComparer.Ordinal.Compare(a.item.InstanceId,b.item.InstanceId);
                return compare!=0?compare:a.slot.CompareTo(b.slot);
            });
            var result=new List<ItemStack>();foreach(var row in rows)result.Add(row.item);return result;
        }
        private static string CategoryName(ItemCategory category)
        {
            var field=typeof(ItemCategory).GetField(category.ToString());var attribute=field!=null?Attribute.GetCustomAttribute(field,typeof(InspectorNameAttribute)) as InspectorNameAttribute:null;return attribute?.displayName??category.ToString();
        }
        private static bool Preview(Trader trader,TraderProfile profile,TradeDirection direction,ItemStack item,out long unit)
            => trader!=null?TradeService.TryPreviewPrice(trader,direction,item,1,out unit,out _,out _):TradeService.TryPreviewPrice(profile,direction,item,1,out unit,out _,out _);
        internal static void DrawComparison(ItemStack selected,ItemStorage bag,TraderProfile profile,RepairPolicy repair,GUIStyle heading,GUIStyle text,Trader trader=null)
        {
            if(selected==null||!selected.Definition.HasDurability||bag==null)return;
            var others=new List<ItemStack>();for(int i=0;i<bag.SlotCount;i++){var item=bag.GetSlot(i);if(item!=null&&!item.IsEmpty&&item!=selected&&item.Definition==selected.Definition)others.Add(item);}
            others.Sort((a,b)=>StringComparer.Ordinal.Compare(a.InstanceId,b.InstanceId));
            GUILayout.Space(14);GUILayout.Label("ВАШИ ЭКЗЕМПЛЯРЫ ЭТОГО ТИПА",heading);
            if(others.Count==0){GUILayout.Label("Других собственных экземпляров этого типа нет.",text);return;}
            for(int i=0;i<others.Count;i++)
            {
                var own=others[i];float delta=own.Durability-selected.Durability;
                GUILayout.Label("Экземпляр "+(i+1)+": "+own.Durability.ToString("0.#")+" / "+own.Definition.MaximumDurability.ToString("0.#")+" · разница "+(delta>0?"+":"")+delta.ToString("0.#"),text);
                if(own.Definition.Weapon!=null&&own.Definition.Weapon.Ammo!=null)GUILayout.Label("Магазин: "+own.LoadedRounds+" патр.",text);
                if(profile!=null)
                {
                    string buy=Preview(trader,profile,TradeDirection.Buy,own,out long buyUnit)?Wallet.FormatRubles(buyUnit):"нет цены";
                    string sell=profile.Accepts(own.Definition)&&Preview(trader,profile,TradeDirection.Sell,own,out long sellUnit)?Wallet.FormatRubles(sellUnit):"не выкупается";
                    GUILayout.Label("Справочно: покупка "+buy+" · продажа "+sell,text);
                }
                if(repair!=null)
                {
                    if(RepairService.TryPreviewCost(repair,own,out var costs,out var reason))
                    {var parts=new List<string>();foreach(var cost in costs)parts.Add(cost.Material.DisplayName+" ×"+cost.Amount);GUILayout.Label("До полного ремонта: "+string.Join(", ",parts),text);}
                    else GUILayout.Label(reason,text);
                }
                GUILayout.Space(5);
            }
            if(profile!=null)GUILayout.Label("Справочные цены текущего торговца за один экземпляр в его текущем состоянии.",text);
        }
    }
}
