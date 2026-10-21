using UnityEngine;
namespace ZombieSurvival.Presentation
{
    /// <summary>Только подготовка кэшированных иконок вне OnGUI/URP RenderLoop; не игровой актёр и не владелец предметов.</summary>
    [DefaultExecutionOrder(30000)]
    public sealed class ModelPreviewPump : MonoBehaviour
    {
        private void LateUpdate()=>ItemModelIcons.RenderPending();
    }
}
