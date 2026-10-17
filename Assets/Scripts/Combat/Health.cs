using System;
using UnityEngine;
using UnityEngine.Events;

namespace ZombieSurvival.Combat
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maximum = 100f;
        [SerializeField, Range(.01f, 1f)] private float startingFraction = 1f;
        [SerializeField] private bool invincible;
        [Header("Events")]
        [SerializeField] private UnityEvent<float> onHealthChanged = new UnityEvent<float>();
        [SerializeField] private UnityEvent onDied = new UnityEvent();
        [SerializeField] private UnityEvent onRevived = new UnityEvent();
        private float current, protectedUntil = float.NegativeInfinity;
        public float Current => current;
        public float Maximum => Mathf.Max(1f, maximum);
        public float Normalized => Mathf.Clamp01(current / Maximum);
        public bool IsDead { get; private set; }
        public bool IsProtected => invincible || Time.time < protectedUntil;
        public DamageInfo LastDamage { get; private set; }
        public event Action Changed;
        public event Action<DamageInfo> Damaged;
        public event Action Died;
        public event Action Revived;

        private void Awake() { current = Maximum * startingFraction; }

        public bool ApplyDamage(DamageInfo damage)
        {
            if (!isActiveAndEnabled || IsDead || IsProtected || !ValidAmount(damage.Amount)) return false;
            current = Mathf.Max(0f, current - damage.Amount);
            LastDamage = damage;
            bool lethal = current <= 0f;
            // Mark dead before notifying listeners, so overlapping hits cannot kill twice.
            if (lethal) IsDead = true;
            Changed?.Invoke(); onHealthChanged.Invoke(current); Damaged?.Invoke(damage);
            if (lethal) { Died?.Invoke(); onDied.Invoke(); }
            return true;
        }

        public float Heal(float amount)
        {
            if (!isActiveAndEnabled || IsDead || !ValidAmount(amount)) return 0f;
            float restored = Mathf.Min(amount, Mathf.Max(0f, Maximum - current));
            if (restored <= 0f) return 0f;
            current += restored;
            Changed?.Invoke(); onHealthChanged.Invoke(current);
            return restored;
        }

        /// <summary>Explicit lifecycle operation; healing never resurrects a dead character.</summary>
        public void Revive(float fraction = 1f, float protectionSeconds = 0f)
        {
            current = Maximum * Mathf.Clamp(fraction, .01f, 1f);
            IsDead = false;
            protectedUntil = Time.time + Mathf.Max(0f, protectionSeconds);
            LastDamage = default;
            Changed?.Invoke(); onHealthChanged.Invoke(current); Revived?.Invoke(); onRevived.Invoke();
        }

        private static bool ValidAmount(float amount) => amount > 0f && !float.IsNaN(amount) && !float.IsInfinity(amount);
    }
}
