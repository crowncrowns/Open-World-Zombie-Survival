using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PostApocalypse.Graffiti;
using PostApocalypse.Graffiti.Editor;

[InitializeOnLoad]
public static class GraffitiPlayVerify
{
    static double due;
    static int errors;
    static GraffitiPlayVerify()
    {
        EditorApplication.playModeStateChanged+=Changed;
    }
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/GraffitiPack/"))throw new Exception("Isolation required");
        EditorSceneManager.OpenScene(GraffitiPainter.Root+"/Demo/GraffitiGallery.unity");
        var first=UnityEngine.Object.FindFirstObjectByType<GraffitiPlacement>();first.opacity=.37f;first.ApplyOpacity();
        PrefabUtility.RecordPrefabInstancePropertyModifications(first);EditorUtility.SetDirty(first);
        EditorSceneManager.MarkSceneDirty(first.gameObject.scene);EditorSceneManager.SaveScene(first.gameObject.scene,"Assets/GraffitiOpacityTest.unity");
        SessionState.SetBool("GraffitiPlayVerify",true);EditorApplication.isPlaying=true;
    }
    static void Log(string message,string stack,LogType type) {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool("GraffitiPlayVerify",false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            errors=0;Application.logMessageReceived+=Log;due=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Check;
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("GraffitiPlayVerify",false);Debug.Log("GRAFFITI_PLAY_PASS");EditorApplication.Exit(0);
        }
    }
    static void Check()
    {
        if(EditorApplication.timeSinceStartup<due)return;
        EditorApplication.update-=Check;
        try
        {
            var all=UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None);
            int expected=JsonUtility.FromJson<GraffitiBuild.Catalog>(File.ReadAllText(GraffitiPainter.Root+"/Documentation/catalog.json")).items.Length;
            if(all.Length!=expected)throw new Exception("Play instance count "+all.Length);
            bool fraction=false;
            foreach(var p in all)
            {
                if(!p.entry||!p.entry.texture||!p.GetComponent<Renderer>().sharedMaterial)throw new Exception("Play references");
                var block=new MaterialPropertyBlock();p.GetComponent<Renderer>().GetPropertyBlock(block);
                if(Mathf.Abs(block.GetColor("_BaseColor").a-p.opacity)>.001f)throw new Exception("Play property block restoration");
                if(Mathf.Abs(p.opacity-.37f)<.001f)fraction=true;
            }
            if(!fraction||errors>0)throw new Exception("Play opacity or errors: "+errors);
            File.WriteAllText(Path.GetFullPath(Application.dataPath+"/../../../../Verification/GraffitiPack/play_checks.json"),"{\"instances\":"+all.Length+",\"serializedOpacityRestored\":true,\"errorsDuringObservation\":0,\"mainGameIntegration\":false}");
            Application.logMessageReceived-=Log;EditorApplication.isPlaying=false;
        }
        catch(Exception ex){Application.logMessageReceived-=Log;Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
