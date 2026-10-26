using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieSurvival.Interaction;
using ZombieSurvival.ModularRoofKit;
using Object=UnityEngine.Object;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class BuildingCityChecks
    {
        public static string RunCase(int floors,int entrances,bool capture=true)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit only");
            var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var roots=original.GetRootGameObjects().Select(g=>g.GetEntityId().ToString()).ToArray();
            var scene=EditorSceneManager.NewPreviewScene();string report="";
            string folder=Path.GetFullPath("work/CityV6_20261025");Directory.CreateDirectory(folder);
            void Check(bool ok,string line){if(!ok)throw new InvalidOperationException(line);report+="PASS "+line+"\n";}
            try
            {
                var profile=BuildingCityDetails.CreateProfile();
                foreach(var invalid in new[]{(17,1),(16,7),(0,1)}){bool rejected=false;try{BuildingGenerator.Preflight(profile,1980,invalid.Item1,invalid.Item2);}catch(InvalidOperationException){rejected=true;}Check(rejected,"unsupported size rejected "+invalid);}
                var b=BuildingGenerator.Build(profile,1980,floors,entrances,"review-v6-"+floors+"-"+entrances,scene);
                Physics.SyncTransforms();
                Check(b.floors==floors&&b.entrances==entrances&&b.revision=="city-v6","requested dimensions and V6 revision");
                Check(b.GetComponentsInChildren<ApartmentSlot>(true).Length==Mathf.Min(floors,4)*entrances*3,"no apartment/entrance interiors above floor 4");
                int stairs=b.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="StairwellModule");
                Check(stairs==Mathf.Min(floors-1,3)*entrances,"stairs stop at floor 4");
                var blockers=b.GetComponentsInChildren<BuildingStairBlocker>(true);Check(blockers.Length==(floors>4?entrances:0),"floor 4→5 blockers per entrance");
                Check(blockers.All(m=>m.passageBlocker&&m.passageBlocker.enabled&&m.passageBlocker.size.y>2&&m.generatedVisual),"replaceable blocker visual and separate solid collision");
                foreach(var blocker in blockers)
                {
                    var box=blocker.passageBlocker;var bounds=box.bounds;
                    float section=Mathf.Round(b.transform.InverseTransformPoint(blocker.transform.position).x/15)*15;
                    for(int i=0;i<=30;i++)
                    {
                        float u=i/30f;var feet=b.transform.TransformPoint(new Vector3(section+.9f,7.5f+1.5f*u,7.2f-2.7f*u));
                        var middle=feet+Vector3.up*.965f;
                        var body=new Bounds(middle,new Vector3(.58f,1.8f,.58f));
                        Check(!bounds.Intersects(body),"blocker leaves incoming 3→4 flight clear sample "+i);
                    }
                }
                var balconies=b.GetComponentsInChildren<PostApocalypse.Balconies.BalconyKitModule>(true);
                Check(floors>5?balconies.Length==0:balconies.Length==Mathf.Max(1,floors-1)*entrances*4,"balcony floor-count rule");
                foreach(var balcony in balconies){float x=b.transform.InverseTransformPoint(balcony.transform.position).x;float centre=Mathf.Round(x/15)*15;Check(Mathf.Abs(x-centre)>=5.99f,"balcony separated from entrance by one complete module");}
                bool pitched=b.transform.Find("CityDetails_V6").Cast<Transform>().Any(t=>t.name.StartsWith("Скатная кровля"));Check(pitched==(floors<5),"1–4 pitched, 5–16 almost flat roof");
                Check(b.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("VentilationStack_E"))==entrances,"roof ventilation retained");
                var pipeModules=b.GetComponentsInChildren<RoofKitModule>(true).Where(m=>m.category=="Downpipes").ToArray();
                var outlets=b.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Нижний слив — ОТ дома").ToArray();Check(outlets.Length==4,"four drainage routes from roof to first floor");
                foreach(var end in outlets)
                {
                    var snap=end.GetComponentsInChildren<RoofSnapPoint>(true).Single(s=>s.name=="Out");var local=b.transform.InverseTransformPoint(snap.transform.position);var direction=b.transform.InverseTransformDirection(snap.transform.forward);
                    Check(local.y>=.45f&&local.y<=.65f&&(local.z<4.5f?direction.z<-.99f:direction.z>.99f),"bottom outlet at first floor points away from facade");
                }
                var points=b.GetComponentsInChildren<RoofSnapPoint>(true).Where(s=>s.kind=="Pipe").ToArray();
                foreach(var point in points.Where(s=>s.name=="In"||s.name=="Downpipe"||s.name=="Out"&&s.transform.parent.parent.name!="Нижний слив — ОТ дома"))
                {
                    bool terminal=point.name=="Out"&&point.GetComponentInParent<RoofKitModule>()?.name=="Нижний слив — ОТ дома";if(terminal)continue;
                    bool joined=points.Any(q=>q!=point&&q.GetComponentInParent<RoofKitModule>()!=point.GetComponentInParent<RoofKitModule>()&&Vector3.Distance(q.transform.position,point.transform.position)<.001f&&Vector3.Dot(q.transform.forward,point.transform.forward)<-.99f);
                    Check(joined,"pipe connection closed "+point.GetComponentInParent<RoofKitModule>()?.name+"/"+point.name);
                }
                if(capture)Capture(b,scene,folder+"/F"+floors+"_E"+entrances+".png");
                var apartmentErrors=BuildingApartments.Validate(b);report+=(apartmentErrors.Count==0?"PASS ":"FAIL ")+"apartment composition/geometry: "+string.Join(";",apartmentErrors)+"\n";
                var pathErrors=BuildingApartments.ValidateInheritedPaths(b);report+=(pathErrors.Count==0?"PASS ":"FAIL ")+"inherited collision paths: "+string.Join(";",pathErrors)+"\n";
                var productionErrors=BuildingGenerator.Validate(b);Check(productionErrors.Count==0,"production validator: "+string.Join(";",productionErrors));
                report+="INFO apartments="+b.GetComponentsInChildren<GeneratedApartment>(true).Length+" renderers="+b.GetComponentsInChildren<Renderer>(true).Length+". Edit geometry only, no Play/FPS.\n";
            }
            catch(Exception e){report+="FAIL "+e+"\n";}
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if(!original.IsValid()||!original.isLoaded||!roots.SequenceEqual(original.GetRootGameObjects().Select(g=>g.GetEntityId().ToString())))report+="FAIL original scene root identity changed\n";
                File.WriteAllText(folder+"/F"+floors+"_E"+entrances+".txt",report);
            }
            return report;
        }
        public static string CreateReview(int floors)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit only");
            var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var roots=original.GetRootGameObjects().Select(g=>g.GetEntityId().ToString()).ToArray();
            var scene=EditorSceneManager.NewPreviewScene();string report="";
            try
            {
                var profile=BuildingCityDetails.CreateProfile();
                var b=BuildingGenerator.Build(profile,1980,floors,1,"city-v6-review-"+floors,scene);
                var errors=BuildingGenerator.Validate(b);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
                string path=BuildingGenerator.SaveNewPrefab(b,BuildingGenerator.Folder+"/CityV6_Review_"+floors+"F_1E.prefab");report="PASS saved new validated review prefab "+path+"\n";
                var blocker=b.GetComponentInChildren<BuildingStairBlocker>();
                if(blocker)
                {
                    var collision=blocker.passageBlocker;var size=collision.size;var centre=collision.center;var previous=blocker.generatedVisual;
                    blocker.replacementPrefab=profile.solidWall;BuildingStairBlockerEditor.Apply(blocker,false);
                    if(previous||!blocker.generatedVisual||!PrefabUtility.IsPartOfPrefabInstance(blocker.generatedVisual)||blocker.passageBlocker!=collision||collision.size!=size||collision.center!=centre||!collision.enabled||blocker.generatedVisual.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled))throw new InvalidOperationException("Replacement damaged passage collision/ownership");
                    report+="PASS blocker model replacement preserves separate solid passage collision\n";
                }
            }
            catch(Exception e){report+="FAIL "+e+"\n";}
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if(!original.IsValid()||!original.isLoaded||!roots.SequenceEqual(original.GetRootGameObjects().Select(g=>g.GetEntityId().ToString())))report+="FAIL original scene root identity changed\n";
                File.WriteAllText("work/CityV6_20261025/Review"+floors+".txt",report);
            }
            return report;
        }
        public static string CheckPlanPreview()
        {
            var target=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots=target.GetRootGameObjects().Select(g=>g.GetEntityId().ToString()).ToArray();bool dirty=target.isDirty;
            var catalog=ScriptableObject.CreateInstance<ZombieSurvival.WorldAuthoring.CityBuildingCatalog>();
            var plan=ScriptableObject.CreateInstance<ZombieSurvival.WorldAuthoring.CityBlockPlan>();string report="";
            try
            {
                catalog.entries.Add(new ZombieSurvival.WorldAuthoring.CityBuildingEntry{key="v6",kind=ZombieSurvival.WorldAuthoring.CityBuildingKind.Profile,profile=BuildingCityDetails.CreateProfile()});plan.catalog=catalog;
                plan.lots.Add(new ZombieSurvival.WorldAuthoring.CityLot{entryKey="v6",position=new Vector3(5000,0,5000),yaw=37,floors=5,entrances=2,size=new Vector2(60,70)});
                using(var preview=ZombieSurvival.WorldAuthoring.Editor.CityBlockBuilder.PreviewLot(plan,0,target))
                {
                    var b=preview.Root.GetComponentInChildren<GeneratedBuilding>();var errors=BuildingGenerator.Validate(b);
                    if(errors.Count>0)throw new InvalidOperationException(string.Join(";",errors));
                    Capture(b,preview.Scene,"work/CityV6_20261025/Plan5F2E.png");
                    report="PASS V6 plan PreviewLot with two entrances, yaw37, translated world coordinates, complete bounds and production validator\n";
                }
            }
            catch(Exception e){report+="FAIL "+e+"\n";}
            finally
            {
                Object.DestroyImmediate(plan);Object.DestroyImmediate(catalog);
                if(target.isDirty!=dirty||!roots.SequenceEqual(target.GetRootGameObjects().Select(g=>g.GetEntityId().ToString())))report+="FAIL preexisting target changed\n";
                File.WriteAllText("work/CityV6_20261025/PlanPreview.txt",report);
            }
            return report;
        }
        static void Capture(GeneratedBuilding b,UnityEngine.SceneManagement.Scene scene,string file)
        {
            var light=new GameObject("Review sun").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-40,0);
            var camera=new GameObject("Review camera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;camera.nearClipPlane=.1f;camera.farClipPlane=500;
            var target=b.transform.TransformPoint(new Vector3((b.entrances-1)*7.5f,b.floors*1.5f,4.5f));camera.transform.position=target+b.transform.TransformDirection(new Vector3(Mathf.Max(20,b.entrances*10),b.floors*1.1f,-Mathf.Max(28,b.entrances*17+b.floors*2)));camera.transform.LookAt(target);
            var rt=new RenderTexture(1280,900,24);Texture2D tex=null;var previous=RenderTexture.active;
            try{rt.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(file,tex.EncodeToPNG());}
            finally{RenderTexture.active=previous;if(tex)Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
