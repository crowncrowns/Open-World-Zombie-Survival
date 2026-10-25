using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PostApocalypse.Lighting;

[InitializeOnLoad]
public static class LightingPackChecks
{
    const string Key="LightingPackChecks.Play";
    static List<string> checks;
    static double start;static int stage;static LightFixtureController a,b;static float firstIntensity;
    static string Proof=>LightingPackBuilder.Proof;
    static LightingPackChecks(){EditorApplication.playModeStateChanged+=State;}
    static void Require(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
    static bool Near(float a,float b)=>Mathf.Abs(a-b)<.0001f;
    static Color Emission(LightFixtureController c)
    {var e=c.emitters[0];var mpb=new MaterialPropertyBlock();e.renderer.GetPropertyBlock(mpb,e.materialIndex);return mpb.GetColor("_EmissionColor");}
    public static void Run()
    {
        LightingPackBuilder.Guard();SessionState.SetBool(Key,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.isPlaying=true;
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){checks=new List<string>();start=EditorApplication.timeSinceStartup;stage=0;EditorApplication.update+=Tick;}
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(stage==0)
            {
                var blockedSources=new List<string>();
                foreach(var f in LightingPackBuilder.Data.fixtures)
                {
                    var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LightingPackBuilder.PathFor(f)));
                    var c=go.GetComponent<LightFixtureController>();var shared=c.emitters.Select(e=>e.renderer.sharedMaterials[e.materialIndex]).ToArray();var original=shared.Select(m=>m.GetColor("_EmissionColor")).ToArray();
                    c.lightColor=Color.white;c.intensity=4;c.mode=LightFixtureController.OperatingMode.Normal;c.ApplyAt(1);
                    Require(c.lights.All(l=>l.light.enabled&&Near(l.light.intensity,4*l.weight)),f.name+" normal real lights and relative weights");
                    Physics.SyncTransforms();
                    foreach(var binding in c.lights.Where(l=>l.light.type==LightType.Spot))
                    {
                        var light=binding.light;
                        bool blocked=Physics.RaycastAll(light.transform.position+light.transform.forward*.001f,light.transform.forward,.40f).Any(hit=>hit.collider.transform.IsChildOf(go.transform));
                        if(blocked)blockedSources.Add(f.name+"/"+light.name);else checks.Add(f.name+" Spot centre ray clears own opaque housing");
                    }
                    Require(Emission(c).maxColorComponent>0,f.name+" emission nonzero");
                    c.intensity=2;c.ApplyAt(1);Require(Near(c.lights[0].light.intensity,2*c.lights[0].weight),f.name+" intensity changes light");
                    Require(Near(Emission(c).r,2*c.emitters[0].weight),f.name+" intensity changes per-slot emission");
                    c.lightEnabled=false;c.ApplyAt(1);Require(c.lights.All(l=>!l.light.enabled)&&Near(Emission(c).maxColorComponent,0),f.name+" off zero light AND emission");
                    c.lightEnabled=true;c.ApplyAt(1);Require(Near(c.EffectiveIntensity,2),f.name+" power restore retains authored intensity");
                    c.mode=LightFixtureController.OperatingMode.Weak;c.ApplyAt(1);Require(Near(c.EffectiveIntensity,.46f),f.name+" Weak factor");
                    c.mode=LightFixtureController.OperatingMode.Off;c.ApplyAt(1);Require(Near(c.EffectiveIntensity,0),f.name+" Off mode");
                    c.mode=LightFixtureController.OperatingMode.Failing;var samples=Enumerable.Range(0,400).Select(k=>c.EvaluateFactor(k*.021)).ToArray();Require(samples.Any(v=>v==0)&&samples.Any(v=>v>.5f),f.name+" Failing pauses and flashes");
                    c.mode=LightFixtureController.OperatingMode.Flickering;var flickerSamples=Enumerable.Range(0,50).Select(k=>c.EvaluateFactor(k*.13)).ToArray();Require(flickerSamples.Max()-flickerSamples.Min()>.01f,f.name+" time-varying flicker over interval");
                    Require(shared.Select((m,i)=>m.GetColor("_EmissionColor")==original[i]).All(v=>v),f.name+" shared materials untouched");
                    c.mode=LightFixtureController.OperatingMode.Normal;c.ApplyAt(0);c.enabled=false;Require(c.lights.All(l=>!l.light.enabled)&&Emission(c).maxColorComponent==0,f.name+" disable stops light and glow");
                    c.enabled=true;c.ApplyAt(0);Require(c.lights.All(l=>l.light.enabled),f.name+" enable restores");
                    UnityEngine.Object.DestroyImmediate(go);
                }
                Require(blockedSources.Count==0,"All Spot centre rays clear own housing; blocked="+string.Join(",",blockedSources));
                var fixture=LightingPackBuilder.Data.fixtures[0];
                a=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LightingPackBuilder.PathFor(fixture))).GetComponent<LightFixtureController>();
                b=UnityEngine.Object.Instantiate(a.gameObject).GetComponent<LightFixtureController>();b.transform.position=Vector3.right;
                a.lightColor=Color.red;a.intensity=1;b.lightColor=Color.blue;b.intensity=5;a.ApplyAt(0);b.ApplyAt(0);
                Require(a.lights[0].light.color==Color.red&&b.lights[0].light.color==Color.blue,"two instances independent light colors");
                Require(Emission(a).r>0&&Emission(a).b==0&&Emission(b).b>0&&Emission(b).r==0,"two instances independent MPB colors");
                Require(a.emitters[0].renderer.sharedMaterials.SequenceEqual(b.emitters[0].renderer.sharedMaterials),"instances reuse common materials");
                a.mode=b.mode=LightFixtureController.OperatingMode.Flickering;
                Require(Enumerable.Range(0,30).Any(k=>Mathf.Abs(a.EvaluateFactor(k*.1)-b.EvaluateFactor(k*.1))>.01f),"cloned fixtures have independent flicker phases");
                a.mode=LightFixtureController.OperatingMode.Normal;a.useColorTemperature=true;a.colorTemperature=2700;a.lightColor=Color.white;a.ApplyAt(0);Require(a.lights[0].light.color.r>a.lights[0].light.color.b,"temperature affects real light");
                a.castShadows=false;a.ApplyAt(0);Require(a.lights.All(l=>l.light.shadows==LightShadows.None),"shadow toggle");
                var observer=new GameObject("Distance observer");observer.transform.position=Vector3.one*100;a.distanceCulling=true;a.distanceObserver=observer.transform;a.ApplyAt(0);Require(a.lights.All(l=>!l.light.enabled),"distance culling disables dynamic sources");
                Require(Emission(a).maxColorComponent>0,"distant culling retains visible diffuser glow");observer.transform.position=a.transform.position;a.ApplyAt(0);Require(a.lights.All(l=>l.light.enabled),"distance return restores light");UnityEngine.Object.DestroyImmediate(observer);
                a.distanceCulling=false;a.useColorTemperature=false;a.lightColor=Color.white;a.castShadows=true;a.enableBuzzingSound=true;a.ApplyAt(0);Require(a.GetComponentsInChildren<AudioSource>().Any(s=>s.isPlaying&&s.spatialBlend==1),"optional original 3D electrical hum");
                a.SetEnabled(false);Require(a.GetComponentsInChildren<AudioSource>().All(s=>!s.isPlaying),"power off stops hum");a.SetEnabled(true);a.mode=LightFixtureController.OperatingMode.Flickering;
                a.ApplyNow();firstIntensity=a.EffectiveIntensity;stage=1;start=EditorApplication.timeSinceStartup;
            }
            else if(stage==1&&EditorApplication.timeSinceStartup-start>.8)
            {
                Require(!Near(a.EffectiveIntensity,firstIntensity),"actual Play Update advances flicker");
                UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(b.gameObject);
                RenderLightProof();
                var report=new LightingPackBuilder.Result{passed=true,unity=Application.unityVersion,urp="17.6.0",fixtures=LightingPackBuilder.Data.fixtures.Length,checks=checks};
                File.WriteAllText(Proof+"/unity_play_checks.json",JsonUtility.ToJson(report,true));
                SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Debug.Log("LIGHTING_PLAY_PASS "+checks.Count);EditorApplication.Exit(0);
            }
        }
        catch(Exception e){File.WriteAllText(Proof+"/unity_play_failure.txt",e.ToString());Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(1);}
    }
    static Camera Camera()
    {var c=new GameObject("Proof Camera").AddComponent<Camera>();c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.035f,.042f,.05f);c.nearClipPlane=.01f;c.farClipPlane=100;c.allowHDR=true;c.gameObject.AddComponent<UniversalAdditionalCameraData>();return c;}
    public static Texture2D Capture(Camera camera,string path,int width=1000,int height=800)
    {
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
        File.WriteAllBytes(path,tex.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);return tex;
    }
    static float Luma(Texture2D t)=>t.GetPixels().Average(c=>c.grayscale);
    static void RenderLightProof()
    {
        var floor=LightingPackBuilder.Flat("ProofFloor",new Color(.45f,.45f,.45f));
        LightingPackBuilder.Box("Receiving floor",new Vector3(0,-.05f,1),new Vector3(5,.1f,5),floor);
        LightingPackBuilder.Box("Opaque receiving wall",new Vector3(0,1.5f,-.08f),new Vector3(5,3,.16f),floor);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.015f,.015f,.015f);var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(RenderSettings.ambientLight);RenderSettings.ambientProbe=sh;
        var f=LightingPackBuilder.Data.fixtures[0];var c=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LightingPackBuilder.PathFor(f))).GetComponent<LightFixtureController>();c.transform.position=new Vector3(0,1.45f,.006f);c.GetComponent<LODGroup>().ForceLOD(0);c.intensity=9;
        var cam=Camera();cam.transform.position=new Vector3(1.4f,1.8f,2.6f);cam.transform.LookAt(new Vector3(0,.65f,.5f));
        c.mode=LightFixtureController.OperatingMode.Off;c.ApplyNow();var off=Capture(cam,Proof+"/Unity_Lighting_Off.png");
        c.mode=LightFixtureController.OperatingMode.Normal;c.ApplyNow();var on=Capture(cam,Proof+"/Unity_Lighting_Normal.png");
        float offValue=Luma(off),onValue=Luma(on);Require(onValue>offValue+.01f,"native URP render brightens receiving surfaces: "+offValue+" -> "+onValue);
        c.mode=LightFixtureController.OperatingMode.Weak;c.ApplyNow();var weak=Capture(cam,Proof+"/Unity_Lighting_Weak.png");Require(Luma(weak)>offValue&&Luma(weak)<onValue,"native Weak render between Off and Normal");
        // Opaque wall occlusion behind the mounted fixture, with shadows enabled.
        cam.transform.position=new Vector3(0,1.4f,-1.5f);cam.transform.LookAt(new Vector3(0,1.4f,0));
        var behind=Capture(cam,Proof+"/Unity_Lighting_BehindWall.png");Require(Luma(behind)<onValue*.5f,"opaque mounting wall remains dark from rear with shadows");
        cam.transform.position=new Vector3(1.4f,1.8f,2.6f);cam.transform.LookAt(new Vector3(0,.65f,.5f));c.mode=LightFixtureController.OperatingMode.Failing;c.flickerSeed=456;
        Directory.CreateDirectory(Proof+"/FailingFrames");
        for(int i=0;i<20;i++){c.ApplyAt(i*.12);var frame=Capture(cam,Proof+"/FailingFrames/Failing_"+i.ToString("00")+".png",750,600);UnityEngine.Object.DestroyImmediate(frame);}
        UnityEngine.Object.DestroyImmediate(off);UnityEngine.Object.DestroyImmediate(on);UnityEngine.Object.DestroyImmediate(weak);UnityEngine.Object.DestroyImmediate(behind);
    }
    public static void CaptureGallery()
    {
        LightingPackBuilder.Guard();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var pipe=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LightingPackBuilder.Root+"/Demo/LightingPack_URP.asset");GraphicsSettings.defaultRenderPipeline=pipe;QualitySettings.renderPipeline=pipe;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.35f,.37f,.40f);var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(RenderSettings.ambientLight);RenderSettings.ambientProbe=sh;
        var sun=new GameObject("Studio key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2.2f;sun.transform.rotation=Quaternion.Euler(38,-32,0);
        var fill=new GameObject("Studio fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.65f;fill.transform.rotation=Quaternion.Euler(15,140,0);
        var camera=Camera();camera.orthographic=true;
        Directory.CreateDirectory(Proof+"/Gallery");
        foreach(var f in LightingPackBuilder.Data.fixtures)
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LightingPackBuilder.PathFor(f)));go.GetComponent<LODGroup>().ForceLOD(0);var c=go.GetComponent<LightFixtureController>();c.SetEnabled(false);
            var bounds=new Bounds();bool first=true;foreach(var r in go.GetComponent<LODGroup>().GetLODs()[0].renderers){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));camera.orthographicSize=size*.73f;
            Vector3 view=f.mount=="ceiling"?new Vector3(1,-1.2f,1.7f):new Vector3(1,.65f,2.4f);camera.transform.position=bounds.center+view.normalized*size*3;camera.transform.LookAt(bounds.center);
            var image=Capture(camera,Proof+"/Gallery/"+f.name+".png",600,600);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);
        }
        EditorSceneManager.OpenScene(LightingPackBuilder.Root+"/Demo/LightingPack_Showcase.unity");
        var cam=UnityEngine.Object.FindFirstObjectByType<Camera>();var texture=Capture(cam,Proof+"/Unity_Showcase.png",1800,1000);UnityEngine.Object.DestroyImmediate(texture);
        Debug.Log("LIGHTING_GALLERY_PASS");
    }
}
