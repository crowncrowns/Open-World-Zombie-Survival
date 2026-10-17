using UnityEngine;
using UnityEngine.UI;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerStaminaHUD : MonoBehaviour
    {
        [Tooltip("Компонент выносливости, отображаемый этим HUD.")]
        [SerializeField] private PlayerStamina stamina;
        [Tooltip("Полоса уровня шума.")]
        [SerializeField] private Image fill;
        [Tooltip("Текст текущего и максимального запаса выносливости.")]
        [SerializeField] private Text reserveText;
        [Tooltip("Текст состояния истощения в HUD.")]
        [SerializeField] private Text stateText;
        [Tooltip("Цвет показателя при нормальном запасе.")]
        [SerializeField] private Color normalColor = new Color(.25f, .85f, .65f);
        [Tooltip("Цвет показателя при низком запасе.")]
        [SerializeField] private Color lowColor = new Color(1f, .68f, .23f);
        [Tooltip("Цвет показателя при полном истощении.")]
        [SerializeField] private Color exhaustedColor = new Color(.95f, .28f, .25f);
        [Tooltip("Доля запаса, ниже которой полоса меняет цвет на предупреждающий.")]
        [SerializeField, Range(0f, 1f)] private float lowThreshold = .25f;
        [Tooltip("Скорость изменения заполнения полосы в секунду; 0 — обновлять сразу.")]
        [SerializeField, Min(0f)] private float fillSpeed = 2f;
        private float displayed;
        private int lastCurrent = -1, lastCapacity = -1;

        private void Awake()
        {
            if (stamina == null) stamina = GetComponentInParent<PlayerStamina>();
            displayed = stamina != null ? stamina.Normalized : 1f;
        }

        private void LateUpdate()
        {
            if (stamina == null) return;
            displayed = fillSpeed <= 0f ? stamina.Normalized : Mathf.MoveTowards(displayed, stamina.Normalized, fillSpeed * Time.deltaTime);
            if (fill != null)
            {
                fill.fillAmount = displayed;
                // A plain Image without a sprite gives a clean bar at any resolution.
                fill.rectTransform.anchorMax = new Vector2(displayed, 1f);
                fill.color = stamina.IsExhausted ? exhaustedColor : stamina.Normalized <= lowThreshold ? lowColor : normalColor;
            }
            int value = Mathf.CeilToInt(stamina.Current), max = Mathf.CeilToInt(stamina.Capacity);
            if (reserveText != null && (lastCurrent != value || lastCapacity != max))
            {
                reserveText.text = value + " / " + max;
                lastCurrent = value; lastCapacity = max;
            }
            if (stateText != null) stateText.text = stamina.IsExhausted ? "ИСТОЩЕНИЕ — ПЕРЕДОХНИ" : "";
        }
    }
}
