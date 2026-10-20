using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Combat;

/// <summary>Одна панель для настоящих сериализованных компонентов, без дубликата характеристик.
/// Изменения сценового экземпляра остаются overrides, ассет префаба редактируется отдельно.</summary>
public sealed class ActorSettingsWindow : EditorWindow
{
    private GameObject actor;
    private int tab;
    [SerializeField, Tooltip("Общий тип предмета для редактирования категории, лимита и максимальной прочности без дублирования настроек.")]
    private ZombieSurvival.Inventory.ItemDefinition itemType;
    private Vector2 scroll;
    private readonly Dictionary<EntityId, Editor> editors = new Dictionary<EntityId, Editor>();
    private readonly string[] tabs = { "Общие", "Движение", "Паркур", "Скрытность / ИИ", "Внешность", "Инвентарь", "Звук", "Бой / оружие", "Деньги / бюджет", "Потребности / сохранение", "Развитие / хотбар" };
    [MenuItem("Tools/Zombie Survival/Настройки персонажа и зомби")]
    public static void Open() { var window = GetWindow<ActorSettingsWindow>("Настройки актёра"); window.minSize = new Vector2(650,450); window.ReadSelection(); }
    private void OnEnable() { Selection.selectionChanged += ReadSelection; ReadSelection(); }
    private void OnDisable() { Selection.selectionChanged -= ReadSelection; ClearEditors(); }
    private void ClearEditors() { foreach (var editor in editors.Values) if (editor != null) DestroyImmediate(editor); editors.Clear(); }
    private void ReadSelection()
    {
        var selected = Selection.activeGameObject;
        if (selected != null)
        {
            var identity = selected.GetComponentInParent<ActorIdentity>();
            if (identity != null) { actor = identity.gameObject; ClearEditors(); }
        }
        Repaint();
    }
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Выбери игрока, зомби или его дочернюю модель. Здесь редактируются исходные компоненты. Настройки экземпляра сцены не меняют префаб; во время Play изменения временные.", MessageType.Info);
        var value = (GameObject)EditorGUILayout.ObjectField("Актёр", actor, typeof(GameObject), true);
        if (value != actor) { actor = value; ClearEditors(); }
        if (actor == null) return;
        tab = GUILayout.SelectionGrid(tab, tabs, 4); scroll = EditorGUILayout.BeginScrollView(scroll);
        if (tab == 0) EditorGUILayout.HelpBox("Фракция — принадлежность актёра. Враждебные фракции — кого именно ОН атакует. Выжившие: игрок и дружелюбные NPC. Заражённые: зомби. Отношение задаётся с каждой стороны отдельно.", MessageType.None);
        foreach (var component in actor.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null || Group(component.GetType().Name) != tab) continue;
            if (component is ActorIdentity)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField("Принадлежность и отношения", EditorStyles.boldLabel);
                var so = new SerializedObject(component); so.Update();
                foreach (string field in new[] { "faction", "hostileFactions", "targetActorsWithoutIdentity" })
                {
                    var property = so.FindProperty(field);
                    string label = field == "faction" ? "Фракция актёра" : field == "hostileFactions" ? "Кого считает врагом" : "Атаковать актёров без фракции";
                    EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
                }
                so.ApplyModifiedProperties(); continue;
            }
            var id = component.GetEntityId();
            if (!editors.TryGetValue(id, out var editor) || editor == null) editors[id] = editor = Editor.CreateEditor(component);
            EditorGUILayout.Space(); EditorGUILayout.LabelField(Title(component.GetType().Name), EditorStyles.boldLabel);
            editor.OnInspectorGUI();
        }
        if (tab == 5)
        {
            EditorGUILayout.Space();
            itemType = (ZombieSurvival.Inventory.ItemDefinition)EditorGUILayout.ObjectField("Тип предмета", itemType, typeof(ZombieSurvival.Inventory.ItemDefinition), false);
            if (itemType != null)
            {
                var id = itemType.GetEntityId();
                if (!editors.TryGetValue(id, out var typeEditor) || typeEditor == null) editors[id] = typeEditor = Editor.CreateEditor(itemType);
                typeEditor.OnInspectorGUI();
            }
        }
        if(tab==9)
        {
            var registries=UnityEngine.Object.FindObjectsByType<ZombieSurvival.Persistence.WorldSaveRegistry>(FindObjectsInactive.Include);
            foreach(var registry in registries)
            {
                if(registry.gameObject.scene!=actor.scene)continue;
                EditorGUILayout.Space();EditorGUILayout.LabelField("Область мира и каталог создаваемых объектов",EditorStyles.boldLabel);
                var id=registry.GetEntityId();
                if(!editors.TryGetValue(id,out var registryEditor)||registryEditor==null)editors[id]=registryEditor=Editor.CreateEditor(registry);
                registryEditor.OnInspectorGUI();
            }
        }
        EditorGUILayout.EndScrollView();
    }
    private static int Group(string type)
    {
        switch (type)
        {
            case "ActorIdentity": case "Health": case "CombatTarget": case "PlayerStamina": case "PlayerLifeCycle": case "PlayerFallDamage": return 0;
            case "FirstPersonController": return 1;
            case "PlayerParkour": return 2;
            case "PlayerNoiseEmitter": case "PlayerInteractor": case "EnemyBrain": case "EnemyCrawlPursuit": case "MeleeAttack": case "NoiseEmitter": return 3;
            case "PlayerCameraMotion": case "PlayerVisual": case "PlayerModelDefinition": case "PlayerCrawlAnimation": case "HumanoidCrawlPose": case "HumanoidGrounding": case "EnemyAnimationBridge": return 4;
            case "ItemStorage": case "PlayerInventory": case "PlayerServiceSession": case "TraderServicePoint": case "WorkbenchServicePoint": case "RepairWorkbench": case "ServiceVisual": case "Trader": return 5;
            case "PlayerProgression": case "PlayerStatModifiers": case "PlayerProgressionMenu": case "PlayerHotbar": return 10;
            case "Wallet": case "TraderBudget": return 8;
            case "PlayerSaveSlots": case "PlayerSaveMenu": case "PlayerNeeds": case "PlayerSaveSystem": case "SaveIdentity": case "WorldSaveObject": case "WorldActorSaveAdapter": case "WorldIdentitySaveAdapter": case "WorldWalletSaveAdapter": case "WorldDoorSaveAdapter": case "WorldToggleSaveAdapter": case "WorldRigidbodySaveAdapter": return 9;
            case "ActorAudioEmitter": case "PlayerMovementAudio": case "NavMeshActorAudio": case "ServicePresentation": return 6;
            case "PlayerWeapons": case "PlayerFlashlight": case "WeaponPracticeTarget": return 7;
            default: return -1;
        }
    }
    private static string Title(string type)
    {
        switch (type)
        {
            case "ActorIdentity": return "Принадлежность и отношения (ActorIdentity)";
            case "Health": return "Здоровье";
            case "CombatTarget": return "Точка обнаружения и попадания";
            case "PlayerStamina": return "Выносливость";
            case "FirstPersonController": return "Управление и движение";
            case "EnemyBrain": return "Поведение, зрение и слух зомби";
            case "EnemyCrawlPursuit": return "Преследование ползком";
            case "PlayerNoiseEmitter": return "Шум игрока";
            case "ItemStorage": return "Слоты предметов";
            case "PlayerInventory": return "Окно рюкзака и выбрасывание";
            case "PlayerServiceSession": return "Общая сессия сервиса";
            case "TraderServicePoint": return "Доступ к торговой сессии";
            case "Trader": return "Профили и личные остатки торговца (Т5)";
            case "WorkbenchServicePoint": return "Доступ к сессии верстака";
            case "RepairWorkbench": return "Правила ремонта верстака";
            case "ServiceVisual": return "Префаб внешности сервиса";
            case "ServicePresentation": return "Анимации и обратная связь сервисов";
            case "PlayerProgression": return "Опыт, уровни и дерево развития";
            case "PlayerStatModifiers": return "Расчёт бонусов характеристик";
            case "PlayerProgressionMenu": return "Окно дерева развития";
            case "PlayerHotbar": return "Быстрые предметы и использование";
            case "PlayerSaveSlots": return "Автосохранение и количество слотов";
            case "PlayerSaveMenu": return "Меню сохранений и загрузки";
            case "WorldSaveObject": return "Участие в сохранении мира";
            case "WorldActorSaveAdapter": return "Состояние NPC: HP, поза, фракции и навигация";
            case "WorldWalletSaveAdapter": return "Кошелёк в едином снимке";
            case "WorldDoorSaveAdapter": return "Состояние двери";
            case "WorldToggleSaveAdapter": return "Состояние переключателя";
            case "WorldRigidbodySaveAdapter": return "Физика сохраняемого корня";
            case "WorldIdentitySaveAdapter": return "Фракции в сохранении мира";
            default: return ObjectNames.NicifyVariableName(type);
        }
    }
}

[CustomEditor(typeof(ActorIdentity))]
public sealed class ActorIdentityEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Все настройки актёра — открыть вкладки")) { Selection.activeGameObject = ((Component)target).gameObject; ActorSettingsWindow.Open(); }
        serializedObject.Update();
        Draw("faction", "Фракция актёра"); Draw("hostileFactions", "Кого считает врагом"); Draw("targetActorsWithoutIdentity", "Атаковать актёров без фракции");
        serializedObject.ApplyModifiedProperties();
    }
    private void Draw(string field, string label)
    {
        var p = serializedObject.FindProperty(field); EditorGUILayout.PropertyField(p,new GUIContent(label,p.tooltip),true);
    }
}


