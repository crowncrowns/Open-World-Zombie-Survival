using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
namespace ZombieSurvival.ModularBuildings.Editor
{
    [CustomEditor(typeof(BuildingStairBlocker)),CanEditMultipleObjects]
    public sealed class BuildingStairBlockerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root=new VisualElement();root.Add(new PropertyField(serializedObject.FindProperty("replacementPrefab"),"Префаб завала"));
            root.Add(new HelpBox("Заменяется только внешний вид. Коллизия прохода остаётся. Поддерживается выделение нескольких завалов.",HelpBoxMessageType.Info));
            var button=new Button(()=>{serializedObject.ApplyModifiedProperties();foreach(var item in targets){var blocker=(BuildingStairBlocker)item;if(blocker.replacementPrefab)Apply(blocker,true);}}){text="Заменить внешний вид выбранных завалов"};
            button.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorUtility.IsPersistent(target));root.Add(button);
            if(EditorUtility.IsPersistent(target))root.Add(new HelpBox("Для замены откройте Prefab Mode или выберите экземпляр в сцене.",HelpBoxMessageType.Info));return root;
        }
        public static void Apply(BuildingStairBlocker blocker,bool undo)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorUtility.IsPersistent(blocker)||!blocker.gameObject.scene.IsValid()||!blocker.replacementPrefab||!PrefabUtility.IsPartOfPrefabAsset(blocker.replacementPrefab))return;
            if(undo)Undo.RecordObject(blocker,"Заменить модель завала");
            var next=(GameObject)PrefabUtility.InstantiatePrefab(blocker.replacementPrefab,blocker.gameObject.scene);next.transform.SetParent(blocker.transform,false);
            next.transform.localPosition=Vector3.zero;next.transform.localRotation=Quaternion.identity;
            // The model is visual-only; its authored collider must not block the incoming flight.
            foreach(var collider in next.GetComponentsInChildren<Collider>(true)){collider.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
            if(undo)Undo.RegisterCreatedObjectUndo(next,"Модель завала");
            if(blocker.generatedVisual){if(undo)Undo.DestroyObjectImmediate(blocker.generatedVisual);else Object.DestroyImmediate(blocker.generatedVisual);}
            blocker.generatedVisual=next;EditorUtility.SetDirty(blocker);PrefabUtility.RecordPrefabInstancePropertyModifications(blocker);
            if(undo)EditorSceneManager.MarkSceneDirty(blocker.gameObject.scene);
        }
    }
}
