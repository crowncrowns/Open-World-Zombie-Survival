using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AdvancedGrassPainter.Editor
{
    [InitializeOnLoad]
    public static class GrassPlayVerification
    {
        const string Flag="AdvancedGrassPainter.PlayVerification";
        const string Root="Assets/Tools/AdvancedGrassPainter";
        static int first=-1,stage;
        static Color32[] still,moving;
        static double started;
        static GrassDataStorage data;
        static Camera camera;
        static GrassWindController wind;
        static GrassVerification.Report report;
        static GrassPlayVerification(){if(SessionState.GetBool(Flag,false))Hook();}
        static void Hook(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;started=EditorApplication.timeSinceStartup;}
        public static void Run()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/work/AdvancedGrassPainter/"))throw new InvalidOperationException("Isolated project only.");
            EditorSceneManager.OpenScene(Root+"/Demo/AdvancedGrassPainter_Demo.unity");SessionState.SetBool(Flag,true);Hook();EditorApplication.EnterPlaymode();
        }
        static void Check(bool condition,string name){if(!condition)throw new Exception(name);report.checks.Add("PASS "+name);Debug.Log("GRASS PLAY PASS "+name);}
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup-started>180)throw new TimeoutException("Grass play checks timed out.");
                if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
                if(first<0)
                {
                    data=UnityEngine.Object.FindAnyObjectByType<GrassDataStorage>();camera=UnityEngine.Object.FindAnyObjectByType<Camera>();wind=data.GetComponent<GrassWindController>();
                    report=new GrassVerification.Report{unity=Application.unityVersion,pipeline="URP 17.6.0 runtime"};first=Time.frameCount;
                    camera.transform.position=new Vector3(3,1.2f,-5.2f);camera.transform.LookAt(new Vector3(1,.2f,-1));wind.strength=0;
                }
                int frame=Time.frameCount-first;
                if(stage==0&&frame>24){wind.animate=false;wind.phaseTime=0;still=Capture();wind.phaseTime=3;var staticPixels=Capture();Check(Different(still,staticPixels)==0,"zero wind ignores phase changes in rendered pixels");Check(data.rendering.submittedDraws>0,"Play camera renders grass");Check(data.rendering.manager.chunks.Count>0,"Play rebuilds runtime LOD chunks");wind.strength=1;wind.phaseTime=0;moving=Capture();stage=2;}
                else if(stage==2&&frame>75)
                {
                    wind.phaseTime=0;moving=Capture();wind.phaseTime=3;var pixels=Capture();int changes=Different(moving,pixels);Check(changes>80,"GPU wind changes actual rendered pixels ("+changes+")");
                    File.WriteAllText(Root+"/Verification/WindPixelChanges.txt",changes.ToString());
                    camera.transform.position+=Vector3.one*10000;Capture();Check(data.rendering.submittedDraws==0,"distance and frustum culling submit zero far chunks");
                    camera.transform.position=new Vector3(3,1.2f,-5.2f);camera.transform.LookAt(new Vector3(1,.2f,-1));
                    foreach(GrassQuality q in Enum.GetValues(typeof(GrassQuality))){data.quality=q;Capture();Check(data.rendering.submittedDraws>0,"quality profile "+q+" renders nearby grass");}
                    long bytes=data.rendering.manager.bytes;data.enabled=false;Check(data.rendering==null,"disable disposes transient renderer");data.enabled=true;data.rendering.RebuildIfNeeded();Check(data.rendering.manager.bytes==bytes,"enable rebuilds identical mesh storage without duplication");
                    Check(data.GetComponentsInChildren<Transform>().Length==1,"no per-blade or per-chunk GameObjects");
                    Finish(true);
                }
            }
            catch(Exception ex){Debug.LogException(ex);if(report==null)report=new GrassVerification.Report();report.checks.Add(ex.ToString());Finish(false);}
        }
        static Color32[] Capture()
        {
            var rt=new RenderTexture(640,400,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
            var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();var pixels=tex.GetPixels32();
            RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);return pixels;
        }
        static int Different(Color32[] a,Color32[] b){int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>6)n++;return n;}
        static void Finish(bool success)
        {
            report.passed=success;File.WriteAllText(Root+"/Verification/Play_report.json",JsonUtility.ToJson(report,true));SessionState.SetBool(Flag,false);EditorApplication.update-=Tick;
            Debug.Log(success?"ADVANCED_GRASS_PLAY_PASS":"ADVANCED_GRASS_PLAY_FAILED");EditorApplication.Exit(success?0:1);
        }
    }
}
