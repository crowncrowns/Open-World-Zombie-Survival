using UnityEditor;
using UnityEngine;
using ZombieSurvival.Services;

[CustomEditor(typeof(ServiceVisual))]
public sealed class ServiceVisualEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var visual = (ServiceVisual)target;
        visual.CaptureManualPose();
        int group = Undo.GetCurrentGroup();
        bool changed = DrawDefaultInspector();
        if (changed) { visual.ApplyModel(); Undo.CollapseUndoOperations(group); }
        EditorGUILayout.HelpBox("Назначьте визуальный prefab. Скрипты NPC/AI отклоняются до создания; состояние торговца остаётся на корне.", MessageType.Info);
        if (visual.GetComponent<Trader>() != null)
        {
            EditorGUILayout.HelpBox("Для нового торговца копируйте корень целиком. Дочерняя модель содержит только внешний вид. Команда ниже копирует также ассортимент, бюджет и назначает новые ID.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(visual)))
                if (GUILayout.Button("Дублировать торговца целиком"))
                    Selection.activeGameObject = TraderCopyTools.Duplicate(visual.GetComponent<Trader>());
        }
        EditorGUILayout.HelpBox("Положение/поворот/масштаб — абсолютные локальные значения Transform модели. Можно двигать сам корень торговца или дочернюю модель: повторное применение не сбрасывает ручную посадку.", MessageType.None);
        if (visual.Instance != null)
        {
            string actual = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual.Instance);
            string configured = AssetDatabase.GetAssetPath(visual.VisualPrefab);
            if (!string.IsNullOrEmpty(actual) && actual != configured)
                EditorGUILayout.HelpBox("Дочерняя модель заменена напрямую в иерархии и отличается от поля «Префаб модели». Назначьте нужный prefab в это поле перед сменой материала/модели; ручная посадка сохранится. Автоматической подмены источника нет.", MessageType.Warning);
        }
        if (!string.IsNullOrEmpty(visual.ValidationMessage)) EditorGUILayout.HelpBox(visual.ValidationMessage, MessageType.Warning);
        if (GUILayout.Button("Применить модель")) visual.ApplyModel();
    }
}
