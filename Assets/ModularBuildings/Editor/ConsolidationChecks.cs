using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZombieSurvival.ModularBuildings.Editor
{
    // Checks the saved handoff scenes without saving or rebuilding user scenes on disk.
    public static class ConsolidationChecks
    {
        public static void Run()
        {
            var lines=new List<string>();
            var folder="Verification/TodayConsolidation";
            Directory.CreateDirectory(folder);
            try
            {
                lines.Add("Unity "+Application.unityVersion);
                // Physics.Raycast uses the default physics scene; preview scenes are not suitable here.
                var buildingScene=EditorSceneManager.OpenScene("Assets/ModularBuildings/Tests/DemoResidentialV4_Review.unity",OpenSceneMode.Additive);
                try
                {
                    var b=buildingScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GeneratedBuilding>(true)).Single();
                    var errors=BuildingGenerator.Validate(b,true);
                    if(errors.Count>0)throw new Exception(string.Join("\n",errors));
                    lines.Add("PASS saved V4 building: full validation including stairs, doors, corners, lining and coplanar audit");
                }
                finally {EditorSceneManager.CloseScene(buildingScene,true);}
                var roadScene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene_TerrainRoads.unity",OpenSceneMode.Additive);
                try
                {
                    var roots=roadScene.GetRootGameObjects();
                    var network=roots.SelectMany(g=>g.GetComponentsInChildren<TerrainRoads.TerrainRoadNetwork>(true)).Single();
                    if(network.links.Count!=21||network.junctions.Count!=13)throw new Exception("Unexpected saved road topology");
                    if(!network.terrain||!network.terrain.terrainData)throw new Exception("Missing network TerrainData");
                    if(!network.conformToTerrain)throw new Exception("Terrain conformance disabled");
                    var dataPath=AssetDatabase.GetAssetPath(network.terrain.terrainData);
                    var terrainBefore=File.ReadAllBytes(dataPath);
                    var poses=roots.ToDictionary(g=>g,g=>(g.transform.position,g.transform.rotation,g.transform.localScale));
                    if(!network.Rebuild())throw new Exception(network.diagnostic);
                    if(!terrainBefore.SequenceEqual(File.ReadAllBytes(dataPath)))throw new Exception("TerrainData changed");
                    foreach(var root in roots)
                    {
                        var pose=poses[root];
                        if(root.transform.position!=pose.Item1||root.transform.rotation!=pose.Item2||root.transform.localScale!=pose.Item3)throw new Exception("Root pose changed: "+root.name);
                    }
                    // Source spline components can have disabled empty renderers. Check the published mesh group.
                    var generated=(GameObject)new SerializedObject(network).FindProperty("generated").objectReferenceValue;
                    if(!generated)throw new Exception("Missing generated road group");
                    var roadRenderers=generated.GetComponentsInChildren<MeshRenderer>(true);
                    if(roadRenderers.Any(r=>r.sharedMaterials.Any(m=>!m)))throw new Exception("Missing road material");
                    if(generated.GetComponentsInChildren<MeshFilter>(true).Any(m=>!m.sharedMesh))throw new Exception("Missing road mesh");
                    lines.Add("PASS saved road scene: 21 links / 13 junctions, Rebuild, materials/meshes, TerrainData bytes and root poses preserved");
                    lines.Add("Road diagnostic: "+network.diagnostic);
                }
                finally {EditorSceneManager.CloseScene(roadScene,true);}
                foreach(var path in new[]{"Assets/Environment/Props/Dumpster/Prefabs/Dumpster_Empty.prefab","Assets/Environment/Props/Dumpster/Prefabs/Dumpster_Filled.prefab","Assets/Environment/Props/PotbellyStove/Prefabs/PF_PotbellyStove_Burning.prefab","Assets/Props/Dumpster/Prefabs/Dumpster_Empty.prefab","Assets/Props/Dumpster/Prefabs/Dumpster_Filled.prefab","Assets/Props/PotbellyStove/Prefabs/PF_PotbellyStove_Burning.prefab"})
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(!prefab||prefab.GetComponentsInChildren<Transform>(true).Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0))throw new Exception("Missing prop script: "+path);
                }
                lines.Add("PASS six prop prefab component references after duplicate-class repair");
                lines.Add("PASS compilation and consolidated Edit Mode checks; no scene saved, no Play Mode or player build");
            }
            catch(Exception e){lines.Add("FAIL "+e);throw;}
            finally {File.WriteAllLines(folder+"/checks.txt",lines);}
        }
    }
}
