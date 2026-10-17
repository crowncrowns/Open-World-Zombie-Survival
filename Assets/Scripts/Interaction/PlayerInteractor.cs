using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Player;

namespace ZombieSurvival.Interaction
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Selection")]
        [Tooltip("Камера для выбора предметов лучом из центра экрана.")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("Максимальная дистанция взаимодействия от камеры, м.")]
        [SerializeField, Min(.1f)] private float interactionDistance = 2.5f;
        [Tooltip("Слои твёрдых объектов, перекрывающих взаимодействие. Предмет за стеной недоступен.")]
        [SerializeField] private LayerMask occlusionMask = ~0;
        [Tooltip("Слои интерактивных объектов для выбора лучом камеры.")]
        [SerializeField] private LayerMask targetMask = ~0;
        [Tooltip("Разрешить взаимодействие в воздухе.")]
        [SerializeField] private bool allowWhileAirborne;
        [Tooltip("Разрешить взаимодействие с миром лёжа.")]
        [SerializeField] private bool allowWhileProne = true;
        [Header("Input and Feedback")]
        [Tooltip("Имя команды взаимодействия в карте Player, по умолчанию Interact.")]
        [SerializeField] private string actionName = "Interact";
        [Tooltip("Минимальная пауза между взаимодействиями, с. Удержание кнопки не повторяет действие.")]
        [SerializeField, Min(0f)] private float repeatCooldown = .2f;
        [Tooltip("Время показа сообщения о результате взаимодействия, с.")]
        [SerializeField, Min(0f)] private float feedbackDuration = 1.5f;
        private FirstPersonController motor;
        private InputAction action;
        private readonly RaycastHit[] hits = new RaycastHit[48];
        private MonoBehaviour targetComponent;
        private IInteractable target;
        private bool armed;
        private float nextUse, feedbackUntil;
        private string feedback;
        public event System.Action SuccessfulInteraction;

        public IInteractable Target => targetComponent != null ? target : null;
        public bool CanUseTarget => Target != null && targetComponent.isActiveAndEnabled && target.CanInteract(this);
        public Camera ViewCamera => viewCamera;
        public bool IsAvailable => motor != null && motor.AcceptingGameplayInput && viewCamera != null
            && viewCamera.isActiveAndEnabled && (motor.Parkour == null || !motor.Parkour.OwnsMovement)
            && (allowWhileAirborne || motor.IsGrounded) && (allowWhileProne || !motor.IsProne);
        public string Feedback => IsAvailable && Time.time < feedbackUntil ? feedback : "";

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (motor == null) motor = GetComponent<FirstPersonController>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        }
        private void OnEnable() { ResolveReferences(); ResetRuntimeState(); }
        private void OnDisable() => ResetRuntimeState();

        private void ResetRuntimeState()
        {
            ClearTarget(); action = null; armed = false;
            nextUse = feedbackUntil = 0f; feedback = "";
        }

        public void ShowFeedback(string message) { feedback = message; feedbackUntil = Time.time + feedbackDuration; }

        public string BindingLabel
        {
            get
            {
                if (action == null) return "?";
                bool gamepad = action.activeControl?.device is Gamepad;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
                    if (binding.effectivePath.StartsWith(gamepad ? "<Gamepad>" : "<Keyboard>"))
                        return action.GetBindingDisplayString(i);
                }
                return action.GetBindingDisplayString();
            }
        }

        private void LateUpdate()
        {
            // Обновление префаба/скриптов в Editor может обнулить сериализованную камеру без нового Awake.
            // Карта команд принадлежит контроллеру: не удерживаем старую копию после её замены.
            ResolveReferences();
            var currentAction = motor != null ? motor.FindGameplayAction(actionName) : null;
            if (!ReferenceEquals(action, currentAction))
            {
                ResetRuntimeState(); action = currentAction;
            }
            if (!IsAvailable || action == null || !action.enabled)
            {
                ClearTarget(); armed = false; feedback = "";
                return;
            }
            // A held key after a menu/focus change must be released before it can interact.
            if (!action.IsPressed()) armed = true;
            SelectTarget();
            if (!armed || !action.WasPressedThisFrame()) return;
            armed = false;
            if (Time.time < nextUse || !CanUseTarget) return;
            // Selection was just recomputed from this frame's camera; no delayed/stale target callbacks.
            if (target.Interact(this)) { nextUse = Time.time + repeatCooldown; SuccessfulInteraction?.Invoke(); }
            if (IsAvailable) SelectTarget(); else ClearTarget();
        }

        private void ClearTarget() { targetComponent = null; target = null; }

        private void SelectTarget()
        {
            ClearTarget();
            int count = Physics.RaycastNonAlloc(viewCamera.transform.position, viewCamera.transform.forward,
                hits, interactionDistance, occlusionMask.value | targetMask.value, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return;
            float nearest = float.PositiveInfinity;
            Collider collider = null;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                nearest = hit.distance; collider = hit.collider;
            }
            // The first solid object occludes everything behind it, even when it is not interactive.
            if (collider == null || (targetMask.value & (1 << collider.gameObject.layer)) == 0) return;
            foreach (var behaviour in collider.GetComponentsInParent<MonoBehaviour>())
            {
                if (!(behaviour is IInteractable interactable)) continue;
                if (!behaviour.isActiveAndEnabled) return;
                targetComponent = behaviour; target = interactable;
                return;
            }
        }
    }
}
