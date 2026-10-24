using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using ZombieSurvival.EnvironmentProps;

public static class DumpsterBuilder
{
    public const string Root="Assets/Environment/Props/Dumpster";
    static Dictionary<string,Material> mats=new Dictionary<string,Material>();
    static string Proof=>Path.GetFullPath("../../../Verification/Dumpster");
    [MenuItem("Tools/Zombie Survival/Dumpster/Build asset prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(Proof);
        foreach(string d in new[]{"Materials","Prefabs","Controllers","Animations","Demo"}) Directory.CreateDirectory(Root+"/"+d);
        AssetDatabase.Refresh();
        foreach(var f in Directory.GetFiles(Root+"/Textures","*.png",SearchOption.AllDirectories))
        {
            string p=f.Replace('\\','/');var t=(TextureImporter)AssetImporter.GetAtPath(p);string before=EditorJsonUtility.ToJson(t);
            t.textureType=p.Contains("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
            t.sRGBTexture=p.Contains("BaseColor")||p.Contains("Stickers_Atlas");t.maxTextureSize=4096;t.mipmapEnabled=true;
            t.alphaIsTransparency=p.Contains("Stickers_Atlas");t.wrapMode=p.Contains("Stickers")?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
            t.textureCompression=TextureImporterCompression.CompressedHQ;if(before!=EditorJsonUtility.ToJson(t))t.SaveAndReimport();
        }
        foreach(string name in new[]{"Body","BodyBaked","Metal","Wheels","Garbage","BagBlue","BagGrey","BagTranslucent","Stickers"}) Material(name);
        foreach(var f in Directory.GetFiles(Root,"*.fbx",SearchOption.AllDirectories))
        {
            string p=f.Replace('\\','/');var mi=(ModelImporter)AssetImporter.GetAtPath(p);string before=EditorJsonUtility.ToJson(mi);
            mi.globalScale=1;mi.useFileScale=true;mi.bakeAxisConversion=true;mi.importCameras=false;mi.importLights=false;
            mi.isReadable=true;mi.importNormals=ModelImporterNormals.Import;mi.importTangents=ModelImporterTangents.CalculateMikk;
            mi.generateSecondaryUV=false;mi.importAnimation=p.Contains("/Animations/");mi.animationType=ModelImporterAnimationType.Generic;
            mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;mi.materialLocation=ModelImporterMaterialLocation.InPrefab;
            if(mi.importAnimation) mi.resampleCurves=true;
            if(before!=EditorJsonUtility.ToJson(mi))mi.SaveAndReimport();
        }
        var controller=Controller();
        MakePrefab(false,controller);MakePrefab(true,controller);
        Demo();AssetDatabase.SaveAssets();AssetDatabase.Refresh();Validate();

        Debug.Log("DUMPSTER_NATIVE_BUILD_PASS");
    }
    static Texture2D Tex(string folder,string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+folder+"/"+name+".png");
    static Material Material(string name)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=Shader.Find(name.StartsWith("Body")?"ZombieSurvival/Dumpster Surface":"Universal Render Pipeline/Lit");
        if(!shader)throw new Exception("Missing shader for "+name);
        if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.shader=shader;
        string f=name.StartsWith("Body")?"Body":name=="Metal"||name=="Wheels"?name:"Garbage";
        m.SetTexture("_BaseMap",name=="Stickers"?Tex("Stickers","Stickers_Atlas"):Tex(f,f+"_BaseColor"));
        m.SetColor("_BaseColor",name=="BagBlue"?new Color(.35f,.65f,1):name=="BagGrey"?new Color(2.8f,2.8f,2.5f):name=="BagTranslucent"?new Color(5,5.6f,4.7f,.83f):Color.white);
        m.SetTexture("_BumpMap",name=="BodyBaked"?Tex(f,"Body_BakedNormal"):Tex(f,f+"_Normal"));
        if(name.StartsWith("Body"))
        {
            m.SetTexture("_MicroNormal",Tex("Body","Body_Normal"));m.SetTexture("_MaskMap",Tex("Body","Body_UnityMask"));m.SetTexture("_GrimeMasks",Tex("Body","Body_GrimeMasks"));
            if(name=="BodyBaked")m.SetTexture("_BakedAO",Tex("Body","Body_BakedAO"));
        }
        else if(name=="Stickers")
        {
            m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.4f);m.SetFloat("_Cull",0);m.SetFloat("_Smoothness",.12f);m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2451;
        }
        else
        {
            m.EnableKeyword("_NORMALMAP");m.EnableKeyword("_METALLICSPECGLOSSMAP");m.EnableKeyword("_OCCLUSIONMAP");
            m.SetTexture("_MetallicGlossMap",Tex(f,f+"_UnityMask"));m.SetTexture("_OcclusionMap",Tex(f,f+"_AO"));m.SetFloat("_Smoothness",1);m.SetFloat("_Metallic",1);
            if(name=="BagTranslucent")
            {
                m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;
            }
        }
        EditorUtility.SetDirty(m);mats[name]=m;return m;
    }
    static AnimationClip Clip(string name,(float t,float a)[] keys)
    {
        string p=Root+"/Animations/"+name+".anim";var c=AssetDatabase.LoadAssetAtPath<AnimationClip>(p);if(!c){c=new AnimationClip();AssetDatabase.CreateAsset(c,p);}
        c.frameRate=30;var curve=new AnimationCurve(keys.Select(k=>new Keyframe(k.t,k.a)).ToArray());
        for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
        c.SetCurve("Lid_Pivot",typeof(Transform),"localEulerAnglesRaw.x",curve);
        c.SetCurve("Lid_Pivot",typeof(Transform),"localEulerAnglesRaw.y",AnimationCurve.Constant(0,keys.Last().t,0));c.SetCurve("Lid_Pivot",typeof(Transform),"localEulerAnglesRaw.z",AnimationCurve.Constant(0,keys.Last().t,0));
        var settings=AnimationUtility.GetAnimationClipSettings(c);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(c,settings);EditorUtility.SetDirty(c);return c;
    }
    static AnimatorController Controller()
    {
        string p=Root+"/Controllers/Dumpster_Animator.controller";
        var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(p);if(c)AssetDatabase.DeleteAsset(p);
        c=AnimatorController.CreateAnimatorControllerAtPath(p);c.AddParameter("Open",AnimatorControllerParameterType.Bool);var sm=c.layers[0].stateMachine;
        var closed=sm.AddState("Closed",new Vector3(250,60));closed.motion=Clip("Dumpster_Closed",new[]{(0f,0f),(.1f,0f)});
        var opening=sm.AddState("Opening",new Vector3(500,60));opening.motion=Clip("Dumpster_Lid_Open",new[]{(0f,0f),(.267f,-18f),(.733f,-91f),(1f,-105f)});
        var open=sm.AddState("Open",new Vector3(500,220));open.motion=Clip("Dumpster_Open",new[]{(0f,-105f),(.1f,-105f)});
        var closing=sm.AddState("Closing",new Vector3(250,220));closing.motion=Clip("Dumpster_Lid_Close",new[]{(0f,-105f),(.3f,-84f),(.667f,-12f),(.8f,0f),(.867f,-1.2f),(.9f,0f)});
        sm.defaultState=closed;
        var t=closed.AddTransition(opening);t.hasExitTime=false;t.duration=0;t.AddCondition(AnimatorConditionMode.If,0,"Open");
        t=opening.AddTransition(open);t.hasExitTime=true;t.exitTime=1;t.duration=0;
        t=open.AddTransition(closing);t.hasExitTime=false;t.duration=0;t.AddCondition(AnimatorConditionMode.IfNot,0,"Open");
        t=closing.AddTransition(closed);t.hasExitTime=true;t.exitTime=1;t.duration=0;
        foreach(var s in sm.states)s.state.writeDefaultValues=false;return c;
    }
    static Transform Group(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
    static Transform MeshNode(GameObject model,string name,Transform parent)
    {
        var src=model.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f=>f.name==name);if(!src)throw new Exception("FBX node missing "+name);
        var g=Group(parent,name);g.localRotation=Quaternion.Euler(0,180,0);var mf=g.gameObject.AddComponent<MeshFilter>();mf.sharedMesh=src.sharedMesh;var mr=g.gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterials=src.GetComponent<Renderer>().sharedMaterials.Select(m=>mats[m && mats.ContainsKey(m.name)?m.name:"Body"]).ToArray();
        if(name=="Dumpster_Body_LOD0") mr.sharedMaterials=mr.sharedMaterials.Select(m=>m.name=="Body"?mats["BodyBaked"]:m).ToArray();
        return g;
    }
    static void Box(Transform p,string name,Vector3 centre,Vector3 size){var g=Group(p,name);var c=g.gameObject.AddComponent<BoxCollider>();c.center=centre;c.size=size;}
    static void MakePrefab(bool filled,RuntimeAnimatorController controller)
    {
        var root=new GameObject(filled?"Dumpster_Filled":"Dumpster_Empty");
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/GameReady/Dumpster_GameReady.fbx");var bags=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/GameReady/Garbage_Bags.fbx");
        var body=Group(root.transform,"Dumpster_Body");var interior=Group(root.transform,"Dumpster_Interior");var wheels=Group(root.transform,"Dumpster_Wheels");var hinges=Group(root.transform,"Dumpster_Hinges");
        // Interior belongs to the watertight body shell. This group marks its position for authoring.
        interior.localPosition=new Vector3(0,.335f,0);
        var pivot=Group(root.transform,"Lid_Pivot");pivot.localPosition=new Vector3(0,1.23f,-.56f);var lid=Group(pivot,"Dumpster_Lid");lid.localPosition=new Vector3(0,.004f,0);
        var wheelGroups=new[]{"Wheel_FL","Wheel_FR","Wheel_RL","Wheel_RR"}.Select(n=>Group(wheels,n)).ToArray();
        Transform garbage=null;Transform[] bagGroups=null;
        if(filled){garbage=Group(root.transform,"Garbage_Bags");bagGroups=Enumerable.Range(1,12).Select(i=>Group(garbage,"Garbage_Bag_"+i.ToString("00"))).ToArray();}
        var lodgroup=root.AddComponent<LODGroup>();var lods=new LOD[3];
        for(int l=0;l<3;l++)
        {
            var rs=new List<Renderer>();
            foreach(var pair in new[]{("Dumpster_Body",body),("Dumpster_Lid",lid),("Dumpster_Hinges",hinges)}) rs.Add(MeshNode(model,pair.Item1+"_LOD"+l,pair.Item2).GetComponent<Renderer>());
            for(int w=0;w<4;w++)rs.Add(MeshNode(model,wheelGroups[w].name+"_LOD"+l,wheelGroups[w]).GetComponent<Renderer>());
            if(filled)for(int b=0;b<12;b++)rs.Add(MeshNode(bags,bagGroups[b].name+"_LOD"+l,bagGroups[b]).GetComponent<Renderer>());
            lods[l]=new LOD(new[]{.32f,.13f,.025f}[l],rs.ToArray());
        }
        var dirt=Group(root.transform,"Decals_Dirt");dirt.gameObject.SetActive(true);
        var stickers=Group(root.transform,"Decals_Stickers");for(int i=0;i<8;i++){var wrapper=Group(stickers,"Sticker_"+i.ToString("00"));MeshNode(model,"Sticker_"+i.ToString("00"),wrapper);}
        for(int l=0;l<2;l++)lods[l].renderers=lods[l].renderers.Concat(stickers.GetComponentsInChildren<Renderer>()).ToArray();
        lodgroup.SetLODs(lods);lodgroup.RecalculateBounds();
        var col=Group(root.transform,"Colliders");
        // Compound hollow collision preserves the accessible mouth; no solid box filling the cavity.
        Box(col,"Floor",new Vector3(0,.31f,0),new Vector3(1.16f,.055f,.91f));
        Box(col,"WallFront",new Vector3(0,.75f,.49f),new Vector3(1.23f,.88f,.045f));Box(col,"WallRear",new Vector3(0,.75f,-.49f),new Vector3(1.23f,.88f,.045f));
        Box(col,"WallLeft",new Vector3(-.61f,.75f,0),new Vector3(.045f,.88f,1.01f));Box(col,"WallRight",new Vector3(.61f,.75f,0),new Vector3(.045f,.88f,1.01f));
        Box(pivot,"LidCollider",new Vector3(0,.045f,.56f),new Vector3(1.35f,.09f,1.08f));
        for(int w=0;w<4;w++){var bc=Group(col,"WheelCollider"+w).gameObject.AddComponent<SphereCollider>();bc.center=new Vector3(w%2==0?-.51f:.51f,.11f,w<2?.39f:-.39f);bc.radius=.108f;}
        var animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var presentation=root.AddComponent<DumpsterPresentation>();var so=new SerializedObject(presentation);so.FindProperty("lidAnimator").objectReferenceValue=animator;so.FindProperty("stickerRoot").objectReferenceValue=stickers;so.FindProperty("bagRoot").objectReferenceValue=garbage;so.FindProperty("variationSeed").intValue=filled?29:17;so.ApplyModifiedPropertiesWithoutUndo();presentation.ApplyVariation();
        PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+root.name+".prefab");UnityEngine.Object.DestroyImmediate(root);
    }
    static void Demo()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,Root+"/Demo/Dumpster_Renderer.asset");
        var pipeline=ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();AssetDatabase.CreateAsset(pipeline,Root+"/Demo/Dumpster_URP.asset");
        pipeline.msaaSampleCount=4;pipeline.shadowDistance=20;pipeline.mainLightShadowmapResolution=2048;pipeline.supportsHDR=true;
        var so=new SerializedObject(pipeline);var list=so.FindProperty("m_RendererDataList");list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=renderer;so.ApplyModifiedPropertiesWithoutUndo();
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        for(int i=0;i<2;i++){var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Dumpster_"+(i==0?"Empty":"Filled")+".prefab"));go.transform.position=new Vector3(i==0?-1:1,0,0);}
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Preview ground";floor.transform.localScale=Vector3.one*4;
        var gm=new Material(Shader.Find("Universal Render Pipeline/Lit"));gm.SetColor("_BaseColor",new Color(.19f,.2f,.21f));AssetDatabase.CreateAsset(gm,Root+"/Demo/PreviewGround.mat");floor.GetComponent<Renderer>().sharedMaterial=gm;
        var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(48,145,0);light.shadows=LightShadows.Soft;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.4f,.44f,.49f);
        var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.32f,.36f,.40f));RenderSettings.ambientProbe=ambient;
        var cam=new GameObject("Preview Camera").AddComponent<Camera>();cam.transform.position=new Vector3(3.8f,2.8f,5.6f);cam.transform.LookAt(new Vector3(0,.7f,0));cam.backgroundColor=new Color(.22f,.25f,.28f);cam.clearFlags=CameraClearFlags.SolidColor;cam.nearClipPlane=.05f;cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        EditorSceneManager.SaveScene(scene,Root+"/Demo/Dumpster_Preview.unity");
    }
    [Serializable] class Item {public string name;public int[] triangles;public Vector3 bounds;public int colliders;public int bags;public int decals;}
    [Serializable] class Report{public string unity;public string urp;public List<Item> prefabs=new List<Item>();public List<string> fbxAnimations=new List<string>();public int textures4K;public bool passed;}
    public static void Validate()
    {
        var report=new Report{unity=Application.unityVersion,urp="17.6.0"};
        foreach(string p in Directory.GetFiles(Root+"/Textures","*.png",SearchOption.AllDirectories))
        {
            var t=AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\','/'));if(t.width!=4096||t.height!=4096)throw new Exception("Texture size "+p);report.textures4K++;
        }
        foreach(var path in Directory.GetFiles(Root+"/Animations","*.fbx"))
        {
            var clips=AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\','/')).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
            if(clips.Length==0)throw new Exception("Missing FBX clip "+path);
            foreach(var c in clips){var bindings=AnimationUtility.GetCurveBindings(c);if(bindings.Length==0)throw new Exception("Empty FBX curves");report.fbxAnimations.Add(c.name+" duration="+c.length+" bindings="+bindings.Length);}
        }
        foreach(string n in new[]{"Dumpster_Empty","Dumpster_Filled"})
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+n+".prefab"));var lods=go.GetComponent<LODGroup>().GetLODs();
            var item=new Item{name=n,triangles=lods.Select(l=>l.renderers.Sum(r=>(int)r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0)/3 + Enumerable.Range(1,r.GetComponent<MeshFilter>().sharedMesh.subMeshCount-1).Sum(s=>(int)r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(s)/3))).ToArray(),colliders=go.GetComponentsInChildren<Collider>().Length,bags=go.transform.Find("Garbage_Bags")?12:0,decals=8};
            if(lods.Length!=3||item.triangles[0]<=item.triangles[1]||item.triangles[1]<=item.triangles[2])throw new Exception("LOD reductions failed");
            foreach(var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m=mf.sharedMesh;if(m.uv.Length!=m.vertexCount||m.normals.Length!=m.vertexCount)throw new Exception("Missing UV/normals "+m.name);
                foreach(var v in m.vertices)if(float.IsNaN(v.x)||float.IsInfinity(v.x))throw new Exception("Invalid vertex");
            }
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(!m||!m.shader||m.shader.name.Contains("Error"))throw new Exception("Missing material");
            var bounds=new Bounds(go.transform.position,Vector3.zero);foreach(var r in lods[0].renderers)bounds.Encapsulate(r.bounds);item.bounds=bounds.size;
            if(item.bounds.x>1.6f||item.bounds.y>1.5f||item.bounds.z>1.4f)throw new Exception("Unexpected assembled dimensions: "+item.bounds);
            report.prefabs.Add(item);UnityEngine.Object.DestroyImmediate(go);
        }
        var errors=ShaderUtil.GetShaderMessages(Shader.Find("ZombieSurvival/Dumpster Surface")).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.message)));
        report.passed=true;File.WriteAllText(Proof+"/unity_import.json",JsonUtility.ToJson(report,true));Debug.Log("DUMPSTER_IMPORT_VALIDATION_PASS");
    }
    public static void Diagnose()
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/GameReady/Dumpster_GameReady.fbx");
        var text=string.Join("\n",model.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.EndsWith("LOD0")||m.name=="Sticker_00").Select(m=>m.name+" bounds="+m.sharedMesh.bounds+" local="+m.transform.localPosition+" rot="+m.transform.localEulerAngles+" scale="+m.transform.localScale+" firstVertex="+m.sharedMesh.vertices[0]));File.WriteAllText(Proof+"/unity_mesh_diagnostic.txt",text);
    }
    public static void BuildAndPlay(){Build();DumpsterPlayChecks.Run();}
}
