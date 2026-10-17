using UnityEngine;
using UnityEngine.Events;

namespace ZombieSurvival.Combat
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount);
    }

    [DisallowMultipleComponent]
    public sealed class DamageableHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField] private UnityEvent onDamaged = new UnityEvent();
        [SerializeField] private UnityEvent onDeath = new UnityEvent();
        [SerializeField, Tooltip("Runtime value; initialized from Maximum Health in Awake.")]
        private float currentHealth;

        public float CurrentHealth => currentHealth;
        public bool IsAlive => currentHealth > 0f;
        private void Awake() => currentHealth = maximumHealth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetHealthForPlay()
        {
            foreach (var health in Object.FindObjectsByType<DamageableHealth>(FindObjectsInactive.Include))
                health.currentHealth = health.maximumHealth;
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f || float.IsNaN(amount)) return;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            onDamaged.Invoke();
            if (!IsAlive) onDeath.Invoke();
        }
    }
}
