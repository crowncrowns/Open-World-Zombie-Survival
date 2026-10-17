using UnityEngine;

namespace ZombieSurvival.Combat
{
    [DisallowMultipleComponent]
    public sealed class MeleeAttack : EnemyAttack
    {
        [SerializeField, Min(0f)] private float damage = 10f;
        protected override void ApplyEffect(CombatTarget target)
            => target.ReceiveDamage(new DamageInfo(damage, gameObject, target.AimPosition, DamageKind.Attack));
    }
}
