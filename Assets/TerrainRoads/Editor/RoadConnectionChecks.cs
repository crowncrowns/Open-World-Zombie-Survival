using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Clipper2Lib;
namespace TerrainRoads.Editor {
public static class RoadConnectionChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run(){
  string folder="Assets/TerrainRoads/ConnectionTestData";Directory.CreateDirectory(folder);var metadata=new List<TerrainRoadSurfaceMap.SectionInfo>();
  using(var w=new BinaryWriter(File.Create(folder+"/Roads.bytes"))){w.Write(1);w.Write(2);for(int part=0;part<2;part++){var v=new List<Vector3>();var tr=new List<int>();float z0=part==0?10:35;for(float x=10;x<60;x+=2.5f)for(float z=z0;z<z0+5;z+=2.5f){int n=v.Count;v.AddRange(new[]{new Vector3(x,0,z),new Vector3(x,0,z+2.5f),new Vector3(x+2.5f,0,z+2.5f),new Vector3(x+2.5f,0,z)});tr.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}w.Write(part);w.Write(v.Count);w.Write(tr.Count);foreach(var p in v){w.Write(p.x);w.Write(p.y);w.Write(p.z);}foreach(int i in tr)w.Write(i);metadata.Add(new TerrainRoadSurfaceMap.SectionInfo{id="test_"+part,x=35,z=z0+2.5f});}}
  File.WriteAllText(folder+"/Roads.json",Newtonsoft.Json.JsonConvert.SerializeObject(metadata));
  var verts=new List<Vector3>();var indices=new List<int>();for(int part=0;part<2;part++){float z0=part==0?10:35;var ring=new[]{new Vector3(10,0,z0),new Vector3(60,0,z0),new Vector3(60,0,z0+5),new Vector3(10,0,z0+5)};for(int edge=0;edge<4;edge++){var a=ring[edge];var b=ring[(edge+1)%4];var d=(b-a).normalized;var n=new Vector3(d.z,0,-d.x)*.18f;int count=Mathf.CeilToInt(Vector3.Distance(a,b)/.4f);for(int k=0;k<count;k++){var p=Vector3.Lerp(a,b,k/(float)count)+Vector3.up*.14f-n.normalized*.01f;var q=Vector3.Lerp(a,b,(k+1)/(float)count)+Vector3.up*.14f-n.normalized*.01f;int o=verts.Count;verts.AddRange(new[]{p,q,q+n,p+n});indices.AddRange(new[]{o,o+1,o+2,o,o+2,o+3});}}}
  using(var w=new BinaryWriter(File.Create(folder+"/Curbs.bytes"))){w.Write(1);w.Write(1);w.Write(0);w.Write(verts.Count);w.Write(indices.Count);foreach(var p in verts){w.Write(p.x);w.Write(p.y);w.Write(p.z);}foreach(int i in indices)w.Write(i);}
  File.WriteAllText(folder+"/Curbs.json",Newtonsoft.Json.JsonConvert.SerializeObject(new[]{new TerrainRoadSurfaceMap.SectionInfo{id="curb_test",x=35,z=25}}));AssetDatabase.Refresh();
  var scene=EditorSceneManager.NewPreviewScene();var td=new TerrainData{heightmapResolution=33,size=new Vector3(80,20,80)};var h=new float[33,33];for(int z=0;z<33;z++)for(int x=0;x<33;x++)h[z,x]=.1f+z*.003f+Mathf.Sin(x*.6f)*Mathf.Sin(z*.7f)*.003f;td.SetHeights(0,0,h);h=td.GetHeights(0,0,33,33);
  var tg=Terrain.CreateTerrainGameObject(td);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(tg,scene);var go=new GameObject("Connection tests");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);go.SetActive(false);var map=go.AddComponent<TerrainRoadSurfaceMap>();map.terrain=tg.GetComponent<Terrain>();map.roadData=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/Roads.bytes");map.sectionData=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/Roads.json");map.curbData=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/Curbs.bytes");map.curbSectionData=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/Curbs.json");var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));map.asphalt=map.curbMaterial=mat;map.enabled=false;go.SetActive(true);var report=new List<object>();
  try{
   bool Hit(Vector3 p,bool curb){var ray=new Ray(p+Vector3.up*100,Vector3.down);return map.GetComponentsInChildren<MeshCollider>(true).Any(c=>{bool isCurb=c.sharedMesh.name.StartsWith("Curbs")||c.name.Contains("бордюры");return isCurb==curb&&c.Raycast(ray,out var hit,200);});}
   var c=new RoadSurfaceConnection{start=new Vector3(30,0,15),startOut=Vector3.forward,end=new Vector3(30,0,35),endOut=Vector3.back,controlA=new Vector3(30,0,22),controlB=new Vector3(30,0,28),width=4};
   foreach(string mode in new[]{"straight","curved","extension"}){
    var value=RoadConnectionAuthoring.Clone(c);if(mode=="curved"){value.controlA.x=36;value.controlB.x=36;}if(mode=="extension"){value.extension=true;value.end=new Vector3(45,0,27);value.controlA=new Vector3(30,0,23);value.controlB=new Vector3(39,0,27);}
    map.connections=new List<RoadSurfaceConnection>{value};Check(map.Rebuild(),mode+": "+map.diagnostic);Check(Hit(RoadConnectionGeometry.Evaluate(value,.5f),false),mode+": missing asphalt");Check(!Hit(new Vector3(30,0,14.99f),true),mode+": internal curb across start");if(!value.extension)Check(!Hit(new Vector3(30,0,35.01f),true),mode+": internal curb across end");else Check(!Hit(value.end+(value.end-value.controlB).normalized*value.width*.5f,true),"Free end blocked by curb");
    double overlap=Clipper.Intersect(map.connectionPlan.original,map.connectionPlan.added,FillRule.NonZero).Sum(p=>Math.Abs(Clipper.Area(p)))/(RoadConnectionGeometry.Scale*RoadConnectionGeometry.Scale);Check(overlap<.00001,mode+": overlap");
    var tc=tg.GetComponent<TerrainCollider>();double min=999,max=-999;int probes=0;foreach(var f in map.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Проезды — объединённое покрытие")){var v=f.sharedMesh.vertices;var tr=f.sharedMesh.triangles;for(int i=0;i<tr.Length;i+=3){var p=f.transform.TransformPoint((v[tr[i]]+v[tr[i+1]]+v[tr[i+2]])/3);Check(tc.Raycast(new Ray(p+Vector3.up*100,Vector3.down),out var hit,200),"Terrain ray miss");double gap=p.y-hit.point.y;Check(gap>0&&gap<.05,"Terrain penetration / hovering");min=Math.Min(min,gap);max=Math.Max(max,gap);probes++;}}
    report.Add(new{mode,passed=true,overlap,probes,minGap=min,maxGap=max});
   }
   var root=go.transform.GetChild(0).gameObject;map.connections[0].width=float.NaN;Check(!map.Rebuild(),"Invalid width accepted");Check(root&&root==go.transform.GetChild(0).gameObject,"Failed rebuild replaced prior geometry");report.Add(new{mode="transactional rejection",passed=true});
   var after=td.GetHeights(0,0,33,33);for(int z=0;z<33;z++)for(int x=0;x<33;x++)Check(h[z,x]==after[z,x]||Mathf.Abs(h[z,x]-after[z,x])<.00002f,"Terrain modified");
   Directory.CreateDirectory("work/RoadConnections_20261025");File.WriteAllText("work/RoadConnections_20261025/geometry-tests.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));Debug.Log("Road connection geometry checks passed");
  }finally{EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Object.DestroyImmediate(td);UnityEngine.Object.DestroyImmediate(mat);}
 }
}
}


