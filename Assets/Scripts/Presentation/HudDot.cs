using UnityEngine;
using UnityEngine.UI;

namespace ZombieSurvival.Presentation
{
    public sealed class HudDot : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear(); var center = rectTransform.rect.center;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            vertices.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= 24; i++)
            {
                float angle = i * Mathf.PI * 2 / 24;
                vertices.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) vertices.AddTriangle(0, i, i + 1);
            }
        }
    }
}
