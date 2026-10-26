using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WindowGrillesPlayChecks
{
    private const string Root="Assets/Environment/Windows/MetalGrilles";
    private const string ActiveKey="WindowGrilles.PlayChecks.Active";
    private const string ResultKey="WindowGrilles.PlayChecks.Result";
    private static readonly List<string> failures=new List<string>();
    private static WindowGrilleHinge[] hinges;
    private static Vector3[] roots;
    private static int stage;
    private static float stageTime;
    private static bool sawIntermediate;
    private static double started;

    static WindowGrillesPlayChecks()
    {
        EditorApplication.playModeStateChanged+=ModeChanged;
        EditorApplication.update+=Tick;
    }

    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/WindowGrilles/"))
            throw new InvalidOperationException("Play checks require the isolated WindowGrilles project.");
        EditorSceneManager.OpenScene(Root+"/DemoScene/MetalGrilles_Demo.unity");
        SessionState.SetBool(ActiveKey,true);
        SessionState.SetString(ResultKey,"");
        EditorApplication.isPlaying=true;
    }

    private static void ModeChanged(PlayModeStateChange change)
    {
        if(!SessionState.GetBool(ActiveKey,false)) return;
        if(change==PlayModeStateChange.EnteredPlayMode)
        {
            failures.Clear();
            hinges=UnityEngine.Object.FindObjectsByType<WindowGrilleHinge>();
            roots=hinges.Select(h=>h.transform.position).ToArray();
            stage=0;stageTime=Time.time;started=EditorApplication.timeSinceStartup;sawIntermediate=false;
            if(hinges.Length!=4)failures.Add("Expected four hinged demo instances, found "+hinges.Length);
        }
        else if(change==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(ActiveKey,false);
            string path=Path.GetFullPath("Assets/Validation/WindowGrilles/play_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string saved=SessionState.GetString(ResultKey,"");
            var report=string.IsNullOrEmpty(saved)?new PlayReport{status="FAIL",failures=new[]{"Play ended without a completed result"}}:
                JsonUtility.FromJson<PlayReport>(saved);
            File.WriteAllText(path,JsonUtility.ToJson(report,true));
            Debug.Log(report.status+" report="+path);
            EditorApplication.Exit(report.status=="WINDOW_GRILLES_PLAY_PASS"?0:1);
        }
    }

    private static void Tick()
    {
        if(!SessionState.GetBool(ActiveKey,false)||!EditorApplication.isPlaying||hinges==null)return;
        if(EditorApplication.timeSinceStartup-started>90)
        {failures.Add("Play checks timed out");Finish();return;}
        float elapsed=Time.time-stageTime;
        foreach(var h in hinges)
        for(int lod=0;lod<4;lod++)
        foreach(string side in new[]{"Left","Right"})
        {
            var leaf=h.transform.Find("LOD"+lod+"/"+side+"LeafPivot_LOD"+lod);
            if(leaf==null)continue;
            foreach(var renderer in leaf.GetComponentsInChildren<Renderer>())
            {
                string failure=h.name+" LOD"+lod+" "+side+" crossed demo wall plane";
                if(renderer.bounds.min.z<h.transform.position.z-.0701f&&!failures.Contains(failure))failures.Add(failure);
            }
        }
        if(stage==0&&elapsed>.20f)
        {
            CheckAngles(false);
            foreach(var h in hinges)h.Open();
            stage=1;stageTime=Time.time;
        }
        else if(stage==1)
        {
            var pivot=hinges[0].transform.Find("LOD0/LeftLeafPivot_LOD0");
            float angle=pivot!=null?Mathf.Abs(Mathf.DeltaAngle(0,pivot.localEulerAngles.y)):0;
            if(angle>5&&angle<100)sawIntermediate=true;
            if(elapsed>1.3f)
            {
                CheckAngles(true);CaptureHinged("Hinged_Open.png");
                foreach(var h in hinges)h.Close();
                stage=2;stageTime=Time.time;
            }
        }
        else if(stage==2&&elapsed>1.3f)
        {
            CheckAngles(false);CaptureHinged("Hinged_Closed.png");
            if(!sawIntermediate)failures.Add("Opening did not show intermediate angles");
            for(int i=0;i<hinges.Length;i++)
                if(Vector3.Distance(hinges[i].transform.position,roots[i])>.0001f)failures.Add("Root moved: "+hinges[i].name);
            stage=3;
            Finish();
        }
    }

    private static void Finish()
    {
        var report=new PlayReport{status=failures.Count==0?"WINDOW_GRILLES_PLAY_PASS":"FAIL",unityVersion=Application.unityVersion,
            hingedInstances=hinges!=null?hinges.Length:0,smoothIntermediate=sawIntermediate,failures=failures.ToArray()};
        SessionState.SetString(ResultKey,JsonUtility.ToJson(report));
        EditorApplication.isPlaying=false;
    }

    private static void CheckAngles(bool open)
    {
        foreach(var h in hinges)
        foreach(string prefix in new[]{"LOD0","LOD1","LOD2","LOD3","Collision"})
        foreach(string side in new[]{"Left","Right"})
        {
            Transform pivot=h.transform.Find(prefix+"/"+side+"LeafPivot"+(prefix=="Collision"?"":"_"+prefix));
            float expected=open?(side=="Left"?-105:105):0;
            if(pivot==null||Mathf.Abs(Mathf.DeltaAngle(pivot.localEulerAngles.y,expected))>.6f)
                failures.Add(h.name+" "+prefix+" "+side+" expected="+expected+" actual="+(pivot!=null?pivot.localEulerAngles.y:-999));
            if(pivot!=null&&(Mathf.Abs(pivot.localPosition.x-(side=="Left"?-.46f:.46f))>.0001f||
                Mathf.Abs(pivot.localPosition.z-.035f)>.0001f||Mathf.Abs(pivot.localPosition.y)>.0001f))
                failures.Add(h.name+" "+prefix+" "+side+" pivot does not coincide with hinge barrel axis");
        }
    }

    private static void CaptureHinged(string name)
    {
        var target=hinges.FirstOrDefault(h=>h.name=="Gallery_Grille_05_B")??hinges[0];
        var center=target.transform.position+new Vector3(0,.43f,0);
        var go=new GameObject("Hinge proof camera");
        var cam=go.AddComponent<Camera>();cam.enabled=false;cam.fieldOfView=45;cam.nearClipPlane=.03f;
        go.transform.position=center+new Vector3(.50f,.27f,1.75f);go.transform.LookAt(center);
        target.GetComponent<LODGroup>().ForceLOD(0);
        var texts=UnityEngine.Object.FindObjectsByType<TextMesh>().Select(t=>t.GetComponent<Renderer>()).ToArray();
        foreach(var text in texts)text.enabled=false;
        WindowGrillesBuilder.CaptureCamera(cam,Root+"/Verification/"+name,1400,1200);
        foreach(var text in texts)text.enabled=true;
        target.GetComponent<LODGroup>().ForceLOD(-1);
        UnityEngine.Object.Destroy(go);
    }

    [Serializable]private sealed class PlayReport
    {public string status;public string unityVersion;public int hingedInstances;public bool smoothIntermediate;public string[] failures;}
}
