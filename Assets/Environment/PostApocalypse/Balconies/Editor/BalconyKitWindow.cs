using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PostApocalypse.Balconies;

namespace PostApocalypse.Balconies.Editor
{
    public sealed class BalconyKitWindow : EditorWindow
    {
        readonly string[] types = {
            "Balcony_01_Soviet", "Balcony_02_Khrushchevka", "Balcony_03_GlazedSoviet",
            "Balcony_04_ModernLoggia", "Balcony_05_Wooden", "Balcony_06_Fortified",
            "Balcony_07_Industrial", "Balcony_08_Overgrown", "Balcony_09_Burned", "Balcony_10_Collapsed"
        };
        readonly string[] states = {"A — Weathered","B — Abandoned","C — Damaged","D — Ruined"};
        int typeIndex;
        int stateIndex;
        int seed=2401024;
        bool snapToGrid=true;
        Vector3 facadeOffset=Vector3.zero;
        Vector3 doorAnchorOffset=Vector3.zero;

        [MenuItem("Tools/Zombie Survival/Post-Apocalypse Balconies/Place and Vary")]
        static void Open() => GetWindow<BalconyKitWindow>("Balconies");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Post-Apocalyptic Balcony Kit",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Выберите Transform стены в начале фасадной ячейки: центр нижней кромки, фасад смотрит по +Z. Смещение фасада учитывает толщину стены; смещение двери корректирует DoorAnchor, ThresholdAnchor и границы проёма.",MessageType.Info);
            typeIndex=EditorGUILayout.Popup("Тип конструкции",typeIndex,types);
            stateIndex=EditorGUILayout.Popup("Состояние",stateIndex,states);
            seed=EditorGUILayout.IntField("Random Seed",seed);
            snapToGrid=EditorGUILayout.Toggle("Привязка 3×3 м",snapToGrid);
            facadeOffset=EditorGUILayout.Vector3Field("Смещение фасада, м",facadeOffset);
            doorAnchorOffset=EditorGUILayout.Vector3Field("Смещение двери, м",doorAnchorOffset);
            EditorGUILayout.Space(6);
            using(new EditorGUI.DisabledScope(Selection.activeTransform==null))
            {
                if(GUILayout.Button("Разместить на выбранной стене",GUILayout.Height(30)))Place();
            }
            if(GUILayout.Button("Выбрать состояние по Seed"))RandomizeState();
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Собрать комплект"))BalconyKitBuilder.BuildPackage();
                if(GUILayout.Button("Проверить комплект"))BalconyKitValidation.Run();
            }
            if(GUILayout.Button("Открыть сцену-витрину"))
            {
                EditorSceneManager.OpenScene(BalconyKitBuilder.DemoScene);
            }
        }

        void Place()
        {
            var wall=Selection.activeTransform;
            string state=((char)('A'+stateIndex)).ToString();
            string name=types[typeIndex]+"_"+state;
            string path=BalconyKitBuilder.Prefabs+"/"+types[typeIndex]+"/"+name+".prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null){EditorUtility.DisplayDialog("Балконы","Сначала соберите пакет и проверьте, что выбранная модель существует.","OK");return;}
            Vector3 position=wall.position;
            Quaternion rotation=wall.rotation;
            if(snapToGrid)
            {
                Vector3 local=Quaternion.Inverse(rotation)*position;
                local.x=Mathf.Round(local.x/3f)*3f;local.y=Mathf.Round(local.y/3f)*3f;
                position=rotation*local;
            }
            position+=rotation*facadeOffset;
            var instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Undo.RegisterCreatedObjectUndo(instance,"Разместить балкон");
            instance.transform.SetPositionAndRotation(position,rotation);
            instance.GetComponent<BalconyKitModule>()?.ApplySeed(seed);
            var mounts=instance.transform.Find("MountPoints");
            if(mounts!=null)
            {
                foreach(string mountName in new[]{"DoorAnchor","ThresholdAnchor","DoorClearance_Left","DoorClearance_Right"})
                {
                    var mount=mounts.Find(mountName);
                    if(mount!=null)mount.localPosition+=doorAnchorOffset;
                }
            }
            Selection.activeGameObject=instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
        }

        void RandomizeState()
        {
            var random=new System.Random(seed);
            stateIndex=random.Next(states.Length);
            Repaint();
        }
    }
}
