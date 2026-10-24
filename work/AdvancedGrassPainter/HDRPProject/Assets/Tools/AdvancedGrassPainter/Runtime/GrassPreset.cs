using System;
using UnityEngine;

namespace AdvancedGrassPainter
{
    public enum GrassKind { Short, Wild, Dry, Overgrown, Mixed, NeglectedUrban }
    public enum GrassQuality { Low, Medium, High, Ultra }
    public enum GrassBrushMode { Paint, Erase, Smooth, Scatter, Fill, Replace, CollisionCleanup }

    [Serializable]
    public sealed class GrassSettings
    {
        [Tooltip("Радиус кисти, м."), Range(.1f, 100)] public float radius = 3;
        [Tooltip("Плотность покрытия, доля 0–1."), Range(0, 1)] public float density = .75f;
        [Tooltip("Минимальное расстояние между центрами пучков, м."), Range(.04f, 2)] public float spacing = .23f;
        [Tooltip("Высота травы, м."), Range(.02f, 2)] public float height = .48f;
        [Tooltip("Ширина травинки, м."), Range(.005f, .3f)] public float width = .025f;
        [Tooltip("Разброс размеров, доля 0–1."), Range(0, 1)] public float randomScale = .5f;
        [Tooltip("Разброс поворота вокруг нормали, градусы."), Range(0, 360)] public float randomRotation = 360;
        [Tooltip("Выраженность скоплений, доля 0–1."), Range(0, 1)] public float clumping = .65f;
        [Tooltip("Доля радиуса мягкой границы кисти, 0–1."), Range(0, 1)] public float edgeFalloff = .6f;
        [Tooltip("Минимальный зазор от конечной геометрии до препятствия, м."), Range(0, 2)] public float clearance = .04f;
        [Tooltip("Ширина зоны плавного уменьшения плотности у препятствий, м."), Range(.01f, 3)] public float obstacleFade = .4f;
        [Tooltip("Максимальный уклон разрешённой поверхности, градусы."), Range(0, 85)] public float maxSlope = 55;
        [Tooltip("Высота поиска поверхности относительно точки кисти, м."), Range(.1f, 20)] public float projection = 4;
        [Tooltip("Запрет травы под перекрытиями в пределах этой высоты, м."), Range(0, 100)] public float overhead = 15;
        [Tooltip("Слои разрешённых поверхностей." )] public LayerMask surfaceLayers = 1;
        [Tooltip("Слои препятствий. Триггеры не учитываются." )] public LayerMask obstacleLayers = ~0;
        [Tooltip("Тип растительности." )] public GrassKind kind = GrassKind.NeglectedUrban;
        [Tooltip("Исходный тип для режима Replace." )] public GrassKind replaceFrom = GrassKind.Short;
        [Tooltip("Основной детерминированный seed." )] public int globalSeed = 12731;
        [Tooltip("Seed кисти; номер мазка хранится в слое." )] public int brushSeed = 91;
        [Tooltip("Сохранять seed при перегенерации." )] public bool lockSeed = true;
        [Tooltip("Длина волны неоднородности роста, м."), Range(.1f, 30)] public float noiseScale = 3;
        [Tooltip("Коэффициент роста участка: 0 создаёт вытоптанную область, 1 — обычный рост."), Range(0, 2)] public float growth = 1;
        [Tooltip("Цвет у основания травинки." )] public Color rootColor = new Color(.11f, .16f, .035f);
        [Tooltip("Цвет верхушки живой травы." )] public Color tipColor = new Color(.39f, .48f, .12f);
        [Tooltip("Цвет сухих участков." )] public Color dryColor = new Color(.57f, .43f, .20f);
        [Tooltip("Максимальное смещение верхушки ветром, м; резервируется при проверке коллизий."), Range(0, .5f)] public float windEnvelope = .09f;
        public GrassSettings Copy() => JsonUtility.FromJson<GrassSettings>(JsonUtility.ToJson(this));
        public void Validate()
        {
            radius=Clamp(radius,.1f,100);density=Clamp(density,0,1);spacing=Clamp(spacing,.04f,2);
            height=Clamp(height,.02f,2);width=Clamp(width,.005f,.3f);randomScale=Clamp(randomScale,0,1);randomRotation=Clamp(randomRotation,0,360);
            clumping=Clamp(clumping,0,1);edgeFalloff=Clamp(edgeFalloff,0,1);clearance=Clamp(clearance,0,2);obstacleFade=Clamp(obstacleFade,.01f,3);
            maxSlope=Clamp(maxSlope,0,85);projection=Clamp(projection,.1f,20);overhead=Clamp(overhead,0,100);noiseScale=Clamp(noiseScale,.1f,30);growth=Clamp(growth,0,2);windEnvelope=Clamp(windEnvelope,0,.5f);
        }
        static float Clamp(float value,float min,float max)=>float.IsNaN(value)||float.IsInfinity(value)?min:Mathf.Clamp(value,min,max);
    }

    [CreateAssetMenu(menuName = "Environment/Advanced Grass Preset")]
    public sealed class GrassPreset : ScriptableObject
    {
        public GrassSettings settings = new GrassSettings();
    }
}
