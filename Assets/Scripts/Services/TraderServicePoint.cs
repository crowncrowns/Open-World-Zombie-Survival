namespace ZombieSurvival.Services
{
    [UnityEngine.DisallowMultipleComponent]
    public sealed class TraderServicePoint : ServicePoint
    {
        public override string SessionTitle => "Торговая сессия";
        public override string SessionDescription => GetComponent<Trader>() != null ? "Просмотр личных остатков и профилей. Сделки пока недоступны (Т5)." : "Проверка доступа к сервису. Каталог подключается компонентом Trader.";
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
