using UnityEngine;

namespace AdvancedGrassPainter
{
    [DisallowMultipleComponent]
    public sealed class GrassObstacle : MonoBehaviour
    {
        [Tooltip("Исключить эту и дочернюю геометрию из препятствий и поверхностей кисти.")]
        public bool ignore;
        [Tooltip("Применять форму Renderer даже при наличии более грубого физического Collider.")]
        public bool useRenderGeometry = true;
        [Tooltip("Дополнительный отступ от этого препятствия, м."), Range(0, 2)]
        public float additionalClearance;
    }
}
