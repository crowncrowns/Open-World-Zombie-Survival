using System;
using UnityEngine;

namespace ZombieSurvival.Inventory
{
    [Serializable]
    public sealed class ItemStack
    {
        [Tooltip("Общий тип предмета, не создавать отдельный ItemDefinition для каждого экземпляра.")]
        [SerializeField] private ItemDefinition definition;
        [Tooltip("Количество в этой стопке.")]
        [SerializeField, Min(0)] private int quantity;
        [Tooltip("ID экземпляра/стопки: сохраняется при полном переносе, новый при разделении ресурсов.")]
        [SerializeField] private string instanceId;
        [Tooltip("Патроны магазина конкретного оружия, отдельно от общего запаса.")]
        [SerializeField, Min(0)] private int loadedRounds;
        [SerializeField, Min(0), Tooltip("Текущая прочность экземпляра, ед. Ноль — реально сломанная вещь, не признак отсутствия данных.")]
        private float durability;
        [SerializeField, Tooltip("Версия состояния: 0 — старые данные без прочности, 1 — прочность уже записана. Не сбрасывать вручную.")]
        private int stateVersion;
        [NonSerialized] internal ItemStorage Owner;
        [NonSerialized] private bool snapshot;
        public ItemDefinition Definition => definition;
        public int Quantity => quantity;
        public string InstanceId { get { Normalize(); return instanceId; } }
        public int LoadedRounds { get { Normalize(); return loadedRounds; } }
        public float Durability { get { Normalize(); return definition != null && definition.HasDurability ? durability : 0f; } }
        public float NormalizedDurability => definition != null && definition.HasDurability ? Durability / definition.MaximumDurability : 0f;
        public bool IsEmpty => definition == null || quantity <= 0;
        public bool IsSnapshot => snapshot;
        public ItemStack(ItemDefinition item, int amount, float? initialDurability = null)
        {
            if (item == null || amount <= 0 || item.IsIndividual && amount != 1) throw new ArgumentException("New individual items require quantity 1; item and amount must be valid.");
            definition = item; quantity = amount; instanceId = Guid.NewGuid().ToString("N"); stateVersion = 1;
            durability = initialDurability ?? item.MaximumDurability; Normalize();
        }
        internal void Normalize()
        {
            if (string.IsNullOrEmpty(instanceId)) instanceId = Guid.NewGuid().ToString("N");
            if (definition == null) return;
            definition.WarnIfUncategorized();
            if (stateVersion == 0) { durability = definition.MaximumDurability; stateVersion = 1; }
            if (float.IsNaN(durability) || float.IsInfinity(durability)) durability = definition.MaximumDurability;
            durability = Mathf.Clamp(durability, 0f, definition.MaximumDurability);
            loadedRounds = Mathf.Max(0, loadedRounds);
        }
        // Read-only snapshot: preserve state for inspection, never an inventory insertion/duplication command.
        public ItemStack Copy(int amount)
        {
            Normalize();
            if (amount <= 0 || amount > quantity || definition == null) throw new ArgumentOutOfRangeException(nameof(amount));
            var copy = new ItemStack(definition, definition.IsIndividual ? 1 : amount, durability);
            copy.quantity = amount; copy.instanceId = instanceId; copy.loadedRounds = loadedRounds; copy.snapshot = true;
            return copy;
        }
        internal ItemStack SplitResource(int amount) => new ItemStack(definition, amount);
        internal ItemStack LegacyExtra()
        {
            var extra = new ItemStack(definition, 1, Durability);
            // Old batch magazine described one gun, never duplicate its ammunition into every new gun.
            return extra;
        }
        internal void SetLoadedRounds(int value) => loadedRounds = Mathf.Max(0, value);
        internal void SetQuantity(int value) => quantity = value;
        internal void SetDurability(float value) { Normalize(); durability = Mathf.Clamp(value, 0, definition.MaximumDurability); }
        internal void RegenerateId() => instanceId = Guid.NewGuid().ToString("N");
    }
}
