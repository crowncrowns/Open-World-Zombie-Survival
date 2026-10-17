using UnityEngine;
using UnityEngine.UI;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerStaminaHUD : MonoBehaviour
    {
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private Image fill;
        [SerializeField] private Text reserveText;
        [SerializeField] private Text stateText;
        [SerializeField] private Color normalColor = new Color(.25f, .85f, .65f);
        [SerializeField] private Color lowColor = new Color(1f, .68f, .23f);
        [SerializeField] private Color exhaustedColor = new Color(.95f, .28f, .25f);
        [SerializeField, Range(0f, 1f)] private float lowThreshold = .25f;
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
