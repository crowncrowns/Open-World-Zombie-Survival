using UnityEngine;

namespace ZombieSurvival.Inventory
{
    public enum ItemCategory { [InspectorName("Не назначено")] Uncategorized = 0, [InspectorName("Оружие")] Weapon = 10, [InspectorName("Патроны")] Ammunition = 20, [InspectorName("Снаряжение")] Equipment = 30, [InspectorName("Еда")] Food = 40, [InspectorName("Медицина")] Medicine = 50, [InspectorName("Материалы")] Material = 60 }
    public enum ItemSubcategory { [InspectorName("Не назначено")] Unspecified = 0, [InspectorName("Огнестрельное")] Firearm = 10, [InspectorName("Ближний бой")] Melee = 11, [InspectorName("Боеприпасы")] Ammunition = 20, [InspectorName("Рюкзак")] Backpack = 30, [InspectorName("Броня")] BodyArmor = 31, [InspectorName("Тактический жилет")] TacticalVest = 32, [InspectorName("Одежда")] Clothing = 33, [InspectorName("Продукты")] Food = 40, [InspectorName("Медицинские средства")] MedicalSupplies = 50, [InspectorName("Текстиль")] Textile = 60, [InspectorName("Оружейные детали")] WeaponParts = 61, [InspectorName("Бронематериалы")] ArmorMaterial = 62 }

    /// <summary>Shared type data; individual durability, quantity and magazine belong to ItemStack.</summary>
    [CreateAssetMenu(menuName = "Zombie Survival/Предмет", fileName = "NewItem")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Tooltip("Стабильный уникальный код типа для будущих сохранений. Не меняйте после выпуска игры.")]
        [SerializeField] private string itemId;
        [Tooltip("Название предмета в интерфейсе.")]
        [SerializeField] private string displayName = "Предмет";
        [Tooltip("Описание предмета. Употребление и крафт подключаются отдельными системами позднее.")]
        [SerializeField, TextArea] private string description;
        [Tooltip("Максимальное количество одинаковых ресурсов в слоте. Оружие и снаряжение всегда по одному.")]
        [SerializeField, Min(1)] private int maxStack = 10;
        [Tooltip("Внешний вид предмета в мире. Нужны Collider и WorldItem, Rigidbody необязателен.")]
        [SerializeField] private GameObject worldPrefab;
        [Tooltip("Настройки оружия. Пусто для остальных типов.")]
        [SerializeField] private ZombieSurvival.Weapons.WeaponDefinition weapon;
        [SerializeField, Tooltip("Основная категория типа. Числовые ID enum стабильны, не перенумеровывать.")]
        private ItemCategory category;
        [SerializeField, Tooltip("Подкатегория типа; не определяет эффекты экипировки или рецепты автоматически.")]
        private ItemSubcategory subcategory;
        [SerializeField, Tooltip("Разрешён ли будущий ремонт этого типа. Сам ремонт на Т2 не реализован.")]
        private bool repairable;
        [SerializeField, Tooltip("Стабильный ID будущего ремонтного профиля (weapon/textile/armor); это не рецепт и не цена.")]
        private string repairProfileId;
        [SerializeField, Min(.01f), Tooltip("Максимальная прочность нового оружия/снаряжения, ед. Текущее значение хранится в экземпляре.")]
        private float maximumDurability = 100f;
        public ZombieSurvival.Weapons.WeaponDefinition Weapon => weapon;
        private bool warnedUncategorized;
        internal void WarnIfUncategorized()
        {
            if (!warnedUncategorized && Category == ItemCategory.Uncategorized)
            { warnedUncategorized = true; Debug.LogWarning("ItemDefinition: категория не назначена; тип остаётся Uncategorized без угадывания по названию: " + itemId, this); }
        }
        public string CategoryLabel => EnumLabel(Category);
        public string SubcategoryLabel => EnumLabel(Subcategory);
        private static string EnumLabel<T>(T value) where T : struct
        {
            var field = typeof(T).GetField(value.ToString());
            var attribute = field != null ? System.Attribute.GetCustomAttribute(field, typeof(InspectorNameAttribute)) as InspectorNameAttribute : null;
            return attribute != null ? attribute.displayName : value.ToString();
        }
        public string Id => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        // Legacy weapon data has a reliable typed reference; never infer categories from names.
        public ItemCategory Category => weapon != null ? ItemCategory.Weapon : category;
        public ItemSubcategory Subcategory => weapon != null ? (weapon.kind == ZombieSurvival.Weapons.WeaponKind.Firearm ? ItemSubcategory.Firearm : ItemSubcategory.Melee) : subcategory;
        public bool IsIndividual => Category == ItemCategory.Weapon || Category == ItemCategory.Equipment;
        public bool HasDurability => IsIndividual;
        public bool Repairable => HasDurability && repairable;
        public string RepairProfileId => repairProfileId;
        public float MaximumDurability => HasDurability ? (float.IsNaN(maximumDurability) || float.IsInfinity(maximumDurability) ? 100f : Mathf.Max(.01f, maximumDurability)) : 0f;
        public int MaxStack => IsIndividual ? 1 : Mathf.Max(1, maxStack);
        public GameObject WorldPrefab => worldPrefab;
    }
}
