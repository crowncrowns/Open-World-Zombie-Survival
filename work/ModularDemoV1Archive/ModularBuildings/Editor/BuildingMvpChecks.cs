using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class BuildingMvpChecks
    {
        [MenuItem("Tools/Zombie Survival/Дома MVP — создать тест и проверить")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Только Edit Mode.");
            var report=new System.Text.StringBuilder();
            var old=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try{
                var profile=BuildingGenerator.CreateProfile();
                var b=BuildingGenerator.Build(profile,1970,5,4,"demo-building-001",scene);
                var errors=BuildingGenerator.Validate(b,true);
                report.AppendLine("Unity "+Application.unityVersion+"; 5 floors, 4 entrances, 40 slots");
                report.AppendLine("VALIDATION: "+(errors.Count==0?"PASS":string.Join("\n",errors))+"; stair capsule probes=896; closed-window rays=116");
                var slot=b.GetComponentsInChildren<ApartmentSlot>()[0];
                var manual=new GameObject("AuthoredApartmentSentinel");manual.transform.SetParent(slot.manualContent,false);
                slot.doorHinge.localRotation=Quaternion.Euler(0,42,0);
                bool refused=false;
                try{BuildingGenerator.Build(profile,1971,4,2,b.buildingId,scene);}catch(InvalidOperationException){refused=true;}
                if(!refused||!manual||Quaternion.Angle(slot.doorHinge.localRotation,Quaternion.Euler(0,42,0))>.01f)throw new Exception("Protection failed");
                report.AppendLine("PASS duplicate-ID regeneration refused; manual child and 42-degree hinge preserved.");
                Object.DestroyImmediate(manual);slot.doorHinge.localRotation=Quaternion.identity;
                int count=scene.rootCount;
                try{BuildingGenerator.Build(profile,1970,0,4,"invalid",scene);throw new Exception("Invalid parameters accepted");}catch(InvalidOperationException){}
                if(scene.rootCount!=count)throw new Exception("Invalid build mutated scene");
                report.AppendLine("PASS invalid parameters rejected before mutation.");
                var obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(obstruction,scene);obstruction.transform.SetParent(b.transform,false);
                obstruction.transform.localPosition=new Vector3(-.75f,1.8f,2.85f);
                Physics.SyncTransforms();
                if(!BuildingGenerator.Validate(b).Any(x=>x.Contains("Заблокирован марш")))throw new Exception("Validator missed a blocked stair");
                Object.DestroyImmediate(obstruction);Physics.SyncTransforms();
                report.AppendLine("PASS injected stair obstruction detected, then removed.");
                // Test an actual path across each intermediate landing, including the U turn.
                Physics.SyncTransforms();int probes=0;
                for(int e=0;e<4;e++)for(int f=0;f<4;f++)for(int n=0;n<=15;n++){
                    var foot=new Vector3(e*9-.75f+n*.1f,f*3+1.565f,4.95f);
                    if(Physics.OverlapCapsule(foot+Vector3.up*.3f,foot+Vector3.up*1.5f,.29f).Any(c=>c.transform.IsChildOf(b.transform)))errors.Add("Intermediate landing blocked "+e+"/"+f+"/"+n);
                    probes++;
                }
                report.AppendLine("Intermediate turn capsule probes: "+probes+"; total errors="+errors.Count);
                int passages=0,holes=0;
                for(int e=0;e<4;e++){
                    // Stop in the lobby, then turn toward a flight; the central gap between flights is guarded.
                    for(int n=0;n<=22;n++){
                        var foot=new Vector3(e*9,.065f,-2.4f+n*.15f);
                        if(Physics.OverlapCapsule(foot+Vector3.up*.3f,foot+Vector3.up*1.5f,.29f).Any(c=>c.transform.IsChildOf(b.transform)))errors.Add("Entrance blocked "+e+"/"+n);
                        passages++;
                    }
                    for(int f=0;f<5;f++)for(int n=-7;n<=7;n++){
                        var foot=new Vector3(e*9+n*.1f,f*3+.065f,.9f);
                        if(Physics.OverlapCapsule(foot+Vector3.up*.3f,foot+Vector3.up*1.5f,.29f).Any(c=>c.transform.IsChildOf(b.transform)))errors.Add("Main landing blocked "+e+"/"+f+"/"+n);
                        passages++;
                    }
                    for(int f=1;f<5;f++){
                        // A ray just below the slab must pass through the shaft instead of meeting a full floor.
                        var origin=new Vector3(e*9-.75f,f*3-.05f,2.85f);
                        if(Physics.RaycastAll(origin,Vector3.up,.1f).Any(h=>h.collider.transform.IsChildOf(b.transform)))errors.Add("Shaft covered "+e+"/"+f);
                        holes++;
                    }
                }
                foreach(var s in b.GetComponentsInChildren<ApartmentSlot>()){
                    var start=s.transform.TransformPoint(new Vector3(0,1.05f,-.6f));
                    if(!Physics.RaycastAll(start,s.transform.forward,1.2f).Any(h=>h.collider.transform.IsChildOf(s.doorHinge)))errors.Add("Closed door doesn't block: "+s.slotId);
                }
                report.AppendLine("Entrance capsule probes="+passages+"; shaft openings="+holes+"; closed-door rays=40");
                if(errors.Count>0)throw new Exception(string.Join("\n",errors.Take(30)));
                string prefab=BuildingGenerator.SaveNewPrefab(b,BuildingGenerator.Folder+"/DemoValidated_5F_4E.prefab");
                report.AppendLine("Saved "+prefab);
                var light=new GameObject("Sun").AddComponent<Light>();SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-30,0);
                var camera=new GameObject("OverviewCamera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.transform.position=new Vector3(43,23,-30);camera.transform.LookAt(new Vector3(13.5f,6,3));camera.farClipPlane=150;
                var path=AssetDatabase.GenerateUniqueAssetPath(BuildingGenerator.Folder+"/DemoValidated_5F_4E.unity");
                EditorSceneManager.SaveScene(scene,path);report.AppendLine("Saved "+path);
                report.AppendLine("PASS. Real controller Play Mode and visual acceptance remain manual.");
            }finally{
                Directory.CreateDirectory("Verification/ModularBuildings");File.WriteAllText("Verification/ModularBuildings/checks.txt",report.ToString());
                EditorSceneManager.CloseScene(scene,true);if(old.IsValid())SceneManager.SetActiveScene(old);
            }
        }
    }
}
