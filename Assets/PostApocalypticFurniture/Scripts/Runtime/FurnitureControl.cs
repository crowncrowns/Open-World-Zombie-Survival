using UnityEngine;
using ZombieSurvival.Interaction;

namespace ZombieSurvival.Furniture
{
    public enum FurnitureControlKind { Power, Channel, Volume, Brightness, Tune }

    /// <summary>Point at the actual button/knob and press the same Interact (F) action.</summary>
    [DisallowMultipleComponent]
    public sealed class FurnitureControl : Interactable
    {
        [SerializeField, Tooltip("Код кнопки внутри модели.")]
        private string stableId;
        [SerializeField, Tooltip("Устройство, которым управляет эта кнопка.")]
        private FurnitureDevice device;
        [SerializeField, Tooltip("Действие при одном нажатии F: питание, канал, громкость, яркость или настройка.")]
        private FurnitureControlKind kind;
        [SerializeField, Tooltip("Название органа управления.")]
        private string label = "Питание";
        [SerializeField, Tooltip("Локальная ось нажатия или вращения, заданная конструкцией детали.")]
        private Vector3 controlAxis = Vector3.forward;
        [SerializeField, Tooltip("Вращающийся регулятор; иначе кнопка с поступательным нажатием.")]
        private bool rotary;
        [SerializeField, Tooltip("Ход кнопки в метрах либо угол дискретного переключателя в градусах.")]
        private float controlExtent = .004f;
        [SerializeField, Tooltip("Фиксированная радиостанция клавиши; -1 означает обычное переключение по кругу.")]
        private int stationPreset = -1;
        [SerializeField, HideInInspector] private Vector3 restPosition;
        [SerializeField, HideInInspector] private Quaternion restRotation = Quaternion.identity;
        [SerializeField, HideInInspector] private bool authoredPose;
        private float pressed;
        public string StableId => stableId;
        public FurnitureControlKind Kind => kind;
        public FurnitureDevice Device => device;
        public void Configure(string id, FurnitureDevice target, FurnitureControlKind action, string displayLabel)
        {
            stableId = id; device = target; kind = action; label = displayLabel;
            if (device != null && (kind == FurnitureControlKind.Tune || kind == FurnitureControlKind.Volume)) device.ReleaseDialToControl(transform);
            restPosition = transform.localPosition; restRotation = transform.localRotation; authoredPose = true;
        }
        public void ConfigureGeometry(Vector3 axis, bool isRotary, float extent)
        {
            controlAxis = axis.sqrMagnitude > .001f ? axis.normalized : Vector3.forward;
            rotary = isRotary; controlExtent = Mathf.Abs(extent);
            RefreshVisual();
        }
        public void ConfigureStationPreset(int station) => stationPreset = Mathf.Max(-1, station);
        private void Awake()
        { if (!authoredPose) { restPosition = transform.localPosition; restRotation = transform.localRotation; authoredPose = true; } }
        private void OnEnable() { pressed = 0f; RefreshVisual(); }
        private void OnDisable()
        { if (authoredPose) { transform.localPosition = restPosition; transform.localRotation = restRotation; } }
        private void LateUpdate()
        { pressed = Mathf.MoveTowards(pressed, 0f, Time.deltaTime * 6f); RefreshVisual(); }
        private void RefreshVisual()
        {
            if (!authoredPose || device == null) return;
            transform.localPosition = restPosition - controlAxis * (rotary ? 0f : pressed * Mathf.Clamp(controlExtent, .001f, .012f));
            if (rotary)
            {
                float fraction = kind == FurnitureControlKind.Volume ? device.Volume
                    : kind == FurnitureControlKind.Tune ? device.Tuning
                    : kind == FurnitureControlKind.Brightness ? device.Brightness
                    : kind == FurnitureControlKind.Power ? (device.IsOn ? 1f : 0f)
                    : device.Channel / (float)Mathf.Max(1, device.ModeCount - 1);
                bool continuous = kind == FurnitureControlKind.Volume || kind == FurnitureControlKind.Tune || kind == FurnitureControlKind.Brightness;
                float angle = continuous ? Mathf.Lerp(-135f, 135f, fraction) : controlExtent * fraction;
                transform.localRotation = restRotation * Quaternion.AngleAxis(angle, controlAxis);
            }
            else transform.localRotation = restRotation;
        }
        public void RefreshRestoredVisual() { pressed = 0f; RefreshVisual(); }
        public override bool CanInteract(PlayerInteractor actor) => base.CanInteract(actor) && device != null && device.isActiveAndEnabled;
        public override string GetPrompt(PlayerInteractor actor)
        {
            if (device == null) return "Устройство не назначено";
            switch (kind)
            {
                case FurnitureControlKind.Power: return device.IsOn ? "Выключить" : "Включить";
                case FurnitureControlKind.Channel: return stationPreset>=0?"Выбрать станцию "+(stationPreset+1):(device.Kind == FurnitureDeviceKind.Radio ? "Сменить канал (" : "Сменить режим (") + (device.Channel + 1) + ")";
                case FurnitureControlKind.Volume: return "Громкость: " + Mathf.RoundToInt(device.Volume * 100f) + "%";
                case FurnitureControlKind.Brightness: return "Яркость: " + Mathf.RoundToInt(device.Brightness * 100f) + "%";
                default: return "Настроить частоту: " + Mathf.RoundToInt(device.Tuning * 100f);
            }
        }
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            pressed = 1f;
            switch (kind)
            {
                case FurnitureControlKind.Power: device.TogglePower(); break;
                case FurnitureControlKind.Channel: if(stationPreset>=0)device.SelectChannel(stationPreset);else device.CycleChannel(); break;
                case FurnitureControlKind.Volume: device.CycleVolume(); break;
                case FurnitureControlKind.Brightness: device.CycleBrightness(); break;
                default: device.Tune(); break;
            }
            RefreshVisual();
            if (actor != null) actor.ShowFeedback(label + ": " + GetPrompt(actor));
            return true;
        }
    }
}
