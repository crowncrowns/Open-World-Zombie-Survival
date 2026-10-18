using UnityEditor;
using UnityEngine;
using ZombieSurvival.Player;

[CustomEditor(typeof(PlayerVisual))]
public sealed class PlayerVisualEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Выбери префаб только внешности и нажми «Применить / заменить модель». Капсула, движение и камера находятся на корне игрока.", MessageType.Info);
        if (GUILayout.Button("Применить / заменить модель")) ((PlayerVisual)target).RebuildModel();
    }
}
