using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieSurvival.Inventory
{
    /// <summary>Single inventory model. Transfers commit both stores before Changed; GetSlot remains a live read-only view.</summary>
    [DisallowMultipleComponent]
    public sealed class ItemStorage : MonoBehaviour
    {
        [Tooltip("Количество слотов. Уменьшение не удаляет занятые слоты; legacy-миграция может расширить список ради сохранности предметов.")]
        [SerializeField, Range(1, 64)] private int slotCount = 12;
        [Tooltip("Начальное содержимое. Не менять список вручную во время операций с предметами.")]
        [SerializeField] private List<ItemStack> slots = new List<ItemStack>();
        public event Action Changed;
        private bool tradeLocked;
        private static int weaponUsesInProgress;
        public static bool IsWeaponUseBusy => weaponUsesInProgress > 0;
        internal bool TryLockTrade() { if (tradeLocked) return false; tradeLocked = true; return true; }
        internal void UnlockTrade() => tradeLocked = false;
        internal void PublishTrade()
        {
            var listeners = Changed;
            if (listeners == null) return;
            foreach (Action listener in listeners.GetInvocationList())
                try { listener(); } catch (Exception error) { Debug.LogException(error, this); }
        }
        internal sealed class PreparedTopUp
        {
            internal ItemStorage storage;
            internal ItemStack[] items;
            internal int[] quantities;
            internal bool changed;
            internal void Commit()
            {
                for(int i=0;i<items.Length;i++)
                {
                    var item=items[i];if(item==null)continue;
                    if(item.Quantity!=quantities[i])item.SetQuantity(quantities[i]);
                    storage.slots[i]=item;item.Owner=storage;
                }
            }
        }
        internal bool TryPrepareTopUp(IReadOnlyList<ZombieSurvival.Services.TraderProfile.Entry> catalog,out PreparedTopUp plan)
        {
            plan=null;if(tradeLocked||!isActiveAndEnabled||catalog==null)return false;EnsureSlots();
            var prepared=new PreparedTopUp{storage=this,items=slots.ToArray(),quantities=new int[slots.Count]};
            for(int i=0;i<slots.Count;i++)if(prepared.items[i]!=null&&!prepared.items[i].IsEmpty)prepared.quantities[i]=prepared.items[i].Quantity;
            foreach(var entry in catalog)
            {
                if(entry==null||entry.item==null||entry.quantity<0)return false;
                long existing=0;for(int i=0;i<prepared.items.Length;i++)if(prepared.items[i]!=null&&!prepared.items[i].IsEmpty&&prepared.items[i].Definition==entry.item)existing+=prepared.quantities[i];
                int missing=(int)Math.Max(0L,(long)entry.quantity-existing);if(missing==0)continue;
                if(!entry.item.IsIndividual)for(int i=0;i<prepared.items.Length&&missing>0;i++)
                {
                    var item=prepared.items[i];if(item==null||item.IsEmpty||item.Definition!=entry.item)continue;
                    int added=Math.Min(missing,Math.Max(0,entry.item.MaxStack-prepared.quantities[i]));prepared.quantities[i]+=added;missing-=added;prepared.changed|=added>0;
                }
                for(int i=0;i<prepared.items.Length&&missing>0;i++)
                {
                    if(prepared.items[i]!=null&&!prepared.items[i].IsEmpty)continue;
                    int amount=Math.Min(missing,entry.item.MaxStack);prepared.items[i]=new ItemStack(entry.item,amount);prepared.quantities[i]=amount;missing-=amount;prepared.changed=true;
                }
            }
            plan=prepared;return true;
        }
        internal sealed class PreparedRepair
        {
            internal ItemStorage storage;
            internal ItemStack target;
            internal float maximum;
            internal readonly List<int> indices=new List<int>();
            internal readonly List<ItemStack> materials=new List<ItemStack>();
            internal readonly List<int> remaining=new List<int>();
            internal void Commit()
            {
                for(int i=0;i<materials.Count;i++)
                {
                    materials[i].SetQuantity(remaining[i]);
                    if(remaining[i]==0){storage.slots[indices[i]]=null;materials[i].Owner=null;}
                }
                target.SetDurability(maximum);
            }
        }
        internal bool TryPrepareRepair(ItemStack target,IReadOnlyList<ZombieSurvival.Services.RepairCost> costs,out PreparedRepair plan,out string reason)
        {
            plan=null;reason="";
            if(tradeLocked||ZombieSurvival.Services.Trader.RestockIsBusy||!isActiveAndEnabled||!Contains(target)||target.IsSnapshot||!target.Definition.Repairable||target.Quantity!=1)
            {reason="Инвентарь занят или вещь недоступна";return false;}
            if(costs==null||costs.Count==0){reason="Рецепт ремонта пуст";return false;}
            var prepared=new PreparedRepair{storage=this,target=target,maximum=target.Definition.MaximumDurability};var seen=new HashSet<ItemDefinition>();
            foreach(var cost in costs)
            {
                if(cost==null||cost.Material==null||cost.Material.HasDurability||cost.Material.Category!=ItemCategory.Material||cost.Amount<=0||!seen.Add(cost.Material))
                {reason="Некорректные материалы ремонта";return false;}
                int needed=cost.Amount;
                for(int i=0;i<slots.Count&&needed>0;i++)
                {
                    var slot=slots[i];if(slot==null||slot.IsEmpty||slot.Definition!=cost.Material||slot==target)continue;
                    int take=Math.Min(needed,slot.Quantity);needed-=take;
                    prepared.indices.Add(i);prepared.materials.Add(slot);prepared.remaining.Add(slot.Quantity-take);
                }
                if(needed>0){reason="Недостаточно материала: "+cost.Material.DisplayName;return false;}
            }
            plan=prepared;return true;
        }
        internal sealed class TradeTransfer
        {
            internal ItemStorage source, destination;
            internal ItemStack original, incoming;
            internal int sourceIndex, amount, remaining, emptyIndex;
            internal bool full;
            internal readonly List<ItemStack> merged = new List<ItemStack>();
            internal readonly List<int> quantities = new List<int>();
            internal void Commit()
            {
                if (full) { source.slots[sourceIndex] = null; original.Owner = null; }
                else original.SetQuantity(original.Quantity - amount);
                for (int i = 0; i < merged.Count; i++) merged[i].SetQuantity(quantities[i]);
                incoming.SetQuantity(remaining);
                if (remaining > 0) { destination.slots[emptyIndex] = incoming; incoming.Owner = destination; }
                else incoming.Owner = null;
            }
        }
        internal bool TryPrepareTrade(ItemStorage destination, ItemStack item, int amount, out TradeTransfer plan)
        {
            plan = null;
            if (tradeLocked || destination == null || destination.tradeLocked || destination == this || amount <= 0
                || !Contains(item) || item.IsSnapshot || amount > item.Quantity || destination.FreeCapacity(item.Definition) < amount) return false;
            foreach (var other in destination.slots) if (other != null && !other.IsEmpty && other.InstanceId == item.InstanceId) return false;
            var prepared = new TradeTransfer { source = this, destination = destination, original = item, sourceIndex = slots.IndexOf(item), amount = amount,
                full = amount == item.Quantity, remaining = amount, emptyIndex = -1 };
            if (!item.Definition.IsIndividual)
                foreach (var target in destination.slots)
                {
                    if (target == null || target.IsEmpty || target.Definition != item.Definition) continue;
                    int moved = Math.Min(prepared.remaining, Math.Max(0, item.Definition.MaxStack - target.Quantity));
                    if (moved == 0) continue;
                    prepared.merged.Add(target); prepared.quantities.Add(target.Quantity + moved); prepared.remaining -= moved;
                    if (prepared.remaining == 0) break;
                }
            if (prepared.remaining > 0)
            {
                prepared.emptyIndex = destination.slots.FindIndex(s => s == null || s.IsEmpty);
                if (prepared.emptyIndex < 0) return false;
            }
            prepared.incoming = prepared.full ? item : item.SplitResource(amount);
            plan = prepared; return true;
        }
        public int SlotCount { get { EnsureSlots(); return slots.Count; } }
        public ItemStack GetSlot(int index) { EnsureSlots(); return index >= 0 && index < slots.Count ? slots[index] : null; }
        private void Awake() => EnsureSlots();
        private void EnsureSlots()
        {
            if (slots == null) slots = new List<ItemStack>();
            while (slots.Count < slotCount) slots.Add(null);
            bool migrated = false;
            for (int i = 0; i < slots.Count; i++)
            {
                var stack = slots[i]; if (stack == null || stack.IsEmpty) continue;
                stack.Normalize(); stack.Owner = this;
                int excess = stack.Quantity - stack.Definition.MaxStack;
                if (excess <= 0) continue;
                stack.SetQuantity(stack.Definition.MaxStack);
                while (excess > 0)
                {
                    int n = Mathf.Min(excess, stack.Definition.MaxStack);
                    var extra = stack.Definition.IsIndividual ? stack.LegacyExtra() : stack.SplitResource(n);
                    int empty = slots.FindIndex(s => s == null || s.IsEmpty);
                    if (empty < 0) { empty = slots.Count; slots.Add(null); }
                    extra.Owner = this; slots[empty] = extra; excess -= n;
                }
                migrated = true;
            }
            var ids = new HashSet<string>();
            foreach (var stack in slots)
            {
                if (stack == null || stack.IsEmpty) continue;
                if (!ids.Add(stack.InstanceId)) { stack.RegenerateId(); ids.Add(stack.InstanceId); migrated = true; }
            }
            if (migrated) Debug.LogWarning("ItemStorage: старые стопки разделены/ID исправлены без потери количества; дополнительные слоты сохранены. Магазин старой пачки остаётся только у первого оружия.", this);
        }
        // Stage 11 restore is a world transaction. No events until EVERY participating storage is replaced.
        // Future trade/repair must not call this API; use the normal transfer/transaction contracts.
        internal void ReplaceSavedSlots(List<ItemStack> restored)
        {
            if (tradeLocked) throw new InvalidOperationException("Хранилище участвует в завершении сделки.");
            EnsureSlots();
            // Retire stale live handles so old UI/NPC references cannot reinsert a pre-load item.
            foreach (var stack in slots) if (stack != null) { stack.Owner = null; stack.SetQuantity(0); }
            slots = restored;
            foreach (var stack in slots) if (stack != null) stack.Owner = this;
            EnsureSlots();
        }
        internal void NotifySavedRestore() => Changed?.Invoke();
        public int FreeCapacity(ItemDefinition item)
        {
            if (item == null) return 0;
            EnsureSlots(); int capacity = 0;
            foreach (var slot in slots)
            {
                int available = slot == null || slot.IsEmpty ? item.MaxStack : slot.Definition == item ? Mathf.Max(0, item.MaxStack - slot.Quantity) : 0;
                if (available > int.MaxValue - capacity) return int.MaxValue;
                capacity += available;
            }
            return capacity;
        }
        public bool TryAddAll(ItemDefinition item, int amount)
        {
            if (tradeLocked) return false;
            if (item == null || amount <= 0 || FreeCapacity(item) < amount) return false;
            while (amount > 0) { int n = Mathf.Min(amount, item.MaxStack); AddIncoming(new ItemStack(item, n)); amount -= n; }
            Changed?.Invoke(); return true;
        }
        public int Count(ItemDefinition item) { EnsureSlots(); int n = 0; foreach (var s in slots) if (s != null && s.Definition == item) n += s.Quantity; return n; }
        public bool Contains(ItemStack stack) { EnsureSlots(); return stack != null && stack.Owner == this && slots.Contains(stack) && !stack.IsEmpty; }
        public bool TryAddStack(ItemStack stack)
        {
            if (tradeLocked) return false;
            if (stack == null || stack.IsEmpty || stack.IsSnapshot || stack.Owner != null || FreeCapacity(stack.Definition) < stack.Quantity) return false;
            foreach (var existing in slots) if (existing != null && !existing.IsEmpty && existing.InstanceId == stack.InstanceId) return false;
            stack.Normalize();
            if (stack.Definition.IsIndividual && stack.Quantity > 1)
            {
                int extras = stack.Quantity - 1; stack.SetQuantity(1); AddIncoming(stack);
                for (int i = 0; i < extras; i++) AddIncoming(stack.LegacyExtra());
                Debug.LogWarning("World legacy batch migrated: original ID/magazine retained once; extra individuals have new IDs and empty magazines.", this);
            }
            else AddIncoming(stack);
            Changed?.Invoke(); return true;
        }
        private void AddIncoming(ItemStack incoming)
        {
            var item = incoming.Definition;
            if (!item.IsIndividual)
                foreach (var slot in slots)
                {
                    if (slot == null || slot.IsEmpty || slot.Definition != item) continue;
                    int n = Mathf.Min(incoming.Quantity, Mathf.Max(0, item.MaxStack - slot.Quantity));
                    slot.SetQuantity(slot.Quantity + n); incoming.SetQuantity(incoming.Quantity - n);
                    if (incoming.Quantity == 0) return; // Receiving stack keeps its ID; exhausted identity is retired.
                }
            while (incoming.Quantity > item.MaxStack)
            {
                var part = incoming.SplitResource(item.MaxStack);
                incoming.SetQuantity(incoming.Quantity - part.Quantity); Place(part);
            }
            if (!incoming.IsEmpty) Place(incoming);
        }
        private void Place(ItemStack stack)
        {
            int empty = slots.FindIndex(s => s == null || s.IsEmpty);
            if (empty < 0) throw new InvalidOperationException("Capacity must be checked before insertion.");
            slots[empty] = stack; stack.Owner = this;
        }
        public bool TrySetDurability(int index, float value)
        {
            if (tradeLocked) return false;
            var stack = GetSlot(index);
            if (stack == null || stack.IsEmpty || !stack.Definition.HasDurability || float.IsNaN(value) || float.IsInfinity(value)) return false;
            float old = stack.Durability; stack.SetDurability(value);
            if (old != stack.Durability) Changed?.Invoke(); return true;
        }
        public int Reload(ItemStack weapon)
        {
            if (tradeLocked) return 0;
            if (!Contains(weapon) || weapon.Definition.Weapon == null) return 0;
            var data = weapon.Definition.Weapon; if (data.Ammo == null) return 0;
            int amount = Mathf.Min(Mathf.Max(0, data.MagazineSize - weapon.LoadedRounds), Count(data.Ammo)), left = amount;
            for (int i = 0; i < slots.Count && left > 0; i++) if (slots[i] != null && slots[i].Definition == data.Ammo)
            { int n = Mathf.Min(left, slots[i].Quantity); RemoveInternal(i, n); left -= n; }
            weapon.SetLoadedRounds(weapon.LoadedRounds + amount); if (amount > 0) Changed?.Invoke(); return amount;
        }
        public bool SpendRound(ItemStack weapon)
        {
            if (tradeLocked) return false;
            if (!Contains(weapon) || weapon.Definition.Weapon == null || weapon.LoadedRounds <= 0) return false;
            weapon.SetLoadedRounds(weapon.LoadedRounds - 1); Changed?.Invoke(); return true;
        }
        public static bool IsWeaponBroken(ItemStack item) => item != null && !item.IsEmpty && item.Definition.Weapon != null
            && item.Definition.HasDurability && item.Durability <= 0f;
        /// <summary>Read-only preflight. Completion must revalidate with TryUseWeapon.</summary>
        public bool CanUseWeapon(ItemStack weapon, float wear, bool spendRound, out string reason)
        {
            reason = "";
            if (tradeLocked || !isActiveAndEnabled) { reason = "Инвентарь занят или недоступен"; return false; }
            if (!Contains(weapon) || weapon.IsSnapshot || weapon.Definition.Weapon == null) { reason = "Оружие больше не принадлежит персонажу"; return false; }
            if (float.IsNaN(wear) || float.IsInfinity(wear) || wear < 0f) { reason = "Некорректная настройка износа оружия"; return false; }
            if (IsWeaponBroken(weapon)) { reason = "Оружие сломано"; return false; }
            if (spendRound && weapon.LoadedRounds <= 0) { reason = "Магазин пуст — R"; return false; }
            return true;
        }
        /// <summary>One committed weapon use: ammunition and condition change before a single notification.
        /// Storage remains mutation-locked through observers, just like a trade commit.</summary>
        public bool TryUseWeapon(ItemStack weapon, float wear, bool spendRound, out string reason)
        {
            if (!CanUseWeapon(weapon, wear, spendRound, out reason)) return false;
            tradeLocked = true; weaponUsesInProgress++;
            try
            {
                float before = weapon.Durability;
                if (spendRound) weapon.SetLoadedRounds(weapon.LoadedRounds - 1);
                if (weapon.Definition.HasDurability && wear > 0f) weapon.SetDurability(Mathf.Max(0f, before - wear));
                if (spendRound || before != weapon.Durability) PublishTrade();
                return true;
            }
            finally { weaponUsesInProgress--; tradeLocked = false; }
        }
        public int TransferTo(ItemStorage destination, int index, int requested = int.MaxValue)
        {
            if (tradeLocked || destination != null && destination.tradeLocked) return 0;
            var slot = GetSlot(index);
            if (destination == null || destination == this || slot == null || slot.IsEmpty || requested <= 0) return 0;
            int amount = Mathf.Min(requested, Mathf.Min(slot.Quantity, destination.FreeCapacity(slot.Definition)));
            if (amount <= 0) return 0;
            foreach (var other in destination.slots) if (other != null && !other.IsEmpty && other.InstanceId == slot.InstanceId) return 0;
            var moved = ExtractInternal(index, amount); destination.AddIncoming(moved);
            Changed?.Invoke(); destination.Changed?.Invoke(); return amount;
        }
        public bool TryExtract(int index, int amount, out ItemStack extracted)
        {
            if (tradeLocked) { extracted = null; return false; }
            extracted = null; var stack = GetSlot(index);
            if (stack == null || stack.IsEmpty || amount <= 0 || amount > stack.Quantity) return false;
            extracted = ExtractInternal(index, amount); Changed?.Invoke(); return true;
        }
        private ItemStack ExtractInternal(int index, int amount)
        {
            var stack = slots[index];
            if (amount == stack.Quantity) { slots[index] = null; stack.Owner = null; return stack; }
            stack.SetQuantity(stack.Quantity - amount); return stack.SplitResource(amount);
        }
        // Only for failed world initialization: return exactly what was extracted, never recreate a default item.
        internal void RestoreExtracted(int preferredIndex, ItemStack stack)
        {
            if (tradeLocked) throw new InvalidOperationException("Хранилище участвует в завершении сделки.");
            EnsureSlots();
            if (stack == null || stack.IsEmpty || stack.Owner != null || stack.IsSnapshot) throw new InvalidOperationException("Invalid rollback payload.");
            if (preferredIndex >= 0 && preferredIndex < slots.Count && (slots[preferredIndex] == null || slots[preferredIndex].IsEmpty))
            { slots[preferredIndex] = stack; stack.Owner = this; }
            else if (FreeCapacity(stack.Definition) >= stack.Quantity) AddIncoming(stack);
            else { slots.Add(stack); stack.Owner = this; Debug.LogWarning("Rollback required an extra slot to preserve the extracted item.", this); }
            Changed?.Invoke();
        }
        internal bool Remove(int index, int amount)
        {
            if (tradeLocked) return false;
            var slot = GetSlot(index); if (slot == null || slot.IsEmpty || amount <= 0 || amount > slot.Quantity) return false;
            RemoveInternal(index, amount); Changed?.Invoke(); return true;
        }
        private void RemoveInternal(int index, int amount)
        {
            var slot = slots[index]; slot.SetQuantity(slot.Quantity - amount);
            if (slot.Quantity == 0) { slot.Owner = null; slots[index] = null; }
        }
    }
}
