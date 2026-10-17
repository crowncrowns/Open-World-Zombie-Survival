using UnityEngine;
using UnityEngine.Events;

namespace ZombieSurvival.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ToggleInteractable : Interactable
    {
        [Header("Switch")]
        [Tooltip("Начальное состояние переключателя.")]
        [SerializeField] private bool startsOn;
        [Tooltip("Текст действия включения.")]
        [SerializeField] private string turnOnPrompt = "Включить";
        [Tooltip("Текст действия выключения.")]
        [SerializeField] private string turnOffPrompt = "Выключить";
        [Tooltip("Сообщение после включения.")]
        [SerializeField] private string onMessage = "Включено";
        [Tooltip("Сообщение после выключения.")]
        [SerializeField] private string offMessage = "Выключено";
        [Tooltip("Индикаторы, цвет которых меняется вместе с переключателем.")]
        [SerializeField] private Renderer[] indicators;
        [Tooltip("Цвет индикатора в выключенном состоянии.")]
        [SerializeField] private Color offColor = new Color(.85f, .18f, .13f);
        [Tooltip("Цвет индикатора во включённом состоянии.")]
        [SerializeField] private Color onColor = new Color(.15f, .85f, .4f);
        [Tooltip("Событие смены состояния переключателя.")]
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
