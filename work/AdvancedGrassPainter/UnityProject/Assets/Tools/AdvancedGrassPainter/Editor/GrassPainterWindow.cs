using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AdvancedGrassPainter.Editor
{
    public sealed class GrassPainterWindow : EditorWindow
    {
        const string Root="Assets/Tools/AdvancedGrassPainter";
        [SerializeField] GrassSettings settings=new GrassSettings();
        [SerializeField] GrassDataStorage layer;
        [SerializeField] GrassPreset preset;
        [SerializeField] GrassBrushMode mode;
        [SerializeField] bool painting, hints=true, autoCleanup;
        int tab,undoGroup=-1;
        double lastAuto;
        Vector2 scroll;
        Vector3 lastDab;
        bool hasLast;
        readonly Vector3[] hintPoints=new Vector3[37];
        readonly Color[] hintColors=new Color[37];
        double nextHints;
        GrassCollisionManager collisions;
        GrassGenerationSystem generation;
        readonly string[] tabs={"Brush Settings","Grass Types","Randomization","Collision Avoidance","Wind Settings","Rendering & Optimization","Presets","Debug"};
        [MenuItem("Tools/Environment/Advanced Grass Painter")]
        public static void Open()=>GetWindow<GrassPainterWindow>("Advanced Grass Painter");
        public static void OpenFor(GrassDataStorage data){var window=GetWindow<GrassPainterWindow>("Advanced Grass Painter");window.layer=data;}
        void OnEnable(){minSize=new Vector2(460,620);SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=UndoRedo;EditorApplication.update+=UpdateObstacles;}
        void OnDisable(){SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=UndoRedo;EditorApplication.update-=UpdateObstacles;EndStroke();collisions?.Dispose();collisions=null;generation=null;}
        void Ensure(){if(collisions==null){collisions=new GrassCollisionManager();generation=new GrassGenerationSystem(collisions);collisions.Refresh();}}
        void UndoRedo(){if(layer)layer.Changed();SceneView.RepaintAll();Repaint();}
        void UpdateObstacles()
        {
            if(!autoCleanup||!layer||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.timeSinceStartup-lastAuto<1)return;
            lastAuto=EditorApplication.timeSinceStartup;Ensure();var changed=new System.Collections.Generic.List<Bounds>(collisions.Refresh());if(changed.Count==0)return;
            Undo.RegisterCompleteObjectUndo(layer,"Grass obstacle movement");
            foreach(var bounds in changed){var b=bounds;b.Expand(6);generation.Cleanup(layer,settings,b);}
            EditorUtility.SetDirty(layer);EditorSceneManager.MarkSceneDirty(layer.gameObject.scene);SceneView.RepaintAll();
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("Advanced Grass Painter",EditorStyles.boldLabel);
            layer=(GrassDataStorage)EditorGUILayout.ObjectField("Активный слой",layer,typeof(GrassDataStorage),true);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Новый независимый слой"))
                {
                    var go=new GameObject("Grass Layer");Undo.RegisterCreatedObjectUndo(go,"New grass layer");
                    layer=Undo.AddComponent<GrassDataStorage>(go);Undo.AddComponent<GrassWindController>(go);
                    bool hd=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline&&UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("HDRenderPipeline");
                    layer.material=hd?GrassHDRPInstaller.Install():AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Grass_URP.mat");Selection.activeGameObject=go;
                }
                painting=GUILayout.Toggle(painting,"Кисть в Scene View","Button");
            }
            if(EditorApplication.isPlaying)EditorGUILayout.HelpBox("Авторинг доступен после остановки Play.",MessageType.Info);
            tab=GUILayout.SelectionGrid(tab,tabs,2);scroll=EditorGUILayout.BeginScrollView(scroll);
            switch(tab)
            {
                case 0:
                    mode=(GrassBrushMode)EditorGUILayout.EnumPopup("Режим",mode);
                    settings.radius=EditorGUILayout.Slider("Радиус, м",settings.radius,.1f,100);
                    settings.density=EditorGUILayout.Slider("Плотность",settings.density,0,1);
                    settings.spacing=EditorGUILayout.Slider("Шаг Poisson, м",settings.spacing,.04f,2);
                    settings.edgeFalloff=EditorGUILayout.Slider("Мягкая граница",settings.edgeFalloff,0,1);
                    settings.growth=EditorGUILayout.Slider("Активность роста",settings.growth,0,2);
                    EditorGUILayout.HelpBox("ЛКМ — мазок; Shift — стирание; Alt — навигация. Fill: один щелчок заполняет круг. Ниже можно заполнить Bounds выделения. Стирание формирует дорожки. Smooth удаляет скученность и заполняет пробелы.",MessageType.Info);
                    using(new EditorGUI.DisabledScope(!layer||Selection.gameObjects.Length==0))if(GUILayout.Button("Fill по Bounds выделенных объектов"))FillSelection();
                    break;
                case 1:
                    settings.kind=(GrassKind)EditorGUILayout.EnumPopup("Растительность",settings.kind);
                    settings.replaceFrom=(GrassKind)EditorGUILayout.EnumPopup("Заменять тип",settings.replaceFrom);
                    settings.height=EditorGUILayout.Slider("Высота, м",settings.height,.02f,2);
                    settings.width=EditorGUILayout.Slider("Ширина, м",settings.width,.005f,.3f);
                    settings.randomScale=EditorGUILayout.Slider("Разброс размера",settings.randomScale,0,1);
                    settings.randomRotation=EditorGUILayout.Slider("Разброс поворота, °",settings.randomRotation,0,360);
                    settings.rootColor=EditorGUILayout.ColorField("Основание",settings.rootColor);settings.tipColor=EditorGUILayout.ColorField("Верхушка",settings.tipColor);settings.dryColor=EditorGUILayout.ColorField("Сухая трава",settings.dryColor);
                    break;
                case 2:
                    settings.globalSeed=EditorGUILayout.IntField("Global Seed",settings.globalSeed);settings.brushSeed=EditorGUILayout.IntField("Brush Seed",settings.brushSeed);
                    settings.lockSeed=EditorGUILayout.Toggle("Lock Seed",settings.lockSeed);
                    settings.clumping=EditorGUILayout.Slider("Скопления",settings.clumping,0,1);settings.noiseScale=EditorGUILayout.Slider("Масштаб шума, м",settings.noiseScale,.1f,30);
                    using(new EditorGUI.DisabledScope(!layer))if(GUILayout.Button("Перегенерировать слой"))Regenerate();
                    using(new EditorGUI.DisabledScope(!layer||Selection.gameObjects.Length==0))if(GUILayout.Button("Перегенерировать зону выделения")){var bounds=SelectionBounds();if(bounds.HasValue){Ensure();generation.Regenerate(layer,settings,bounds);}else ShowNotification(new GUIContent("У выделения нет Renderer для определения области"));}
                    using(new EditorGUI.DisabledScope(!layer||settings.lockSeed))if(GUILayout.Button("Regenerate with New Seed")){settings.globalSeed=unchecked((int)DateTime.UtcNow.Ticks);Regenerate();}
                    EditorGUILayout.HelpBox("Перегенерация применяет текущие параметры к сохранённым мазкам, сохраняя их радиусы/Bounds. Lock Seed сохраняет seed каждого мазка. После Erase/Replace/Cleanup история сбрасывается: обновляются только оставшиеся пучки; вид меняется через Replace.",MessageType.Info);
                    break;
                case 3:
                    settings.surfaceLayers=Mask("Слои поверхностей",settings.surfaceLayers);settings.obstacleLayers=Mask("Слои препятствий",settings.obstacleLayers);
                    settings.clearance=EditorGUILayout.Slider("Зазор, м",settings.clearance,0,2);settings.obstacleFade=EditorGUILayout.Slider("Переход плотности, м",settings.obstacleFade,.01f,3);
                    settings.maxSlope=EditorGUILayout.Slider("Максимальный уклон, °",settings.maxSlope,0,85);settings.projection=EditorGUILayout.Slider("Поиск земли ±, м",settings.projection,.1f,20);
                    settings.overhead=EditorGUILayout.Slider("Проверка перекрытий, м",settings.overhead,0,100);
                    autoCleanup=EditorGUILayout.Toggle("Очистка после перемещения",autoCleanup);
                    using(new EditorGUI.DisabledScope(!layer))if(GUILayout.Button("Recalculate Collisions — весь слой")){Ensure();collisions.Refresh(true);Undo.RegisterCompleteObjectUndo(layer,"Recalculate grass collisions");generation.Cleanup(layer,settings);EditorUtility.SetDirty(layer);EditorSceneManager.MarkSceneDirty(layer.gameObject.scene);}
                    if(GUILayout.Button("Перестроить collision proxy cache")){Ensure();collisions.Refresh(true);}
                    EditorGUILayout.HelpBox("Кэш использует точные MeshCollider proxy и пространственный hash. GrassObstacle исключает объекты или добавляет зазор. Динамическая очистка работает при открытом окне, раз в секунду, только в изменённых Bounds. Пересекающиеся пучки удаляются; пустоты восстанавливаются кистью.",MessageType.Info);
                    break;
                case 4:
                    settings.windEnvelope=EditorGUILayout.Slider("Резерв ветра, м",settings.windEnvelope,0,.5f);
                    if(layer){var wind=layer.GetComponent<GrassWindController>();if(wind){var so=new SerializedObject(wind);so.Update();var p=so.GetIterator();bool enter=true;while(p.NextVisible(enter)){enter=false;if(p.name!="m_Script")EditorGUILayout.PropertyField(p,true);}so.ApplyModifiedProperties();}}
                    EditorGUILayout.HelpBox("Фактический ветер ограничен резервом каждого пучка. Для изменения резерва используйте перегенерацию: геометрия проверится повторно.",MessageType.Info);
                    break;
                case 5:
                    if(layer){var so=new SerializedObject(layer);so.Update();foreach(var name in new[]{"material","chunkSize","drawDistance","quality","castShadows"})EditorGUILayout.PropertyField(so.FindProperty(name));if(so.ApplyModifiedProperties())layer.Changed();}
                    EditorGUILayout.HelpBox("Уникальная геометрия объединяется в чанки. По одному RenderMesh на видимый чанк: LOD 6/3/2 сегмента и детерминированное прореживание. Счётчик ниже — подачи для последней камеры, без дополнительных shadow/depth passes.",MessageType.Info);
                    using(new EditorGUI.DisabledScope(!GrassHDRPInstaller.Available))if(GUILayout.Button("Установить HDRP Shader Graph и назначить слою")){var material=GrassHDRPInstaller.Install();if(layer){Undo.RecordObject(layer,"Assign HDRP grass material");layer.material=material;EditorUtility.SetDirty(layer);}}
                    break;
                case 6:
                    preset=(GrassPreset)EditorGUILayout.ObjectField("Пресет",preset,typeof(GrassPreset),false);
                    if(GUILayout.Button("Загрузить пресет")&&preset)settings=preset.settings.Copy();
                    if(GUILayout.Button("Сохранить новый пресет")){string path=EditorUtility.SaveFilePanelInProject("Grass preset","GrassPreset","asset","Сохранение параметров",Root+"/Presets");if(!string.IsNullOrEmpty(path)){var asset=CreateInstance<GrassPreset>();asset.settings=settings.Copy();AssetDatabase.CreateAsset(asset,path);preset=asset;}}
                    if(GUILayout.Button("Экспорт настроек JSON")){string path=EditorUtility.SaveFilePanel("Экспорт настроек","","GrassSettings.json","json");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,JsonUtility.ToJson(settings,true));}
                    if(GUILayout.Button("Импорт настроек JSON")){string path=EditorUtility.OpenFilePanel("Импорт настроек","","json");if(!string.IsNullOrEmpty(path)){try{var imported=JsonUtility.FromJson<GrassSettings>(File.ReadAllText(path));if(imported==null)throw new FormatException("Пустые настройки");imported.Validate();settings=imported;}catch(Exception ex){Debug.LogError("Не удалось импортировать настройки травы: "+ex.Message);}}}
                    break;
                case 7:
                    hints=EditorGUILayout.Toggle("Подсказки Scene View",hints);
                    if(layer){EditorGUILayout.LabelField("Пучков / травинок",layer.plants.Count+" / "+BladeTotal());EditorGUILayout.LabelField("Чанков / подач",(layer.rendering?.manager.chunks.Count??0)+" / "+(layer.rendering?.submittedDraws??0));EditorGUILayout.LabelField("Данные и меши, МБ",((layer.rendering?.manager.bytes??0)/1048576f).ToString("F2"));EditorGUILayout.LabelField("Последняя генерация, мс",layer.generationMilliseconds.ToString("F1"));EditorGUILayout.LabelField("Отклонено препятствиями",layer.rejectedObstacles.ToString());}
                    if(collisions!=null)EditorGUILayout.LabelField("Collision proxies",collisions.Count.ToString());
                    break;
            }
            EditorGUILayout.EndScrollView();
        }
        int BladeTotal(){int total=0;foreach(var p in layer.plants)total+=GrassMeshGenerator.BladeCount(p);return total;}
        static LayerMask Mask(string label,LayerMask mask){var names=new string[32];for(int i=0;i<32;i++){string name=LayerMask.LayerToName(i);names[i]=string.IsNullOrEmpty(name)?"Layer "+i:name;}return EditorGUILayout.MaskField(label,mask,names);}
        void Regenerate(){if(!layer)return;Ensure();generation.Regenerate(layer,settings);EditorSceneManager.MarkSceneDirty(layer.gameObject.scene);SceneView.RepaintAll();}
        static Bounds? SelectionBounds(){Bounds? result=null;foreach(var go in Selection.gameObjects)foreach(var r in go.GetComponentsInChildren<Renderer>()){if(result.HasValue){var b=result.Value;b.Encapsulate(r.bounds);result=b;}else result=r.bounds;}return result;}
        void FillSelection(){var b=SelectionBounds();if(!b.HasValue){ShowNotification(new GUIContent("Выберите объекты с Renderer для области Fill"));return;}if(!layer)return;Ensure();var s=settings.Copy();s.radius=Mathf.Clamp(new Vector2(b.Value.extents.x,b.Value.extents.z).magnitude,.1f,100);generation.Apply(layer,b.Value.center,s,GrassBrushMode.Fill,b);SceneView.RepaintAll();}
        void EndStroke(){if(undoGroup>=0)Undo.CollapseUndoOperations(undoGroup);undoGroup=-1;hasLast=false;}
        void SceneGUI(SceneView view)
        {
            if(!painting||!layer||EditorApplication.isPlayingOrWillChangePlaymode||PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            var e=Event.current;if(e.alt)return;Ensure();
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if(e.type==EventType.MouseUp&&e.button==0){EndStroke();e.Use();return;}
            Ray ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            // Scene handles raycasting works for mesh-only surfaces; generation projects into the independent cache.
            if(!collisions.RaycastSurface(ray,settings,out var hit))return;
            if(hints&&e.type==EventType.Repaint)
            {
                bool valid=Vector3.Angle(Vector3.up,hit.normal)<=settings.maxSlope;var sample=GrassGenerationSystem.CreatePlant(hit.point,hit.normal,settings,1);
                bool clear=collisions.Project(hit.point,settings,out var surface)&&collisions.Accept(sample,surface,settings);
                float weight=clear?collisions.EdgeWeight(hit.point,surface,settings):0;
                Handles.color=!valid||!clear?new Color(1,.18f,.12f,.85f):weight<.8f?new Color(1,.75f,.1f,.85f):new Color(.2f,1,.3f,.85f);
                Handles.DrawWireDisc(hit.point,hit.normal,settings.radius,2);Handles.color=new Color(1,1,1,.25f);Handles.DrawWireDisc(hit.point,hit.normal,settings.radius*(1-settings.edgeFalloff));
                DrawPreviewPoints(hit.point);
            }
            if((e.type==EventType.MouseDown||e.type==EventType.MouseDrag)&&e.button==0)
            {
                bool single=mode==GrassBrushMode.Fill;
                if(!hasLast||!single&&Vector3.Distance(lastDab,hit.point)>=Mathf.Max(.08f,settings.radius*.16f))
                {
                    if(undoGroup<0){Undo.IncrementCurrentGroup();undoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Grass brush stroke");}
                    generation.Apply(layer,hit.point,settings,e.shift?GrassBrushMode.Erase:mode);lastDab=hit.point;hasLast=true;Repaint();
                }
                e.Use();
            }
            view.Repaint();
        }
        void DrawPreviewPoints(Vector3 center)
        {
            if(EditorApplication.timeSinceStartup>=nextHints)
            {
                nextHints=EditorApplication.timeSinceStartup+.2;
                for(int i=0;i<hintPoints.Length;i++)
                {
                    float angle=i*2.39996323f;float radius=settings.radius*Mathf.Sqrt(i/(float)(hintPoints.Length-1));
                    Vector3 point=center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                    bool projected=collisions.Project(point,settings,out var surface);hintPoints[i]=projected?surface.point+surface.normal*.015f:point;
                    var plant=GrassGenerationSystem.CreatePlant(projected?surface.point:point,projected?surface.normal:Vector3.up,settings,GrassRandomizationSystem.Hash(settings.globalSeed,i));
                    bool allowed=projected&&collisions.Accept(plant,surface,settings);float edge=allowed?collisions.EdgeWeight(surface.point,surface,settings):0;
                    hintColors[i]=!allowed?new Color(1,.15f,.1f,.8f):edge<.8f?new Color(1,.75f,.1f,.8f):new Color(.2f,1,.3f,.8f);
                }
            }
            for(int i=0;i<hintPoints.Length;i++){Handles.color=hintColors[i];Handles.DotHandleCap(0,hintPoints[i],Quaternion.identity,HandleUtility.GetHandleSize(hintPoints[i])*.035f,EventType.Repaint);}
        }
    }
}
