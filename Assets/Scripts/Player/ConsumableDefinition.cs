using UnityEngine;
using ZombieSurvival.Inventory;

namespace ZombieSurvival.Player
{
    /// <summary>Адаптер потребления поверх типа предмета. Не меняет категории/прочность/цены второго разработчика.</summary>
    [CreateAssetMenu(menuName="Zombie Survival/Еда и напиток",fileName="Consumable")]
    public sealed class ConsumableDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Тип предмета рюкзака. Одна единица расходуется только при полезном эффекте.")]
        private ItemDefinition item;
        [SerializeField, Min(0), Tooltip("Восстановление сытости за одну единицу, ед.")]
        private float food = 25;
        [SerializeField, Min(0), Tooltip("Восстановление воды за одну единицу, ед.")]
        private float water;
        [SerializeField, Min(0), Tooltip("Восстановление здоровья за одну единицу, HP. Не воскрешает.")]
        private float healing;
        public ItemDefinition Item => item;
        private static float Safe(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : Mathf.Max(0,v);
        public float Food => Safe(food);
        public float Water => Safe(water);
        public float Healing => Safe(healing);
    }
}
