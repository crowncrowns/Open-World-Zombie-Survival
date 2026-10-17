using UnityEngine;

namespace ZombieSurvival.Player
{
    /// <summary>Model-specific references belong to the appearance prefab, not the movement controller.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerModelDefinition : MonoBehaviour
    {
        [SerializeField] private Animator modelAnimator;
        [Tooltip("Separate head, hair, eyes etc. Hidden only for the player's camera; still cast shadows.")]
        [SerializeField] private Renderer[] firstPersonHiddenRenderers = new Renderer[0];
        [Tooltip("Optional dedicated first-person meshes. Revealed only for the player's camera.")]
        [SerializeField] private Renderer[] firstPersonOnlyRenderers = new Renderer[0];

        public Animator ModelAnimator => modelAnimator;
        public Renderer[] FirstPersonHiddenRenderers => firstPersonHiddenRenderers;
        public Renderer[] FirstPersonOnlyRenderers => firstPersonOnlyRenderers;
    }
}
