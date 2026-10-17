using UnityEngine;

namespace ZombieSurvival.Audio
{
    /// <summary>Необязательная замена шагов на конкретной поверхности. Не меняет голос, здоровье или AI.</summary>
    public sealed class FootstepSurface : MonoBehaviour
    {
        [Tooltip("Набор шагов этой поверхности. Назначается на пол или родителя его коллайдеров. Без него используются звуки актёра.")]
        [SerializeField] private ActorAudioProfile profile;
        public ActorAudioProfile Profile => profile;
    }
}
