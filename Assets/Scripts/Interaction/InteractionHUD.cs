using UnityEngine;
using UnityEngine.UI;

namespace ZombieSurvival.Interaction
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class InteractionHUD : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private Text nameText;
        [SerializeField] private Text actionText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Image reticle;
        [SerializeField] private Color neutralColor = new Color(1f, 1f, 1f, .65f);
        [SerializeField] private Color usableColor = new Color(.3f, 1f, .7f);
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
