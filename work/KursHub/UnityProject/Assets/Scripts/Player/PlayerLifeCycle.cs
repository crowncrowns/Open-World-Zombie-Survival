using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(FirstPersonController))]
    public sealed class PlayerLifeCycle : MonoBehaviour
    {
        [Header("Возрождение")]
        [Tooltip("Необязательная точка возрождения сцены. Без неё используется стартовая позиция игрока.")]
        [SerializeField] private Transform respawnPoint;
        [Tooltip("Время после смерти до разрешённого возрождения, с.")]
        [SerializeField, Min(0f)] private float respawnDelay = 1.5f;
        [Tooltip("Доля здоровья после возрождения от 0 до 1.")]
        [SerializeField, Range(.01f, 1f)] private float respawnHealthFraction = 1f;
        [Tooltip("Защита от урона после возрождения, с.")]
        [SerializeField, Min(0f)] private float spawnProtectionSeconds = 1.5f;
        [Tooltip("Восстанавливать стартовый запас выносливости при возрождении.")]
        [SerializeField] private bool resetStaminaOnRespawn = true;
        [Tooltip("Слои препятствий для проверки свободного места перед возрождением.")]
        [SerializeField] private LayerMask spawnObstructionMask = ~0;
        [Tooltip("Принимать ввод только когда окно игры активно.")]
        [SerializeField] private bool requireApplicationFocus = true;
        [Tooltip("Команды возрождения, по умолчанию R и Start на геймпаде.")]
        [SerializeField] private InputAction respawnInput = CreateRespawnInput();
        [Tooltip("Событие успешного возрождения после восстановления позы и показателей.")]
        [SerializeField] private UnityEvent onRespawned = new UnityEvent();
        private FirstPersonController motor;
        private CharacterController capsule;
        private Health health;
        private InputAction action;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private float canRespawnAt;
        private bool armed;
        private readonly Collider[] overlaps = new Collider[32];
        public event System.Action Respawned;
        public bool IsDead => health != null && health.IsDead;
        public float RespawnWait => Mathf.Max(0f, canRespawnAt - Time.time);
        public string RespawnError { get; private set; }
        public string BindingLabel => action != null ? action.GetBindingDisplayString() : "R";

        private static InputAction CreateRespawnInput()
        {
            var input = new InputAction("Respawn", InputActionType.Button, "<Keyboard>/r");
            input.AddBinding("<Gamepad>/start");
            return input;
        }

        private void Awake()
        {
            motor = GetComponent<FirstPersonController>(); capsule = GetComponent<CharacterController>(); health = GetComponent<Health>();
            initialPosition = transform.position; initialRotation = transform.rotation;
            action = respawnInput.Clone();
        }
        private void OnEnable() { health.Died += OnDeath; action.Enable(); if (health.IsDead) OnDeath(); }
        private void OnDisable() { health.Died -= OnDeath; action.Disable(); }
        private void OnDestroy() => action?.Dispose();
        private void OnDeath()
        {
            canRespawnAt = Time.time + respawnDelay; armed = false; RespawnError = "";
            motor.SetAlive(false);
        }
        private void Update()
        {
            if (!IsDead || Time.deltaTime <= 0f || requireApplicationFocus && !Application.isFocused) return;
            if (!action.IsPressed()) armed = true;
            if (armed && action.WasPressedThisFrame()) { armed = false; TryRespawn(); }
        }
        public bool TryRespawn()
        {
            if (!IsDead || RespawnWait > 0f || Time.deltaTime <= 0f) return false;
            Vector3 position = respawnPoint != null ? respawnPoint.position : initialPosition;
            Quaternion rotation = respawnPoint != null ? respawnPoint.rotation : initialRotation;
            if (!SpawnIsFree(position))
            {
                RespawnError = "Точка возрождения занята. Освободи её и повтори.";
                return false;
            }
            motor.ResetForRespawn(position, rotation);
            if (resetStaminaOnRespawn && motor.Stamina != null) motor.Stamina.ResetReserve();
            health.Revive(respawnHealthFraction, spawnProtectionSeconds);
            motor.SetAlive(true);
            RespawnError = ""; armed = false; onRespawned.Invoke(); Respawned?.Invoke();
            return true;
        }
        private bool SpawnIsFree(Vector3 position)
        {
            float radius = Mathf.Max(.01f, capsule.radius - capsule.skinWidth);
            Vector3 feet = position + Vector3.up * motor.BottomOffset;
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * (radius + .02f),
                feet + Vector3.up * (motor.StandingHeight - radius), radius, overlaps, spawnObstructionMask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (!overlaps[i].transform.IsChildOf(transform)) return false;
            return true;
        }
    }
}
