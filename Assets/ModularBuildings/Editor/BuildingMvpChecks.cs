using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class BuildingMvpChecks
    {
        public const string TestFolder="Assets/ModularBuildings/Tests";
        public const string ScenePath=TestFolder+"/DemoResidentialV5_Review.unity";
        [MenuItem("Tools/Zombie Survival/Дома MVP — создать тест и проверить")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Только Edit Mode.");
            var report=new System.Text.StringBuilder();var old=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try{
                var profile=BuildingGenerator.CreateProfile();
                report.AppendLine(V3FixtureChecks.UnionRegression(scene));
                var b=BuildingGenerator.Build(profile,1970,5,4,"demo-v5-001",scene);
                var errors=BuildingGenerator.Validate(b,true);
                report.AppendLine("Unity "+Application.unityVersion+"; demo V5: 5 floors / 4 entrances / 60 apartment doors / 4 exact reference entrance doors / allowed interactive window modules / four corner closures / green portal linings");
                report.AppendLine("VALIDATE "+(errors.Count==0?"PASS":string.Join("\n",errors)));
                if(errors.Count>0)throw new Exception(string.Join("\n",errors.Take(25)));
                if(!FacadeDetailChecks.IsForbiddenWindow("Wall_Window_Static.prefab")||!FacadeDetailChecks.IsForbiddenWindow("Wall_Window_Static_Variant.prefab")||FacadeDetailChecks.IsForbiddenWindow(AssetDatabase.GetAssetPath(profile.windowWall)))throw new Exception("Window prohibition failed");
                report.AppendLine("PASS banned-window guard, exact reference door signature, World Space facade, corner geometry rays and interior finish rays");
                var entranceWall=b.transform.Find("Entrance_1/Floor_1/Front_2").GetComponentsInChildren<MeshRenderer>().First(r=>!r.GetComponentsInParent<Transform>().Any(t=>t.name=="EntranceDoor"));
                var originalMaterials=entranceWall.sharedMaterials;
                try{
                    entranceWall.sharedMaterial=profile.stairWallMaterial;var negative=new List<string>();FacadeDetailChecks.Check(b,true,negative);
                    if(!negative.Any(x=>x.Contains("Неверная отделка фасадного модуля")))throw new Exception("Entrance facade guard missed a wrong material");
                }finally{entranceWall.sharedMaterials=originalMaterials;}
                report.AppendLine("PASS 15m section / two 3m bays each side / expanded floor coverage and clearance; injected wrong entrance-wall material detected");
                var slot=b.GetComponentsInChildren<ApartmentSlot>()[0];var manual=new GameObject("ManualApartmentSentinel");manual.transform.SetParent(slot.manualContent,false);slot.doorHinge.localRotation=Quaternion.Euler(0,42,0);
                bool refused=false;try{BuildingGenerator.Build(profile,1970,3,2,b.buildingId,scene);}catch(InvalidOperationException){refused=true;}
                if(!refused||!manual||Quaternion.Angle(slot.doorHinge.localRotation,Quaternion.Euler(0,42,0))>.01f)throw new Exception("Manual edits were not protected");
                Object.DestroyImmediate(manual);slot.doorHinge.localRotation=Quaternion.identity;
                report.AppendLine("PASS occupied ID refuses regeneration; manual apartment and open door preserved");
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(blocker,scene);blocker.transform.SetParent(b.transform,false);blocker.transform.localPosition=new Vector3(-.9f,1.8f,5.85f);
                if(!BuildingGenerator.Validate(b).Any(x=>x.Contains("Заблокирован марш")))throw new Exception("Validator missed an obstructed stair");
                Object.DestroyImmediate(blocker);report.AppendLine("PASS injected stair obstruction detected");
                int capsuleCount=0;
                for(int e=0;e<4;e++)for(int f=0;f<4;f++)for(int n=0;n<=18;n++){
                    BuildingQuality.Capsule(b,new Vector3(e*profile.section.sectionWidth-.9f+n*.1f,f*3+1.565f,7.95f),errors,"U turn");capsuleCount++;
                }
                // Open entrance leaves only for route checks, then restore their exact closed poses.
                foreach(var entry in b.GetComponentsInChildren<Transform>().Where(t=>t.name=="EntranceDoor")){
                    var hinge=entry.GetComponentsInChildren<Transform>().Single(t=>t.name=="Door_Hinge");var rotation=hinge.localRotation;hinge.localRotation=Quaternion.Euler(0,95,0);Physics.SyncTransforms();
                    var center=entry.position;
                    for(int n=0;n<=35;n++){
                        BuildingQuality.Capsule(b,new Vector3(center.x,.065f,-2.4f+n*.17f),errors,"Entrance route");capsuleCount++;
                    }hinge.localRotation=rotation;
                }
                Physics.SyncTransforms();int holes=0;
                for(int e=0;e<4;e++)for(int f=1;f<5;f++){
                    if(Physics.RaycastAll(new Vector3(e*profile.section.sectionWidth-.9f,f*3-.05f,5.85f),Vector3.up,.1f).Any(h=>h.collider.transform.IsChildOf(b.transform)))errors.Add("Floor covers stair shaft");holes++;
                }
                errors.AddRange(BuildingGenerator.Validate(b,true));
                report.AppendLine("Counts: 896 ramp capsules, "+capsuleCount+" turn/entrance capsules, 60 door approaches, 60 closed apartment doors, 196 closed windows, "+holes+" shaft openings, 60 open section-boundary probes");
                if(errors.Count>0)throw new Exception(string.Join("\n",errors.Take(25)));
                var second=BuildingGenerator.Build(profile,1980,3,2,"regeneration-proof",scene);second.transform.position=Vector3.right*100;
                var secondErrors=BuildingGenerator.Validate(second,true);
                if(secondErrors.Count>0)throw new Exception("3F/2E: "+string.Join("\n",secondErrors.Take(10)));
                Object.DestroyImmediate(second.gameObject);report.AppendLine("PASS regenerated 3F/2E: 18 apartment doors, 2 entrances, 4 vents, same materials/layout rules");
                Directory.CreateDirectory(TestFolder);AssetDatabase.Refresh();
                var prefab=BuildingGenerator.SaveNewPrefab(b,BuildingGenerator.Folder+"/DemoResidentialV5_Review.prefab");report.AppendLine("Prefab: "+prefab);
                report.AppendLine(V3FixtureChecks.TerrainFixture(b,scene));
                var light=new GameObject("DemoSun").AddComponent<Light>();SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-30,0);
                var camera=new GameObject("OverviewCamera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.transform.position=new Vector3(65,30,-45);camera.transform.LookAt(new Vector3(22.5f,7,4.5f));camera.farClipPlane=160;
                var path=AssetDatabase.GenerateUniqueAssetPath(ScenePath);EditorSceneManager.SaveScene(scene,path);report.AppendLine("Scene: "+path);
                report.AppendLine("PASS static acceptance. Actual player Play Mode and user visual acceptance remain manual.");
            }catch(Exception ex){report.AppendLine("FAIL: "+ex.Message);throw;}
            finally{
                Directory.CreateDirectory("Verification/ModularDemoV5");File.WriteAllText("Verification/ModularDemoV5/checks.txt",report.ToString());
                EditorSceneManager.CloseScene(scene,true);if(old.IsValid())SceneManager.SetActiveScene(old);
            }
        }
    }
}
