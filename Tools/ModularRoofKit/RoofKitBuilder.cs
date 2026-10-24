using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using ZombieSurvival.ModularRoofKit;

// Offline authoring in the dedicated project. Never changes the open game scene or its pipeline.
public static class RoofKitBuilder
{
    const string R="Assets/Environment/ModularRoofKit";
    static string Proof=>Path.GetFullPath("../../../Verification/ModularRoofKit");
    [Serializable] public class Snap {public string name,kind;public float[] position,direction;public float size;}
    [Serializable] public class Item {public string name,category,variant,family;public float length;public Snap[] snaps;public int[] triangles;}
    [Serializable] public class Catalog {public int schema;public Item[] items;}
    [Serializable] public class Check {public string name;public int[] triangles;public Vector3 bounds,min,max;public int colliders,snaps;}
    [Serializable] public class Report {public string unity,urp;public bool passed;public int textures4K;public List<Check> prefabs=new();public List<string> checks=new();}
    static Catalog catalog;
    static readonly string[] Names={"M_Roof_Metal_Old","M_Roof_Metal_Rusted","M_Roof_Metal_Damaged","M_Gutter_Galvanized","M_Gutter_Painted","M_Gutter_HeavyRust","M_Fence_PaintedWhite","M_Fence_Rust","M_Fence_PeelingPaint","M_RoofProps_Brick","M_RoofProps_Organic"};
    static Dictionary<string,Material> mats=new();
    static void Require(bool good,string reason){if(!good)throw new Exception(reason);}
    static Vector3 V(float[] a)=>new(a[0],a[1],a[2]);
    static void Isolated(){Require(Application.dataPath.Replace('\\','/').Contains("/work/ModularRoofKit/UnityProject/Assets"),"Run offline build only in work/ModularRoofKit/UnityProject.");}
    public static void Build()
    {
        Isolated();Directory.CreateDirectory(Proof);catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(R+"/catalog.json"));
        foreach(var d in new[]{"Materials","Prefabs","Demo"})Directory.CreateDirectory(R+"/"+d);AssetDatabase.Refresh();
        foreach(var file in Directory.GetFiles(R+"/Textures","*.png",SearchOption.AllDirectories))
        {
            var p=file.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(p);var before=EditorJsonUtility.ToJson(importer);
            importer.textureType=p.Contains("_Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=p.Contains("BaseColor");importer.maxTextureSize=4096;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.textureCompression=TextureImporterCompression.Compressed;importer.compressionQuality=60;
            if(before!=EditorJsonUtility.ToJson(importer))importer.SaveAndReimport();
        }
        for(int i=0;i<Names.Length;i++)MakeMaterial(i);
        foreach(var item in catalog.items)
        {
            var path=R+"/Models/"+item.category+"/"+item.name+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);Require(importer!=null,"Missing importer "+path);var before=EditorJsonUtility.ToJson(importer);
            importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.isReadable=true;
            importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.generateSecondaryUV=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            if(before!=EditorJsonUtility.ToJson(importer))importer.SaveAndReimport();
            foreach(var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())if(mats.TryGetValue(mat.name,out var replacement))importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),mat.name),replacement);
            importer.SaveAndReimport();MakePrefab(item,path);
        }
        MakeDemo();AssetDatabase.SaveAssets();AssetDatabase.Refresh();Validate();RenderViews();AssetDatabase.SaveAssets();
        Debug.Log("MODULAR_ROOF_KIT_BUILD_PASS");
    }
    static Texture2D Tex(string folder,string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(R+"/Textures/"+folder+"/"+name+".png");
    static void MakeMaterial(int i)
    {
        string folder=i<9?new[]{"Roof","Gutters","Fences"}[i/3]:"Props";int state=i<9?new[]{0,2,1,0,1,2,0,2,1}[i]:i-9;string stem=folder+"_"+state;
        string p=R+"/Materials/"+Names[i]+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}m.shader=Shader.Find("Universal Render Pipeline/Lit");
        Require(m.shader,"Missing URP Lit");m.SetTexture("_BaseMap",Tex(folder,stem+"_BaseColor"));m.SetColor("_BaseColor",Color.white);
        m.SetTexture("_BumpMap",Tex(folder,folder+"_Normal"));m.SetFloat("_BumpScale",i==0?.12f:.3f);m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap",Tex(folder,(i<9?stem:folder)+"_UnityMask"));m.SetFloat("_Smoothness",1);m.SetFloat("_Metallic",1);m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetTexture("_OcclusionMap",Tex(folder,folder+"_AO"));m.SetFloat("_OcclusionStrength",.7f);m.EnableKeyword("_OCCLUSIONMAP");
        EditorUtility.SetDirty(m);mats[Names[i]]=m;
    }
    static string Folder(Item item)
    {
        if(item.category!="RoofFences")return item.category=="RoofProps"?"Props":item.category;
        string sub=item.variant.Contains("Rust")?"Rusted":item.variant=="Bent"?"Bent":new[]{"Collapsed","BrokenBars","MissingSegment","Abandoned"}.Contains(item.variant)?"Destroyed":"Intact";
        return "RoofFences/"+sub;
    }
    static void MakePrefab(Item item,string path)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(source,"FBX import failed "+path);
        var root=new GameObject(item.name);var visual=UnityEngine.Object.Instantiate(source,root.transform);visual.name="Visual";
        foreach(var importedGroup in visual.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(importedGroup);
        var filters=visual.GetComponentsInChildren<MeshFilter>(true);Require(filters.Length==3,"Expected three FBX LOD meshes "+item.name);
        var group=root.AddComponent<LODGroup>();var lods=new LOD[3];
        for(int i=0;i<3;i++)
        {
            var filter=filters.Single(f=>f.name.EndsWith("_LOD"+i));var renderer=filter.GetComponent<MeshRenderer>();
            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>mats[m.name]).ToArray();lods[i]=new LOD(new[]{.40f,.16f,.025f}[i],new[]{renderer});
            if(i==1)
            {
                // Independent collision object survives LOD visibility changes and follows actual holes.
                var c=new GameObject("Static mesh collision");c.transform.SetParent(filter.transform.parent,false);c.transform.localPosition=filter.transform.localPosition;c.transform.localRotation=filter.transform.localRotation;c.transform.localScale=filter.transform.localScale;
                var col=c.AddComponent<MeshCollider>();col.sharedMesh=filter.sharedMesh;col.convex=false;
            }
        }
        group.SetLODs(lods);group.RecalculateBounds();var snaps=new GameObject("SnapPoints");snaps.transform.SetParent(root.transform,false);
        foreach(var s in item.snaps)
        {
            var go=new GameObject(s.name);go.transform.SetParent(snaps.transform,false);go.transform.localPosition=V(s.position);var dir=V(s.direction).normalized;
            go.transform.localRotation=Quaternion.LookRotation(dir,Mathf.Abs(dir.y)>.95f?Vector3.forward:Vector3.up);var point=go.AddComponent<RoofSnapPoint>();point.kind=s.kind;point.size=s.size;
        }
        var module=root.AddComponent<RoofKitModule>();module.category=item.category;module.family=item.family;module.condition=item.variant;module.length=item.length;module.variationSeed=17;module.ApplyVariation();
        var dirpath=R+"/Prefabs/"+Folder(item);Directory.CreateDirectory(dirpath);PrefabUtility.SaveAsPrefabAsset(root,dirpath+"/"+item.name+".prefab");UnityEngine.Object.DestroyImmediate(root);
    }
    static GameObject Place(string name,Transform parent,Vector3 pos,Quaternion rot)
    {
        var prefab=RoofKitAssemblyWindow.Find(name);Require(prefab,"Missing prefab "+name);var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localRotation=rot;return go;
    }
    static GameObject Node(string name,Transform parent=null){var g=new GameObject(name);if(parent)g.transform.SetParent(parent,false);return g;}
    static void Label(string text,Transform parent,Vector3 pos,float size=.15f)
    {
        var g=Node(text,parent);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(90,0,0);var t=g.AddComponent<TextMesh>();t.text=text;t.characterSize=size;t.fontSize=48;t.color=new Color(.16f,.20f,.21f);t.anchor=TextAnchor.MiddleCenter;
        var renderer=g.GetComponent<MeshRenderer>();var mat=AssetDatabase.LoadAssetAtPath<Material>(R+"/Demo/Label.mat");
        if(!mat){mat=new Material(renderer.sharedMaterial);mat.shader=Shader.Find("ZombieSurvival/RoofKit Label");AssetDatabase.CreateAsset(mat,R+"/Demo/Label.mat");}renderer.sharedMaterial=mat;
    }
    static Material SimpleMat(string name,Color color)
    {
        var p=R+"/Demo/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}m.color=color;return m;
    }
    static void MakeDemo()
    {
        Isolated();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(R+"/Demo/RoofKit_URP.asset");
        if(!rp)
        {
            var rd=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(rd,R+"/Demo/RoofKit_Renderer.asset");rp=ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();AssetDatabase.CreateAsset(rp,R+"/Demo/RoofKit_URP.asset");
            var so=new SerializedObject(rp);var arr=so.FindProperty("m_RendererDataList");arr.arraySize=1;arr.GetArrayElementAtIndex(0).objectReferenceValue=rd;so.ApplyModifiedPropertiesWithoutUndo();
        }
        rp.msaaSampleCount=4;rp.shadowDistance=70;rp.mainLightShadowmapResolution=4096;rp.supportsHDR=true;GraphicsSettings.defaultRenderPipeline=rp;QualitySettings.renderPipeline=rp;
        var plinth=SimpleMat("DisplayConcrete",new Color(.34f,.35f,.33f));var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Neutral display ground";ground.transform.localScale=new Vector3(9,1,9);ground.GetComponent<Renderer>().sharedMaterial=SimpleMat("Ground",new Color(.48f,.49f,.45f));
        var titles=new[]{"01  OLD / INTACT","02  DAMAGED","03  ABANDONED"};
        for(int sample=0;sample<3;sample++)
        {
            var root=Node(titles[sample]);root.transform.position=new Vector3(sample*11-15,2.5f,-8);
            var baseObject=GameObject.CreatePrimitive(PrimitiveType.Cube);baseObject.name="Display plinth";baseObject.transform.SetParent(root.transform,false);baseObject.transform.localPosition=new Vector3(4,-1.26f,3);baseObject.transform.localScale=new Vector3(7.6f,2.4f,5.6f);baseObject.GetComponent<Renderer>().sharedMaterial=plinth;
            for(int x=0;x<8;x+=2)for(int z=0;z<6;z++)
            {
                string n="Roof_Straight_2m";
                if(sample==1&&((x+z)%5==0))n=z%2==0?"Roof_Bent":"Roof_Damaged";
                if(sample==2)n=new[]{"Roof_Rusted","Roof_MissingPanel","Roof_Destroyed","Roof_Lifted"}[(x/2+z)%4];
                var go=Place(n,root.transform,new Vector3(x,0,z),Quaternion.identity);go.GetComponent<RoofKitModule>().variationSeed=x*31+z*17+sample;go.GetComponent<RoofKitModule>().ApplyVariation();
            }
            // Three roofs use the same modular footprint; individual sections remain replaceable.
            var fence=RoofKitAssemblyWindow.Perimeter(7,5,root.transform);fence.transform.SetParent(root.transform,true);fence.transform.localPosition=new Vector3(.5f,.14f,.5f);
            var sections=fence.GetComponentsInChildren<RoofKitModule>();
            for(int i=0;i<sections.Length;i++)if(sample>0)
            {
                var old=sections[i];string suffix=sample==1?(i%4==0?"Bent":"LightRust"):new[]{"MissingSegment","Collapsed","Abandoned","HeavyRust"}[i%4];
                var go=Place("RoofFence_Straight_0.5m_"+suffix,fence.transform,old.transform.localPosition,old.transform.localRotation);UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var starts=new[]{new Vector3(.5f,.08f,0),new Vector3(8,.08f,.5f),new Vector3(7.5f,.08f,6),new Vector3(0,.08f,5.5f)};
            GameObject outlet=null;
            for(int side=0;side<4;side++)
            {
                var rot=Quaternion.Euler(0,new[]{0,-90,180,90}[side],0);float len=side%2==0?7:5;
                for(int x=0;x<len;x++)
                {
                    string n=side==0&&x==1?"Gutter_Outlet":sample==1&&x%3==0?"Gutter_Bent":sample==2?new[]{"Gutter_Leaves","Gutter_Corroded","Gutter_Sagging"}[x%3]:"Gutter_Straight_1m";
                    var go=Place(n,root.transform,starts[side]+rot*(Vector3.right*x),rot);if(n=="Gutter_Outlet")outlet=go;
                    Place("Gutter_Bracket",root.transform,starts[side]+rot*(Vector3.right*(x+.18f)),rot);
                }
                Place("Gutter_Corner_90_Outer",root.transform,starts[side]+rot*(Vector3.right*len),rot);
            }
            if(outlet)
            {
                var point=outlet.GetComponentsInChildren<RoofSnapPoint>().Single(s=>s.name=="Downpipe");
                var end=RoofKitAssemblyWindow.Attach(RoofKitAssemblyWindow.Find("Downpipe_Offset"),point,"In","Out");
                for(int i=0;i<1;i++)end=RoofKitAssemblyWindow.Attach(RoofKitAssemblyWindow.Find(sample==2?"Downpipe_MissingSegment":sample==1?"Downpipe_Dented":"Downpipe_Straight_1m"),end,"In","Out");
                RoofKitAssemblyWindow.Attach(RoofKitAssemblyWindow.Find("Downpipe_BottomDrain"),end,"In","Out");
                for(int i=0;i<3;i++)Place("Downpipe_Clamp",root.transform,new Vector3(2,-.35f-i*.62f,.3f),Quaternion.identity);
            }
            Label(titles[sample],null,new Vector3(sample*11-11,.02f,-9.4f),.24f);
        }
        // Category catalogue on a separate terrace. All 112+ entries are independent prefab instances.
        int row=0;
        foreach(var cat in catalog.items.GroupBy(i=>i.category))
        {
            Label(cat.Key,null,new Vector3(-11,.025f,4+row*2.6f),.25f);row++;
            int j=0;
            foreach(var item in cat)
            {
                int col=j%8;int rr=j/8;var pos=new Vector3(-15+col*4.3f,.35f,4+(row+rr)*2.6f);
                var go=Place(item.name,null,pos,Quaternion.identity);
                // Pipe previews stand upright from their lower end, so no gallery meshes enter the floor.
                if(item.category=="Downpipes")go.transform.position+=Vector3.up*Mathf.Max(item.length,.7f);
                Label(item.name,null,new Vector3(pos.x+1,.025f,pos.z-.55f),.085f);j++;
            }
            row+=(j+7)/8+1;
        }
        var light=Node("Daylight sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(43,-32,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.52f,.58f);var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.38f,.42f,.48f));RenderSettings.ambientProbe=sh;
        var camera=Node("Roof Kit Camera").AddComponent<Camera>();camera.gameObject.AddComponent<UniversalAdditionalCameraData>();camera.transform.position=new Vector3(25,23,-27);camera.transform.LookAt(new Vector3(0,1,2));camera.nearClipPlane=.05f;camera.farClipPlane=200;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.64f,.74f,.80f);
        EditorSceneManager.SaveScene(scene,R+"/Demo/RoofKit_DemoScene.unity");
    }
    public static void Validate()
    {
        Isolated();catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(R+"/catalog.json"));var report=new Report{unity=Application.unityVersion,urp="17.6.0"};
        foreach(var p in Directory.GetFiles(R+"/Textures","*.png",SearchOption.AllDirectories))
        {
            var t=AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\','/'));Require(t&&t.width==4096&&t.height==4096,"4K texture failed "+p);report.textures4K++;
        }
        foreach(var item in catalog.items)
        {
            var go=RoofKitAssemblyWindow.Find(item.name);Require(go,"Missing prefab "+item.name);var group=go.GetComponent<LODGroup>();Require(group&&group.GetLODs().Length==3,"LOD group "+item.name);
            var check=new Check{name=item.name,colliders=go.GetComponentsInChildren<Collider>(true).Length,snaps=go.GetComponentsInChildren<RoofSnapPoint>(true).Length,triangles=new int[3]};
            Require(check.colliders>0,"Missing collider "+item.name);Require(check.snaps==item.snaps.Length,"Snaps "+item.name);
            Bounds bounds=new Bounds();bool first=true;
            foreach(var f in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m=f.sharedMesh;Require(m&&m.vertexCount>0,"Empty mesh "+item.name);Require(m.uv.Length==m.vertexCount,"Missing UV "+item.name);Require(m.normals.Length==m.vertexCount,"Missing normals");
                int l=f.name.EndsWith("LOD0")?0:f.name.EndsWith("LOD1")?1:2;check.triangles[l]+=m.triangles.Length/3;
                Require(f.GetComponent<Renderer>().sharedMaterials.All(x=>x&&x.shader&&!x.shader.name.Contains("Error")&&x.GetTexture("_BaseMap")&&x.GetTexture("_BumpMap")&&x.GetTexture("_MetallicGlossMap")),"Missing PBR binding "+item.name);
                if(l==0)foreach(var v in m.vertices){var p=go.transform.InverseTransformPoint(f.transform.TransformPoint(v));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            }
            check.bounds=bounds.size;check.min=bounds.min;check.max=bounds.max;
            if(item.name.StartsWith("Roof_Straight"))Require(bounds.min.x>=-.01f&&Mathf.Abs(bounds.max.x-item.length)<.01f&&bounds.min.z>=-.01f&&Mathf.Abs(bounds.max.z-1)<.01f,"Roof imported axes / dimensions "+JsonUtility.ToJson(check));
            if(item.name.StartsWith("Gutter_Straight"))Require(Mathf.Abs(bounds.max.x-item.length)<.01f&&bounds.min.x>-.01f&&bounds.min.y<-.07f&&bounds.max.y<.01f,"Gutter dimensions / U bowl "+JsonUtility.ToJson(check));
            if(item.name.StartsWith("Downpipe_Straight"))Require(Mathf.Abs(bounds.min.y+item.length)<.01f&&bounds.max.y<.01f&&bounds.size.x>.099f&&bounds.size.x<.11f,"Pipe dimensions / orientation "+JsonUtility.ToJson(check));
            Require(check.triangles[0]>=check.triangles[1]&&check.triangles[1]>=check.triangles[2],"LOD triangle progression "+item.name);
            report.prefabs.Add(check);
        }
        report.checks.Add("All FBX imported; three LODs, UV0, normals, materials, static colliders and named sockets present");
        VerifyAssembly(report);report.passed=true;File.WriteAllText(Proof+"/unity_validation.json",JsonUtility.ToJson(report,true));Debug.Log("ROOF_KIT_VALIDATION_PASS "+report.prefabs.Count+" prefabs / "+report.textures4K+" textures");
    }
    static void VerifyAssembly(Report report)
    {
        var root=Node("Temporary snap audit");
        try
        {
            foreach(var pair in new[]{new[]{"Gutter_Straight_1m","End","Gutter_Corner_90_Outer","Start","End"},new[]{"Downpipe_Straight_1m","Out","Downpipe_Elbow_72","In","Out"},new[]{"RoofFence_Straight_1m","End","RoofFence_Corner_90","Start","End"},new[]{"Roof_Straight_2m","East","Roof_Straight_4m","West","East"}})
            {
                var a=Place(pair[0],root.transform,Vector3.zero,Quaternion.identity);var target=a.GetComponentsInChildren<RoofSnapPoint>().Single(s=>s.name==pair[1]);
                var end=RoofKitAssemblyWindow.Attach(RoofKitAssemblyWindow.Find(pair[2]),target,pair[3],pair[4]);var input=end.GetComponentInParent<RoofKitModule>().GetComponentsInChildren<RoofSnapPoint>().Single(s=>s.name==pair[3]);
                Require(Vector3.Distance(input.transform.position,target.transform.position)<1e-5f&&Vector3.Dot(input.transform.forward,target.transform.forward)<-.9999f,"Snap mating failed "+pair[0]);
                report.checks.Add("Snap position/orientation matched: "+pair[0]+" -> "+pair[2]);
            }
            // A mismatched reducer end must be rejected before scene mutation.
            var pipe=Place("Downpipe_Straight_1m",root.transform,Vector3.zero,Quaternion.identity);var p=pipe.GetComponentsInChildren<RoofSnapPoint>().First();var fence=RoofKitAssemblyWindow.Find("RoofFence_Straight_1m").GetComponentsInChildren<RoofSnapPoint>().First();Require(!p.Compatible(fence),"Mismatched socket accepted");
            report.checks.Add("Wrong socket category rejected; pipe nominal outer diameter 100 mm, gutter width 150 mm");
            var reducer=RoofKitAssemblyWindow.Find("Downpipe_Transition").GetComponentsInChildren<RoofSnapPoint>().Single(s=>s.name=="Out");Require(!p.Compatible(reducer),"125/100 mm reducer mismatch accepted");report.checks.Add("125 mm reducer output rejects 100 mm pipe");
            var replace=Place("Roof_Straight_2m",root.transform,new Vector3(40,0,0),Quaternion.identity);Selection.activeGameObject=replace;
            RoofKitAssemblyWindow.ReplaceSelected("Bent",1,123);Require(!replace&&root.GetComponentsInChildren<RoofKitModule>().Any(m=>m.name=="Roof_Bent"&&m.GetComponentsInChildren<RoofSnapPoint>().Length==4),"Roof damaged replacement contract failed");report.checks.Add("Damaged roof replacement preserves all four socket contracts");
            var perimeter=RoofKitAssemblyWindow.Perimeter(2,2,null);perimeter.transform.SetParent(root.transform,true);var modules=perimeter.GetComponentsInChildren<RoofKitModule>();Require(modules.Length==16&&modules.Select(m=>m.transform.position).Distinct().Count()==16,"Perimeter overlaps start posts");report.checks.Add("Closed perimeter: 16 half-metre sections, 16 distinct starting posts");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    public static void RenderViews()
    {
        Isolated();EditorSceneManager.OpenScene(R+"/Demo/RoofKit_DemoScene.unity");var cam=UnityEngine.Object.FindAnyObjectByType<Camera>();
        var views=new[]{("Overview",new Vector3(23,24,-30),new Vector3(0,1,-4)),("Intact",new Vector3(-9,6,-12),new Vector3(-11,2.6f,-5)),("Damaged",new Vector3(2,6,-12),new Vector3(0,2.6f,-5)),("Abandoned",new Vector3(13,6,-12),new Vector3(11,2.6f,-5)),("Rear",new Vector3(24,15,6),new Vector3(0,2,-5)),("GutterDetail",new Vector3(-12.5f,3.0f,-9.0f),new Vector3(-13,2.4f,-7.8f)),("Gallery",new Vector3(0,46,14),new Vector3(0,0,32))};
        foreach(var view in views)
        {
            cam.transform.position=view.Item2;cam.transform.LookAt(view.Item3);cam.fieldOfView=view.Item1=="GutterDetail"?42:50;
            var rt=new RenderTexture(1800,1100,24,RenderTextureFormat.ARGB32);cam.targetTexture=rt;cam.Render();var before=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(Proof+"/"+view.Item1+".png",tex.EncodeToPNG());RenderTexture.active=before;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
        Debug.Log("ROOF_KIT_VIEWS_RENDERED");
    }
}
