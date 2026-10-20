using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Economy;

namespace ZombieSurvival.Services
{
    /// <summary>One service point, shared immutable catalogs, private stock per NPC/profile. No transaction API.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(TraderServicePoint), typeof(TraderBudget), typeof(ServicePresentation))]
    public sealed class Trader : MonoBehaviour
    {
        [Serializable] public sealed class Stock
        {
            [Tooltip("Общий профиль ассортимента, постоянный ID.")] public TraderProfile profile;
            [Tooltip("Личное хранилище этого NPC/профиля; не разделять между торговцами.")] public ItemStorage storage;
            [SerializeField, HideInInspector] internal bool initialized;
            [SerializeField, HideInInspector] internal double restockElapsed;
        }
        [SerializeField, InspectorName("Тип торговца"), Tooltip("Выбранный профиль. Возврат к профилю не пополняет товары и бюджет.")] private TraderProfile activeProfile;
        [SerializeField, InspectorName("Личные запасы"), Tooltip("Постоянные личные хранилища профилей. Создаются настройкой компонента до игры, не при переключении.")] private Stock[] stocks = Array.Empty<Stock>();
        [SerializeField,InspectorName("Пополнять товары"),Tooltip("Пополнять каждый начатый профиль до количества каталога. Бюджет и существующие вещи не меняются.")] private bool restockEnabled=true;
        [SerializeField,Min(.01f),InspectorName("Интервал пополнения, с"),Tooltip("Интервал активной игры NPC, в игровых секундах. Пауза, выключенный NPC и время вне игры не учитываются.")] private float restockIntervalSeconds=600;
        [SerializeField,Range(0,10000),InspectorName("Наценка продажи, %"),Tooltip("Увеличение SellPercent профиля: ×(100+наценка)/100. На цену выкупа не влияет.")] private int saleMarkupPercent;
        [SerializeField,Range(0,100),InspectorName("Скидка продажи, %"),Tooltip("Скидка после наценки: ×(100−скидка)/100. Итоговый процент продажи не может быть ниже BuyPercent.")] private int saleDiscountPercent;
        public bool RestockEnabled=>restockEnabled;
        public float RestockIntervalSeconds=>restockIntervalSeconds;
        public int SaleMarkupPercent=>saleMarkupPercent;
        public int SaleDiscountPercent=>saleDiscountPercent;
        public static bool RestockIsBusy{get;private set;}
        private string observedSettings;
        internal string ExactSettingsStamp=>CreateSettingsStamp();
        private TraderProfile observedProfile;
        private long catalogVersion = 1;
        private bool initializing;
        private bool restocking;
        private bool subscribed;
        internal bool Initializing => initializing;
        public event Action CatalogChanged;
        public TraderProfile Profile => activeProfile;
        public long CatalogVersion { get { SynchronizeSettings();return catalogVersion; } }
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
            ConfigureDefaults(); observedProfile = activeProfile;observedSettings=CreateSettingsStamp();
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
        private void StockChanged() { if (!initializing&&!restocking) InvalidateCatalog(); }
        private void Update()
        {
            if(activeProfile!=observedProfile&&!TrySelectProfile(activeProfile))activeProfile=observedProfile;
            SynchronizeSettings();AdvanceRestock(Time.deltaTime);
        }
        public bool TrySelectProfile(TraderProfile profile)
        {
            if (initializing||RestockIsBusy) return false;
            bool found = false; foreach (var row in stocks) if (row != null && row.profile == profile && profile != null) { found = true; break; }
            if (!found) return false;
            if (observedProfile == profile && activeProfile == profile) return true;
            activeProfile = observedProfile = profile; InvalidateCatalog(); return true;
        }
        public bool ValidateSelection(TraderProfile expectedProfile, long expectedVersion, out string reason)
        {
            SynchronizeSettings();
            try { ValidateConfiguration(); }
            catch (InvalidOperationException error) { reason = error.Message; return false; }
            if (Application.isPlaying) foreach (var row in stocks)
                if (!row.initialized) { reason = "Начальные запасы торговца ещё не готовы."; return false; }
            if (initializing || RestockIsBusy || !isActiveAndEnabled || Profile != expectedProfile || catalogVersion != expectedVersion)
            { reason = "Ассортимент торговца изменился. Выберите товар заново."; return false; }
            reason = ""; return true;
        }
        public void ValidateConfiguration()
        {
            if(float.IsNaN(restockIntervalSeconds)||float.IsInfinity(restockIntervalSeconds)||restockIntervalSeconds<=0||saleMarkupPercent<0||saleMarkupPercent>10000||saleDiscountPercent<0||saleDiscountPercent>100)
                throw new InvalidOperationException("Некорректный интервал, наценка или скидка торговца.");
            if (stocks == null || stocks.Length == 0 || stocks.Length > 16) throw new InvalidOperationException("Торговцу нужны настроенные профили/хранилища.");
            var ids = new HashSet<string>(); var stores = new HashSet<ItemStorage>(); bool selected = false;
            foreach (var row in stocks)
            {
                if (row == null || row.profile == null || row.storage == null || row.storage.transform == transform
                    || !row.storage.transform.IsChildOf(transform) || row.storage.GetComponentInParent<Trader>(true) != this
                    || !ids.Add(row.profile.Id) || !stores.Add(row.storage)) throw new InvalidOperationException("Неверное/общее хранилище профиля торговца.");
                row.profile.Validate();if(double.IsNaN(row.restockElapsed)||double.IsInfinity(row.restockElapsed)||row.restockElapsed<0||row.restockElapsed>1e12)throw new InvalidOperationException("Некорректный прогресс пополнения.");
                if(!TryGetEffectiveSellPercent(row.profile,out _,out var priceReason))throw new InvalidOperationException(priceReason);selected |= row.profile == activeProfile;
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
        internal void InvalidateCatalog()
        {
            observedSettings=CreateSettingsStamp();catalogVersion=catalogVersion==long.MaxValue?1:catalogVersion+1;
            var listeners=CatalogChanged;if(listeners!=null)foreach(Action listener in listeners.GetInvocationList())try{listener();}catch(Exception error){Debug.LogException(error,this);}
        }
        private string CreateSettingsStamp()
        {
            string stamp=restockEnabled+"|"+restockIntervalSeconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+saleMarkupPercent+"|"+saleDiscountPercent+"|"+(activeProfile!=null?activeProfile.GetEntityId().ToString():"null");
            foreach(var row in stocks??Array.Empty<Stock>())stamp+="\n"+(row?.profile!=null?JsonUtility.ToJson(row.profile):"null");return stamp;
        }
        private void SynchronizeSettings(){var stamp=CreateSettingsStamp();if(observedSettings==null){observedSettings=stamp;return;}if(stamp!=observedSettings)InvalidateCatalog();}
        public bool TryGetEffectiveSellPercent(TraderProfile profile,out decimal percent,out string reason)
        {
            percent=0;reason="";
            if(profile==null||saleMarkupPercent<0||saleMarkupPercent>10000||saleDiscountPercent<0||saleDiscountPercent>100){reason="Некорректная наценка или скидка";return false;}
            try{profile.Validate();}catch(InvalidOperationException error){reason=error.Message;return false;}
            percent=(decimal)profile.SellPercent*(100+saleMarkupPercent)/100m*(100-saleDiscountPercent)/100m;
            if(percent<profile.BuyPercent){reason="Цена продажи после скидки ниже цены выкупа";return false;}return true;
        }
        public double GetRestockRemainingSeconds(TraderProfile profile)
        {
            if(!restockEnabled||float.IsNaN(restockIntervalSeconds)||float.IsInfinity(restockIntervalSeconds)||restockIntervalSeconds<=0)return double.PositiveInfinity;
            foreach(var row in stocks??Array.Empty<Stock>())if(row!=null&&row.profile==profile&&row.initialized)
                return double.IsNaN(row.restockElapsed)||double.IsInfinity(row.restockElapsed)||row.restockElapsed<0?double.PositiveInfinity:Math.Max(0,restockIntervalSeconds-row.restockElapsed);
            return double.PositiveInfinity;
        }
        // Internal simulated-time hook for isolated tests. Gameplay UI cannot request a stock grant.
        internal bool AdvanceRestock(double scaledSeconds)
        {
            if(!Application.isPlaying||Time.timeScale<=0||!isActiveAndEnabled||!restockEnabled||initializing||RestockIsBusy||TradeService.IsBusy||RepairService.IsBusy||ZombieSurvival.Weapons.PlayerWeapons.IsBusy||ItemStorage.IsWeaponUseBusy
                ||double.IsNaN(scaledSeconds)||double.IsInfinity(scaledSeconds)||scaledSeconds<=0)return false;
            foreach(var saving in FindObjectsByType<ZombieSurvival.Persistence.PlayerSaveSystem>(FindObjectsInactive.Include))if(saving.gameObject.scene==gameObject.scene&&saving.IsWorking)return false;
            try{ValidateConfiguration();}catch(InvalidOperationException){return false;}
            var elapsed=new double[stocks.Length];var plans=new List<ItemStorage.PreparedTopUp>();var due=new List<Stock>();
            for(int i=0;i<stocks.Length;i++)
            {
                var row=stocks[i];elapsed[i]=row.restockElapsed;if(!row.initialized)continue;
                double total=row.restockElapsed+scaledSeconds;if(double.IsInfinity(total)||total>1e12)return false;
                elapsed[i]=total;
                if(total<restockIntervalSeconds)continue;
                if(!row.storage.TryPrepareTopUp(row.profile.Catalog,out var plan))return false;
                plans.Add(plan);due.Add(row);elapsed[i]=total%restockIntervalSeconds;
            }
            if(due.Count==0){for(int i=0;i<stocks.Length;i++)stocks[i].restockElapsed=elapsed[i];return true;}
            RestockIsBusy=true;restocking=true;var locked=new List<ItemStorage>();
            try
            {
                foreach(var row in due){if(!row.storage.TryLockTrade())return false;locked.Add(row.storage);}
                foreach(var plan in plans)plan.Commit();for(int i=0;i<stocks.Length;i++)stocks[i].restockElapsed=elapsed[i];
                InvalidateCatalog();foreach(var plan in plans)if(plan.changed)plan.storage.PublishTrade();return true;
            }
            finally{foreach(var store in locked)store.UnlockTrade();restocking=false;RestockIsBusy=false;}
        }
    }
}
