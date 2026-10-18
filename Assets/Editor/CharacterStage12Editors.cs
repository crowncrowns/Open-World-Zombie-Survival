using UnityEditor;
using UnityEngine;
using ZombieSurvival.Progression;

/// <summary>InspectorName и Tooltip находятся у исходных полей runtime, а не в отдельной таблице значений окна.</summary>
[CustomEditor(typeof(SkillTreeDefinition))]
public sealed class SkillTreeDefinitionInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var tree = (SkillTreeDefinition)target;
        if (GUILayout.Button("Открыть визуальный редактор дерева")) SkillTreeEditorWindow.Open(tree);
        EditorGUILayout.HelpBox("Визуальный редактор и списки ниже меняют один asset. Для создания связи, переноса узлов и безопасного переименования ID удобнее использовать редактор. Координаты — центр узла, в пикселях.", MessageType.Info);
        DrawDefaultInspector();
        if (!tree.Validate(out string reason)) EditorGUILayout.HelpBox(reason, MessageType.Error);
        else EditorGUILayout.HelpBox($"Дерево корректно: {tree.Tabs.Count} вкладок, {tree.Nodes.Count} узлов.", MessageType.Info);
    }
}

[CustomEditor(typeof(PlayerProgression))]
public sealed class PlayerProgressionInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var player = (PlayerProgression)target;
        if (player.Definition && GUILayout.Button("Редактировать назначенное дерево")) SkillTreeEditorWindow.Open(player.Definition);
        EditorGUILayout.HelpBox("Очки развития не являются рублями. Базовые значения задаются в Health, PlayerStamina, FirstPersonController и типах оружия; приобретённые узлы только добавляют модификаторы.", MessageType.None);
        if (!Application.isPlaying) return;
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Текущий уровень", player.Level);
            EditorGUILayout.LongField("Всего опыта, XP", player.Experience);
            EditorGUILayout.LongField("До следующего уровня, XP", player.ExperienceToNextLevel);
            EditorGUILayout.IntField("Свободные очки", player.AvailablePoints);
            if (player.Definition)
                foreach (var node in player.Definition.Nodes)
                    if (node != null && player.RankOf(node.Id) > 0) EditorGUILayout.IntField(node.Title + " — ранг", player.RankOf(node.Id));
        }
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
}

[CustomEditor(typeof(PlayerStatModifiers))]
public sealed class PlayerStatModifiersInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Производный набор бонусов от PlayerProgression. Отдельно редактируемых базовых характеристик здесь нет. Для новой характеристики добавьте стабильный StatId и подключите Evaluate в механике, которая использует значение.", MessageType.Info);
        DrawDefaultInspector();
    }
}
