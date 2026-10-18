using UnityEngine;

namespace ZombieSurvival.AI
{
    /// <summary>Humanoid crawl cycle: low torso, alternating hand/foot reaches and planted pauses.
    /// Called by the model's IK bridge, so changing Humanoid models preserves the animation.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class HumanoidCrawlPose : MonoBehaviour
    {
        [Tooltip("Включить процедурную анимацию ползания Humanoid. На физику и путь не влияет.")]
        [SerializeField] private bool animationEnabled = true;
        [Tooltip("Скорость плавного перехода между обычной позой и ползанием.")]
        [SerializeField, Min(.1f)] private float blendSpeed = 8f;
        [Tooltip("Пройденное расстояние на полный цикл движения рук и ног, м.")]
        [SerializeField, Min(.1f)] private float metresPerCycle = 1.3f;
        [Header("Тело")]
        [Tooltip("Высота таза над землёй в позе ползания, м.")]
        [SerializeField, Min(.1f)] private float bodyHeight = .42f;
        [Tooltip("Смещение таза вперёд от корня в ползании, м. Отрицательное значение смещает назад.")]
        [SerializeField] private float bodyForwardOffset = -.08f;
        [Tooltip("Наклон тела вперёд при ползании, градусов.")]
        [SerializeField, Range(45f, 100f)] private float bodyPitch = 82f;
        [Tooltip("Амплитуда покачивания тела при ползании, м.")]
        [SerializeField, Min(0f)] private float bodyBob = .018f;
        [Header("Руки")]
        [Tooltip("Положение кистей впереди корня при ползании, м.")]
        [SerializeField] private float handForward = .64f;
        [Tooltip("Боковое расстояние кисти от оси тела при ползании, м.")]
        [SerializeField, Min(0f)] private float handWidth = .28f;
        [Tooltip("Высота кистей над землёй при ползании, м.")]
        [SerializeField, Min(0f)] private float handHeight = .12f;
        [Tooltip("Амплитуда движения рук в направлении ползания, м.")]
        [SerializeField, Min(0f)] private float reach = .18f;
        [Tooltip("Высота подъёма переносимой руки, м.")]
        [SerializeField, Min(0f)] private float handLift = .07f;
        [Header("Стопы и колени")]
        [Tooltip("Положение стоп вдоль тела относительно корня, м. Отрицательное — позади.")]
        [SerializeField] private float footForward = -.65f;
        [Tooltip("Боковое расстояние стопы от оси тела при ползании, м.")]
        [SerializeField, Min(0f)] private float footWidth = .19f;
        [Tooltip("Высота стоп над землёй при ползании, м.")]
        [SerializeField, Min(0f)] private float footHeight = .1f;
        [Tooltip("Боковой вынос коленей при ползании, м.")]
        [SerializeField, Min(0f)] private float kneeWidth = .32f;
        [Tooltip("Амплитуда перемещения стоп вдоль направления движения, м.")]
        [SerializeField, Min(0f)] private float footReach = .16f;

        private EnemyCrawlPursuit crawl;
        private UnityEngine.AI.NavMeshAgent agent;
        private float weight, phase;
        public float BlendWeight => weight;

        private void Awake() { crawl = GetComponentInParent<EnemyCrawlPursuit>(); agent = GetComponentInParent<UnityEngine.AI.NavMeshAgent>(); }

        public bool ApplyIK(Animator animator)
        {
            if (crawl == null || !ZombieSurvival.Combat.DamageReceiver.IsAlive(ZombieSurvival.Combat.DamageReceiver.Find(this)))
            {
                weight = 0f;
                ClearLegIK(animator);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                return true;
            }
            bool active = isActiveAndEnabled && animationEnabled && crawl.IsCrawling;
            weight = active ? crawl.PoseWeight : Mathf.MoveTowards(weight, 0f, blendSpeed * Time.deltaTime);
            if (weight <= 0f) { ClearLegIK(animator); return false; }
            Vector3 velocity = Vector3.ProjectOnPlane((agent.enabled && agent.isOnNavMesh ? agent.velocity : Vector3.zero), transform.up);
            float speed = velocity.magnitude;
            // Follow real displacement, including strafing/backward crawl; idle stays planted.
            phase += speed * Time.deltaTime / Mathf.Max(.1f, metresPerCycle) * Mathf.PI * 2f;
            float cycle = Mathf.Sin(phase);
            float movement = Mathf.Clamp01(speed / .5f);
            Vector3 up = transform.up, forward = transform.forward, right = transform.right;
            Vector3 travel = speed > .01f ? velocity.normalized : forward;
            Vector3 feet = crawl.transform.position;
            Vector3 body = feet + up * (bodyHeight + Mathf.Cos(phase * 2f) * bodyBob * movement) + forward * bodyForwardOffset;
            animator.bodyPosition = Vector3.Lerp(animator.bodyPosition, body, weight);
            animator.bodyRotation = Quaternion.Slerp(animator.bodyRotation,
                Quaternion.LookRotation(forward, up) * Quaternion.Euler(bodyPitch, 0f, 0f), weight);
            Limb(animator, AvatarIKGoal.LeftHand, feet + forward * handForward - right * handWidth
                + up * (handHeight + Mathf.Max(0f, cycle) * handLift * movement) + travel * (cycle * reach * movement));
            Limb(animator, AvatarIKGoal.RightHand, feet + forward * handForward + right * handWidth
                + up * (handHeight + Mathf.Max(0f, -cycle) * handLift * movement) - travel * (cycle * reach * movement));
            Limb(animator, AvatarIKGoal.LeftFoot, feet + forward * footForward - right * footWidth + up * footHeight - travel * (cycle * footReach * movement));
            Limb(animator, AvatarIKGoal.RightFoot, feet + forward * footForward + right * footWidth + up * footHeight + travel * (cycle * footReach * movement));
            Hint(animator, AvatarIKHint.LeftKnee, feet - forward * .2f - right * kneeWidth + up * .18f);
            Hint(animator, AvatarIKHint.RightKnee, feet - forward * .2f + right * kneeWidth + up * .18f);
            Hint(animator, AvatarIKHint.LeftElbow, feet + forward * .2f - right * .45f + up * .15f);
            Hint(animator, AvatarIKHint.RightElbow, feet + forward * .2f + right * .45f + up * .15f);
            return true;
        }

        private void Limb(Animator animator, AvatarIKGoal goal, Vector3 target)
        {
            animator.SetIKRotationWeight(goal, 0f);
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKPosition(goal, target);
        }
        private static void ClearLegIK(Animator animator)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
        }
        private void Hint(Animator animator, AvatarIKHint hint, Vector3 target)
        {
            animator.SetIKHintPositionWeight(hint, weight);
            animator.SetIKHintPosition(hint, target);
        }
    }
}
