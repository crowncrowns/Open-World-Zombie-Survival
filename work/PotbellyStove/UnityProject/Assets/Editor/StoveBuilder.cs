using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using ZombieSurvival.EnvironmentProps;

public static class StoveBuilder
{
    public const string Root="Assets/Environment/Props/PotbellyStove";
    public static string Proof=>Path.GetFullPath("../../../Verification/PotbellyStove");
    static Dictionary<string,Material> mats=new();
    public static void Build()
    {
        Directory.CreateDirectory(Proof);
        foreach(var d in new[]{"Materials","VFX","Prefabs","Demo","Animations"})Directory.CreateDirectory(Root+"/"+d);
        AssetDatabase.Refresh();
        foreach(var file in Directory.GetFiles(Root+"/Textures","*.png"))
        {
            var t=(TextureImporter)AssetImporter.GetAtPath(file);t.maxTextureSize=4096;t.mipmapEnabled=true;
            t.textureType=file.Contains("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
            t.sRGBTexture=file.Contains("BaseColor")||file.Contains("Emission");
            t.alphaIsTransparency=file.Contains("Opacity");t.wrapMode=file.Contains("Opacity")?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
            t.textureCompression=TextureImporterCompression.CompressedHQ;t.SaveAndReimport();
        }
        foreach(var n in new[]{"Stove_Metal","Stove_Rust","Embers","Ash","Fire","Smoke","Sparks"})Mat(n);
        foreach(var file in Directory.GetFiles(Root+"/Models","*.fbx"))
        {
            var mi=(ModelImporter)AssetImporter.GetAtPath(file);mi.globalScale=1;mi.useFileScale=true;mi.bakeAxisConversion=true;
            mi.importCameras=false;mi.importLights=false;mi.importAnimation=false;mi.isReadable=true;
            mi.importNormals=ModelImporterNormals.Import;mi.importTangents=ModelImporterTangents.CalculateMikk;mi.generateSecondaryUV=false;
            mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;mi.materialLocation=ModelImporterMaterialLocation.InPrefab;mi.SaveAndReimport();
        }
        SetupPipeline();
        var root=new GameObject("PF_PotbellyStove_Burning");root.SetActive(false);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/PotbellyStove_LODs.fbx"));model.name="Stove_Model";model.transform.SetParent(root.transform,false);
        model.transform.localRotation=Quaternion.Euler(0,180,0);
        foreach(var r in model.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterials=r.sharedMaterials.Select(m=>mats[m.name.Replace(" (Instance)","")]).ToArray();
        }
        var lods=new List<LOD>();
        for(int i=0;i<3;i++)
        {
            var t=model.GetComponentsInChildren<Transform>().First(t=>t.name=="LOD"+i);
            lods.Add(new LOD(new[]{.24f,.10f,.025f}[i],t.GetComponentsInChildren<Renderer>()));
        }
        var group=root.AddComponent<LODGroup>();group.SetLODs(lods.ToArray());group.RecalculateBounds();group.fadeMode=LODFadeMode.None;
        var vfx=Child(root,"VFX");var flames=new List<ParticleSystem>();
        for(int i=0;i<4;i++)
        {
            var p=Particle(vfx,"Fire_VFX"+(i==0?"":"_"+i),new Vector3((i-1.5f)*.045f,.225f,.13f),"Fire");
            var main=p.main;main.startLifetime=new ParticleSystem.MinMaxCurve(.5f,.85f);main.startSpeed=new ParticleSystem.MinMaxCurve(.10f,.18f);main.startSize3D=true;
            main.startSizeX=new ParticleSystem.MinMaxCurve(.035f,.075f);main.startSizeY=new ParticleSystem.MinMaxCurve(.10f,.22f);main.startSizeZ=.05f;main.maxParticles=48;
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.45f,.04f,.7f),new Color(1,.85f,.24f,.9f));
            var noise=p.noise;noise.enabled=true;noise.strength=.016f;noise.frequency=5;noise.scrollSpeed=.7f;
            Lifetime(p,new Color(1,.9f,.35f,0),new Color(1,.5f,.035f,.8f),new Color(.9f,.08f,.005f,0));
            flames.Add(p);
        }
        var smoke=Particle(vfx,"Smoke_VFX",new Vector3(.15f,2.675f,-.125f),"Smoke");
        smoke.transform.position=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Smoke_Outlet").position;
        {
            var m=smoke.main;m.startLifetime=new ParticleSystem.MinMaxCurve(5,8);m.startSpeed=new ParticleSystem.MinMaxCurve(.08f,.13f);m.startSize=new ParticleSystem.MinMaxCurve(.10f,.19f);m.maxParticles=90;m.startRotation=new ParticleSystem.MinMaxCurve(-3.14f,3.14f);m.simulationSpace=ParticleSystemSimulationSpace.World;
            var n=smoke.noise;n.enabled=true;n.strength=new ParticleSystem.MinMaxCurve(.04f,.09f);n.frequency=.6f;n.scrollSpeed=.12f;n.damping=true;
            var vel=smoke.velocityOverLifetime;vel.enabled=true;vel.space=ParticleSystemSimulationSpace.Local;vel.x=.025f;vel.y=.04f;vel.z=.025f;
            var sz=smoke.sizeOverLifetime;sz.enabled=true;sz.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.5f),new Keyframe(1,3.8f)));
            Lifetime(smoke,new Color(.55f,.57f,.59f,0),new Color(.55f,.57f,.59f,.09f),new Color(.60f,.62f,.64f,0));
        }
        var sparks=Particle(vfx,"Sparks_VFX",new Vector3(0,.26f,.18f),"Sparks");
        {var m=sparks.main;m.startLifetime=new ParticleSystem.MinMaxCurve(.25f,.65f);m.startSpeed=new ParticleSystem.MinMaxCurve(.10f,.24f);m.startSize=new ParticleSystem.MinMaxCurve(.0015f,.004f);m.maxParticles=12;m.gravityModifier=.02f;m.startColor=new Color(1,.4f,.02f);Lifetime(sparks,new Color(1,.8f,.2f,0),new Color(1,.3f,.01f,1),new Color(1,.05f,0,0));}
        var lightRoot=Child(root,"Lighting");var light=Child(lightRoot,"Fire_Light").AddComponent<Light>();light.type=LightType.Point;light.transform.localPosition=new Vector3(0,.30f,.15f);light.color=new Color(1,.42f,.13f);light.range=2.8f;light.intensity=.5f;light.shadows=LightShadows.Soft;light.shadowBias=.015f;light.shadowNormalBias=.025f;
        var colRoot=Child(root,"Collision");
        var door=model.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="Door_Mesh");
        var doorCollider=door.gameObject.AddComponent<BoxCollider>();doorCollider.center=door.sharedMesh.bounds.center;doorCollider.size=door.sharedMesh.bounds.size;
        // Convex compounds leave the firebox mouth open rather than sealing it with one capsule.
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI*2/12;if(Mathf.Cos(a)>.89f)continue;
            var g=Child(colRoot,"Body_Segment");g.transform.localPosition=new Vector3(Mathf.Sin(a)*.295f,.42f,Mathf.Cos(a)*.295f);g.transform.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);
            g.AddComponent<BoxCollider>().size=new Vector3(.16f,.65f,.027f);
        }
        Box(colRoot,"Top",new Vector3(0,.754f,0),new Vector3(.64f,.022f,.64f));
        Box(colRoot,"Base",new Vector3(0,.10f,0),new Vector3(.60f,.026f,.60f));
        var pipe=Child(colRoot,"Chimney_Collider");pipe.transform.localPosition=new Vector3(0,1.73f,-.125f);var cap=pipe.AddComponent<CapsuleCollider>();cap.radius=.076f;cap.height=1.88f;
        var controller=root.AddComponent<StoveController>();controller.Configure(flames.ToArray(),smoke,sparks,light,root.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m.name=="M_Embers")).ToArray());
        root.SetActive(true);
        foreach(var ps in new[]{flames[0],smoke,sparks})PrefabUtility.SaveAsPrefabAsset(ps.gameObject,Root+"/VFX/"+ps.name+".prefab");
        PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/PF_PotbellyStove_Burning.prefab");
        UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        Demo();Validate();
        AssetDatabase.ExportPackage(Root,Path.GetFullPath("../../../outputs/PotbellyStove_Unity.unitypackage"),ExportPackageOptions.Recurse);
        Debug.Log("STOVE_BUILD_PASS");
    }
    static GameObject Child(GameObject parent,string n){var g=new GameObject(n);g.transform.SetParent(parent.transform,false);return g;}
    static void Box(GameObject parent,string n,Vector3 p,Vector3 s){var g=Child(parent,n);g.transform.localPosition=p;g.AddComponent<BoxCollider>().size=s;}
    static Texture2D Tex(string n)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+n+".png");
    static Material Mat(string n)
    {
        bool particle=n=="Fire"||n=="Smoke"||n=="Sparks";
        var shader=Shader.Find(particle?"Universal Render Pipeline/Particles/Unlit":"Universal Render Pipeline/Lit");if(!shader)throw new Exception("Shader missing");
        var m=new Material(shader){name="M_"+n,enableInstancing=true};
        if(particle)
        {
            m.SetTexture("_BaseMap",Tex(n=="Smoke"?"Smoke_Opacity":"Flame_Opacity"));
            m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);
            m.SetColor("_BaseColor",n=="Smoke"?Color.white:new Color(2,1.5f,1));m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;
        }
        else
        {
            m.SetTexture("_BaseMap",Tex(n=="Embers"?"Embers_BaseColor":"Stove_BaseColor"));
            m.SetTexture("_BumpMap",Tex("Stove_Normal"));m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",n=="Ash"?.3f:.7f);
            m.SetTexture("_MetallicGlossMap",Tex("Stove_UnityMask"));m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Metallic",1);m.SetFloat("_Smoothness",1);
            m.SetTexture("_OcclusionMap",Tex("Stove_AO"));m.EnableKeyword("_OCCLUSIONMAP");
            if(n=="Ash"||n=="Embers"){m.DisableKeyword("_METALLICSPECGLOSSMAP");m.SetTexture("_MetallicGlossMap",null);m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.08f);}
            m.SetColor("_BaseColor",n=="Stove_Rust"?new Color(.8f,.64f,.48f):n=="Ash"?new Color(.3f,.3f,.3f):Color.white);
            if(n=="Embers"){m.EnableKeyword("_EMISSION");m.SetTexture("_EmissionMap",Tex("Embers_Emission"));m.SetColor("_EmissionColor",new Color(4,4,4));m.SetFloat("_Smoothness",.05f);}
        }
        var path=Root+"/Materials/M_"+n+".mat";if(AssetDatabase.LoadAssetAtPath<Material>(path))AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(m,path);mats[n]=m;return m;
    }
    static ParticleSystem Particle(GameObject parent,string n,Vector3 pos,string material)
    {
        var g=Child(parent,n);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(-90,0,0);
        var p=g.AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var m=p.main;m.loop=true;m.playOnAwake=true;m.prewarm=true;m.duration=8;m.cullingMode=ParticleSystemCullingMode.Automatic;m.simulationSpace=ParticleSystemSimulationSpace.Local;
        var em=p.emission;em.rateOverTime=material=="Smoke"?5.5f:material=="Sparks"?2.2f:23;
        var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=8;shape.radius=material=="Smoke"?.025f:.012f;shape.randomDirectionAmount=0;
        var r=g.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=mats[material];r.renderMode=ParticleSystemRenderMode.Billboard;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.localBounds=new Bounds(Vector3.zero,material=="Smoke"?new Vector3(4,4,4):Vector3.one);
        return p;
    }
    static void Lifetime(ParticleSystem p,Color first,Color middle,Color last)
    {
        var c=p.colorOverLifetime;c.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(first,0),new GradientColorKey(middle,.3f),new GradientColorKey(last,1)},new[]{new GradientAlphaKey(first.a,0),new GradientAlphaKey(middle.a,.3f),new GradientAlphaKey(last.a,1)});c.color=g;
    }
    static void SetupPipeline()
    {
        string path=Root+"/Demo/Stove_URP.asset";
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root+"/Demo/Stove_Renderer.asset");if(!renderer){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,Root+"/Demo/Stove_Renderer.asset");}
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);if(!pipeline){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,path);}pipeline.name="Stove_URP";pipeline.supportsHDR=true;pipeline.supportsCameraDepthTexture=true;
        var settings=new SerializedObject(pipeline);var shadows=settings.FindProperty("m_AdditionalLightShadowsSupported");if(shadows!=null)shadows.boolValue=true;settings.ApplyModifiedPropertiesWithoutUndo();
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;PlayerSettings.colorSpace=ColorSpace.Linear;
    }
    public static void Demo()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.25f,.28f,.33f);RenderSettings.ambientIntensity=1;
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/PF_PotbellyStove_Burning.prefab"));
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Preview_Ground";ground.transform.localScale=Vector3.one*2;
        var gm=new Material(Shader.Find("Universal Render Pipeline/Lit"));gm.color=new Color(.075f,.067f,.056f);gm.SetFloat("_Smoothness",.1f);ground.GetComponent<Renderer>().sharedMaterial=gm;
        AssetDatabase.CreateAsset(gm,Root+"/Demo/Preview_Ground.mat");
        var key=new GameObject("Preview_Key").AddComponent<Light>();key.type=LightType.Directional;key.color=new Color(1,.76f,.51f);key.intensity=2.1f;key.transform.rotation=Quaternion.Euler(40,-35,0);key.shadows=LightShadows.Soft;
        var fill=new GameObject("Preview_Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.color=new Color(.48f,.62f,1);fill.intensity=.7f;fill.transform.rotation=Quaternion.Euler(25,145,0);
        var camera=new GameObject("Preview_Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.03f,.04f);camera.fieldOfView=33;camera.nearClipPlane=.03f;camera.farClipPlane=80;
        camera.transform.position=new Vector3(1.55f,1.65f,4.8f);camera.transform.LookAt(new Vector3(0,1.35f,0));camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"/Demo/PotbellyStove_Demo.unity");
        foreach(var p in root.GetComponentsInChildren<ParticleSystem>())p.Simulate(8,true,true,true);
        Capture(camera,"Unity_Full.png",1000,1400);
        camera.transform.position=new Vector3(.82f,.67f,1.7f);camera.transform.LookAt(new Vector3(0,.39f,0));Capture(camera,"Unity_Firebox.png",1200,1000);
        camera.transform.position=new Vector3(-1.8f,1.55f,-4.5f);camera.transform.LookAt(new Vector3(0,1.35f,0));Capture(camera,"Unity_Rear.png",1000,1400);
    }
    public static void Capture(Camera camera,string name,int w,int h)
    {
        var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.Create();camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Proof,name),tex.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
    public static void Validate()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/PF_PotbellyStove_Burning.prefab");if(!prefab)throw new Exception("Prefab missing");
        var group=prefab.GetComponent<LODGroup>();var lods=group.GetLODs();if(lods.Length!=3)throw new Exception("LOD count");
        var counts=lods.Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();
        if(!(counts[0]>counts[1]&&counts[1]>counts[2]))throw new Exception("LOD reduction");
        foreach(var r in prefab.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(!m||!m.shader||!m.shader.isSupported)throw new Exception("Broken material "+r.name);
        if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab)>0)throw new Exception("Missing script");
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/PotbellyStove.fbx");var bounds=new Bounds();foreach(var r in model.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
        if(bounds.size.y<2.5f||bounds.size.y>2.9f)throw new Exception("Metric scale "+bounds.size);
        File.WriteAllText(Path.Combine(Proof,"UnityImport.json"),"{\"unity\":\""+Application.unityVersion+"\",\"pipeline\":\"URP 17.6.0\",\"triangles\":["+string.Join(",",counts)+"],\"heightMeters\":"+bounds.size.y.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"colliders\":"+prefab.GetComponentsInChildren<Collider>(true).Length+",\"materialReferences\":true,\"lodReduction\":true}");
        Debug.Log("STOVE_IMPORT_PASS "+string.Join(",",counts));
    }
}
