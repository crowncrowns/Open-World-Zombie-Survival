using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Interaction;
using ZombieSurvival.Player;
using ZombieSurvival.Inventory;

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
        private readonly TradeWindow tradeWindow = new TradeWindow();
        private readonly WorkbenchWindow workbenchWindow = new WorkbenchWindow();
        private RepairWorkbench activeWorkbench;
        private ServicePresentation presentation;
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
        private void OnDisable() { Close(); tradeWindow.Dispose(); workbenchWindow.Dispose(); close?.Dispose(); close = null; }
        private void OnDestroy() { tradeWindow.Dispose(); workbenchWindow.Dispose(); }
        private void OnApplicationFocus(bool focused) { if (!focused && requireApplicationFocus) Close(); }
        private void Update()
        {
            if (!IsOpen) return;
            if (close != null && close.WasPressedThisFrame()) { Close(); return; }
            if (!ValidateAccess(out var reason)) { Status = reason; Close(); }
            if (IsOpen && point is WorkbenchServicePoint && (activeWorkbench == null || !activeWorkbench.isActiveAndEnabled)) { Status = "Верстак недоступен."; Close(); return; }
            if (IsOpen) { tradeWindow.Tick(this, point.GetComponent<Trader>()); if (activeWorkbench != null) workbenchWindow.Tick(this, activeWorkbench); }
        }
        public bool TryOpen(ServicePoint service)
        {
            Resolve();
            if (!isActiveAndEnabled || IsOpen || motor == null || !motor.AcceptingGameplayInput || actor == null || !actor.IsAvailable || service == null
                || requireApplicationFocus && !Application.isFocused || !service.CanAccess(actor, out _)) return false;
            var repair = service.GetComponent<RepairWorkbench>();
            if (service is WorkbenchServicePoint && (repair == null || !repair.isActiveAndEnabled)) return false;
            activeWorkbench = repair;
            point = service; IsOpen = true; AdvanceSessionVersion(); Status = "Сессия открыта.";
            presentation=service.GetComponent<ServicePresentation>();
            if(presentation!=null)presentation.BeginSession(actor.ViewCamera!=null?actor.ViewCamera.transform:actor.transform);
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
            if(presentation!=null)presentation.EndSession();presentation=null;
            if (IsOpen) AdvanceSessionVersion();
            IsOpen = false; point = null;
            tradeWindow.Reset();
            workbenchWindow.Reset(); activeWorkbench = null;
            if (motor != null) motor.SetGameplayInputLock(this, false);
        }
        /// <summary>UI result feedback only. It cannot grant access or perform an economic operation.</summary>
        public void ReportOperationResult(bool success)
        {
            if(!IsOpen||point==null||presentation==null)return;
            try{presentation.PlayResult(success,activeWorkbench!=null);}
            catch(System.Exception error){Debug.LogException(error,this);}
        }
        private void AdvanceSessionVersion() => SessionVersion = SessionVersion == long.MaxValue ? 1 : SessionVersion + 1;
        private void OnGUI()
        {
            if (!IsOpen || point == null) return;
            var trader = point.GetComponent<Trader>();
            if (trader != null) { tradeWindow.Draw(this, trader); return; }
            if (point is WorkbenchServicePoint)
            {
                if (activeWorkbench == null || !activeWorkbench.isActiveAndEnabled) { Close(); return; }
                workbenchWindow.Draw(this, activeWorkbench); return;
            }
            var old = GUI.matrix; var color = GUI.color; int oldDepth = GUI.depth;
            try
            {
                GUI.depth = -100; float scale = Mathf.Min(Screen.width / 1000f, Screen.height / 700f); if (scale <= 0) return;
                GUI.matrix = Matrix4x4.Scale(Vector3.one * scale); float width = Screen.width / scale, height = Screen.height / scale;
                GUI.color = new Color(0,0,0,.7f); GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture);
                var panel = new Rect((width-600)/2,(height-270)/2,600,270);
                GUI.color = new Color(.055f,.075f,.095f); GUI.DrawTexture(panel,Texture2D.whiteTexture); GUI.color=color;
                GUILayout.BeginArea(panel,GUI.skin.box); GUILayout.Label(point.SessionTitle.ToUpperInvariant()); GUILayout.Space(15);
                GUILayout.Label(point.SessionDescription); GUILayout.Space(12); GUILayout.Label(Status); GUILayout.FlexibleSpace();
                if (GUILayout.Button("Закрыть · Escape")) Close(); GUILayout.EndArea();
            }
            finally { GUI.matrix=old; GUI.color=color; GUI.depth=oldDepth; }
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
