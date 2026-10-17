using UnityEngine;

namespace ZombieSurvival.Combat
{
    public enum DamageKind { Attack, Environment, Fall }

    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Source;
        public readonly Vector3 Point;
        public readonly DamageKind Kind;
        public DamageInfo(float amount, GameObject source, Vector3 point, DamageKind kind = DamageKind.Attack)
        { Amount = amount; Source = source; Point = point; Kind = kind; }
    }

    public interface IDamageable
    {
        bool IsDead { get; }
        bool ApplyDamage(DamageInfo damage);
    }
}
