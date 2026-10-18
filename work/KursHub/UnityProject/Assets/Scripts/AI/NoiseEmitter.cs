using UnityEngine;

namespace ZombieSurvival.AI
{
    /// <summary>Call Emit from gameplay, an animation event, or a UnityEvent.</summary>
    [DisallowMultipleComponent]
    public sealed class NoiseEmitter : MonoBehaviour
    {
        [Tooltip("Исходная громкость игрового события слуха. Ослабляется с расстоянием и препятствиями; AudioSource не требуется.")]
        [SerializeField, Min(0f)] private float loudness = 5f;
        [Tooltip("Предельная дистанция действия, м. Для атаки измеряется от точки удара до цели; для шума — радиус слышимости.")]
        [SerializeField, Min(0f)] private float range = 60f;

        public void Emit() => GameplayNoise.Emit(transform.position, loudness, range, gameObject);
    }
}
