using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public sealed class BuildingGeneratorWindow : EditorWindow
    {
        BuildingSeriesProfile profile; int year=1970,floors=5,entrances=4;
        string id="demo-building-001",message=""; GeneratedBuilding current;
        UnityEngine.SceneManagement.Scene preview;
        RenderTexture previewTexture;
        [MenuItem("Tools/Zombie Survival/Модульные дома MVP")]
        static void Open()=>GetWindow<BuildingGeneratorWindow>("Дома MVP");
        void OnDisable(){if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);if(previewTexture)DestroyImmediate(previewTexture);}
        void OnGUI(){
            EditorGUILayout.HelpBox("Демо V5: секция 15×12 м, по два модуля 3 м слева и справа от подъезда, единый World Space фасад. Нажмите «Создать/выбрать демо-профиль» для V5. Старые дома не перезаписываются.",MessageType.Info);
            profile=(BuildingSeriesProfile)EditorGUILayout.ObjectField("Профиль",profile,typeof(BuildingSeriesProfile),false);
            if(GUILayout.Button("Создать/выбрать демо-профиль"))profile=BuildingGenerator.CreateProfile();
            year=EditorGUILayout.IntField("Год",year);floors=EditorGUILayout.IntField("Этажи",floors);entrances=EditorGUILayout.IntField("Подъезды",entrances);id=EditorGUILayout.TextField("Постоянный ID",id);
            GUI.enabled=!EditorApplication.isPlayingOrWillChangePlaymode;
            if(GUILayout.Button("Preview — изолированная сцена"))Run(()=>{
                if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);
                preview=EditorSceneManager.NewPreviewScene();
                current=BuildingGenerator.Build(profile,year,floors,entrances,id,preview);
                var light=new GameObject("PreviewSun").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,preview);
                light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-30,0);
                var camera=new GameObject("PreviewCamera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,preview);
                float center=(entrances-1)*profile.section.sectionWidth/2;
                camera.transform.position=new Vector3(center+entrances*6,floors*3+10,-entrances*9-12);camera.transform.LookAt(new Vector3(center,floors*1.5f,3));
                camera.farClipPlane=250;camera.scene=preview;
                if(!previewTexture)previewTexture=new RenderTexture(800,450,24);
                camera.targetTexture=previewTexture;camera.Render();camera.enabled=false;
                message="Preview создан; при закрытии окна удаляется только эта временная сцена.";
            });
            if(GUILayout.Button("Generate — новый дом (Undo)"))Run(()=>{
                var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if(EditorSceneManager.IsPreviewScene(scene)||PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Откройте отдельную тестовую сцену.");
                current=BuildingGenerator.Build(profile,year,floors,entrances,id,scene);
                Undo.RegisterCreatedObjectUndo(current.gameObject,"Generate building");Selection.activeGameObject=current.gameObject;message="Создан новый дом. Сцена автоматически не сохраняется.";
            });
            current=(GeneratedBuilding)EditorGUILayout.ObjectField("Дом",current,typeof(GeneratedBuilding),true);
            if(GUILayout.Button("Validate"))Run(()=>{var errors=BuildingGenerator.Validate(current);message=errors.Count==0?"PASS: структурные и капсульные проверки. Play и визуальный просмотр отдельно.":string.Join("\n",errors);});
            if(GUILayout.Button("Save Prefab — новый файл"))Run(()=>message=BuildingGenerator.SaveNewPrefab(current,BuildingGenerator.Folder+"/Building.prefab"));
            GUI.enabled=true;EditorGUILayout.HelpBox(message,MessageType.None);
            if(previewTexture)GUILayout.Label(previewTexture,GUILayout.Height(225),GUILayout.ExpandWidth(true));
        }
        void Run(Action action){try{action();}catch(Exception ex){message=ex.Message;Debug.LogException(ex);}}
    }
}
