namespace ZombieSurvival.Services
{
    [UnityEngine.DisallowMultipleComponent]
    public sealed class WorkbenchServicePoint : ServicePoint
    {
        public override string SessionTitle => "Сессия верстака";
        public override string SessionDescription => "Проверка доступа к верстаку. Ремонт материалами появится на следующих этапах.";
    }
}
