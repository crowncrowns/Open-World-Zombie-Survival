using System.Collections.Generic;
using UnityEngine;

namespace ZombieSurvival.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatTarget : MonoBehaviour
    {
        private static readonly HashSet<CombatTarget> targets = new HashSet<CombatTarget>();
        [Tooltip("Локальная точка прицеливания, если не включено следование за коллайдером.")]
        [SerializeField] private Vector3 aimOffset = new Vector3(0f, 1f, 0f);
        [Tooltip("Брать точку видимости из фактической капсулы: приседание и ползание позволяют спрятаться за низким укрытием.")]
        [SerializeField] private bool useColliderCenter;
        [Tooltip("Коллайдер фактической позы для вычисления точки обнаружения и попадания.")]
        [SerializeField] private Collider aimCollider;
        [Tooltip("Высота точки обнаружения внутри границ коллайдера: 0 — низ, 1 — верх.")]
        [SerializeField, Range(0f, 1f)] private float aimHeightFraction = .85f;
        private IDamageable damageable;
        private ActorIdentity identity;

        public static IEnumerable<CombatTarget> ActiveTargets => targets;
        public Vector3 AimPosition
        {
            get
            {
                if (!useColliderCenter || aimCollider == null) return transform.TransformPoint(aimOffset);
                var bounds = aimCollider.bounds;var point = bounds.center;
                point.y = Mathf.Lerp(bounds.min.y, bounds.max.y, aimHeightFraction);
                return point;
            }
        }
        public bool IsAlive => isActiveAndEnabled && DamageReceiver.IsAlive(damageable);
        public bool CanBeTargetedBy(Transform attacker)
        {
            if (!IsAlive || attacker == null || ActorIdentity.Root(attacker) == ActorIdentity.Root(transform)) return false;
            var source = attacker.GetComponentInParent<ActorIdentity>();
            return source == null || source.IsHostileTo(identity);
        }

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
            damageable = DamageReceiver.Find(this);
            identity = GetComponentInParent<ActorIdentity>();
            if (aimCollider == null) aimCollider = GetComponent<Collider>();
        }
        private void Awake() => ResolveHealth();
        private void OnEnable() => targets.Add(this);
        private void OnDisable() => targets.Remove(this);
        public void ReceiveDamage(float amount) => ReceiveDamage(new DamageInfo(amount, null, AimPosition));
        public void ReceiveDamage(DamageInfo damage) => damageable?.ApplyDamage(damage);
    }
}
