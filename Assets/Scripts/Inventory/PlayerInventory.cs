using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Player;

namespace ZombieSurvival.Inventory
{
    /// <summary>Прототип окна добычи. Время мира не останавливается; UI закрывается при смерти,
    /// удалении ящика или удалении от него. Данные остаются в независимом ItemStorage.</summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent, RequireComponent(typeof(ItemStorage), typeof(FirstPersonController))]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [Tooltip("Открыть/закрыть рюкзак. Отдельное действие работает и при выключенной карте движения.")]
        [SerializeField] private InputAction toggleInput = new InputAction("Inventory", InputActionType.Button, "<Keyboard>/tab");
        [Tooltip("Закрыть окно инвентаря.")]
        [SerializeField] private InputAction closeInput = new InputAction("CloseInventory", InputActionType.Button, "<Keyboard>/escape");
        [Tooltip("Максимальное расстояние до открытого ящика, м. За пределами окно закрывается.")]
        [SerializeField, Min(1f)] private float containerDistance = 3.5f;
        [Tooltip("Дистанция выбрасывания перед игроком, м. Проверяется свободное место и земля.")]
        [SerializeField, Range(.6f, 2f)] private float dropDistance = 1.1f;
        [Tooltip("Слои земли и препятствий при выбрасывании предмета.")]
        [SerializeField] private LayerMask dropMask = ~0;
        [Tooltip("Закрывать окно при потере фокуса приложения. Отключается только для автоматических тестов.")]
        [SerializeField] private bool requireApplicationFocus = true;
        private InputAction toggle, close;
        private FirstPersonController motor;
        private ItemStorage storage, container;
        private bool hadContainer, restoreInput;
        private int selected = -1;
        private bool selectedContainer;
        private Vector2 leftScroll, rightScroll;
        private string feedback = "";
        public bool IsOpen { get; private set; }
        public ItemStorage Storage => storage != null ? storage : GetComponent<ItemStorage>();
        private void Awake() { motor = GetComponent<FirstPersonController>(); storage = GetComponent<ItemStorage>(); toggle = toggleInput.Clone(); close = closeInput.Clone(); }
        private void OnEnable() { toggle?.Enable(); close?.Enable(); }
        private void OnDisable() { Close(); toggle?.Disable(); close?.Disable(); }
        private void OnDestroy() { toggle?.Dispose(); close?.Dispose(); }
        private void Update()
        {
            if (IsOpen && (!motor.IsAlive || requireApplicationFocus && !Application.isFocused || hadContainer && (container == null || !container.isActiveAndEnabled
                || Vector3.Distance(transform.position, container.transform.position) > containerDistance))) { Close(); return; }
            if (IsOpen && close.WasPressedThisFrame()) { Close(); return; }
            if (!toggle.WasPressedThisFrame()) return;
            if (IsOpen) Close(); else if (motor.AcceptingGameplayInput) Open(null);
        }
        public bool Open(ItemStorage other)
        {
            if (IsOpen || !motor.AcceptingGameplayInput || motor.Parkour != null && motor.Parkour.OwnsMovement
                || other == storage || other != null && Vector3.Distance(transform.position, other.transform.position) > containerDistance) return false;
            container = other; hadContainer = other != null; selected = -1; feedback = "";
            restoreInput = motor.GameplayInputEnabled; motor.SetGameplayInputEnabled(false); IsOpen = true; return true;
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; container = null; hadContainer = false;
            if (motor != null && restoreInput) motor.SetGameplayInputEnabled(true);
        }
        public bool Drop(int index, int amount)
        {
            if (motor == null || !motor.IsAlive) return false;
            var hotbar = GetComponent<PlayerHotbar>();
            string[] before = hotbar != null ? hotbar.CaptureAssignments() : null;
            bool dropped = WorldItemDropService.TryDrop(transform, motor.FeetPosition, Storage, index, amount, dropDistance, dropMask);
            // Changed может временно очистить назначение целой стопки до отката неудачного Initialize.
            if (!dropped && hotbar != null) hotbar.RestoreAssignments(before, hotbar.SelectedIndex);
            return dropped;
        }
        private void OnGUI()
        {
            if (!IsOpen) return;
            float scale = Mathf.Min(Screen.width / 1000f, Screen.height / 700f);
            Matrix4x4 old = GUI.matrix; GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            var quickSlots = GetComponent<PlayerHotbar>();
            float panelHeight = quickSlots != null && quickSlots.SlotCount > 5 ? 680 : 620;
            Color oldColor = GUI.color;
            GUI.color = new Color(0, 0, 0, .55f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = new Color(.055f, .075f, .095f, 1f);
            GUI.DrawTexture(new Rect((width - 900) / 2, (height - panelHeight) / 2, 900, panelHeight), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUILayout.BeginArea(new Rect((width - 900) / 2, (height - panelHeight) / 2, 900, panelHeight), GUI.skin.box);
            GUILayout.BeginHorizontal(); GUILayout.Label("РЮКЗАК" + (hadContainer ? "  /  ЯЩИК" : "") + (GetComponent<ZombieSurvival.Economy.Wallet>() != null ? "    " + GetComponent<ZombieSurvival.Economy.Wallet>().FormattedBalance : ""));
            if (GUILayout.Button("Закрыть [Tab / Esc]", GUILayout.Width(190))) Close(); GUILayout.EndHorizontal();
            GUILayout.Label("Мир продолжает жить. Выбери слот, затем перенеси или выброси стопку.");
            var playerNeeds = GetComponent<ZombieSurvival.Player.PlayerNeeds>();
            if (playerNeeds != null) GUILayout.Label($"Сытость: {playerNeeds.Food:0} / {playerNeeds.FoodCapacity:0}    Вода: {playerNeeds.Water:0} / {playerNeeds.WaterCapacity:0}");
            GUILayout.BeginHorizontal(); DrawStorage(storage, false, ref leftScroll);
            if (hadContainer && container != null) DrawStorage(container, true, ref rightScroll); GUILayout.EndHorizontal();
            var source = selectedContainer ? container : storage; var stack = source != null ? source.GetSlot(selected) : null;
            if (stack != null && !stack.IsEmpty)
            {
                GUILayout.Label(stack.Definition.DisplayName + " ×" + stack.Quantity + "\n" + stack.Definition.Description);
                GUILayout.BeginHorizontal();
                var needs = GetComponent<ZombieSurvival.Player.PlayerNeeds>();
                if (!selectedContainer && needs != null && needs.CanConsume(stack.Definition) && GUILayout.Button("Употребить 1"))
                    feedback = needs.TryConsume(selected) ? "Предмет употреблён" : "Показатели полные или предмет недоступен";
                if (!selectedContainer && stack.Definition.Weapon != null && GUILayout.Button("Экипировать"))
                { var weapons = GetComponent<ZombieSurvival.Weapons.PlayerWeapons>(); if (weapons != null) { weapons.Equip(selected); feedback = "Оружие выбрано. Закрой рюкзак для атаки."; } }
                if (hadContainer && container != null)
                {
                    var destination = selectedContainer ? storage : container;
                    if (GUILayout.Button(selectedContainer ? "В рюкзак — всё" : "В ящик — всё")) Transfer(source, destination, int.MaxValue);
                    if (GUILayout.Button("Перенести 1")) Transfer(source, destination, 1);
                }
                if (!selectedContainer && GUILayout.Button("Выбросить всё")) feedback = Drop(selected, stack.Quantity) ? "Стопка на земле" : "Нельзя выбросить: нет свободного места перед игроком";
                if (!selectedContainer && GUILayout.Button("Выбросить 1")) feedback = Drop(selected, 1) ? "Предмет на земле" : "Нельзя выбросить: нет свободного места перед игроком";
                GUILayout.EndHorizontal();
            }
            DrawHotbarAssignments(stack);
            GUILayout.Label(feedback);
            var saveStatus = GetComponent<ZombieSurvival.Persistence.PlayerSaveSystem>();
            if (saveStatus != null) GUILayout.Label(saveStatus.Status);
            GUILayout.EndArea(); GUI.matrix = old;
        }
        private void Transfer(ItemStorage source, ItemStorage destination, int amount)
        {
            int moved = source.TransferTo(destination, selected, amount);
            feedback = moved > 0 ? $"Перенесено: {moved}" : "Нет места — предметы остались в исходном слоте";
        }
        private void DrawStorage(ItemStorage target, bool isContainer, ref Vector2 scroll)
        {
            GUILayout.BeginVertical(GUILayout.Width(hadContainer ? 430 : 870));
            GUILayout.Label(isContainer ? "ЯЩИК" : "ТВОИ ПРЕДМЕТЫ");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(290));
            for (int i = 0; i < target.SlotCount; i++)
            {
                var slot = target.GetSlot(i); bool active = selected == i && selectedContainer == isContainer;
                string title = $"{i + 1:00}  " + (slot == null || slot.IsEmpty ? "— свободно —" : $"{slot.Definition.DisplayName} ×{slot.Quantity} / {slot.Definition.MaxStack}");
                if (slot != null && !slot.IsEmpty && slot.Definition.Weapon != null && slot.Definition.Weapon.kind == ZombieSurvival.Weapons.WeaponKind.Firearm) title += $"  [магазин: {slot.LoadedRounds}]";
                if (slot != null && !slot.IsEmpty)
                {
                    title += "  [" + slot.Definition.CategoryLabel + " / " + slot.Definition.SubcategoryLabel + "]";
                    if (slot.Definition.HasDurability) title += $"  [прочность: {slot.Durability:0.#}/{slot.Definition.MaximumDurability:0.#}]";
                }
                if (GUILayout.Toggle(active, title, GUI.skin.button, GUILayout.Height(35)) && !active) { selected = i; selectedContainer = isContainer; feedback = ""; }
            }
            GUILayout.EndScrollView(); GUILayout.EndVertical();
        }
        private void DrawHotbarAssignments(ItemStack selectedStack)
        {
            var hotbar = GetComponent<PlayerHotbar>(); if (hotbar == null || !hotbar.isActiveAndEnabled) return;
            GUILayout.Label("БЫСТРЫЕ СЛОТЫ — назначение предмета из своего рюкзака / очистка");
            for (int row = 0; row < hotbar.SlotCount; row += 5)
            {
                GUILayout.BeginHorizontal();
                for (int slot = row; slot < Mathf.Min(row + 5, hotbar.SlotCount); slot++)
                {
                    GUILayout.BeginVertical(GUILayout.Width(840f / Mathf.Min(5, hotbar.SlotCount)));
                    var assigned = hotbar.GetAssignedItem(slot);
                    string title = (slot + 1) + (hotbar.SelectedIndex == slot ? " ★" : "") + "  " + (assigned != null ? assigned.Definition.DisplayName : "Пусто");
                    if (GUILayout.Button(title, GUILayout.Height(24))) hotbar.Select(slot);
                    GUILayout.BeginHorizontal();
                    bool enabled = GUI.enabled;
                    GUI.enabled = enabled && !selectedContainer && selectedStack != null && !selectedStack.IsEmpty;
                    if (GUILayout.Button("Назначить")) feedback = hotbar.Assign(slot, selected) ? "Предмет назначен в быстрый слот " + (slot + 1) : "Назначить можно только предмет из своего рюкзака";
                    GUI.enabled = enabled;
                    if (GUILayout.Button("×", GUILayout.Width(24))) { hotbar.Clear(slot); feedback = "Назначение очищено; предмет остался в рюкзаке"; }
                    GUILayout.EndHorizontal(); GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}

