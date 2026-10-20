using System;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Economy;
using ZombieSurvival.Weapons;

namespace ZombieSurvival.Services
{
    public enum TradeDirection { Buy, Sell }
    public sealed class TradeQuote
    {
        public Trader Trader { get; internal set; }
        public TraderProfile Profile { get; internal set; }
        public ItemStack Item { get; internal set; }
        public int Quantity { get; internal set; }
        public long UnitPrice { get; internal set; }
        public long TotalPrice { get; internal set; }
        public TradeDirection Direction { get; internal set; }
        internal PlayerServiceSession session;
        internal ItemStorage source, destination;
        internal Wallet payer, recipient;
        internal long version;
        internal long sessionVersion;
        internal string id;
        internal string settingsStamp;
        internal int available, rounds;
        internal float condition, maximum;
        internal bool consumed;
        internal TradeQuote() { }
    }
    public static class TradeService
    {
        public static bool IsBusy { get; private set; }
        private static bool Fail(string message, out string reason) { reason = message; return false; }
        /// <summary>Read-only display price, NOT an authorization or a quote. Uses the same pricing as execution.</summary>
        public static bool TryPreviewPrice(TraderProfile profile, TradeDirection direction, ItemStack item, int quantity, out long unit, out long total, out string reason)
        {
            unit = total = 0;
            if (profile == null || item == null || item.IsEmpty || quantity <= 0 || quantity > item.Quantity
                || !Enum.IsDefined(typeof(TradeDirection), direction) || item.Definition.IsIndividual && quantity != 1)
                return Fail("Выберите вещь и допустимое количество.", out reason);
            try { profile.Validate(); }
            catch (InvalidOperationException error) { return Fail(error.Message, out reason); }
            return TryPrice(profile, direction, item, quantity, out unit, out total, out reason);
        }
        public static bool TryPreviewPrice(Trader trader,TradeDirection direction,ItemStack item,int quantity,out long unit,out long total,out string reason)
        {
            unit=total=0;
            if(trader==null)return Fail("Торговец недоступен",out reason);
            if(!trader.TryGetEffectiveSellPercent(trader.Profile,out var sell,out reason))return false;
            if(item==null||item.IsEmpty||quantity<=0||quantity>item.Quantity||!Enum.IsDefined(typeof(TradeDirection),direction)||item.Definition.IsIndividual&&quantity!=1)return Fail("Выберите вещь и допустимое количество",out reason);
            return TryPrice(trader.Profile,direction,item,quantity,out unit,out total,out reason,sell);
        }
        public static bool TryQuote(PlayerServiceSession session, Trader trader, TradeDirection direction, ItemStack item, int quantity, out TradeQuote quote, out string reason)
        {
            quote = null;
            if (IsBusy||Trader.RestockIsBusy) return Fail("Предыдущая сделка или пополнение ещё завершается.", out reason);
            return Build(session, trader, direction, item, quantity, out quote, out reason);
        }
        private static bool Build(PlayerServiceSession session, Trader trader, TradeDirection direction, ItemStack item, int quantity, out TradeQuote quote, out string reason)
        {
            quote = null;
            if (session == null || trader == null || !Enum.IsDefined(typeof(TradeDirection), direction)) return Fail("Торговая сессия недоступна.", out reason);
            var point = trader.GetComponent<TraderServicePoint>();
            if (!session.ValidateAccess(point, out reason)) return false;
            if (!trader.ValidateSelection(trader.Profile, trader.CatalogVersion, out reason)) return false;
            var player = session.gameObject;
            var bag = player.GetComponent<ItemStorage>(); var money = player.GetComponent<Wallet>(); var budget = trader.GetComponent<TraderBudget>();
            var stock = trader.ActiveStock;
            if (bag == null || stock == null || bag == stock || !bag.isActiveAndEnabled || !stock.isActiveAndEnabled
                || money == null || !money.isActiveAndEnabled || budget == null || !budget.isActiveAndEnabled || !budget.Funds.isActiveAndEnabled || budget.Funds == money)
                return Fail("Инвентарь или кошелёк участника недоступен.", out reason);
            var source = direction == TradeDirection.Buy ? stock : bag; var destination = direction == TradeDirection.Buy ? bag : stock;
            if (item == null || item.IsEmpty || item.IsSnapshot || !source.Contains(item) || quantity <= 0 || quantity > item.Quantity || item.Definition.IsIndividual && quantity != 1)
                return Fail("Выбранная вещь или количество уже изменились.", out reason);
            if (direction == TradeDirection.Sell)
            {
                if (!trader.Profile.Accepts(item.Definition)) return Fail("Торговец не выкупает эту категорию.", out reason);
                var weapons = player.GetComponent<PlayerWeapons>();
                if (weapons != null && weapons.Equipped == item) return Fail("Сначала уберите оружие из рук.", out reason);
            }
            if (!TryPreviewPrice(trader, direction, item, quantity, out long unit, out long total, out reason)) return false;
            if (destination.FreeCapacity(item.Definition) < quantity) return Fail("Недостаточно места для всего количества.", out reason);
            var payer = direction == TradeDirection.Buy ? money : budget.Funds;
            var recipient = direction == TradeDirection.Buy ? budget.Funds : money;
            if (total > 0 && !payer.CanDebit(total)) return Fail("Недостаточно рублей у покупателя.", out reason);
            if (total > 0 && !recipient.CanCredit(total)) return Fail("Баланс получателя переполнен или недоступен.", out reason);
            quote = new TradeQuote { session = session, Trader = trader, Profile = trader.Profile, Direction = direction, Item = item, Quantity = quantity,
                source = source, destination = destination, payer = payer, recipient = recipient, UnitPrice = unit, TotalPrice = total, version = trader.CatalogVersion,
                id = item.InstanceId, settingsStamp=trader.ExactSettingsStamp, available = item.Quantity, rounds = item.LoadedRounds, condition = item.Durability, maximum = item.Definition.MaximumDurability, sessionVersion = session.SessionVersion };
            reason = ""; return true;
        }
        private static TraderProfile.Entry Entry(TraderProfile profile, ItemDefinition item)
        { foreach (var entry in profile.Catalog) if (entry.item == item) return entry; return null; }
        private static long PricePart(long basis, decimal percent, decimal condition, bool buying)
        {
            decimal value = (decimal)basis * percent / 100m * condition;
            value = buying ? decimal.Ceiling(value) : decimal.Floor(value);
            if (buying && basis > 0 && percent > 0 && value < 1) value = 1;
            if (value < 0 || value > long.MaxValue) throw new OverflowException();
            return (long)value;
        }
        private static bool TryPrice(TraderProfile profile, TradeDirection direction, ItemStack item, int quantity, out long unit, out long total, out string reason,decimal? sellOverride=null)
        {
            unit = total = 0; var entry = Entry(profile, item.Definition);
            if (entry == null) return Fail("Для этого типа не назначена базовая цена в профиле.", out reason);
            try
            {
                bool buying = direction == TradeDirection.Buy; decimal percent = buying ? sellOverride??profile.SellPercent : profile.BuyPercent;
                decimal condition = item.Definition.HasDurability ? (decimal)item.Durability / (decimal)item.Definition.MaximumDurability : 1m;
                unit = PricePart(entry.basePrice, percent, condition, buying);
                if (item.LoadedRounds > 0)
                {
                    var weapon = item.Definition.Weapon;
                    if (weapon == null || weapon.Ammo == null || item.LoadedRounds > weapon.MagazineSize) return Fail("Магазин оружия повреждён.", out reason);
                    var ammo = Entry(profile, weapon.Ammo);
                    if (ammo == null) return Fail("Нет цены патронов магазина: сначала разгрузите оружие.", out reason);
                    unit = checked(unit + checked(PricePart(ammo.basePrice, percent, 1m, buying) * item.LoadedRounds));
                }
                total = checked(unit * quantity); reason = ""; return true;
            }
            catch (OverflowException) { return Fail("Стоимость превышает диапазон целых рублей.", out reason); }
            catch (DivideByZeroException) { return Fail("Некорректная максимальная прочность типа.", out reason); }
        }
        public static bool TryExecute(TradeQuote quote, out string reason)
        {
            if (IsBusy || Trader.RestockIsBusy || quote == null || quote.consumed) return Fail("Предложение уже исполнено или сделка занята.", out reason);
            IsBusy = true; bool sourceLock = false, destinationLock = false, payerLock = false, recipientLock = false;
            try
            {
                if (quote.session == null || quote.session.SessionVersion != quote.sessionVersion) return Fail("Торговая сессия уже сменилась.", out reason);
                if (quote.Trader == null || quote.Profile == null || !quote.Trader.ValidateSelection(quote.Profile, quote.version, out reason)) return Fail("Ассортимент изменился. Выберите вещь заново.", out reason);
                if (!Build(quote.session, quote.Trader, quote.Direction, quote.Item, quote.Quantity, out var current, out reason)) return false;
                if (current.source != quote.source || current.destination != quote.destination || current.payer != quote.payer || current.recipient != quote.recipient
                    || current.id != quote.id || current.available != quote.available || current.rounds != quote.rounds || current.condition != quote.condition || current.maximum != quote.maximum
                    || current.UnitPrice != quote.UnitPrice || current.TotalPrice != quote.TotalPrice||current.settingsStamp!=quote.settingsStamp)
                    return Fail("Состояние вещи или цена изменились. Получите новое предложение.", out reason);
                if (!quote.source.TryPrepareTrade(quote.destination, quote.Item, quote.Quantity, out var transfer)) return Fail("Нельзя перенести всё количество без потерь.", out reason);
                if (!quote.payer.TryPrepareTradePayment(quote.recipient, quote.TotalPrice, out var payment)) return Fail("Платёж больше недоступен.", out reason);
                sourceLock = quote.source.TryLockTrade(); destinationLock = quote.destination.TryLockTrade(); payerLock = quote.payer.TryLockTrade(); recipientLock = quote.recipient.TryLockTrade();
                if (!sourceLock || !destinationLock || !payerLock || !recipientLock) return Fail("Участник занят другой операцией.", out reason);
                // No public calls, normalization, allocation or events in this commit section.
                transfer.Commit(); payment.Commit(); quote.consumed = true;
                quote.source.PublishTrade(); quote.destination.PublishTrade(); payment.Publish();
                reason = ""; return true;
            }
            finally
            {
                if (recipientLock) quote.recipient.UnlockTrade(); if (payerLock) quote.payer.UnlockTrade();
                if (destinationLock) quote.destination.UnlockTrade(); if (sourceLock) quote.source.UnlockTrade(); IsBusy = false;
            }
        }
    }
}
