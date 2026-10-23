using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
namespace TerrainRoads.Editor {
[CustomEditor(typeof(TerrainRoadNetwork))]
public class NetworkInspector : UnityEditor.Editor {
 public override UnityEngine.UIElements.VisualElement CreateInspectorGUI(){var root=new UnityEngine.UIElements.VisualElement();var n=(TerrainRoadNetwork)target;UnityEditor.UIElements.InspectorElement.FillDefaultInspector(root,serializedObject,this);var status=new UnityEngine.UIElements.HelpBox(n.diagnostic,UnityEngine.UIElements.HelpBoxMessageType.Info);root.Add(status);root.Add(new UnityEngine.UIElements.HelpBox("Концы сплайнов должны совпадать с общими узлами. Исключения бордюров задаются в метрах от начала сплайна. Оранжевые маркеры — места планировки; TerrainData не изменяется.",UnityEngine.UIElements.HelpBoxMessageType.Info));root.Add(new UnityEngine.UIElements.Button(()=>{n.Rebuild();status.text=n.diagnostic;EditorSceneManager.MarkSceneDirty(n.gameObject.scene);SceneView.RepaintAll();}){text="Rebuild всей сети"});root.schedule.Execute(()=>{if(n)status.text=n.diagnostic;}).Every(500);return root;}
 public void OnSceneGUI(){var n=(TerrainRoadNetwork)target;for(int i=0;i<n.junctions.Count;i++){var j=n.junctions[i];Handles.Label(j.position+Vector3.up*2,$"{i}: {j.label}");EditorGUI.BeginChangeCheck();var p=Handles.PositionHandle(j.position,Quaternion.identity);if(EditorGUI.EndChangeCheck()){Undo.RecordObject(n,"Переместить дорожный узел");j.position=p;foreach(var l in n.links){if(!l.road||!l.road.route)continue;var container=l.road.route;var spline=container.Splines[l.road.splineIndex];foreach(bool start in new[]{true,false})if((start?l.start:l.end)==i){Undo.RecordObject(container,"Переместить конец сплайна");int k=start?0:spline.Count-1;var knot=spline[k];knot.Position=container.transform.InverseTransformPoint(p);spline.SetKnot(k,knot);EditorUtility.SetDirty(container);}}EditorUtility.SetDirty(n);n.Rebuild();}}}
}
}
