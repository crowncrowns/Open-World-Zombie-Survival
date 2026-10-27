using System;
using UnityEngine;

namespace ZombieSurvival.Furniture
{
    public enum FurnitureDeviceKind { General, Radio, Television, Light }
    [Serializable]
    public sealed class FurnitureDeviceState
    {
        public int marker;
        public string id;
        public bool on;
        public int channel;
        public float volume, brightness, tuning, playbackSeconds;
    }

    /// <summary>Presentation state only. Audio/lamps cannot create items, damage, UV rules or free power resources.</summary>
    [DisallowMultipleComponent]
    public sealed class FurnitureDevice : MonoBehaviour
    {
        [SerializeField, Tooltip("Уникальный код устройства внутри предмета; входит в сохранение.")]
        private string stableId = "device";
        [SerializeField, Tooltip("Тип устройства: радио, экран, свет или обычное питание.")]
        private FurnitureDeviceKind kind;
        [SerializeField, Tooltip("Основной пространственный AudioSource для радиостанции.")]
        private AudioSource source;
        [SerializeField, Tooltip("Отдельный пространственный AudioSource помех и щелчков.")]
        private AudioSource noiseSource;
        [SerializeField, Tooltip("Отдельный пространственный AudioSource коротких сигналов питания.")]
        private AudioSource effectsSource;
        [SerializeField, Tooltip("Плейлист оригинальных или лицензированных радиоканалов.")]
        private FurnitureRadioPlaylist playlist;
        [SerializeField, Tooltip("Источники обычного игрового света. Не являются ультрафиолетом.")]
        private Light[] lights = Array.Empty<Light>();
        [SerializeField, Tooltip("Renderer индикаторов либо экрана; обновляется только emission через MPB.")]
        private Renderer[] indicators = Array.Empty<Renderer>();
        [SerializeField, Tooltip("Стрелка настройки. Движется по локальной оси X.")]
        private Transform needle;
        [SerializeField, Tooltip("Ручка настройки. Вращается по локальной оси Z.")]
        private Transform tuningDial;
        [SerializeField, Tooltip("Необязательная мембрана динамика. Небольшая вибрация только при звуке.")]
        private Transform speaker;
        [SerializeField, Tooltip("Оригинальный звук щелчка питания.")]
        private AudioClip switchClip;
        [SerializeField, Tooltip("Оригинальная петля радиопомех.")]
        private AudioClip staticClip;
        [SerializeField, Min(.01f), Tooltip("Полуширина устойчивого приёма вокруг радиостанции, доля шкалы 0–1.")]
        private float stationWidth = .055f;
        [SerializeField, Min(0f), Tooltip("Длительность помех при смене канала, с.")]
        private float tuningNoiseDuration = .35f;
        [SerializeField, Min(1), Tooltip("Число режимов обычного устройства без радиоплейлиста.")]
        private int modeCount = 3;
        [SerializeField, Min(0f), Tooltip("Ход стрелки частоты по шкале, м.")]
        private float needleTravel = .12f;
        [SerializeField, Min(0f), Tooltip("Амплитуда вибрации отдельной мембраны динамика, м.")]
        private float speakerAmplitude = .0003f;
        [SerializeField, Tooltip("Цвет индикатора включённого устройства.")]
        private Color indicatorColor = new Color(.25f, .75f, .22f, 1f);
        [SerializeField, HideInInspector] private float[] lightIntensities = Array.Empty<float>();
        [SerializeField, HideInInspector] private Vector3 needleRest, speakerRest;
        [SerializeField, HideInInspector] private Quaternion dialRest = Quaternion.identity;
        private bool on;
        private int channel;
        private float volume = .5f, brightness = .75f, tuning, playbackSeconds, settleTime;
        private MaterialPropertyBlock block;
        private bool restoring;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public string StableId => stableId;
        public FurnitureDeviceKind Kind => kind;
        public bool IsOn => on;
        public int Channel => channel;
        public float Volume => volume;
        public float Brightness => brightness;
        public float Tuning => tuning;
        public int StationCount => playlist != null ? playlist.Count : 0;
        public int ModeCount => StationCount > 0 ? StationCount : kind == FurnitureDeviceKind.Radio ? 0 : Mathf.Max(1, modeCount);
        public AudioSource Source => source;
        public AudioSource NoiseSource => noiseSource;

        public void Configure(FurnitureDeviceKind deviceKind, AudioSource radioSource, FurnitureRadioPlaylist stations,
            Light[] controlledLights, Renderer[] indicatorRenderers, Transform dial = null, Transform pointer = null,
            Transform membrane = null, AudioClip powerSound = null, AudioClip interference = null)
        {
            kind = deviceKind; source = radioSource; playlist = stations;
            lights = controlledLights ?? Array.Empty<Light>(); indicators = indicatorRenderers ?? Array.Empty<Renderer>();
            tuningDial = dial; needle = pointer; speaker = membrane; switchClip = powerSound; staticClip = interference;
            lightIntensities = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lightIntensities[i] = lights[i].intensity;
            if (needle != null) needleRest = needle.localPosition;
            if (tuningDial != null) dialRest = tuningDial.localRotation;
            if (speaker != null) speakerRest = speaker.localPosition;
            if (source == null && (kind == FurnitureDeviceKind.Radio || kind == FurnitureDeviceKind.Television))
            { source = gameObject.AddComponent<AudioSource>(); }
            if (noiseSource == null && staticClip != null) noiseSource = gameObject.AddComponent<AudioSource>();
            if (effectsSource == null && switchClip != null) effectsSource = gameObject.AddComponent<AudioSource>();
            ConfigureAudio(source); ConfigureAudio(noiseSource); ConfigureAudio(effectsSource);
            if (effectsSource != null) { effectsSource.loop = false; effectsSource.volume = .45f; effectsSource.maxDistance = 8f; }
            if (StationCount == 1) tuning = .5f;
            RefreshPresentation(false);
        }
        public void SetStableId(string value) { stableId = value; }
        public void ReleaseDialToControl(Transform candidate)
        {
            // A clickable tuning knob has one visual owner. The device retains the scale needle.
            if (candidate == null || tuningDial != candidate) return;
            candidate.localRotation = dialRest; tuningDial = null;
        }
        private void Awake()
        { ConfigureAudio(source); ConfigureAudio(noiseSource); ConfigureAudio(effectsSource); if (effectsSource != null) effectsSource.loop = false; }
        private static void ConfigureAudio(AudioSource value)
        {
            if (value == null) return;
            value.playOnAwake = false; value.spatialBlend = 1f; value.rolloffMode = AudioRolloffMode.Logarithmic;
            value.minDistance = .65f; value.maxDistance = 15f; value.dopplerLevel = 0f; value.loop = true;
        }
        private void OnEnable() => RefreshPresentation(true);
        private void OnDisable()
        {
            SavePlaybackCursor();
            if (source != null) source.Stop(); if (noiseSource != null) noiseSource.Stop(); if (effectsSource != null) effectsSource.Stop();
            if (speaker != null) speaker.localPosition = speakerRest;
        }
        public void TogglePower() => SetPower(!on);
        public void SetPower(bool value)
        {
            if (on == value) return;
            SavePlaybackCursor(); on = value;
            RefreshPresentation(true);
            if (Application.isPlaying && isActiveAndEnabled && effectsSource != null && switchClip != null)
                effectsSource.PlayOneShot(switchClip, .7f);
        }
        public void CycleChannel()
        {
            if (ModeCount <= 0) return;
            SelectChannel((channel + 1) % ModeCount);
        }
        public void SelectChannel(int requested)
        {
            if (ModeCount <= 0 || requested < 0 || requested >= ModeCount) return;
            SavePlaybackCursor(); channel = requested;
            tuning = ModeCount <= 1 ? .5f : channel / (float)(ModeCount - 1); playbackSeconds = 0f;
            settleTime = tuningNoiseDuration; RefreshPresentation(true);
        }
        public void CycleVolume()
        { volume = volume >= .999f ? 0f : Mathf.Min(1f, Mathf.Round((volume + .2f) * 10f) / 10f); RefreshPresentation(true); }
        public void CycleBrightness()
        { brightness = brightness >= .999f ? .25f : Mathf.Min(1f, brightness + .25f); RefreshPresentation(true); }
        public void Tune()
        {
            tuning = tuning >= .999f ? 0f : Mathf.Min(1f, tuning + .05f);
            int next = StationCount > 1 ? Mathf.RoundToInt(tuning * (StationCount - 1)) : 0;
            if (next != channel) { SavePlaybackCursor(); channel = next; playbackSeconds = 0f; }
            settleTime = tuningNoiseDuration; RefreshPresentation(true);
        }
        private void Update()
        {
            if (!on || restoring) return;
            if (settleTime > 0f) settleTime = Mathf.Max(0f, settleTime - Time.deltaTime);
            UpdateAudioLevels();
            if (speaker != null)
            {
                float vibration = Mathf.Sin(Time.time * 117f) * Mathf.Sin(Time.time * 31f) * speakerAmplitude * volume * Signal();
                speaker.localPosition = speakerRest + Vector3.forward * vibration;
            }
        }
        private float Signal()
        {
            if (StationCount <= 0) return 0f;
            float station = StationCount <= 1 ? .5f : channel / (float)(StationCount - 1);
            float reception = 1f - Mathf.Clamp01(Mathf.Abs(tuning - station) / Mathf.Max(.005f, stationWidth));
            return reception * (settleTime > 0f ? 1f - Mathf.Clamp01(settleTime / Mathf.Max(.001f, tuningNoiseDuration)) : 1f);
        }
        private void UpdateAudioLevels()
        {
            float signal = kind == FurnitureDeviceKind.Radio ? Signal() : 1f;
            if (source != null) source.volume = on ? volume * signal : 0f;
            if (noiseSource != null) noiseSource.volume = on ? volume * Mathf.Lerp(.014f, .36f, 1f - signal) : 0f;
        }
        private void SavePlaybackCursor()
        { if (source != null && source.clip != null && source.isPlaying) playbackSeconds = source.time; }
        public void RefreshPresentation(bool allowAudio)
        {
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) { lights[i].enabled = on; lights[i].intensity = (i < lightIntensities.Length ? lightIntensities[i] : 1f) * brightness; }
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var indicator in indicators)
            {
                if (indicator == null) continue;
                int slots = indicator.sharedMaterials.Length;
                for (int slot = 0; slot < slots; slot++)
                {
                    var material = indicator.sharedMaterials[slot];
                    if (material == null || !(material.name == "Emission" || material.name == "Screen") || !material.HasProperty(EmissionColor)) continue;
                    indicator.GetPropertyBlock(block, slot);
                    Color activeColor = kind == FurnitureDeviceKind.Television ? Color.Lerp(new Color(.20f,.38f,.57f),new Color(.32f,.54f,.39f),channel / (float)Mathf.Max(1,ModeCount-1)) : indicatorColor;
                    float modeLevel = kind == FurnitureDeviceKind.General ? Mathf.Lerp(.55f,1f,channel / (float)Mathf.Max(1,ModeCount-1)) : .8f;
                    block.SetColor(EmissionColor, on ? activeColor * (kind == FurnitureDeviceKind.Light ? brightness * 2f : modeLevel) : Color.black);
                    indicator.SetPropertyBlock(block, slot);
                }
            }
            if (tuningDial != null) tuningDial.localRotation = dialRest * Quaternion.AngleAxis(Mathf.Lerp(-135f, 135f, tuning), Vector3.forward);
            if (needle != null) needle.localPosition = needleRest + Vector3.right * ((tuning - .5f) * needleTravel);
            if (source != null)
            {
                if (!on || !allowAudio || !Application.isPlaying || !isActiveAndEnabled) source.Stop();
                else
                {
                    AudioClip desired = playlist != null ? playlist.Get(channel)?.clip : null;
                    if (source.clip != desired) { source.Stop(); source.clip = desired; }
                    if (desired != null && !source.isPlaying)
                    {
                        source.time = Mathf.Clamp(playbackSeconds, 0f, Mathf.Max(0f, desired.length - .001f)); source.Play();
                    }
                }
            }
            if (noiseSource != null)
            {
                if (!on || !allowAudio || !Application.isPlaying || !isActiveAndEnabled) noiseSource.Stop();
                else if (staticClip != null)
                {
                    if (noiseSource.clip != staticClip) { noiseSource.Stop(); noiseSource.clip = staticClip; }
                    if (!noiseSource.isPlaying) noiseSource.Play();
                }
            }
            UpdateAudioLevels();
        }
        public FurnitureDeviceState CaptureState()
        {
            SavePlaybackCursor();
            return new FurnitureDeviceState { marker = 173, id = stableId, on = on, channel = channel,
                volume = volume, brightness = brightness, tuning = tuning, playbackSeconds = playbackSeconds };
        }
        public bool ValidateState(FurnitureDeviceState value)
        {
            if (value == null || value.marker != 173 || value.id != stableId || value.channel < 0 || value.channel >= Mathf.Max(1, ModeCount)
                || !Unit(value.volume) || !Unit(value.brightness) || !Unit(value.tuning)
                || !Finite(value.playbackSeconds) || value.playbackSeconds < 0f) return false;
            var clip = playlist != null ? playlist.Get(value.channel)?.clip : null;
            return clip == null ? value.playbackSeconds == 0f : value.playbackSeconds <= clip.length + .01f;
        }
        public void RestoreStateSilently(FurnitureDeviceState value)
        {
            restoring = true;
            on = value.on; channel = value.channel; volume = value.volume; brightness = value.brightness;
            tuning = value.tuning; playbackSeconds = value.playbackSeconds; settleTime = 0f;
            if (effectsSource != null) effectsSource.Stop();
            if (speaker != null) speaker.localPosition = speakerRest;
            RefreshPresentation(false); restoring = false;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Unit(float value) => Finite(value) && value >= 0f && value <= 1f;
    }
}
