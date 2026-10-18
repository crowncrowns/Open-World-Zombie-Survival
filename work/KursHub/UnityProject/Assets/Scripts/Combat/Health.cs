using System;
using UnityEngine;
using UnityEngine.Events;
using ZombieSurvival.Progression;

namespace ZombieSurvival.Combat
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [Header("Здоровье")]
        [Tooltip("Максимальный запас здоровья.")]
        [SerializeField, Min(1f)] private float maximum = 100f;
        [Tooltip("Начальная доля максимального запаса от 0 до 1.")]
        [SerializeField, Range(.01f, 1f)] private float startingFraction = 1f;
        [Tooltip("Неуязвимость: игнорировать входящий урон.")]
        [SerializeField] private bool invincible;
        [Header("События")]
        [Tooltip("Событие изменения здоровья, передаёт текущее значение.")]
        [SerializeField] private UnityEvent<float> onHealthChanged = new UnityEvent<float>();
        [Tooltip("Событие смерти, вызывается один раз до возрождения.")]
        [SerializeField] private UnityEvent onDied = new UnityEvent();
        [Tooltip("Событие явного возрождения актёра.")]
        [SerializeField] private UnityEvent onRevived = new UnityEvent();
        private float current, protectedUntil = float.NegativeInfinity;
        private PlayerStatModifiers statModifiers;
        private bool initialized;
        public float Current { get { EnsureInitialized(); return current; } }
        public float BaseMaximum => Mathf.Max(1f, maximum);
        public float Maximum
        {
            get
            {
                if (statModifiers == null) statModifiers = GetComponent<PlayerStatModifiers>();
                return Mathf.Max(1f, statModifiers != null ? statModifiers.Evaluate(StatId.HealthMaximum, BaseMaximum) : BaseMaximum);
            }
        }
        public float Normalized => Mathf.Clamp01(current / Maximum);
        public bool IsDead { get; private set; }
        public bool IsProtected => invincible || Time.time < protectedUntil;
        public DamageInfo LastDamage { get; private set; }
        public event Action Changed;
        public event Action<DamageInfo> Damaged;
        public event Action Died;
        public event Action Revived;
        public event Action WorldStateRestored;
        // Общее событие канонического Health: награда привязана к источнику последнего смертельного удара,
        // а не к имени модели, конкретному EnemyBrain или опросу всех NPC каждый кадр.
        public static event Action<Health, DamageInfo> ActorKilled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents() => ActorKilled = null;

        private void EnsureInitialized() { if (initialized) return; initialized = true; current = Maximum * startingFraction; }
        private void Awake() => EnsureInitialized();

        public bool ApplyDamage(DamageInfo damage)
        {
            if (!isActiveAndEnabled || IsDead || IsProtected || !ValidAmount(damage.Amount)) return false;
            current = Mathf.Max(0f, current - damage.Amount);
            LastDamage = damage;
            bool lethal = current <= 0f;
            // Mark dead before notifying listeners, so overlapping hits cannot kill twice.
            if (lethal) IsDead = true;
            Changed?.Invoke(); onHealthChanged.Invoke(current); Damaged?.Invoke(damage);
            if (lethal) { Died?.Invoke(); onDied.Invoke(); ActorKilled?.Invoke(this, damage); }
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
            initialized = true;
            current = Maximum * Mathf.Clamp(fraction, .01f, 1f);
            IsDead = false;
            protectedUntil = Time.time + Mathf.Max(0f, protectionSeconds);
            LastDamage = default;
            Changed?.Invoke(); onHealthChanged.Invoke(current); Revived?.Invoke(); onRevived.Invoke();
        }

        // Player-only alive snapshot restore; unlike Revive's fraction this preserves sub-1 HP exactly.
        internal void RestoreAlive(float value)
        {
            initialized = true;
            current = Mathf.Clamp(value, .0001f, Maximum); IsDead = false;
            protectedUntil = float.NegativeInfinity; LastDamage = default;
            Changed?.Invoke(); onHealthChanged.Invoke(current); Revived?.Invoke(); onRevived.Invoke();
        }
        // Загрузка мира не является ударом/убийством: XP и Damaged не воспроизводятся.
        internal void RestoreWorldStateSilently(float value, bool dead)
        {
            initialized = true; current = value; IsDead = dead;
            protectedUntil = float.NegativeInfinity; LastDamage = default;
        }
        internal void NotifyWorldRestore()
        {
            Changed?.Invoke(); onHealthChanged.Invoke(current);
            // Died/Revived и UnityEvent могли выдавать добычу: загрузка не повторяет игровые последствия.
            WorldStateRestored?.Invoke();
        }
        /// <summary>Смена максимума сохраняет абсолютное HP: покупка/повторная загрузка не лечит бесплатно.</summary>
        public void RefreshDerivedMaximum() => RefreshDerivedMaximum(true);
        internal void RefreshDerivedMaximum(bool notify)
        {
            current = Mathf.Clamp(current, 0f, Maximum);
            if(notify){Changed?.Invoke(); onHealthChanged.Invoke(current);}
        }
        private static bool ValidAmount(float amount) => amount > 0f && !float.IsNaN(amount) && !float.IsInfinity(amount);
    }
}
