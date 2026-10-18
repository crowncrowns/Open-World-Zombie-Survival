using UnityEngine;
using ZombieSurvival.Interaction;
using ZombieSurvival.Progression;

namespace ZombieSurvival.Player
{
    /// <summary>Только полигон: выдача опыта проверяет обычные правила уровней, не меняет Inspector-базу.</summary>
    public sealed class ProgressionTestConsole : Interactable
    {
        [SerializeField, InspectorName("Опыт за нажатие"), Min(1), Tooltip("Количество опыта за одно E на тестовой консоли, ед. Не источник наград в обычной карте.")]
        private int experience = 100;
        public override string GetPrompt(PlayerInteractor actor) => "Тест развития: получить " + experience + " опыта";
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            var progression = actor.GetComponent<PlayerProgression>();
            if (progression == null) return false;
            progression.AddExperience(experience); actor.ShowFeedback("Опыт получен. K — открыть дерево развития"); return true;
        }
    }
}
