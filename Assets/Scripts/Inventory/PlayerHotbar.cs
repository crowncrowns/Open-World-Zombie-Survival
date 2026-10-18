using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Player;
using ZombieSurvival.Weapons;

namespace ZombieSurvival.Inventory
{
    /// <summary>Ссылки на ID принадлежащих игроку вещей, без второго владельца. Один маршрут Attack для оружия, еды и будущих инструментов.</summary>
    [DefaultExecutionOrder(140)]
    [DisallowMultipleComponent, RequireComponent(typeof(ItemStorage), typeof(FirstPersonController), typeof(PlayerInventory))]
    public sealed class PlayerHotbar : MonoBehaviour
    {
        [SerializeField, InspectorName("Количество быстрых слотов"), Range(1, 10), Tooltip("Количество быстрых слотов, шт. Первые пять выбираются клавишами 1–5; остальные доступны колесом и D-pad.")]
        private int slotCount = 5;
        [SerializeField, InspectorName("Назначенные экземпляры"), Tooltip("Назначения по постоянным InstanceId собственного рюкзака. Предметы и их состояние здесь не копируются.")]
        private string[] assignments = new string[5];
        [SerializeField, InspectorName("Выбранный слот"), Tooltip("Индекс выбранного быстрого слота, начиная с 0. Пустой слот освобождает руки.")]
        private int selectedIndex;
        [SerializeField, InspectorName("Показывать хотбар"), Tooltip("Показывать панель быстрых предметов в нижней центральной части HUD.")]
        private bool showHud = true;
        [SerializeField, InspectorName("Пауза применения"), Min(.05f), Tooltip("Минимальная пауза между успешными применениями расходуемого предмета, с. Одно нажатие расходует не более одной единицы.")]
        private float consumeCooldown = .25f;
        [SerializeField, InspectorName("Шаг колеса"), Min(.01f), Tooltip("Изменение scroll/y на один шаг хотбара, ед. Input System нормализует колесо к 1; дробные движения трекпада накапливаются.")]
        private float scrollStep = 1;
        [SerializeField, InspectorName("Обратить прокрутку"), Tooltip("Обратить направление выбора колесом мыши. По умолчанию прокрутка вверх выбирает предыдущий слот.")]
        private bool invertScroll;
        private ItemStorage storage;
        private FirstPersonController motor;
        private PlayerInventory inventory;
        private PlayerWeapons weapons;
        private PlayerNeeds needs;
        private InputAction attack, secondary, scroll, drop, previous, next;
        private readonly InputAction[] direct = new InputAction[5];
        private bool armed;
        private float scrollRemainder, useReadyAt;
        private string status = "";
        private float statusUntil;
        private int layoutSize;
        public event Action Changed;
        public int SlotCount => Mathf.Clamp(slotCount, 1, 10);
        public int SelectedIndex => selectedIndex;
        public ItemStorage Storage => storage != null ? storage : GetComponent<ItemStorage>();
        public ItemStack ActiveStack => GetAssignedItem(selectedIndex);
        public bool CanUse => isActiveAndEnabled && motor != null && motor.AcceptingGameplayInput
            && (inventory == null || !inventory.IsOpen) && !motor.IsSprinting
            && (motor.Parkour == null || !motor.Parkour.OwnsMovement);

        private void OnEnable() => Initialize();
        private void Initialize()
        {
            if (storage != null) storage.Changed -= OnStorageChanged;
            storage = GetComponent<ItemStorage>(); motor = GetComponent<FirstPersonController>();
            inventory = GetComponent<PlayerInventory>(); weapons = GetComponent<PlayerWeapons>(); needs = GetComponent<PlayerNeeds>();
            storage.Changed += OnStorageChanged;
            attack = secondary = scroll = drop = previous = next = null;
            for (int i = 0; i < direct.Length; i++) direct[i] = null;
            armed = false; scrollRemainder = useReadyAt = statusUntil = 0; status = "";
            EnsureLayout(); PruneAssignments(); SyncWeapon();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreForPlay()
        {
            foreach (var h in FindObjectsByType<PlayerHotbar>(FindObjectsInactive.Include)) if (h.isActiveAndEnabled) h.Initialize();
        }
        private void OnDisable()
        {
            if (storage != null) storage.Changed -= OnStorageChanged;
            armed = false; scrollRemainder = 0;
        }
        private void EnsureLayout()
        {
            int count = SlotCount;
            if (assignments == null) assignments = new string[count];
            if (assignments.Length != count) Array.Resize(ref assignments, count);
            selectedIndex = Mathf.Clamp(selectedIndex, 0, count - 1); layoutSize = count;
        }
        private int FindInventorySlot(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < Storage.SlotCount; i++)
            {
                var item = Storage.GetSlot(i);
                if (item != null && !item.IsEmpty && item.InstanceId == id) return i;
            }
            return -1;
        }
        public ItemStack GetAssignedItem(int index)
        {
            EnsureLayout();
            return index >= 0 && index < assignments.Length ? Storage.GetSlot(FindInventorySlot(assignments[index])) : null;
        }
        public bool Assign(int hotbarSlot, int inventorySlot)
        {
            EnsureLayout(); var item = Storage.GetSlot(inventorySlot);
            if (hotbarSlot < 0 || hotbarSlot >= SlotCount || item == null || item.IsEmpty || !Storage.Contains(item)) return false;
            for (int i = 0; i < assignments.Length; i++) if (assignments[i] == item.InstanceId) assignments[i] = null;
            assignments[hotbarSlot] = item.InstanceId;
            armed = false; SyncWeapon(); Changed?.Invoke(); return true;
        }
        public void Clear(int slot)
        {
            EnsureLayout(); if (slot < 0 || slot >= SlotCount) return;
            assignments[slot] = null; armed = false; SyncWeapon(); Changed?.Invoke();
        }
        public bool Select(int slot)
        {
            EnsureLayout(); if (slot < 0 || slot >= SlotCount) return false;
            if (selectedIndex == slot) { SyncWeapon(); return true; }
            selectedIndex = slot; armed = false; SyncWeapon(); Changed?.Invoke(); return true;
        }
        /// <summary>Экипировка из рюкзака проходит здесь, чтобы оружие соответствовало выбранному быстрому слоту.</summary>
        public bool EquipInventorySlot(int inventorySlot)
        {
            var item = Storage.GetSlot(inventorySlot); if (item == null || item.IsEmpty || item.Definition.Weapon == null) return false;
            EnsureLayout(); int target = Array.IndexOf(assignments, item.InstanceId);
            if (target < 0) { target = selectedIndex; if (!Assign(target, inventorySlot)) return false; }
            return Select(target);
        }
        private void SyncWeapon()
        {
            if (weapons == null) weapons = GetComponent<PlayerWeapons>();
            if (weapons == null || !weapons.isActiveAndEnabled || motor == null || !motor.IsAlive) return;
            var item = ActiveStack;
            if (item == null || item.Definition.Weapon == null) { if (weapons.Equipped != null) weapons.Unequip(); return; }
            if (weapons.Equipped != item) weapons.EquipFromHotbar(FindInventorySlot(item.InstanceId));
        }
        private bool PruneAssignments()
        {
            EnsureLayout(); bool changed = false; var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < assignments.Length; i++)
            {
                if (string.IsNullOrEmpty(assignments[i])) continue;
                if (FindInventorySlot(assignments[i]) >= 0 && seen.Add(assignments[i])) continue;
                assignments[i] = null; changed = true;
            }
            return changed;
        }
        private void OnStorageChanged()
        {
            bool changed = PruneAssignments(); SyncWeapon(); if (changed) Changed?.Invoke();
        }
        public string[] CaptureAssignments()
        {
            PruneAssignments(); return (string[])assignments.Clone();
        }
        public void RestoreAssignments(string[] savedAssignments, int savedSelected)
        {
            // Вызывать после фиксации всех хранилищ снимка. Не найденные в собственном рюкзаке ID становятся пустыми слотами.
            assignments = new string[SlotCount];
            if (savedAssignments != null) Array.Copy(savedAssignments, assignments, Mathf.Min(assignments.Length, savedAssignments.Length));
            selectedIndex = Mathf.Clamp(savedSelected, 0, SlotCount - 1); EnsureLayout();
            PruneAssignments(); armed = false; useReadyAt = scrollRemainder = 0; SyncWeapon(); Changed?.Invoke();
        }
        public static bool ValidateSavedAssignments(string[] saved, int selected, int configuredSlotCount, ISet<string> ownedIds, out string error)
        {
            error = "";
            if (saved == null || saved.Length < 1 || saved.Length > 10 || selected < 0 || selected >= saved.Length
                || configuredSlotCount < 1 || configuredSlotCount > 10)
            { error = "Некорректный размер или выбранный слот хотбара."; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in saved)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (id.Length > 128 || !ids.Add(id)) { error = "Повторяющийся или некорректный ID хотбара."; return false; }
                // Не искать пропавшие ID по типу/имени: RestoreAssignments очищает такую ссылку.
            }
            return true;
        }
        private void BindActions()
        {
            var current = motor.FindGameplayAction("Attack"); if (current == attack && direct[0] != null) return;
            attack = current; secondary = motor.FindGameplayAction("Aim"); scroll = motor.FindGameplayAction("HotbarScroll");
            drop = motor.FindGameplayAction("Drop"); previous = motor.FindGameplayAction("Previous"); next = motor.FindGameplayAction("Next");
            for (int i = 0; i < direct.Length; i++) direct[i] = motor.FindGameplayAction("Slot" + (i + 1));
            armed = false; scrollRemainder = 0;
        }
        private void Update()
        {
            BindActions(); if (layoutSize != SlotCount) { EnsureLayout(); PruneAssignments(); SyncWeapon(); Changed?.Invoke(); }
            if (!CanUse) { armed = false; scrollRemainder = 0; return; }
            SyncWeapon();
            for (int i = 0; i < direct.Length && i < SlotCount; i++) if (direct[i] != null && direct[i].WasPressedThisFrame()) Select(i);
            if (previous != null && previous.WasPressedThisFrame()) Select((selectedIndex + SlotCount - 1) % SlotCount);
            if (next != null && next.WasPressedThisFrame()) Select((selectedIndex + 1) % SlotCount);
            float delta = scroll != null ? scroll.ReadValue<float>() : 0;
            if (Mathf.Abs(delta) > .001f)
            {
                if (scrollRemainder != 0 && Mathf.Sign(delta) != Mathf.Sign(scrollRemainder)) scrollRemainder = 0;
                scrollRemainder += delta;
                int steps = (int)(Mathf.Abs(scrollRemainder) / Mathf.Max(.01f, scrollStep));
                if (steps > 0)
                {
                    int direction = (scrollRemainder > 0 ? -1 : 1) * (invertScroll ? -1 : 1);
                    Select(((selectedIndex + direction * (steps % SlotCount)) % SlotCount + SlotCount) % SlotCount);
                    scrollRemainder -= Mathf.Sign(scrollRemainder) * steps * Mathf.Max(.01f, scrollStep);
                }
            }
            if (drop != null && drop.WasPressedThisFrame()) { TryDropActive(); return; }
            if (attack == null || !attack.IsPressed()) armed = true;
            var item = ActiveStack;
            bool repeat = item != null && item.Definition.Weapon != null && item.Definition.Weapon.kind == WeaponKind.Firearm && item.Definition.Weapon.automatic;
            if (armed && attack != null && (repeat ? attack.IsPressed() : attack.WasPressedThisFrame())) TryUsePrimary();
            if (secondary != null && secondary.WasPressedThisFrame() && (item == null || item.Definition.Weapon == null)) TryUseSecondary();
        }
        public bool TryUsePrimary()
        {
            if (!CanUse) return false; var item = ActiveStack;
            if (item == null) { Feedback("Слот пуст — назначь предмет в Tab"); return false; }
            if (item.Definition.Weapon != null) { SyncWeapon(); return weapons != null && weapons.TryAttack(); }
            if (weapons != null && (weapons.IsAttacking || weapons.IsReloading)) return false;
            if (Time.time < useReadyAt) return false;
            bool result = false;
            if (needs != null && needs.CanConsume(item.Definition))
            {
                result = needs.TryConsume(FindInventorySlot(item.InstanceId));
                Feedback(result ? "Использовано: " + item.Definition.DisplayName : "Показатели полные — предмет не израсходован");
            }
            else
            {
                foreach (var component in GetComponents<MonoBehaviour>())
                    if (component != null && component.isActiveAndEnabled && component is IPlayerItemUseHandler handler && handler.CanHandle(item))
                    { result = handler.TryUsePrimary(this, item); break; }
                if (!result) Feedback("У этого предмета пока нет действия");
            }
            if (result) useReadyAt = Time.time + consumeCooldown;
            return result;
        }
        public bool TryUseSecondary()
        {
            if (!CanUse) return false; var item = ActiveStack;
            if (item == null || item.Definition.Weapon != null || Time.time < useReadyAt) return false;
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component != null && component.isActiveAndEnabled && component is IPlayerItemUseHandler handler && handler.CanHandle(item))
                {
                    bool used = handler.TryUseSecondary(this, item);
                    if (used) useReadyAt = Time.time + consumeCooldown;
                    return used;
                }
            return false;
        }
        public bool TryDropActive()
        {
            if (!CanUse) return false; var item = ActiveStack; if (item == null) return false;
            int slot = FindInventorySlot(item.InstanceId);
            bool dropped = inventory != null && inventory.Drop(slot, 1);
            Feedback(dropped ? "Предмет выброшен" : "Нельзя выбросить: нет свободного места перед игроком"); return dropped;
        }
        private void Feedback(string text) { status = text; statusUntil = Time.unscaledTime + 2.5f; }
        private void OnGUI()
        {
            if (!showHud || motor == null || !motor.IsAlive || !motor.GameplayInputEnabled) return;
            float scale = Mathf.Min(Screen.width / 1000f, Screen.height / 700f); var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale); float width = Screen.width / scale, height = Screen.height / scale;
            float cell = Mathf.Min(112, (width - 40) / SlotCount), total = cell * SlotCount;
            var old = GUI.color;
            for (int i = 0; i < SlotCount; i++)
            {
                Rect rect = new Rect((width - total) / 2 + cell * i + 2, height - 78, cell - 4, 63);
                GUI.color = i == selectedIndex ? new Color(.94f, .62f, .22f, 1) : new Color(.24f, .31f, .36f, .95f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = new Color(.045f, .055f, .06f, 1);
                GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), Texture2D.whiteTexture); GUI.color = old;
                var item = GetAssignedItem(i); string label = i < 5 ? (i + 1).ToString() : "·";
                if (item == null) label += "  —\nПусто";
                else
                {
                    label += "  " + item.Definition.DisplayName + "\n";
                    var weapon = item.Definition.Weapon;
                    if (weapon != null && weapon.kind == WeaponKind.Firearm) label += item.LoadedRounds + " / " + Storage.Count(weapon.Ammo);
                    else if (item.Definition.HasDurability) label += item.Durability.ToString("0.#") + "/" + item.Definition.MaximumDurability.ToString("0.#");
                    else label += "×" + item.Quantity;
                }
                GUI.Label(new Rect(rect.x + 5, rect.y + 5, rect.width - 10, rect.height - 10), label,
                    new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, alignment = TextAnchor.MiddleCenter });
            }
            string hint = Time.unscaledTime < statusUntil ? status : "1–5 / колесо — выбор · ЛКМ — использовать · Q — выбросить 1 · Tab — назначить";
            GUI.Label(new Rect(20, height - 104, width - 40, 24), hint,
                new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter });
            GUI.color = old; GUI.matrix = matrix;
        }
    }
}
