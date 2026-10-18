using UnityEditor;
using UnityEngine;
using ZombieSurvival.Services;

[CustomEditor(typeof(ServiceVisual))]
public sealed class ServiceVisualEditor : Editor
{
    public override void OnInspectorGUI()
    {
        int group = Undo.GetCurrentGroup();
        bool changed = DrawDefaultInspector();
        var visual = (ServiceVisual)target;
        if (changed) { visual.ApplyModel(); Undo.CollapseUndoOperations(group); }
        EditorGUILayout.HelpBox("Назначьте визуальный prefab. Скрипты NPC/AI отклоняются до создания; состояние торговца остаётся на корне.", MessageType.Info);
        if (!string.IsNullOrEmpty(visual.ValidationMessage)) EditorGUILayout.HelpBox(visual.ValidationMessage, MessageType.Warning);
        if (GUILayout.Button("Применить модель")) visual.ApplyModel();
    }
}
