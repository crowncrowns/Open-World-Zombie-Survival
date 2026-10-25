using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class BenchRuntimeValidation
{
    const string Key="BenchRU.NativeFrame.Active";
    static RenderTexture target;
    static int startFrame;
    static readonly List<string> errors=new List<string>();
    [Serializable] class Result { public string result; public int frames; public int benches; public string pipeline; public string gpuResidentDrawer; public string[] errors; }
    static BenchRuntimeValidation()
    {
        if(SessionState.GetBool(Key,false)) { EditorApplication.update+=Tick; Application.logMessageReceived+=Log; }
    }
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').EndsWith("/work/Benches_RU/UnityProject/Assets",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Isolated bench project required.");
        EditorSceneManager.OpenScene("Assets/Environment/Props/Benches_RU/Scenes/Demo_Benches_RU.unity");
        SessionState.SetBool(Key,true);
        SessionState.SetString(Key+".Start",DateTime.UtcNow.Ticks.ToString());
        EditorApplication.update-=Tick; EditorApplication.update+=Tick;
        Application.logMessageReceived-=Log; Application.logMessageReceived+=Log;
        EditorApplication.isPlaying=true;
    }
    static void Log(string message,string stack,LogType type)
    { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors.Add(message+"\n"+stack); }
    static void Tick()
    {
        try
        {
            if(!SessionState.GetBool(Key,false)) return;
            var begin=new DateTime(long.Parse(SessionState.GetString(Key+".Start","0")),DateTimeKind.Utc);
            if((DateTime.UtcNow-begin).TotalSeconds>180) throw new TimeoutException("Native Play frame capture timed out.");
            if(!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            var camera=Camera.main;
            if(camera==null) return;
            if(target==null)
            {
                target=new RenderTexture(1600,1100,24,RenderTextureFormat.ARGB32); target.Create();
                camera.targetTexture=target; startFrame=Time.frameCount; return;
            }
            if(Time.frameCount-startFrame<24) return;
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","Verification"));
            var old=RenderTexture.active; RenderTexture.active=target;
            var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(folder,"RuntimeFrame_GPUResident.png"),pixels.EncodeToPNG());
            RenderTexture.active=old;
            var pipeline=QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            var count=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Count(go=>go.name.StartsWith("Bench_RU_",StringComparison.Ordinal));
            var report=new Result { result=errors.Count==0 && count==15 ? "PASS":"FAIL", frames=Time.frameCount-startFrame, benches=count,
                pipeline=pipeline!=null ? pipeline.name:"missing", gpuResidentDrawer=pipeline!=null ? pipeline.gpuResidentDrawerMode.ToString():"missing",errors=errors.ToArray() };
            File.WriteAllText(Path.Combine(folder,"RuntimeFrameAudit.json"),JsonUtility.ToJson(report,true));
            SessionState.SetBool(Key,false); EditorApplication.update-=Tick;
            Debug.Log("BENCH_RU_NATIVE_FRAME_"+report.result+" frames="+report.frames+" benches="+count);
            EditorApplication.Exit(report.result=="PASS"?0:1);
        }
        catch(Exception exception)
        {
            SessionState.SetBool(Key,false); EditorApplication.update-=Tick;
            Debug.LogException(exception); EditorApplication.Exit(1);
        }
    }
}
