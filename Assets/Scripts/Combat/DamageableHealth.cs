using UnityEngine;
using UnityEngine.Events;

namespace ZombieSurvival.Combat
{
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
        public bool IsDead => !IsAlive;
        private void Awake() => currentHealth = maximumHealth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetHealthForPlay()
        {
            foreach (var health in Object.FindObjectsByType<DamageableHealth>(FindObjectsInactive.Include))
                health.currentHealth = health.maximumHealth;
        }

        // Preserve UnityEvent/legacy callers while sharing the same contract as player Health.
        public void TakeDamage(float amount) => ApplyDamage(new DamageInfo(amount, null, transform.position));

        public bool ApplyDamage(DamageInfo damage)
        {
            float amount = damage.Amount;
            if (!isActiveAndEnabled || !IsAlive || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            onDamaged.Invoke();
            if (!IsAlive) onDeath.Invoke();
            return true;
        }
    }
}
