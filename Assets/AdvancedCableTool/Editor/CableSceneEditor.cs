using UnityEditor;
using UnityEngine;

namespace AdvancedCableTool.Editor
{
    [CustomEditor(typeof(CableGenerator))]
    public sealed class CableSceneEditor : UnityEditor.Editor
    {
        string placementWarning;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck(); DrawPropertiesExcluding(serializedObject, "m_Script");
            if (EditorGUI.EndChangeCheck())
            {
                var c = (CableGenerator)target; var before = CableEditing.Capture(c);
                serializedObject.ApplyModifiedProperties(); CableEditing.Commit(c, before, out placementWarning);
                serializedObject.Update();
            }
            else serializedObject.ApplyModifiedProperties();
            CableGenerator cable = (CableGenerator)target;
            EditorGUILayout.LabelField("Измеренная длина", cable.MeasuredLength.ToString("F3") + " м");
            if (!string.IsNullOrEmpty(cable.Status)) EditorGUILayout.HelpBox(cable.Status, MessageType.Warning);
            if (!string.IsNullOrEmpty(placementWarning)) EditorGUILayout.HelpBox(placementWarning, MessageType.Warning);
            if (GUILayout.Button("Detach End B / Освободить конец B"))
            { Undo.RecordObject(cable, "Detach cable B"); cable.CommitShape(); cable.DetachEndB(); cable.Rebuild(); EditorUtility.SetDirty(cable); }
            if (GUILayout.Button("Отсоединить оба конца"))
            { Undo.RecordObject(cable, "Detach cable"); cable.CommitShape(); cable.DetachAll(); cable.Rebuild(); EditorUtility.SetDirty(cable); }
            if (GUILayout.Button("Открыть Cable Tool")) CableToolWindow.Open();
        }
        void OnSceneGUI()
        {
            CableGenerator cable = (CableGenerator)target;
            for (int i = 0; i < cable.anchors.Count; ++i)
            {
                CableAnchor a = cable.anchors[i]; if (!a.attached) continue;
                Vector3 p = a.Resolve(); Handles.Label(p + Vector3.up * .12f, "Опора " + (i + 1));
                EditorGUI.BeginChangeCheck(); Vector3 moved = Handles.PositionHandle(p, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    var before = CableEditing.Capture(cable); Undo.RecordObject(cable, "Move cable anchor"); a.Attach(moved, a.target);
                    if (!CableEditing.Commit(cable, before, out placementWarning)) SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(placementWarning));
                }
            }
        }
    }
}
