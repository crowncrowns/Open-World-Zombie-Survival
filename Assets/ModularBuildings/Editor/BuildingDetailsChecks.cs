using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieSurvival.Interaction;
using Object=UnityEngine.Object;

namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class BuildingDetailsChecks
    {
        const string DirectoryPath="Verification/ApartmentDirectEntry_20261026";
        static string Stamp(Scene scene)=>scene.isDirty+"|"+string.Join(";",scene.GetRootGameObjects().Select(g=>g.GetEntityId()+":"+g.transform.position.ToString("R")+":"+g.transform.rotation.ToString("R")));
        public static string VerifySaved(string path)
        {
            var lines=new List<string>();void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);lines.Add("PASS "+text);}
            var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var b=go.GetComponent<GeneratedBuilding>();foreach(var a in b.GetComponentsInChildren<GeneratedApartment>(true))CheckInternalDoors(b,a,Check);
                Check(BuildingApartments.ValidateFixedLobbyAndDoors(b).Count==0,"saved fixed lobby and door mounts");Check(BuildingPitchedRoof.Validate(b).Count==0,"saved roof contact/cornice/rail");
            }
            catch(Exception e){lines.Add("FAIL "+e);}
            finally{PrefabUtility.UnloadPrefabContents(go);Directory.CreateDirectory(DirectoryPath);File.WriteAllLines(DirectoryPath+"/Saved_"+Path.GetFileNameWithoutExtension(path)+".txt",lines);}
            return string.Join("\n",lines);
        }
        public static string Run(int floors,int entrances,int seed,bool capture=false)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit only");
            Directory.CreateDirectory(DirectoryPath);string file=DirectoryPath+$"/Details_F{floors}_E{entrances}_S{seed}.txt";
            var lines=new List<string>();void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);lines.Add("PASS "+text);}
            var originals=new List<Scene>();for(int i=0;i<SceneManager.sceneCount;i++){var s=SceneManager.GetSceneAt(i);if(s.isLoaded&&!EditorSceneManager.IsPreviewScene(s))originals.Add(s);}
            var stamps=originals.ToDictionary(s=>s.handle,Stamp);var active=SceneManager.GetActiveScene();
            var profile=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildingSeriesProfile>(BuildingGenerator.Folder+"/CityProfileV6.asset"));profile.citySeed=seed;
            var preview=EditorSceneManager.NewPreviewScene();File.WriteAllText(file,"BEGIN "+DateTime.UtcNow.ToString("O"));
            try
            {
                var b=BuildingGenerator.Build(profile,1980,floors,entrances,$"seven-details-F{floors}-E{entrances}-S{seed}",preview);File.AppendAllText(file,"\nBUILT "+DateTime.UtcNow.ToString("O"));
                Check(BuildingGenerator.Validate(b).Count==0,"production validation: "+string.Join(";",BuildingGenerator.Validate(b)));
                Check(b.GetComponentsInChildren<ApartmentSlot>(true).Length==Math.Min(floors,4)*entrances*3,"fixed V5 lobby slots on all accessible floors");
                var apartments=b.GetComponentsInChildren<GeneratedApartment>(true);
                Check(BuildingApartments.ValidateNoGeneratedWalls(b).Count==0,"only source apartment geometry behind entrance doors");
                foreach(var apartment in apartments)
                {
                    var slot=apartment.GetComponentInParent<ApartmentSlot>();
                    Check(Vector3.Distance(apartment.prefabInstance.transform.TransformPoint(apartment.layout.entryFloorPosition),slot.transform.position)<.001f,"source entrance exactly meets lobby door");
                    Check(apartment.connectorRegions.Length==0&&apartment.connectorPath.Length==0,"no generated vestibule or corridor");
                    Check(apartment.GetComponentsInChildren<Collider>(true).All(c=>c.transform.IsChildOf(apartment.prefabInstance.transform)),"no added colliders outside source prefab");
                    CheckInternalDoors(b,apartment,Check);
                }
                var injected=GameObject.CreatePrimitive(PrimitiveType.Cube);injected.name="Regression probe — forbidden wall";injected.transform.SetParent(apartments[0].transform,false);
                Check(BuildingApartments.ValidateNoGeneratedWalls(b).Count>0,"validator rejects a generated wall outside source prefab");
                injected.transform.SetParent(apartments[0].prefabInstance.transform,false);
                Check(BuildingApartments.ValidateNoGeneratedWalls(b).Count>0,"validator rejects geometry added inside prefab instance");
                Object.DestroyImmediate(injected);Check(BuildingApartments.ValidateNoGeneratedWalls(b).Count==0,"clean source geometry accepted after probe removal");
                var balconies=b.GetComponentsInChildren<PostApocalypse.Balconies.BalconyKitModule>(true);
                if(balconies.Length>0)
                {
                    var sources=balconies.Select(m=>AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(m.gameObject))).Distinct().OrderBy(p=>p).ToArray();
                    Check(sources.Length==3&&sources.All(p=>p.EndsWith("_A.prefab")||p.EndsWith("_B.prefab")||p.EndsWith("_C.prefab")),"balcony A/B/C prefab variation");
                    var modules=BuildingExteriorMaterials.Floors(b).SelectMany(f=>f.Cast<Transform>()).Where(t=>t.name.StartsWith("Front_")||t.name.StartsWith("Rear_")).ToArray();
                    var surfaces=modules.SelectMany(m=>m.GetComponentsInChildren<MeshRenderer>(true)).Where(BuildingExteriorMaterials.IsBalconyWall).ToArray();
                    var facadeWalls=new HashSet<MeshRenderer>(BuildingExteriorMaterials.Walls(b));
                    var protectedMaterials=modules.SelectMany(m=>m.GetComponentsInChildren<MeshRenderer>(true)).Where(r=>!BuildingExteriorMaterials.IsBalconyWall(r)&&!facadeWalls.Contains(r)).ToDictionary(r=>r,r=>r.sharedMaterials);
                    Check(surfaces.Length==balconies.Length*3&&surfaces.All(r=>r.sharedMaterials.All(m=>m==profile.facadeMaterial)),"all balcony wall pieces match facade");
                    BuildingExteriorMaterials.Apply(b,profile.stairWallMaterial,false,false);
                    Check(surfaces.All(r=>r.sharedMaterials.All(m=>m==profile.stairWallMaterial)),"later Inspector facade override includes balcony exits");
                    Check(protectedMaterials.All(p=>p.Key.sharedMaterials.SequenceEqual(p.Value)),"glass, frames and hardware materials preserved");
                    BuildingExteriorMaterials.Apply(b,profile.facadeMaterial,false,false);
                }
                Check(BuildingPitchedRoof.Validate(b).Count==0,"roof/cornice/vent-support/rail contact validation");
                if(capture)
                {
                    Capture(preview,b,new Vector3((entrances-1)*BuildingFootprint.SectionWidth(b),1.6f,-.7f),new Vector3((entrances-1)*BuildingFootprint.SectionWidth(b),1.5f,5),file.Replace(".txt","_lobby.png"),true);
                    var a=apartments.First();var inward=a.prefabInstance.transform.TransformDirection(a.layout.inwardDirection);
                    var entry=b.transform.TransformPoint(a.entryLocal)+Vector3.up*1.6f;
                    Capture(preview,b,b.transform.InverseTransformPoint(entry-inward*1.1f),b.transform.InverseTransformPoint(entry+inward*3),file.Replace(".txt","_entrance.png"),true);
                    Capture(preview,b,b.transform.InverseTransformPoint(entry),b.transform.InverseTransformPoint(entry+inward*4),file.Replace(".txt","_apartment.png"),true);
                    if(floors<5)
                    {
                        var vent=b.transform.Find("Roof_Flat_2Percent/VentilationDuct_E1");var target=b.transform.InverseTransformPoint(vent.position)+new Vector3(0,.5f,.3f);
                        Capture(preview,b,target+new Vector3(2,1.4f,-4),target,file.Replace(".txt","_vent.png"),false);
                        Capture(preview,b,new Vector3(-4,floors*3-1,-5),new Vector3(0,floors*3+.3f,-1.65f),file.Replace(".txt","_cornice.png"),false);
                    }
                }
                b.transform.SetPositionAndRotation(new Vector3(3000,17,2000),Quaternion.Euler(0,37,0));
                Check(BuildingGenerator.Validate(b).Count==0,"production validation after move/rotation");
                lines.Add("INFO apartment count="+apartments.Length+"; balcony count="+balconies.Length);
            }
            catch(Exception e){lines.Add("FAIL "+e);}
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);Object.DestroyImmediate(profile);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);
                if(originals.Any(s=>!s.IsValid()||!s.isLoaded||Stamp(s)!=stamps[s.handle]))lines.Add("FAIL original scene roots/poses/dirty changed");else lines.Add("PASS original scene roots/poses/dirty retained");
                File.WriteAllLines(file,lines);
            }
            return string.Join("\n",lines);
        }
        static void CheckInternalDoors(GeneratedBuilding b,GeneratedApartment apartment,Action<bool,string> check)
        {
            foreach(var owner in apartment.prefabInstance.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Door_Single_Metal"))
            {
                var door=owner.GetComponent<DoorInteractable>();typeof(DoorInteractable).GetMethod("EnsureInitialized",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(door,null);
                var hinge=owner.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Door_Hinge");var leaf=owner.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="Door_Leaf");
                check(door.IsOpen&&Mathf.Abs(Mathf.DeltaAngle(hinge.localEulerAngles.y,95))<.01f&&Mathf.Abs(Mathf.DeltaAngle(hinge.localEulerAngles.x,0))<.01f,"mounted/open internal door "+owner.parent.parent.name);
                var box=leaf.bounds;check(Mathf.Abs(b.transform.InverseTransformPoint(new Vector3(box.center.x,box.min.y,box.center.z)).y-(apartment.floor*3+.01f))<.003f,"internal door bottom contacts frame/floor");
            }
        }
        static void Capture(Scene scene,GeneratedBuilding b,Vector3 eye,Vector3 target,string path,bool fill)
        {
            var cameraObject=new GameObject("Seven fix review camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;
            var sunObject=new GameObject("Seven fix review sun");SceneManager.MoveGameObjectToScene(sunObject,scene);var sun=sunObject.AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-35,0);
            var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                camera.fieldOfView=72;camera.aspect=1280f/720;camera.nearClipPlane=.03f;camera.farClipPlane=300;camera.transform.position=b.transform.TransformPoint(eye);camera.transform.LookAt(b.transform.TransformPoint(target));
                if(fill){var light=cameraObject.AddComponent<Light>();light.type=LightType.Point;light.range=12;light.intensity=2;}
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(sunObject);}
        }
    }
}
