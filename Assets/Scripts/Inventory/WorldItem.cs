using UnityEngine;
using ZombieSurvival.Interaction;

namespace ZombieSurvival.Inventory
{
    public sealed class WorldItem : Interactable
    {
        [Tooltip("Тип лежащего в мире предмета.")]
        [SerializeField] private ItemDefinition item;
        [Tooltip("Количество в этой стопке. Если всё количество не помещается, стопка остаётся на месте.")]
        [SerializeField, Min(1)] private int quantity = 1;
        [Tooltip("Состояние выброшенного экземпляра оружия. Для обычных ресурсов пусто.")]
        [SerializeField] private ItemStack instance;
        private bool collecting;
        public ItemDefinition Item => item;
        public int Quantity => quantity;
        public void Initialize(ItemDefinition definition, int amount) { item = definition; quantity = amount; instance = null; collecting = false; }
        public void Initialize(ItemStack stack) { Initialize(stack.Definition, stack.Quantity); instance = stack.Copy(stack.Quantity); }
        public override bool CanInteract(PlayerInteractor actor) => base.CanInteract(actor) && !collecting && quantity > 0 && item != null && actor.GetComponent<ItemStorage>() != null;
        public override string GetPrompt(PlayerInteractor actor) => item != null ? $"Взять: {item.DisplayName} ×{quantity}" : "Не назначен тип предмета";
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            collecting = true;
            if (!(instance != null && !instance.IsEmpty ? actor.GetComponent<ItemStorage>().TryAddStack(instance) : actor.GetComponent<ItemStorage>().TryAddAll(item, quantity))) { collecting = false; actor.ShowFeedback("Нет места в рюкзаке — предмет остался на земле"); return false; }
            quantity = 0; actor.ShowFeedback("Предмет добавлен в рюкзак"); gameObject.SetActive(false); Destroy(gameObject); return true;
        }
    }
}
