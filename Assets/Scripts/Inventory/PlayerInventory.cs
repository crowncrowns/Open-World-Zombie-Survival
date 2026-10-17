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
            if (!motor.IsAlive) return false;
            var stack = Storage.GetSlot(index);
            if (stack == null || stack.IsEmpty || amount <= 0 || amount > stack.Quantity || stack.Definition.WorldPrefab == null) return false;
            Vector3 origin = motor.FeetPosition + Vector3.up * .35f;
            Vector3 destination = origin + transform.forward * dropDistance;
            // Не выбрасываем сквозь стену, под карту или внутрь другого актёра.
            foreach (var hit in Physics.SphereCastAll(origin, .19f, transform.forward, dropDistance, dropMask, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) return false;
            if (!Physics.Raycast(destination + Vector3.up, Vector3.down, out var ground, 3f, dropMask, QueryTriggerInteraction.Ignore)
                || ground.normal.y < .6f || ground.transform.IsChildOf(transform)) return false;
            destination = ground.point + Vector3.up * .22f;
            foreach (var hit in Physics.OverlapSphere(destination, .18f, dropMask, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) return false;
            var obj = Instantiate(stack.Definition.WorldPrefab, destination, Quaternion.identity);
            obj.SetActive(false); var world = obj.GetComponent<WorldItem>();
            if (world == null) { Destroy(obj); return false; }
            world.Initialize(stack.Copy(amount));
            if (!Storage.Remove(index, amount)) { Destroy(obj); return false; }
            obj.SetActive(true); return true;
        }
        private void OnGUI()
        {
            if (!IsOpen) return;
            float scale = Mathf.Min(Screen.width / 1000f, Screen.height / 700f);
            Matrix4x4 old = GUI.matrix; GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            Color oldColor = GUI.color;
            GUI.color = new Color(0, 0, 0, .55f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = new Color(.055f, .075f, .095f, 1f);
            GUI.DrawTexture(new Rect((width - 900) / 2, (height - 620) / 2, 900, 620), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUILayout.BeginArea(new Rect((width - 900) / 2, (height - 620) / 2, 900, 620), GUI.skin.box);
            GUILayout.BeginHorizontal(); GUILayout.Label("РЮКЗАК" + (hadContainer ? "  /  ЯЩИК" : ""));
            if (GUILayout.Button("Закрыть [Tab / Esc]", GUILayout.Width(190))) Close(); GUILayout.EndHorizontal();
            GUILayout.Label("Мир продолжает жить. Выбери слот, затем перенеси или выброси стопку.");
            GUILayout.BeginHorizontal(); DrawStorage(storage, false, ref leftScroll);
            if (hadContainer && container != null) DrawStorage(container, true, ref rightScroll); GUILayout.EndHorizontal();
            var source = selectedContainer ? container : storage; var stack = source != null ? source.GetSlot(selected) : null;
            if (stack != null && !stack.IsEmpty)
            {
                GUILayout.Label(stack.Definition.DisplayName + " ×" + stack.Quantity + "\n" + stack.Definition.Description);
                GUILayout.BeginHorizontal();
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
            GUILayout.Label(feedback); GUILayout.EndArea(); GUI.matrix = old;
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
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(380));
            for (int i = 0; i < target.SlotCount; i++)
            {
                var slot = target.GetSlot(i); bool active = selected == i && selectedContainer == isContainer;
                string title = $"{i + 1:00}  " + (slot == null || slot.IsEmpty ? "— свободно —" : $"{slot.Definition.DisplayName} ×{slot.Quantity} / {slot.Definition.MaxStack}");
                if (slot != null && !slot.IsEmpty && slot.Definition.Weapon != null && slot.Definition.Weapon.kind == ZombieSurvival.Weapons.WeaponKind.Firearm) title += $"  [магазин: {slot.LoadedRounds}]";
                if (GUILayout.Toggle(active, title, GUI.skin.button, GUILayout.Height(35)) && !active) { selected = i; selectedContainer = isContainer; feedback = ""; }
            }
            GUILayout.EndScrollView(); GUILayout.EndVertical();
        }
    }
}
