using System;
using UnityEngine;

namespace AK74Reference
{
    // Animation events change visual magazine state only. Ammunition and damage stay in the host game's backend.
    [DisallowMultipleComponent]
    public sealed class AK74Presentation : MonoBehaviour
    {
        [Tooltip("Рендереры установленного магазина во всех LOD. Только визуальное состояние.")]
        public Renderer[] magazineRenderers = Array.Empty<Renderer>();
        [Tooltip("Рендереры запасного магазина во всех LOD. По умолчанию скрыты.")]
        public Renderer[] spareMagazineRenderers = Array.Empty<Renderer>();
        [Tooltip("Явная дульная точка. Локальная синяя ось направлена из ствола.")]
        public Transform muzzle;
        [Tooltip("Точка выброса гильзы. Эффекты подключаются отдельно.")]
        public Transform eject;
        [Tooltip("Цель правой руки; окончательная поза зависит от рук игрока.")]
        public Transform rightHand;
        [Tooltip("Цель левой руки; окончательная поза зависит от рук игрока.")]
        public Transform leftHand;
        [Tooltip("Опорная точка механического прицела; окончательное ADS сведение зависит от камеры.")]
        public Transform aim;

        public event Action<string> VisualEvent;
        public int MagazineRemovedCount { get; private set; }
        public int SpareShownCount { get; private set; }
        public int MagazineInsertedCount { get; private set; }
        public int ReloadCompleteCount { get; private set; }

        void OnEnable() => ResetMagazineVisibility();
        void OnDisable() => ResetMagazineVisibility();

        public void ResetMagazineVisibility()
        {
            SetVisible(magazineRenderers, true);
            SetVisible(spareMagazineRenderers, false);
        }

        public void OnMagazineRemoved() { SetVisible(magazineRenderers, false); MagazineRemovedCount++; Emit(nameof(OnMagazineRemoved)); }
        public void OnSpareMagazineShown() { SetVisible(spareMagazineRenderers, true); SpareShownCount++; Emit(nameof(OnSpareMagazineShown)); }
        public void OnMagazineInserted() { ResetMagazineVisibility(); MagazineInsertedCount++; Emit(nameof(OnMagazineInserted)); }
        public void OnReloadComplete() { ResetMagazineVisibility(); ReloadCompleteCount++; Emit(nameof(OnReloadComplete)); }
        public void OnShot() => Emit(nameof(OnShot));
        public void OnBoltBack() => Emit(nameof(OnBoltBack));
        public void OnBoltForward() => Emit(nameof(OnBoltForward));
        public void OnSafetyChanged(int mode) => Emit(nameof(OnSafetyChanged));
        public void OnWeaponFire(int mode) => Emit(nameof(OnWeaponFire));
        public void OnBoltPulled() => Emit(nameof(OnBoltPulled));
        public void OnBoltReleased() => Emit(nameof(OnBoltReleased));
        public void OnFire() => Emit(nameof(OnFire));
        public void OnBolt() => Emit(nameof(OnBolt));
        public void OnSafety() => Emit(nameof(OnSafety));
        public void OnDraw() => Emit(nameof(OnDraw));
        public void OnHolster() => Emit(nameof(OnHolster));
        public void OnInspect() => Emit(nameof(OnInspect));
        void Emit(string name) => VisualEvent?.Invoke(name);
        static void SetVisible(Renderer[] items, bool visible)
        {
            if (items == null) return;
            foreach (var item in items) if (item != null) item.enabled = visible;
        }
    }
}
