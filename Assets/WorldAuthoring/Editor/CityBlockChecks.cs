using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieSurvival.ModularBuildings;
using ZombieSurvival.WorldAuthoring;
using ZombieSurvival.WorldAuthoring.Editor;
using Object=UnityEngine.Object;

/// <summary>Native Edit-only checks in a disposable additive scene, never rebuilds/saves an existing scene.</summary>
public static class CityBlockChecks
{
    private static string Digest(string path)
    {
        if(string.IsNullOrEmpty(path)||!File.Exists(path))return "absent";
        using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream));
    }
    private static string SceneStamp(Scene scene)
    {
        var roots=scene.GetRootGameObjects();
        return scene.handle+"|"+scene.path+"|"+scene.isDirty+"|"+roots.Length+"|"+
            roots.Sum(r=>r.GetComponentsInChildren<Transform>(true).Length)+"|"+
            roots.Sum(r=>r.GetComponentsInChildren<Component>(true).Length)+"|"+
            string.Join(";",roots.Select(r=>r.GetEntityId()+":"+r.transform.position.ToString("R")+":"+r.transform.rotation.ToString("R")+":"+r.transform.localScale.ToString("R")));
    }
    public static string Run(bool includeProfile=false)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("CityBlockChecks: Edit only");
        if(PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Close Prefab Stage before isolated checks");
        var results=new List<string>();
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);results.Add("PASS "+label);}
        var oldActive=SceneManager.GetActiveScene();var oldScenes=new List<Scene>();
        for(int i=0;i<SceneManager.sceneCount;i++){var scene=SceneManager.GetSceneAt(i);if(scene.isLoaded&&!EditorSceneManager.IsPreviewScene(scene))oldScenes.Add(scene);}
        var stamps=oldScenes.ToDictionary(s=>s.handle,SceneStamp);var hashes=oldScenes.Where(s=>!string.IsNullOrEmpty(s.path)).Select(s=>s.path).Distinct().ToDictionary(p=>p,Digest);
        const string profilePath="Assets/ModularBuildings/Generated/DemoProfileV5.asset";
        if(File.Exists(profilePath))hashes[profilePath]=Digest(profilePath);
        string folder="Assets/__CityChecks_"+Guid.NewGuid().ToString("N");
        Scene target=default;CityBlockPlan plan=null;CityBuildingCatalog catalog=null;BuildingSeriesProfile badProfile=null;
        try
        {
            AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
            target=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);target.name="CityBlockChecks isolated";SceneManager.SetActiveScene(target);
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="City check source cube";cube.transform.localScale=Vector3.one*2;
            string prefabPath=folder+"/Cube.prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(cube,prefabPath);Object.DestroyImmediate(cube);
            string cubeHash=Digest(prefabPath);
            catalog=ScriptableObject.CreateInstance<CityBuildingCatalog>();
            var entry=new CityBuildingEntry{key="cube",title="Test cube",kind=CityBuildingKind.Prefab,prefab=prefab,prefabFootprint=Vector2.one*2};
            var profile=AssetDatabase.LoadAssetAtPath<BuildingSeriesProfile>(profilePath);
            Check(profile!=null,"existing DemoProfileV5 source available");
            var profileEntry=new CityBuildingEntry{key="profile",title="Existing V5",kind=CityBuildingKind.Profile,profile=profile};
            catalog.entries.Add(entry);catalog.entries.Add(profileEntry);AssetDatabase.CreateAsset(catalog,folder+"/Catalog.asset");
            plan=ScriptableObject.CreateInstance<CityBlockPlan>();plan.catalog=catalog;
            var lot=new CityLot{title="A",entryKey="cube",position=new Vector3(10000,3,10000),size=new Vector2(4,4),yaw=30,floors=1,entrances=1};
            plan.lots.Add(lot);Check(!string.IsNullOrEmpty(plan.planId)&&!string.IsNullOrEmpty(lot.id),"new plan and lot receive stable IDs");
            string targetBefore=SceneStamp(target);var errors=CityBlockBuilder.Validate(plan,target);
            Check(errors.Count==0,"valid prefab plan: "+string.Join(";",errors));
            Check(SceneStamp(target)==targetBefore&&oldScenes.All(s=>SceneStamp(s)==stamps[s.handle])&&Digest(prefabPath)==cubeHash,"Validate has no scene/source mutation");
            void Reject(Action change,Action restore,string label)
            {try{change();Check(CityBlockBuilder.Validate(plan,target).Count>0,label);}finally{restore();}}
            Reject(()=>catalog.entries.Add(entry),()=>catalog.entries.RemoveAt(catalog.entries.Count-1),"duplicate catalog keys rejected");
            var second=new CityLot{title="B",entryKey="cube",position=lot.position+Vector3.right*12,size=Vector2.one*4,floors=1,entrances=1};
            plan.lots.Add(second);string secondId=second.id;
            Reject(()=>second.id=lot.id,()=>second.id=secondId,"duplicate active lot IDs rejected");
            var pose=lot.position;Reject(()=>lot.position=new Vector3(float.NaN,0,0),()=>lot.position=pose,"NaN position rejected");
            Reject(()=>lot.size=Vector2.zero,()=>lot.size=Vector2.one*4,"nonpositive lot dimensions rejected");
            Reject(()=>{second.position=lot.position+Vector3.right;second.yaw=65;},()=>{second.position=lot.position+Vector3.right*12;second.yaw=0;},"rotated XZ SAT overlap rejected");
            lot.yaw=0;second.position=lot.position+Vector3.right*4;
            Check(CityBlockBuilder.Validate(plan,target).Count==0,"exact parcel-edge touching accepted");second.position=lot.position+Vector3.right*12;lot.yaw=30;
            badProfile=ScriptableObject.CreateInstance<BuildingSeriesProfile>();badProfile.section=new SectionTemplate{templateId="unsupported"};AssetDatabase.CreateAsset(badProfile,folder+"/BadProfile.asset");
            Reject(()=>{lot.entryKey="profile";profileEntry.profile=badProfile;},()=>{lot.entryKey="cube";profileEntry.profile=profile;},"unsupported profile preflight rejected");
            Reject(()=>lot.entryKey="profile",()=>lot.entryKey="cube","profile body cannot fit undersized parcel");
            var previewCount=EditorSceneManager.previewSceneCount;int rootsBefore=target.rootCount;
            using(var preview=CityBlockBuilder.PreviewLot(plan,0,target))
            {
                Check(EditorSceneManager.IsPreviewScene(preview.Scene)&&preview.Root.scene==preview.Scene&&target.rootCount==rootsBefore,"preview isolated from target");
                Check(preview.Root.GetComponentsInChildren<CityGeneratedLot>(true).Length==1,"preview has exactly one lot marker");
            }
            Check(EditorSceneManager.previewSceneCount==previewCount,"preview scene disposed");
            lot.size=Vector2.one*1;entry.prefabFootprint=Vector2.one;
            bool boundsRefused=false;try{using(var rejected=CityBlockBuilder.PreviewLot(plan,0,target)){} }catch(InvalidOperationException){boundsRefused=true;}
            Check(boundsRefused&&target.rootCount==rootsBefore&&EditorSceneManager.previewSceneCount==previewCount,"actual full mesh/collider exceeds reserved rectangle: rejected without target mutation");
            lot.size=Vector2.one*4;entry.prefabFootprint=Vector2.one*2;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            var generated=CityBlockBuilder.GenerateSelected(plan,0,target);Undo.CollapseUndoOperations(group);
            Check(generated.scene==target&&Vector3.Distance(generated.transform.position,lot.position)<.0001f&&Quaternion.Angle(generated.transform.rotation,Quaternion.Euler(0,lot.yaw,0))<.001f&&generated.transform.localScale==Vector3.one,"generated root exact pose/unit scale");
            var content=generated.transform.GetChild(0).gameObject;
            Check(PrefabUtility.GetCorrespondingObjectFromSource(content)==prefab,"generated visual preserves prefab connection");
            var marker=generated.GetComponent<CityGeneratedLot>();
            Check(marker.planId==plan.planId&&marker.lotId==lot.id&&marker.buildingId==CityBlockBuilder.BuildingId(plan,lot),"generated marker stores exact plan/lot/building IDs");
            string generatedId=marker.buildingId;
            if(Application.dataPath.Contains("_CodexRecovery"))
            {
                Undo.PerformUndo();Check(!target.GetRootGameObjects().Any(r=>r.GetComponent<CityGeneratedLot>()?.buildingId==generatedId),"single Undo removes generated root (synchronous recovery Edit check)");
                Undo.PerformRedo();generated=target.GetRootGameObjects().Single(r=>r.GetComponent<CityGeneratedLot>()?.buildingId==generatedId);
                Check(generated.GetComponent<CityGeneratedLot>().lotId==lot.id,"Redo restores same lot ID");
            }
            else results.Add("SKIP Undo/Redo outside recovery: preexisting tools may rebuild on global Undo callbacks");
            var manual=new GameObject("manual child sentinel");manual.transform.SetParent(generated.transform,false);var manualPose=manual.transform.localPosition;
            bool duplicateRefused=false;try{CityBlockBuilder.GenerateSelected(plan,0,target);}catch(InvalidOperationException){duplicateRefused=true;}
            Check(duplicateRefused&&manual!=null&&manual.transform.parent==generated.transform&&manual.transform.localPosition==manualPose,"duplicate generation refuses and preserves original/manual child");
            Check(CityBlockBuilder.Validate(plan,target).Count==0,"own placed lot permits remaining planned lot");
            var next=CityBlockBuilder.GenerateSelected(plan,1,target);Check(next!=null&&target.GetRootGameObjects().Count(r=>r.GetComponent<CityGeneratedLot>()!=null)==2,"second selected lot added without rebuilding first");
            var foreign=new GameObject("foreign ID sentinel");var foreignMarker=foreign.AddComponent<CityGeneratedLot>();foreignMarker.buildingId=generatedId;foreignMarker.planId="foreign";foreignMarker.lotId="foreign";
            Check(CityBlockBuilder.Validate(plan,target).Count>0,"foreign loaded marker ID conflict rejected");Object.DestroyImmediate(foreign);
            // Check actual geometry of an existing unmarked modular building, not only marker IDs.
            var existing=GameObject.CreatePrimitive(PrimitiveType.Cube);existing.name="existing building bounds sentinel";existing.transform.position=lot.position;existing.AddComponent<GeneratedBuilding>().buildingId="unrelated-existing";
            Check(CityBlockBuilder.Validate(plan,target).Count>0,"existing GeneratedBuilding geometric overlap rejected");Object.DestroyImmediate(existing);
            if(includeProfile)
            {
                var profileLot=new CityLot{title="Native V5 one floor",entryKey="profile",position=new Vector3(10100,0,10100),size=new Vector2(45,55),floors=1,entrances=1};plan.lots.Add(profileLot);
                using(var preview=CityBlockBuilder.PreviewLot(plan,plan.lots.Count-1,target))
                {
                    var b=preview.Root.GetComponentInChildren<GeneratedBuilding>(true);
                    Check(b!=null&&b.floors==1&&b.entrances==1&&b.profile==profile,"actual V5 profile builder used in isolated preview");
                    Check(b.GetComponentsInChildren<ApartmentSlot>(true).Length==3&&b.transform.Find("Entrance_1")!=null,"actual V5 apartment slots and entrance hierarchy retained");
                }
                plan.lots.RemoveAt(plan.lots.Count-1);
            }
            string planId=plan.planId,lotId=lot.id;
            string planPath=folder+"/Plan.asset";AssetDatabase.CreateAsset(plan,planPath);AssetDatabase.SaveAssetIfDirty(plan);var json=EditorJsonUtility.ToJson(plan);Resources.UnloadAsset(plan);plan=AssetDatabase.LoadAssetAtPath<CityBlockPlan>(planPath);
            Check(plan!=null&&plan.planId==planId&&plan.lots[0].id==lotId&&EditorJsonUtility.ToJson(plan)==json,"plan asset fresh-load roundtrip IDs and serialized lot fields");
            Check(Digest(prefabPath)==cubeHash&&hashes.All(pair=>Digest(pair.Key)==pair.Value),"preexisting scene files/profile and test source prefab hashes unchanged");
        }
        catch(Exception error){results.Add("FAIL "+error);}
        finally
        {
            if(target.IsValid())
            {
                foreach(var root in target.GetRootGameObjects())foreach(var component in root.GetComponentsInChildren<Component>(true))if(component!=null)Undo.ClearUndo(component);
                foreach(var root in target.GetRootGameObjects())foreach(var transform in root.GetComponentsInChildren<Transform>(true))Undo.ClearUndo(transform.gameObject);
                EditorSceneManager.CloseScene(target,true);
            }
            if(oldActive.IsValid()&&oldActive.isLoaded)SceneManager.SetActiveScene(oldActive);
            if(AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
            if(plan!=null&&!EditorUtility.IsPersistent(plan))Object.DestroyImmediate(plan);
            if(catalog!=null&&!EditorUtility.IsPersistent(catalog))Object.DestroyImmediate(catalog);
            if(badProfile!=null&&!EditorUtility.IsPersistent(badProfile))Object.DestroyImmediate(badProfile);
            foreach(var scene in oldScenes)
            {
                if(!scene.IsValid()||!scene.isLoaded||SceneStamp(scene)!=stamps[scene.handle])results.Add("FAIL preexisting loaded scene fingerprint changed: "+scene.path);
            }
            if(hashes.Any(pair=>Digest(pair.Key)!=pair.Value))results.Add("FAIL preexisting scene/profile file SHA changed");
        }
        return string.Join("\n",results);
    }
}
