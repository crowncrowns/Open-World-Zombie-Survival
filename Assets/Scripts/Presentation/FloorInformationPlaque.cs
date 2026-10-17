using TMPro;
using UnityEngine;

namespace ZombieSurvival.Presentation
{
    // Keeps legacy setup-script text updates compatible; only the compact TMP plaque renders.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FloorInformationPlaque : MonoBehaviour
    {
        [SerializeField, Tooltip("Исходная надпись: полный текст сохраняется для генераторов полигона.")]
        private TextMesh source;
        [SerializeField, Tooltip("Текст небольшой напольной таблички.")]
        private TMP_Text caption;
        public void Bind(TextMesh original, TMP_Text text) { source = original; caption = text; Refresh(); }
        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();
        private void Refresh()
        {
            if (source == null || caption == null) return;
            var renderer = source.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
            if (caption.text != source.text) caption.text = source.text;
        }
    }
}
