using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using AdvancedGrassPainter;

public static class GrassHDRPChecks
{
    const string Root="Assets/Tools/AdvancedGrassPainter";
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/AdvancedGrassPainter/HDRPProject/"))throw new Exception("Isolated HDRP project only");
        PlayerSettings.colorSpace=ColorSpace.Linear;
        var settingsType=typeof(HDRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineGlobalSettings");settingsType.GetMethod("Ensure",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{true});
        var rp=AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(Root+"/Demo/HDRP_Test.asset");if(!rp){rp=ScriptableObject.CreateInstance<HDRenderPipelineAsset>();AssetDatabase.CreateAsset(rp,Root+"/Demo/HDRP_Test.asset");}
        GraphicsSettings.defaultRenderPipeline=rp;QualitySettings.renderPipeline=rp;
        EditorSceneManager.OpenScene(Root+"/Demo/AdvancedGrassPainter_Demo.unity");
        foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>())
        {var m=new Material(Shader.Find("HDRP/Lit"));if(r.sharedMaterial)m.SetColor("_BaseColor",r.sharedMaterial.color);r.sharedMaterial=m;}
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/GrassHDRP.shadergraph");if(!shader)throw new Exception("Missing native HDRP shader graph");
        var data=UnityEngine.Object.FindAnyObjectByType<GrassDataStorage>();data.material=AdvancedGrassPainter.Editor.GrassHDRPInstaller.Install();
        var camera=UnityEngine.Object.FindAnyObjectByType<Camera>();camera.gameObject.AddComponent<HDAdditionalCameraData>();camera.transform.position=new Vector3(3,1.2f,-5.2f);camera.transform.LookAt(new Vector3(1,.2f,-1));
        var volume=new GameObject("HDRP exposure and sky").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();
        var exposure=volume.sharedProfile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(9);
        var environment=volume.sharedProfile.Add<VisualEnvironment>(true);environment.skyType.Override(SkySettings.GetUniqueID<GradientSky>());
        var sky=volume.sharedProfile.Add<GradientSky>(true);sky.top.Override(new Color(.25f,.35f,.5f));sky.middle.Override(new Color(.5f,.55f,.6f));sky.bottom.Override(new Color(.15f,.12f,.08f));
        var sun=UnityEngine.Object.FindAnyObjectByType<Light>();sun.gameObject.AddComponent<HDAdditionalLightData>();sun.intensity=10000;
        var wind=data.GetComponent<GrassWindController>();wind.animate=false;wind.phaseTime=0;
        data.Changed();data.rendering.RebuildIfNeeded();
        for(int i=0;i<4;i++)Capture(camera,null);wind.strength=0;wind.phaseTime=0;var still=Capture(camera,null);wind.phaseTime=3;var other=Capture(camera,null);int stillChanges=0;for(int i=0;i<still.Length;i++)if(Math.Abs(still[i].r-other[i].r)+Math.Abs(still[i].g-other[i].g)+Math.Abs(still[i].b-other[i].b)>6)stillChanges++;wind.strength=1;wind.phaseTime=0;Color32[] a=Capture(camera,"HDRP_Close");wind.phaseTime=3;Color32[] b=Capture(camera,"HDRP_Wind");
        int changes=0,magenta=0;for(int i=0;i<a.Length;i++){if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>6)changes++;if(a[i].r>180&&a[i].b>180&&a[i].g<40)magenta++;}
        var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m=>m.message).ToArray();
        bool pass=errors.Length==0&&magenta==0&&data.rendering.submittedDraws>0&&changes>50&&stillChanges==0;
        File.WriteAllText(Root+"/Verification/HDRP_report.json","{\"passed\":"+pass.ToString().ToLowerInvariant()+",\"unity\":\""+Application.unityVersion+"\",\"pipeline\":\"HDRP 17.6.0\",\"windPixelChanges\":"+changes+",\"magentaPixels\":"+magenta+",\"shaderErrors\":"+errors.Length+",\"submissions\":"+data.rendering.submittedDraws+"}");
        Debug.Log("HDRP ZERO WIND PIXEL CHANGES="+stillChanges);foreach(var error in errors)Debug.LogError(error);AssetDatabase.SaveAssets();if(!pass)throw new Exception("HDRP grass render verification failed, changes="+changes+" magenta="+magenta);Debug.Log("ADVANCED_GRASS_HDRP_PASS");
    }
    static Color32[] Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(960,600,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();if(name!=null)File.WriteAllBytes(Root+"/Verification/"+name+".png",tex.EncodeToPNG());var pixels=tex.GetPixels32();RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);return pixels;
    }
}



