using UnityEditor;
using UnityEngine;

namespace AdvancedGrassPainter.Editor
{
    [CustomEditor(typeof(GrassDataStorage))]
    public sealed class GrassDataStorageEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var data=(GrassDataStorage)target;
            EditorGUILayout.LabelField("Редактируемый слой травы",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Пучков",data.plants.Count.ToString());
            EditorGUILayout.HelpBox("Пучки сохраняются в мировых координатах. Перемещение этого служебного объекта не перемещает поле; изменения участка выполняйте кистью.",MessageType.Info);
            serializedObject.Update();foreach(var name in new[]{"material","chunkSize","drawDistance","quality","castShadows"})EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            if(serializedObject.ApplyModifiedProperties())data.Changed();
            if(GUILayout.Button("Открыть кисть для этого слоя"))GrassPainterWindow.OpenFor(data);
        }
    }
}
