using System;
using UnityEngine;
using ZombieSurvival.Economy;

namespace ZombieSurvival.Persistence
{
    [DisallowMultipleComponent, RequireComponent(typeof(Wallet))]
    public sealed class WorldWalletSaveAdapter : WorldSaveAdapter
    {
        [Serializable] private sealed class State { public long reserve; public bool infinite, hasBudget; }
        private bool modeChanged, reserveChanged;
        public override string Key => "wallet";
        public override string Capture()
        {
            var wallet = GetComponent<Wallet>(); var budget = GetComponent<TraderBudget>();
            // Сначала инициализируем адаптер режима; в файле — конечный резерв даже у infinite-источника.
            bool infinite = budget != null ? budget.IsInfinite : wallet.IsInfinite;
            return JsonUtility.ToJson(new State { reserve = wallet.Balance, infinite = infinite, hasBudget = budget != null });
        }
        public override void Validate(string json, SavedWorldObject owner)
        {
            var state = Read<State>(json);
            if (state.reserve < 0 || state.hasBudget != (GetComponent<TraderBudget>() != null) || state.infinite && !state.hasBudget)
                throw new InvalidOperationException("Некорректный кошелёк/режим бюджета: " + owner.id);
        }
        public override void ApplySilent(string json)
        {
            var state = Read<State>(json); var wallet = GetComponent<Wallet>(); modeChanged = wallet.IsInfinite != state.infinite; reserveChanged=wallet.Balance!=state.reserve;
            wallet.RestoreSavedState(state.reserve, state.infinite); GetComponent<TraderBudget>()?.MarkSavedModeInitialized();
        }
        public override void Publish()
        { if(reserveChanged)GetComponent<Wallet>().NotifySavedRestore(); if (modeChanged) GetComponent<TraderBudget>()?.NotifySavedMode(); }
    }
}
