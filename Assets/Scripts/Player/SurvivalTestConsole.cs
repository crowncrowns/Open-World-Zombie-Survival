using UnityEngine;
using ZombieSurvival.Interaction;
using ZombieSurvival.Persistence;

namespace ZombieSurvival.Player
{
    public enum SurvivalTestAction { [InspectorName("Истощить до 10 / 10")] Drain, [InspectorName("Восстановить потребности")] Restore, [InspectorName("Сохранить")] Save, [InspectorName("Загрузить")] Load }
    public sealed class SurvivalTestConsole : Interactable
    {
        [SerializeField, Tooltip("Действие тестовой консоли. Сохранить/загрузить используют общий PlayerSaveSystem, без второго формата.")]
        private SurvivalTestAction action;
        public override string GetPrompt(PlayerInteractor actor)=>action==SurvivalTestAction.Drain?"Сытость и вода → 10":action==SurvivalTestAction.Restore?"Восстановить сытость и воду":action==SurvivalTestAction.Save?"Сохранить персонажа":"Загрузить персонажа";
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            var needs=actor.GetComponent<PlayerNeeds>();if(needs==null)return false;
            if(action==SurvivalTestAction.Drain)needs.RestoreValues(10,10);
            else if(action==SurvivalTestAction.Restore)needs.ResetNeeds();
            else { var save=actor.GetComponent<PlayerSaveSystem>();if(save==null)return false;bool ok=action==SurvivalTestAction.Save?save.TrySave():save.TryLoad();actor.ShowFeedback(save.Status);return ok; }
            actor.ShowFeedback(GetPrompt(actor));return true;
        }
    }
}
