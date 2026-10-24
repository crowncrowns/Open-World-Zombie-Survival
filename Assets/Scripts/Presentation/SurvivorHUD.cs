using UnityEngine;
using UnityEngine.UI;
using ZombieSurvival.Combat;
using ZombieSurvival.Inventory;
using ZombieSurvival.Interaction;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;
using ZombieSurvival.Progression;
using ZombieSurvival.Weapons;

namespace ZombieSurvival.Presentation
{
    /// <summary>One passive presentation layer. No input actions, inventory copies or gameplay writes.</summary>
    [DefaultExecutionOrder(400), DisallowMultipleComponent, RequireComponent(typeof(FirstPersonController))]
    public sealed class SurvivorHUD : MonoBehaviour
    {
        [SerializeField, InspectorName("Плавное появление"), Min(.01f), Tooltip("Время появления и скрытия блоков HUD, с. Не зависит от скорости игрового времени.")]
        private float fadeSeconds = .3f;
        [SerializeField, InspectorName("Задержка здоровья"), Min(0), Tooltip("Сколько секунд показывать здоровье после изменения. При ранении шкала остаётся видна до полного лечения.")]
        private float healthHold = 4f;
        [SerializeField, InspectorName("Задержка выносливости"), Min(0), Tooltip("Сколько секунд оставлять шкалу после восстановления полного запаса выносливости.")]
        private float staminaHold = 1.5f;
        [SerializeField, InspectorName("Задержка хотбара"), Min(0), Tooltip("Сколько секунд показывать быстрые слоты после выбора, назначения, использования или выброса предмета.")]
        private float hotbarHold = 3.5f;
        [SerializeField, InspectorName("Задержка оружия"), Min(0), Tooltip("Сколько секунд показывать боезапас после смены оружия, выстрела или перезарядки. При прицеливании и пустом магазине он остаётся виден.")]
        private float weaponHold = 4f;
        [SerializeField, InspectorName("Низкие потребности"), Range(.05f, .8f), Tooltip("Доля сытости или воды, ниже которой соответствующий показатель остаётся виден: 0,35 = 35%.")]
        private float needsThreshold = .35f;
        [SerializeField, InspectorName("Критическое здоровье"), Range(.01f, .5f), Tooltip("Доля здоровья для красного предупреждения: 0,25 = 25%. Пульсирует только маленькая метка, не весь экран.")]
        private float criticalHealth = .25f;
        [SerializeField, InspectorName("Порог заметного шума"), Min(.1f), Tooltip("Минимальная громкость реального события GameplayNoise для индикатора. Это не вероятность обнаружения и не информация о скрытом враге.")]
        private float noiseThreshold = 1.5f;
        [SerializeField, InspectorName("Опорный уровень шума"), Range(0, 100), Tooltip("Игровой уровень в дБ при громкости GameplayNoise 1. Формула: опорный уровень + 20 log10(громкость). Это условная шкала, не измерение физического dB SPL; слух NPC остаётся прежним.")]
        private float referenceNoiseDb = 50f;
        [SerializeField, InspectorName("Размер интерфейса"), Range(.75f, 1.4f), Tooltip("Множитель масштаба относительно макета 1920×1080. Все блоки учитывают безопасную область экрана.")]
        private float interfaceScale = 1f;
        [SerializeField, InspectorName("Длина луча прицела"), Range(2f, 20f), Tooltip("Длина каждого из четырёх лучей перекрестья, пиксели. Не зависит от масштаба панелей HUD.")]
        private float reticleSize = 6f;
        [SerializeField, InspectorName("Толщина перекрестья"), Range(1f, 4f), Tooltip("Толщина светлой линии перекрестья, пиксели. По умолчанию тонкая линия в 1 пиксель.")]
        private float reticleThickness = 1f;
        [SerializeField, InspectorName("Просвет в центре прицела"), Range(0f, 12f), Tooltip("Расстояние от центра попадания до начала каждого луча, пиксели. Центр не закрывает цель.")]
        private float reticleGap = 3f;
        [SerializeField, InspectorName("Обводка перекрестья"), Range(0f, 2f), Tooltip("Толщина тёмной обводки вокруг лучей, пиксели. Помогает видеть прицел на светлом фоне; 0 выключает обводку.")]
        private float reticleOutline = 1f;

        private static readonly Color Paper = new Color(.85f, .83f, .75f);
        private static readonly Color Muted = new Color(.53f, .56f, .52f);
        private static readonly Color Amber = new Color(.83f, .62f, .32f);
        private static readonly Color Red = new Color(.78f, .27f, .23f);
        private static readonly Color Sage = new Color(.59f, .67f, .52f);
        private static readonly Color Back = new Color(.055f, .065f, .058f, .88f);
        private FirstPersonController motor;
        private Health health;
        private PlayerStamina stamina;
        private PlayerNeeds needs;
        private PlayerHotbar hotbar;
        private ItemStorage storage;
        private PlayerWeapons weapons;
        private PlayerInteractor interactor;
        private PlayerNoiseEmitter noise;
        private PlayerLifeCycle life;
        private PlayerSaveSystem saves;
        private PlayerProgression progression;
        private Canvas canvas;
        private RectTransform safe;
        private Font font;
        private ContextualHudGroup hpGroup, staminaGroup, foodGroup, waterGroup, hotbarGroup, weaponGroup, noiseGroup, promptGroup, toastGroup;
        private Meter hpMeter, staminaMeter, foodMeter, waterMeter;
        private Image flash, criticalMark;
        private Color reticleColor = Paper;
        private Text weaponName, ammo, weaponState, noiseText, targetName, targetAction, feedback, toast, deathText;
        private RectTransform death;
        private Rect lastSafe;
        private Vector2Int lastScreen;
        private Cell[] cells;
        private string lastWeaponId, lastSaveStatus;
        private int lastRounds, lastLevel;
        private long lastXp;
        private readonly System.Collections.Generic.Dictionary<Canvas, bool> legacyCanvases = new System.Collections.Generic.Dictionary<Canvas, bool>();
        private float lastHp, lastStamina, lastFood, lastWater, flashUntil;
        private bool initialized;
        public static bool Owns(Component legacy)
        {
            var owner = legacy.GetComponentInParent<SurvivorHUD>();
            return owner != null && owner.isActiveAndEnabled && owner.canvas != null;
        }

        private sealed class Meter { internal RectTransform Fill; internal Image Ink; internal Text Value, Label; internal float Displayed = 1; }
        private sealed class Cell { internal Image Back, Accent; internal RawImage Model; internal Text Number, Detail; }
        private void OnEnable()
        {
            Resolve();
            if (canvas == null) Build();
            canvas.enabled = true;
            HideLegacyCanvas();
            Subscribe(false); Subscribe(true); ResetPresentation();
        }
        private void Resolve()
        {
            motor = GetComponent<FirstPersonController>(); health = GetComponent<Health>();
            stamina = GetComponent<PlayerStamina>(); needs = GetComponent<PlayerNeeds>();
            hotbar = GetComponent<PlayerHotbar>(); weapons = GetComponent<PlayerWeapons>();
            storage = GetComponent<ItemStorage>();
            interactor = GetComponent<PlayerInteractor>(); noise = GetComponent<PlayerNoiseEmitter>();
            life = GetComponent<PlayerLifeCycle>(); saves = GetComponent<PlayerSaveSystem>();
            progression = GetComponent<PlayerProgression>();
        }
        private void Subscribe(bool add)
        {
            if (health != null)
            {
                if (add) { health.Damaged += Damaged; health.Revived += ResetPresentation; health.WorldStateRestored += ResetPresentation; }
                else { health.Damaged -= Damaged; health.Revived -= ResetPresentation; health.WorldStateRestored -= ResetPresentation; }
            }
            if (hotbar != null)
            {
                if (add) { hotbar.PresentationActivity += RevealHotbar; hotbar.Changed += RevealHotbar; }
                else { hotbar.PresentationActivity -= RevealHotbar; hotbar.Changed -= RevealHotbar; }
            }
        }
        private void HideLegacyCanvas()
        {
            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(behaviour is PlayerHealthHUD || behaviour is PlayerStaminaHUD || behaviour is PlayerNoiseHUD || behaviour is InteractionHUD)) continue;
                var old = behaviour.GetComponentInParent<Canvas>();
                if (old == null || old == canvas || legacyCanvases.ContainsKey(old)) continue;
                legacyCanvases.Add(old, old.enabled); old.enabled = false;
            }
        }
        private void OnDisable()
        {
            Subscribe(false); if (canvas != null) canvas.enabled = false;
            foreach (var pair in legacyCanvases) if (pair.Key != null) pair.Key.enabled = pair.Value;
            legacyCanvases.Clear();
        }
        private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || motor == null || !motor.IsAlive
                || !motor.isActiveAndEnabled || !motor.GameplayInputEnabled) return;
            // Перекрестье — постоянный слой того же HUD, без шрифтового глифа,
            // самодельного UGUI mesh и затухания контекстных панелей. Ввод он не читает.
            var camera = interactor != null ? interactor.ViewCamera : null;
            Vector3 point = camera != null ? camera.ViewportToScreenPoint(new Vector3(.5f + motor.AimReticleViewportOffset, .5f, 0))
                : new Vector3(Screen.width * (.5f + motor.AimReticleViewportOffset), Screen.height * .5f, 0);
            var matrix = GUI.matrix; var tint = GUI.color; int depth = GUI.depth;
            GUI.matrix = Matrix4x4.identity; GUI.depth = -1000;
            DrawAimCrosshair(new Vector2(point.x, Screen.height - point.y), reticleColor,
                reticleSize, reticleGap, reticleThickness, reticleOutline);
            GUI.depth = depth; GUI.color = tint; GUI.matrix = matrix;
        }
        internal static void DrawAimCrosshair(Vector2 center, Color ink, float length = 6f,
            float gap = 3f, float thickness = 1f, float outline = 1f)
        {
            center = new Vector2(Mathf.Round(center.x), Mathf.Round(center.y));
            length = Mathf.Round(Mathf.Clamp(length, 2f, 20f));
            gap = Mathf.Round(Mathf.Clamp(gap, 0f, 12f));
            thickness = Mathf.Round(Mathf.Clamp(thickness, 1f, 4f));
            outline = Mathf.Clamp(outline, 0f, 2f);
            float x = Mathf.Round(center.x - thickness * .5f), y = Mathf.Round(center.y - thickness * .5f);
            var tint = GUI.color;
            DrawAimLine(new Rect(center.x - gap - length, y, length, thickness), ink, outline);
            DrawAimLine(new Rect(center.x + gap, y, length, thickness), ink, outline);
            DrawAimLine(new Rect(x, center.y - gap - length, thickness, length), ink, outline);
            DrawAimLine(new Rect(x, center.y + gap, thickness, length), ink, outline);
            GUI.color = tint;
        }
        private static void DrawAimLine(Rect line, Color ink, float outline)
        {
            if (outline > 0)
            {
                GUI.color = new Color(0, 0, 0, .85f);
                GUI.DrawTexture(new Rect(line.x - outline, line.y - outline, line.width + outline * 2, line.height + outline * 2), Texture2D.whiteTexture);
            }
            GUI.color = ink; GUI.DrawTexture(line, Texture2D.whiteTexture);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreForPlay()
        {
            foreach (var hud in FindObjectsByType<SurvivorHUD>(FindObjectsInactive.Include))
                if (hud.isActiveAndEnabled)
                {
                    hud.Subscribe(false); hud.Resolve();
                    if (hud.canvas == null) hud.Build();
                    hud.canvas.enabled = true; hud.HideLegacyCanvas();
                    hud.Subscribe(true); hud.ResetPresentation();
                }
        }
        private void ResetPresentation()
        {
            initialized = false; flashUntil = float.NegativeInfinity;
            if (canvas == null) return;
            foreach (var group in new[] { hpGroup, staminaGroup, foodGroup, waterGroup, hotbarGroup, weaponGroup, noiseGroup, promptGroup, toastGroup }) group.Reset();
            flash.color = Color.clear; death.gameObject.SetActive(false);
        }
        private void RevealHotbar() { if (hotbarGroup != null) hotbarGroup.Hold(hotbarHold); }
        private void Damaged(DamageInfo _) { flashUntil = Time.unscaledTime + .45f; hpGroup?.Hold(healthHold); }

        private void LateUpdate()
        {
            if (canvas == null || motor == null) return;
            UpdateSafeArea();
            bool alive = health == null ? motor.IsAlive : !health.IsDead;
            // GameplayInputEnabled includes locks from inventory, progression and service menus.
            bool visible = alive && motor.isActiveAndEnabled && motor.GameplayInputEnabled;
            if (!initialized)
            {
                lastHp = health != null ? health.Current : 0; lastStamina = stamina != null ? stamina.Current : 0;
                lastFood = needs != null ? needs.Food : 0; lastWater = needs != null ? needs.Water : 0;
                lastSaveStatus = saves != null ? saves.Status : "";
                lastLevel = progression != null ? progression.Level : 0; lastXp = progression != null ? progression.Experience : 0;
                lastWeaponId = weapons != null ? weapons.Equipped?.InstanceId : null;
                lastRounds = weapons != null && weapons.Equipped != null ? weapons.Equipped.LoadedRounds : -1;
                hpMeter.Displayed = health != null ? health.Normalized : 1;
                staminaMeter.Displayed = stamina != null ? stamina.Normalized : 1;
                initialized = true;
            }
            if (health != null)
            {
                if (!Mathf.Approximately(lastHp, health.Current)) hpGroup.Hold(healthHold);
                lastHp = health.Current;
                bool critical = health.Normalized <= criticalHealth;
                UpdateMeter(hpMeter, health.Normalized, health.Current, health.Maximum, critical ? "КРИТИЧЕСКОЕ СОСТОЯНИЕ" : "ЗДОРОВЬЕ", critical ? Red : Paper);
                criticalMark.color = new Color(Red.r, Red.g, Red.b, critical ? .6f + .25f * Mathf.Sin(Time.unscaledTime * 3) : 0);
                hpGroup.Tick(health.Normalized < .9999f, visible, fadeSeconds);
            }
            if (stamina != null)
            {
                if (stamina.Normalized < .9999f || stamina.Current < lastStamina) staminaGroup.Hold(staminaHold);
                lastStamina = stamina.Current;
                UpdateMeter(staminaMeter, stamina.Normalized, stamina.Current, stamina.Capacity, stamina.IsExhausted ? "НУЖНО ПЕРЕВЕСТИ ДУХ" : "ВЫНОСЛИВОСТЬ", stamina.IsExhausted ? Amber : Sage);
                staminaGroup.Tick(stamina.IsExhausted, visible && stamina.isActiveAndEnabled, fadeSeconds);
            }
            if (needs != null)
            {
                if (needs.Food > lastFood + .01f) foodGroup.Hold(4);
                if (needs.Water > lastWater + .01f) waterGroup.Hold(4);
                lastFood = needs.Food; lastWater = needs.Water;
                float f = needs.Food / needs.FoodCapacity, w = needs.Water / needs.WaterCapacity;
                UpdateMeter(foodMeter, f, needs.Food, needs.FoodCapacity, f <= .1f ? "ГОЛОД" : "СЫТОСТЬ", f <= .1f ? Red : Amber);
                UpdateMeter(waterMeter, w, needs.Water, needs.WaterCapacity, w <= .1f ? "ОБЕЗВОЖИВАНИЕ" : "ВОДА", w <= .1f ? Red : Paper);
                foodGroup.Tick(f <= needsThreshold, visible && needs.isActiveAndEnabled, fadeSeconds);
                waterGroup.Tick(w <= needsThreshold, visible && needs.isActiveAndEnabled, fadeSeconds);
            }
            UpdateHotbar(visible);
            UpdateWeapon(visible);
            UpdateInteraction(visible);
            bool loud = noise != null && noise.isActiveAndEnabled && noise.CurrentLoudness >= noiseThreshold;
            if (loud) noiseText.text = "///  " + Mathf.RoundToInt(referenceNoiseDb + 20f * Mathf.Log10(Mathf.Max(.0001f, noise.CurrentLoudness))) + " дБ  ·  " + noise.CurrentNoiseLabel.ToUpperInvariant();
            noiseGroup.Tick(loud, visible, fadeSeconds);
            UpdateNotifications();
            toastGroup.Tick(false, visible, fadeSeconds);
            flash.color = new Color(.5f, .07f, .035f, visible ? Mathf.Clamp01((flashUntil - Time.unscaledTime) / .45f) * .12f : 0);
            bool dead = !alive && GetComponent<PlayerSaveMenu>()?.IsOpen != true;
            death.gameObject.SetActive(dead);
            if (dead && life != null) deathText.text = "ВЫ НЕ ВЫЖИЛИ\n\n" + (!string.IsNullOrEmpty(life.RespawnError) ? life.RespawnError : life.RespawnWait > 0 ? "Возрождение через " + Mathf.CeilToInt(life.RespawnWait) + " с" : "[" + life.BindingLabel + "]  Вернуться") + "\n\nF6 — сохранения";
        }
        private void UpdateHotbar(bool visible)
        {
            if (hotbar == null) { hotbarGroup.Tick(false, false, fadeSeconds); return; }
            if (cells.Length != hotbar.SlotCount) BuildCells();
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i]; var item = hotbar.GetAssignedItem(i); bool selected = i == hotbar.SelectedIndex;
                cell.Back.color = selected ? new Color(.14f, .16f, .13f, .96f) : Back;
                cell.Accent.color = selected ? Amber : new Color(.38f, .41f, .36f, .35f);
                cell.Number.color = selected ? Amber : Muted;
                cell.Model.texture = item != null ? ItemModelIcons.Get(item.Definition) : null;
                cell.Model.enabled = cell.Model.texture != null;
                cell.Detail.text = item != null ? "×" + item.Quantity : "";
            }
            hotbarGroup.Tick(false, visible && hotbar.isActiveAndEnabled, fadeSeconds);
        }
        private void UpdateWeapon(bool visible)
        {
            var item = weapons != null && weapons.isActiveAndEnabled ? weapons.Equipped : null;
            if (item?.InstanceId != lastWeaponId || (item != null && item.LoadedRounds != lastRounds)) weaponGroup.Hold(weaponHold);
            lastWeaponId = item?.InstanceId; lastRounds = item != null ? item.LoadedRounds : -1;
            if (item != null)
            {
                bool firearm = item.Definition.Weapon.kind == WeaponKind.Firearm;
                weaponName.text = item.Definition.DisplayName.ToUpperInvariant();
                int reserve = storage != null ? storage.Count(item.Definition.Weapon.Ammo) : 0;
                ammo.text = firearm ? item.LoadedRounds.ToString("00") + "  <size=18>/ " + reserve.ToString("00") + "</size>" : "БЛИЖНИЙ БОЙ";
                ammo.color = firearm && item.LoadedRounds == 0 ? Amber : Paper;
                weaponState.text = weapons.IsReloading ? "ПЕРЕЗАРЯДКА…" : firearm && item.LoadedRounds == 0 ? "[R]  ПЕРЕЗАРЯДИТЬ" : weapons.Status;
                weaponGroup.Tick(weapons.IsAiming || weapons.IsAttacking || weapons.IsReloading || firearm && item.LoadedRounds == 0, visible, fadeSeconds);
            }
            else weaponGroup.Tick(false, false, fadeSeconds);
        }
        private void UpdateInteraction(bool visible)
        {
            bool active = visible && interactor != null && interactor.isActiveAndEnabled && interactor.IsAvailable;
            var target = active ? interactor.Target : null;
            bool usable = target != null && interactor.CanUseTarget;
            targetName.text = target != null ? target.DisplayName : "";
            targetAction.text = target != null ? usable ? "[" + interactor.BindingLabel + "]   " + target.GetPrompt(interactor) : "НЕДОСТУПНО" : "";
            targetAction.color = usable ? Paper : Amber;
            // Never leave the previous object's name visible during a fade after loss of sight.
            promptGroup.Tick(target != null, active, fadeSeconds);
            feedback.text = active ? interactor.Feedback : "";
            if (hotbar != null && !string.IsNullOrEmpty(hotbar.PresentationFeedback)) feedback.text = hotbar.PresentationFeedback;
            if (!visible) feedback.text = "";
            reticleColor = weapons != null && weapons.HitMarkerVisible ? Red : usable ? Amber : Paper;
        }
        private void UpdateNotifications()
        {
            if (saves != null && saves.Status != lastSaveStatus)
            {
                lastSaveStatus = saves.Status; toast.text = saves.Status; toastGroup.Hold(6);
            }
            if (progression != null && (progression.Experience != lastXp || progression.Level != lastLevel))
            {
                toast.text = progression.Level > lastLevel ? "НОВЫЙ УРОВЕНЬ  " + progression.Level + "   ·   [K] Развитие" : "ОПЫТ  +" + System.Math.Max(0L, progression.Experience - lastXp);
                lastXp = progression.Experience; lastLevel = progression.Level; toastGroup.Hold(4);
            }
        }
        private void UpdateMeter(Meter meter, float fraction, float value, float maximum, string label, Color color)
        {
            meter.Displayed = Mathf.MoveTowards(meter.Displayed, fraction, Time.unscaledDeltaTime * 1.8f);
            meter.Fill.anchorMax = new Vector2(meter.Displayed, 1);
            meter.Ink.color = color; meter.Label.text = label;
            meter.Value.text = Mathf.CeilToInt(value) + " <color=#92998B>/ " + Mathf.CeilToInt(maximum) + "</color>";
        }

        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Survivor HUD (runtime)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false); canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080) / interfaceScale; scaler.matchWidthOrHeight = .5f;
            // No GraphicRaycaster/EventSystem: the HUD cannot steal interaction or menu input.
            safe = Rect(go.transform, "Safe area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hp = Panel("Health", new Vector2(0, 0), new Vector2(54, 153), new Vector2(284, 61), out hpGroup);
            hpMeter = MakeMeter(hp, "ЗДОРОВЬЕ", Paper);
            criticalMark = Box(hp, "Critical mark", new Vector2(-10, 19), new Vector2(3, 23), Color.clear);
            var sp = Panel("Stamina", Vector2.zero, new Vector2(54, 87), new Vector2(284, 56), out staminaGroup);
            staminaMeter = MakeMeter(sp, "ВЫНОСЛИВОСТЬ", Sage);
            var fp = Panel("Food", Vector2.zero, new Vector2(54, 232), new Vector2(132, 48), out foodGroup);
            foodMeter = MakeMeter(fp, "СЫТОСТЬ", Amber, 132);
            var wp = Panel("Water", Vector2.zero, new Vector2(206, 232), new Vector2(132, 48), out waterGroup);
            waterMeter = MakeMeter(wp, "ВОДА", Paper, 132);
            Panel("Hotbar", new Vector2(.5f, 0), new Vector2(0, 52), new Vector2(620, 95), out hotbarGroup);
            BuildCells();
            var gun = Panel("Weapon", new Vector2(1, 0), new Vector2(-54, 95), new Vector2(270, 112), out weaponGroup);
            weaponName = Label(gun, "Weapon name", new Vector2(0, 82), new Vector2(270, 24), 15, Muted, TextAnchor.MiddleRight);
            ammo = Label(gun, "Magazine", new Vector2(0, 29), new Vector2(270, 50), 38, Paper, TextAnchor.MiddleRight);
            weaponState = Label(gun, "Weapon state", new Vector2(0, 0), new Vector2(270, 25), 14, Amber, TextAnchor.MiddleRight);
            var ns = Panel("Noise", Vector2.zero, new Vector2(54, 43), new Vector2(310, 25), out noiseGroup);
            noiseText = Label(ns, "Noise event", Vector2.zero, new Vector2(310, 25), 13, Muted);
            var prompt = Panel("Interaction", new Vector2(.5f, .5f), new Vector2(0, -106), new Vector2(420, 66), out promptGroup);
            targetName = Label(prompt, "Target", new Vector2(0, 33), new Vector2(420, 30), 20, Paper, TextAnchor.MiddleCenter);
            targetAction = Label(prompt, "Action", Vector2.zero, new Vector2(420, 30), 16, Paper, TextAnchor.MiddleCenter);
            var fb = Rect(safe, "Feedback", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -166), new Vector2(760, 46));
            feedback = Label(fb, "Feedback text", Vector2.zero, new Vector2(760, 46), 16, Paper, TextAnchor.MiddleCenter);
            var note = Panel("Notification", new Vector2(.5f, 1), new Vector2(0, -78), new Vector2(700, 70), out toastGroup);
            toast = Label(note, "Notification text", Vector2.zero, new Vector2(700, 70), 17, Paper, TextAnchor.MiddleCenter);
            flash = Rect(go.transform, "Damage flash", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            flash.raycastTarget = false; flash.color = Color.clear;
            death = Rect(safe, "Death", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var shade = death.gameObject.AddComponent<Image>(); shade.raycastTarget = false; shade.color = new Color(.035f, .04f, .035f, .86f);
            var message = Rect(death, "Death message", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1000, 240));
            deathText = Label(message, "Message", Vector2.zero, new Vector2(1000, 240), 26, Paper, TextAnchor.MiddleCenter);
            death.gameObject.SetActive(false); UpdateSafeArea();
        }
        private void BuildCells()
        {
            var root = (RectTransform)hotbarGroup.Canvas.transform;
            foreach (Transform child in root) Destroy(child.gameObject);
            int count = hotbar != null ? hotbar.SlotCount : 5;
            float width = Mathf.Min(124, 920f / count), total = count * width;
            root.sizeDelta = new Vector2(total, 95); cells = new Cell[count];
            for (int i = 0; i < count; i++)
            {
                var rect = Rect(root, "Slot " + (i + 1), Vector2.zero, Vector2.zero, new Vector2(i * width + 2, 0), new Vector2(width - 5, 82));
                rect.pivot = Vector2.zero;
                var background = rect.gameObject.AddComponent<Image>(); background.raycastTarget = false; background.color = Back;
                var iconRect = Rect(rect, "Item model", Vector2.zero, Vector2.zero, new Vector2(18, 13), new Vector2(width - 39, 61));
                iconRect.pivot = Vector2.zero;
                var icon = iconRect.gameObject.AddComponent<RawImage>(); icon.raycastTarget = false; icon.enabled = false;
                cells[i] = new Cell
                {
                    Back = background,
                    Model = icon,
                    Accent = Box(rect, "Selection", Vector2.zero, new Vector2(width - 5, 2), Amber),
                    Number = Label(rect, "Key", new Vector2(10, 56), new Vector2(22, 19), 12, Muted),
                    Detail = Label(rect, "Count", new Vector2(9, 4), new Vector2(width - 23, 24), 17, Paper, TextAnchor.MiddleRight)
                };
                cells[i].Number.text = i < 5 ? (i + 1).ToString("00") : "·";
            }
            Label(root, "Selection hint", new Vector2(0, 88), new Vector2(total, 22), 11, Muted, TextAnchor.MiddleCenter).text = "БЫСТРЫЙ ДОСТУП   /   1–5 · КОЛЕСО   /   TAB — РЮКЗАК";
        }
        private Meter MakeMeter(RectTransform parent, string title, Color color, float width = 284)
        {
            var label = Label(parent, "Label", new Vector2(0, 32), new Vector2(width, 21), width < 200 ? 11 : 12, Muted);
            label.text = title;
            var value = Label(parent, "Value", new Vector2(0, 11), new Vector2(width, 23), 16, Paper);
            var track = Box(parent, "Track", Vector2.zero, new Vector2(width, 3), new Color(.3f, .33f, .28f, .6f));
            var fill = Rect(track.transform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = fill.gameObject.AddComponent<Image>(); image.raycastTarget = false; image.color = color;
            return new Meter { Fill = fill, Ink = image, Value = value, Label = label };
        }
        private RectTransform Panel(string name, Vector2 anchor, Vector2 position, Vector2 size, out ContextualHudGroup group)
        {
            var rect = Rect(safe, name, anchor, anchor, position, size);
            rect.pivot = new Vector2(anchor.x, anchor.y); group = new ContextualHudGroup(rect); return rect;
        }
        private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Image Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(parent, name, Vector2.zero, Vector2.zero, position, size); rect.pivot = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private Text Label(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var rect = Rect(parent, name, Vector2.zero, Vector2.zero, position, size); rect.pivot = Vector2.zero;
            var text = rect.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = fontSize;
            text.color = color; text.alignment = alignment; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .9f); shadow.effectDistance = new Vector2(1, -1);
            return text;
        }
        private void UpdateSafeArea()
        {
            var area = Screen.safeArea; var screen = new Vector2Int(Screen.width, Screen.height);
            if (area == lastSafe && screen == lastScreen) return;
            lastSafe = area; lastScreen = screen;
            safe.anchorMin = new Vector2(area.xMin / Mathf.Max(1, Screen.width), area.yMin / Mathf.Max(1, Screen.height));
            safe.anchorMax = new Vector2(area.xMax / Mathf.Max(1, Screen.width), area.yMax / Mathf.Max(1, Screen.height));
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }
    }
}
