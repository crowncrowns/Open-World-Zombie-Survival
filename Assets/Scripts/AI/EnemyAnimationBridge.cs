using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using ZombieSurvival.Combat;

namespace ZombieSurvival.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyAnimationBridge : MonoBehaviour
    {
        [Tooltip("Длина исходного клипа атаки, с. Используется для подгонки анимации под подготовку и восстановление удара.")]
        [SerializeField, Min(0.01f)] private float attackClipLength = 1f;
        [Tooltip("Время сглаживания скорости в Animator, с.")]
        [SerializeField, Min(0f)] private float speedDamping = 0.1f;
        [Tooltip("Animator Humanoid-модели. Без явной ссылки ищется на модели актёра.")]
        [SerializeField] private Animator modelAnimator;
        [Header("Необязательные параметры Animator")]
        [Tooltip("Имя параметра Animator для скорости движения. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string speedParameter = "Speed";
        [Tooltip("Имя параметра Animator для запуска удара (Trigger). Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string attackParameter = "Attack";
        [Tooltip("Имя параметра Animator для продолжения атаки (Bool). Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string attackingParameter = "Attacking";
        [Tooltip("Имя параметра Animator для скорости клипа атаки. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string attackPlaybackParameter = "AttackPlayback";
        [Tooltip("Имя параметра Animator для смерти. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string deadParameter = "Dead";
        private int Speed, Attack, Attacking, AttackPlayback, Dead;
        private readonly Dictionary<int, AnimatorControllerParameterType> parameters = new Dictionary<int, AnimatorControllerParameterType>();
        private Animator animator;
        private NavMeshAgent agent;
        private EnemyAttack[] attacks;
        private EnemyAttack activeAttack;
        private IDamageable health;

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
            animator = modelAnimator != null ? modelAnimator : GetComponentInChildren<Animator>();
            agent = GetComponent<NavMeshAgent>();
            health = DamageReceiver.Find(this);
            if (animator == null) return;
            animator.applyRootMotion = false;
            activeAttack = null;
            attacks = GetComponents<EnemyAttack>();
            foreach (var attack in attacks)
            {
                attack.AnimationStarted += BeginAttack;
                attack.AttackFinished += FinishAttack;
            }
            if (animator.runtimeAnimatorController == null) return;
            Speed = Animator.StringToHash(speedParameter); Attack = Animator.StringToHash(attackParameter);
            Attacking = Animator.StringToHash(attackingParameter); AttackPlayback = Animator.StringToHash(attackPlaybackParameter); Dead = Animator.StringToHash(deadParameter);
            parameters.Clear();foreach (var parameter in animator.parameters) parameters[parameter.nameHash] = parameter.type;
            Bool(Dead, false);if (Has(Attack, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(Attack);
            Bool(Attacking, false);Float(Speed, 0f);Float(AttackPlayback, 1f);
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
            if (animator == null || animator.runtimeAnimatorController == null) return;
            bool alive = DamageReceiver.IsAlive(health);
            Bool(Dead, !alive);
            if (!alive)
            {
                Bool(Attacking, false);Float(Speed, 0f);
                return;
            }
            Vector3 velocity = agent.enabled && agent.isOnNavMesh ? agent.velocity : Vector3.zero;
            velocity.y = 0f;
            if (Has(Speed, AnimatorControllerParameterType.Float)) animator.SetFloat(Speed, velocity.magnitude, speedDamping, Time.deltaTime);
        }

        private void BeginAttack(EnemyAttack attack)
        {
            if (animator == null || animator.runtimeAnimatorController == null || !DamageReceiver.IsAlive(health)) return;
            activeAttack = attack;
            // Animation starts after StopDelay. One cycle spans Windup + Recovery;
            // the hit pose must be authored at Windup / (Windup + Recovery).
            float duration = Mathf.Max(0.01f, attack.Windup + attack.Recovery);
            Float(AttackPlayback, attackClipLength / duration);Bool(Attacking, true);
            if (Has(Attack, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(Attack);
        }

        private void FinishAttack(EnemyAttack attack)
        {
            if (animator == null || attack != activeAttack || animator.runtimeAnimatorController == null) return;
            activeAttack = null;
            Bool(Attacking, false);
        }
        private bool Has(int hash, AnimatorControllerParameterType type) => parameters.TryGetValue(hash, out var actual) && actual == type;
        private void Float(int hash, float value) { if (Has(hash, AnimatorControllerParameterType.Float)) animator.SetFloat(hash, value); }
        private void Bool(int hash, bool value) { if (Has(hash, AnimatorControllerParameterType.Bool)) animator.SetBool(hash, value); }
    }
}
