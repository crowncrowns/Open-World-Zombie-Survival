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

public static class LightingPackBuilder
{
    public const string Root="Assets/PostApocalypse/LightingPack";
    public static string Proof=>Path.GetFullPath("../../../Verification/LightingPack");
    [Serializable] public class SourceLight { public float[] position; public string type; public float[] direction; public float weight; }
    [Serializable] public class Fixture { public int id; public string category,name,title,shape,mount,wear,geometryHash; public float width,height,depth,intensity,range,angle; public int[] triangles; public SourceLight[] lights; }
    [Serializable] public class Manifest { public Fixture[] fixtures; public string[] decor; }
    public static Manifest Data=>JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/manifest.json"));
    public static void Guard() { if(!Application.dataPath.Replace('\\','/').Contains("/work/LightingPack/UnityProject/Assets")) throw new InvalidOperationException("Builder is restricted to work/LightingPack/UnityProject; never run on user scenes."); Directory.CreateDirectory(Proof); }
    static readonly string[] MaterialNames={"Steel","Aluminium","Plastic","Glass","Bulb","Rubber","Copper","ClearGlass"};
    static readonly string[] Wears={"Used","Worn","Damaged"};
    public static void Build()
    {
        Guard();
        Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Demo");AssetDatabase.Refresh();
        ConfigureTextures();Materials();ConfigureModels();
        foreach(var f in Data.fixtures) Prefab(f);
        foreach(var n in Data.decor) Decor(n);
        if(Data.fixtures.Length>=90)Demo();AssetDatabase.SaveAssets();Validate();
        Debug.Log("LIGHTING_PACK_BUILD_PASS");
    }
    public static void BuildAndPlay() { Build();LightingPackChecks.Run(); }
    public static void RefineAndPlay()
    {
        Guard();AssetDatabase.Refresh();Materials();Demo();AssetDatabase.SaveAssets();Validate();LightingPackChecks.Run();
    }
    public static void RebuildAndPlay()
    {
        Guard();AssetDatabase.Refresh();
        var import=(ModelImporter)AssetImporter.GetAtPath(Root+"/Models/Interior/Light_Interior_Pendant_Damaged.fbx");
        import.secondaryUVPackMargin=.25f;import.SaveAndReimport();
        foreach(var f in Data.fixtures)Prefab(f);
        foreach(var n in Data.decor)Decor(n);
        Demo();AssetDatabase.SaveAssets();Validate();LightingPackChecks.Run();
    }
    static void ConfigureTextures()
    {
        foreach(var p in Directory.GetFiles(Root+"/Textures","*.png",SearchOption.AllDirectories))
        {
            var path=p.Replace('\\','/');var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            bool normal=path.EndsWith("/Normal.png"),color=path.EndsWith("/BaseColor.png")||path.EndsWith("/Emission.png");
            imp.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;imp.sRGBTexture=color;
            imp.maxTextureSize=2048;imp.wrapMode=TextureWrapMode.Repeat;imp.mipmapEnabled=true;imp.textureCompression=TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
    }
    static Texture2D Tex(string wear,string name,string map)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+wear+"/"+name+"/"+map+".png");
    static void Materials()
    {
        foreach(var wear in Wears) foreach(var n in MaterialNames)
        {
            var path=Root+"/Materials/"+n+"_"+wear+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat) {mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.name=n+"_"+wear;mat.enableInstancing=true;
            mat.SetTexture("_BaseMap",Tex(wear,n,"BaseColor"));mat.SetColor("_BaseColor",Color.white);
            mat.SetTexture("_BumpMap",Tex(wear,n,"Normal"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",n=="Glass"||n=="Bulb"||n=="ClearGlass"?.035f:.12f);
            mat.SetTexture("_MetallicGlossMap",Tex(wear,n,"MetallicSmoothness"));mat.SetFloat("_Metallic",1);mat.SetFloat("_Smoothness",1);mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetTexture("_OcclusionMap",Tex(wear,n,"AO"));mat.SetFloat("_OcclusionStrength",.65f);
            bool emissive=n=="Glass"||n=="Bulb"||n=="ClearGlass";
            mat.SetTexture("_EmissionMap",Tex(wear,n,"Emission"));mat.SetColor("_EmissionColor",Color.black);
            if(emissive)mat.EnableKeyword("_EMISSION");else mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            if(n=="ClearGlass")
            {
                mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_ZWrite",0);
                mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);mat.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
                mat.SetColor("_BaseColor",new Color(1,1,1,.24f));mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;
                mat.SetShaderPassEnabled("ShadowCaster",false);
            }
            EditorUtility.SetDirty(mat);
        }
    }
    static void ConfigureModels()
    {
        foreach(var path in Directory.GetFiles(Root+"/Models","*.fbx",SearchOption.AllDirectories))
        {
            var p=path.Replace('\\','/');var imp=(ModelImporter)AssetImporter.GetAtPath(p);
            imp.globalScale=1;imp.useFileScale=true;imp.importAnimation=false;imp.isReadable=true;
            imp.importNormals=ModelImporterNormals.Import;imp.generateSecondaryUV=true;imp.addCollider=false;imp.importCameras=false;imp.importLights=false;
            imp.secondaryUVPackMargin=.25f;
            imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            string wear=Wears.FirstOrDefault(w=>p.EndsWith("_"+w+".fbx"))??"Damaged";
            foreach(var n in MaterialNames)imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),n+"_"+wear),AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+n+"_"+wear+".mat"));
            imp.SaveAndReimport();
        }
    }
    static Transform Group(Transform parent,string name) {var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    static Vector3 Vec(float[] v)=>new Vector3(v[0],v[1],v[2]);
    public static string PathFor(Fixture f)=>Root+"/Prefabs/"+f.category+"/"+f.name+".prefab";
    static void Prefab(Fixture f)
    {
        var root=new GameObject(f.name);var model=Group(root.transform,"Model");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/"+f.category+"/"+f.name+".fbx");
        if(!source)throw new Exception("Missing FBX "+f.name);
        var meshRoot=(GameObject)PrefabUtility.InstantiatePrefab(source);meshRoot.transform.SetParent(model,false);
        PrefabUtility.UnpackPrefabInstance(meshRoot,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);meshRoot.name="Geometry";
        // Explicit external assignments also protect against importer material-name suffixes.
        foreach(var r in meshRoot.GetComponentsInChildren<Renderer>(true))
        {
            var mm=r.sharedMaterials;
            for(int k=0;k<mm.Length;k++) {string n=mm[k]?mm[k].name:"";var key=MaterialNames.FirstOrDefault(v=>n.StartsWith(v+"_",StringComparison.Ordinal));if(key!=null)mm[k]=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+key+"_"+f.wear+".mat");}
            r.sharedMaterials=mm;
            if(r.name.Contains("Glass")||r.name.Contains("Bulb"))r.shadowCastingMode=ShadowCastingMode.Off;
        }
        var lodGroup=root.AddComponent<LODGroup>();var lods=new LOD[3];
        for(int i=0;i<3;i++) {int j=i;var rs=meshRoot.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("LOD"+j+"_",StringComparison.Ordinal)).ToArray();if(rs.Length==0)throw new Exception("Missing LOD"+i+" "+f.name);lods[i]=new LOD(new[]{.13f,.055f,.012f}[i],rs);}
        lodGroup.SetLODs(lods);lodGroup.RecalculateBounds();
        var lighting=Group(root.transform,"Lighting");var controller=root.AddComponent<LightFixtureController>();
        controller.intensity=f.intensity;controller.range=f.range;controller.spotAngle=f.angle;
        controller.lightColor=f.category=="Interior"?new Color(.84f,.91f,1):new Color(1,.84f,.61f);
        controller.colorTemperature=f.category=="Interior"?4200:3000;
        controller.buzzingClip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/ElectricalHum.wav");
        var bindings=new List<LightFixtureController.LightBinding>();
        for(int i=0;i<f.lights.Length;i++)
        {
            var sl=f.lights[i];var t=Group(lighting,i==0?"MainLight":"SecondaryLight"+i);t.localPosition=Vec(sl.position);
            if(f.wear=="Damaged"&&new[]{"oval","armored","longcage","sealed","basement"}.Contains(f.shape))t.localPosition+=Vector3.forward*.04f;
            if(sl.direction!=null&&sl.direction.Length==3)t.localRotation=Quaternion.LookRotation(Vec(sl.direction),Mathf.Abs(Vec(sl.direction).normalized.y)>.99f?Vector3.forward:Vector3.up);
            var l=t.gameObject.AddComponent<Light>();l.type=sl.type=="Point"?LightType.Point:LightType.Spot;l.shadows=LightShadows.Soft;
            l.shadowBias=.015f;l.shadowNormalBias=.02f;l.shadowNearPlane=.01f;l.renderMode=LightRenderMode.ForcePixel;
            l.gameObject.AddComponent<UniversalAdditionalLightData>();
            bindings.Add(new LightFixtureController.LightBinding{light=l,weight=sl.weight});
        }
        controller.lights=bindings.ToArray();var emit=new List<LightFixtureController.EmissionBinding>();
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mf=r.GetComponent<MeshFilter>();if(!mf)continue;
            for(int j=0;j<r.sharedMaterials.Length;j++)
                if(r.sharedMaterials[j]&&r.sharedMaterials[j].IsKeywordEnabled("_EMISSION")&&j<mf.sharedMesh.subMeshCount&&mf.sharedMesh.GetIndexCount(j)>0)
                    emit.Add(new LightFixtureController.EmissionBinding{renderer=r,materialIndex=j,weight=r.sharedMaterials[j].name.StartsWith("ClearGlass")?.12f:1});
        }
        controller.emitters=emit.ToArray();Group(root.transform,"Effects");controller.ApplyNow();
        // LOD0 opaque mesh collision preserves apertures instead of filling the whole assembly with a box.
        var colliders=Group(root.transform,"Colliders");
        foreach(var r in lods[0].renderers.Where(r=>r.name.Contains("Housing")||r.name.Contains("Mounting")))
        {var t=Group(colliders,r.name);t.SetPositionAndRotation(r.transform.position,r.transform.rotation);t.localScale=r.transform.lossyScale;var c=t.gameObject.AddComponent<MeshCollider>();c.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;}
        Directory.CreateDirectory(Root+"/Prefabs/"+f.category);PrefabUtility.SaveAsPrefabAsset(root,PathFor(f));UnityEngine.Object.DestroyImmediate(root);
    }
    static void Decor(string n)
    {
        var root=new GameObject(n);var src=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/Decor/"+n+".fbx");var child=(GameObject)PrefabUtility.InstantiatePrefab(src);child.transform.SetParent(root.transform,false);
        var box=root.AddComponent<BoxCollider>();var bounds=new Bounds(Vector3.zero,Vector3.zero);foreach(var r in child.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);box.center=bounds.center;box.size=bounds.size;
        Directory.CreateDirectory(Root+"/Prefabs/Decor");PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Decor/"+n+".prefab");UnityEngine.Object.DestroyImmediate(root);
    }
    public static Material Flat(string name,Color color)
    {
        var p=Root+"/Demo/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);return m;
    }
    public static GameObject Box(string name,Vector3 p,Vector3 size,Material material)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
    public static LightFixtureController Place(Fixture f,Vector3 position,LightFixtureController.OperatingMode mode=LightFixtureController.OperatingMode.Normal)
    {var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PathFor(f)));go.transform.position=position;var c=go.GetComponent<LightFixtureController>();c.mode=mode;c.ApplyNow();return c;}
    static void Demo()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var rd=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root+"/Demo/LightingPack_Renderer.asset");if(!rd){rd=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(rd,Root+"/Demo/LightingPack_Renderer.asset");}
        var rso=new SerializedObject(rd);rso.FindProperty("m_RenderingMode").intValue=2;rso.ApplyModifiedPropertiesWithoutUndo();
        var pipe=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Demo/LightingPack_URP.asset");if(!pipe){pipe=ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();AssetDatabase.CreateAsset(pipe,Root+"/Demo/LightingPack_URP.asset");}
        var so=new SerializedObject(pipe);var list=so.FindProperty("m_RendererDataList");list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=rd;
        so.FindProperty("m_AdditionalLightsRenderingMode").intValue=1;so.FindProperty("m_AdditionalLightShadowsSupported").boolValue=true;
        so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=8;so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue=4096;
        so.ApplyModifiedPropertiesWithoutUndo();pipe.supportsHDR=true;pipe.msaaSampleCount=4;pipe.shadowDistance=40;
        GraphicsSettings.defaultRenderPipeline=pipe;QualitySettings.renderPipeline=pipe;QualitySettings.pixelLightCount=8;
        var plaster=Flat("Concrete",new Color(.30f,.32f,.29f));var green=Flat("OldGreenPaint",new Color(.13f,.22f,.17f));var metal=Flat("DoorSteel",new Color(.09f,.11f,.12f));var floor=Flat("Floor",new Color(.16f,.17f,.16f));
        var used=Data.fixtures.Where(f=>f.wear=="Used").ToArray();
        // Five architectural bays: stairwell, corridor, courtyard, entry, technical room.
        for(int b=0;b<5;b++)
        {
            float x=b*7;Box("Zone "+b+" floor",new Vector3(x,-.10f,0),new Vector3(6,.2f,6),floor);
            if(b!=2){Box("Back wall",new Vector3(x,1.5f,-2.8f),new Vector3(6,3,.18f),plaster);Box("Painted wall lower",new Vector3(x,.65f,-2.69f),new Vector3(6,1.3f,.015f),green);}
            if(b==0)
            {
                for(int k=0;k<6;k++)Box("Concrete step",new Vector3(x-1.3f,.09f*(k+1),-.2f-k*.32f),new Vector3(1.4f,.18f*(k+1),.32f),plaster);
                Place(used[0],new Vector3(x+1.1f,2.0f,-2.58f));Place(used[1],new Vector3(x-1.3f,2.2f,-2.58f),LightFixtureController.OperatingMode.Weak);
            }
            if(b==1)
            {
                Box("Corridor ceiling",new Vector3(x,3.04f,-.2f),new Vector3(5,.08f,4.8f),plaster);
                Place(used[16],new Vector3(x-1.3f,2.99f,-.5f));Place(used[17],new Vector3(x+1.1f,2.99f,-.5f),LightFixtureController.OperatingMode.Flickering);
                Place(used[18],new Vector3(x,2.99f,-2));
            }
            if(b==2)
            {
                for(int k=0;k<2;k++){float xx=x+(k==0?-1.9f:1.9f);Box("Pole",new Vector3(xx,2,0),new Vector3(.095f,4,.095f),metal);Place(used[8+k],new Vector3(xx,3.75f,.055f),k==0?LightFixtureController.OperatingMode.Normal:LightFixtureController.OperatingMode.Weak);}
            }
            if(b==3)
            {
                Box("Entrance door",new Vector3(x,1.05f,-2.52f),new Vector3(1.15f,2.1f,.12f),metal);Box("Canopy",new Vector3(x,2.6f,-2.1f),new Vector3(2.1f,.06f,1.4f),metal);
                Place(used[6],new Vector3(x,2.4f,-2.55f));Place(used[12],new Vector3(x,2.56f,-1.8f),LightFixtureController.OperatingMode.Weak);
            }
            if(b==4)
            {
                Box("Technical room ceiling mounting slab",new Vector3(x-1,3.0f,-.4f),new Vector3(1.6f,.10f,1.6f),plaster);
                Place(used[20],new Vector3(x-1,2.95f,-.4f));Place(used[24],new Vector3(x,2.2f,-2.56f),LightFixtureController.OperatingMode.Failing);
                Place(used[26],new Vector3(x+1.6f,2.1f,-2.56f));Place(used[27],new Vector3(x+1,0,0));Box("Workbench",new Vector3(x-1,.75f,-1.7f),new Vector3(1.8f,.12f,.65f),metal);
            }
        }
        // Complete numbered gallery, 30 unique geometries × three independent wear states.
        var galleryRefs=new List<LightFixtureController>();
        foreach(var f in Data.fixtures)
        {
            int index=(f.id-1)*3+Array.IndexOf(Wears,f.wear);int col=index%15,row=index/15;
            Vector3 p=new Vector3(col*1.65f,1.35f,-10-row*1.8f);
            if(f.mount=="ceiling")Box("Sample ceiling panel "+f.id+f.wear,p+new Vector3(0,.035f,0),new Vector3(1.48f,.07f,1.45f),plaster);
            else if(f.mount=="floor")Box("Sample base "+f.id+f.wear,p+new Vector3(0,-.025f,0),new Vector3(1.48f,.05f,1.45f),plaster);
            else Box("Sample mounting panel "+f.id+f.wear,p+new Vector3(0,0,-.065f),new Vector3(1.48f,1.45f,.10f),plaster);
            var c=Place(f,p);c.mode=f.wear=="Used"?LightFixtureController.OperatingMode.Normal:(f.wear=="Worn"?LightFixtureController.OperatingMode.Weak:LightFixtureController.OperatingMode.Off);c.distanceCulling=true;c.cullDistance=4;c.ApplyNow();
            galleryRefs.Add(c);
        }
        // Explicit independent instances of one prefab.
        var a=Place(used[0],new Vector3(0,1.2f,3));a.intensity=1;a.lightColor=new Color(1,.45f,.2f);a.ApplyNow();
        var bb=Place(used[0],new Vector3(1,1.2f,3));bb.intensity=5;bb.lightColor=new Color(.3f,.6f,1);bb.ApplyNow();
        Box("Independent example mounting wall",new Vector3(.5f,1.2f,2.94f),new Vector3(1.8f,1.6f,.10f),plaster);
        for(int k=0;k<Data.decor.Length;k++) {var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Decor/"+Data.decor[k]+".prefab"));go.transform.position=new Vector3(k*.65f,.1f,5);}
        var sun=new GameObject("Evening ambient").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.45f;sun.transform.rotation=Quaternion.Euler(35,140,0);sun.shadows=LightShadows.Soft;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.18f,.21f,.25f);
        var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(RenderSettings.ambientLight);RenderSettings.ambientProbe=sh;
        var cam=new GameObject("Showcase Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.transform.position=new Vector3(3.6f,3.6f,6.4f);cam.transform.LookAt(new Vector3(0,1.4f,-.7f));cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.025f,.032f,.045f);cam.nearClipPlane=.02f;cam.farClipPlane=150;cam.allowHDR=true;cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cam.gameObject.AddComponent<AudioListener>();
        var controls=cam.gameObject.AddComponent<LightingShowcaseControls>();controls.viewCamera=cam;controls.gallery=galleryRefs.ToArray();controls.independentExamples=new[]{a,bb};
        controls.sampleNames=Data.fixtures.Select(f=>f.title+" · "+f.wear).ToArray();controls.ceilingSamples=Data.fixtures.Select(f=>f.mount=="ceiling").ToArray();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),Root+"/Demo/LightingPack_Showcase.unity");
    }
    [Serializable] public class Result { public bool passed;public string unity,urp;public int fixtures,decor,textures;public List<string> checks=new List<string>(); }
    public static void Validate()
    {
        Guard();var result=new Result{unity=Application.unityVersion,urp="17.6.0"};var data=Data;
        foreach(var f in data.fixtures)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(PathFor(f));if(!root)throw new Exception("Missing prefab "+f.name);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script "+f.name);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(!m||m.shader.name!="Universal Render Pipeline/Lit")throw new Exception("Material "+f.name+" renderer="+r.name+" material="+(m?m.name:"NULL"));
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))if(mf.sharedMesh.uv2.Length!=mf.sharedMesh.vertexCount)throw new Exception("Missing lightmap UV2 "+f.name+" "+mf.name);
            var lod=root.GetComponent<LODGroup>().GetLODs();if(lod.Length!=3)throw new Exception("LODs "+f.name);
            var ts=lod.Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();
            if(ts[0]<=ts[1]||ts[1]<=ts[2])throw new Exception("LOD triangle decrease "+f.name+" "+string.Join(",",ts));
            var c=root.GetComponent<LightFixtureController>();if(c.lights.Length==0||c.emitters.Length==0)throw new Exception("Missing lighting bindings "+f.name);
            if(Mathf.Abs(c.lights.Sum(l=>l.weight)-1)> .001f)throw new Exception("Light weights "+f.name);
            foreach(var b in c.emitters)if(!b.renderer.sharedMaterials[b.materialIndex].IsKeywordEnabled("_EMISSION"))throw new Exception("Emission keyword "+f.name);
            // Check expected metre-scale after importer axis conversion.
            var bounds=new Bounds();bool first=true;foreach(var r in lod[0].renderers){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            if(bounds.size.magnitude>3||bounds.size.magnitude<.05f)throw new Exception("Scale "+f.name+" "+bounds.size);
            result.checks.Add(f.name+" refs/materials/3LOD/lights/emission/scale PASS; tris="+string.Join("/",ts)+" size="+bounds.size.ToString("F3"));result.fixtures++;
        }
        foreach(var p in Directory.GetFiles(Root+"/Textures","*.png",SearchOption.AllDirectories)) {var t=AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\','/'));if(!t||t.width!=2048||t.height!=2048)throw new Exception("2K map "+p);result.textures++;}
        foreach(var p in Directory.GetFiles(Root+"/Prefabs/Decor","*.prefab"))
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\','/'));if(!prefab)throw new Exception("Missing decor "+p);
            foreach(var t in prefab.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing decor script "+p);
            foreach(var r in prefab.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(!m||m.shader.name!="Universal Render Pipeline/Lit")throw new Exception("Decor material "+p);
            result.decor++;result.checks.Add(prefab.name+" decor references/materials PASS");
        }
        result.passed=true;File.WriteAllText(Proof+"/unity_import.json",JsonUtility.ToJson(result,true));Debug.Log("LIGHTING_IMPORT_PASS "+result.fixtures);
    }
    public static void ColdCheck() { Validate();LightingPackChecks.CaptureGallery();Debug.Log("LIGHTING_COLD_PASS"); }
    public static void Export() { Guard();Directory.CreateDirectory("../../../outputs");AssetDatabase.ExportPackage(Root,"../../../outputs/LightingPack_URP.unitypackage",ExportPackageOptions.Recurse);Debug.Log("LIGHTING_EXPORT_PASS"); }
}
