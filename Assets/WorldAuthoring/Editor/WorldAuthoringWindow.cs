using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using ZombieSurvival.ModularBuildings;

namespace ZombieSurvival.WorldAuthoring.Editor
{
    public sealed class WorldAuthoringWindow : EditorWindow
    {
        const string StylePath="Assets/WorldAuthoring/Editor/WorldAuthoring.uss";
        [SerializeField] CityBlockPlan plan;
        [SerializeField] int selected;
        CityLotPreview preview;
        RenderTexture texture;
        Image image;
        Label message;
        ScrollView content;
        SerializedObject data;
        bool drawLots=true;
        string previewSourceStamp;
        double nextPreviewCheck;
        [SerializeField] string lastMessage="Выберите участок и нажмите «Проверить план» или «Предпросмотр».";

        string SourceStamp()
        {
            if(!plan||!plan.catalog)return "";
            var entry=plan.lots!=null&&selected>=0&&selected<plan.lots.Count
                ?plan.catalog.entries?.FirstOrDefault(e=>e!=null&&e.key==plan.lots[selected]?.entryKey):null;
            return EditorJsonUtility.ToJson(plan)+EditorJsonUtility.ToJson(plan.catalog)
                +(entry?.profile?EditorJsonUtility.ToJson(entry.profile):"")
                +(entry?.prefab?AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(entry.prefab)).ToString():"");
        }
        void Update()
        {
            if(preview==null||EditorApplication.timeSinceStartup<nextPreviewCheck)return;
            nextPreviewCheck=EditorApplication.timeSinceStartup+.5;
            if(SourceStamp()==previewSourceStamp)return;
            ClosePreview();SetMessage("Источники изменились. Нажмите предпросмотр снова, чтобы увидеть актуальный результат.");
        }

        [MenuItem("Tools/Zombie Survival/Город — планировка квартала")]
        public static void Open()=>GetWindow<WorldAuthoringWindow>("Планировка города");
        public void SetPlan(CityBlockPlan value){ClosePreview();plan=value;selected=0;CreateGUI();}
        void OnEnable(){SceneView.duringSceneGui+=DrawLots;Undo.undoRedoPerformed+=OnUndo;EditorApplication.playModeStateChanged+=OnPlayState;}
        void OnDisable(){SceneView.duringSceneGui-=DrawLots;Undo.undoRedoPerformed-=OnUndo;EditorApplication.playModeStateChanged-=OnPlayState;ClosePreview();SceneView.RepaintAll();}
        void OnUndo(){ClosePreview();BuildContent();SceneView.RepaintAll();}
        void OnPlayState(PlayModeStateChange state){ClosePreview();BuildContent();}

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var sheet=AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);if(sheet)rootVisualElement.styleSheets.Add(sheet);
            rootVisualElement.AddToClassList("city-window");
            var title=new Label("Планировка квартала");title.AddToClassList("city-title");rootVisualElement.Add(title);
            rootVisualElement.Add(new HelpBox("План хранится отдельным asset. Рамки в Scene не меняют дома, дороги или Terrain. Здание появляется только по кнопке «Создать выбранный дом»; существующие дома не заменяются.",HelpBoxMessageType.Info));
            var selector=new ObjectField("План квартала"){objectType=typeof(CityBlockPlan),allowSceneObjects=false};selector.SetValueWithoutNotify(plan);
            selector.RegisterValueChangedCallback(e=>{ClosePreview();plan=e.newValue as CityBlockPlan;selected=0;BuildContent();SceneView.RepaintAll();});rootVisualElement.Add(selector);
            var row=new VisualElement();row.AddToClassList("city-row");
            row.Add(new Button(CreateCatalog){text="Новый каталог"});row.Add(new Button(CreatePlan){text="Новый план"});rootVisualElement.Add(row);
            var report=new ScrollView();report.AddToClassList("city-status");
            message=new Label(lastMessage);message.AddToClassList("city-report");report.Add(message);rootVisualElement.Add(report);
            content=new ScrollView();content.style.flexGrow=1;rootVisualElement.Add(content);BuildContent();
        }
        void BuildContent()
        {
            if(content==null)return;content.Unbind();content.Clear();data=null;
            if(!plan){content.Add(new HelpBox("Создайте каталог типов зданий, затем пустой план квартала, или выберите существующий план.",HelpBoxMessageType.Info));return;}
            data=new SerializedObject(plan);
            var edit=new VisualElement();content.Add(edit);
            edit.Add(new PropertyField(data.FindProperty("planId"),"Постоянный ID плана"));
            edit.Add(new PropertyField(data.FindProperty("catalog"),"Каталог зданий"));
            edit.Bind(data);
            edit.RegisterCallback<SerializedPropertyChangeEvent>(_=>{ClosePreview();SceneView.RepaintAll();});
            content.Add(new Button(()=>{Selection.activeObject=plan.catalog;EditorGUIUtility.PingObject(plan.catalog);}){text="Редактировать каталог в Inspector"});
            var draw=new Toggle("Показывать участки в Scene"){value=drawLots};draw.RegisterValueChangedCallback(e=>{drawLots=e.newValue;SceneView.RepaintAll();});content.Add(draw);
            var buttons=new VisualElement();buttons.AddToClassList("city-row");
            buttons.Add(new Button(AddLot){text="Добавить участок"});
            buttons.Add(new Button(DuplicateLot){text="Копировать участок с новым ID"});
            buttons.Add(new Button(RemoveLot){text="Удалить запись участка"});content.Add(buttons);
            if(plan.lots==null||plan.lots.Count==0){content.Add(new HelpBox("План пуст. Добавление участка создаёт только запись в плане, без объекта в сцене.",HelpBoxMessageType.Info));return;}
            selected=Mathf.Clamp(selected,0,plan.lots.Count-1);
            var choices=plan.lots.Select((l,i)=>$"{i+1}. {(l==null?"Пустая запись":l.title)}").ToList();
            var pick=new PopupField<string>("Выбранный участок",choices,selected);pick.RegisterValueChangedCallback(e=>{ClosePreview();selected=choices.IndexOf(e.newValue);BuildContent();SceneView.RepaintAll();});content.Add(pick);
            if(plan.lots[selected]==null)return;
            var lot=plan.lots[selected];
            var entries=plan.catalog?.entries?.Where(e=>e!=null&&!string.IsNullOrEmpty(e.key)).ToList()??new List<CityBuildingEntry>();
            if(entries.Count>0)
            {
                var labels=entries.Select(e=>$"{e.title} [{e.key}]").ToList();int current=entries.FindIndex(e=>e.key==lot.entryKey);
                if(current<0)labels.Insert(0,"Выберите тип здания");
                var kinds=new PopupField<string>("Тип здания",labels,current<0?0:current);
                kinds.RegisterValueChangedCallback(e=>{int i=labels.IndexOf(e.newValue)-(current<0?1:0);if(i<0)return;Undo.RecordObject(plan,"Тип здания участка");lot.entryKey=entries[i].key;EditorUtility.SetDirty(plan);ClosePreview();BuildContent();SceneView.RepaintAll();});content.Add(kinds);
            }
            var fields=new VisualElement();fields.AddToClassList("city-form");content.Add(fields);
            var element=data.FindProperty("lots").GetArrayElementAtIndex(selected);
            void Field(string name,string label)=>fields.Add(new PropertyField(element.FindPropertyRelative(name),label));
            Field("id","Постоянный ID участка");Field("title","Название / адрес");Field("enabled","Включён");Field("entryKey","Ключ типа в каталоге");
            Field("position","Центр участка, мировые X/Y/Z");Field("yaw","Поворот вокруг Y, °");Field("size","Размер участка X/Z, м");
            Field("year","Год постройки (метаданные)");Field("floors","Этажи (для профиля)");Field("entrances","Подъезды (для профиля)");
            fields.Add(new HelpBox("Новый профиль V6: 1–16 этажей и 1–6 подъездов. Старые V4/V5 сохраняют прежние ограничения 1–9 / 1–8. Для новых квартир, балконов, кровли и водостоков выберите тип V6.",HelpBoxMessageType.Info));
            fields.Bind(data);fields.RegisterCallback<SerializedPropertyChangeEvent>(_=>{ClosePreview();SceneView.RepaintAll();});
            var placement=new VisualElement();placement.AddToClassList("city-row");
            placement.Add(new Button(()=>Run(()=>{var view=SceneView.lastActiveSceneView;if(view)view.LookAt(lot.position,view.rotation,Mathf.Max(lot.size.x,lot.size.y));})){text="Показать участок"});
            placement.Add(new Button(()=>Run(()=>{
                var t=Terrain.activeTerrains.FirstOrDefault(t=>{var p=t.transform.position;var s=t.terrainData.size;return lot.position.x>=p.x&&lot.position.x<=p.x+s.x&&lot.position.z>=p.z&&lot.position.z<=p.z+s.z;});
                if(!t)throw new InvalidOperationException("Под центром участка не найден активный Terrain. Укажите Y вручную.");
                Undo.RecordObject(plan,"Высота центра участка");lot.position.y=t.SampleHeight(lot.position)+t.transform.position.y;EditorUtility.SetDirty(plan);ClosePreview();BuildContent();SceneView.RepaintAll();
            })){text="Взять высоту Terrain в центре"});content.Add(placement);
            content.Add(new HelpBox("В Scene выбранный участок можно передвигать ручкой. Y — уровень пола первого этажа, а не автоматическое выравнивание всего дома. Размер участка должен вмещать лестницы и свесы. Проверка не заменяет игровой проход и не учитывает все деревья, заборы или дорожное полотно.",HelpBoxMessageType.Info));
            var actions=new VisualElement();actions.AddToClassList("city-row");
            actions.Add(new Button(CheckPlan){text="Проверить план"});
            actions.Add(new Button(ShowPreview){text="Предпросмотр выбранного дома"});
            actions.Add(new Button(()=>Run(()=>{
                ClosePreview();var root=CityBlockBuilder.GenerateSelected(plan,selected,UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Selection.activeGameObject=root;SetMessage("Создан только выбранный дом. Ctrl+Z отменяет создание. Сцена не сохранена автоматически.");SceneView.RepaintAll();
            })){text="Создать выбранный дом (Undo)"});content.Add(actions);
            actions.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode&&PrefabStageUtility.GetCurrentPrefabStage()==null);
            image=new Image();image.AddToClassList("city-preview");image.style.display=DisplayStyle.None;image.scaleMode=ScaleMode.ScaleToFit;content.Add(image);
            content.Add(new Button(ClosePreview){text="Закрыть предпросмотр"});
            content.Add(new HelpBox("Профиль использует существующий модульный генератор. Его предпросмотр может добавить новые производные меши в штатный кэш; исходные префабы и готовые здания не перезаписываются. Неподдерживаемые серии не генерируются.",HelpBoxMessageType.Info));
        }
        void SetMessage(string text){lastMessage=text;if(message!=null)message.text=text;Repaint();}
        void Run(Action action){try{if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Остановите Play перед авторингом.");action();}catch(Exception e){SetMessage(e.Message);}}
        void CheckPlan()=>Run(()=>{var errors=CityBlockBuilder.Validate(plan,UnityEngine.SceneManagement.SceneManager.GetActiveScene());SetMessage(errors.Count==0?"План прошёл проверку. Создание домов выполняется отдельно.":string.Join("\n",errors));});
        void AddLot()=>Run(()=>{
            if(!plan)throw new InvalidOperationException("Сначала выберите план.");Undo.RecordObject(plan,"Добавить участок");
            var center=SceneView.lastActiveSceneView?SceneView.lastActiveSceneView.pivot:Vector3.zero;
            plan.lots??=new List<CityLot>();plan.lots.Add(new CityLot{title="Участок "+(plan.lots.Count+1),entryKey=plan.catalog?.entries?.FirstOrDefault(e=>e!=null)?.key,position=center,size=new Vector2(40,45),floors=1,entrances=1});
            selected=plan.lots.Count-1;EditorUtility.SetDirty(plan);ClosePreview();BuildContent();SceneView.RepaintAll();
        });
        void DuplicateLot()=>Run(()=>{
            if(!plan||plan.lots==null||selected<0||selected>=plan.lots.Count||plan.lots[selected]==null)throw new InvalidOperationException("Выберите участок.");
            var copy=JsonUtility.FromJson<CityLot>(JsonUtility.ToJson(plan.lots[selected]));copy.id=Guid.NewGuid().ToString("N");copy.title+=" — копия";copy.position+=Quaternion.Euler(0,copy.yaw,0)*Vector3.right*(copy.size.x+5);
            Undo.RecordObject(plan,"Копировать участок");plan.lots.Add(copy);selected=plan.lots.Count-1;EditorUtility.SetDirty(plan);ClosePreview();BuildContent();SceneView.RepaintAll();
        });
        void RemoveLot()=>Run(()=>{
            if(!plan||plan.lots==null||selected<0||selected>=plan.lots.Count)return;
            Undo.RecordObject(plan,"Удалить запись участка");plan.lots.RemoveAt(selected);selected=Mathf.Max(0,selected-1);EditorUtility.SetDirty(plan);ClosePreview();BuildContent();SceneView.RepaintAll();
            SetMessage("Удалена только запись плана. Построенные объекты сцены не удалялись.");
        });
        void CreateCatalog()=>Run(()=>{
            string path=EditorUtility.SaveFilePanelInProject("Новый каталог зданий","CityBuildingCatalog","asset","Выберите место каталога");if(string.IsNullOrEmpty(path))return;
            var catalog=CreateInstance<CityBuildingCatalog>();var profile=ZombieSurvival.ModularBuildings.Editor.BuildingGenerator.CreateProfile();
            if(profile)catalog.entries.Add(new CityBuildingEntry{key="residential-v6",title="Городской дом V6 — квартиры, балконы, водостоки",kind=CityBuildingKind.Profile,profile=profile});
            AssetDatabase.CreateAsset(catalog,path);Selection.activeObject=catalog;EditorGUIUtility.PingObject(catalog);
            SetMessage("Каталог создан. Добавляйте готовые prefab или проверенные профили в Inspector.");
        });
        void CreatePlan()=>Run(()=>{
            string path=EditorUtility.SaveFilePanelInProject("Новый план квартала","CityBlockPlan","asset","Выберите место плана");if(string.IsNullOrEmpty(path))return;
            var value=CreateInstance<CityBlockPlan>();value.catalog=Selection.activeObject as CityBuildingCatalog??plan?.catalog;
            AssetDatabase.CreateAsset(value,path);SetPlan(value);Selection.activeObject=value;
        });
        void ShowPreview()=>Run(()=>{
            ClosePreview();preview=CityBlockBuilder.PreviewLot(plan,selected,UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            try{
                var renderers=preview.Root.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
                if(renderers.Length==0)throw new InvalidOperationException("У предпросмотра нет видимой геометрии.");
                Bounds bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                var light=new GameObject("City preview sun").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,preview.Scene);light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-30,0);
                var camera=new GameObject("City preview camera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,preview.Scene);camera.scene=preview.Scene;
                float distance=Mathf.Max(12,bounds.size.magnitude*1.25f);camera.transform.position=bounds.center+new Vector3(.65f,.5f,-1).normalized*distance;camera.transform.LookAt(bounds.center);camera.fieldOfView=50;camera.farClipPlane=distance*4;camera.nearClipPlane=.1f;
                texture=new RenderTexture(800,450,24);texture.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=texture});camera.enabled=false;
                if(image!=null){image.image=texture;image.style.display=DisplayStyle.Flex;content.ScrollTo(image);}previewSourceStamp=SourceStamp();SetMessage("Предпросмотр готов: изображение показано ниже. Дом на карту не добавлен.");
            }catch{ClosePreview();throw;}
        });
        void ClosePreview(){if(image!=null){image.image=null;image.style.display=DisplayStyle.None;}preview?.Dispose();preview=null;previewSourceStamp=null;if(texture){texture.Release();DestroyImmediate(texture);texture=null;}}
        void DrawLots(SceneView view)
        {
            if(!drawLots||!plan||plan.lots==null||EditorApplication.isPlayingOrWillChangePlaymode||PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            var old=Handles.color;
            try{for(int i=0;i<plan.lots.Count;i++){
                var lot=plan.lots[i];if(lot==null||!lot.enabled||!float.IsFinite(lot.position.x)||!float.IsFinite(lot.position.y)||!float.IsFinite(lot.position.z)||!float.IsFinite(lot.yaw)||!float.IsFinite(lot.size.x)||!float.IsFinite(lot.size.y)||lot.size.x<=0||lot.size.y<=0)continue;
                var rotation=Quaternion.Euler(0,lot.yaw,0);var corners=new Vector3[5];for(int c=0;c<4;c++)corners[c]=lot.position+rotation*new Vector3((c==0||c==3?-1:1)*lot.size.x*.5f,.05f,(c<2?-1:1)*lot.size.y*.5f);corners[4]=corners[0];
                Handles.color=i==selected?Color.cyan:new Color(.5f,.65f,.7f,.7f);Handles.DrawAAPolyLine(2,corners);
                if(i!=selected)continue;Handles.Label(lot.position+Vector3.up*.2f,lot.title);
                EditorGUI.BeginChangeCheck();var position=Handles.PositionHandle(lot.position,rotation);
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(plan,"Передвинуть участок плана");lot.position=position;EditorUtility.SetDirty(plan);ClosePreview();data?.Update();}
            }}finally{Handles.color=old;}
        }
    }
}
