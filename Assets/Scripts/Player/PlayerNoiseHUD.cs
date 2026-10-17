using UnityEngine;
using UnityEngine.UI;

namespace ZombieSurvival.Player
{
    public sealed class PlayerNoiseHUD : MonoBehaviour
    {
        [Tooltip("Источник игровых звуков этого игрока. Индикатор показывает именно отправленные события слуха.")]
        [SerializeField] private PlayerNoiseEmitter source;
        [Tooltip("Полоса уровня шума.")]
        [SerializeField] private Image fill;
        [Tooltip("Текст уровня, типа движения и предельной дальности звука.")]
        [SerializeField] private Text label;
        [Tooltip("Громкость, соответствующая полной полосе (100%). Процент не означает шанс обнаружения.")]
        [SerializeField, Min(.1f)] private float fullScaleLoudness = 4f;
        private void Awake() { if (source == null) source = GetComponentInParent<PlayerNoiseEmitter>(); }
        private void LateUpdate()
        {
            if (source == null) return;
            float amount = Mathf.Clamp01(source.CurrentLoudness / fullScaleLoudness);
            if (fill != null) { fill.rectTransform.anchorMax = new Vector2(amount, 1); fill.color = Color.Lerp(new Color(.3f,.8f,.6f), new Color(1,.35f,.15f), amount); }
            if (label != null) label.text = amount <= 0f ? "ШУМ: 0% · Тишина    |    Alt — тихий шаг    Tab — рюкзак"
                : $"ШУМ: {amount:P0} · {source.CurrentNoiseLabel} · до {source.CurrentRange:0} м";
        }
    }
}
