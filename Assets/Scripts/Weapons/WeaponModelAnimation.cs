using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;

namespace ZombieSurvival.Weapons
{
    /// <summary>
    /// Представление единственной модели в руках. Слушает уже совершённые действия PlayerWeapons;
    /// события Animator никогда не расходуют патроны, не изнашивают предмет и не наносят урон.
    /// Мировой предмет и иконка без Initialize остаются самостоятельными визуальными ассетами.
    /// </summary>
    [DefaultExecutionOrder(180), DisallowMultipleComponent]
    public sealed class WeaponModelAnimation : MonoBehaviour
    {
        [SerializeField, InspectorName("Аниматор модели"), Tooltip("Animator механизма оружия. Без ссылки ищется первый дочерний Animator; Animator персонажа не используется.")]
        private Animator modelAnimator;
        [SerializeField, InspectorName("Основной магазин"), Tooltip("Рендереры установленного магазина во всех LOD. Восстанавливаются при отмене перезарядки; пустой массив допустим.")]
        private Renderer[] installedMagazine = new Renderer[0];
        [SerializeField, InspectorName("Запасной магазин"), Tooltip("Рендереры временного запасного магазина во всех LOD. После отмены/скрытия модели выключаются; пустой массив допустим.")]
        private Renderer[] spareMagazine = new Renderer[0];
        [SerializeField, InspectorName("Состояние покоя"), Tooltip("Точное имя состояния Animator на слое 0. Пустое или отсутствующее состояние пропускается без изменения боевой логики.")]
        private string idleState = "Idle";
        [SerializeField, InspectorName("Доставание"), Tooltip("Точное имя состояния доставания. Запускается один раз для новой модели, а не после каждого скрытия у стены.")]
        private string equipState = "Equip";
        [SerializeField, InspectorName("Выстрел"), Tooltip("Точное имя состояния одного выстрела. При автоматическом огне каждый совершённый выстрел перезапускает механизм.")]
        private string fireState = "Fire_Single";
        [SerializeField, InspectorName("Пустая перезарядка"), Tooltip("Точное имя состояния перезарядки пустого магазина. Скорость клипа согласуется с длительностью в WeaponDefinition.")]
        private string emptyReloadState = "Reload_Empty";
        [SerializeField, InspectorName("Неполная перезарядка"), Tooltip("Точное имя состояния перезарядки с оставшимися патронами. Не меняет количество патронов самостоятельно.")]
        private string tacticalReloadState = "Reload_Tactical";
        [SerializeField, InspectorName("Ходьба"), Tooltip("Необязательное циклическое состояние движения оружия при обычном/медленном шаге.")]
        private string walkState = "Walk";
        [SerializeField, InspectorName("Бег"), Tooltip("Необязательное циклическое состояние движения оружия выше порога бега, без изменения скорости персонажа.")]
        private string runState = "Run";
        [SerializeField, InspectorName("Спринт"), Tooltip("Необязательное состояние спринта. Владелец может полностью скрывать оружие на спринте; компонент не обходит это ограничение.")]
        private string sprintState = "Sprint";
        [SerializeField, Min(0), InspectorName("Порог движения"), Tooltip("Минимальная фактическая горизонтальная скорость для анимации шага, м/с.")]
        private float movementThreshold = .1f;
        [SerializeField, Min(.1f), InspectorName("Порог бега"), Tooltip("Горизонтальная скорость перехода от Walk к Run, м/с. Учитывается только реальное движение мотора.")]
        private float runThreshold = 4.5f;

        private readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        private PlayerWeapons owner;
        private FirstPersonController motor;
        private ItemStack original;
        private WeaponDefinition definition;
        private bool subscribed, initialized, trackingReload;
        private float actionEnds, reloadStartedAt, reloadDuration;
        private string activeState, activeReloadState;
        private AnimationClip activeReloadClip;
        private AnimationEvent[] reloadEvents;

        public Animator ModelAnimator => modelAnimator;

        /// <summary>Вызывается PlayerWeapons после создания HeldWeapon. Не назначать компонент на игровой корень игрока.</summary>
        public void Initialize(PlayerWeapons weaponOwner)
        {
            if (initialized && owner == weaponOwner && owner != null && original == owner.Equipped) return;
            Unsubscribe();
            owner = weaponOwner;
            motor = owner != null ? owner.GetComponent<FirstPersonController>() : null;
            original = owner != null ? owner.Equipped : null;
            definition = original != null && !original.IsEmpty ? original.Definition.Weapon : null;
            initialized = true;
            trackingReload = false;
            ResolveAnimator();
            Subscribe();
            RestoreMagazine();
            if (!IsOwned()) return;
            actionEnds = Time.time + Play(equipState);
            if (owner.IsReloading) Reload(definition);
        }

        private void ResolveAnimator()
        {
            if (modelAnimator == null) modelAnimator = GetComponentInChildren<Animator>(true);
            clips.Clear();
            if (modelAnimator == null || modelAnimator.runtimeAnimatorController == null) return;
            modelAnimator.applyRootMotion = false;
            foreach (var clip in modelAnimator.runtimeAnimatorController.animationClips)
                if (clip != null && !clips.ContainsKey(clip.name)) clips.Add(clip.name, clip);
        }

        private void Subscribe()
        {
            if (subscribed || owner == null) return;
            owner.ShotFired += Fire;
            owner.ReloadStarted += Reload;
            owner.WeaponEquipped += Equipped;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (owner != null && subscribed)
            {
                owner.ShotFired -= Fire;
                owner.ReloadStarted -= Reload;
                owner.WeaponEquipped -= Equipped;
            }
            subscribed = false;
        }

        private bool IsOwned()
        {
            if (owner == null || !owner.isActiveAndEnabled || original == null || definition == null || owner.Equipped != original) return false;
            // При смене на другое оружие того же типа старый Destroy отложен до конца кадра.
            // Проверка конкретного HeldWeapon не даёт двум моделям обработать одно событие.
            var view = owner.View;
            return view != null && (view.transform == transform || transform.IsChildOf(view.transform));
        }

        private void Fire(WeaponDefinition data)
        {
            if (data != definition || !IsOwned()) return;
            trackingReload = false;
            RestoreMagazine();
            actionEnds = Time.time + ClipLength(fireState);
            if (isActiveAndEnabled) Play(fireState);
        }

        private void Reload(WeaponDefinition data)
        {
            if (data != definition || !IsOwned()) return;
            activeReloadState = original.LoadedRounds == 0 ? emptyReloadState : tacticalReloadState;
            activeReloadClip = FindClip(activeReloadState);
            reloadEvents = activeReloadClip != null ? activeReloadClip.events : null;
            reloadStartedAt = Time.time;
            reloadDuration = Mathf.Max(.1f, data.reloadSeconds);
            trackingReload = true;
            actionEnds = 0;
            RestoreMagazine();
            if (isActiveAndEnabled) ResumeReload();
        }

        private void Equipped(WeaponDefinition data)
        {
            // Initialize уже запустил Equip. Повторное событие не должно второй раз сбрасывать клип.
            if (data != definition || !IsOwned()) CancelVisual();
        }

        private void OnEnable()
        {
            if (!initialized || owner == null) return;
            if (IsOwned() && trackingReload && owner.IsReloading) ResumeReload();
            else CancelVisual();
        }

        private void OnDisable()
        {
            RestoreMagazine();
            if (modelAnimator != null) modelAnimator.speed = 1;
            activeState = null;
            // Near-wall скрывает весь HeldWeapon через SetActive. Оставляем подписку до Destroy:
            // скрытая модель запоминает начало действия, но не запускает неактивный Animator.
            // Повторный enable восстанавливает фазу перезарядки без нового Equip.
        }

        private void OnDestroy() => Unsubscribe();
        private void Update() => Tick();
        private void LateUpdate()
        {
            if (trackingReload && IsOwned() && owner.IsReloading)
                ApplyMagazinePhase(Mathf.Clamp01((Time.time - reloadStartedAt) / reloadDuration));
        }

        public void Tick()
        {
            if (!initialized || !IsOwned()) return;
            if (trackingReload)
            {
                if (owner.IsReloading) return;
                CancelVisual();
            }
            if (Time.time < actionEnds) return;
            string state = idleState;
            if (motor != null && motor.IsAlive && motor.GameplayInputEnabled && !owner.IsAiming && motor.PlanarVelocity.magnitude > movementThreshold)
                state = motor.IsSprinting ? sprintState : motor.PlanarVelocity.magnitude >= runThreshold ? runState : walkState;
            if (string.IsNullOrEmpty(state) || !HasState(state)) state = idleState;
            if (activeState != state) Play(state);
        }

        private void ResumeReload()
        {
            float phase = Mathf.Clamp01((Time.time - reloadStartedAt) / reloadDuration);
            float clipSeconds = activeReloadClip != null ? activeReloadClip.length : reloadDuration;
            Play(activeReloadState, phase, clipSeconds / reloadDuration);
            // При продолжении с середины Animator не воспроизводит уже пройденные события.
            // Читаем визуальные события исходного клипа, не дублируя их времена в настройках.
            ApplyMagazinePhase(phase);
        }

        private void ApplyMagazinePhase(float phase)
        {
            RestoreMagazine();
            if (activeReloadClip == null || reloadEvents == null) return;
            foreach (var visualEvent in reloadEvents)
            {
                if (visualEvent.time > phase * activeReloadClip.length) continue;
                switch (visualEvent.functionName)
                {
                    case "OnMagazineRemoved": SetVisible(installedMagazine, false); break;
                    case "OnSpareMagazineShown": SetVisible(spareMagazine, true); break;
                    case "OnMagazineInserted":
                    case "OnReloadComplete": RestoreMagazine(); break;
                }
            }
        }

        private void CancelVisual()
        {
            trackingReload = false;
            actionEnds = 0;
            RestoreMagazine();
            Play(idleState);
        }

        private bool HasState(string state) => modelAnimator != null && modelAnimator.runtimeAnimatorController != null
            && !string.IsNullOrEmpty(state) && modelAnimator.HasState(0, Animator.StringToHash(state));

        private float Play(string state, float phase = 0, float speed = 1)
        {
            if (!isActiveAndEnabled || modelAnimator == null || !modelAnimator.isActiveAndEnabled || !HasState(state)) return 0;
            modelAnimator.speed = Mathf.Max(.01f, speed);
            modelAnimator.Play(Animator.StringToHash(state), 0, phase);
            activeState = state;
            return ClipLength(state) / modelAnimator.speed;
        }

        private AnimationClip FindClip(string state)
        {
            if (string.IsNullOrEmpty(state)) return null;
            if (clips.TryGetValue(state, out var clip)) return clip;
            int separator = state.LastIndexOf('.');
            return separator >= 0 && clips.TryGetValue(state.Substring(separator + 1), out clip) ? clip : null;
        }

        private float ClipLength(string state) => FindClip(state)?.length ?? 0;
        private void RestoreMagazine()
        {
            SetVisible(installedMagazine, true);
            SetVisible(spareMagazine, false);
        }
        private static void SetVisible(Renderer[] renderers, bool value)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = value;
        }
    }
}
