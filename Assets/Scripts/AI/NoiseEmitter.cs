using UnityEngine;

namespace ZombieSurvival.AI
{
    /// <summary>Call Emit from gameplay, an animation event, or a UnityEvent.</summary>
    [DisallowMultipleComponent]
    public sealed class NoiseEmitter : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float loudness = 5f;
        [SerializeField, Min(0f)] private float range = 60f;

        public void Emit() => GameplayNoise.Emit(transform.position, loudness, range, gameObject);
    }
}
