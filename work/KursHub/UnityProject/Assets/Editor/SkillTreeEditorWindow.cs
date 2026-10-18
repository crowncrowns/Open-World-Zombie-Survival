using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Progression;

/// <summary>
/// Редактор исходного SkillTreeDefinition. Он не хранит вторую копию графа: каждый жест меняет
/// SerializedProperty исходного asset, поэтому Undo, GUID и изменения в Git остаются обычными Unity-данными.
/// </summary>
public sealed class SkillTreeEditorWindow : EditorWindow
{
    private const float InspectorWidth = 380f;
    private const float NodeWidth = 180f;
    private const float NodeHeight = 82f;

    [SerializeField, Tooltip("Редактируемый asset дерева. Изменения сохраняются в нём, а не в окне редактора.")]
    private SkillTreeDefinition tree;
    [SerializeField, Tooltip("ID выбранного узла редактора; не является состоянием прокачки игрока.")]
    private string selectedNodeId;
    [SerializeField, Tooltip("ID открытой вкладки редактора дерева.")]
    private string activeTabId;
    [SerializeField, Tooltip("Смещение полотна в пикселях редактора.")]
    private Vector2 pan = new Vector2(80f, 80f);
    [SerializeField, Tooltip("Масштаб полотна: 1 соответствует исходным размерам узлов.")]
    private float zoom = 1f;

    private SerializedObject source;
    private Vector2 inspectorScroll;
    private bool connectMode;
    private int inspectorPage;
    private string renameNodeId = "";
    private bool draggingNode;
    private bool panning;
    private Vector2 dragMouse;
    private Vector2 dragOrigin;
    private int dragUndoGroup = -1;
    private GUIStyle nodeTitle;
    private GUIStyle nodeDetail;
    private GUIStyle centeredHint;

    [MenuItem("Tools/Zombie Survival/Редактор дерева развития")]
    public static void Open()
    {
        var window = GetWindow<SkillTreeEditorWindow>();
        window.titleContent = new GUIContent("Дерево развития");
        window.minSize = new Vector2(880f, 500f);
        if (Selection.activeObject is SkillTreeDefinition selected) window.SelectTree(selected);
        window.Show();
    }

    public static void Open(SkillTreeDefinition definition)
    {
        Open();
        GetWindow<SkillTreeEditorWindow>().SelectTree(definition);
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
        BindSource();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        EndDrag();
    }

    private void OnUndoRedo()
    {
        BindSource();
        Repaint();
    }

    private void SelectTree(SkillTreeDefinition definition)
    {
        EndDrag();
        tree = definition;
        selectedNodeId = null;
        activeTabId = null;
        connectMode = false;
        BindSource();
        Repaint();
    }

    private void BindSource()
    {
        source = tree ? new SerializedObject(tree) : null;
    }

    private void OnGUI()
    {
        DrawToolbar();
        if (!tree)
        {
            EditorGUILayout.HelpBox("Выберите существующее дерево или создайте asset. Затем добавляйте вкладки, узлы и зависимости. Дерево назначается игроку в PlayerProgression.", MessageType.Info);
            return;
        }
        if (source == null || source.targetObject != tree) BindSource();
        source.Update();
        EnsureTabSelection();
        DrawTabs();
        DrawGraphToolbar();

        var body = GUILayoutUtility.GetRect(10f, 100000f, 10f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        float sideWidth = Mathf.Min(InspectorWidth, position.width * .47f);
        var graphRect = new Rect(body.x, body.y, Mathf.Max(100f, body.width - sideWidth - 4f), body.height);
        var sideRect = new Rect(graphRect.xMax + 4f, body.y, sideWidth, body.height);
        DrawGraph(graphRect);
        GUILayout.BeginArea(sideRect, EditorStyles.helpBox);
        inspectorPage = GUILayout.Toolbar(inspectorPage, new[] { "Выбранный узел", "Дерево / вкладки" });
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
        if (inspectorPage == 0) DrawNodeInspector(); else DrawTreeInspector();
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
        source.ApplyModifiedProperties();

        if (!tree.Validate(out string reason)) EditorGUILayout.HelpBox("Ошибка дерева: " + reason, MessageType.Error);
        GUILayout.Label("ЛКМ — выбор / перемещение · СКМ или Alt + ЛКМ — полотно · колесо — масштаб · Ctrl + ЛКМ по узлу — добавить зависимость выбранному", EditorStyles.miniLabel);
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var candidate = (SkillTreeDefinition)EditorGUILayout.ObjectField(new GUIContent("Дерево", "Исходный asset SkillTreeDefinition."), tree, typeof(SkillTreeDefinition), false);
            if (candidate != tree) SelectTree(candidate);
            if (GUILayout.Button(new GUIContent("Создать", "Создать самостоятельный asset дерева вне vendor-папок."), EditorStyles.toolbarButton, GUILayout.Width(70f))) CreateTree();
            using (new EditorGUI.DisabledScope(!tree))
            {
                if (GUILayout.Button("В Project", EditorStyles.toolbarButton, GUILayout.Width(75f))) EditorGUIUtility.PingObject(tree);
                if (GUILayout.Button(new GUIContent("Сохранить", "Записать изменённый asset на диск. Undo продолжает работать."), EditorStyles.toolbarButton, GUILayout.Width(85f)))
                {
                    source?.ApplyModifiedProperties();
                    EditorUtility.SetDirty(tree);
                    AssetDatabase.SaveAssetIfDirty(tree);
                    ShowNotification(new GUIContent("Дерево сохранено"));
                }
            }
        }
    }

    private void DrawTabs()
    {
        var tabs = source.FindProperty("tabs");
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            for (int i = 0; i < tabs.arraySize; i++)
            {
                var tab = tabs.GetArrayElementAtIndex(i);
                string id = Value(tab, "id");
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = tab.FindPropertyRelative("color").colorValue;
                bool chosen = GUILayout.Toggle(activeTabId == id, Value(tab, "title"), EditorStyles.toolbarButton);
                GUI.backgroundColor = previousColor;
                if (chosen && activeTabId != id) activeTabId = id;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Вкладка", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                AddTab();
                inspectorPage = 1;
            }
        }
    }

    private void DrawGraphToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(activeTabId)))
            {
                if (GUILayout.Button("+ Узел", EditorStyles.toolbarButton, GUILayout.Width(75f))) AddNode((new Vector2(180f, 120f) - pan) / zoom);
            }
            using (new EditorGUI.DisabledScope(SelectedIndex() < 0))
            {
                connectMode = GUILayout.Toggle(connectMode, new GUIContent("Связать", "Выбранный узел будет требовать узел, по которому вы нажмёте. Можно переключить вкладку для межвкладочной связи."), EditorStyles.toolbarButton, GUILayout.Width(80f));
            }
            if (GUILayout.Button("По центру", EditorStyles.toolbarButton, GUILayout.Width(90f))) CenterGraph();
            GUILayout.Label($"{zoom:P0}", EditorStyles.miniLabel, GUILayout.Width(40f));
            if (connectMode) GUILayout.Label("Нажмите необходимый узел → выбранный будет зависеть от него", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
        }
    }

    private void DrawGraph(Rect graphRect)
    {
        EditorGUI.DrawRect(graphRect, new Color(.105f, .115f, .13f));
        GUI.BeginGroup(graphRect);
        var local = new Rect(Vector2.zero, graphRect.size);
        DrawGrid(local);
        var nodes = source.FindProperty("nodes");
        var visible = new List<int>();
        for (int i = 0; i < nodes.arraySize; i++) if (Value(nodes.GetArrayElementAtIndex(i), "tabId") == activeTabId) visible.Add(i);

        Handles.BeginGUI();
        foreach (int index in visible)
        {
            var child = nodes.GetArrayElementAtIndex(index);
            var prerequisites = child.FindPropertyRelative("prerequisiteIds");
            for (int p = 0; p < prerequisites.arraySize; p++)
            {
                int parentIndex = FindNode(prerequisites.GetArrayElementAtIndex(p).stringValue);
                if (parentIndex < 0) continue;
                var parent = nodes.GetArrayElementAtIndex(parentIndex);
                if (Value(parent, "tabId") != activeTabId) continue;
                Rect a = NodeRect(parent), b = NodeRect(child);
                Vector2 direction = (b.center - a.center).normalized;
                Vector2 start = a.center + direction * (NodeHeight * zoom * .35f);
                Vector2 end = b.center - direction * (NodeHeight * zoom * .35f);
                Color color = TabColor(activeTabId);
                Handles.DrawBezier(start, end, start + direction * 50f * zoom, end - direction * 50f * zoom, color, null, 2.5f);
                Vector2 arrow = Vector2.Lerp(start, end, .78f);
                Vector2 normal = new Vector2(-direction.y, direction.x);
                Handles.color = color;
                Handles.DrawAAPolyLine(3f, arrow - direction * 10f * zoom + normal * 5f * zoom, arrow, arrow - direction * 10f * zoom - normal * 5f * zoom);
            }
        }
        Handles.EndGUI();

        nodeTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = Mathf.RoundToInt(12f * zoom), clipping = TextClipping.Clip, wordWrap = false };
        nodeDetail = new GUIStyle(EditorStyles.miniLabel) { fontSize = Mathf.RoundToInt(10f * zoom), clipping = TextClipping.Clip };
        nodeTitle.normal.textColor = Color.white;
        nodeDetail.normal.textColor = new Color(.8f, .84f, .88f);
        foreach (int index in visible) DrawNode(nodes.GetArrayElementAtIndex(index));
        if (visible.Count == 0)
        {
            centeredHint ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true };
            GUI.Label(local, "На этой вкладке нет узлов.\nНажмите «+ Узел» или ПКМ на полотне.", centeredHint);
        }
        HandleGraphInput(local, visible);
        GUI.EndGroup();
    }

    private void DrawGrid(Rect rect)
    {
        float step = 40f * zoom;
        Handles.BeginGUI();
        Handles.color = new Color(1f, 1f, 1f, .045f);
        for (float x = pan.x % step; x < rect.width; x += step) Handles.DrawLine(new Vector2(x, 0), new Vector2(x, rect.height));
        for (float y = pan.y % step; y < rect.height; y += step) Handles.DrawLine(new Vector2(0, y), new Vector2(rect.width, y));
        Handles.EndGUI();
    }

    private void DrawNode(SerializedProperty node)
    {
        Rect rect = NodeRect(node);
        bool selected = Value(node, "id") == selectedNodeId;
        Color color = TabColor(Value(node, "tabId"));
        EditorGUI.DrawRect(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), selected ? new Color(1f, .8f, .32f) : color * .75f);
        EditorGUI.DrawRect(rect, selected ? new Color(.23f, .24f, .27f) : new Color(.17f, .18f, .21f));
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 4f * zoom), color);
        float inset = 8f * zoom;
        GUI.Label(new Rect(rect.x + inset, rect.y + 8f * zoom, rect.width - inset * 2, 22f * zoom), Value(node, "title"), nodeTitle);
        GUI.Label(new Rect(rect.x + inset, rect.y + 32f * zoom, rect.width - inset * 2, 18f * zoom), $"Цена: {node.FindPropertyRelative("pointCost").intValue} · рангов: {node.FindPropertyRelative("maxRank").intValue}", nodeDetail);
        int dependencies = node.FindPropertyRelative("prerequisiteIds").arraySize;
        string relation = node.FindPropertyRelative("requireAllPrerequisites").boolValue ? "все" : "любой";
        GUI.Label(new Rect(rect.x + inset, rect.y + 53f * zoom, rect.width - inset * 2, 18f * zoom), dependencies == 0 ? "Начальный навык" : $"Зависимости: {dependencies} ({relation})", nodeDetail);
    }

    private void HandleGraphInput(Rect local, List<int> visible)
    {
        Event e = Event.current;
        var nodes = source.FindProperty("nodes");
        int hovered = -1;
        for (int i = visible.Count - 1; i >= 0; i--)
        {
            if (NodeRect(nodes.GetArrayElementAtIndex(visible[i])).Contains(e.mousePosition)) { hovered = visible[i]; break; }
        }
        if (e.type == EventType.ScrollWheel && local.Contains(e.mousePosition))
        {
            float oldZoom = zoom;
            zoom = Mathf.Clamp(zoom * Mathf.Pow(1.1f, -e.delta.y), .4f, 1.6f);
            pan = e.mousePosition - (e.mousePosition - pan) / oldZoom * zoom;
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseDown && local.Contains(e.mousePosition))
        {
            if (e.button == 2 || e.button == 0 && e.alt)
            {
                panning = true;
                dragMouse = e.mousePosition;
                dragOrigin = pan;
                e.Use();
            }
            else if (e.button == 0)
            {
                if (hovered >= 0)
                {
                    var node = nodes.GetArrayElementAtIndex(hovered);
                    string clicked = Value(node, "id");
                    if ((connectMode || e.control || e.command) && SelectedIndex() >= 0)
                    {
                        AddDependency(selectedNodeId, clicked);
                        connectMode = false;
                    }
                    else
                    {
                        BeginNodeDrag(node, e.mousePosition);
                    }
                }
                else if (!connectMode) { selectedNodeId = null; draggingNode = false; }
                GUI.FocusControl(null);
                e.Use();
                Repaint();
            }
            else if (e.button == 1)
            {
                ShowContextMenu(hovered, (e.mousePosition - pan) / zoom);
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && (draggingNode || panning))
        {
            if (panning) pan = dragOrigin + e.mousePosition - dragMouse;
            else
            {
                MoveSelectedNode(dragOrigin + (e.mousePosition - dragMouse) / zoom);
            }
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp && (draggingNode || panning))
        {
            EndDrag();
            e.Use();
        }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            connectMode = false;
            EndDrag();
            Repaint();
        }
    }

    private void EndDrag()
    {
        source?.ApplyModifiedProperties();
        if (dragUndoGroup >= 0) Undo.CollapseUndoOperations(dragUndoGroup);
        dragUndoGroup = -1;
        draggingNode = false;
        panning = false;
    }

    private void BeginNodeDrag(SerializedProperty node, Vector2 mouse)
    {
        selectedNodeId = renameNodeId = Value(node, "id");
        inspectorPage = 0;
        draggingNode = true;
        dragMouse = mouse;
        dragOrigin = node.FindPropertyRelative("position").vector2Value;
        Undo.IncrementCurrentGroup();
        dragUndoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Переместить узел развития");
    }

    private void MoveSelectedNode(Vector2 logicalPosition)
    {
        int selected = SelectedIndex();
        if (selected >= 0) source.FindProperty("nodes").GetArrayElementAtIndex(selected).FindPropertyRelative("position").vector2Value = logicalPosition;
    }

    private void ShowContextMenu(int index, Vector2 logicalPosition)
    {
        var menu = new GenericMenu();
        if (!string.IsNullOrEmpty(activeTabId)) menu.AddItem(new GUIContent("Добавить узел здесь"), false, () => { source.Update(); AddNode(logicalPosition); Commit(); });
        if (index >= 0)
        {
            string id = Value(source.FindProperty("nodes").GetArrayElementAtIndex(index), "id");
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Дублировать узел"), false, () => { source.Update(); DuplicateNode(id); Commit(); });
            if (SelectedIndex() >= 0 && selectedNodeId != id) menu.AddItem(new GUIContent("Выбранный узел требует этот"), false, () => { source.Update(); AddDependency(selectedNodeId, id); Commit(); });
            menu.AddItem(new GUIContent("Удалить узел и ссылки на него"), false, () => { source.Update(); DeleteNode(id); Commit(); });
        }
        menu.ShowAsContext();
    }

    private void DrawNodeInspector()
    {
        int index = SelectedIndex();
        if (index < 0)
        {
            EditorGUILayout.HelpBox("Выберите узел на полотне. Зависимость направлена от необходимого навыка к следующему. Связи между вкладками поддерживаются и перечислены в инспекторе.", MessageType.Info);
            return;
        }
        var node = source.FindProperty("nodes").GetArrayElementAtIndex(index);
        EditorGUILayout.LabelField("Свойства навыка", EditorStyles.boldLabel);
        Field(node, "title", "Название");
        Field(node, "description", "Описание");
        DrawNodeTab(node);
        Field(node, "position", "Позиция на полотне");
        Field(node, "pointCost", "Цена каждого ранга, очки");
        Field(node, "maxRank", "Максимум рангов");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Постоянный ID", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField("Текущий ID", Value(node, "id"));
        renameNodeId = EditorGUILayout.TextField(new GUIContent("Новый ID", "Менять до выпуска сохранений. Кнопка обновит ссылки в этом asset; старые сохранения требуют миграции."), renameNodeId);
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(renameNodeId) || renameNodeId == selectedNodeId))
        {
            if (GUILayout.Button("Переименовать ID и зависимости")) RenameNode(selectedNodeId, renameNodeId.Trim());
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Необходимые навыки", EditorStyles.boldLabel);
        Field(node, "requireAllPrerequisites", "Нужны все зависимости");
        var prerequisites = node.FindPropertyRelative("prerequisiteIds");
        for (int i = 0; i < prerequisites.arraySize; i++)
        {
            string id = prerequisites.GetArrayElementAtIndex(i).stringValue;
            int parent = FindNode(id);
            string title = parent < 0 ? "ОШИБКА: " + id : NodeLabel(source.FindProperty("nodes").GetArrayElementAtIndex(parent));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(title, id), EditorStyles.miniButton))
                {
                    if (parent >= 0) FocusNode(id);
                }
                if (GUILayout.Button(new GUIContent("×", "Удалить только эту зависимость."), GUILayout.Width(24f))) { prerequisites.DeleteArrayElementAtIndex(i); break; }
            }
        }
        if (prerequisites.arraySize == 0) EditorGUILayout.LabelField("Нет: навык доступен без предыдущих покупок.", EditorStyles.wordWrappedMiniLabel);
        if (GUILayout.Button("+ Выбрать зависимость из списка")) ShowDependencyMenu(selectedNodeId);
        EditorGUILayout.HelpBox("Все зависимости: требуется каждый предыдущий навык. Если флажок снят — достаточно любого. Стрелки показывают порядок покупки; циклы запрещены.", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Эффекты каждого ранга", EditorStyles.boldLabel);
        var effects = node.FindPropertyRelative("effects");
        for (int i = 0; i < effects.arraySize; i++)
        {
            var effect = effects.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Эффект {i + 1}", EditorStyles.boldLabel);
                    if (GUILayout.Button("Удалить", GUILayout.Width(75f))) { effects.DeleteArrayElementAtIndex(i); break; }
                }
                Field(effect, "stat", "Характеристика");
                Field(effect, "flat", "Прибавка, единицы");
                Field(effect, "percent", "Прибавка, %");
                Field(effect, "abilityId", "ID способности");
            }
        }
        if (GUILayout.Button("+ Эффект"))
        {
            effects.InsertArrayElementAtIndex(effects.arraySize);
            var effect = effects.GetArrayElementAtIndex(effects.arraySize - 1);
            effect.FindPropertyRelative("stat").enumValueIndex = 0;
            effect.FindPropertyRelative("flat").floatValue = 0;
            effect.FindPropertyRelative("percent").floatValue = 0;
            effect.FindPropertyRelative("abilityId").stringValue = "";
        }
        EditorGUILayout.HelpBox("Пример: здоровье +5 → характеристика «Максимальное здоровье», единицы 5, проценты 0. Выносливость +1% → «Запас выносливости», единицы 0, проценты 1. ID способности открывает флаг для игровых систем; сам по себе он не создаёт новую механику.", MessageType.Info);
        if (GUILayout.Button("Дублировать выбранный узел")) DuplicateNode(selectedNodeId);
        if (GUILayout.Button("Удалить узел и ссылки на него")) DeleteNode(selectedNodeId);
    }

    private void DrawNodeTab(SerializedProperty node)
    {
        var tabs = source.FindProperty("tabs");
        var titles = new string[tabs.arraySize];
        int current = -1;
        for (int i = 0; i < tabs.arraySize; i++)
        {
            var tab = tabs.GetArrayElementAtIndex(i);
            titles[i] = Value(tab, "title");
            if (Value(node, "tabId") == Value(tab, "id")) current = i;
        }
        EditorGUI.BeginChangeCheck();
        int choice = EditorGUILayout.Popup(new GUIContent("Вкладка", "Только раздел отображения. Зависимости могут вести в другие вкладки."), current, titles);
        if (EditorGUI.EndChangeCheck() && choice >= 0)
        {
            node.FindPropertyRelative("tabId").stringValue = Value(tabs.GetArrayElementAtIndex(choice), "id");
            activeTabId = node.FindPropertyRelative("tabId").stringValue;
        }
    }

    private void DrawTreeInspector()
    {
        EditorGUILayout.LabelField("Исходные данные дерева", EditorStyles.boldLabel);
        var id = source.FindProperty("treeId");
        EditorGUILayout.PropertyField(id, new GUIContent("ID дерева", id.tooltip));
        EditorGUILayout.HelpBox("ID дерева и узлов — ключи сохранения. После выпуска игры не меняйте их без миграции. Позиции, тексты, цвета и зависимости редактируются в этом asset. Базовые значения характеристик задаются в компонентах персонажа.", MessageType.Info);
        var tabs = source.FindProperty("tabs");
        for (int i = 0; i < tabs.arraySize; i++)
        {
            var tab = tabs.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Вкладка {i + 1}", EditorStyles.boldLabel);
                Field(tab, "title", "Название");
                Field(tab, "color", "Цвет ветви");
                // ID вкладки не участвует в покупке, но при его смене нужно перенести все узлы сразу.
                string oldId = Value(tab, "id");
                EditorGUI.BeginChangeCheck();
                string newId = EditorGUILayout.DelayedTextField(new GUIContent("ID вкладки", "Уникальный непустой ключ. При изменении узлы автоматически переводятся на новый ID."), oldId);
                if (EditorGUI.EndChangeCheck()) RenameTab(i, oldId, newId.Trim());
                if (GUILayout.Button("Удалить вкладку…")) { DeleteTab(i); break; }
            }
        }
        if (GUILayout.Button("+ Новая вкладка")) AddTab();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Узлов во всём дереве: {source.FindProperty("nodes").arraySize}");
        if (GUILayout.Button("Проверить дерево"))
        {
            Commit();
            bool valid = tree.Validate(out string reason);
            EditorUtility.DisplayDialog("Проверка дерева", valid ? "Дерево корректно. Проверьте также эффекты в Play и сохранение / загрузку." : reason, "Понятно");
        }
    }

    private void AddNode(Vector2 logicalPosition)
    {
        var nodes = source.FindProperty("nodes");
        nodes.InsertArrayElementAtIndex(nodes.arraySize);
        var node = nodes.GetArrayElementAtIndex(nodes.arraySize - 1);
        string id = Guid.NewGuid().ToString("N");
        node.FindPropertyRelative("id").stringValue = id;
        node.FindPropertyRelative("title").stringValue = "Новый навык";
        node.FindPropertyRelative("description").stringValue = "";
        node.FindPropertyRelative("tabId").stringValue = activeTabId ?? "";
        node.FindPropertyRelative("position").vector2Value = logicalPosition;
        node.FindPropertyRelative("pointCost").intValue = 1;
        node.FindPropertyRelative("maxRank").intValue = 1;
        node.FindPropertyRelative("requireAllPrerequisites").boolValue = true;
        node.FindPropertyRelative("prerequisiteIds").ClearArray();
        node.FindPropertyRelative("effects").ClearArray();
        selectedNodeId = renameNodeId = id;
        inspectorPage = 0;
        connectMode = false;
    }

    private void DuplicateNode(string id)
    {
        int index = FindNode(id);
        if (index < 0) return;
        // Сначала фиксируем отредактированные поля, затем читаем исходный экземпляр. InsertArrayElement
        // имеет неопределённое начальное содержимое, поэтому заполняем копию явно, включая все эффекты.
        source.ApplyModifiedProperties();
        var original = tree.Nodes[index];
        var snapshot = JsonUtility.FromJson<SkillNodeDefinition>(JsonUtility.ToJson(original));
        source.Update();
        var nodes = source.FindProperty("nodes");
        nodes.InsertArrayElementAtIndex(nodes.arraySize);
        var copy = nodes.GetArrayElementAtIndex(nodes.arraySize - 1);
        selectedNodeId = renameNodeId = Guid.NewGuid().ToString("N");
        copy.FindPropertyRelative("id").stringValue = selectedNodeId;
        copy.FindPropertyRelative("title").stringValue = snapshot.title + " (копия)";
        copy.FindPropertyRelative("description").stringValue = snapshot.description;
        copy.FindPropertyRelative("tabId").stringValue = snapshot.tabId;
        copy.FindPropertyRelative("position").vector2Value = snapshot.position + new Vector2(35f, 100f);
        copy.FindPropertyRelative("pointCost").intValue = snapshot.pointCost;
        copy.FindPropertyRelative("maxRank").intValue = snapshot.maxRank;
        copy.FindPropertyRelative("requireAllPrerequisites").boolValue = snapshot.requireAllPrerequisites;
        var parents = copy.FindPropertyRelative("prerequisiteIds");
        parents.ClearArray();
        foreach (string parentId in snapshot.prerequisiteIds)
        {
            parents.InsertArrayElementAtIndex(parents.arraySize);
            parents.GetArrayElementAtIndex(parents.arraySize - 1).stringValue = parentId;
        }
        var effects = copy.FindPropertyRelative("effects");
        effects.ClearArray();
        foreach (var effect in snapshot.effects)
        {
            effects.InsertArrayElementAtIndex(effects.arraySize);
            var target = effects.GetArrayElementAtIndex(effects.arraySize - 1);
            target.FindPropertyRelative("stat").intValue = (int)effect.stat;
            target.FindPropertyRelative("flat").floatValue = effect.flat;
            target.FindPropertyRelative("percent").floatValue = effect.percent;
            target.FindPropertyRelative("abilityId").stringValue = effect.abilityId;
        }
        activeTabId = Value(copy, "tabId");
        inspectorPage = 0;
    }

    private void DeleteNode(string id)
    {
        int index = FindNode(id);
        if (index < 0) return;
        if (!EditorUtility.DisplayDialog("Удаление навыка", "Удалить выбранный узел и все зависимости от него? Отменить можно через Ctrl+Z. Уже выпущенные сохранения потребуют миграции удалённого ID.", "Удалить", "Отмена")) return;
        var nodes = source.FindProperty("nodes");
        nodes.DeleteArrayElementAtIndex(index);
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var parents = nodes.GetArrayElementAtIndex(i).FindPropertyRelative("prerequisiteIds");
            for (int p = parents.arraySize - 1; p >= 0; p--) if (parents.GetArrayElementAtIndex(p).stringValue == id) parents.DeleteArrayElementAtIndex(p);
        }
        if (selectedNodeId == id) selectedNodeId = null;
        connectMode = false;
    }

    private void RenameNode(string oldId, string newId)
    {
        if (FindNode(newId) >= 0) { ShowNotification(new GUIContent("Такой ID узла уже существует")); return; }
        var nodes = source.FindProperty("nodes");
        int index = FindNode(oldId);
        if (index < 0) return;
        nodes.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue = newId;
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var parents = nodes.GetArrayElementAtIndex(i).FindPropertyRelative("prerequisiteIds");
            for (int p = 0; p < parents.arraySize; p++) if (parents.GetArrayElementAtIndex(p).stringValue == oldId) parents.GetArrayElementAtIndex(p).stringValue = newId;
        }
        selectedNodeId = renameNodeId = newId;
    }

    private void ShowDependencyMenu(string childId)
    {
        var menu = new GenericMenu();
        var nodes = source.FindProperty("nodes");
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var node = nodes.GetArrayElementAtIndex(i);
            string id = Value(node, "id");
            if (id == childId) continue;
            string label = NodeLabel(node).Replace("/", "∕");
            if (CreatesCycle(childId, id)) menu.AddDisabledItem(new GUIContent(label + " (цикл)"));
            else menu.AddItem(new GUIContent(label), HasDependency(childId, id), () => { source.Update(); AddDependency(childId, id); Commit(); });
        }
        if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("Добавьте другой узел"));
        menu.ShowAsContext();
    }

    private void AddDependency(string childId, string parentId)
    {
        if (childId == parentId || CreatesCycle(childId, parentId)) { ShowNotification(new GUIContent("Эта связь создаёт цикл")); return; }
        int index = FindNode(childId);
        if (index < 0 || FindNode(parentId) < 0 || HasDependency(childId, parentId)) return;
        var list = source.FindProperty("nodes").GetArrayElementAtIndex(index).FindPropertyRelative("prerequisiteIds");
        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = parentId;
    }

    private bool HasDependency(string child, string parent)
    {
        int index = FindNode(child);
        if (index < 0) return false;
        var list = source.FindProperty("nodes").GetArrayElementAtIndex(index).FindPropertyRelative("prerequisiteIds");
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).stringValue == parent) return true;
        return false;
    }

    private bool CreatesCycle(string childId, string parentId)
    {
        var visited = new HashSet<string>();
        var queue = new Stack<string>();
        queue.Push(parentId);
        var nodes = source.FindProperty("nodes");
        while (queue.Count > 0)
        {
            string id = queue.Pop();
            if (id == childId) return true;
            if (!visited.Add(id)) continue;
            int index = FindNode(id);
            if (index < 0) continue;
            var parents = nodes.GetArrayElementAtIndex(index).FindPropertyRelative("prerequisiteIds");
            for (int p = 0; p < parents.arraySize; p++) queue.Push(parents.GetArrayElementAtIndex(p).stringValue);
        }
        return false;
    }

    private void AddTab()
    {
        var tabs = source.FindProperty("tabs");
        tabs.InsertArrayElementAtIndex(tabs.arraySize);
        var tab = tabs.GetArrayElementAtIndex(tabs.arraySize - 1);
        activeTabId = "tab_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        tab.FindPropertyRelative("id").stringValue = activeTabId;
        tab.FindPropertyRelative("title").stringValue = "Новая вкладка";
        tab.FindPropertyRelative("color").colorValue = new Color(.3f, .7f, 1f);
    }

    private void RenameTab(int index, string oldId, string newId)
    {
        var tabs = source.FindProperty("tabs");
        if (string.IsNullOrWhiteSpace(newId)) { ShowNotification(new GUIContent("ID вкладки не может быть пустым")); return; }
        for (int i = 0; i < tabs.arraySize; i++) if (i != index && Value(tabs.GetArrayElementAtIndex(i), "id") == newId) { ShowNotification(new GUIContent("ID вкладки уже существует")); return; }
        tabs.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue = newId;
        var nodes = source.FindProperty("nodes");
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var tab = nodes.GetArrayElementAtIndex(i).FindPropertyRelative("tabId");
            if (tab.stringValue == oldId) tab.stringValue = newId;
        }
        if (activeTabId == oldId) activeTabId = newId;
    }

    private void DeleteTab(int index)
    {
        var tabs = source.FindProperty("tabs");
        string id = Value(tabs.GetArrayElementAtIndex(index), "id");
        var nodes = source.FindProperty("nodes");
        int count = 0;
        for (int i = 0; i < nodes.arraySize; i++) if (Value(nodes.GetArrayElementAtIndex(i), "tabId") == id) count++;
        if (tabs.arraySize == 1 && count > 0) { EditorUtility.DisplayDialog("Последняя вкладка", "Сначала удалите её узлы либо добавьте вкладку, куда можно их перенести.", "Понятно"); return; }
        string targetId = tabs.arraySize > 1 ? Value(tabs.GetArrayElementAtIndex(index == 0 ? 1 : 0), "id") : null;
        string message = count == 0 ? "Удалить пустую вкладку?" : $"Перенести {count} узлов в первую оставшуюся вкладку и удалить эту? Узлы и зависимости сохранятся.";
        if (!EditorUtility.DisplayDialog("Удаление вкладки", message, "Удалить", "Отмена")) return;
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var tab = nodes.GetArrayElementAtIndex(i).FindPropertyRelative("tabId");
            if (tab.stringValue == id) tab.stringValue = targetId;
        }
        tabs.DeleteArrayElementAtIndex(index);
        if (activeTabId == id) activeTabId = targetId;
    }

    private void EnsureTabSelection()
    {
        var tabs = source.FindProperty("tabs");
        for (int i = 0; i < tabs.arraySize; i++) if (Value(tabs.GetArrayElementAtIndex(i), "id") == activeTabId) return;
        activeTabId = tabs.arraySize > 0 ? Value(tabs.GetArrayElementAtIndex(0), "id") : null;
    }

    private void CenterGraph()
    {
        var nodes = source.FindProperty("nodes");
        Vector2 sum = Vector2.zero;
        int count = 0;
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var node = nodes.GetArrayElementAtIndex(i);
            if (Value(node, "tabId") != activeTabId) continue;
            sum += node.FindPropertyRelative("position").vector2Value;
            count++;
        }
        float graphWidth = position.width - Mathf.Min(InspectorWidth, position.width * .47f) - 4f;
        pan = count == 0 ? new Vector2(80f, 80f) : new Vector2(graphWidth * .5f, (position.height - 110f) * .5f) - sum / count * zoom;
    }

    private void FocusNode(string id)
    {
        int index = FindNode(id);
        if (index < 0) return;
        var node = source.FindProperty("nodes").GetArrayElementAtIndex(index);
        selectedNodeId = renameNodeId = id;
        activeTabId = Value(node, "tabId");
        float graphWidth = position.width - Mathf.Min(InspectorWidth, position.width * .47f) - 4f;
        pan = new Vector2(graphWidth * .5f, (position.height - 110f) * .5f) - node.FindPropertyRelative("position").vector2Value * zoom;
        Repaint();
    }

    private int SelectedIndex() => string.IsNullOrEmpty(selectedNodeId) ? -1 : FindNode(selectedNodeId);

    private int FindNode(string id)
    {
        var nodes = source.FindProperty("nodes");
        for (int i = 0; i < nodes.arraySize; i++) if (Value(nodes.GetArrayElementAtIndex(i), "id") == id) return i;
        return -1;
    }

    private Color TabColor(string id)
    {
        var tabs = source.FindProperty("tabs");
        for (int i = 0; i < tabs.arraySize; i++)
        {
            var tab = tabs.GetArrayElementAtIndex(i);
            if (Value(tab, "id") == id) return tab.FindPropertyRelative("color").colorValue;
        }
        return new Color(.8f, .3f, .3f);
    }

    private Rect NodeRect(SerializedProperty node) => new Rect((node.FindPropertyRelative("position").vector2Value - new Vector2(NodeWidth, NodeHeight) * .5f) * zoom + pan, new Vector2(NodeWidth, NodeHeight) * zoom);
    private static string Value(SerializedProperty property, string name) => property.FindPropertyRelative(name).stringValue;
    private static string NodeLabel(SerializedProperty node) => Value(node, "title") + " [" + Value(node, "tabId") + "]";
    private static void Field(SerializedProperty parent, string name, string label)
    {
        var field = parent.FindPropertyRelative(name);
        EditorGUILayout.PropertyField(field, new GUIContent(label, field.tooltip), true);
    }

    private void Commit()
    {
        source.ApplyModifiedProperties();
        EditorUtility.SetDirty(tree);
        Repaint();
    }

    private void CreateTree()
    {
        string path = EditorUtility.SaveFilePanelInProject("Новое дерево развития", "CharacterSkillTree", "asset", "Сохраните авторское дерево вне vendor-папок.");
        if (string.IsNullOrEmpty(path)) return;
        var asset = CreateInstance<SkillTreeDefinition>();
        AssetDatabase.CreateAsset(asset, path);
        SelectTree(asset);
        source.Update();
        source.FindProperty("treeId").stringValue = Guid.NewGuid().ToString("N");
        // Не полагаемся на конструктор сериализуемых элементов: массивы Unity копируют предыдущий элемент.
        source.FindProperty("tabs").ClearArray();
        source.FindProperty("nodes").ClearArray();
        AddTab();
        var tab = source.FindProperty("tabs").GetArrayElementAtIndex(0);
        tab.FindPropertyRelative("title").stringValue = "Характеристики";
        source.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(asset);
        Selection.activeObject = asset;
    }
}
