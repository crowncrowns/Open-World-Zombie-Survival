using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
namespace TerrainRoads.Editor {
public static class RoadConnectionAuthoring {
 public const string ScenePath="Assets/Scenes/SampleScene_CorrectedRoads_20261025.unity";
  public static void ResetControls(RoadSurfaceConnection c){c.start.y=c.end.y=0;c.startOut.y=c.endOut.y=0;c.startOut.Normalize();c.endOut.Normalize();float length=Vector3.Distance(c.start,c.end);c.controlA=c.start+c.startOut*length*.33f;c.controlB=c.extension?c.end-(c.end-c.start).normalized*length*.33f:c.end+c.endOut*length*.33f;}
 public static RoadSurfaceConnection Clone(RoadSurfaceConnection c)=>JsonUtility.FromJson<RoadSurfaceConnection>(JsonUtility.ToJson(c));
 public static bool Apply(TerrainRoadSurfaceMap map,RoadSurfaceConnection draft,int index,out string message){var next=map.connections.Select(Clone).ToList();if(index<0)next.Add(Clone(draft));else if(index<next.Count)next[index]=Clone(draft);else{message="Соединение уже удалено.";return false;}return Change(map,next,"Создать / изменить проезд",out message);}
 public static bool Delete(TerrainRoadSurfaceMap map,int index,out string message){var next=map.connections.Select(Clone).ToList();if(index<0||index>=next.Count){message="Выберите проезд.";return false;}next.RemoveAt(index);return Change(map,next,"Удалить проезд",out message);}
 static bool Change(TerrainRoadSurfaceMap map,List<RoadSurfaceConnection> next,string action,out string message){
  if(!map||map.gameObject.scene.path!=ScenePath){message="Работайте в SampleScene_CorrectedRoads_20261025.";return false;}
  Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(action);Undo.RecordObject(map,action);var previous=map.connections;map.connections=next;
  if(!map.Rebuild()){map.connections=previous;message=map.diagnostic;Undo.CollapseUndoOperations(group);return false;}
  EditorUtility.SetDirty(map);EditorSceneManager.MarkSceneDirty(map.gameObject.scene);Undo.CollapseUndoOperations(group);message="Проезд применён. Полотно объединено, бордюры перестроены. Ctrl+S сохраняет сцену.";return true;
 }
}
[InitializeOnLoad] static class RoadConnectionUndo {
 static readonly Dictionary<TerrainRoadSurfaceMap,string> hashes=new();
 [Serializable] class Connections {public List<RoadSurfaceConnection> values;}
 static string Fingerprint(TerrainRoadSurfaceMap m)=>JsonUtility.ToJson(new Connections{values=m.connections});
 static RoadConnectionUndo(){TerrainRoadSurfaceMap.Built+=m=>hashes[m]=Fingerprint(m);Undo.undoRedoPerformed+=()=>EditorApplication.delayCall+=Restore;}
 static void Restore(){foreach(var m in UnityEngine.Object.FindObjectsByType<TerrainRoadSurfaceMap>()){if(m.gameObject.scene.path!=RoadConnectionAuthoring.ScenePath)continue;string now=Fingerprint(m);if(hashes.TryGetValue(m,out var old)&&old==now)continue;if(m.connections.Count>0||m.connectionPlan!=null){if(!m.Rebuild())Debug.LogWarning(m.diagnostic,m);}hashes[m]=now;}SceneView.RepaintAll();}
}
public sealed class RoadConnectionsWindow : EditorWindow {
 TerrainRoadSurfaceMap map;RoadConnectionGeometry.EdgeIndex edges;
 RoadSurfaceConnection draft;int editIndex=-1,picking;bool hasStart,hasEnd;
 FloatField widthField,radiusField;Toggle openEnd;Label status;ObjectField mapField;PopupField<string> existing;
 readonly List<string> names=new();
 public TerrainRoadSurfaceMap Map=>map;
 [MenuItem("Tools/Terrain Roads/Соединить или продлить дорогу")]
 public static void Open(){var w=GetWindow<RoadConnectionsWindow>();w.titleContent=new GUIContent("Проезды");w.minSize=new Vector2(360,430);w.Show();}
 void OnEnable(){SceneView.duringSceneGui+=DuringScene;TerrainRoadSurfaceMap.Built+=OnBuilt;}
 void OnDisable(){SceneView.duringSceneGui-=DuringScene;TerrainRoadSurfaceMap.Built-=OnBuilt;SceneView.RepaintAll();}
 void OnBuilt(TerrainRoadSurfaceMap m){if(m==map){edges=null;RefreshList();}}
 public void CreateGUI(){
  var root=rootVisualElement;root.Clear();root.style.paddingLeft=10;root.style.paddingRight=10;root.style.paddingTop=8;
  root.Add(new Label("Соединение и продление дорог"){style={unityFontStyleAndWeight=FontStyle.Bold,fontSize=15,marginBottom=8}});
  root.Add(new HelpBox("Выберите первый край в Scene. Для соединения укажите второй край; для продления — точку Terrain. Alt + мышь — обзор. Escape — отмена выбора. Голубые ручки задают изгиб. Изменения вступают в силу после «Применить».",HelpBoxMessageType.Info));
  mapField=new ObjectField("Сеть покрытия"){objectType=typeof(TerrainRoadSurfaceMap),allowSceneObjects=true};mapField.RegisterValueChangedCallback(e=>{map=e.newValue as TerrainRoadSurfaceMap;edges=null;RefreshList();});root.Add(mapField);
  root.Add(new Button(FindMap){text="Найти сеть в нужной сцене",name="find-map"});
  var row=new VisualElement();row.style.flexDirection=FlexDirection.Row;
  var connect=new Button(()=>Begin(false)){text="Соединить два края",name="connect"};connect.style.flexGrow=1;row.Add(connect);
  var extend=new Button(()=>Begin(true)){text="Продлить до точки",name="extend"};extend.style.flexGrow=1;row.Add(extend);root.Add(row);
  widthField=new FloatField("Ширина проезда, м"){value=4};widthField.RegisterValueChangedCallback(e=>{if(draft!=null)draft.width=Mathf.Clamp(e.newValue,.8f,20);SceneView.RepaintAll();});root.Add(widthField);
  radiusField=new FloatField("Скругление примыкания, м"){value=1};radiusField.RegisterValueChangedCallback(e=>{if(draft!=null)draft.junctionRadius=Mathf.Clamp(e.newValue,0,4);SceneView.RepaintAll();});root.Add(radiusField);
  openEnd=new Toggle("Без бордюра у свободного конца"){value=true};openEnd.RegisterValueChangedCallback(e=>{if(draft!=null)draft.openEnd=e.newValue;});root.Add(openEnd);
  root.Add(new Button(()=>{if(draft==null||!hasStart||!hasEnd){SetStatus("Сначала выберите начало и конец.");return;}if(RoadConnectionAuthoring.Apply(map,draft,editIndex,out var message)){editIndex=map.connections.FindIndex(c=>c.id==draft.id);RefreshList();}SetStatus(message);SceneView.RepaintAll();}){text="Применить / перестроить",name="apply"});
  root.Add(new Button(()=>{picking=0;draft=null;hasStart=hasEnd=false;editIndex=-1;SetStatus("Черновик отменён. Применённая геометрия не изменена.");SceneView.RepaintAll();}){text="Отменить черновик"});
  existing=new PopupField<string>("Существующие проезды",new List<string>{"—"},0);root.Add(existing);
  root.Add(new Button(()=>{int index=existing.index-1;if(!map||index<0||index>=map.connections.Count){SetStatus("Выберите проезд из списка.");return;}editIndex=index;draft=RoadConnectionAuthoring.Clone(map.connections[index]);hasStart=hasEnd=true;picking=0;widthField.SetValueWithoutNotify(draft.width);radiusField.SetValueWithoutNotify(draft.junctionRadius);openEnd.SetValueWithoutNotify(draft.openEnd);SetStatus("Переместите ручки изгиба / конца в Scene, затем Применить.");SceneView.RepaintAll();}){text="Редактировать выбранный"});
  root.Add(new Button(()=>{if(!map)return;if(RoadConnectionAuthoring.Delete(map,existing.index-1,out var message)){draft=null;hasStart=hasEnd=false;editIndex=-1;RefreshList();}SetStatus(message);SceneView.RepaintAll();}){text="Удалить выбранный проезд",name="delete-connection"});
  status=new Label{style={whiteSpace=WhiteSpace.Normal,marginTop=10}};root.Add(status);FindMap();if(draft!=null){widthField.SetValueWithoutNotify(draft.width);radiusField.SetValueWithoutNotify(draft.junctionRadius);openEnd.SetValueWithoutNotify(draft.openEnd);SetStatus("Черновик восстановлен. Измените ручки или выберите существующий проезд.");}
 }
 void SetStatus(string text){if(status!=null)status.text=text;Repaint();}
 void FindMap(){var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(RoadConnectionAuthoring.ScenePath);if(!scene.isLoaded){SetStatus("Откройте SampleScene_CorrectedRoads_20261025.");return;}map=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TerrainRoadSurfaceMap>(true)).FirstOrDefault();if(mapField!=null)mapField.SetValueWithoutNotify(map);edges=null;RefreshList();SetStatus(map?"Выберите режим.":"В сцене нет Terrain Road Surface Map.");}
 void RefreshList(){if(existing==null)return;names.Clear();names.Add("—");if(map)for(int i=0;i<map.connections.Count;i++)names.Add((i+1)+". "+map.connections[i].label+(map.connections[i].extension?" (продление)":" (соединение)"));existing.choices=new List<string>(names);existing.index=0;}
 public void Begin(bool extension){if(!map)FindMap();if(!map||map.gameObject.scene.path!=RoadConnectionAuthoring.ScenePath){SetStatus("Выберите сеть в указанной сцене.");return;}edges=new RoadConnectionGeometry.EdgeIndex(map);draft=new RoadSurfaceConnection{extension=extension,openEnd=openEnd.value,width=Mathf.Clamp(widthField.value,.8f,20),junctionRadius=Mathf.Clamp(radiusField.value,0,4),label=extension?"Подъезд / продление":"Соединение дорог"};editIndex=-1;hasStart=hasEnd=false;picking=1;SetStatus("Щёлкните первый край дороги (не дальше 4 м от бордюра).");SceneView.RepaintAll();}
 Vector3 Display(Vector3 p){if(!map||!map.terrain)return p;p.y=map.terrain.SampleHeight(p)+map.terrain.transform.position.y+.2f;return p;}
 void DuringScene(SceneView view){if(!map||draft==null)return;var ev=Event.current;if(ev.type==EventType.KeyDown&&ev.keyCode==KeyCode.Escape){picking=0;draft=null;hasStart=hasEnd=false;SetStatus("Выбор отменён.");ev.Use();return;}
  if(picking>0&&!ev.alt){HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));var ray=HandleUtility.GUIPointToWorldRay(ev.mousePosition);var terrainCollider=map.terrain?map.terrain.GetComponent<TerrainCollider>():null;Vector3 p=default,n=default;RaycastHit hit=default;bool valid=terrainCollider&&terrainCollider.Raycast(ray,out hit,10000);
   if(valid){p=hit.point;if(picking==1||!draft.extension){if(edges==null)edges=new RoadConnectionGeometry.EdgeIndex(map);valid=edges.Nearest(p,4,out p,out n);}if(valid){Handles.color=Color.cyan;Handles.DrawWireDisc(Display(p),Vector3.up,.5f);}}
   if(ev.type==EventType.MouseDown&&ev.button==0){ev.Use();if(!valid){SetStatus("Не найден подходящий край / Terrain.");return;}p.y=0;if(picking==1){draft.start=p;draft.startOut=n;hasStart=true;picking=2;SetStatus(draft.extension?"Укажите свободный конец на Terrain.":"Укажите второй край дороги.");}else{draft.end=p;draft.endOut=n;hasEnd=true;picking=0;RoadConnectionAuthoring.ResetControls(draft);SetStatus("Настройте ширину и голубые ручки. Нажмите Применить.");}view.Repaint();}
   if(ev.type==EventType.MouseMove)view.Repaint();
  }
  if(hasStart){Handles.color=Color.green;Handles.DrawWireDisc(Display(draft.start),Vector3.up,.5f);}if(!hasEnd)return;
  Handles.color=Color.cyan;var a=Display(draft.start);var b=Display(draft.end);var ca=Display(draft.controlA);var cb=Display(draft.controlB);Handles.DrawBezier(a,b,ca,cb,Color.cyan,null,3);Handles.DrawDottedLine(a,ca,4);Handles.DrawDottedLine(b,cb,4);
  EditorGUI.BeginChangeCheck();var newA=Handles.PositionHandle(ca,Quaternion.identity);var newB=Handles.PositionHandle(cb,Quaternion.identity);if(EditorGUI.EndChangeCheck()){draft.controlA=new Vector3(newA.x,0,newA.z);draft.controlB=new Vector3(newB.x,0,newB.z);}
  if(draft.extension){EditorGUI.BeginChangeCheck();var end=Handles.PositionHandle(b,Quaternion.identity);if(EditorGUI.EndChangeCheck()){var delta=new Vector3(end.x-draft.end.x,0,end.z-draft.end.z);draft.end+=delta;draft.controlB+=delta;}}
  try{var points=RoadConnectionGeometry.Sample(draft);var left=new List<Vector3>();var right=new List<Vector3>();for(int i=0;i<points.Count;i++){var direction=points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(i-1,0)];var side=Vector3.Cross(Vector3.up,direction.normalized)*draft.width*.5f;left.Add(Display(points[i]-side));right.Add(Display(points[i]+side));}Handles.color=new Color(0,.8f,1,.7f);Handles.DrawAAPolyLine(2,left.ToArray());Handles.DrawAAPolyLine(2,right.ToArray());}catch(Exception e){Handles.Label(a,e.Message);}
  var middle=RoadConnectionGeometry.Evaluate(draft,.5f);var tangent=RoadConnectionGeometry.Evaluate(draft,.51f)-RoadConnectionGeometry.Evaluate(draft,.49f);tangent.y=0;var sideHandle=Vector3.Cross(Vector3.up,tangent.normalized);
  EditorGUI.BeginChangeCheck();var widthHandle=Handles.Slider(Display(middle+sideHandle*draft.width*.5f),sideHandle,HandleUtility.GetHandleSize(Display(middle))*.12f,Handles.CubeHandleCap,0);if(EditorGUI.EndChangeCheck()){draft.width=Mathf.Clamp(2*Vector3.Dot(widthHandle-Display(middle),sideHandle),.8f,20);widthField.SetValueWithoutNotify(draft.width);}
  Handles.Label(a,"Начало");Handles.Label(b,draft.extension?"Свободный конец":"Второй край");
 }
}
}



