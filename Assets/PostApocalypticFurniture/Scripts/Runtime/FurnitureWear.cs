using System;
using UnityEngine;

namespace ZombieSurvival.Furniture
{
    [Serializable]
    public sealed class FurnitureWearState
    {
        public int marker, seed;
        public float dirt, fade, damage;
        public Color fadeColor;
    }

    /// <summary>Per-instance wear without material clones or geometry duplication.</summary>
    [DisallowMultipleComponent]
    public sealed class FurnitureWear : MonoBehaviour
    {
        [SerializeField, Tooltip("Seed пространственной маски износа. Одинаковый seed воспроизводит тот же рисунок.")]
        private int seed = 1;
        [SerializeField, Range(0f, 1f), Tooltip("Количество грязи, 0–1. Не увеличивает число полигонов.")]
        private float dirt = .4f;
        [SerializeField, Range(0f, 1f), Tooltip("Выцветание материалов, 0–1.")]
        private float fade = .25f;
        [SerializeField, Range(0f, 1f), Tooltip("Степень повреждения поверхности и доступных повреждённых деталей, 0–1.")]
        private float damage = .3f;
        [SerializeField, ColorUsage(false), Tooltip("Оттенок выцветания ткани и краски. Общий материал остаётся прежним.")]
        private Color fadeColor = new Color(.56f, .48f, .37f, 1f);
        [SerializeField, Tooltip("Renderer предмета, включая все LOD. Маски задаются через MaterialPropertyBlock.")]
        private Renderer[] renderers = Array.Empty<Renderer>();
        [SerializeField, Tooltip("Дополнительные авторские повреждённые детали. Их видимость определяется степенью damage.")]
        private GameObject[] damagedParts = Array.Empty<GameObject>();
        [SerializeField, Tooltip("Порог видимости каждой повреждённой детали, 0–1.")]
        private float[] damageThresholds = Array.Empty<float>();
        private MaterialPropertyBlock block;
        private static readonly int DirtId = Shader.PropertyToID("_FurnitureDirt");
        private static readonly int FadeId = Shader.PropertyToID("_FurnitureFade");
        private static readonly int DamageId = Shader.PropertyToID("_FurnitureDamage");
        private static readonly int SeedId = Shader.PropertyToID("_FurnitureSeed");
        private static readonly int FadeColorId = Shader.PropertyToID("_FurnitureWearColor");
        public int Seed => seed;
        public float Dirt => dirt;
        public float Fade => fade;
        public float Damage => damage;
        public Color FadeColor => fadeColor;

        public void Configure(int instanceSeed, float dirtAmount, float fadeAmount, float damageAmount, Renderer[] targets)
        {
            seed = instanceSeed; dirt = Mathf.Clamp01(dirtAmount); fade = Mathf.Clamp01(fadeAmount); damage = Mathf.Clamp01(damageAmount);
            renderers = targets ?? Array.Empty<Renderer>(); Apply();
        }
        public void SetDamageGroups(GameObject[] targets, float[] thresholds)
        {
            if (targets == null || thresholds == null || targets.Length != thresholds.Length)
                throw new ArgumentException("Число повреждённых деталей и порогов должно совпадать.");
            damagedParts = targets; damageThresholds = thresholds; Apply();
        }
        public void SetWear(int instanceSeed, float dirtAmount, float fadeAmount, float damageAmount, Color fadedTint)
        {
            seed = instanceSeed; dirt = Mathf.Clamp01(dirtAmount); fade = Mathf.Clamp01(fadeAmount); damage = Mathf.Clamp01(damageAmount);
            fadeColor = new Color(Mathf.Clamp01(fadedTint.r), Mathf.Clamp01(fadedTint.g), Mathf.Clamp01(fadedTint.b), Mathf.Clamp01(fadedTint.a)); Apply();
        }
        private void OnEnable() => Apply();
        private void OnValidate()
        {
            dirt = Mathf.Clamp01(dirt); fade = Mathf.Clamp01(fade); damage = Mathf.Clamp01(damage);
            if (!Application.isPlaying) Apply();
        }
        public void Apply()
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            // One block per material slot preserves both the authored PBR values and device indicators.
            foreach (var target in renderers)
            {
                if (target == null) continue;
                int count = target.sharedMaterials.Length;
                for (int slot = 0; slot < count; slot++)
                {
                    target.GetPropertyBlock(block, slot);
                    block.SetFloat(DirtId, dirt); block.SetFloat(FadeId, fade); block.SetFloat(DamageId, damage);
                    block.SetFloat(SeedId, PositiveSeed(seed)); block.SetColor(FadeColorId, fadeColor);
                    target.SetPropertyBlock(block, slot);
                }
            }
            int parts = Mathf.Min(damagedParts != null ? damagedParts.Length : 0, damageThresholds != null ? damageThresholds.Length : 0);
            for (int i = 0; i < parts; i++) if (damagedParts[i] != null) damagedParts[i].SetActive(damage >= damageThresholds[i]);
        }
        private static float PositiveSeed(int value) => (uint)value % 65521u;
        public FurnitureWearState CaptureState() => new FurnitureWearState
        { marker = 935, seed = seed, dirt = dirt, fade = fade, damage = damage, fadeColor = fadeColor };
        public bool ValidateState(FurnitureWearState value) => value != null && value.marker == 935 && Unit(value.dirt) && Unit(value.fade)
            && Unit(value.damage) && Unit(value.fadeColor.r) && Unit(value.fadeColor.g) && Unit(value.fadeColor.b) && Unit(value.fadeColor.a);
        public void RestoreStateSilently(FurnitureWearState value)
        { seed = value.seed; dirt = value.dirt; fade = value.fade; damage = value.damage; fadeColor = value.fadeColor; Apply(); }
        private static bool Unit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
    }
}
