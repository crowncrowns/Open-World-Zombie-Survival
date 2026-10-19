namespace ZombieSurvival.Services
{
    [UnityEngine.DisallowMultipleComponent]
    public sealed class WorkbenchServicePoint : ServicePoint
    {
        public override string SessionTitle => "Верстак";
        public override string SessionDescription => GetComponent<RepairWorkbench>() != null
            ? "Полное восстановление за материалы из своего рюкзака. Рубли не расходуются."
            : "Верстак не настроен: добавьте компонент RepairWorkbench.";
        public override bool CanAccess(ZombieSurvival.Interaction.PlayerInteractor actor, out string reason)
        {
            var workbench = GetComponent<RepairWorkbench>();
            if (workbench == null || !workbench.isActiveAndEnabled) { reason = "Верстак недоступен."; return false; }
            return base.CanAccess(actor, out reason);
        }
    }
}
