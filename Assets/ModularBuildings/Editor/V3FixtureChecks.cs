using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class V3FixtureChecks
    {
        public static string UnionRegression(Scene scene)
        {
            var root=new GameObject("PlanarUnionRegression");SceneManager.MoveGameObjectToScene(root,scene);root.transform.position=Vector3.right*200;
            try{
                foreach(float x in new[]{0f,.5f}){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(root.transform,false);go.transform.localPosition=Vector3.right*x;}
                if(CoplanarAudit.Scan(root).overlapPairs==0)throw new Exception("Audit missed injected coplanar overlap");
                SeamUnion.Resolve(root);var audit=CoplanarAudit.Scan(root);
                if(audit.overlapPairs>0||audit.unreadable.Count>0)throw new Exception("Planar union regression failed");
                double area=0;
                foreach(var f in root.GetComponentsInChildren<MeshFilter>()){
                    var m=root.transform.worldToLocalMatrix*f.transform.localToWorldMatrix;var vertices=f.sharedMesh.vertices;var indices=f.sharedMesh.triangles;
                    for(int i=0;i<indices.Length;i+=3){var a=m.MultiplyPoint3x4(vertices[indices[i]]);var b=m.MultiplyPoint3x4(vertices[indices[i+1]]);var c=m.MultiplyPoint3x4(vertices[indices[i+2]]);var normal=Vector3.Cross(b-a,c-a);if(normal.normalized.y>.99f)area+=normal.magnitude*.5;}
                }
                if(Math.Abs(area-1.5)>.0001)throw new Exception("Union opened a gap or lost surface area: "+area);
                return "PASS injected coplanar overlap detected; resolved with top coverage area 1.5 m2 (no area lost)";
            }finally{Object.DestroyImmediate(root);}
        }
        public static string TerrainFixture(GeneratedBuilding b,Scene scene)
        {
            var data=new TerrainData{heightmapResolution=65,size=new Vector3(64,3,32)};var heights=new float[65,65];
            for(int z=0;z<65;z++)for(int x=0;x<65;x++)heights[z,x]=.5f+.15f*Mathf.Sin(x*Mathf.PI/32)+.15f*Mathf.Cos(z*Mathf.PI/32);
            data.SetHeights(0,0,heights);
            var layers=AssetDatabase.FindAssets("t:TerrainLayer",new[]{"Assets/Maps"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<TerrainLayer>).Where(x=>x).Take(1).ToArray();data.terrainLayers=layers;
            var path=AssetDatabase.GenerateUniqueAssetPath(BuildingMvpChecks.TestFolder+"/TerrainPlacementV3.asset");AssetDatabase.CreateAsset(data,path);
            var go=Terrain.CreateTerrainGameObject(data);go.name="TerrainPlacementFixture";SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=new Vector3(-12,-3,-10);
            var terrain=go.GetComponent<Terrain>();var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if(shader){var material=new Material(shader){name="DemoTerrainV3"};var materialPath=AssetDatabase.GenerateUniqueAssetPath(BuildingMvpChecks.TestFolder+"/TerrainPlacementV3.mat");AssetDatabase.CreateAsset(material,materialPath);terrain.materialTemplate=material;}
            Physics.SyncTransforms();var errors=CheckTerrain(b,terrain,out int probes);
            if(errors.Count>0)throw new Exception(string.Join("\n",errors));return "PASS uneven Terrain fixture: "+probes+" perimeter probes, terrain within 3m skirt coverage";
        }
        public static List<string> CheckTerrain(GeneratedBuilding b,Terrain terrain,out int probes)
        {
            var errors=new List<string>();probes=0;var skirt=b.transform.Find("TerrainFoundation");float last=(b.entrances-1)*9+4.5f;
            var samples=new List<Vector3>();var directions=new List<Vector3>();
            for(float x=-4.5f;x<=last+.001f;x+=.5f)foreach(int side in new[]{-1,1}){samples.Add(new Vector3(x,0,side<0?-1.5f:10.5f));directions.Add(new Vector3(0,0,-side));}
            for(float z=-1.5f;z<=10.501f;z+=.5f)foreach(int side in new[]{-1,1}){samples.Add(new Vector3(side<0?-4.5f:last,0,z));directions.Add(new Vector3(-side,0,0));}
            for(int i=0;i<samples.Count;i++){
                var point=b.transform.TransformPoint(samples[i]);float height=terrain.SampleHeight(point)+terrain.transform.position.y;float localY=b.transform.InverseTransformPoint(new Vector3(point.x,height,point.z)).y;
                if(localY< -b.profile.foundationDepth+.05f||localY>-.05f){errors.Add("Terrain outside foundation coverage at "+point);continue;}
                var direction=b.transform.TransformDirection(directions[i]);point.y=height+.05f;
                if(!Physics.RaycastAll(point-direction*.8f,direction,1.6f).Any(h=>h.collider.transform.IsChildOf(skirt)))errors.Add("Skirt gap at terrain sample "+point);
                probes++;
            }return errors;
        }
    }
}
