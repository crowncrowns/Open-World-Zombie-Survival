using UnityEngine;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Interaction
{
    public sealed class HealthTestInteractable : Interactable
    {
        [Tooltip("Включено — лечить, выключено — наносить тестовый урон.")]
        [SerializeField] private bool healing;
        [Tooltip("Количество здоровья для тестового лечения или урона.")]
        [SerializeField, Min(.01f)] private float amount = 25f;
        [Tooltip("Текст предлагаемого действия в HUD.")]
        [SerializeField] private string prompt = "Получить тестовый урон";
        public override string GetPrompt(PlayerInteractor actor) => prompt;
        public override bool CanInteract(PlayerInteractor actor)
        {
            var health = actor != null ? actor.GetComponent<Health>() : null;
            return base.CanInteract(actor) && health != null && !health.IsDead;
        }
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            var health = actor.GetComponent<Health>();
            bool success = healing ? health.Heal(amount) > 0f
                : health.ApplyDamage(new DamageInfo(amount, gameObject, transform.position, DamageKind.Environment));
            actor.ShowFeedback(success ? healing ? "Здоровье восстановлено" : "Получен урон" : healing ? "Здоровье полное" : "Урон не прошёл");
            return success;
        }
    }
}
