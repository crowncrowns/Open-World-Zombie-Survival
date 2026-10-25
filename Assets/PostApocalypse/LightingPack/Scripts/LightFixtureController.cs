using System;
using UnityEngine;

namespace PostApocalypse.Lighting
{
    /// <summary>Per-instance dynamic lighting. Does not modify shared materials or baked lightmaps.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LightFixtureController : MonoBehaviour
    {
        public enum OperatingMode { Normal, Flickering, Weak, Failing, Off }
        [Serializable] public struct LightBinding { public Light light; [Min(0)] public float weight; }
        [Serializable] public struct EmissionBinding { public Renderer renderer; public int materialIndex; [Min(0)] public float weight; }
        [Header("Light Settings")]
        [Tooltip("Включить питание светильника")] public bool lightEnabled = true;
        [Min(0), Tooltip("Общая интенсивность. Относительные веса источников сохраняются.")] public float intensity = 3;
        [ColorUsage(false, true)] public Color lightColor = new Color(1, .88f, .68f);
        public bool useColorTemperature;
        [Range(1000, 20000)] public float colorTemperature = 3500;
        [Min(.01f)] public float range = 6;
        [Range(1, 179)] public float spotAngle = 95;
        public bool castShadows = true;
        [Min(0)] public float emissionMultiplier = 1;
        [Tooltip("Baked освещение не меняется в Play. Для регулируемых ламп используйте Realtime.")] public LightmapBakeType bakeType = LightmapBakeType.Realtime;
        [Header("Light Effects")]
        public OperatingMode mode;
        public bool enableFlickering;
        [Min(.01f)] public float flickerSpeed = 8;
        [Range(0, 1)] public float flickerStrength = .6f;
        public bool randomFlicker = true;
        public bool enableBuzzingSound;
        public AudioClip buzzingClip;
        [Range(0, 1)] public float buzzingVolume = .08f;
        [Tooltip("Предпросмотр мерцания в редакторе; не изменяет материалы.")] public bool previewEffectsInEditor;
        [Header("Performance")]
        public bool distanceCulling;
        [Min(1)] public float cullDistance = 35;
        public Transform distanceObserver;
        [Tooltip("Необязательный индивидуальный seed; 0 использует ID экземпляра.")] public int flickerSeed;
        [Header("Bindings — authored by prefab")]
        public LightBinding[] lights = Array.Empty<LightBinding>();
        public EmissionBinding[] emitters = Array.Empty<EmissionBinding>();
        MaterialPropertyBlock block;
        AudioSource buzz;
        Camera cachedCamera;
        double nextCameraLookup;
        double nextUpdate;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        public float EffectiveIntensity { get; private set; }
        public void SetEnabled(bool value) { lightEnabled = value; ApplyNow(); }
        public void SetIntensity(float value) { intensity = Mathf.Max(0, value); ApplyNow(); }
        void OnEnable() { ApplyNow(); }
        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextUpdate) return;
            nextUpdate = now + 1.0 / 30.0;
            ApplyAt(now);
        }
        void OnValidate()
        {
            intensity = Mathf.Max(0, intensity); range = Mathf.Max(.01f, range);
            flickerSpeed = Mathf.Max(.01f, flickerSpeed); emissionMultiplier = Mathf.Max(0, emissionMultiplier);
        }
        public float EvaluateFactor(double time)
        {
            if (!lightEnabled || mode == OperatingMode.Off) return 0;
            float factor = mode == OperatingMode.Weak ? .23f : 1;
            bool animate = Application.isPlaying || previewEffectsInEditor;
            if (!animate) return factor;
            uint seed = unchecked((uint)(flickerSeed != 0 ? flickerSeed : GetEntityId().GetHashCode()));
            float phase = (seed % 65521) * .01371f;
            double t = time * Mathf.Max(.01f, flickerSpeed) + phase;
            if (mode == OperatingMode.Failing)
            {
                // Independent deterministic intervals: long pauses, flash clusters, and unstable recovery.
                uint cell = unchecked((uint)Math.Floor(t));
                float a = Hash(cell ^ seed), b = Hash((cell / 9) ^ (seed + 731));
                return b < .22f ? 0 : (a < .35f ? 0 : (a < .6f ? .12f : 1));
            }
            if (enableFlickering || mode == OperatingMode.Flickering)
            {
                float wave = randomFlicker ? Mathf.PerlinNoise((float)t * .51f, phase + .7f) : (.5f + .5f * Mathf.Sin((float)t));
                factor *= Mathf.Lerp(1, wave, Mathf.Clamp01(flickerStrength));
            }
            return factor;
        }
        static float Hash(uint n) { n ^= n >> 16; n *= 0x7feb352d; n ^= n >> 15; n *= 0x846ca68b; n ^= n >> 16; return (n & 0xffffff) / 16777215f; }
        public void ApplyNow() { ApplyAt(Time.realtimeSinceStartupAsDouble); }
        public void ApplyAt(double time)
        {
            if (!isActiveAndEnabled) return;
            bool far = false;
            if (Application.isPlaying && distanceCulling)
            {
                Transform observer = distanceObserver;
                if (!observer)
                {
                    if (!cachedCamera && time >= nextCameraLookup) { cachedCamera = Camera.main; nextCameraLookup = time + 2; }
                    if (cachedCamera) observer = cachedCamera.transform;
                }
                far = observer && (observer.position - transform.position).sqrMagnitude > cullDistance * cullDistance;
            }
            EffectiveIntensity = Mathf.Max(0, intensity) * EvaluateFactor(time);
            Color color = lightColor * (useColorTemperature ? Mathf.CorrelatedColorTemperatureToRGB(Mathf.Clamp(colorTemperature,1000,20000)) : Color.white);
            foreach (var binding in lights)
            {
                var l = binding.light; if (!l) continue;
                l.enabled = EffectiveIntensity > .0001f && !far;
                l.intensity = EffectiveIntensity * Mathf.Max(0, binding.weight);
                l.color = color; l.useColorTemperature = false;
                l.range = Mathf.Max(.01f, range);
                if (l.type == LightType.Spot) { l.spotAngle = spotAngle; l.innerSpotAngle = spotAngle * .65f; }
                l.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
                // Changing bake type at runtime would misrepresent baked GI.
                if (!Application.isPlaying) l.lightmapBakeType = bakeType;
            }
            block ??= new MaterialPropertyBlock();
            foreach (var binding in emitters)
            {
                if (!binding.renderer || binding.materialIndex < 0) continue;
                binding.renderer.GetPropertyBlock(block, binding.materialIndex);
                block.SetColor(Emission, color * (EffectiveIntensity * Mathf.Max(0, emissionMultiplier) * Mathf.Max(0, binding.weight)));
                binding.renderer.SetPropertyBlock(block, binding.materialIndex);
                block.Clear();
            }
            bool audible = Application.isPlaying && enableBuzzingSound && buzzingClip && EffectiveIntensity > .0001f && !far;
            if (audible && !buzz)
            {
                var child = new GameObject("FixtureBuzz_Runtime"); child.transform.SetParent(transform, false);
                child.hideFlags = HideFlags.DontSave; buzz = child.AddComponent<AudioSource>();
                buzz.loop = true; buzz.spatialBlend = 1; buzz.playOnAwake = false; buzz.maxDistance = range;
            }
            if (buzz)
            {
                if (buzz.clip != buzzingClip) { buzz.Stop(); buzz.clip = buzzingClip; }
                buzz.volume = buzzingVolume * Mathf.Clamp01(EffectiveIntensity / Mathf.Max(.001f, intensity));
                if (audible && !buzz.isPlaying) buzz.Play(); else if (!audible && buzz.isPlaying) buzz.Stop();
            }
        }
        void OnDisable()
        {
            EffectiveIntensity = 0;
            foreach (var b in lights) if (b.light) b.light.enabled = false;
            block ??= new MaterialPropertyBlock();
            foreach (var b in emitters) if (b.renderer && b.materialIndex >= 0)
            { b.renderer.GetPropertyBlock(block,b.materialIndex); block.SetColor(Emission,Color.black); b.renderer.SetPropertyBlock(block,b.materialIndex); block.Clear(); }
            if (buzz) buzz.Stop();
        }
        void OnDestroy() { if (buzz) { if (Application.isPlaying) Destroy(buzz.gameObject); else DestroyImmediate(buzz.gameObject); } }
    }
}
