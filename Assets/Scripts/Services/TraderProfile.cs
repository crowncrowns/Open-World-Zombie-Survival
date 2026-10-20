using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Inventory;

namespace ZombieSurvival.Services
{
    [CreateAssetMenu(menuName = "Zombie Survival/Торговый профиль")]
    public sealed class TraderProfile : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            [Tooltip("Существующий тип предмета; состояние экземпляра хранится только в ItemStorage.")] public ItemDefinition item;
            [Min(0), Tooltip("Начальное количество и целевой остаток при пополнении. Пополнение добавляет только недостающее, не удаляет проданные игроком экземпляры.")] public int quantity;
            [Tooltip("Базовая цена одной единицы в целых рублях. Ноль разрешён явно; отрицательная цена запрещена.")] public long basePrice;
        }
        [SerializeField, Tooltip("Постоянный ID профиля. Не менять после создания сохраняемого торговца.")] private string profileId;
        [SerializeField, Tooltip("Название специализации для интерфейса.")] private string displayName;
        [SerializeField, Tooltip("Общие начальные товары и базовые цены; это не текущие остатки NPC.")] private Entry[] catalog = Array.Empty<Entry>();
        [SerializeField, Tooltip("Категории предметов для выкупа; каждому типу также нужна запись базовой цены.")] private ItemCategory[] acceptedCategories = Array.Empty<ItemCategory>();
        [SerializeField, Range(0, 10000), Tooltip("Цена продажи торговцем, % базовой. Итог учитывает прочность и патроны магазина.")] private int sellPercent = 100;
        [SerializeField, Range(0, 10000), Tooltip("Цена выкупа торговцем, процентов базовой; не должна превышать продажу.")] private int buyPercent = 50;
        public string Id => profileId;
        public string DisplayName => displayName;
        public IReadOnlyList<Entry> Catalog => catalog;
        public int SellPercent => sellPercent;
        public int BuyPercent => buyPercent;
        public event Action Changed;
        private void OnValidate() => Changed?.Invoke();
        public bool Accepts(ItemDefinition item) => item != null && acceptedCategories != null && Array.IndexOf(acceptedCategories, item.Category) >= 0;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(profileId) || profileId.Length > 64 || catalog == null || catalog.Length > 64
                || acceptedCategories == null || sellPercent < 0 || sellPercent > 10000 || buyPercent < 0 || buyPercent > sellPercent)
                throw new InvalidOperationException("Некорректный торговый профиль: " + name);
            var types = new HashSet<ItemDefinition>(); int required = 0;
            var categories = new HashSet<ItemCategory>();
            foreach (var category in acceptedCategories)
                if (!Enum.IsDefined(typeof(ItemCategory), category) || !categories.Add(category)) throw new InvalidOperationException("Неизвестная/повторная категория выкупа: " + name);
            foreach (var entry in catalog)
            {
                if (entry == null || entry.item == null || !types.Add(entry.item) || entry.quantity < 0 || entry.basePrice < 0)
                    throw new InvalidOperationException("Пустой/повторный товар, отрицательное количество или цена: " + name);
                checked { required += (entry.quantity / entry.item.MaxStack) + (entry.quantity % entry.item.MaxStack == 0 ? 0 : 1); }
                if (required > 64 || !TryScalePrice(entry.basePrice, sellPercent, out _) || !TryScalePrice(entry.basePrice, buyPercent, out _))
                    throw new InvalidOperationException("Начальный запас/цена превышает допустимый диапазон: " + name);
            }
        }
        public static bool TryScalePrice(long price, int percent, out long result)
        {
            result = 0; if (price < 0 || percent < 0 || percent > 10000) return false;
            // Divide first, preserving integer remainder, so a representable result does not overflow an intermediate multiply.
            try { result = checked((price / 100) * percent + (price % 100) * percent / 100); return true; }
            catch (OverflowException) { return false; }
        }
    }
}
