using System;
using UnityEngine;
using ZombieSurvival.Services;

namespace ZombieSurvival.Persistence
{
    [DisallowMultipleComponent, RequireComponent(typeof(Trader))]
    public sealed class WorldTraderSaveAdapter : WorldSaveAdapter
    {
        [Serializable] private sealed class Row { public string profile, storage; public bool initialized; }
        [Serializable] private sealed class State { public string selected; public Row[] profiles; }
        public override string Key => "trader";
        public override string Capture()
        {
            var trader = GetComponent<Trader>(); trader.ValidateConfiguration();
            if (trader.Initializing) throw new InvalidOperationException("Начальная выдача торговца ещё не завершена.");
            var state = new State { selected = trader.Profile.Id, profiles = new Row[trader.Stocks.Count] };
            for (int i = 0; i < state.profiles.Length; i++)
            {
                var row = trader.Stocks[i]; var id = row.storage.GetComponent<SaveIdentity>();
                if (id == null || string.IsNullOrWhiteSpace(id.Id)) throw new InvalidOperationException("Запасу торговца нужен постоянный SaveIdentity.");
                state.profiles[i] = new Row { profile = row.profile.Id, storage = id.Id, initialized = row.initialized };
            }
            return JsonUtility.ToJson(state);
        }
        public override void Validate(string json, SavedWorldObject owner)
        {
            var trader = GetComponent<Trader>(); trader.ValidateConfiguration(); var state = Read<State>(json);
            if (trader.Initializing || state.profiles == null || state.profiles.Length != trader.Stocks.Count)
                throw new InvalidOperationException("Неполные метаданные торговца.");
            var ids = new System.Collections.Generic.HashSet<string>(); bool selected = false;
            foreach (var saved in state.profiles)
            {
                if (saved == null || !saved.initialized || !ids.Add(saved.profile ?? ""))
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
                if (row.profile.Id == state.selected) trader.RestoreProfile(row.profile);
            }
        }
        public override void Publish() => GetComponent<Trader>().InvalidateCatalog();
    }
}
