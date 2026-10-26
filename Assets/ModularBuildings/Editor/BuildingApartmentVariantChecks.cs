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
using ZombieSurvival.Interaction;
using Object=UnityEngine.Object;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class BuildingApartmentVariantChecks
    {
        private static string Hash(string path)
        {if(string.IsNullOrEmpty(path)||!File.Exists(path))return "absent";using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream));}
        private static string Signature(GeneratedBuilding b)=>string.Join(";",b.GetComponentsInChildren<GeneratedApartment>(true).OrderBy(a=>a.apartmentId,StringComparer.Ordinal).Select(a=>a.apartmentId+" angle="+a.entranceAngle.ToString("R")+":"+string.Join(",",a.GetComponentsInChildren<GeneratedApartmentRoom>().Select(r=>r.kind+"/"+r.availableForLoot+"/"+r.floorSize.ToString("R")))));
        public static string Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit only");
            if(PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Close Prefab Stage");
            const string profilePath="Assets/ModularBuildings/Generated/CityProfileV6.asset";
            var source=AssetDatabase.LoadAssetAtPath<BuildingSeriesProfile>(profilePath);if(!source)throw new InvalidOperationException("Existing CityProfileV6 required; checks never create/update the source profile.");
            var original=SceneManager.GetActiveScene();var scenes=new List<Scene>();for(int i=0;i<SceneManager.sceneCount;i++){var s=SceneManager.GetSceneAt(i);if(s.isLoaded&&!EditorSceneManager.IsPreviewScene(s))scenes.Add(s);}
            var sceneHashes=scenes.Where(s=>!string.IsNullOrEmpty(s.path)).Select(s=>s.path).Distinct().ToDictionary(p=>p,Hash);
            var dirty=scenes.ToDictionary(s=>s.handle,s=>s.isDirty);var roots=scenes.ToDictionary(s=>s.handle,s=>string.Join(",",s.GetRootGameObjects().Select(g=>g.GetEntityId().ToString())));
            string sourceHash=Hash(profilePath);string scope="Assets/__V6DoorCheck_"+Guid.NewGuid().ToString("N");
            var lines=new List<string>();string first=null;var signatures=new HashSet<string>();
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);lines.Add("PASS "+message);}
            try
            {
                AssetDatabase.CreateFolder("Assets",Path.GetFileName(scope));
                for(int run=1;run<=7;run++)
                {
                    int seed=run==7?1:run;var profile=Object.Instantiate(source);profile.citySeed=seed;
                    var scene=EditorSceneManager.NewPreviewScene();
                    try
                    {
                        string randomBefore=JsonUtility.ToJson(UnityEngine.Random.state);
                        var b=BuildingGenerator.Build(profile,1980,3,1,"variant-"+seed,scene);
                        string randomAfter=JsonUtility.ToJson(UnityEngine.Random.state);
                        if(randomBefore==randomAfter)lines.Add("PASS UnityEngine.Random unchanged seed="+seed);
                        else lines.Add("INFO UnityEngine.Random changed somewhere in full builder seed="+seed+"; apartment generator uses only System.Random, inspect other generators before attributing cause");
                        var errors=BuildingApartments.Validate(b);Check(errors.Count==0,"seed="+seed+" apartment metadata/layout: "+string.Join(";",errors));
                        errors=BuildingApartments.ValidateInheritedPaths(b);Check(errors.Count==0,"seed="+seed+" inherited doorway/room paths: "+string.Join(";",errors));
                        var apartments=b.GetComponentsInChildren<GeneratedApartment>(true);var rooms=b.GetComponentsInChildren<GeneratedApartmentRoom>(true);
                        lines.Add("INFO seed="+seed+" apartments="+apartments.Length+" living="+rooms.Count(r=>r.kind==GeneratedRoomKind.Living)+" bathroom="+rooms.Count(r=>r.kind==GeneratedRoomKind.Bathroom)+" toilet="+rooms.Count(r=>r.kind==GeneratedRoomKind.Toilet)+" available="+rooms.Count(r=>r.availableForLoot));
                        string signature=Signature(b);if(run==1)first=signature;if(run==7)Check(signature==first,"same seed/building ID reproduces exact apartment/room/availability/size/angle signature");else signatures.Add(signature);
                        if(run==1)
                        {
                            string path=scope+"/DoorRoundtrip.prefab";var saved=PrefabUtility.SaveAsPrefabAsset(b.gameObject,path);Check(saved!=null,"new temporary building prefab saved");
                            var loaded=PrefabUtility.LoadPrefabContents(path);
                            try
                            {
                                var restored=loaded.GetComponentsInChildren<GeneratedApartment>(true);Check(restored.Length==apartments.Length,"prefab roundtrip apartment count");
                                var initialize=typeof(DoorInteractable).GetMethod("EnsureInitialized",BindingFlags.Instance|BindingFlags.NonPublic);
                                foreach(var apartment in restored)
                                {
                                    Check(apartment.entranceDoor!=null,"roundtrip door reference "+apartment.apartmentId);
                                    initialize.Invoke(apartment.entranceDoor,null);
                                    var slot=apartment.GetComponentInParent<ApartmentSlot>();
                                    Check(apartment.entranceDoor.IsOpen&&Mathf.Abs(Mathf.DeltaAngle(slot.doorHinge.localEulerAngles.y,apartment.entranceAngle))<.01f&&Mathf.Abs(Mathf.Abs(apartment.entranceAngle)-125)<.01f,"roundtrip initialized door open at authored125 degrees "+apartment.apartmentId);
                                }
                                Check(Signature(loaded.GetComponent<GeneratedBuilding>())==signature,"prefab roundtrip layout signature unchanged");
                            }
                            finally{PrefabUtility.UnloadPrefabContents(loaded);}
                        }
                    }
                    finally{EditorSceneManager.ClosePreviewScene(scene);Object.DestroyImmediate(profile);}
                }
                lines.Add("INFO distinct signatures among six seeded building IDs="+signatures.Count+"; random sample does not promise every allowed combination");
            }
            catch(Exception e){lines.Add("FAIL "+e);}
            finally
            {
                if(AssetDatabase.IsValidFolder(scope))AssetDatabase.DeleteAsset(scope);
                if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
                foreach(var scene in scenes)
                    if(!scene.IsValid()||!scene.isLoaded||scene.isDirty!=dirty[scene.handle]||string.Join(",",scene.GetRootGameObjects().Select(g=>g.GetEntityId().ToString()))!=roots[scene.handle])lines.Add("FAIL preexisting scene roots/dirty changed: "+scene.path);
                if(sourceHash!=Hash(profilePath)||sceneHashes.Any(p=>Hash(p.Key)!=p.Value))lines.Add("FAIL source profile / preexisting scene disk SHA changed");
                else lines.Add("PASS source profile and preexisting scene disk SHA unchanged");
            }
            return string.Join("\n",lines);
        }
    }
}
