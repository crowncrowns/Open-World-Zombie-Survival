using UnityEngine;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(FirstPersonController))]
    public sealed class PlayerFallDamage : MonoBehaviour
    {
        [Tooltip("Скорость падения, ниже которой приземление не наносит урон, м/с.")]
        [SerializeField, Min(0f)] private float safeLandingSpeed = 10f;
        [Tooltip("Урон за каждый м/с скорости падения сверх безопасного порога.")]
        [SerializeField, Min(0f)] private float damagePerExcessSpeed = 10f;
        [Tooltip("Множитель урона при успешном перекате после падения: 0 — без урона, 1 — без снижения.")]
        [SerializeField, Range(0f, 1f)] private float landingRollMultiplier = .35f;
        private FirstPersonController motor;
        private Health health;
        private void Awake() { motor = GetComponent<FirstPersonController>(); health = GetComponent<Health>(); }
        private void OnEnable() => motor.Landed += OnLanded;
        private void OnDisable() => motor.Landed -= OnLanded;
        private void OnLanded(float impactSpeed)
        {
            float damage = Mathf.Max(0f, impactSpeed - safeLandingSpeed) * damagePerExcessSpeed;
            if (motor.Parkour != null && motor.Parkour.State == PlayerParkour.TraversalState.Rolling) damage *= landingRollMultiplier;
            health.ApplyDamage(new DamageInfo(damage, gameObject, motor.FeetPosition, DamageKind.Fall));
        }
    }
}
