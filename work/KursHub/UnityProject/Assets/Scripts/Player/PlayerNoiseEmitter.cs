using UnityEngine;
using ZombieSurvival.AI;
using ZombieSurvival.Interaction;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController), typeof(CharacterController))]
    public sealed class PlayerNoiseEmitter : MonoBehaviour
    {
        [Header("Ходьба")]
        [Tooltip("Расстояние между событиями шагов для обычной ходьбы, м. Шаги возникают только при реальном перемещении.")]
        [SerializeField, Min(.1f)] private float walkStepDistance = 1.8f;
        [Tooltip("Исходная громкость обычной ходьбы. Больше — легче услышать, включая шаги за спиной зомби.")]
        [SerializeField, Min(0f)] private float walkLoudness = 1f;
        [Tooltip("Предельная дальность шума обычной ходьбы, м. Итоговую слышимость также ограничивает порог слуха зомби.")]
        [SerializeField, Min(0f)] private float walkRange = 18f;
        [Header("Тихий шаг (Alt)")]
        [Tooltip("Расстояние между тихими шагами, м.")]
        [SerializeField, Min(.1f)] private float slowStepDistance = 1.6f;
        [Tooltip("Громкость тихого шага. Слышимость не зависит от направления взгляда зомби.")]
        [SerializeField, Min(0f)] private float slowLoudness = .3f;
        [Tooltip("Предельный радиус слышимости тихого шага, м.")]
        [SerializeField, Min(0f)] private float slowRange = 6f;
        [Header("Бег")]
        [Tooltip("Расстояние между событиями шагов для бега, м. Шаги возникают только при реальном перемещении.")]
        [SerializeField, Min(.1f)] private float runStepDistance = 2.2f;
        [Tooltip("Исходная громкость бега. Больше — легче услышать, включая шаги за спиной зомби.")]
        [SerializeField, Min(0f)] private float runLoudness = 3f;
        [Tooltip("Предельная дальность шума бега, м. Итоговую слышимость также ограничивает порог слуха зомби.")]
        [SerializeField, Min(0f)] private float runRange = 28f;
        [Header("Приседание")]
        [Tooltip("Расстояние между событиями шагов для ходьбы сидя, м. Шаги возникают только при реальном перемещении.")]
        [SerializeField, Min(.1f)] private float crouchStepDistance = 1.4f;
        [Tooltip("Исходная громкость ходьбы сидя. Больше — легче услышать, включая шаги за спиной зомби.")]
        [SerializeField, Min(0f)] private float crouchLoudness = .2f;
        [Tooltip("Предельная дальность шума ходьбы сидя, м. Итоговую слышимость также ограничивает порог слуха зомби.")]
        [SerializeField, Min(0f)] private float crouchRange = 5f;
        [Header("Ползание")]
        [Tooltip("Расстояние между событиями шагов для ползания, м. Шаги возникают только при реальном перемещении.")]
        [SerializeField, Min(.1f)] private float crawlStepDistance = 1f;
        [Tooltip("Исходная громкость ползания. Больше — легче услышать, включая шаги за спиной зомби.")]
        [SerializeField, Min(0f)] private float crawlLoudness = .08f;
        [Tooltip("Предельная дальность шума ползания, м. Итоговую слышимость также ограничивает порог слуха зомби.")]
        [SerializeField, Min(0f)] private float crawlRange = 3f;
        [Header("Действия (громкость / радиус)")]
        [Tooltip("Шум прыжка: X — исходная громкость, Y — предельный радиус слышимости в метрах.")]
        [SerializeField] private Vector2 jumpNoise = new Vector2(2f, 15f);
        [Tooltip("Шум переката: X — громкость, Y — максимальный радиус слышимости в метрах.")]
        [SerializeField] private Vector2 rollNoise = new Vector2(4f, 20f);
        [Tooltip("Шум подтягивания и преодоления: X — исходная громкость, Y — предельный радиус слышимости в метрах.")]
        [SerializeField] private Vector2 climbNoise = new Vector2(2f, 15f);
        [Tooltip("Шум взаимодействия: X — громкость, Y — максимальный радиус слышимости в метрах.")]
        [SerializeField] private Vector2 interactionNoise = new Vector2(.5f, 8f);
        [Tooltip("Шум приземления: X — исходная громкость, Y — предельный радиус слышимости в метрах.")]
        [SerializeField] private Vector2 landingNoise = new Vector2(4f, 24f);
        [Tooltip("Минимальная скорость падения, вызывающая шум приземления, м/с.")]
        [SerializeField, Min(0f)] private float minimumLandingSpeed = 4f;
        [Tooltip("Скорость падения, при которой шум приземления достигает полной громкости, м/с.")]
        [SerializeField, Min(.1f)] private float fullLandingNoiseSpeed = 12f;
        [Tooltip("Перемещение за кадр больше этого расстояния не считается шагами, м. Предотвращает шум телепортации.")]
        [SerializeField, Min(.1f)] private float ignoreTeleportDistance = 5f;
        public float CurrentLoudness { get; private set; }
        public float CurrentRange { get; private set; }
        public string CurrentNoiseLabel { get; private set; } = "Тишина";
        public string CurrentGait => movement == null || !movement.IsAlive ? "Тишина" : movement.IsProne ? "Ползание" : movement.IsCrouching ? "Крадётся" : movement.IsSprinting ? "Бег" : movement.IsSlowWalking ? "Тихий шаг" : "Шаг";
        private float noiseEnds;
        private CharacterController body;
        private FirstPersonController movement;
        private PlayerInteractor interactor;
        private Vector3 previousPosition;
        private float travelled;
        private int previousGait;
        private PlayerParkour.TraversalState previousTraversal;
        private void OnEnable() => Initialize();
        private void Initialize()
        {
            if (movement != null) { movement.Jumped -= OnJumped; movement.Landed -= OnLanded; }
            if (interactor != null) interactor.SuccessfulInteraction -= OnInteraction;
            body = GetComponent<CharacterController>(); movement = GetComponent<FirstPersonController>();
            interactor = GetComponent<PlayerInteractor>();
            movement.Jumped += OnJumped; movement.Landed += OnLanded;
            if (interactor != null) interactor.SuccessfulInteraction += OnInteraction;
            CurrentLoudness = CurrentRange = noiseEnds = 0f; CurrentNoiseLabel = "Тишина";
            previousPosition = transform.position; travelled = 0f; previousGait = -1;
            previousTraversal = PlayerParkour.TraversalState.None;
        }
        // Загрузка/телепорт не являются шагом: сбрасываем накопленный путь и старую подсказку HUD.
        internal void ResetAfterTeleport() => Initialize();
        private void OnDisable()
        {
            if (movement != null) { movement.Jumped -= OnJumped; movement.Landed -= OnLanded; }
            if (interactor != null) interactor.SuccessfulInteraction -= OnInteraction;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreForPlay()
        {
            foreach (var emitter in Object.FindObjectsByType<PlayerNoiseEmitter>(FindObjectsInactive.Include))
                if (emitter.isActiveAndEnabled) emitter.Initialize();
        }
        public void Emit(Vector2 profile, NoiseKind kind)
        {
            if (!isActiveAndEnabled || profile.x <= 0f || profile.y <= 0f) return;
            CurrentLoudness = profile.x; CurrentRange = profile.y; noiseEnds = Time.time + 1.1f;
            CurrentNoiseLabel = kind == NoiseKind.Footstep ? CurrentGait : kind == NoiseKind.Jump ? "Прыжок"
                : kind == NoiseKind.Landing ? "Приземление" : kind == NoiseKind.Roll ? "Перекат"
                : kind == NoiseKind.Climb ? "Паркур" : kind == NoiseKind.Interaction ? "Взаимодействие" : kind == NoiseKind.Gunshot ? "Выстрел" : kind == NoiseKind.Melee ? "Удар" : "Шум";
            GameplayNoise.Emit(transform.position, profile.x, profile.y, gameObject, kind);
        }
        private void OnJumped() { if (movement.IsAlive) Emit(jumpNoise, NoiseKind.Jump); }
        private void OnInteraction() { if (movement.IsAlive) Emit(interactionNoise, NoiseKind.Interaction); }
        private void OnLanded(float speed)
        {
            if (speed < minimumLandingSpeed) return;
            var profile = landingNoise; profile.x *= Mathf.Clamp01(speed / Mathf.Max(.1f, fullLandingNoiseSpeed));
            Emit(profile, NoiseKind.Landing);
        }
        private void LateUpdate()
        {
            if (Time.time >= noiseEnds || !movement.IsAlive) CurrentLoudness = CurrentRange = 0f;
            Vector3 delta = transform.position - previousPosition;previousPosition = transform.position;delta.y = 0f;
            var traversal = movement.Parkour != null ? movement.Parkour.State : PlayerParkour.TraversalState.None;
            if (movement.IsAlive && traversal != previousTraversal)
            {
                if (traversal == PlayerParkour.TraversalState.Rolling) Emit(rollNoise, NoiseKind.Roll);
                if (traversal == PlayerParkour.TraversalState.Climbing || traversal == PlayerParkour.TraversalState.Vaulting) Emit(climbNoise, NoiseKind.Climb);
            }
            previousTraversal = traversal;
            if (!movement.IsAlive || !movement.AcceptingGameplayInput || !body.isGrounded
                || traversal != PlayerParkour.TraversalState.None || delta.sqrMagnitude < .000001f
                || delta.sqrMagnitude > ignoreTeleportDistance * ignoreTeleportDistance)
            { travelled = 0f; return; }
            int gait = movement.IsProne ? 3 : movement.IsCrouching ? 2 : movement.IsSprinting ? 1 : movement.IsSlowWalking ? 4 : 0;
            if (gait != previousGait) { travelled = 0f; previousGait = gait; }
            travelled += delta.magnitude;
            float stride = gait == 4 ? slowStepDistance : gait == 3 ? crawlStepDistance : gait == 2 ? crouchStepDistance : gait == 1 ? runStepDistance : walkStepDistance;
            if (travelled < Mathf.Max(.1f, stride)) return;
            travelled %= Mathf.Max(.1f, stride);
            Emit(gait == 4 ? new Vector2(slowLoudness, slowRange) : gait == 3 ? new Vector2(crawlLoudness, crawlRange) : gait == 2 ? new Vector2(crouchLoudness, crouchRange)
                : gait == 1 ? new Vector2(runLoudness, runRange) : new Vector2(walkLoudness, walkRange), NoiseKind.Footstep);
        }
    }
}
