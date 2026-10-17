using UnityEditor;
using UnityEngine;
using ZombieSurvival.Player;

[CustomEditor(typeof(PlayerVisual))]
public sealed class PlayerVisualEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Choose a visual-only Model Prefab, then Apply / Replace Model. Capsule, movement and camera remain on the player root.", MessageType.Info);
        if (GUILayout.Button("Apply / Replace Model")) ((PlayerVisual)target).RebuildModel();
    }
}
