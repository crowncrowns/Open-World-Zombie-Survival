using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
namespace TerrainRoads.Editor {
[CustomEditor(typeof(TerrainConformingRoad))]
public class RoadInspector : UnityEditor.Editor {
 public override UnityEngine.UIElements.VisualElement CreateInspectorGUI(){
  var root=new UnityEngine.UIElements.VisualElement();var r=(TerrainConformingRoad)target;
  UnityEditor.UIElements.InspectorElement.FillDefaultInspector(root,serializedObject,this);
  var status=new UnityEngine.UIElements.HelpBox(r.diagnostic,UnityEngine.UIElements.HelpBoxMessageType.Info);root.Add(status);
  root.Add(new UnityEngine.UIElements.HelpBox("Orange markers: Terrain grading required. Heights are never modified. A smooth road can float or cut into rough ground; grade a Terrain copy manually. Collider is optional and overlaps Terrain collision beneath the road.",UnityEngine.UIElements.HelpBoxMessageType.Warning));
  root.Add(new UnityEngine.UIElements.Button(()=>{r.Rebuild();status.text=r.diagnostic;SceneView.RepaintAll();}){text="Rebuild"});
  root.schedule.Execute(()=>{if(r){status.text=r.diagnostic;status.messageType=r.GetComponent<MeshFilter>().sharedMesh?UnityEngine.UIElements.HelpBoxMessageType.Info:UnityEngine.UIElements.HelpBoxMessageType.Error;}}).Every(500);
  return root;
 }
 public void OnSceneGUI(){var r=(TerrainConformingRoad)target;if(!r.route||r.route.Splines.Count==0)return;
  Vector3 p=(Vector3)r.route.EvaluatePosition(0,.5f);Vector3 d=(Vector3)r.route.EvaluateTangent(0,.5f);d.y=0;if(d.sqrMagnitude<.001f)return;var side=Vector3.Cross(Vector3.up,d.normalized);
  Handles.color=Color.cyan;EditorGUI.BeginChangeCheck();Vector3 h=Handles.Slider(p+side*r.width*.5f,side,HandleUtility.GetHandleSize(p)*.15f,Handles.CubeHandleCap,0);
  if(EditorGUI.EndChangeCheck()){Undo.RecordObject(r,"Change road width");r.width=Mathf.Max(.2f,2*Vector3.Dot(h-p,side));EditorUtility.SetDirty(r);r.Rebuild();}
 }
}
[InitializeOnLoad]
static class RoadRefresh {
 static double next;static bool dirty=true;
 static RoadRefresh(){Spline.Changed+=(s,i,m)=>dirty=true; Undo.undoRedoPerformed+=()=>dirty=true;TerrainCallbacks.heightmapChanged+=(t,r,s)=>dirty=true;EditorApplication.update+=Update;}
 static readonly System.Collections.Generic.Dictionary<TerrainConformingRoad,int> hashes=new();
 static void Update(){if(EditorApplication.timeSinceStartup<next||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;next=EditorApplication.timeSinceStartup+.75;
  foreach(var r in Object.FindObjectsByType<TerrainConformingRoad>(FindObjectsInactive.Exclude)){
   if(r.network||!r.autoRefresh||EditorUtility.IsPersistent(r)||!r.gameObject.scene.IsValid())continue;
   int hash=EditorJsonUtility.ToJson(r).GetHashCode()^r.transform.localToWorldMatrix.GetHashCode();if(r.route)hash^=r.route.transform.localToWorldMatrix.GetHashCode();if(r.terrain)hash^=r.terrain.transform.localToWorldMatrix.GetHashCode();
   var id=r;if(dirty||!hashes.TryGetValue(id,out int old)||old!=hash||!r.GeneratedMesh){r.Rebuild();hashes[id]=hash;}
  } dirty=false;
 }
}
public static class RoadTools {
 [MenuItem("Tools/Terrain Roads/Create from selected Spline (separate object)")]
 public static void Create(){var source=Selection.activeGameObject?Selection.activeGameObject.GetComponent<SplineContainer>():null;if(!source){EditorUtility.DisplayDialog("Terrain Roads","Select an object with SplineContainer.","OK");return;}
  var go=new GameObject(source.name+" Terrain Road");Undo.RegisterCreatedObjectUndo(go,"Create terrain road");var r=go.AddComponent<TerrainConformingRoad>();r.route=source;r.terrain=Terrain.activeTerrain;
  var renderer=source.GetComponent<MeshRenderer>();if(renderer)go.GetComponent<MeshRenderer>().sharedMaterial=renderer.sharedMaterial;r.Rebuild();Selection.activeGameObject=go;
 }
 [MenuItem("Tools/Terrain Roads/Create isolated demonstration")]
 public static void Demo(){
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);EditorSceneManager.SetActiveScene(scene);
  var data=new TerrainData{heightmapResolution=129,size=new Vector3(160,25,160)};var heights=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=.1f+z/128f*.3f+Mathf.Sin(x*.3f)*.004f;data.SetHeights(0,0,heights);
  const string folder="Assets/TerrainRoads/Demo";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/TerrainRoads","Demo");AssetDatabase.CreateAsset(data,AssetDatabase.GenerateUniqueAssetPath(folder+"/DemoTerrain.asset"));var terrain=Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();
  var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.12f,.12f,.13f)};AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath(folder+"/Asphalt.mat"));
  for(int k=0;k<2;k++){var go=new GameObject(k==0?"Straight":"Bend");var route=go.AddComponent<SplineContainer>();var spline=new Spline();foreach(var p in k==0?new[]{new Vector3(20,0,20),new Vector3(20,0,130)}:new[]{new Vector3(60,0,20),new Vector3(90,0,40),new Vector3(120,0,90),new Vector3(90,0,130)})spline.Add(new BezierKnot(p),TangentMode.AutoSmooth);route.Spline=spline;var road=go.AddComponent<TerrainConformingRoad>();road.route=route;road.terrain=terrain;go.GetComponent<MeshRenderer>().sharedMaterial=mat;if(!road.Rebuild())throw new System.Exception(road.diagnostic);}
  new GameObject("Sun",typeof(Light)).GetComponent<Light>().type=LightType.Directional;EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath(folder+"/RoadDemo.unity"));
 }
 public static void VerifyBatch(){try{Demo();foreach(var r in Object.FindObjectsByType<TerrainConformingRoad>(FindObjectsInactive.Exclude)){if(!r.Rebuild()||r.GeneratedMesh.vertexCount<10)throw new System.Exception(r.diagnostic);foreach(var normal in r.GeneratedMesh.normals)if(normal.y<.1f)throw new System.Exception("Invalid normal");Debug.Log("ROAD_TEST_OK "+r.name+" "+r.diagnostic);}AssetDatabase.SaveAssets();EditorApplication.Exit(0);}catch(System.Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
}



