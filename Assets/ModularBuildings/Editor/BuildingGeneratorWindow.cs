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
        RenderTexture previewTexture;Material exteriorMaterial,baseTrimMaterial;bool includeEntranceDoors=true;Vector2 scroll;
        [MenuItem("Tools/Zombie Survival/Модульные дома MVP")]
        static void Open()=>GetWindow<BuildingGeneratorWindow>("Дома MVP");
        void OnDisable(){if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);if(previewTexture)DestroyImmediate(previewTexture);}
        void OnGUI(){
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Профиль V6: до 16 этажей / 6 подъездов. Лестницы только до 4 этажа, квартиры до 3, балконы до 5 этажей. Для новых правил нажмите «Создать/выбрать профиль V6». Старые дома не перестраиваются.",MessageType.Info);
            profile=(BuildingSeriesProfile)EditorGUILayout.ObjectField("Профиль",profile,typeof(BuildingSeriesProfile),false);
            if(GUILayout.Button("Создать/выбрать профиль V6"))profile=BuildingGenerator.CreateProfile();
            year=EditorGUILayout.IntField("Год",year);floors=EditorGUILayout.IntField("Этажи",floors);entrances=EditorGUILayout.IntField("Подъезды",entrances);id=EditorGUILayout.TextField("Постоянный ID",id);
            exteriorMaterial=(Material)EditorGUILayout.ObjectField(new GUIContent("Материал наружных стен","Пусто — материал профиля. Применяется к стенам всех фасадов, торцам, цоколю и углам."),exteriorMaterial,typeof(Material),false);
            baseTrimMaterial=(Material)EditorGUILayout.ObjectField(new GUIContent("Материал цоколя","Пусто — материал профиля или красно-коричневый WeatheredPlinth. Общая отделка стен цоколь не перекрашивает."),baseTrimMaterial,typeof(Material),false);
            includeEntranceDoors=EditorGUILayout.Toggle("Включая входные двери",includeEntranceDoors);
            EditorGUILayout.HelpBox("Новые дома: по два окна на каждом этаже обоих торцов, отдельный цоколь и одинаковые высокие входные лестницы. Лишние ступени уходят под Terrain, форма не подгоняется по рельефу. Общий материал меняет стены и, по галочке, полотна/коробки входных дверей. Стёкла, оконные рамы, фурнитура и внутренняя отделка сохраняются.",MessageType.Info);
            GUI.enabled=!EditorApplication.isPlayingOrWillChangePlaymode;
            if(GUILayout.Button("Preview — изолированная сцена"))Run(()=>{
                if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);
                preview=EditorSceneManager.NewPreviewScene();
                current=BuildingGenerator.Build(profile,year,floors,entrances,id,preview,exteriorMaterial:exteriorMaterial,includeEntranceDoors:includeEntranceDoors,baseTrimMaterial:baseTrimMaterial);
                var light=new GameObject("PreviewSun").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,preview);
                light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-30,0);
                var camera=new GameObject("PreviewCamera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,preview);
                float center=(entrances-1)*profile.section.sectionWidth/2;
                camera.transform.position=new Vector3(center+entrances*6,floors*3+10,-entrances*9-12);camera.transform.LookAt(new Vector3(center,floors*1.5f,3));
                camera.farClipPlane=250;camera.scene=preview;
                if(!previewTexture)previewTexture=new RenderTexture(800,450,24);
                camera.targetTexture=previewTexture;camera.Render();camera.enabled=false;
                message="Preview создан; при закрытии окна удаляется только эта временная сцена. "+current.GetComponentInChildren<GeneratedEntranceAccess>().diagnostic;
            });
            if(GUILayout.Button("Generate — новый дом (Undo)"))Run(()=>{
                var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if(EditorSceneManager.IsPreviewScene(scene)||PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Откройте отдельную тестовую сцену.");
                current=BuildingGenerator.Build(profile,year,floors,entrances,id,scene,exteriorMaterial:exteriorMaterial,includeEntranceDoors:includeEntranceDoors,baseTrimMaterial:baseTrimMaterial);
                Undo.RegisterCreatedObjectUndo(current.gameObject,"Generate building");Selection.activeGameObject=current.gameObject;message="Создан новый дом. Сцена автоматически не сохраняется. "+current.GetComponentInChildren<GeneratedEntranceAccess>().diagnostic;
            });
            current=(GeneratedBuilding)EditorGUILayout.ObjectField("Дом",current,typeof(GeneratedBuilding),true);
            if(GUILayout.Button("Взять выбранный дом"))Run(()=>{
                current=Selection.activeGameObject?Selection.activeGameObject.GetComponentInParent<GeneratedBuilding>():null;
                if(!current)throw new InvalidOperationException("Выделите корень дома или его дочерний объект.");
                message="Выбран дом "+current.buildingId+". Изменения ещё не применены.";
            });
            if(GUILayout.Button("Применить материал ко всем наружным стенам"))Run(()=>{
                if(!current||!current.profile)throw new InvalidOperationException("Выберите дом в поле «Дом» или кнопкой «Взять выбранный дом».");
                int count=BuildingExteriorMaterials.Apply(current,exteriorMaterial?exteriorMaterial:current.profile.facadeMaterial,includeEntranceDoors);
                SceneView.RepaintAll();message="Материал назначен: "+count+" объектов. Доступен Undo. Геометрия дома не перестраивалась; сохраните сцену Ctrl+S.";
            });
            if(GUILayout.Button("Применить материал цоколя"))Run(()=>{
                BuildingBaseTrim.ApplyMaterial(current,baseTrimMaterial);SceneView.RepaintAll();message="Материал цоколя изменён. Доступен Undo; сохраните сцену Ctrl+S.";
            });
            if(GUILayout.Button("Обновить одинаковые входные лестницы"))Run(()=>{
                var access=BuildingEntranceStairs.Refit(current);message=access.diagnostic;SceneView.RepaintAll();
            });
            if(GUILayout.Button("Validate"))Run(()=>{var errors=BuildingGenerator.Validate(current);message=errors.Count==0?"PASS: структурные и капсульные проверки. Play и визуальный просмотр отдельно.":string.Join("\n",errors);});
            if(GUILayout.Button("Save Prefab — новый файл"))Run(()=>message=BuildingGenerator.SaveNewPrefab(current,BuildingGenerator.Folder+"/Building.prefab"));
            GUI.enabled=true;EditorGUILayout.HelpBox(message,MessageType.None);
            if(previewTexture)GUILayout.Label(previewTexture,GUILayout.Height(225),GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();
        }
        void Run(Action action){try{action();}catch(Exception ex){message=ex.Message;Debug.LogException(ex);}}
    }
}
