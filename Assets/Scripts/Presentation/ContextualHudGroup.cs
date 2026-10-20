using UnityEngine;

namespace ZombieSurvival.Presentation
{
    // Presentation-only state: game components remain the source of all values.
    internal sealed class ContextualHudGroup
    {
        internal readonly CanvasGroup Canvas;
        private readonly RectTransform rect;
        private readonly Vector2 home;
        private float until = float.NegativeInfinity;
        internal ContextualHudGroup(RectTransform rect)
        {
            this.rect = rect; home = rect.anchoredPosition;
            Canvas = rect.gameObject.AddComponent<CanvasGroup>();
            Canvas.alpha = 0; Canvas.interactable = Canvas.blocksRaycasts = false;
        }
        internal void Hold(float seconds) => until = Time.unscaledTime + seconds;
        internal void Reset() { until = float.NegativeInfinity; Canvas.alpha = 0; }
        internal void Tick(bool needed, bool allowed, float fadeSeconds)
        {
            float target = allowed && (needed || Time.unscaledTime < until) ? 1 : 0;
            Canvas.alpha = allowed ? Mathf.MoveTowards(Canvas.alpha, target,
                Time.unscaledDeltaTime / Mathf.Max(.01f, fadeSeconds)) : 0;
            rect.anchoredPosition = home + Vector2.down * ((1 - Canvas.alpha) * 7);
        }
    }
}
