using UnityEngine;
using UnityEngine.UI;

namespace ZombieSurvival.Interaction
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class InteractionHUD : MonoBehaviour
    {
        [Tooltip("Компонент выбора интерактивной цели у игрока.")]
        [SerializeField] private PlayerInteractor interactor;
        [Tooltip("Панель подсказки взаимодействия.")]
        [SerializeField] private GameObject promptPanel;
        [Tooltip("Текст названия выбранного объекта.")]
        [SerializeField] private Text nameText;
        [Tooltip("Текст действия и кнопки взаимодействия.")]
        [SerializeField] private Text actionText;
        [Tooltip("Текст результата последнего взаимодействия.")]
        [SerializeField] private Text feedbackText;
        [Tooltip("Прицел в центре экрана.")]
        [SerializeField] private Image reticle;
        [Tooltip("Цвет прицела без интерактивной цели.")]
        [SerializeField] private Color neutralColor = new Color(1f, 1f, 1f, .65f);
        [Tooltip("Цвет прицела для доступного действия.")]
        [SerializeField] private Color usableColor = new Color(.3f, 1f, .7f);
        [Tooltip("Цвет прицела для недоступного действия.")]
        [SerializeField] private Color unavailableColor = new Color(1f, .65f, .3f);

        private void Awake() { if (interactor == null) interactor = GetComponentInParent<PlayerInteractor>(); }
        private void LateUpdate()
        {
            bool active = interactor != null && interactor.isActiveAndEnabled && interactor.IsAvailable;
            var target = active ? interactor.Target : null;
            bool usable = target != null && interactor.CanUseTarget;
            if (promptPanel != null) promptPanel.SetActive(target != null);
            if (target != null)
            {
                if (nameText != null) nameText.text = target.DisplayName;
                if (actionText != null) actionText.text = usable ? "[" + interactor.BindingLabel + "]  " + target.GetPrompt(interactor) : "Недоступно";
            }
            if (feedbackText != null) feedbackText.text = active ? interactor.Feedback : "";
            if (reticle != null)
            {
                reticle.enabled = active;
                reticle.color = target == null ? neutralColor : usable ? usableColor : unavailableColor;
            }
        }
        private void OnDisable()
        {
            if (promptPanel != null) promptPanel.SetActive(false);
            if (feedbackText != null) feedbackText.text = "";
            if (reticle != null) reticle.enabled = false;
        }
    }
}
