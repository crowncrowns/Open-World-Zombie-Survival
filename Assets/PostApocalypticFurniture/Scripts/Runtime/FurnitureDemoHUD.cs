using UnityEngine;
using ZombieSurvival.Interaction;

namespace ZombieSurvival.Furniture
{
    /// <summary>Showcase-only presentation. Input remains in the existing player components.</summary>
    public sealed class FurnitureDemoHUD : MonoBehaviour
    {
        [SerializeField, Tooltip("Существующий PlayerInteractor демонстрационного игрока.")]
        private PlayerInteractor interactor;
        public void Configure(PlayerInteractor value) { interactor = value; }
        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(10, 8, Screen.width - 20, 60), "МЕБЕЛЬ · 200 ПРЕДМЕТОВ\nWASD — движение · мышь — обзор · F — взаимодействие · Esc — курсор", style);
            ZombieSurvival.Presentation.SurvivorHUD.DrawAimCrosshair(new Vector2(Screen.width*.5f,Screen.height*.5f),Color.white);
            if (interactor != null && interactor.Target != null)
                GUI.Label(new Rect(10, Screen.height * .64f, Screen.width - 20, 35), interactor.BindingLabel + " — " + interactor.Target.GetPrompt(interactor), style);
            if (interactor != null && !string.IsNullOrEmpty(interactor.Feedback))
                GUI.Label(new Rect(10, Screen.height * .7f, Screen.width - 20, 35), interactor.Feedback, style);
        }
    }
}
