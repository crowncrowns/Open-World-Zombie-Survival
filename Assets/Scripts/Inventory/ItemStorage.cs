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
            foreach (var slot in slots) capacity += slot == null || slot.IsEmpty ? item.MaxStack : slot.Definition == item ? Mathf.Max(0, item.MaxStack - slot.Quantity) : 0;
            return capacity;
        }
        public bool TryAddAll(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0 || FreeCapacity(item) < amount) return false;
            while (amount > 0) { int n = Mathf.Min(amount, item.MaxStack); AddIncoming(new ItemStack(item, n)); amount -= n; }
            Changed?.Invoke(); return true;
        }
        public int Count(ItemDefinition item) { EnsureSlots(); int n = 0; foreach (var s in slots) if (s != null && s.Definition == item) n += s.Quantity; return n; }
        public bool Contains(ItemStack stack) { EnsureSlots(); return stack != null && stack.Owner == this && slots.Contains(stack) && !stack.IsEmpty; }
        public bool TryAddStack(ItemStack stack)
        {
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
            var stack = GetSlot(index);
            if (stack == null || stack.IsEmpty || !stack.Definition.HasDurability || float.IsNaN(value) || float.IsInfinity(value)) return false;
            float old = stack.Durability; stack.SetDurability(value);
            if (old != stack.Durability) Changed?.Invoke(); return true;
        }
        public int Reload(ItemStack weapon)
        {
            if (!Contains(weapon) || weapon.Definition.Weapon == null) return 0;
            var data = weapon.Definition.Weapon; if (data.Ammo == null) return 0;
            int amount = Mathf.Min(Mathf.Max(0, data.MagazineSize - weapon.LoadedRounds), Count(data.Ammo)), left = amount;
            for (int i = 0; i < slots.Count && left > 0; i++) if (slots[i] != null && slots[i].Definition == data.Ammo)
            { int n = Mathf.Min(left, slots[i].Quantity); RemoveInternal(i, n); left -= n; }
            weapon.SetLoadedRounds(weapon.LoadedRounds + amount); if (amount > 0) Changed?.Invoke(); return amount;
        }
        public bool SpendRound(ItemStack weapon)
        {
            if (!Contains(weapon) || weapon.Definition.Weapon == null || weapon.LoadedRounds <= 0) return false;
            weapon.SetLoadedRounds(weapon.LoadedRounds - 1); Changed?.Invoke(); return true;
        }
        public int TransferTo(ItemStorage destination, int index, int requested = int.MaxValue)
        {
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
