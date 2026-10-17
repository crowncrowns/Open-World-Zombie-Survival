using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieSurvival.Inventory
{
    /// <summary>Общее хранилище игрока, ящика и будущего NPC. Изменения только через API;
    /// события публикуются ПОСЛЕ завершения обеих сторон переноса.</summary>
    [DisallowMultipleComponent]
    public sealed class ItemStorage : MonoBehaviour
    {
        [Tooltip("Количество слотов. Изменяйте до Play; уменьшение не удаляет уже занятые слоты.")]
        [SerializeField, Range(1, 64)] private int slotCount = 12;
        [Tooltip("Начальное содержимое. Не меняйте список вручную во время переноса предметов.")]
        [SerializeField] private List<ItemStack> slots = new List<ItemStack>();
        public event Action Changed;
        public int SlotCount { get { EnsureSlots(); return slots.Count; } }
        public ItemStack GetSlot(int index) { EnsureSlots(); return index >= 0 && index < slots.Count ? slots[index] : null; }
        private void Awake() => EnsureSlots();
        private void EnsureSlots() { while (slots.Count < slotCount) slots.Add(null); }
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
            AddInternal(item, amount); Changed?.Invoke(); return true;
        }
        private void AddInternal(ItemDefinition item, int amount)
        {
            for (int i = 0; i < slots.Count && amount > 0; i++)
            {
                var slot = slots[i]; if (slot == null || slot.IsEmpty || slot.Definition != item) continue;
                int n = Mathf.Min(amount, Mathf.Max(0, item.MaxStack - slot.Quantity));
                slot.SetQuantity(slot.Quantity + n); amount -= n;
            }
            for (int i = 0; i < slots.Count && amount > 0; i++)
                if (slots[i] == null || slots[i].IsEmpty) { int n = Mathf.Min(amount, item.MaxStack); slots[i] = new ItemStack(item, n); amount -= n; }
        }
        public int Count(ItemDefinition item) { EnsureSlots(); int n = 0; foreach (var s in slots) if (s != null && s.Definition == item) n += s.Quantity; return n; }
        public bool Contains(ItemStack stack) { EnsureSlots(); return stack != null && slots.Contains(stack) && !stack.IsEmpty; }
        public bool TryAddStack(ItemStack stack)
        {
            if (stack == null || stack.IsEmpty || FreeCapacity(stack.Definition) < stack.Quantity) return false;
            if (stack.Definition.Weapon == null) return TryAddAll(stack.Definition, stack.Quantity);
            if (stack.Quantity != 1) return false;
            for (int i = 0; i < slots.Count; i++) if (slots[i] == null || slots[i].IsEmpty) { slots[i] = stack.Copy(1); Changed?.Invoke(); return true; }
            return false;
        }
        // Перезарядка коммитится атомарно: сначала меняются обе стороны, затем одно событие.
        public int Reload(ItemStack weapon)
        {
            if (!Contains(weapon) || weapon.Definition.Weapon == null) return 0;
            var data = weapon.Definition.Weapon; if (data.Ammo == null) return 0;
            int needed = Mathf.Max(0, data.MagazineSize - weapon.LoadedRounds);
            int amount = Mathf.Min(needed, Count(data.Ammo)), left = amount;
            for (int i = 0; i < slots.Count && left > 0; i++) if (slots[i] != null && slots[i].Definition == data.Ammo)
            { int n = Mathf.Min(left, slots[i].Quantity); RemoveInternal(i, n); left -= n; }
            weapon.SetLoadedRounds(weapon.LoadedRounds + amount); if (amount > 0) Changed?.Invoke(); return amount;
        }
        public bool SpendRound(ItemStack weapon)
        {
            if (!Contains(weapon) || weapon.LoadedRounds <= 0) return false;
            weapon.SetLoadedRounds(weapon.LoadedRounds - 1); Changed?.Invoke(); return true;
        }
        public int TransferTo(ItemStorage destination, int index, int requested = int.MaxValue)
        {
            var slot = GetSlot(index);
            if (destination == null || destination == this || slot == null || slot.IsEmpty || requested <= 0) return 0;
            int amount = Mathf.Min(requested, Mathf.Min(slot.Quantity, destination.FreeCapacity(slot.Definition)));
            if (amount <= 0) return 0;
            if (slot.Definition.Weapon != null)
            {
                for (int i = 0; i < destination.slots.Count; i++) if (destination.slots[i] == null || destination.slots[i].IsEmpty) { destination.slots[i] = slot.Copy(1); break; }
            }
            else destination.AddInternal(slot.Definition, amount);
            RemoveInternal(index, amount);
            Changed?.Invoke(); destination.Changed?.Invoke(); return amount;
        }
        internal bool Remove(int index, int amount)
        {
            var slot = GetSlot(index); if (slot == null || slot.IsEmpty || amount <= 0 || amount > slot.Quantity) return false;
            RemoveInternal(index, amount); Changed?.Invoke(); return true;
        }
        private void RemoveInternal(int index, int amount)
        {
            var slot = slots[index]; slot.SetQuantity(slot.Quantity - amount); if (slot.Quantity == 0) slots[index] = null;
        }
    }
}
