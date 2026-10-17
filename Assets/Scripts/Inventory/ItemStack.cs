using System;
using UnityEngine;

namespace ZombieSurvival.Inventory
{
    [Serializable]
    public sealed class ItemStack
    {
        [Tooltip("Общий тип предмета. Не создавайте отдельный ItemDefinition для каждой стопки.")]
        [SerializeField] private ItemDefinition definition;
        [Tooltip("Количество в этой конкретной стопке.")]
        [SerializeField, Min(0)] private int quantity;
        [Tooltip("ID конкретной стопки; при разделении новая стопка получает новый ID.")]
        [SerializeField] private string instanceId;
        [Tooltip("Патроны в магазине конкретного оружия. Не являются общим запасом рюкзака.")]
        [SerializeField, Min(0)] private int loadedRounds;
        public int LoadedRounds => loadedRounds;
        internal void SetLoadedRounds(int value) => loadedRounds = Mathf.Max(0, value);
        // Копия для мира/переноса сохраняет ID и боезапас. Ресурсы при разделении получают новый ID.
        public ItemStack Copy(int amount) { var copy = new ItemStack(definition, amount); if (definition != null && definition.Weapon != null) { copy.instanceId = instanceId; copy.loadedRounds = loadedRounds; } return copy; }
        public ItemDefinition Definition => definition;
        public int Quantity => quantity;
        public string InstanceId => instanceId;
        public bool IsEmpty => definition == null || quantity <= 0;
        public ItemStack(ItemDefinition item, int amount) { definition = item; quantity = amount; instanceId = Guid.NewGuid().ToString("N"); }
        internal void SetQuantity(int value) { quantity = value; }
    }
}
