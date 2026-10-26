using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
    public static class ApartmentPrefabChecks
    {
        private static string Hash(string path){if(string.IsNullOrEmpty(path)||!File.Exists(path))return "absent";using(var sha=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(f));}
        public static string RunCase(int floors,int entrances,int seed,bool capture=true)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit only");
            const string sourcePath="Assets/Maps/Buildings/Apartments Prefabs/Apart_Variant_1.prefab";
            const string profilePath="Assets/ModularBuildings/Generated/CityProfileV6.asset";
            var source=AssetDatabase.LoadAssetAtPath<BuildingSeriesProfile>(profilePath);if(!source||!source.apartmentLayout)throw new InvalidOperationException("Existing V6 profile/layout required");
            string directory="Verification/ApartmentDirectEntry_20261026";Directory.CreateDirectory(directory);string name=$"F{floors}_E{entrances}_S{seed}";
            var lines=new List<string>();void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);lines.Add("PASS "+label);}
            var oldActive=SceneManager.GetActiveScene();var scenes=new List<Scene>();for(int i=0;i<SceneManager.sceneCount;i++){var scene=SceneManager.GetSceneAt(i);if(scene.isLoaded&&!EditorSceneManager.IsPreviewScene(scene))scenes.Add(scene);}
            var stamps=scenes.ToDictionary(s=>s.handle,s=>s.isDirty+"|"+string.Join(";",s.GetRootGameObjects().Select(r=>r.GetEntityId()+":"+r.transform.position.ToString("R")+":"+r.transform.rotation.ToString("R"))));
            var disk=scenes.Where(s=>!string.IsNullOrEmpty(s.path)).Select(s=>s.path).Distinct().ToDictionary(p=>p,Hash);disk[sourcePath]=Hash(sourcePath);disk[profilePath]=Hash(profilePath);
            var preview=EditorSceneManager.NewPreviewScene();var profile=Object.Instantiate(source);profile.citySeed=seed;
            try
            {
                File.WriteAllText(Path.Combine(directory,name+".txt"),DateTime.UtcNow.ToString("O")+" BEGIN Build\n");
                var b=BuildingGenerator.Build(profile,1980,floors,entrances,"prefab-review-"+name,preview);
                File.AppendAllText(Path.Combine(directory,name+".txt"),DateTime.UtcNow.ToString("O")+" AFTER Build\n");
                Physics.SyncTransforms();var errors=BuildingGenerator.Validate(b);
                File.AppendAllText(Path.Combine(directory,name+".txt"),DateTime.UtcNow.ToString("O")+" AFTER Validate errors="+errors.Count+"\n");Check(errors.Count==0,"production Validate=0: "+string.Join(";",errors));
                lines.Add("INFO production validator includes apartment metadata, continuous slabs and inherited entry/lobby/room paths");
                float sectionWidth=BuildingFootprint.SectionWidth(profile,entrances),totalWidth=sectionWidth*entrances;
                Check(b.generatedSectionWidth==sectionWidth,"authored section width="+sectionWidth);
                var slab=b.transform.Find("ContinuousFloorSlabs/FloorSlab_1").GetComponent<MeshFilter>().sharedMesh;
                Check(Mathf.Abs(slab.bounds.size.x-(totalWidth-.4f))<.002f,"slab spans full body width");
                var weather=b.transform.Find("Roof_Flat_2Percent/WeatherRoof").GetComponent<MeshFilter>().sharedMesh;
                Check(Mathf.Abs(weather.bounds.size.x-(totalWidth+.4f))<.002f,"flat weather roof spans body and overhang");
                var pitched=b.transform.Find("CityDetails_V6").Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith("Скатная кровля"));
                if(pitched)Check(Mathf.Abs(pitched.GetComponent<MeshFilter>().sharedMesh.bounds.size.x-(totalWidth+1))<.002f,"pitched roof spans full body width");
                if(entrances==1)
                {
                    Check(b.transform.Find("Entrance_1/Floor_1").Cast<Transform>().Count(t=>t.name.StartsWith("Front_"))==7,"single entrance uses seven complete facade bays");
                    foreach(var balcony in b.GetComponentsInChildren<PostApocalypse.Balconies.BalconyKitModule>(true))Check(Mathf.Abs(Mathf.Abs(b.transform.InverseTransformPoint(balcony.transform.position).x)-9)<.002f,"balcony is on an outer bay");
                    foreach(var outlet in b.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Нижний слив — ОТ дома"))Check(Mathf.Abs(Mathf.Abs(b.transform.InverseTransformPoint(outlet.position).x)-10.5f)<.002f,"downpipe reaches widened facade corner");
                }
                var apartments=b.GetComponentsInChildren<GeneratedApartment>(true);Check(apartments.Length>=1&&apartments.Length<=3,"whole prefab apartment count="+apartments.Length);
                foreach(var apartment in apartments)
                {
                    Check(apartment.sourcePrefab==source.apartmentLayout.prefab&&PrefabUtility.GetCorrespondingObjectFromSource(apartment.prefabInstance)==apartment.sourcePrefab,"nested original source link "+apartment.apartmentId);
                    Check((apartment.prefabInstance.transform.localScale-Vector3.one).sqrMagnitude<1e-8f,"unit prefab scale "+apartment.apartmentId);
                    var walls=apartment.prefabInstance.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).Select(r=>BoundsIn(b.transform,r)).Where(r=>r.size.y>2.8f).ToArray();
                    Check(walls.Length>0,"full-height structural wall samples exist");float min=walls.Min(r=>r.min.y);
                    Check(walls.All(w=>Mathf.Abs(w.min.y-apartment.floor*3)<.003f),"all structural walls contact floor "+apartment.apartmentId+" actualMin="+min.ToString("F6")+" expected="+(apartment.floor*3));
                }
                for(int i=0;i<apartments.Length;i++)for(int j=i+1;j<apartments.Length;j++)if(apartments[i].floor==apartments[j].floor)
                    Check(!apartments[i].occupiedRegions.Any(a=>apartments[j].occupiedRegions.Any(c=>a.xMin<c.xMax&&a.xMax>c.xMin&&a.yMin<c.yMax&&a.yMax>c.yMin)),"no occupied-region overlap "+i+"/"+j);
                if(capture)
                {
                    Capture(preview,b,Path.Combine(directory,name+"-exterior.png"),false);
                    Capture(preview,b,Path.Combine(directory,name+"-interior.png"),true);
                    lines.Add("INFO native URP exterior/interior captures written; visual acceptance is separate");
                }
                if(floors==3&&(entrances==1||entrances==2)&&seed==25)
                {
                    Check(source.citySeed==25,"persistent original profile seed25 matches review");b.profile=source;
                    string path=AssetDatabase.GenerateUniqueAssetPath(BuildingGenerator.Folder+"/CityV6_DirectApartment_Review_3F_"+entrances+"E.prefab");
                    Check(PrefabUtility.SaveAsPrefabAsset(b.gameObject,path)!=null,"new review prefab saved: "+path);
                    var loaded=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var restored=loaded.GetComponentsInChildren<GeneratedApartment>(true);Check(restored.Length==apartments.Length,"review prefab apartment count retained");
                        foreach(var apartment in restored)
                        {
                            var door=apartment.entranceDoor;typeof(DoorInteractable).GetMethod("EnsureInitialized",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(door,null);
                            var slot=apartment.GetComponentInParent<ApartmentSlot>();
                            Check(door.IsOpen&&Mathf.Abs(Mathf.DeltaAngle(slot.doorHinge.localEulerAngles.y,apartment.entranceAngle))<.01f&&Mathf.Abs(Mathf.Abs(apartment.entranceAngle)-125)<.01f,"persistent initial door125 "+apartment.apartmentId);
                            Check(apartment.layout==source.apartmentLayout&&apartment.sourcePrefab==source.apartmentLayout.prefab&&PrefabUtility.GetCorrespondingObjectFromSource(apartment.prefabInstance)==apartment.sourcePrefab&&(apartment.prefabInstance.transform.localScale-Vector3.one).sqrMagnitude<1e-8f,"persistent source/layout/nested link/scale");
                        }
                    }
                    finally{PrefabUtility.UnloadPrefabContents(loaded);}
                }
                lines.Add("INFO source prefab bytes="+new FileInfo(sourcePath).Length+" SHA256="+Hash(sourcePath));
            }
            catch(Exception error){lines.Add("FAIL "+error);}
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);Object.DestroyImmediate(profile);if(oldActive.IsValid()&&oldActive.isLoaded)SceneManager.SetActiveScene(oldActive);
                foreach(var scene in scenes)
                    if(!scene.IsValid()||!scene.isLoaded||stamps[scene.handle]!=scene.isDirty+"|"+string.Join(";",scene.GetRootGameObjects().Select(r=>r.GetEntityId()+":"+r.transform.position.ToString("R")+":"+r.transform.rotation.ToString("R"))))lines.Add("FAIL original scene roots/poses/dirty changed: "+scene.path);
                if(disk.Any(pair=>Hash(pair.Key)!=pair.Value))lines.Add("FAIL source prefab/profile or original scene disk SHA changed");else lines.Add("PASS original source/profile/scene disk SHA unchanged");
                File.WriteAllText(Path.Combine(directory,name+".txt"),string.Join("\n",lines));
            }
            return string.Join("\n",lines);
        }
        private static Bounds BoundsIn(Transform root,Renderer renderer)
        {
            var box=renderer.localBounds;bool first=true;Bounds result=default;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {var point=root.InverseTransformPoint(renderer.transform.TransformPoint(box.center+Vector3.Scale(box.extents,new Vector3(x,y,z))));if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);}return result;
        }
        private static void Capture(Scene scene,GeneratedBuilding b,string path,bool interior)
        {
            var cameraObject=new GameObject("Apartment check camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;
            var sunObject=new GameObject("Apartment check sun");SceneManager.MoveGameObjectToScene(sunObject,scene);var sun=sunObject.AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-35,0);
            var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var active=RenderTexture.active;
            try
            {
                camera.aspect=1280f/720;camera.fieldOfView=65;camera.nearClipPlane=.03f;camera.farClipPlane=500;
                if(interior)
                {
                    var apartment=b.GetComponentsInChildren<GeneratedApartment>().First();var eye=b.transform.TransformPoint(apartment.entryLocal+Vector3.up*1.6f);camera.transform.position=eye;
                    camera.transform.rotation=Quaternion.LookRotation(apartment.prefabInstance.transform.TransformDirection(apartment.layout.inwardDirection),b.transform.up);
                    var fill=cameraObject.AddComponent<Light>();fill.type=LightType.Point;fill.range=15;fill.intensity=5;
                }
                else{float section=BuildingFootprint.SectionWidth(b),width=b.entrances*section;var centre=b.transform.TransformPoint(new Vector3((b.entrances-1)*section/2,b.floors*1.5f,4.5f));camera.transform.position=centre+b.transform.TransformVector(new Vector3(width*.65f,Mathf.Max(10,b.floors*2),-width));camera.transform.LookAt(centre);}
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{RenderTexture.active=active;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(sunObject);}
        }
    }
}
