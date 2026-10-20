using System;
using UnityEngine;
using ZombieSurvival.Services;

namespace ZombieSurvival.Persistence
{
    [DisallowMultipleComponent, RequireComponent(typeof(Trader))]
    public sealed class WorldTraderSaveAdapter : WorldSaveAdapter
    {
        [Serializable] private sealed class Row { public string profile, storage; public bool initialized; public double restockElapsed; }
        // Keep the outer block version: old trader JSON safely migrates clocks to zero without issuing goods.
        [Serializable] private sealed class State { public int economyVersion; public string selected; public Row[] profiles; }
        public override string Key => "trader";
        public override string Capture()
        {
            var trader = GetComponent<Trader>(); trader.ValidateConfiguration();
            if (trader.Initializing || Trader.RestockIsBusy) throw new InvalidOperationException("Выдача товаров торговца ещё не завершена.");
            var state = new State { economyVersion = 1, selected = trader.Profile.Id, profiles = new Row[trader.Stocks.Count] };
            for (int i = 0; i < state.profiles.Length; i++)
            {
                var row = trader.Stocks[i]; var id = row.storage.GetComponent<SaveIdentity>();
                if (id == null || string.IsNullOrWhiteSpace(id.Id)) throw new InvalidOperationException("Запасу торговца нужен постоянный SaveIdentity.");
                if (!ValidElapsed(row.restockElapsed)) throw new InvalidOperationException("Некорректный таймер пополнения торговца.");
                state.profiles[i] = new Row { profile = row.profile.Id, storage = id.Id, initialized = row.initialized, restockElapsed = row.restockElapsed };
            }
            return JsonUtility.ToJson(state);
        }
        public override void Validate(string json, SavedWorldObject owner)
        {
            var trader = GetComponent<Trader>(); trader.ValidateConfiguration(); var state = Read<State>(json);
            if (trader.Initializing || Trader.RestockIsBusy || state.economyVersion < 0 || state.economyVersion > 1 || state.profiles == null || state.profiles.Length != trader.Stocks.Count)
                throw new InvalidOperationException("Неполные метаданные торговца.");
            var ids = new System.Collections.Generic.HashSet<string>(); bool selected = false;
            foreach (var saved in state.profiles)
            {
                if (saved == null || !saved.initialized || !ids.Add(saved.profile ?? "") || !ValidElapsed(saved.restockElapsed))
                    throw new InvalidOperationException("Начальный запас торговца не завершён или повторяется профиль.");
                Trader.Stock match = null; foreach (var row in trader.Stocks) if (row.profile.Id == saved.profile) match = row;
                if (match == null || match.storage.GetComponent<SaveIdentity>()?.Id != saved.storage)
                    throw new InvalidOperationException("Изменился состав личных хранилищ торговца; нужна миграция.");
                selected |= saved.profile == state.selected;
            }
            if (!selected) throw new InvalidOperationException("Неизвестный выбранный профиль торговца.");
        }
        public override void ApplySilent(string json)
        {
            var trader = GetComponent<Trader>(); var state = Read<State>(json);
            foreach (var row in trader.Stocks)
            {
                row.initialized = true;
                foreach (var saved in state.profiles) if (saved.profile == row.profile.Id)
                    row.restockElapsed = state.economyVersion == 0 ? 0 : saved.restockElapsed;
                if (row.profile.Id == state.selected) trader.RestoreProfile(row.profile);
            }
        }
        private static bool ValidElapsed(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 1e12;
        public override void Publish() => GetComponent<Trader>().InvalidateCatalog();
    }
}
