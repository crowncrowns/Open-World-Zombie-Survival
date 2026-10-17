using UnityEngine;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Presentation
{
    /// <summary>Presentation only: reads common Health, never creates combat targets or gameplay noise.</summary>
    [DisallowMultipleComponent]
    public sealed class ActorHealthIndicator : MonoBehaviour
    {
        [SerializeField, Tooltip("Общее здоровье корня актёра; единственный источник значения HP.")]
        private Health health;
        [SerializeField, Tooltip("Мировой Canvas кольца; без камеры, EventSystem и Raycaster.")]
        private Canvas indicatorCanvas;
        [SerializeField, Tooltip("Полое кольцо: красное оставшееся здоровье, белый сектор потерь.")]
        private HollowHealthRing healthGraphic;
        [SerializeField, Tooltip("Кость головы; следует за моделью при ползании. При отсутствии используется корень актёра.")]
        private Transform head;
        [SerializeField, Min(0f), Tooltip("Высота центра кольца над головой, м.")]
        private float headClearance = .28f;
        [SerializeField, Tooltip("Необязательная игровая камера; иначе активная Camera.main.")]
        private Camera viewCamera;

        public void Bind(Health source, Canvas canvas, HollowHealthRing graphic, Transform headBone)
        { health = source; indicatorCanvas = canvas; healthGraphic = graphic; head = headBone; headClearance = .28f; Refresh(); }
        private void OnEnable() { if (health == null) health = GetComponent<Health>(); Refresh(); }
        private void OnDisable() { if (indicatorCanvas != null) indicatorCanvas.enabled = false; }
        private void LateUpdate()
        {
            // Polling deliberately also handles disabled Domain+Scene Reload and revive while hidden.
            Refresh();
            if (indicatorCanvas == null || !indicatorCanvas.enabled) return;
            var camera = viewCamera != null && viewCamera.isActiveAndEnabled ? viewCamera : Camera.main;
            if (camera == null) { indicatorCanvas.enabled = false; return; }
            var position = head != null ? head.position : transform.position + Vector3.up * 1.65f;
            indicatorCanvas.transform.SetPositionAndRotation(position + Vector3.up * headClearance, camera.transform.rotation);
        }
        private void Refresh()
        {
            if (health == null || indicatorCanvas == null) return;
            indicatorCanvas.enabled = health.isActiveAndEnabled && !health.IsDead;
            if (healthGraphic != null) healthGraphic.SetHealth(health.Normalized);
        }
    }
}



