using System;
using UnityEngine;

namespace PostApocalypse.Balconies
{
    [DisallowMultipleComponent]
    public sealed class BalconyKitModule : MonoBehaviour
    {
        [Header("Модульная посадка")]
        [Tooltip("Конструктивный тип балкона из каталога комплекта.")]
        public string balconyType = "Balcony_01_Soviet";

        [Tooltip("Состояние геометрии: A — потрёпанный, B — заброшенный, C — повреждённый, D — руинированный.")]
        public string wearState = "A";

        [Tooltip("Ширина фасадной ячейки, метры.")]
        public float facadeModuleWidth = 3f;

        [Tooltip("Ширина балконной плиты, метры.")]
        public float balconyWidth = 2.7f;

        [Tooltip("Глубина балкона от фасадной плоскости, метры.")]
        public float balconyDepth = 1.2f;

        [Tooltip("Ширина совместимого дверного проёма, метры.")]
        public float doorOpeningWidth = .9f;

        [Tooltip("Высота совместимого дверного проёма, метры.")]
        public float doorOpeningHeight = 2.1f;

        [Tooltip("Воспроизводимый seed оттенка и локального износа экземпляра.")]
        public int randomSeed = 2401024;

        [Tooltip("Сила оттеночного разброса материалов данного экземпляра.")]
        [Range(0f, .12f)] public float tintVariation = .045f;

        public void ApplySeed(int seed)
        {
            randomSeed = seed;
            var random = new System.Random(seed);
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                float value = 1f + ((float)random.NextDouble() * 2f - 1f) * tintVariation;
                float warm = 1f + ((float)random.NextDouble() * 2f - 1f) * tintVariation * .35f;
                Color tint = new Color(value * warm, value, value / warm, 1f);
                block.SetColor("_BaseColor", tint);
                block.SetColor("_Color", tint);
                block.SetFloat("_Smoothness", Mathf.Clamp01(.28f + ((float)random.NextDouble() - .5f) * tintVariation));
                renderer.SetPropertyBlock(block);
            }
        }

        public bool HasValidMountLayout(float tolerance = .002f)
        {
            Transform mounts = transform.Find("MountPoints");
            if (mounts == null) return false;
            Transform left = mounts.Find("WallAttachment_Left");
            Transform right = mounts.Find("WallAttachment_Right");
            Transform door = mounts.Find("DoorAnchor");
            Transform front = mounts.Find("BalconyFront");
            if (left == null || right == null || door == null || front == null) return false;
            return Mathf.Abs(left.localPosition.x + balconyWidth * .5f) <= tolerance
                && Mathf.Abs(right.localPosition.x - balconyWidth * .5f) <= tolerance
                && Mathf.Abs(left.localPosition.z) <= tolerance
                && Mathf.Abs(right.localPosition.z) <= tolerance
                && Mathf.Abs(door.localPosition.y - .15f) <= tolerance
                && Mathf.Abs(front.localPosition.z - balconyDepth) <= tolerance;
        }
    }
}
