using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Economy;

namespace ZombieSurvival.Services
{
    /// <summary>One service point, shared immutable catalogs, private stock per NPC/profile. No transaction API.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(TraderServicePoint), typeof(TraderBudget))]
    public sealed class Trader : MonoBehaviour
    {
        [Serializable] public sealed class Stock
        {
            [Tooltip("Общий профиль ассортимента, постоянный ID.")] public TraderProfile profile;
            [Tooltip("Личное хранилище этого NPC/профиля; не разделять между торговцами.")] public ItemStorage storage;
            [SerializeField, HideInInspector] internal bool initialized;
        }
        [SerializeField, InspectorName("Тип торговца"), Tooltip("Выбранный профиль. Возврат к профилю не пополняет товары и бюджет.")] private TraderProfile activeProfile;
        [SerializeField, InspectorName("Личные запасы"), Tooltip("Постоянные личные хранилища профилей. Создаются настройкой компонента до игры, не при переключении.")] private Stock[] stocks = Array.Empty<Stock>();
        private TraderProfile observedProfile;
        private long catalogVersion = 1;
        private bool initializing;
        private bool subscribed;
        internal bool Initializing => initializing;
        public event Action CatalogChanged;
        public TraderProfile Profile => activeProfile;
        public long CatalogVersion => catalogVersion;
        public IReadOnlyList<Stock> Stocks => stocks;
        public ItemStorage ActiveStock
        {
            get { foreach (var row in stocks) if (row != null && row.profile == activeProfile) return row.storage; return null; }
        }
        private void Reset() => ConfigureDefaults();
        private void ConfigureDefaults()
        {
            if (stocks != null && stocks.Length > 0) return;
            var profiles = Resources.LoadAll<TraderProfile>("Trading");
            Array.Sort(profiles, (a, b) => string.CompareOrdinal(a.Id, b.Id));
            if (profiles.Length == 0) return;
            stocks = new Stock[profiles.Length];
            for (int i = 0; i < profiles.Length; i++)
            {
                var child = new GameObject("Stock " + profiles[i].Id); child.transform.SetParent(transform, false);
                stocks[i] = new Stock { profile = profiles[i], storage = child.AddComponent<ItemStorage>() };
            }
            activeProfile = Array.Find(profiles, p => p.Id == "weapons_ammo") ?? profiles[0];
            if (GetComponentInChildren<Collider>(true) == null)
            { var access = gameObject.AddComponent<BoxCollider>(); access.center = Vector3.up; access.size = new Vector3(1,2,1); }
        }
        private void OnEnable()
        {
            ConfigureDefaults(); observedProfile = activeProfile;
            if (Application.isPlaying) InitializeStock();
            Subscribe();
        }
        private void Subscribe()
        {
            if (subscribed) return;
            foreach (var row in stocks) if (row != null)
            {
                if (row.profile != null) row.profile.Changed += StockChanged;
                if (row.storage != null) row.storage.Changed += StockChanged;
            }
            subscribed = true;
        }
        private void OnDisable()
        {
            if (!subscribed) return;
            foreach (var row in stocks) if (row != null)
            {
                if (row.profile != null) row.profile.Changed -= StockChanged;
                if (row.storage != null) row.storage.Changed -= StockChanged;
            }
            subscribed = false;
        }
        private void StockChanged() { if (!initializing) InvalidateCatalog(); }
        private void Update()
        {
            if (activeProfile == observedProfile) return;
            if (!TrySelectProfile(activeProfile)) activeProfile = observedProfile;
        }
        public bool TrySelectProfile(TraderProfile profile)
        {
            if (initializing) return false;
            bool found = false; foreach (var row in stocks) if (row != null && row.profile == profile && profile != null) { found = true; break; }
            if (!found) return false;
            if (observedProfile == profile && activeProfile == profile) return true;
            activeProfile = observedProfile = profile; InvalidateCatalog(); return true;
        }
        public bool ValidateSelection(TraderProfile expectedProfile, long expectedVersion, out string reason)
        {
            try { ValidateConfiguration(); }
            catch (InvalidOperationException error) { reason = error.Message; return false; }
            if (Application.isPlaying) foreach (var row in stocks)
                if (!row.initialized) { reason = "Начальные запасы торговца ещё не готовы."; return false; }
            if (initializing || !isActiveAndEnabled || Profile != expectedProfile || catalogVersion != expectedVersion)
            { reason = "Ассортимент торговца изменился. Выберите товар заново."; return false; }
            reason = ""; return true;
        }
        public void ValidateConfiguration()
        {
            if (stocks == null || stocks.Length == 0 || stocks.Length > 16) throw new InvalidOperationException("Торговцу нужны настроенные профили/хранилища.");
            var ids = new HashSet<string>(); var stores = new HashSet<ItemStorage>(); bool selected = false;
            foreach (var row in stocks)
            {
                if (row == null || row.profile == null || row.storage == null || row.storage.transform == transform
                    || !row.storage.transform.IsChildOf(transform) || row.storage.GetComponentInParent<Trader>(true) != this
                    || !ids.Add(row.profile.Id) || !stores.Add(row.storage)) throw new InvalidOperationException("Неверное/общее хранилище профиля торговца.");
                row.profile.Validate(); selected |= row.profile == activeProfile;
            }
            if (!selected) throw new InvalidOperationException("Выбранный профиль не входит в личные хранилища торговца.");
        }
        public void InitializeStock()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Начальный запас создаётся только в Play, не запекается в общий prefab.");
            if (initializing) return;
            ValidateConfiguration();
            // Validate ALL rows before any creation; seeding is explicit, never inferred from an empty storage.
            foreach (var row in stocks)
            {
                if (row.initialized) continue;
                for (int i = 0; i < row.storage.SlotCount; i++)
                    if (row.storage.GetSlot(i) != null && !row.storage.GetSlot(i).IsEmpty)
                        throw new InvalidOperationException("Неначатый запас должен быть пустым; нельзя смешать авторские экземпляры с начальной выдачей.");
                int required = 0;
                foreach (var entry in row.profile.Catalog) required += entry.quantity / entry.item.MaxStack + (entry.quantity % entry.item.MaxStack == 0 ? 0 : 1);
                if (required > row.storage.SlotCount) throw new InvalidOperationException("Недостаточно слотов для начального каталога.");
            }
            initializing = true;
            try
            {
                foreach (var row in stocks)
                {
                    if (row.initialized) continue;
                    row.initialized = true;
                    foreach (var entry in row.profile.Catalog)
                        if (entry.quantity > 0 && !row.storage.TryAddAll(entry.item, entry.quantity)) throw new InvalidOperationException("Начальная выдача не прошла предварительно проверенную вместимость.");
                }
            }
            finally { initializing = false; }
        }
        internal void RestoreProfile(TraderProfile profile)
        { activeProfile = observedProfile = profile; }
        internal void InvalidateCatalog() { catalogVersion = catalogVersion == long.MaxValue ? 1 : catalogVersion + 1; CatalogChanged?.Invoke(); }
    }
}
