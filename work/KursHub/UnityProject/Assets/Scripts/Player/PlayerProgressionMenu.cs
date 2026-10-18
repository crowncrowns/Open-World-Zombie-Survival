using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;
using ZombieSurvival.Inventory;
using ZombieSurvival.Progression;

namespace ZombieSurvival.Player
{
    /// <summary>Представление дерева. Покупки и проверка зависимостей принадлежат PlayerProgression, не UI.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerProgression), typeof(FirstPersonController))]
    [DefaultExecutionOrder(55)]
    public sealed class PlayerProgressionMenu : MonoBehaviour
    {
        [SerializeField, InspectorName("Открыть дерево развития"), Tooltip("Кнопка открытия дерева. Отдельное действие доступно при отключённой карте движения.")]
        private InputAction toggleInput = new InputAction("Skills", InputActionType.Button, "<Keyboard>/k");
        [SerializeField, InspectorName("Закрыть дерево"), Tooltip("Кнопка закрытия дерева развития.")]
        private InputAction closeInput = new InputAction("CloseSkills", InputActionType.Button, "<Keyboard>/escape");
        [SerializeField, InspectorName("Фон дерева"), Tooltip("Заменяемая иллюстрация интерфейса. Не задаёт реальные узлы и зависимости: они берутся из SkillTreeDefinition.")]
        private Texture2D background;
        [SerializeField, InspectorName("Требовать фокус окна"), Tooltip("Закрывать меню при потере фокуса приложения. Отключать только при автоматической проверке.")]
        private bool requireApplicationFocus = true;
        private InputAction toggle, close;
        private FirstPersonController motor;
        private PlayerProgression progression;
        private Vector2 graphScroll, tabsScroll, detailsScroll;
        private string selectedId, feedback = "Выберите узел, чтобы увидеть эффект и требования.";
        private int tab;
        public bool IsOpen { get; private set; }
        private void Awake()
        {
            motor = GetComponent<FirstPersonController>(); progression = GetComponent<PlayerProgression>();
            toggle = toggleInput.Clone(); close = closeInput.Clone();
        }
        private void OnEnable() { toggle?.Enable(); close?.Enable(); }
        private void OnDisable() { Close(); toggle?.Disable(); close?.Disable(); }
        private void OnDestroy() { toggle?.Dispose(); close?.Dispose(); }
        private void Update()
        {
            if (IsOpen && (!motor.IsAlive || requireApplicationFocus && !Application.isFocused)) { Close(); return; }
            if (IsOpen && close.WasPressedThisFrame()) { Close(); return; }
            if (toggle.WasPressedThisFrame()) { if (IsOpen) Close(); else Open(); }
        }
        public bool Open()
        {
            if (IsOpen || !motor.AcceptingGameplayInput || GetComponent<PlayerInventory>()?.IsOpen == true
                || motor.Parkour != null && motor.Parkour.OwnsMovement) return false;
            IsOpen = true; motor.SetGameplayInputLock(this, true); return true;
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (motor != null) motor.SetGameplayInputLock(this, false);
        }
        private static void Fill(Rect rect, Color color)
        { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        private static void Line(Vector2 from, Vector2 to, Color color)
        {
            var oldMatrix = GUI.matrix; var delta = to - from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            Fill(new Rect(from.x, from.y - 1.5f, delta.magnitude, 3), color); GUI.matrix = oldMatrix;
        }
        private static string Effects(SkillNodeDefinition node)
        {
            string text = "";
            foreach (var effect in node.Effects)
            {
                if (!string.IsNullOrEmpty(effect.abilityId)) text += "Открывает способность\n";
                if (effect.flat != 0 || effect.percent != 0) text += StatLabel(effect.stat) + (effect.flat != 0 ? $" {effect.flat:+0.##;-0.##;0} ед." : "")
                    + (effect.percent != 0 ? $" {effect.percent:+0.##;-0.##;0}%" : "") + " за ранг\n";
            }
            return text;
        }
        public static string StatLabel(StatId stat)
        {
            switch (stat)
            {
                case StatId.HealthMaximum: return "Максимальное здоровье";
                case StatId.StaminaCapacity: return "Запас выносливости";
                case StatId.StaminaRecovery: return "Восстановление выносливости";
                case StatId.WalkSpeed: return "Скорость ходьбы";
                case StatId.RunSpeed: return "Скорость бега";
                case StatId.JumpHeight: return "Высота прыжка";
                case StatId.MeleeDamage: return "Урон ближнего боя";
                case StatId.FirearmDamage: return "Урон огнестрельного оружия";
                case StatId.HealingPower: return "Лечение предметами";
                default: return stat.ToString();
            }
        }
        private void OnGUI()
        {
            if (!IsOpen)
            {
                if (motor == null || !motor.AcceptingGameplayInput || progression == null) return;
                var old = GUI.matrix; float hudScale = Mathf.Min(Screen.width / 1000f, Screen.height / 700f);
                GUI.matrix = Matrix4x4.Scale(Vector3.one * hudScale);
                Fill(new Rect(18, 52, 355, 28), new Color(.04f, .055f, .065f, .9f));
                GUI.Label(new Rect(25, 55, 340, 24), $"Уровень {progression.Level} · Очки: {progression.AvailablePoints} · K — развитие");
                GUI.matrix = old; return;
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            var matrix = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            Fill(new Rect(0, 0, width, height), new Color(.025f, .03f, .035f, .98f));
            if (background != null) { var color = GUI.color; GUI.color = new Color(1, 1, 1, .22f); GUI.DrawTexture(new Rect(0, 0, width, height), background, ScaleMode.ScaleAndCrop); GUI.color = color; }
            var title = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(30, 18, 650, 40), "РАЗВИТИЕ ПЕРСОНАЖА", title);
            GUI.Label(new Rect(30, 60, width - 280, 28), $"Уровень {progression.Level}   •   Опыт {progression.Experience}   •   До уровня: {progression.ExperienceToNextLevel}   •   Очки: {progression.AvailablePoints}");
            if (GUI.Button(new Rect(width - 210, 24, 180, 36), "Закрыть [K / Esc]")) Close();
            var tree = progression.Definition;
            if (tree == null || tree.Tabs.Count == 0)
            { GUI.Label(new Rect(35, 140, 800, 60), "Не назначено дерево. Настройте SkillTreeDefinition в редакторе дерева развития."); GUI.matrix = matrix; return; }
            if (!tree.Validate(out var treeError))
            { GUI.Label(new Rect(35, 140, width - 70, 100), "В дереве развития ошибка: " + treeError); GUI.matrix = matrix; return; }
            tab = Mathf.Clamp(tab, 0, tree.Tabs.Count - 1);
            tabsScroll = GUI.BeginScrollView(new Rect(30, 98, width - 60, 49), tabsScroll, new Rect(0, 0, Mathf.Max(width - 80, tree.Tabs.Count * 205), 35));
            for (int i = 0; i < tree.Tabs.Count; i++)
            {
                var old = GUI.backgroundColor; GUI.backgroundColor = i == tab ? tree.Tabs[i].color : Color.gray;
                if (GUI.Button(new Rect(i * 205, 0, 195, 35), tree.Tabs[i].title)) { tab = i; selectedId = null; graphScroll = Vector2.zero; }
                GUI.backgroundColor = old;
            }
            GUI.EndScrollView();
            var graph = new Rect(25, 153, width - 365, height - 215);
            Fill(graph, new Color(.03f, .04f, .045f, .78f));
            float canvasWidth = Mathf.Max(1200, graph.width - 20), canvasHeight = Mathf.Max(750, graph.height - 20);
            Vector2 offset = Vector2.zero;
            foreach (var node in tree.Nodes.Where(n => n.TabId == tree.Tabs[tab].id))
            { offset.x = Mathf.Max(offset.x, 100 - node.Position.x); offset.y = Mathf.Max(offset.y, 60 - node.Position.y); }
            foreach (var node in tree.Nodes) if (node.TabId == tree.Tabs[tab].id)
            { canvasWidth = Mathf.Max(canvasWidth, node.Position.x + offset.x + 110); canvasHeight = Mathf.Max(canvasHeight, node.Position.y + offset.y + 80); }
            graphScroll = GUI.BeginScrollView(graph, graphScroll, new Rect(0, 0, canvasWidth, canvasHeight));
            foreach (var node in tree.Nodes)
            {
                if (node.TabId != tree.Tabs[tab].id) continue;
                foreach (var parentId in node.PrerequisiteIds)
                {
                    var parent = tree.Nodes.FirstOrDefault(n => n.Id == parentId);
                    if (parent == null || parent.TabId != node.TabId) continue;
                    Line(parent.Position + offset + new Vector2(0, 34), node.Position + offset - new Vector2(0, 34),
                        progression.RankOf(parentId) > 0 ? tree.Tabs[tab].color : new Color(.25f, .28f, .3f));
                }
            }
            foreach (var node in tree.Nodes)
            {
                if (node.TabId != tree.Tabs[tab].id) continue;
                bool allowed = progression.CanPurchase(node.Id, out _); int rank = progression.RankOf(node.Id);
                var color = GUI.backgroundColor;
                GUI.backgroundColor = node.Id == selectedId ? new Color(.9f, .7f, .3f) : rank > 0 ? new Color(.35f, .65f, .48f) : allowed ? new Color(.65f, .5f, .25f) : new Color(.3f, .33f, .36f);
                if (GUI.Button(new Rect(node.Position.x + offset.x - 80, node.Position.y + offset.y - 34, 160, 68), node.Title + $"\n{rank}/{node.MaxRank}  •  {node.PointCost} очк.")) selectedId = node.Id;
                GUI.backgroundColor = color;
            }
            GUI.EndScrollView();
            var detail = new Rect(width - 325, 153, 300, height - 215);
            Fill(detail, new Color(.045f, .055f, .06f, .95f));
            GUILayout.BeginArea(new Rect(detail.x + 15, detail.y + 15, detail.width - 30, detail.height - 30));
            detailsScroll = GUILayout.BeginScrollView(detailsScroll);
            var selected = tree.Nodes.FirstOrDefault(n => n.Id == selectedId);
            if (selected != null)
            {
                GUILayout.Label(selected.Title, new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, wordWrap = true });
                GUILayout.Label(selected.Description, new GUIStyle(GUI.skin.label) { wordWrap = true });
                GUILayout.Space(14); GUILayout.Label(Effects(selected), new GUIStyle(GUI.skin.label) { wordWrap = true });
                var modifiers = GetComponent<PlayerStatModifiers>();
                foreach (var effect in selected.Effects.Where(e => e.flat != 0 || e.percent != 0).GroupBy(e => e.stat).Select(g => g.First()))
                {
                    float baseline = Baseline(effect.stat);
                    if (float.IsNaN(baseline)) GUILayout.Label("Величина эффекта зависит от применяемого предмета.", new GUIStyle(GUI.skin.label) { wordWrap = true });
                    else GUILayout.Label($"{StatLabel(effect.stat)}:\n{modifiers.Evaluate(effect.stat, baseline):0.##} → {progression.PreviewStatAfterPurchase(selected.Id, effect.stat, baseline):0.##}");
                }
                GUILayout.Label($"Ранг: {progression.RankOf(selected.Id)} / {selected.MaxRank}\nСтоимость: {selected.PointCost} очк.");
                GUILayout.Space(12);
                if (selected.PrerequisiteIds.Count > 0)
                {
                    GUILayout.Label(selected.RequireAllPrerequisites ? "Требуются все узлы:" : "Требуется один из узлов:");
                    foreach (var id in selected.PrerequisiteIds)
                    {
                        var parent = tree.Nodes.FirstOrDefault(n => n.Id == id);
                        GUILayout.Label((progression.RankOf(id) > 0 ? "✓ " : "• ") + (parent != null ? parent.Title : id));
                    }
                }
                bool can = progression.CanPurchase(selected.Id, out var reason);
                var enabled = GUI.enabled; GUI.enabled = can;
                if (GUILayout.Button("Улучшить", GUILayout.Height(42)))
                { progression.TryPurchase(selected.Id, out var result); feedback = result; }
                GUI.enabled = enabled;
                if (!can) GUILayout.Label(reason, new GUIStyle(GUI.skin.label) { wordWrap = true });
            }
            else GUILayout.Label("Выберите узел дерева.\n\nЛинии показывают зависимости. Между вкладками требования указаны в карточке.", new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUI.Label(new Rect(30, height - 48, width - 60, 40), feedback);
            GUI.matrix = matrix;
        }
        private float Baseline(StatId stat)
        {
            switch (stat)
            {
                case StatId.HealthMaximum: return GetComponent<ZombieSurvival.Combat.Health>().BaseMaximum;
                case StatId.StaminaCapacity: return motor.Stamina.BaseCapacity;
                case StatId.StaminaRecovery: return motor.Stamina.BaseRecoveryPerSecond;
                case StatId.WalkSpeed: return motor.BaseWalkSpeed;
                case StatId.RunSpeed: return motor.BaseRunSpeed;
                case StatId.JumpHeight: return motor.BaseJumpHeight;
                // Не показывать выдуманный урон от условной базы 100: величина зависит от конкретной вещи.
                default:
                    var item = GetComponent<ZombieSurvival.Weapons.PlayerWeapons>()?.Equipped;
                    var weapon = item?.Definition.Weapon;
                    if (weapon != null && (stat == StatId.MeleeDamage && weapon.kind == ZombieSurvival.Weapons.WeaponKind.Melee
                        || stat == StatId.FirearmDamage && weapon.kind == ZombieSurvival.Weapons.WeaponKind.Firearm)) return weapon.damage;
                    return float.NaN;
            }
        }
    }
}
