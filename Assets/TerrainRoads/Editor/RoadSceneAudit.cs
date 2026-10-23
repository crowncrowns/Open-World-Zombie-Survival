using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace TerrainRoads.Editor {
public static class RoadSceneAudit {
 public static void Inspect(){
 var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Additive);var b=new StringBuilder();
 foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Terrain>(true)){
 var d=t.terrainData;var h=d.GetHeights(0,0,d.heightmapResolution,d.heightmapResolution);float min=1,max=0;foreach(float v in h){min=Mathf.Min(min,v);max=Mathf.Max(max,v);}b.AppendLine($"Terrain {t.name}: data={AssetDatabase.GetAssetPath(d)}, size={d.size}, resolution={d.heightmapResolution}, height range normalized={min}..{max}, collider={t.GetComponent<TerrainCollider>()!=null}, material={t.materialTemplate}");}
 foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)){if(t.GetComponent<MeshRenderer>()||t.name.Contains("Spline")||t.name=="Roads")b.AppendLine($"Object {t.name}; mesh={t.GetComponent<MeshFilter>()?.sharedMesh}; renderer={t.GetComponent<MeshRenderer>()!=null}; collider={t.GetComponent<Collider>()!=null}");}
 File.WriteAllText("RoadWorkflowBackup/scene-audit.txt",b.ToString());EditorSceneManager.CloseScene(scene,true);
 }
}
}

