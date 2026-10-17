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
        public bool IsAlive => isActiveAndEnabled && (damageable == null || damageable.IsAlive);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => targets.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreTargets()
        {
            targets.Clear();
            foreach (var target in Object.FindObjectsByType<CombatTarget>(FindObjectsInactive.Include))
            {
                target.damageable = target.GetComponent<IDamageable>();
                if (target.isActiveAndEnabled) targets.Add(target);
            }
        }
        private void Awake() => damageable = GetComponent<IDamageable>();
        private void OnEnable() => targets.Add(this);
        private void OnDisable() => targets.Remove(this);
        public void ReceiveDamage(float amount) => damageable?.TakeDamage(amount);
    }
}
