using UnityEngine;
using UnityEngine.AI;
using ZombieSurvival.Combat;

namespace ZombieSurvival.AI
{
    /// <summary>Grounds the animated body without moving the navigation root; preserves lifted swing feet.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class HumanoidGrounding : MonoBehaviour
    {
        [Tooltip("Слои земли для процедурной постановки ног.")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [Tooltip("Высота начала проверки земли над стопой, м.")]
        [SerializeField, Min(0.1f)] private float probeHeight = 0.6f;
        [Tooltip("Расстояние проверки поверхности, м.")]
        [SerializeField, Min(0.2f)] private float probeDistance = 1.4f;
        [Tooltip("Предельное смещение визуального тела относительно корня навигации по высоте, м.")]
        [SerializeField, Range(0.05f, 0.6f)] private float maximumBodyCorrection = 0.35f;
        [Tooltip("Зазор подошвы над землёй, м.")]
        [SerializeField, Range(0f, 0.1f)] private float soleClearance = 0.008f;
        [Tooltip("Высота стопы над землёй для начала фиксации, м.")]
        [SerializeField, Range(0.01f, 0.2f)] private float plantHeight = 0.025f;
        [Tooltip("Высота стопы над землёй, при которой фиксация отпускается, м.")]
        [SerializeField, Range(0.02f, 0.3f)] private float releaseHeight = 0.055f;
        [Tooltip("Предельное растяжение зафиксированной ноги до отпускания стопы, м.")]
        [SerializeField, Range(0.05f, 0.6f)] private float maximumPlantStretch = 0.3f;
        [Tooltip("Скорость сглаживания визуальной постановки ног.")]
        [SerializeField, Min(1f)] private float smoothing = 20f;
        [Tooltip("Время плавного закрепления стопы на земле, с.")]
        [SerializeField, Range(0.04f, 0.4f)] private float acquireDuration = 0.12f;
        [Tooltip("Время плавного отпускания закреплённой стопы, с.")]
        [SerializeField, Range(0.04f, 0.4f)] private float releaseDuration = 0.16f;
        [Tooltip("Максимальный угол наклона земли для постановки стоп, градусов.")]
        [SerializeField, Range(0f, 60f)] private float maximumGroundSlope = 50f;
        [SerializeField]
        [Tooltip("Текущая вертикальная поправка визуального тела в Play. Корень остаётся под управлением NavMeshAgent.")]
        private float bodyOffset;

        private Animator animator;
        private NavMeshAgent agent;
        private Transform leftBone;
        private Transform rightBone;
        private Transform leftToes;
        private Transform rightToes;
        private EnemyAttack[] attacks;
        private IDamageable health;
        private Foot left = new Foot();
        private Foot right = new Foot();
        public float BodyOffset => bodyOffset;
        public bool LeftPlanted => left.planted;
        public bool RightPlanted => right.planted;

        private sealed class Foot
        {
            public bool planted;
            public Vector3 position;
            public Quaternion rotation;
            public float weight;
            public Vector3 forward;
            public bool rotationCalibrated;
            public float goalToeYaw;
            public bool waitingForSwing;
            public float transitionTime;
            public float releaseStartWeight;
        }

        private void OnEnable() => Initialize();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetForPlay()
        {
            foreach (var grounding in Object.FindObjectsByType<HumanoidGrounding>(FindObjectsInactive.Include))
                if (grounding.isActiveAndEnabled) grounding.Initialize();
        }

        private void Initialize()
        {
            animator = GetComponent<Animator>();
            agent = GetComponentInParent<NavMeshAgent>();
            attacks = agent != null ? agent.GetComponents<EnemyAttack>() : GetComponentsInParent<EnemyAttack>();
            health = DamageReceiver.Find(this);
            if (!animator.isHuman) return;
            leftBone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightBone = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
            bodyOffset = 0f;
            left = new Foot();
            right = new Foot();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || leftBone == null || rightBone == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            if (!DamageReceiver.IsAlive(health))
            {
                bodyOffset = 0f; left.planted = right.planted = false;
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f); animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f); animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                return;
            }
            var crawlPose = GetComponent<HumanoidCrawlPose>();
            if (crawlPose != null && crawlPose.ApplyIK(animator)) { bodyOffset = 0f; left.planted = right.planted = false; return; }
            float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            // NavMesh rasterization can sit above the actual collider surface. Correct the visual body only.
            float desiredOffset = 0f;
            if (Probe(transform.position, out RaycastHit rootHit))
                desiredOffset = Mathf.Clamp(rootHit.point.y - transform.position.y,
                    -maximumBodyCorrection, maximumBodyCorrection);
            bodyOffset = Mathf.Lerp(bodyOffset, desiredOffset, blend);

            // Capture animated ankles before changing the body position; Animator re-evaluates them every frame.
            Vector3 leftPosition = leftBone.position + Vector3.up * bodyOffset;
            Vector3 rightPosition = rightBone.position + Vector3.up * bodyOffset;
            CalibrateGoalYaw(AvatarIKGoal.LeftFoot, leftBone, leftToes, left);
            CalibrateGoalYaw(AvatarIKGoal.RightFoot, rightBone, rightToes, right);
            animator.bodyPosition += Vector3.up * bodyOffset;
            bool allowLock = true;
            foreach (var attack in attacks) if (attack != null && attack.IsBusy) allowLock = false;
            PlaceFoot(AvatarIKGoal.LeftFoot, leftPosition, animator.leftFeetBottomHeight, left, blend, allowLock);
            PlaceFoot(AvatarIKGoal.RightFoot, rightPosition, animator.rightFeetBottomHeight, right, blend, allowLock);
        }

        private void CalibrateGoalYaw(AvatarIKGoal goal, Transform bone, Transform toes, Foot foot)
        {
            if (foot.rotationCalibrated) return;
            foot.rotationCalibrated = true;
            // Human IK goals and imported foot bones have different local axes, often mirrored left/right.
            // Measure each anatomical toe direction in human-goal space, inside the IK callback.
            // Toes are optional in Humanoid Avatars; without them use the standard goal-forward axis.
            if (toes == null) return;
            Vector3 toeInGoal = Quaternion.Inverse(animator.GetIKRotation(goal)) * (toes.position - bone.position);
            toeInGoal.y = 0f;
            if (toeInGoal.sqrMagnitude > 0.000001f)
                foot.goalToeYaw = Vector3.SignedAngle(Vector3.forward, toeInGoal, Vector3.up);
        }

        private Quaternion GroundGoalRotation(Vector3 normal, Foot foot)
        {
            Vector3 heading = Vector3.ProjectOnPlane(transform.forward, normal);
            if (heading.sqrMagnitude < 0.000001f) heading = Vector3.ProjectOnPlane(transform.right, normal);
            return Quaternion.LookRotation(heading.normalized, normal)
                * Quaternion.AngleAxis(-foot.goalToeYaw, Vector3.up);
        }

        private void PlaceFoot(AvatarIKGoal goal, Vector3 animatedPosition,
            float bottomHeight, Foot foot, float blend, bool allowLock)
        {
            bool foundGround = Probe(animatedPosition, out RaycastHit hit);
            float lift = foundGround ? animatedPosition.y - bottomHeight - hit.point.y : float.PositiveInfinity;
            Vector3 surfacePosition = hit.point + Vector3.up * (bottomHeight + soleClearance);
            float stretch = Vector2.Distance(new Vector2(animatedPosition.x, animatedPosition.z),
                new Vector2(foot.position.x, foot.position.z));
            bool released = false;
            bool moving = new Vector2(agent.velocity.x, agent.velocity.z).sqrMagnitude > 0.0025f;
            if (foot.planted && (!allowLock || !foundGround || lift > releaseHeight || stretch > maximumPlantStretch
                || Vector3.Angle(foot.forward, transform.forward) > 25f))
            {
                foot.planted = false;
                foot.waitingForSwing = allowLock && moving;
                foot.transitionTime = 0f;
                foot.releaseStartWeight = foot.weight;
                released = true;
            }
            // A stretch release is not another footfall: require the actual animation to swing first.
            // Idle/turn-on-the-spot and attack recovery may plant without a new locomotion cycle.
            if (!moving || !allowLock || (foundGround && lift > releaseHeight)) foot.waitingForSwing = false;
            if (!released && allowLock && !foot.planted && !foot.waitingForSwing && foot.weight <= 0.0001f
                && foundGround && lift <= plantHeight)
            {
                foot.planted = true;
                foot.transitionTime = 0f;
                foot.position = surfacePosition;
                foot.forward = transform.forward;
                foot.rotation = GroundGoalRotation(hit.normal, foot);
            }
            if (foot.planted)
            {
                // Keep XZ fixed during stance, following only the collider height at that fixed contact.
                if (Probe(foot.position, out RaycastHit plantedHit))
                {
                    foot.position.y = plantedHit.point.y + bottomHeight + soleClearance;
                    foot.rotation = Quaternion.Slerp(foot.rotation, GroundGoalRotation(plantedHit.normal, foot), blend);
                }
            }
            // Never replace an active target. Fade fully to animation before acquiring the next contact.
            // SmoothStep gives zero endpoint velocity, unlike the former immediate 0.x -> 1 replant.
            foot.transitionTime += Time.deltaTime;
            foot.weight = foot.planted
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(foot.transitionTime / acquireDuration))
                : Mathf.SmoothStep(foot.releaseStartWeight, 0f, Mathf.Clamp01(foot.transitionTime / releaseDuration));
            animator.SetIKPositionWeight(goal, foot.weight);
            animator.SetIKRotationWeight(goal, foot.weight);
            animator.SetIKPosition(goal, foot.position);
            animator.SetIKRotation(goal, foot.rotation);
        }

        private bool Probe(Vector3 position, out RaycastHit nearest)
        {
            nearest = default;
            float minimumDistance = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(position + Vector3.up * probeHeight, Vector3.down,
                probeDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider is CharacterController || hit.collider.GetComponentInParent<NavMeshAgent>() != null
                    || hit.collider.GetComponentInParent<CombatTarget>() != null) continue;
                if (hit.normal.y < Mathf.Cos(maximumGroundSlope * Mathf.Deg2Rad)) continue;
                if (hit.distance >= minimumDistance) continue;
                minimumDistance = hit.distance;
                nearest = hit;
            }
            return minimumDistance < float.PositiveInfinity;
        }
    }
}
