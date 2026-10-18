using UnityEngine;

namespace ZombieSurvival.Player
{
    /// <summary>Model-specific references belong to the appearance prefab, not the movement controller.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerModelDefinition : MonoBehaviour
    {
        [Tooltip("Animator Humanoid-модели. Без явной ссылки ищется на модели актёра.")]
        [SerializeField] private Animator modelAnimator;
        [Tooltip("Меши головы, волос и глаз, скрытые только от собственной камеры игрока. Продолжают отбрасывать тени.")]
        [SerializeField] private Renderer[] firstPersonHiddenRenderers = new Renderer[0];
        [Tooltip("Необязательные отдельные меши от первого лица, видимые только в собственной камере игрока.")]
        [SerializeField] private Renderer[] firstPersonOnlyRenderers = new Renderer[0];

        public Animator ModelAnimator => modelAnimator;
        public Renderer[] FirstPersonHiddenRenderers => firstPersonHiddenRenderers;
        public Renderer[] FirstPersonOnlyRenderers => firstPersonOnlyRenderers;
    }
}
