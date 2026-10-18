using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Progression;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Команды ввода")]
        [Tooltip("Исходный набор команд. Каждый игрок использует отдельную копию в Play, не отключая команды интерфейса.")]
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("Название карты команд игрока в Input Actions.")]
        [SerializeField] private string playerActionMap = "Player";
        [Tooltip("Имя команды перемещения в карте Input Actions игрока. Привязки клавиш меняются в Input Actions, не в этом поле.")]
        [SerializeField] private string moveActionName = "Move";
        [Tooltip("Имя команды обзора в карте Input Actions игрока. Привязки клавиш меняются в Input Actions, не в этом поле.")]
        [SerializeField] private string lookActionName = "Look";
        [Tooltip("Имя команды бега в карте Input Actions игрока. Привязки клавиш меняются в Input Actions, не в этом поле.")]
        [SerializeField] private string sprintActionName = "Sprint";
        [Tooltip("Имя команды приседания в карте Input Actions игрока. Привязки клавиш меняются в Input Actions, не в этом поле.")]
        [SerializeField] private string crouchActionName = "Crouch";
        [Tooltip("Имя команды прыжка в карте Input Actions игрока. Привязки клавиш меняются в Input Actions, не в этом поле.")]
        [SerializeField] private string jumpActionName = "Jump";
        [Tooltip("Имя команды переката в карте Input Actions игрока. Привязки клавиш меняются в Input Actions, не в этом поле.")]
        [SerializeField] private string rollActionName = "Roll";
        [Tooltip("Имя команды положения лёжа в карте Input Actions игрока. Привязки клавиш меняются в Input Actions, не в этом поле.")]
        [SerializeField] private string proneActionName = "Prone";
        [Tooltip("Команда освобождения курсора, по умолчанию Escape. Привязки редактируются без изменения кода.")]
        [SerializeField] private InputAction releaseCursorInput = new InputAction("ReleaseCursor", InputActionType.Button, "<Keyboard>/escape");
        [Tooltip("Команда захвата курсора для обзора; по умолчанию левая кнопка мыши.")]
        [SerializeField] private InputAction captureCursorInput = new InputAction("CaptureCursor", InputActionType.Button, "<Mouse>/leftButton");

        [Header("Движение")]
        [Tooltip("Разрешить движение персонажа от команд игрока.")]
        [SerializeField] private bool movementEnabled = true;
        [Tooltip("Разрешить бег с расходом выносливости.")]
        [SerializeField] private bool sprintEnabled = true;
        [Tooltip("Скорость обычной ходьбы, м/с.")]
        [SerializeField, Min(0f)] private float walkSpeed = 4f;
        [Tooltip("Скорость бега, м/с.")]
        [SerializeField, Min(0f)] private float runSpeed = 7f;

        [Tooltip("Скорость тихого шага при удержании Alt, м/с. Alt имеет приоритет над бегом.")]
        [SerializeField, Min(0f)] private float slowWalkSpeed = 1.5f;
        [Tooltip("Имя команды тихого шага в карте Input Actions игрока. Привязки клавиш меняются в Input Actions, не в этом поле.")]
        [SerializeField] private string slowWalkActionName = "SlowWalk";

        [Header("Прыжок и движение в воздухе")]
        [Tooltip("Разрешить прыжки с земли.")]
        [SerializeField] private bool jumpEnabled = true;
        [Tooltip("Расчётная высота прыжка без препятствий, м.")]
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.15f;
        [Tooltip("Время после схода с края, когда ещё можно прыгнуть, с.")]
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [Tooltip("Сколько секунд хранить нажатие прыжка до приземления.")]
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;
        [Tooltip("Минимальная пауза между прыжками, с.")]
        [SerializeField, Min(0.01f)] private float jumpCooldown = 0.15f;
        [Tooltip("Ускорение управления в воздухе, м/с².")]
        [SerializeField, Min(0f)] private float airAcceleration = 8f;
        [Tooltip("Доля управления направлением в воздухе: 0 — без управления, 1 — полное.")]
        [SerializeField, Range(0f, 1f)] private float airControl = 0.5f;
        [Tooltip("Торможение горизонтального движения в воздухе, м/с².")]
        [SerializeField, Min(0f)] private float airDrag = 0.5f;
        [Tooltip("Ограничение горизонтальной скорости в воздухе, м/с.")]
        [SerializeField, Min(0.1f)] private float maxAirSpeed = 9f;
        [Tooltip("Множитель гравитации при падении. Больше — быстрее падение.")]
        [SerializeField, Min(1f)] private float fallingGravityMultiplier = 1.25f;

        [Header("Приседание")]
        [Tooltip("Разрешить приседание.")]
        [SerializeField] private bool crouchEnabled = true;
        [Tooltip("Выключено — удерживать C; включено — нажатие переключает приседание и вставание.")]
        [SerializeField] private bool toggleCrouch;
        [Tooltip("Высота физической капсулы сидя, м. Ноги остаются на земле.")]
        [SerializeField, Min(0.1f)] private float crouchHeight = 1f;
        [Tooltip("Скорость передвижения сидя, м/с.")]
        [SerializeField, Min(0f)] private float crouchSpeed = 2f;
        [Tooltip("Скорость изменения высоты капсулы при приседании, м/с. Камера следует за капсулой.")]
        [SerializeField, Min(0.1f)] private float crouchTransitionSpeed = 4f;
        [Tooltip("Опускание камеры относительно стоячей позы при приседании, м.")]
        [SerializeField, Min(0f)] private float crouchCameraDrop = 0.75f;
        [Tooltip("Минимальный зазор камеры до верхней и нижней границ капсулы, м.")]
        [SerializeField, Min(0f)] private float cameraHeadClearance = 0.1f;
        [Tooltip("Дополнительный запас места над головой перед вставанием, м.")]
        [SerializeField, Min(0f)] private float standingClearance = 0.02f;
        [Tooltip("Слои препятствий для проверки вставания. Собственные коллайдеры и триггеры игнорируются.")]
        [SerializeField] private LayerMask standingObstructionMask = ~0;

        [Header("Положение лёжа и ползание")]
        [Tooltip("Разрешить переход в положение лёжа и ползание.")]
        [SerializeField] private bool proneEnabled = true;
        [Tooltip("Высота физической капсулы лёжа, м. Не меньше двух радиусов капсулы.")]
        [SerializeField, Min(.1f)] private float proneHeight = .8f;
        [Tooltip("Скорость ползания, м/с.")]
        [SerializeField, Min(0f)] private float crawlSpeed = 1.1f;
        [Tooltip("Опускание камеры относительно стоячей позы при ползании, м.")]
        [SerializeField, Min(0f)] private float proneCameraDrop = 1.1f;
        [Tooltip("Скорость изменения высоты капсулы при переходе лёжа, м/с.")]
        [SerializeField, Min(.1f)] private float proneTransitionSpeed = 3f;

        [Header("Гравитация")]
        [Tooltip("Ускорение свободного падения, м/с².")]
        [SerializeField, Min(0.1f)] private float gravity = 20f;
        [Tooltip("Небольшая скорость вниз для сохранения контакта с землёй, м/с.")]
        [SerializeField, Min(0f)] private float groundStickSpeed = 2f;
        [Tooltip("Предельная вертикальная скорость падения, м/с.")]
        [SerializeField, Min(0.1f)] private float terminalFallSpeed = 50f;

        [Header("Обзор")]
        [Tooltip("Точка камеры игрока. Поворачивается вверх и вниз и опускается при смене позы.")]
        [SerializeField] private Transform playerCamera;
        [Tooltip("Разрешить обзор мышью и правым стиком.")]
        [SerializeField] private bool lookEnabled = true;
        [Tooltip("Поворот камеры в градусах на пиксель движения мыши.")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
        [Tooltip("Множитель чувствительности горизонтального обзора.")]
        [SerializeField, Min(0f)] private float horizontalLookMultiplier = 1f;
        [Tooltip("Множитель чувствительности вертикального обзора.")]
        [SerializeField, Min(0f)] private float verticalLookMultiplier = 1f;
        [Tooltip("Скорость обзора стиком, градусов/с. Движение мыши не умножается на время кадра.")]
        [SerializeField, Min(0f)] private float stickLookSpeed = 180f;
        [Tooltip("Инвертировать направление вертикального обзора.")]
        [SerializeField] private bool invertVerticalLook;
        [Tooltip("Предел наклона камеры вверх и вниз, градусов.")]
        [SerializeField, Range(1f, 89f)] private float verticalLookLimit = 85f;

        [Header("Курсор и фокус окна")]
        [Tooltip("Захватывать курсор при включении управления и возвращении в игру.")]
        [SerializeField] private bool captureCursorOnEnable = true;
        [Tooltip("Принимать игровые команды только при захваченном курсоре. Для автоматических тестов можно отключить.")]
        [SerializeField] private bool requireCursorCapture = true;
        [Tooltip("Принимать ввод только когда окно игры активно.")]
        [SerializeField] private bool requireApplicationFocus = true;

        private CharacterController controller;
        private InputActionAsset runtimeActions;
        private InputActionMap gameplayMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction slowWalkAction;
        private InputAction crouchAction;
        private InputAction jumpAction;
        private InputAction rollAction;
        private InputAction proneAction;
        private PlayerStamina stamina;
        private PlayerStatModifiers statModifiers;
        private bool proneRequested, proneToggleArmed;
        private PlayerParkour parkour;
        private Vector3 planarVelocity;
        private float airborneMoveSpeed;
        private float lastGroundedTime = float.NegativeInfinity;
        private float lastJumpPress = float.NegativeInfinity;
        private float lastJumpTime = float.NegativeInfinity;
        private bool wasGrounded;
        private bool skipJumpInput;
        private InputAction releaseCursorAction;
        private InputAction captureCursorAction;
        private float verticalVelocity;
        private float pitch;
        private bool skipLook;
        private bool gameplayInputEnabled = true;
        private bool crouchRequested;
        private bool crouchToggleArmed;
        private bool skipCrouchInput;
        private float standingHeight;
        private float standingStepOffset;
        private Vector3 standingCenter;
        private Vector3 standingCameraPosition;
        private readonly Collider[] standingOverlaps = new Collider[32];

        public bool GameplayInputEnabled => gameplayInputEnabled;
        public bool IsAlive { get; private set; } = true;
        public event Action<float> Landed;
        public event Action Jumped;
        public bool AcceptingGameplayInput => IsAlive && isActiveAndEnabled && gameplayInputEnabled && HasFocus
            && (!requireCursorCapture || Cursor.lockState == CursorLockMode.Locked)
            && gameplayMap != null && gameplayMap.enabled && Time.deltaTime > 0f;
        /// <summary>Other player systems share this player's runtime actions and input gate.</summary>
        public InputAction FindGameplayAction(string actionName) => gameplayMap?.FindAction(actionName);
        public bool IsCrouching => crouchRequested || controller != null && controller.height < standingHeight - 0.001f;
        public bool IsStandingBlocked { get; private set; }
        public bool IsProne => IsAlive && (parkour == null || !parkour.OwnsMovement)
            && (proneRequested || controller != null && controller.height < Mathf.Clamp(crouchHeight, controller.radius * 2f, standingHeight) - .05f);
        public bool IsSprinting { get; private set; }
        public bool IsSlowWalking { get; private set; }
        public PlayerStamina Stamina => stamina;
        public bool IsGrounded => controller != null && controller.isGrounded && verticalVelocity <= 0f
            && (parkour == null || !parkour.OwnsMovement);
        public float VerticalSpeed => verticalVelocity;
        public float StandingHeight => standingHeight;
        public float BottomOffset => standingCenter.y - standingHeight * 0.5f;
        public Vector3 FeetPosition => transform.position + transform.up * BottomOffset;
        public Vector3 PlanarVelocity => planarVelocity;
        public PlayerParkour Parkour => parkour;
        public float BaseWalkSpeed => walkSpeed;
        public float BaseRunSpeed => runSpeed;
        public float BaseJumpHeight => jumpHeight;
        public float EffectiveWalkSpeed => EvaluateStat(StatId.WalkSpeed, walkSpeed);
        public float EffectiveRunSpeed => EvaluateStat(StatId.RunSpeed, runSpeed);
        public float EffectiveJumpHeight => Mathf.Max(.1f, EvaluateStat(StatId.JumpHeight, jumpHeight));
        private float EvaluateStat(StatId stat, float baseline)
        {
            if (statModifiers == null) statModifiers = GetComponent<PlayerStatModifiers>();
            return statModifiers != null ? statModifiers.Evaluate(stat, baseline) : baseline;
        }
        internal float GravityAcceleration => gravity;
        internal float TerminalFallSpeed => terminalFallSpeed;
        internal float GroundStickSpeed => groundStickSpeed;
        internal float CrouchHeight => Mathf.Clamp(crouchHeight, controller.radius * 2f, standingHeight);

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            parkour = GetComponent<PlayerParkour>();
            stamina = GetComponent<PlayerStamina>();
            if (playerCamera == null)
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                if (childCamera != null) playerCamera = childCamera.transform;
            }
            if (playerCamera == null)
            {
                Debug.LogError("FirstPersonController requires a child camera.", this);
                enabled = false;
                return;
            }
            pitch = Mathf.DeltaAngle(0f, playerCamera.localEulerAngles.x);
            standingHeight = controller.height;
            standingCenter = controller.center;
            standingStepOffset = controller.stepOffset;
            standingCameraPosition = transform.InverseTransformPoint(playerCamera.position);
            airborneMoveSpeed = EffectiveWalkSpeed;

            // Never enable/disable the shared asset: the UI and other players may use it.
            InputActionAsset source = inputActions != null ? inputActions : InputSystem.actions;
            if (source == null)
            {
                Debug.LogError("FirstPersonController requires an Input Actions asset.", this);
                enabled = false;
                return;
            }
            runtimeActions = Instantiate(source);
            gameplayMap = runtimeActions.FindActionMap(playerActionMap);
            moveAction = gameplayMap?.FindAction(moveActionName);
            lookAction = gameplayMap?.FindAction(lookActionName);
            sprintAction = gameplayMap?.FindAction(sprintActionName);
            slowWalkAction = gameplayMap?.FindAction(slowWalkActionName);
            if (slowWalkAction != null) slowWalkAction.wantsInitialStateCheck = true;
            crouchAction = gameplayMap?.FindAction(crouchActionName);
            jumpAction = gameplayMap?.FindAction(jumpActionName);
            rollAction = gameplayMap?.FindAction(rollActionName);
            proneAction = gameplayMap?.FindAction(proneActionName);
            if (parkour != null && rollAction == null || proneEnabled && proneAction == null)
                Debug.LogWarning("Player Input Actions are missing Roll or Prone. Restore those actions/bindings to enable traversal controls.", this);
            if (moveAction == null || lookAction == null || sprintAction == null || crouchAction == null || jumpAction == null)
            {
                Debug.LogError("FirstPersonController: a required movement action is missing. Check the Input Actions fields.", this);
                enabled = false;
                return;
            }
            sprintAction.wantsInitialStateCheck = true;
            crouchAction.wantsInitialStateCheck = true;
            releaseCursorAction = releaseCursorInput.Clone();
            captureCursorAction = captureCursorInput.Clone();
        }

        private void OnEnable()
        {
            if (gameplayMap == null) return;
            releaseCursorAction.Enable();
            captureCursorAction.Enable();
            SetCursorCaptured(captureCursorOnEnable && HasFocus);
        }

        private void OnDisable()
        {
            gameplayMap?.Disable();
            releaseCursorAction?.Disable();
            captureCursorAction?.Disable();
            SetCursorCaptured(false);
        }

        private void OnDestroy()
        {
            releaseCursorAction?.Dispose();
            captureCursorAction?.Dispose();
            if (runtimeActions != null) Destroy(runtimeActions);
        }

        private bool HasFocus => !requireApplicationFocus || Application.isFocused;

        private void OnApplicationFocus(bool focused)
        {
            // Regaining focus requires an explicit click so returning to the Editor is safe.
            if (!focused) SetCursorCaptured(false);
        }

        /// <summary>Menus disable gameplay input here while keeping UI actions available.</summary>
        public void SetGameplayInputEnabled(bool value)
        {
            gameplayInputEnabled = value;
            if (!value) lastJumpPress = float.NegativeInfinity;
            if (!isActiveAndEnabled) return;
            SetCursorCaptured(value && captureCursorOnEnable && HasFocus);
        }

        private void Update()
        {
            if (!IsAlive)
            {
                IsSprinting = false; IsSlowWalking = false;
                if (Time.deltaTime > 0f && controller.enabled)
                {
                    verticalVelocity = controller.isGrounded ? -groundStickSpeed
                        : Mathf.Max(-terminalFallSpeed, verticalVelocity - gravity * Time.deltaTime);
                    controller.Move(Vector3.up * (verticalVelocity * Time.deltaTime));
                }
                return;
            }
            if (releaseCursorAction.WasPressedThisFrame())
                SetCursorCaptured(false);
            else if (gameplayInputEnabled && HasFocus && captureCursorAction.WasPressedThisFrame())
                SetCursorCaptured(true);

            bool acceptingInput = gameplayInputEnabled && HasFocus
                && (!requireCursorCapture || Cursor.lockState == CursorLockMode.Locked);
            if (!acceptingInput) lastJumpPress = float.NegativeInfinity;
            IsSprinting = false; IsSlowWalking = false;
            if (stamina != null) stamina.Tick(Time.deltaTime, acceptingInput);
            if (acceptingInput != gameplayMap.enabled)
            {
                if (acceptingInput) EnableGameplayMap();
                else gameplayMap.Disable();
                skipLook = true;
            }
            if (Time.deltaTime <= 0f)
            {
                lastJumpPress = float.NegativeInfinity;
                proneToggleArmed = crouchToggleArmed = false;
                return;
            }
            Vector2 movement = acceptingInput && movementEnabled
                ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
            if (parkour == null || !parkour.OwnsMovement) UpdateCrouch(acceptingInput);
            IsSlowWalking = acceptingInput && !IsCrouching && !IsProne && slowWalkAction != null && slowWalkAction.IsPressed();
            bool running = !IsSlowWalking && acceptingInput && sprintEnabled && !IsCrouching && !IsProne && sprintAction.IsPressed()
                && (stamina == null || stamina.CanSprint);
            bool jumpPressed = acceptingInput && !skipJumpInput && jumpAction.WasPressedThisFrame();
            bool crouchPressed = acceptingInput && crouchAction.WasPressedThisFrame();
            bool rollPressed = acceptingInput && rollAction != null && rollAction.WasPressedThisFrame();
            skipJumpInput = false;

            if (acceptingInput && lookEnabled && !skipLook)
            {
                Vector2 look = lookAction.ReadValue<Vector2>();
                bool pointerLook = lookAction.activeControl?.device is Pointer;
                look *= pointerLook ? mouseSensitivity : stickLookSpeed * Time.deltaTime;
                look.x *= horizontalLookMultiplier;
                look.y *= verticalLookMultiplier * (invertVerticalLook ? -1f : 1f);
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -verticalLookLimit, verticalLookLimit);
                playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            skipLook = false;

            Vector3 desiredVelocity = (transform.right * movement.x + transform.forward * movement.y)
                * (IsProne ? crawlSpeed : IsCrouching ? crouchSpeed : running ? EffectiveRunSpeed : IsSlowWalking ? slowWalkSpeed : EffectiveWalkSpeed);
            if (parkour != null && parkour.Tick(Time.deltaTime, acceptingInput, movement, desiredVelocity,
                jumpPressed, crouchPressed, rollPressed))
            {
                lastJumpPress = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                wasGrounded = false;
                return;
            }
            if (jumpPressed) lastJumpPress = Time.time;
            bool grounded = IsGrounded;
            if (grounded) lastGroundedTime = Time.time;
            bool bufferedJump = Time.time - lastJumpPress <= jumpBufferTime;
            if (jumpEnabled && !IsCrouching && !IsProne && bufferedJump && Time.time - lastGroundedTime <= coyoteTime
                && Time.time - lastJumpTime >= jumpCooldown && (stamina == null || stamina.TrySpend(stamina.JumpCost)))
            {
                // Use real take-off momentum: pressing Sprint after leaving the floor cannot create a running jump.
                if (grounded) planarVelocity = Vector3.ProjectOnPlane(controller.velocity, Vector3.up);
                airborneMoveSpeed = Mathf.Clamp(Mathf.Max(EffectiveWalkSpeed, planarVelocity.magnitude), 0f, maxAirSpeed);
                verticalVelocity = Mathf.Sqrt(2f * gravity * EffectiveJumpHeight);
                lastJumpTime = Time.time;
                lastJumpPress = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                grounded = false;
                Jumped?.Invoke();
            }
            if (grounded)
            {
                planarVelocity = desiredVelocity;
                airborneMoveSpeed = Mathf.Clamp(Mathf.Max(EffectiveWalkSpeed, planarVelocity.magnitude), 0f, maxAirSpeed);
            }
            else if (acceptingInput)
            {
                Vector3 airTarget = desiredVelocity.sqrMagnitude > .001f ? desiredVelocity.normalized * airborneMoveSpeed : Vector3.zero;
                planarVelocity = Vector3.MoveTowards(planarVelocity, airTarget,
                    (airTarget.sqrMagnitude > 0f ? airAcceleration * airControl : airDrag) * Time.deltaTime);
                planarVelocity = Vector3.ClampMagnitude(planarVelocity, maxAirSpeed);
            }
            else planarVelocity = Vector3.zero;

            if (grounded && verticalVelocity < 0f) verticalVelocity = -groundStickSpeed;
            float acceleration = gravity * (verticalVelocity < 0f && !grounded ? fallingGravityMultiplier : 1f);
            verticalVelocity = Mathf.Max(verticalVelocity - acceleration * Time.deltaTime, -terminalFallSpeed);
            float impactSpeed = Mathf.Max(0f, -verticalVelocity);
            Vector3 velocity = planarVelocity;
            velocity.y = verticalVelocity;
            CollisionFlags collisions = controller.Move(velocity * Time.deltaTime);
            IsSprinting = running && grounded && new Vector2(controller.velocity.x, controller.velocity.z).magnitude > .1f;
            if (IsSprinting && stamina != null) stamina.DrainSprint(Time.deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            bool landed = !wasGrounded && (collisions & CollisionFlags.Below) != 0 && verticalVelocity <= 0f;
            wasGrounded = (collisions & CollisionFlags.Below) != 0 && verticalVelocity <= 0f;
            if (landed && parkour != null) parkour.NotifyLanded(impactSpeed);
            if (landed) ReportLanding(impactSpeed);
        }

        internal void ReportLanding(float impactSpeed) => Landed?.Invoke(impactSpeed);

        public void SetAlive(bool alive)
        {
            IsAlive = alive;
            if (!alive)
            {
                if (parkour != null) parkour.ResetTraversal();
                ReleaseParkour(Vector3.zero);
                IsSprinting = false; IsSlowWalking = false;
            }
            SetCursorCaptured(alive && gameplayInputEnabled && captureCursorOnEnable && HasFocus);
        }

        public void ResetForRespawn(Vector3 position, Quaternion rotation)
        {
            if (parkour != null) parkour.ResetTraversal();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
            controller.height = standingHeight; controller.center = standingCenter; controller.stepOffset = standingStepOffset;
            playerCamera.position = transform.TransformPoint(standingCameraPosition);
            playerCamera.localRotation = Quaternion.identity;
            pitch = 0f; IsStandingBlocked = false; IsSprinting = false; IsSlowWalking = false;
            crouchRequested = proneRequested = crouchToggleArmed = proneToggleArmed = false;
            lastJumpTime = float.NegativeInfinity;
            ReleaseParkour(Vector3.zero, -groundStickSpeed);
            skipLook = skipJumpInput = skipCrouchInput = true;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        internal float SavedViewPitch => pitch;
        internal float SavedPoseHeight(bool prone, bool crouched) => prone ? Mathf.Clamp(proneHeight, controller.radius*2f, CrouchHeight) : crouched ? CrouchHeight : StandingHeight;
        internal void RestoreSavedPose(Vector3 position, Quaternion rotation, float savedPitch, bool prone, bool crouched)
        {
            ResetForRespawn(position, rotation);
            proneRequested = prone; crouchRequested = crouched && !prone;
            SetParkourPosture(SavedPoseHeight(prone, crouched), prone ? proneCameraDrop : crouched ? crouchCameraDrop : 0);
            pitch = Mathf.Clamp(savedPitch, -verticalLookLimit, verticalLookLimit);
            playerCamera.localRotation = Quaternion.Euler(pitch, 0, 0);
            SetAlive(true); Physics.SyncTransforms();
        }
        private void UpdateCrouch(bool acceptingInput)
        {
            // Menus/focus loss must not unexpectedly change the player's posture.
            if (!acceptingInput) return;
            // Let the Input System finish its initial state check before reading a re-enabled action.
            if (skipCrouchInput)
            {
                skipCrouchInput = false;
                return;
            }
            if (!crouchEnabled) crouchRequested = false;
            else if (toggleCrouch)
            {
                if (!crouchAction.IsPressed()) crouchToggleArmed = true;
                if (crouchToggleArmed && crouchAction.WasPressedThisFrame())
                {
                    crouchRequested = !crouchRequested;
                    crouchToggleArmed = false;
                }
            }
            else crouchRequested = crouchAction.IsPressed();

            if (proneAction != null)
            {
                if (!proneAction.IsPressed()) proneToggleArmed = true;
                if (proneEnabled && proneToggleArmed && proneAction.WasPressedThisFrame() && (proneRequested || IsGrounded))
                {
                    proneRequested = !proneRequested;
                    if (proneRequested) crouchRequested = false;
                    proneToggleArmed = false;
                }
            }
            if (!proneEnabled || crouchAction.WasPressedThisFrame()) proneRequested = false;

            float lowHeight = Mathf.Clamp(crouchHeight, controller.radius * 2f, standingHeight);
            float crawlHeight = Mathf.Clamp(proneHeight, controller.radius * 2f, lowHeight);
            IsStandingBlocked = controller.height < standingHeight - .001f && !CanFitHeight(standingHeight);
            float targetHeight = proneRequested ? crawlHeight : crouchRequested ? lowHeight : standingHeight;
            if (targetHeight > controller.height && !CanFitHeight(targetHeight))
                targetHeight = !proneRequested && CanFitHeight(lowHeight) ? lowHeight : Mathf.Min(crawlHeight, controller.height);
            float height = Mathf.MoveTowards(controller.height, targetHeight,
                (IsProne || targetHeight < lowHeight ? proneTransitionSpeed : crouchTransitionSpeed) * Time.deltaTime);
            float bottom = standingCenter.y - standingHeight * 0.5f;
            // Lower step offset before shrinking to keep it within the controller's valid range.
            controller.stepOffset = IsStandingBlocked || proneRequested || height < lowHeight - .05f ? 0f : Mathf.Min(standingStepOffset, height);
            controller.height = height;
            controller.center = new Vector3(standingCenter.x, bottom + height * 0.5f, standingCenter.z);

            float fraction = standingHeight > lowHeight ? (standingHeight - height) / (standingHeight - lowHeight) : 0f;
            float cameraDrop = height >= lowHeight ? crouchCameraDrop * fraction
                : Mathf.Lerp(crouchCameraDrop, proneCameraDrop, Mathf.InverseLerp(lowHeight, crawlHeight, height));
            Vector3 cameraPosition = standingCameraPosition;
            float clearance = Mathf.Min(cameraHeadClearance, height * 0.5f);
            cameraPosition.y = Mathf.Clamp(standingCameraPosition.y - cameraDrop,
                bottom + clearance, bottom + height - clearance);
            playerCamera.position = transform.TransformPoint(cameraPosition);
        }

        internal void SetParkourPosture(float height, float cameraDrop)
        {
            height = Mathf.Clamp(height, controller.radius * 2f, standingHeight);
            controller.stepOffset = Mathf.Min(standingStepOffset, height);
            controller.height = height;
            controller.center = new Vector3(standingCenter.x, BottomOffset + height * .5f, standingCenter.z);
            Vector3 position = standingCameraPosition;
            position.y = Mathf.Clamp(standingCameraPosition.y - cameraDrop, BottomOffset + .1f, BottomOffset + height - .1f);
            playerCamera.position = transform.TransformPoint(position);
        }

        internal void ReleaseParkour(Vector3 horizontalVelocity, float fallSpeed = 0f)
        {
            planarVelocity = Vector3.ProjectOnPlane(horizontalVelocity, Vector3.up);
            airborneMoveSpeed = Mathf.Max(EffectiveWalkSpeed, planarVelocity.magnitude);
            verticalVelocity = fallSpeed;
            lastJumpPress = lastGroundedTime = float.NegativeInfinity;
            wasGrounded = false;
        }

        internal void AddParkourCameraTilt(float angle)
        {
            playerCamera.localRotation = Quaternion.Euler(pitch, 0f, angle);
        }

        internal bool CanFitHeight(float targetHeight)
        {
            if (targetHeight <= controller.height + .001f) return true;
            Vector3 scale = transform.lossyScale;
            float verticalScale = Mathf.Abs(scale.y);
            float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            // Respect CharacterController's skin tolerance, especially beside walls.
            float radius = Mathf.Max(0.001f, (controller.radius - controller.skinWidth) * radiusScale);
            float bottom = standingCenter.y - standingHeight * 0.5f;
            Vector3 capsuleBottom = transform.TransformPoint(new Vector3(standingCenter.x, bottom, standingCenter.z));
            Vector3 lower = capsuleBottom + transform.up * Mathf.Max(radius, controller.height * verticalScale - radius);
            Vector3 upper = capsuleBottom + transform.up * Mathf.Max(radius, targetHeight * verticalScale - radius + standingClearance);
            int count = Physics.OverlapCapsuleNonAlloc(lower, upper, radius, standingOverlaps,
                standingObstructionMask, QueryTriggerInteraction.Ignore);
            // Be conservative if an unusually crowded area fills the buffer.
            if (count == standingOverlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                if (!standingOverlaps[i].transform.IsChildOf(transform)) return false;
            }
            return true;
        }

        private void SetCursorCaptured(bool captured)
        {
            captured &= IsAlive;
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
            skipLook = true;
            if (gameplayMap == null) return;
            if (IsAlive && isActiveAndEnabled && gameplayInputEnabled && HasFocus && (!requireCursorCapture || captured))
                EnableGameplayMap();
            else gameplayMap.Disable();
        }

        private void EnableGameplayMap()
        {
            if (!IsAlive || gameplayMap.enabled) return;
            // Initial state checks must not turn a held toggle button into a new press after a menu.
            crouchToggleArmed = false;
            proneToggleArmed = false;
            skipCrouchInput = true;
            skipJumpInput = true;
            gameplayMap.Enable();
        }
    }
}
