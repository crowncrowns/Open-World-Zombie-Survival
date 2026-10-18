using System;
using System.Globalization;
using UnityEngine;

namespace ZombieSurvival.Economy
{
    /// <summary>Whole rubles, separate from inventory. One monetary contract for actors and test budgets.</summary>
    [DisallowMultipleComponent]
    public sealed class Wallet : MonoBehaviour
    {
        [SerializeField, InspectorName("Стартовый баланс, ₽"), Min(0), Tooltip("Начальный баланс новой игровой сессии, целые рубли. По умолчанию 0; enable/открытие UI его не начисляют повторно.")]
        private long startingBalance;
        private long balance;
        private bool initialized, infinite;
        public event Action Changed;
        public long Balance { get { EnsureInitialized(); return balance; } }
        public bool IsInfinite { get { EnsureInitialized(); return infinite; } }
        public string FormattedBalance => IsInfinite ? "∞ ₽" : FormatRubles(Balance);
        public static string FormatRubles(long value) => value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU")).Replace('\u00a0',' ') + " ₽";
        private void Awake() => EnsureInitialized();
        private void OnEnable() => EnsureInitialized();
        private void EnsureInitialized() { if (!initialized) { balance = Math.Max(0L, startingBalance); initialized = true; } }
        internal void SetInfiniteMode(bool value) { EnsureInitialized(); infinite = value; }
        internal void InitializeForNewPlay() { balance = Math.Max(0L, startingBalance); initialized = true; infinite = false; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreForPlay()
        {
            // Explicit ordering also works with both Domain Reload and Scene Reload disabled.
            foreach (var wallet in UnityEngine.Object.FindObjectsByType<Wallet>(FindObjectsInactive.Include)) wallet.InitializeForNewPlay();
            foreach (var budget in UnityEngine.Object.FindObjectsByType<TraderBudget>(FindObjectsInactive.Include)) budget.InitializeForNewPlay();
        }
        public bool CanCredit(long amount) { EnsureInitialized(); return isActiveAndEnabled && amount > 0 && (infinite || amount <= long.MaxValue - balance); }
        public bool CanDebit(long amount) { EnsureInitialized(); return isActiveAndEnabled && amount > 0 && (infinite || balance >= amount); }
        public bool TryCredit(long amount)
        {
            if (!CanCredit(amount)) return false;
            if (!infinite) { balance += amount; Changed?.Invoke(); }
            return true;
        }
        public bool TryDebit(long amount)
        {
            if (!CanDebit(amount)) return false;
            if (!infinite) { balance -= amount; Changed?.Invoke(); }
            return true;
        }
        public void Credit(long amount)
        { if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount)); if (!TryCredit(amount)) throw new InvalidOperationException("Wallet disabled or balance would overflow."); }
        public void Debit(long amount)
        { if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount)); if (!TryDebit(amount)) throw new InvalidOperationException("Wallet disabled or insufficient rubles."); }
        public bool TryTransferTo(Wallet recipient, long amount)
        {
            if (recipient == null || recipient == this || !CanDebit(amount) || !recipient.CanCredit(amount)) return false;
            // Commit both finite reserves first; infinite endpoints are explicit sources/sinks, not long.MaxValue balances.
            bool debited = !infinite, credited = !recipient.infinite;
            if (debited) balance -= amount;
            if (credited) recipient.balance += amount;
            if (debited) Changed?.Invoke();
            if (credited) recipient.Changed?.Invoke();
            return true;
        }
    }
}

