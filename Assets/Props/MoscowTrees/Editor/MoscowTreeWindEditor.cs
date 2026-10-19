#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(MoscowTreeWind)), CanEditMultipleObjects]
public sealed class MoscowTreeWindEditor : Editor
{
    private void OnEnable() => EditorApplication.update += UpdatePreview;
    private void OnDisable()
    {
        EditorApplication.update -= UpdatePreview;
        foreach (var item in targets)
            if (item is MoscowTreeWind wind && wind != null) wind.RestoreRestPose();
    }
    private void UpdatePreview()
    {
        if (Application.isPlaying || serializedObject == null || target == null) return;
        serializedObject.Update();
        if (!serializedObject.FindProperty("animateInEditMode").boolValue) return;
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("Ветер уже встроен в префаб: ствол и ветви качаются в Play без Animator. Это процедурная анимация Unity, а не клип внутри FBX.", MessageType.Info);
        Field("enableWind", "Включить ветер");
        Field("windFrom", "Откуда дует ветер");
        if (serializedObject.FindProperty("windFrom").enumValueIndex == (int)MoscowTreeWind.WindFromDirection.Custom)
            Field("customTravelDirection", "Куда дует: мировой XZ");
        Field("windStrength", "Сила, множитель"); Field("windSpeed", "Скорость качания, множитель");
        Field("gustiness", "Выраженность порывов"); Field("gustFrequency", "Частота порывов, 1/с");
        Field("trunkBendDegrees", "Качание ствола, °"); Field("branchBendDegrees", "Качание ветвей, °");
        Field("variationSeed", "Вариация фазы");
        bool asset = EditorUtility.IsPersistent(target) || PrefabStageUtility.GetCurrentPrefabStage() != null;
        using (new EditorGUI.DisabledScope(asset)) Field("animateInEditMode", "Предпросмотр в сцене без Play");
        if (asset) EditorGUILayout.HelpBox("Для просмотра анимации перетащите префаб в сцену и включите Play либо предпросмотр на его экземпляре.", MessageType.None);
        serializedObject.ApplyModifiedProperties();
        if (GUILayout.Button("Вернуть исходную позу"))
        {
            serializedObject.FindProperty("animateInEditMode").boolValue = false;
            serializedObject.ApplyModifiedProperties();
            foreach (var item in targets) ((MoscowTreeWind)item).RestoreRestPose();
            SceneView.RepaintAll();
        }
    }
    private void Field(string name, string label)
    {
        var property = serializedObject.FindProperty(name);
        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
    }
}
#endif
