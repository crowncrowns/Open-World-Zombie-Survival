using UnityEngine;
using UnityEngine.UI;

namespace ZombieSurvival.Presentation
{
    // Historical class/GUID retained for existing prefab references; draws a radial HP annulus.
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("Zombie Survival/Presentation/Radial Health Ring")]
    public sealed class HollowHealthRing : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField, Tooltip("Цвет потерянного здоровья: белый сектор от 12 часов по часовой стрелке.")]
        private Color lostHealthColor = Color.white;
        [SerializeField, Range(.2f, .9f), Tooltip("Внутренний радиус как доля внешнего: 0,68 оставляет прозрачный центр и заметную толщину кольца.")]
        private float innerRadiusRatio = .68f;
        private float normalizedHealth = 1f;
        public float NormalizedHealth => normalizedHealth;
        public void SetHealth(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            if (Mathf.Approximately(normalizedHealth, normalized)) return;
            normalizedHealth = normalized;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            float lostAngle = (1f - normalizedHealth) * Mathf.PI * 2f;
            Arc(vh, rect.center, radius, radius * innerRadiusRatio, 0f, lostAngle, lostHealthColor);
            Arc(vh, rect.center, radius, radius * innerRadiusRatio, lostAngle, Mathf.PI * 2f, color);
        }
        private static void Arc(VertexHelper vh, Vector2 center, float radius, float inner, float start, float end, Color tint)
        {
            float sweep = end - start;
            if (sweep <= .000001f) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(sweep / (Mathf.PI * 2f) * 96));
            int first = vh.currentVertCount;
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.Lerp(start, end, i / (float)steps);
                var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                vh.AddVert(center + direction * radius, tint, Vector2.zero);
                vh.AddVert(center + direction * inner, tint, Vector2.zero);
                if (i > 0)
                {
                    int n = first + (i - 1) * 2;
                    vh.AddTriangle(n, n + 2, n + 1);
                    vh.AddTriangle(n + 2, n + 3, n + 1);
                }
            }
        }
    }
}

