using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Splines;
namespace TerrainRoads.Editor {
public static class RoadVerification {
 public static void Run(){
 var report=new StringBuilder();var roads=UnityEngine.Object.FindObjectsByType<TerrainConformingRoad>(FindObjectsInactive.Exclude);if(roads.Length<2)throw new Exception("Open the demonstration first.");
 foreach(var road in roads){
  var data=road.terrain.terrainData;int res=data.heightmapResolution;var before=data.GetHeights(0,0,res,res);
  Require(road.Rebuild(),road.diagnostic);Require(road.GeneratedMesh.vertexCount>=10,"Missing mesh");foreach(var normal in road.GeneratedMesh.normals)Require(normal.y>.1f,"Downward normals");
  var after=data.GetHeights(0,0,res,res);for(int z=0;z<res;z++)for(int x=0;x<res;x++)Require(before[z,x]==after[z,x],"Terrain changed");
  var terrain=road.terrain;try{road.terrain=null;Require(!road.Rebuild(),"Missing Terrain accepted");}finally{road.terrain=terrain;road.Rebuild();}
  report.AppendLine(road.name+": PASS mesh, normals, missing Terrain, unchanged heights; "+road.GeneratedMesh.vertexCount+" vertices");
 }
 var go=new GameObject("Temporary road geometry test");
 try{
  var route=go.AddComponent<SplineContainer>();var road=go.AddComponent<TerrainConformingRoad>();road.autoRefresh=false;road.route=route;road.terrain=roads[0].terrain;
  var spline=new Spline();foreach(var p in new[]{new Vector3(60,0,60),new Vector3(100,0,60),new Vector3(100,0,100),new Vector3(60,0,100)})spline.Add(new BezierKnot(p),TangentMode.AutoSmooth);spline.Closed=true;route.Spline=spline;
  Require(road.Rebuild(),road.diagnostic);var v=road.GeneratedMesh.vertices;for(int j=0;j<5;j++)Require(Vector3.Distance(v[j],v[v.Length-5+j])<.001f,"Closed seam gap");report.AppendLine("Closed curve: PASS seam positions");
  spline=new Spline();foreach(var p in new[]{new Vector3(40,0,40),new Vector3(120,0,120),new Vector3(40,0,120),new Vector3(120,0,40)})spline.Add(new BezierKnot(p),TangentMode.AutoSmooth);route.Spline=spline;road.width=1;
  Require(!road.Rebuild()&&road.diagnostic.Contains("self-intersection"),"Crossing was not diagnosed: "+road.diagnostic);report.AppendLine("Crossing curve: PASS rejected");
  spline=new Spline();foreach(var p in new[]{new Vector3(70,0,70),new Vector3(75,0,70),new Vector3(75,0,75),new Vector3(70,0,75)})spline.Add(new BezierKnot(p),TangentMode.AutoSmooth);route.Spline=spline;road.width=12;road.sampleStep=.25f;
  Require(!road.Rebuild(),"Impossible narrow turn accepted");report.AppendLine("Narrow turn rejected: PASS; "+road.diagnostic);
 }finally{UnityEngine.Object.DestroyImmediate(go);}
 Directory.CreateDirectory("RoadWorkflowBackup");File.WriteAllText("RoadWorkflowBackup/test-results.txt",report.ToString());Debug.Log(report.ToString());
 }
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
}


