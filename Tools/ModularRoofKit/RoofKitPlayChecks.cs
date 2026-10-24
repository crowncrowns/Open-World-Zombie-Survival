using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.ModularRoofKit;

[InitializeOnLoad]
public static class RoofKitPlayChecks
{
    const string Key="RoofKit.PlayAudit";
    static int frames;
    [Serializable] class Result{public bool passed;public int actualPlayFrames;public List<string> checks=new();}
    static Result result;
    static string Proof=>Path.GetFullPath("../../../Verification/ModularRoofKit/play_validation.json");
    static RoofKitPlayChecks(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/ModularRoofKit/UnityProject/Assets"))throw new Exception("Isolated project only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){frames=0;EditorApplication.update+=Tick;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(File.Exists(Proof)&&JsonUtility.FromJson<Result>(File.ReadAllText(Proof)).passed?0:2);}
    }
    static void Check(bool passed,string name){if(!passed)throw new Exception(name);result.checks.Add(name);}
    static GameObject Spawn(string name,Vector3 pos)
    {
        var g=UnityEngine.Object.Instantiate(RoofKitAssemblyWindow.Find(name));g.name=name;g.transform.position=pos;return g;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        if(++frames<15||Time.frameCount<10)return;EditorApplication.update-=Tick;result=new Result{actualPlayFrames=Time.frameCount};
        try
        {
            var pipe=Spawn("Downpipe_Straight_1m",Vector3.zero);var gutter=Spawn("Gutter_Straight_1m",new Vector3(4,0,0));
            Spawn("Roof_Straight_2m",new Vector3(8,0,0));Spawn("Roof_Straight_2m",new Vector3(10,0,0));Physics.SyncTransforms();
            Check(Physics.Raycast(new Vector3(.2f,-.5f,0),Vector3.left,out var pHit,.3f)&&Mathf.Abs(pHit.point.x-.05f)<.004f,"Pipe outer shell is physical at 100 mm outside diameter");
            Check(!Physics.Raycast(new Vector3(0,.1f,0),Vector3.down,1.2f),"Pipe bore remains open along its full axis");
            Check(Physics.Raycast(new Vector3(4.5f,.2f,0),Vector3.down,out var gHit,.5f)&&gHit.point.y<-.07f,"Open gutter has physical concave bowl and inner floor");
            Check(!Physics.Raycast(new Vector3(4.5f,.035f,-.2f),Vector3.forward,.4f),"Gutter has no invisible collider lid");
            foreach(float x in new[]{9.999f,10.001f})Check(Physics.Raycast(new Vector3(x,.5f,.5f),Vector3.down,out var rHit,1)&&rHit.point.y>.08f,"Roof collision continuous on seam at x="+x);
            var end=pipe.GetComponentsInChildren<RoofSnapPoint>().Single(p=>p.name=="Out");
            var next=RoofKitAssemblyWindow.Attach(RoofKitAssemblyWindow.Find("Downpipe_Elbow_45"),end,"In","Out");
            var inlet=next.GetComponentInParent<RoofKitModule>().GetComponentsInChildren<RoofSnapPoint>().Single(s=>s.name=="In");
            Check(Vector3.Distance(inlet.transform.position,end.transform.position)<1e-5f,"Elbow socket attaches in runtime at exact pipe endpoint");
            var lod=gutter.GetComponent<LODGroup>();
            for(int i=0;i<3;i++){lod.ForceLOD(i);Check(gutter.GetComponentsInChildren<MeshCollider>().All(c=>c.enabled&&c.sharedMesh),"Static collision retained under forced LOD "+i);}lod.ForceLOD(-1);
            var m=gutter.GetComponent<RoofKitModule>();var before=m.GetComponentsInChildren<Renderer>().First().sharedMaterial;m.variationSeed=931;m.ApplyVariation();Check(ReferenceEquals(before,m.GetComponentsInChildren<Renderer>().First().sharedMaterial),"Seed variation does not instantiate or replace shared materials");
            var families=new[]{"RoofFence_Straight_0.5m","RoofFence_Straight_1m","RoofFence_Straight_2m"};
            foreach(string family in families)
            {
                var all=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Environment/ModularRoofKit/Prefabs/RoofFences"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(g=>g.GetComponent<RoofKitModule>().family==family).ToArray();
                Check(all.Length==8,"Eight independent fence states: "+family);
                var vertices=all.Select(g=>{var f=g.GetComponentsInChildren<MeshFilter>().Single(x=>x.name.EndsWith("LOD0"));return string.Join(";",f.sharedMesh.vertices.Select(v=>v.ToString("F5")));}).Distinct().Count();
                Check(vertices>=5,"At least five distinct geometric fence states: "+family);
            }
            result.passed=true;Debug.Log("ROOF_KIT_PLAY_PASS "+result.checks.Count);
        }
        catch(Exception e){Debug.LogException(e);result.checks.Add("FAIL: "+e.Message);}
        File.WriteAllText(Proof,JsonUtility.ToJson(result,true));EditorApplication.isPlaying=false;
    }
}
