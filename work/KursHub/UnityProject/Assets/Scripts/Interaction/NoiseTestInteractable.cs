using UnityEngine;
using ZombieSurvival.AI;

namespace ZombieSurvival.Interaction
{
    public sealed class NoiseTestInteractable : Interactable
    {
        [Tooltip("Исходная громкость игрового события слуха. Ослабляется с расстоянием и препятствиями; AudioSource не требуется.")]
        [SerializeField, Min(0f)] private float loudness = 10f;
        [Tooltip("Предельная дистанция действия, м. Для атаки измеряется от точки удара до цели; для шума — радиус слышимости.")]
        [SerializeField, Min(0f)] private float range = 35f;
        public override string GetPrompt(PlayerInteractor actor) => "Создать отвлекающий шум";
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            GameplayNoise.Emit(transform.position, loudness, range, gameObject, NoiseKind.Generic);
            actor.ShowFeedback("Создан отвлекающий шум");
            return true;
        }
    }
}
