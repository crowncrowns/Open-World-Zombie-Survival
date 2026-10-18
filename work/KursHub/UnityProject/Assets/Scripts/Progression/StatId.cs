using UnityEngine;

namespace ZombieSurvival.Progression
{
    // Числа сохраняются в ассетах. Новые характеристики добавлять с новым ID,
    // а затем подключать Evaluate в компоненте, который действительно использует величину.
    public enum StatId
    {
        [InspectorName("Максимум здоровья, HP")] HealthMaximum = 0,
        [InspectorName("Максимум выносливости, ед.")] StaminaCapacity = 10,
        [InspectorName("Восстановление выносливости, ед/с")] StaminaRecovery = 11,
        [InspectorName("Скорость ходьбы, м/с")] WalkSpeed = 20,
        [InspectorName("Скорость бега, м/с")] RunSpeed = 21,
        [InspectorName("Высота прыжка, м")] JumpHeight = 22,
        [InspectorName("Урон ближнего оружия, HP")] MeleeDamage = 30,
        [InspectorName("Урон огнестрельного оружия, HP")] FirearmDamage = 31,
        [InspectorName("Лечение предметами, HP")] HealingPower = 40
    }
}
