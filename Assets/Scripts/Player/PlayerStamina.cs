using UnityEngine;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [Header("Reserve")]
        [SerializeField] private bool staminaEnabled = true;
        [SerializeField, Min(1f)] private float capacity = 100f;
        [SerializeField, Range(0f, 1f)] private float startingFraction = 1f;
        [SerializeField, Range(.01f, 1f)] private float sprintResumeFraction = .2f;
        [Header("Recovery")]
        [SerializeField, Min(0f)] private float recoveryPerSecond = 18f;
        [SerializeField, Min(0f)] private float recoveryDelay = 1.5f;
        [Header("Costs")]
        [SerializeField, Min(0f)] private float sprintPerSecond = 16f;
        [SerializeField, Min(0f)] private float jumpCost = 10f;
        [SerializeField, Min(0f)] private float climbCost = 16f;
        [SerializeField, Min(0f)] private float vaultCost = 8f;
        [SerializeField, Min(0f)] private float rollCost = 12f;

        private float current, recoveryWait;
        private bool exhausted;
        public float Current => current;
        public float Capacity => Mathf.Max(1f, capacity);
        public float Normalized => Mathf.Clamp01(current / Capacity);
        public bool IsExhausted => staminaEnabled && exhausted;
        public bool CanSprint => !isActiveAndEnabled || !staminaEnabled || !exhausted && current > 0f;
        public float JumpCost => jumpCost;
        public float ClimbCost => climbCost;
        public float VaultCost => vaultCost;
        public float RollCost => rollCost;

        private void Awake() => ResetReserve();

        public void ResetReserve()
        {
            current = Capacity * startingFraction;
            exhausted = current <= 0f;
            recoveryWait = 0f;
        }

        public bool CanSpend(float amount) => !isActiveAndEnabled || !staminaEnabled || current + .0001f >= Mathf.Max(0f, amount);

        public bool TrySpend(float amount)
        {
            if (!CanSpend(amount)) return false;
            if (!isActiveAndEnabled || !staminaEnabled || amount <= 0f) return true;
            Spend(amount);
            return true;
        }

        private void Spend(float amount)
        {
            current = Mathf.Max(0f, current - amount);
            recoveryWait = recoveryDelay;
            if (current <= .0001f) { current = 0f; exhausted = true; }
        }

        /// <summary>Called by the motor with scaled game time. Menus/focus loss freeze the reserve.</summary>
        public void Tick(float deltaTime, bool acceptingInput)
        {
            if (!isActiveAndEnabled || !staminaEnabled || !acceptingInput) return;
            current = Mathf.Clamp(current, 0f, Capacity);
            float remaining = Mathf.Max(0f, deltaTime - recoveryWait);
            recoveryWait = Mathf.Max(0f, recoveryWait - deltaTime);
            current = Mathf.Min(Capacity, current + recoveryPerSecond * remaining);
            if (exhausted && Normalized >= sprintResumeFraction) exhausted = false;
        }

        public void DrainSprint(float deltaTime)
        {
            if (isActiveAndEnabled && staminaEnabled && deltaTime > 0f) Spend(sprintPerSecond * deltaTime);
        }
    }
}
