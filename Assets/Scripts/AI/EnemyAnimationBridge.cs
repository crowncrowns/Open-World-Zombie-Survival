using UnityEngine;
using UnityEngine.AI;
using ZombieSurvival.Combat;

namespace ZombieSurvival.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator), typeof(NavMeshAgent))]
    public sealed class EnemyAnimationBridge : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float attackClipLength = 1f;
        [SerializeField, Min(0f)] private float speedDamping = 0.1f;
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Attacking = Animator.StringToHash("Attacking");
        private static readonly int AttackPlayback = Animator.StringToHash("AttackPlayback");
        private Animator animator;
        private NavMeshAgent agent;
        private EnemyAttack[] attacks;
        private EnemyAttack activeAttack;

        private void OnEnable() => Initialize();
        private void OnDisable() => Unsubscribe();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreBridgesForPlay()
        {
            foreach (var bridge in Object.FindObjectsByType<EnemyAnimationBridge>(FindObjectsInactive.Include))
                if (bridge.isActiveAndEnabled) bridge.Initialize();
        }

        private void Initialize()
        {
            Unsubscribe();
            animator = GetComponent<Animator>();
            agent = GetComponent<NavMeshAgent>();
            animator.applyRootMotion = false;
            activeAttack = null;
            attacks = GetComponents<EnemyAttack>();
            foreach (var attack in attacks)
            {
                attack.AnimationStarted += BeginAttack;
                attack.AttackFinished += FinishAttack;
            }
            if (animator.runtimeAnimatorController == null) return;
            animator.ResetTrigger(Attack);
            animator.SetBool(Attacking, false);
            animator.SetFloat(Speed, 0f);
            animator.SetFloat(AttackPlayback, 1f);
        }

        private void Unsubscribe()
        {
            if (attacks == null) return;
            foreach (var attack in attacks)
            {
                if (attack == null) continue;
                attack.AnimationStarted -= BeginAttack;
                attack.AttackFinished -= FinishAttack;
            }
        }

        private void Update()
        {
            if (animator.runtimeAnimatorController == null) return;
            Vector3 velocity = agent.enabled && agent.isOnNavMesh ? agent.velocity : Vector3.zero;
            velocity.y = 0f;
            animator.SetFloat(Speed, velocity.magnitude, speedDamping, Time.deltaTime);
        }

        private void BeginAttack(EnemyAttack attack)
        {
            if (animator.runtimeAnimatorController == null) return;
            activeAttack = attack;
            // Animation starts after StopDelay. One cycle spans Windup + Recovery;
            // the hit pose must be authored at Windup / (Windup + Recovery).
            float duration = Mathf.Max(0.01f, attack.Windup + attack.Recovery);
            animator.SetFloat(AttackPlayback, attackClipLength / duration);
            animator.SetBool(Attacking, true);
            animator.SetTrigger(Attack);
        }

        private void FinishAttack(EnemyAttack attack)
        {
            if (attack != activeAttack || animator.runtimeAnimatorController == null) return;
            activeAttack = null;
            animator.SetBool(Attacking, false);
        }
    }
}
