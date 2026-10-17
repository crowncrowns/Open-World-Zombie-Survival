using System.Collections.Generic;
using UnityEngine;

namespace ZombieSurvival.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatTarget : MonoBehaviour
    {
        private static readonly HashSet<CombatTarget> targets = new HashSet<CombatTarget>();
        [SerializeField] private Vector3 aimOffset = new Vector3(0f, 1f, 0f);
        private IDamageable damageable;

        public static IEnumerable<CombatTarget> ActiveTargets => targets;
        public Vector3 AimPosition => transform.TransformPoint(aimOffset);
        public bool IsAlive => isActiveAndEnabled && (damageable == null || !damageable.IsDead);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => targets.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreTargets()
        {
            targets.Clear();
            foreach (var target in Object.FindObjectsByType<CombatTarget>(FindObjectsInactive.Include))
            {
                target.ResolveHealth();
                if (target.isActiveAndEnabled) targets.Add(target);
            }
        }
        private void ResolveHealth()
        {
            // Prefer the player's full health/lifecycle component if a merged scene contains both.
            var health = GetComponent<Health>();
            damageable = health != null ? (IDamageable)health : GetComponent<IDamageable>();
        }
        private void Awake() => ResolveHealth();
        private void OnEnable() => targets.Add(this);
        private void OnDisable() => targets.Remove(this);
        public void ReceiveDamage(float amount) => ReceiveDamage(new DamageInfo(amount, null, AimPosition));
        public void ReceiveDamage(DamageInfo damage) => damageable?.ApplyDamage(damage);
    }
}
