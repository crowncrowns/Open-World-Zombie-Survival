using UnityEngine;
using ZombieSurvival.AI;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController), typeof(CharacterController))]
    public sealed class PlayerNoiseEmitter : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float walkStepDistance = 1.8f;
        [SerializeField, Min(0.1f)] private float runStepDistance = 2.2f;
        [SerializeField, Min(0f)] private float walkLoudness = 1f;
        [SerializeField, Min(0f)] private float runLoudness = 3f;
        [SerializeField, Min(0f)] private float walkRange = 12f;
        [SerializeField, Min(0f)] private float runRange = 28f;
        private CharacterController body;
        private FirstPersonController movement;
        private IDamageable health;
        private Vector3 previousPosition;
        private float travelled;

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            movement = GetComponent<FirstPersonController>();
            var playerHealth = GetComponent<Health>();
            health = playerHealth != null ? (IDamageable)playerHealth : GetComponent<IDamageable>();
        }
        private void OnEnable() { previousPosition = transform.position; travelled = 0f; }

        private void LateUpdate()
        {
            Vector3 delta = transform.position - previousPosition;
            previousPosition = transform.position;
            delta.y = 0f;
            if ((health != null && health.IsDead) || !body.isGrounded || !movement.enabled || !movement.IsAlive
                || delta.sqrMagnitude < 0.000001f || delta.sqrMagnitude > 25f)
            {
                travelled = 0f;
                return;
            }
            travelled += delta.magnitude;
            bool running = movement.IsSprinting;
            if (travelled < (running ? runStepDistance : walkStepDistance)) return;
            travelled = 0f;
            GameplayNoise.Emit(transform.position, running ? runLoudness : walkLoudness,
                running ? runRange : walkRange, gameObject);
        }
    }
}
