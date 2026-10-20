using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Interaction;
using ZombieSurvival.Player;

namespace ZombieSurvival.Services
{
    /// <summary>One service session per actor. Does not own trade, repair, money or inventory state.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(FirstPersonController), typeof(PlayerInteractor))]
    [DefaultExecutionOrder(60)]
    public sealed class PlayerServiceSession : MonoBehaviour
    {
        [SerializeField, InspectorName("Закрыть сервис"), Tooltip("Отдельное действие закрытия работает при заблокированной карте движения. По умолчанию Escape / кнопка отмены геймпада.")]
        private InputAction closeInput = new InputAction("CloseService", InputActionType.Button, "<Keyboard>/escape");
        [SerializeField, InspectorName("Требовать фокус окна"), Tooltip("Закрывать сервис при потере фокуса приложения. Отключать только для автоматических проверок.")]
        private bool requireApplicationFocus = true;
        private FirstPersonController motor;
        private PlayerInteractor actor;
        private InputAction close;
        private ServicePoint point;
        private Trader selectedTrader;
        private long selectedVersion;
        private string selectedItemId;
        private TradeDirection tradeDirection;
        private TradeQuote tradeQuote;
        private string quantityText = "1";
        private string quoteReason = "";
        private Vector2 catalogScroll;
        public bool IsOpen { get; private set; }
        public long SessionVersion { get; private set; }
        public ServicePoint Point => IsOpen ? point : null;
        public PlayerInteractor Actor { get { Resolve(); return actor; } }
        public string Status { get; private set; } = "";

        private void OnEnable()
        {
            Resolve(); Close();
            InitializeCloseInput();
        }
        private void InitializeCloseInput()
        {
            close?.Dispose(); close = closeInput.Clone();
            bool hasGamepadCancel = false;
            foreach (var binding in close.bindings)
                if (binding.effectivePath == "<Gamepad>/buttonEast") { hasGamepadCancel = true; break; }
            if (!hasGamepadCancel) close.AddBinding("<Gamepad>/buttonEast");
            close.Enable();
        }
        private void Resolve() { if (motor == null) motor = GetComponent<FirstPersonController>(); if (actor == null) actor = GetComponent<PlayerInteractor>(); }
        private void OnDisable() { Close(); close?.Dispose(); close = null; }
        private void OnApplicationFocus(bool focused) { if (!focused && requireApplicationFocus) Close(); }
        private void Update()
        {
            if (!IsOpen) return;
            if (close != null && close.WasPressedThisFrame()) { Close(); return; }
            if (!ValidateAccess(out var reason)) { Status = reason; Close(); }
        }
        public bool TryOpen(ServicePoint service)
        {
            Resolve();
            if (!isActiveAndEnabled || IsOpen || motor == null || !motor.AcceptingGameplayInput || actor == null || !actor.IsAvailable || service == null
                || requireApplicationFocus && !Application.isFocused || !service.CanAccess(actor, out _)) return false;
            point = service; IsOpen = true; AdvanceSessionVersion(); Status = "Сессия открыта.";
            motor.SetGameplayInputLock(this, true); return true;
        }
        /// <summary>Future operations must call this immediately before committing; cached access is not a permit.</summary>
        public bool ValidateAccess(out string reason)
        {
            if (!isActiveAndEnabled || !IsOpen || point == null) { reason = "Сессия закрыта."; return false; }
            if (requireApplicationFocus && !Application.isFocused) { reason = "Окно игры не в фокусе."; return false; }
            return point.CanAccess(actor, out reason);
        }
        public bool ValidateAccess(ServicePoint expectedPoint, out string reason)
        {
            if (expectedPoint == null || Point != expectedPoint) { reason = "Сервис текущей сессии изменился."; return false; }
            return ValidateAccess(out reason);
        }
        public void Close()
        {
            if (IsOpen) AdvanceSessionVersion();
            IsOpen = false; point = null;
            selectedTrader = null; selectedVersion = 0; selectedItemId = null;
            tradeQuote = null; quantityText = "1"; quoteReason = "";
            if (motor != null) motor.SetGameplayInputLock(this, false);
        }
        private void AdvanceSessionVersion() => SessionVersion = SessionVersion == long.MaxValue ? 1 : SessionVersion + 1;
        private void OnGUI()
        {
            if (!IsOpen || point == null) return;
            // Prototype shell matches the existing inventory/progression panels; service rules stay in the API.
            var old = GUI.matrix; var color = GUI.color; int oldDepth = GUI.depth;
            GUI.depth = -100;
            float scale = Mathf.Min(Screen.width / 1000f, Screen.height / 700f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.color = new Color(0, 0, 0, .6f); GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture); GUI.color = color;
            var trader = point.GetComponent<Trader>();
            float panelHeight = trader != null ? 650 : 270;
            float panelWidth = trader != null ? 720 : 600;
            var panel = new Rect((width - panelWidth) / 2, (height - panelHeight) / 2, panelWidth, panelHeight);
            GUI.color = new Color(.055f, .075f, .095f, 1f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture); GUI.color = color;
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label(point.SessionTitle.ToUpperInvariant()); GUILayout.Space(15);
            GUILayout.Label(point.SessionDescription); GUILayout.Space(12);
            if (trader != null) DrawTraderCatalog(trader);
            GUILayout.Label("Движение и бой заблокированы. Мир продолжает работать.");
            if (GUILayout.Button("Проверить доступ")) Status = ValidateAccess(out var reason) ? "Доступ подтверждён." : reason;
            GUILayout.Label(Status); GUILayout.FlexibleSpace();
            if (GUILayout.Button("Закрыть · Escape")) Close();
            GUILayout.EndArea(); GUI.matrix = old; GUI.color = color; GUI.depth = oldDepth;
        }
        private void DrawTraderCatalog(Trader trader)
        {
            if (selectedTrader != trader || selectedVersion != trader.CatalogVersion)
            { selectedTrader = trader; selectedVersion = trader.CatalogVersion; ClearTradeSelection(); }
            if (trader.Profile == null) { GUILayout.Label("Торговый профиль не назначен."); return; }
            GUILayout.Label("Профиль: " + trader.Profile.DisplayName);
            var budget = trader.GetComponent<ZombieSurvival.Economy.TraderBudget>();
            var wallet = GetComponent<ZombieSurvival.Economy.Wallet>();
            GUILayout.Label("Ваш баланс: " + (wallet != null ? wallet.FormattedBalance : "кошелёк отсутствует")
                + "   |   Бюджет торговца: " + (budget == null ? "не задан" : budget.IsInfinite ? "бесконечный" : budget.Funds.FormattedBalance));
            int tab = GUILayout.Toolbar((int)tradeDirection, new[] { "Купить у торговца", "Продать из рюкзака" });
            if (tab != (int)tradeDirection) { tradeDirection = (TradeDirection)tab; ClearTradeSelection(); }
            var storage = tradeDirection == TradeDirection.Buy ? trader.ActiveStock : GetComponent<ZombieSurvival.Inventory.ItemStorage>();
            ZombieSurvival.Inventory.ItemStack selected = null;
            catalogScroll = GUILayout.BeginScrollView(catalogScroll, GUILayout.Height(200));
            bool any = false;
            if (storage != null) for (int i = 0; i < storage.SlotCount; i++)
            {
                var item = storage.GetSlot(i); if (item == null || item.IsEmpty) continue;
                any = true;
                if (GUILayout.Button((item.InstanceId == selectedItemId ? "● " : "") + DescribeTradeItem(item)))
                { selectedItemId = item.InstanceId; quantityText = "1"; RefreshTradeQuote(trader, item); }
                if (item.InstanceId == selectedItemId) selected = item;
            }
            if (!any) GUILayout.Label(tradeDirection == TradeDirection.Buy ? "У торговца нет товаров." : "Рюкзак пуст.");
            GUILayout.EndScrollView();
            if (selected == null) { tradeQuote = null; GUILayout.Label("Выберите предмет для сделки."); return; }
            GUILayout.Label("Выбрано: " + DescribeTradeItem(selected));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Количество:", GUILayout.Width(95));
            string nextQuantity = GUILayout.TextField(quantityText, 10, GUILayout.Width(90));
            if (nextQuantity != quantityText) { quantityText = nextQuantity; RefreshTradeQuote(trader, selected); }
            if (GUILayout.Button("Всё", GUILayout.Width(65))) { quantityText = selected.Quantity.ToString(); RefreshTradeQuote(trader, selected); }
            if (GUILayout.Button("Обновить цену")) RefreshTradeQuote(trader, selected);
            GUILayout.EndHorizontal();
            if (tradeQuote != null)
                GUILayout.Label("За единицу: " + ZombieSurvival.Economy.Wallet.FormatRubles(tradeQuote.UnitPrice)
                    + "   |   Итого: " + ZombieSurvival.Economy.Wallet.FormatRubles(tradeQuote.TotalPrice));
            else GUILayout.Label(quoteReason);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && tradeQuote != null;
            bool execute = GUILayout.Button(tradeDirection == TradeDirection.Buy ? "Купить выбранное количество" : "Продать выбранное количество");
            GUI.enabled = oldEnabled;
            if (execute)
            {
                bool success = TradeService.TryExecute(tradeQuote, out var reason);
                Status = success ? "Сделка выполнена." : reason;
                // Never silently refresh and execute a stale quote on the same click.
                ClearTradeSelection();
            }
        }
        private void ClearTradeSelection()
        { selectedItemId = null; tradeQuote = null; quantityText = "1"; quoteReason = ""; }
        private void RefreshTradeQuote(Trader trader, ZombieSurvival.Inventory.ItemStack item)
        {
            tradeQuote = null;
            if (!int.TryParse(quantityText, out int quantity) || quantity <= 0)
            { quoteReason = "Введите целое положительное количество."; return; }
            TradeService.TryQuote(this, trader, tradeDirection, item, quantity, out tradeQuote, out quoteReason);
        }
        private static string DescribeTradeItem(ZombieSurvival.Inventory.ItemStack item)
        {
            string state = item.Definition.HasDurability ? " · прочность " + item.Durability.ToString("0.#") + "/" + item.Definition.MaximumDurability.ToString("0.#") : "";
            if (item.Definition.Weapon != null && item.Definition.Weapon.Ammo != null) state += " · патронов в магазине: " + item.LoadedRounds;
            return item.Definition.DisplayName + " ×" + item.Quantity + state;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetForPlay()
        {
            // No Scene Reload: OnEnable is not guaranteed on the existing scene objects.
            foreach (var session in FindObjectsByType<PlayerServiceSession>(FindObjectsInactive.Include))
            {
                session.Resolve(); session.Close(); session.Status = "";
                session.close?.Dispose(); session.close = null;
                if (session.isActiveAndEnabled) session.InitializeCloseInput();
            }
        }
    }
}
