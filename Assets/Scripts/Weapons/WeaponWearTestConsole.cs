using UnityEngine;
using ZombieSurvival.Interaction;
using ZombieSurvival.Inventory;

namespace ZombieSurvival.Weapons
{
    /// <summary>Явная выдача новых тестовых вещей, не ремонт и не источник обычной добычи.</summary>
    public sealed class WeaponWearTestConsole : Interactable
    {
        [SerializeField, Tooltip("Тип нового тестового предмета. Существующие вещи игрока не изменяются.")]
        private ItemDefinition item;
        [SerializeField, Min(1), Tooltip("Количество выдаваемого ресурса, шт. Индивидуальное оружие выдаётся по одному.")]
        private int quantity = 12;
        [SerializeField, Min(0), Tooltip("Прочность нового тестового оружия, ед. При 1 пистолет выдержит два выстрела по 0,5, топор — один удар по 1.")]
        private float initialDurability = 1;

        public override string GetPrompt(PlayerInteractor actor) => item == null ? "Т8: предмет не назначен"
            : "Тест Т8: выдать " + item.DisplayName + (item.HasDurability ? " (прочность " + initialDurability.ToString("0.#") + ")" : " ×" + quantity);

        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            if (actor == null || item == null || quantity <= 0 || float.IsNaN(initialDurability) || float.IsInfinity(initialDurability) || initialDurability < 0) return false;
            var storage = actor.GetComponent<ItemStorage>();
            if (storage == null || !storage.isActiveAndEnabled) return false;
            int amount = item.IsIndividual ? 1 : quantity;
            if (storage.FreeCapacity(item) < amount)
            { actor.ShowFeedback("Недостаточно места: тестовые предметы не выданы."); return false; }
            bool success = item.IsIndividual
                ? storage.TryAddStack(new ItemStack(item, 1, item.HasDurability ? initialDurability : (float?)null))
                : storage.TryAddAll(item, amount);
            actor.ShowFeedback(success ? "Тестовый предмет выдан. Tab — выбрать оружие; R — зарядить; ЛКМ — испытать износ." : "Хранилище занято: ничего не выдано.");
            return success;
        }
    }
}
