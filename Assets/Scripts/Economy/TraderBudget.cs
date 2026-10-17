using System;
using UnityEngine;

namespace ZombieSurvival.Economy
{
    /// <summary>Budget policy only; no trader, catalog or duplicate currency balance.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Wallet))]
    public sealed class TraderBudget : MonoBehaviour
    {
        [SerializeField, Tooltip("Явный бесконечный тестовый режим на старте сессии. Конечный резерв Wallet заморожен, не пополняется и не обнуляется.")]
        private bool startInfinite;
        private Wallet wallet;
        private bool modeInitialized;
        public event Action ModeChanged;
        public Wallet Funds { get { Initialize(); return wallet; } }
        public bool IsInfinite => Funds.IsInfinite;
        private void OnEnable() => Initialize();
        private void Initialize()
        {
            if (wallet == null) wallet = GetComponent<Wallet>();
            if (!modeInitialized) { wallet.SetInfiniteMode(startInfinite); modeInitialized = true; }
        }
        internal void InitializeForNewPlay() { modeInitialized = false; Initialize(); }
        public void SetInfinite(bool value)
        {
            Initialize(); if (wallet.IsInfinite == value) return;
            wallet.SetInfiniteMode(value); ModeChanged?.Invoke();
        }
        public bool TryPay(Wallet recipient, long amount) => isActiveAndEnabled && Funds.TryTransferTo(recipient, amount);
        public bool TryReceiveFrom(Wallet payer, long amount) => isActiveAndEnabled && payer != null && payer.TryTransferTo(Funds, amount);
    }
}
