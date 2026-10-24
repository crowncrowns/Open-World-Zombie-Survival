using UnityEngine;

namespace PostApocalypse.Graffiti
{
    // No per-frame work. A property block restores the serialized opacity on load.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GraffitiPlacement : MonoBehaviour
    {
        [Tooltip("Композиция из каталога граффити.")] public GraffitiEntry entry;
        [Range(0, 1), Tooltip("Непрозрачность краски, 0–1.")] public float opacity = 1;
        [Tooltip("Объект создан инструментом Graffiti Painter; используется для группового удаления.")]
        public bool paintedByTool;
        [HideInInspector] public Component nativeProjector;
        void OnEnable() => ApplyOpacity();
        void OnValidate() => ApplyOpacity();
        public void ApplyOpacity()
        {
            if (nativeProjector)
            {
                nativeProjector.GetType().GetProperty("fadeFactor")?.SetValue(nativeProjector, opacity);
                return;
            }
            var r = GetComponent<Renderer>();
            if (!r || !r.sharedMaterial) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            var color = Color.white; color.a = opacity;
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
        }
    }
}
