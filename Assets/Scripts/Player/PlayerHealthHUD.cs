using UnityEngine;
using UnityEngine.UI;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealthHUD : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private PlayerLifeCycle lifeCycle;
        [SerializeField] private Image fill;
        [SerializeField] private Text valueText;
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private Text respawnText;
        [SerializeField] private Image damageFlash;
        [SerializeField] private Color normalColor = new Color(.92f, .3f, .32f);
        [SerializeField] private Color lowColor = new Color(1f, .65f, .2f);
        [SerializeField, Range(0f, 1f)] private float lowThreshold = .25f;
        [SerializeField, Range(0f, .5f)] private float flashOpacity = .15f;
        [SerializeField, Min(.01f)] private float flashDuration = .3f;
        private float flashUntil;
        private void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            if (lifeCycle == null) lifeCycle = GetComponentInParent<PlayerLifeCycle>();
        }
        private void OnEnable() { if (health != null) { health.Damaged += OnDamaged; health.Revived += OnRevived; } }
        private void OnDisable()
        {
            if (health != null) { health.Damaged -= OnDamaged; health.Revived -= OnRevived; }
            if (damageFlash != null) damageFlash.color = Color.clear;
            if (deathPanel != null) deathPanel.SetActive(false);
        }
        private void OnDamaged(DamageInfo damage) => flashUntil = Time.time + flashDuration;
        private void OnRevived() => flashUntil = 0f;
        private void LateUpdate()
        {
            if (health == null) return;
            if (fill != null)
            {
                fill.rectTransform.anchorMax = new Vector2(health.Normalized, 1f);
                fill.color = health.Normalized <= lowThreshold ? lowColor : normalColor;
            }
            if (valueText != null) valueText.text = Mathf.CeilToInt(health.Current) + " / " + Mathf.CeilToInt(health.Maximum);
            if (deathPanel != null) deathPanel.SetActive(health.IsDead);
            if (respawnText != null && health.IsDead && lifeCycle != null)
                respawnText.text = !string.IsNullOrEmpty(lifeCycle.RespawnError) ? lifeCycle.RespawnError
                    : lifeCycle.RespawnWait > 0f ? "Возрождение через " + Mathf.CeilToInt(lifeCycle.RespawnWait) + " с"
                    : "[" + lifeCycle.BindingLabel + "]  Возродиться";
            if (damageFlash != null) damageFlash.color = new Color(1f, .05f, .05f,
                Mathf.Clamp01((flashUntil - Time.time) / Mathf.Max(.01f, flashDuration)) * flashOpacity);
        }
    }
}
