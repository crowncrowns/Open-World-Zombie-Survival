using UnityEngine;

namespace ZombieSurvival.Player
{
    /// <summary>Collision-driven traversal. CharacterController remains enabled throughout every action.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController), typeof(CharacterController))]
    public sealed class PlayerParkour : MonoBehaviour
    {
        public enum TraversalState { None, Hanging, Climbing, Vaulting, Rolling }

        [Header("Обнаружение поверхностей")]
        [Tooltip("Слои геометрии, которую можно обнаруживать и преодолевать паркуром.")]
        [SerializeField] private LayerMask obstacleMask = ~0;
        [Tooltip("Расстояние проверки поверхности, м.")]
        [SerializeField, Min(.1f)] private float probeDistance = 0.9f;
        [Tooltip("Высота луча поиска стены относительно ног, м.")]
        [SerializeField, Min(.05f)] private float wallProbeHeight = .5f;
        [Tooltip("Отступ луча внутрь поверхности при поиске верхней грани, м.")]
        [SerializeField, Min(.02f)] private float topProbeInset = .15f;
        [Tooltip("Максимальный угол наклона верхней поверхности выступа, градусов.")]
        [SerializeField, Range(0f, 60f)] private float maxTopSlope = 30f;
        [Tooltip("Зазор капсулы от поверхности на траектории паркура, м.")]
        [SerializeField, Min(.01f)] private float surfaceClearance = .06f;
        [Tooltip("Допустимое расхождение с траекторией паркура, м. При блокировке действие прерывается.")]
        [SerializeField, Min(.02f)] private float maxPathError = .12f;

        [Header("Захват выступа")]
        [Tooltip("Разрешить захват выступов руками.")]
        [SerializeField] private bool ledgeGrabEnabled = true;
        [Tooltip("Автоматически хвататься за доступный выступ при движении вперёд.")]
        [SerializeField] private bool autoGrabWhileMovingForward = true;
        [Tooltip("Минимальная высота захватываемого выступа над ногами, м.")]
        [SerializeField, Min(.1f)] private float minGrabHeight = 1.05f;
        [Tooltip("Максимальная высота захватываемого выступа над ногами, м.")]
        [SerializeField, Min(.2f)] private float maxGrabHeight = 2.2f;
        [Tooltip("Высота выступа над ногами во время виса, м.")]
        [SerializeField, Min(.2f)] private float hangingHandHeight = 1.65f;
        [Tooltip("Допустимая вертикальная погрешность захвата края, м.")]
        [SerializeField, Min(.05f)] private float grabVerticalTolerance = .45f;
        [Tooltip("Максимальная скорость подъёма, при которой разрешён захват, м/с.")]
        [SerializeField, Min(0f)] private float maxAscendingGrabSpeed = 6f;
        [Tooltip("Зазор между физической капсулой и стеной при висе, м.")]
        [SerializeField, Min(.01f)] private float wallGap = .08f;
        [Tooltip("Запрет повторного захвата после отпускания, с.")]
        [SerializeField, Min(0f)] private float regrabDelay = .4f;
        [Tooltip("Скорость отталкивания от стены при отпускании выступа, м/с.")]
        [SerializeField, Min(0f)] private float dropPushOffSpeed = 1f;
        [Tooltip("Предел горизонтального обзора относительно стены при висе, градусов.")]
        [SerializeField, Range(0f, 90f)] private float hangingLookAngle = 65f;

        [Header("Положение рук на выступе (Humanoid IK)")]
        [Tooltip("Привязывать руки Humanoid-модели к выступу с помощью IK.")]
        [SerializeField] private bool handIKEnabled = true;
        [Tooltip("Расстояние кисти от центра захвата, м.")]
        [SerializeField, Min(.05f)] private float handSpacing = .22f;
        [Tooltip("Поправка высоты кистей относительно края выступа, м.")]
        [SerializeField] private float handHeightOffset = .02f;
        [Tooltip("Отступ кистей наружу от стены при висе, м.")]
        [SerializeField, Min(0f)] private float handOutset = .035f;
        [Tooltip("Скорость плавного включения и отпускания IK рук.")]
        [SerializeField, Min(.1f)] private float handIKBlendSpeed = 8f;
        [Tooltip("Доля подтягивания, на которой кисти отпускают край: от 0 до 1.")]
        [SerializeField, Range(0f, 1f)] private float handIKClimbReleaseProgress = .2f;

        [Header("Подтягивание и движение вдоль края")]
        [Tooltip("Разрешить подтягивание из виса на выступ.")]
        [SerializeField] private bool climbEnabled = true;
        [Tooltip("Разрешить движение вдоль края во время виса.")]
        [SerializeField] private bool shimmyEnabled = true;
        [Tooltip("Скорость движения вдоль края, м/с.")]
        [SerializeField, Min(0f)] private float shimmySpeed = 1.1f;
        [Tooltip("Допустимый перепад высоты края при движении вдоль него, м.")]
        [SerializeField, Min(.02f)] private float maxShimmyHeightChange = .15f;
        [Tooltip("Отступ внутрь площадки для безопасного вставания после подтягивания, м.")]
        [SerializeField, Min(.01f)] private float standInset = .16f;
        [Tooltip("Длительность подтягивания на выступ, с.")]
        [SerializeField, Min(.1f)] private float climbDuration = .85f;

        [Header("Низкие препятствия")]
        [Tooltip("Разрешить преодоление низких препятствий.")]
        [SerializeField] private bool vaultEnabled = true;
        [Tooltip("Минимальная высота низкого препятствия для преодоления, м.")]
        [SerializeField, Min(.1f)] private float minVaultHeight = .45f;
        [Tooltip("Максимальная высота низкого препятствия, м.")]
        [SerializeField, Min(.2f)] private float maxVaultHeight = 1.15f;
        [Tooltip("Дальность поиска места приземления за стенкой, м.")]
        [SerializeField, Min(.5f)] private float vaultReachBeyondWall = 1.5f;
        [Tooltip("Допустимое падение за преодолеваемым препятствием, м.")]
        [SerializeField, Min(0f)] private float maxVaultDrop = 1.2f;
        [Tooltip("Длительность преодоления низкого препятствия, с.")]
        [SerializeField, Min(.1f)] private float vaultDuration = .55f;

        [Header("Перекат и подкат")]
        [Tooltip("Разрешить перекаты и подкаты.")]
        [SerializeField] private bool rollEnabled = true;
        [Tooltip("Горизонтальная скорость переката/подката, м/с.")]
        [SerializeField, Min(.1f)] private float rollSpeed = 6.5f;
        [Tooltip("Полная длительность переката на открытом месте, с.")]
        [SerializeField, Min(.1f)] private float rollDuration = .6f;
        [Tooltip("Пауза перед следующим перекатом, с.")]
        [SerializeField, Min(0f)] private float rollCooldown = .45f;
        [Tooltip("Высота физической капсулы во время переката, м.")]
        [SerializeField, Min(.1f)] private float rollCapsuleHeight = .8f;
        [Tooltip("Опускание камеры во время переката, м.")]
        [SerializeField, Min(0f)] private float rollCameraDrop = .95f;
        [Tooltip("Боковой наклон камеры при перекате, градусов. 0 отключает эффект.")]
        [SerializeField, Range(0f, 40f)] private float rollCameraTilt = 10f;
        [Tooltip("Сколько секунд хранить нажатие переката перед приземлением.")]
        [SerializeField, Min(0f)] private float landingRollBuffer = .35f;
        [Tooltip("Минимальная скорость падения для переката при приземлении, м/с.")]
        [SerializeField, Min(0f)] private float minimumLandingRollSpeed = 6f;
        [Tooltip("Автоматически выполнять перекат при жёстком приземлении, если хватает выносливости.")]
        [SerializeField] private bool autoRollOnHardLanding;
        [Tooltip("Под низкой крышей завершить подкат и перейти к ползанию через указанное время, с.")]
        [SerializeField, Min(0f)] private float rollToCrawlAfter = .25f;

        private FirstPersonController motor;
        private CharacterController capsule;
        private TraversalState state;
        private Ledge ledge;
        private Vector3 pathStart, pathLift, pathAcross, pathEnd;
        private Vector3 rollDirection;
        private float elapsed, duration, nextGrabTime, nextRollTime, rollFallSpeed;
        private float rollBufferedUntil = float.NegativeInfinity;
        private bool inputAllowed;
        private bool rollWasGrounded;
        private readonly RaycastHit[] rayHits = new RaycastHit[24];
        private readonly Collider[] overlaps = new Collider[32];

        private struct Ledge
        {
            public Vector3 point, normal;
            public Collider surface;
            public Vector3 surfacePosition, surfaceScale;
            public Quaternion surfaceRotation;
        }

        public TraversalState State => state;
        public bool OwnsMovement => state != TraversalState.None;
        public bool IsHanging => state == TraversalState.Hanging;
        public float ActionProgress => duration > 0f ? Mathf.Clamp01(elapsed / duration) : 0f;
        public Vector3 LedgePoint => ledge.point + ledge.normal * topProbeInset;
        public Vector3 LedgeNormal => ledge.normal;
        public bool UseHandIK => handIKEnabled;
        public float HandSpacing => handSpacing;
        public float HandHeightOffset => handHeightOffset;
        public float HandOutset => handOutset;
        public float HandIKBlendSpeed => handIKBlendSpeed;
        public float HandIKClimbReleaseProgress => handIKClimbReleaseProgress;

        private void Awake()
        {
            motor = GetComponent<FirstPersonController>();
            capsule = GetComponent<CharacterController>();
        }

        private void OnDisable()
        {
            if (motor != null && OwnsMovement) CancelTraversal();
            rollBufferedUntil = float.NegativeInfinity;
        }

        public bool Tick(float deltaTime, bool acceptingInput, Vector2 movement, Vector3 desiredVelocity,
            bool jumpPressed, bool crouchPressed, bool rollPressed)
        {
            if (!isActiveAndEnabled) return false;
            inputAllowed = acceptingInput;
            if (!acceptingInput) rollBufferedUntil = float.NegativeInfinity;
            if (OwnsMovement)
            {
                if (state != TraversalState.Rolling && !SurfaceStillValid())
                {
                    CancelTraversal();
                    return true;
                }
                if (!acceptingInput) return true;
                if (state == TraversalState.Hanging) TickHang(deltaTime, movement, jumpPressed, crouchPressed);
                else if (state == TraversalState.Rolling) TickRoll(deltaTime);
                else TickPath(deltaTime);
                return true;
            }
            if (!acceptingInput) return false;
            if (rollPressed)
            {
                if (motor.IsGrounded && TryBeginRoll(desiredVelocity.sqrMagnitude > .01f ? desiredVelocity : motor.PlanarVelocity)) return true;
                if (!motor.IsGrounded) rollBufferedUntil = Time.time + landingRollBuffer;
            }
            Vector3 forward = transform.forward;
            if (jumpPressed && motor.IsGrounded && !motor.IsCrouching && movement.y > .1f && TryVault(forward)) return true;
            bool grabRequested = jumpPressed || autoGrabWhileMovingForward && movement.y > .1f;
            if (ledgeGrabEnabled && !motor.IsGrounded && !motor.IsCrouching && grabRequested
                && Time.time >= nextGrabTime && motor.VerticalSpeed <= maxAscendingGrabSpeed
                && TryFindLedge(motor.FeetPosition, forward, minGrabHeight, maxGrabHeight, out var candidate)
                && Mathf.Abs(candidate.point.y - motor.FeetPosition.y - hangingHandHeight) <= grabVerticalTolerance)
            {
                Vector3 hangPosition = HangPosition(candidate);
                if (CapsuleFree(hangPosition, motor.StandingHeight) && MoveTo(hangPosition))
                {
                    ledge = candidate;
                    state = TraversalState.Hanging;
                    motor.ReleaseParkour(Vector3.zero);
                    motor.SetParkourPosture(motor.StandingHeight, 0f);
                    FaceLedge();
                    return true;
                }
            }
            return false;
        }

        public void NotifyLanded(float impactSpeed)
        {
            if (!inputAllowed || OwnsMovement || impactSpeed < minimumLandingRollSpeed) return;
            if (autoRollOnHardLanding || Time.time <= rollBufferedUntil)
                TryBeginRoll(motor.PlanarVelocity.sqrMagnitude > .01f ? motor.PlanarVelocity : transform.forward);
            rollBufferedUntil = float.NegativeInfinity;
        }

        private void TickHang(float deltaTime, Vector2 movement, bool jumpPressed, bool crouchPressed)
        {
            float facing = Quaternion.LookRotation(-ledge.normal, Vector3.up).eulerAngles.y;
            float yaw = facing + Mathf.Clamp(Mathf.DeltaAngle(facing, transform.eulerAngles.y), -hangingLookAngle, hangingLookAngle);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (crouchPressed || movement.y < -.5f)
            {
                state = TraversalState.None;
                nextGrabTime = Time.time + regrabDelay;
                motor.ReleaseParkour(ledge.normal * dropPushOffSpeed, -1f);
                return;
            }
            if (jumpPressed && climbEnabled)
            {
                Vector3 end = StandPosition(ledge);
                if (CapsuleFree(end, motor.StandingHeight) && HasStandSupport(end) && Spend(motor.Stamina != null ? motor.Stamina.ClimbCost : 0f))
                    BeginPath(TraversalState.Climbing, end, ledge.point.y, climbDuration);
                return;
            }
            if (!shimmyEnabled || Mathf.Abs(movement.x) < .1f) return;
            Vector3 tangent = Vector3.Cross(ledge.normal, Vector3.up);
            Vector3 nextFeet = motor.FeetPosition + tangent * (movement.x * shimmySpeed * deltaTime);
            if (TryFindLedge(nextFeet, -ledge.normal, minGrabHeight, maxGrabHeight, out var next)
                && Mathf.Abs(next.point.y - ledge.point.y) <= maxShimmyHeightChange
                && Vector3.Angle(next.normal, ledge.normal) < 20f
                && CapsuleFree(HangPosition(next), motor.StandingHeight) && MoveTo(HangPosition(next))) ledge = next;
        }

        private bool TryVault(Vector3 forward)
        {
            if (!vaultEnabled || !TryFindLedge(motor.FeetPosition, forward, minVaultHeight, maxVaultHeight, out var candidate)) return false;
            Vector3 beyond = candidate.point + forward * vaultReachBeyondWall;
            Vector3 origin = new Vector3(beyond.x, candidate.point.y + .5f, beyond.z);
            float castDistance = candidate.point.y - motor.FeetPosition.y + .5f + maxVaultDrop;
            if (!Ray(origin, Vector3.down, castDistance, out var ground) || ground.normal.y < Mathf.Cos(maxTopSlope * Mathf.Deg2Rad)
                || ground.point.y > candidate.point.y + .05f || motor.FeetPosition.y - ground.point.y > maxVaultDrop) return false;
            Vector3 end = RootFromFeet(ground.point + Vector3.up * surfaceClearance);
            if (!CapsuleFree(end, motor.StandingHeight)) return false;
            if (!Spend(motor.Stamina != null ? motor.Stamina.VaultCost : 0f)) return false;
            ledge = candidate;
            BeginPath(TraversalState.Vaulting, end, candidate.point.y, vaultDuration);
            return true;
        }

        private void BeginPath(TraversalState nextState, Vector3 end, float topY, float seconds)
        {
            state = nextState;
            motor.ReleaseParkour(Vector3.zero);
            motor.SetParkourPosture(motor.StandingHeight, 0f);
            FaceLedge();
            pathStart = transform.position;
            pathEnd = end;
            float highY = Mathf.Max(topY + surfaceClearance - motor.BottomOffset, end.y);
            pathLift = new Vector3(pathStart.x, highY, pathStart.z);
            pathAcross = new Vector3(end.x, highY, end.z);
            elapsed = 0f;
            duration = Mathf.Max(.1f, seconds);
        }

        private void TickPath(float deltaTime)
        {
            if (!CapsuleFree(pathEnd, motor.StandingHeight) || !HasStandSupport(pathEnd)) { CancelTraversal(); return; }
            elapsed += deltaTime;
            float t = ActionProgress;
            Vector3 target = t < .5f ? Vector3.Lerp(pathStart, pathLift, Mathf.SmoothStep(0f, 1f, t / .5f))
                : t < .9f ? Vector3.Lerp(pathLift, pathAcross, Mathf.SmoothStep(0f, 1f, (t - .5f) / .4f))
                : Vector3.Lerp(pathAcross, pathEnd, Mathf.SmoothStep(0f, 1f, (t - .9f) / .1f));
            if (!MoveTo(target)) { CancelTraversal(); return; }
            if (t >= 1f)
            {
                state = TraversalState.None;
                nextGrabTime = Time.time + regrabDelay;
                motor.ReleaseParkour(Vector3.zero, -1f);
            }
        }

        private bool TryBeginRoll(Vector3 direction)
        {
            if (!rollEnabled || motor.IsProne || Time.time < nextRollTime || direction.sqrMagnitude < .01f
                || !Spend(motor.Stamina != null ? motor.Stamina.RollCost : 0f)) return false;
            state = TraversalState.Rolling;
            rollDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            elapsed = 0f;
            duration = Mathf.Max(.1f, rollDuration);
            nextRollTime = Time.time + duration + rollCooldown;
            rollBufferedUntil = float.NegativeInfinity;
            rollFallSpeed = -motor.GroundStickSpeed;
            rollWasGrounded = capsule.isGrounded;
            motor.ReleaseParkour(Vector3.zero);
            motor.SetParkourPosture(rollCapsuleHeight, rollCameraDrop);
            capsule.stepOffset = 0f;
            return true;
        }

        private void TickRoll(float deltaTime)
        {
            elapsed += deltaTime;
            motor.SetParkourPosture(rollCapsuleHeight, rollCameraDrop);
            capsule.stepOffset = 0f;
            motor.AddParkourCameraTilt(Mathf.Sin(ActionProgress * Mathf.PI) * rollCameraTilt);
            rollFallSpeed = capsule.isGrounded ? -motor.GroundStickSpeed
                : Mathf.Max(-motor.TerminalFallSpeed, rollFallSpeed - motor.GravityAcceleration * deltaTime);
            Vector3 before = transform.position;
            float speed = rollSpeed * Mathf.Lerp(1f, .65f, ActionProgress);
            CollisionFlags collisions = capsule.Move((rollDirection * speed + Vector3.up * rollFallSpeed) * deltaTime);
            bool grounded = (collisions & CollisionFlags.Below) != 0;
            bool landed = !rollWasGrounded && grounded;
            rollWasGrounded = grounded;
            if (landed) motor.ReportLanding(Mathf.Max(0f, -rollFallSpeed));
            if (!motor.IsAlive) return;
            float distance = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
            if (ActionProgress >= 1f || distance < speed * deltaTime * .15f
                || elapsed >= rollToCrawlAfter && !motor.CanFitHeight(motor.CrouchHeight))
            {
                state = TraversalState.None;
                motor.AddParkourCameraTilt(0f);
                motor.ReleaseParkour(rollDirection * speed, rollFallSpeed);
            }
        }

        private bool TryFindLedge(Vector3 feet, Vector3 direction, float minHeight, float maxHeight, out Ledge candidate)
        {
            candidate = default;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (!Ray(feet + Vector3.up * Mathf.Min(wallProbeHeight, minHeight + .01f), direction, probeDistance, out var wall)
                || Mathf.Abs(wall.normal.y) > .3f || Vector3.Dot(wall.normal, direction) > -.5f) return false;
            if (wall.collider is CharacterController || wall.rigidbody != null && !wall.rigidbody.isKinematic) return false;
            Vector3 origin = wall.point + direction * topProbeInset;
            origin.y = feet.y + maxHeight + .1f;
            if (!Ray(origin, Vector3.down, maxHeight - minHeight + .2f, out var top)
                || top.normal.y < Mathf.Cos(maxTopSlope * Mathf.Deg2Rad)) return false;
            float height = top.point.y - feet.y;
            if (height < minHeight || height > maxHeight) return false;
            // Unpredictably moving/dynamic props are not valid handholds in this prototype.
            if (top.collider is CharacterController || top.rigidbody != null && !top.rigidbody.isKinematic) return false;
            candidate.point = top.point;
            candidate.normal = Vector3.ProjectOnPlane(wall.normal, Vector3.up).normalized;
            candidate.surface = top.collider;
            candidate.surfacePosition = top.collider.transform.position;
            candidate.surfaceScale = top.collider.transform.lossyScale;
            candidate.surfaceRotation = top.collider.transform.rotation;
            return true;
        }

        private Vector3 HangPosition(Ledge target) => RootFromFeet(target.point + target.normal * (capsule.radius + wallGap + topProbeInset)
            - Vector3.up * hangingHandHeight);
        private Vector3 StandPosition(Ledge target) => RootFromFeet(target.point - target.normal * (capsule.radius + standInset)
            + Vector3.up * surfaceClearance);
        private Vector3 RootFromFeet(Vector3 feet) => feet - Vector3.up * motor.BottomOffset;
        private void FaceLedge() => transform.rotation = Quaternion.LookRotation(-ledge.normal, Vector3.up);

        private bool SurfaceStillValid() => ledge.surface != null && ledge.surface.enabled && ledge.surface.gameObject.activeInHierarchy
            && Vector3.Distance(ledge.surface.transform.position, ledge.surfacePosition) < .01f
            && Vector3.Distance(ledge.surface.transform.lossyScale, ledge.surfaceScale) < .001f
            && Quaternion.Angle(ledge.surface.transform.rotation, ledge.surfaceRotation) < 1f;

        private void CancelTraversal()
        {
            state = TraversalState.None;
            nextGrabTime = Time.time + regrabDelay;
            motor.AddParkourCameraTilt(0f);
            motor.ReleaseParkour(Vector3.zero, -1f);
        }

        public void ResetTraversal()
        {
            CancelTraversal();
            nextGrabTime = nextRollTime = 0f;
            rollBufferedUntil = float.NegativeInfinity;
            inputAllowed = false;
            elapsed = duration = 0f;
        }

        private bool Spend(float cost) => motor.Stamina == null || motor.Stamina.TrySpend(cost);

        private bool MoveTo(Vector3 point)
        {
            capsule.Move(point - transform.position);
            return Vector3.Distance(transform.position, point) <= maxPathError;
        }

        private bool CapsuleFree(Vector3 root, float height)
        {
            float radius = Mathf.Max(.02f, capsule.radius - capsule.skinWidth);
            Vector3 bottom = root + Vector3.up * motor.BottomOffset;
            Vector3 a = bottom + Vector3.up * (radius + .02f);
            Vector3 b = bottom + Vector3.up * Mathf.Max(radius + .02f, height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(a, b, radius, overlaps, obstacleMask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (!overlaps[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        private bool HasStandSupport(Vector3 root)
        {
            Vector3 feet = root + Vector3.up * motor.BottomOffset;
            return Ray(feet + Vector3.up * .1f, Vector3.down, .3f, out var support)
                && support.normal.y >= Mathf.Cos(maxTopSlope * Mathf.Deg2Rad)
                && !(support.collider is CharacterController)
                && (support.rigidbody == null || support.rigidbody.isKinematic);
        }

        private bool Ray(Vector3 origin, Vector3 direction, float distance, out RaycastHit result)
        {
            result = default;
            int count = Physics.RaycastNonAlloc(origin, direction, rayHits, distance, obstacleMask, QueryTriggerInteraction.Ignore);
            if (count == rayHits.Length) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (rayHits[i].collider.transform.IsChildOf(transform) || rayHits[i].distance >= nearest) continue;
                nearest = rayHits[i].distance;
                result = rayHits[i];
            }
            return nearest < float.PositiveInfinity;
        }
    }
}
