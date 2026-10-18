namespace ZombieSurvival.Services
{
    [UnityEngine.DisallowMultipleComponent]
    public sealed class TraderServicePoint : ServicePoint
    {
        public override string SessionTitle => "Торговая сессия";
        public override string SessionDescription => "Проверка доступа к сервису. Каталог и сделки появятся на следующих этапах.";
    }
}
