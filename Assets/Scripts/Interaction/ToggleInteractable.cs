using UnityEngine;
using UnityEngine.Events;

namespace ZombieSurvival.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ToggleInteractable : Interactable
    {
        [Header("Switch")]
        [SerializeField] private bool startsOn;
        [SerializeField] private string turnOnPrompt = "Включить";
        [SerializeField] private string turnOffPrompt = "Выключить";
        [SerializeField] private string onMessage = "Включено";
        [SerializeField] private string offMessage = "Выключено";
        [SerializeField] private Renderer[] indicators;
        [SerializeField] private Color offColor = new Color(.85f, .18f, .13f);
        [SerializeField] private Color onColor = new Color(.15f, .85f, .4f);
        [SerializeField] private UnityEvent<bool> onStateChanged = new UnityEvent<bool>();
        private MaterialPropertyBlock colors;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        public bool IsOn { get; private set; }

        private void Awake() { IsOn = startsOn; colors = new MaterialPropertyBlock(); ApplyColor(); }
        public override string GetPrompt(PlayerInteractor actor) => IsOn ? turnOffPrompt : turnOnPrompt;
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            IsOn = !IsOn;
            ApplyColor();
            actor.ShowFeedback(IsOn ? onMessage : offMessage);
            onStateChanged.Invoke(IsOn);
            return true;
        }
        private void ApplyColor()
        {
            if (indicators == null) return;
            foreach (var indicator in indicators)
            {
                if (indicator == null) continue;
                indicator.GetPropertyBlock(colors);
                colors.SetColor(BaseColor, IsOn ? onColor : offColor);
                colors.SetColor(ColorId, IsOn ? onColor : offColor);
                indicator.SetPropertyBlock(colors);
            }
        }
    }
}
