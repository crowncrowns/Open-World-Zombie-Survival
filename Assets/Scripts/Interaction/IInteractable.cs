namespace ZombieSurvival.Interaction
{
    /// <summary>Objects provide their own behaviour; the player only selects and invokes them.</summary>
    public interface IInteractable
    {
        string DisplayName { get; }
        string GetPrompt(PlayerInteractor actor);
        bool CanInteract(PlayerInteractor actor);
        bool Interact(PlayerInteractor actor);
    }
}
