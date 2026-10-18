using UnityEngine;
using ZombieSurvival.Interaction;

namespace ZombieSurvival.Inventory
{
    [RequireComponent(typeof(ItemStorage))]
    public sealed class StorageContainer : Interactable
    {
        public override string GetPrompt(PlayerInteractor actor) => "Открыть ящик";
        public override bool CanInteract(PlayerInteractor actor) => base.CanInteract(actor) && actor.GetComponent<PlayerInventory>() != null;
        protected override bool PerformInteraction(PlayerInteractor actor) => actor.GetComponent<PlayerInventory>().Open(GetComponent<ItemStorage>());
    }
}
