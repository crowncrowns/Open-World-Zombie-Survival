using UnityEngine;
using UnityEngine.Events;

namespace ZombieSurvival.Interaction
{
    public abstract class Interactable : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [SerializeField] private bool interactionEnabled = true;
        [SerializeField] private string displayName = "Объект";
        [SerializeField] private UnityEvent onInteracted = new UnityEvent();

        public string DisplayName => displayName;
        public virtual bool CanInteract(PlayerInteractor actor) => interactionEnabled && isActiveAndEnabled;
        public abstract string GetPrompt(PlayerInteractor actor);

        public bool Interact(PlayerInteractor actor)
        {
            if (!CanInteract(actor) || !PerformInteraction(actor)) return false;
            onInteracted.Invoke();
            return true;
        }

        protected abstract bool PerformInteraction(PlayerInteractor actor);
    }
}
