using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdvancedGrassPainter
{
    [Serializable]
    public struct GrassPlant
    {
        public Vector3 position, normal;
        public int seed;
        public GrassKind kind;
        public float height, width, yaw, clearance, wind;
        public Color root, tip;
    }

    [Serializable]
    public sealed class GrassStroke
    {
        public Vector3 center;
        public int sequence;
        public GrassSettings settings;
        public bool bounded;
        public Bounds bounds;
    }

    // World-space records deliberately survive changes to the storage object's transform.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GrassDataStorage : MonoBehaviour
    {
        [Tooltip("Независимый редактируемый слой; координаты пучков заданы в пространстве мира.")]
        public List<GrassPlant> plants = new List<GrassPlant>();
        [Tooltip("История генеративных мазков для воспроизводимой перегенерации слоя.")]
        public List<GrassStroke> strokes = new List<GrassStroke>();
        [Tooltip("Следующий номер мазка, сохраняется вместе со сценой.")]
        public int nextStroke;
        [Tooltip("Материал активного рендер-пайплайна.")]
        public Material material;
        [Tooltip("Размер чанка, м."), Range(2, 32)] public float chunkSize = 8;
        [Tooltip("Максимальная дальность отрисовки, м."), Range(5, 300)] public float drawDistance = 85;
        [Tooltip("Профиль детализации и дальности.")] public GrassQuality quality = GrassQuality.High;
        [Tooltip("Отбрасывать тени травой.")] public bool castShadows = true;
        [NonSerialized] public double generationMilliseconds;
        [NonSerialized] public int rejectedObstacles;
        [NonSerialized] public int revision;
        [NonSerialized] public GrassRenderingSystem rendering;

        public void Changed() { unchecked { revision++; } if (rendering != null) rendering.Invalidate(); }
        void OnEnable() { rendering = new GrassRenderingSystem(this); rendering.Enable(); }
        void OnDisable() { rendering?.Dispose(); rendering = null; }
        void OnValidate() { chunkSize = Mathf.Clamp(chunkSize, 2, 32); Changed(); }
        void Update() { rendering?.RebuildIfNeeded(); }
    }
}
