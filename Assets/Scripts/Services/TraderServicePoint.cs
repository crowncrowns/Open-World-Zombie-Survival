namespace ZombieSurvival.Services
{
    [UnityEngine.DisallowMultipleComponent]
    public sealed class TraderServicePoint : ServicePoint
    {
        public override string SessionTitle => "Торговля";
        public override string SessionDescription => GetComponent<Trader>() != null ? "Выберите покупку или продажу, предмет и количество. Цена учитывает состояние вещи и патроны в магазине." : "Проверка доступа к сервису. Каталог подключается компонентом Trader.";
        public override bool CanAccess(ZombieSurvival.Interaction.PlayerInteractor actor, out string reason)
        {
            var trader = GetComponent<Trader>();
            if (trader != null)
            {
                if (!trader.isActiveAndEnabled) { reason = "Торговец отключён."; return false; }
                try { trader.ValidateConfiguration(); }
                catch (System.InvalidOperationException error) { reason = error.Message; return false; }
            }
            return base.CanAccess(actor, out reason);
        }
    }
}
