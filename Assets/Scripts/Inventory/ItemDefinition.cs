using UnityEngine;

namespace ZombieSurvival.Inventory
{
    /// <summary>Общий неизменяемый тип предмета. Количество и ID стопки хранятся в ItemStack.</summary>
    [CreateAssetMenu(menuName = "Zombie Survival/Предмет", fileName = "NewItem")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Tooltip("Стабильный уникальный код типа для будущих сохранений. Не меняйте после выпуска игры.")]
        [SerializeField] private string itemId;
        [Tooltip("Название предмета в интерфейсе.")]
        [SerializeField] private string displayName = "Предмет";
        [Tooltip("Описание предмета. Употребление и крафт подключаются отдельными системами позднее.")]
        [SerializeField, TextArea] private string description;
        [Tooltip("Максимальное количество одинаковых предметов в одном слоте.")]
        [SerializeField, Min(1)] private int maxStack = 10;
        [Tooltip("Внешний вид предмета в мире. Нужны Collider и WorldItem, а Rigidbody необязателен.")]
        [SerializeField] private GameObject worldPrefab;
        [Tooltip("Настройки оружия. Пусто для ресурсов; оружие всегда занимает отдельный слот.")]
        [SerializeField] private ZombieSurvival.Weapons.WeaponDefinition weapon;
        public ZombieSurvival.Weapons.WeaponDefinition Weapon => weapon;
        public string Id => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public int MaxStack => weapon != null ? 1 : Mathf.Max(1, maxStack);
        public GameObject WorldPrefab => worldPrefab;
    }
}
