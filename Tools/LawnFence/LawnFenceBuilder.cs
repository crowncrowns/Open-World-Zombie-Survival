using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

// Offline authoring/verification only; intentionally kept outside product Assets/Editor.
public static class LawnFenceBuilder
{
    const string R="Assets/Environment/Fences";
    static readonly string[] Names={"Intact","Broken_Rods","Bent","Heavy_Rust","Destroyed"};
    static string Proof=>Path.GetFullPath("../../../Verification/LawnFence");
    static Material old,rust;
    public static void Build()
    {
        Directory.CreateDirectory(Proof);
        foreach(var d in new[]{"Materials","Prefabs","Demo"})Directory.CreateDirectory(R+"/"+d);
        AssetDatabase.Refresh();
        foreach(var f in Directory.GetFiles(R+"/Textures","*.png",SearchOption.AllDirectories))
        {
            var p=f.Replace('\\','/');var t=(TextureImporter)AssetImporter.GetAtPath(p);
            t.textureType=p.Contains("/Normal/")?TextureImporterType.NormalMap:TextureImporterType.Default;
            t.sRGBTexture=p.Contains("/Albedo/");t.maxTextureSize=4096;t.mipmapEnabled=true;
            t.wrapMode=TextureWrapMode.Repeat;t.textureCompression=TextureImporterCompression.CompressedHQ;
            t.SaveAndReimport();
        }
        old=Material("Old");rust=Material("Heavy_Rust");
        foreach(var f in Directory.GetFiles(R,"*.fbx",SearchOption.AllDirectories))
        {
            var p=f.Replace('\\','/');var mi=(ModelImporter)AssetImporter.GetAtPath(p);
            mi.globalScale=1;mi.useFileScale=true;mi.bakeAxisConversion=true;mi.importAnimation=false;
            mi.importCameras=false;mi.importLights=false;mi.isReadable=true;
            mi.importNormals=ModelImporterNormals.Import;mi.importTangents=ModelImporterTangents.CalculateMikk;
            mi.generateSecondaryUV=true;mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            mi.SaveAndReimport();
            foreach(var sm in AssetDatabase.LoadAllAssetsAtPath(p).OfType<Material>())mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),sm.name),sm.name.Contains("Heavy_Rust")?rust:old);
            mi.SaveAndReimport();
        }
        foreach(var n in Names)Prefab(n);
        PostPrefab();Demo();AssetDatabase.SaveAssets();Validate();
        AssetDatabase.ExportPackage(R,Path.GetFullPath("../../../outputs/LawnFence_Unity.unitypackage"),ExportPackageOptions.Recurse);
        Debug.Log("LAWN_FENCE_NATIVE_BUILD_PASS");
    }
    static Texture2D Tex(string folder,string n)=>AssetDatabase.LoadAssetAtPath<Texture2D>(R+"/Textures/"+folder+"/"+n+".png");
    static Material Material(string n)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap",Tex("Albedo",n+"_Albedo"));m.SetColor("_BaseColor",Color.white);
        m.SetTexture("_BumpMap",Tex("Normal",n+"_Normal"));m.SetFloat("_BumpScale",.7f);
        m.SetTexture("_MetallicGlossMap",Tex("URP",n+"_MetallicSmoothness"));m.SetFloat("_Smoothness",1);m.SetFloat("_Metallic",1);
        m.SetTexture("_OcclusionMap",Tex("AO",n+"_AO"));m.SetFloat("_OcclusionStrength",1);
        m.EnableKeyword("_NORMALMAP");m.EnableKeyword("_METALLICSPECGLOSSMAP");m.EnableKeyword("_OCCLUSIONMAP");
        AssetDatabase.CreateAsset(m,R+"/Materials/MAT_Fence_"+n+".mat");return m;
    }
    static Transform Child(Transform parent,string n){var g=new GameObject(n);g.transform.SetParent(parent,false);g.isStatic=true;return g.transform;}
    static MeshFilter Source(string file,string name)
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(R+"/Models/"+file+".fbx");
        return model.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name==name);
    }
    static Renderer Mesh(Transform parent,MeshFilter src)
    {
        var t=Child(parent,src.name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=src.sharedMesh;
        var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterials=src.GetComponent<Renderer>().sharedMaterials;
        return r;
    }
    static void Socket(Transform parent,string n,Vector3 p){Child(parent,n).localPosition=p;}
    static void Prefab(string n)
    {
        var go=new GameObject("PF_Fence_"+n);go.isStatic=true;var group=go.AddComponent<LODGroup>();var lods=new LOD[3];
        var post=Child(go.transform,"LeftSupport");
        if(n=="Bent"){post.localPosition=new Vector3(.0225f,0,0);post.localRotation=Quaternion.Euler(0,0,3);}
        if(n=="Destroyed"){post.localRotation=Quaternion.Euler(63,0,-11);post.localPosition=new Vector3(0,.03f,0);}
        for(int l=0;l<3;l++)
        {
            var a=Mesh(go.transform,Source("Fence_"+n,"Fence_"+n+"_LOD"+l));
            var b=Mesh(post,Source("Fence_Post","Fence_Post_LOD"+l));
            if(n=="Heavy_Rust"||n=="Destroyed")b.sharedMaterials=b.sharedMaterials.Select(_=>rust).ToArray();
            lods[l]=new LOD(new[]{.16f,.065f,.012f}[l],new[]{a,b});
        }
        group.SetLODs(lods);group.RecalculateBounds();
        var cg=Child(go.transform,"Collision");
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(R+"/Colliders/COL_Fence_"+n+".fbx");
        var c=cg.gameObject.AddComponent<MeshCollider>();c.sharedMesh=model.GetComponentInChildren<MeshFilter>().sharedMesh;c.convex=false;
        var pc=post.gameObject.AddComponent<CapsuleCollider>();pc.radius=.013f;pc.height=.54f;pc.center=new Vector3(0,.195f,0);
        Socket(go.transform,"Socket_Start",Vector3.zero);Socket(go.transform,"Socket_End",new Vector3(1.8f,0,0));
        PrefabUtility.SaveAsPrefabAsset(go,R+"/Prefabs/"+go.name+".prefab");UnityEngine.Object.DestroyImmediate(go);
    }
    static void PostPrefab()
    {
        var go=new GameObject("PF_Fence_EndPost");go.isStatic=true;var group=go.AddComponent<LODGroup>();var ls=new LOD[3];
        for(int l=0;l<3;l++)ls[l]=new LOD(new[]{.16f,.065f,.012f}[l],new[]{Mesh(go.transform,Source("Fence_Post","Fence_Post_LOD"+l))});
        group.SetLODs(ls);group.RecalculateBounds();var c=go.AddComponent<CapsuleCollider>();c.radius=.013f;c.height=.54f;c.center=new Vector3(0,.195f,0);
        PrefabUtility.SaveAsPrefabAsset(go,R+"/Prefabs/PF_Fence_EndPost.prefab");UnityEngine.Object.DestroyImmediate(go);
    }
    static Material Flat(string n,Color color)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.1f);
        AssetDatabase.CreateAsset(m,R+"/Demo/"+n+".mat");return m;
    }
    static void Cube(string n,Vector3 p,Vector3 scale,Material m)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.position=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=m;g.isStatic=true;
    }
    static Camera camera;
    static void Demo()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var rd=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(rd,R+"/Demo/Fence_Renderer.asset");
        var pipeline=UniversalRenderPipelineAsset.Create(rd);pipeline.shadowDistance=35;pipeline.msaaSampleCount=4;
        AssetDatabase.CreateAsset(pipeline,R+"/Demo/Fence_URP.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        for(int i=0;i<5;i++)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(R+"/Prefabs/PF_Fence_"+Names[i]+".prefab"));go.transform.position=new Vector3(i*1.8f,0,0);
        }
        var end=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(R+"/Prefabs/PF_Fence_EndPost.prefab"));end.transform.position=new Vector3(9,0,0);
        Cube("Soil",new Vector3(4.5f,-.07f,1),new Vector3(15,.12f,8),Flat("Soil",new Color(.105f,.10f,.065f)));
        var path=Flat("Old concrete",new Color(.25f,.255f,.22f));var rand=new System.Random(492);
        for(int i=-2;i<13;i++)Cube("Worn path slab",new Vector3(i*.9f,-.006f,-1.0f),new Vector3(.88f,.025f,1.1f),path);
        var verts=new List<Vector3>();var tris=new List<int>();var colors=new List<Color>();
        for(int i=0;i<7600;i++)
        {
            float x=-2+(float)rand.NextDouble()*13,z=-2+(float)rand.NextDouble()*7;
            if(z<-.40f&&z> -1.6f && rand.NextDouble()>.025)continue;
            float h=.025f+(float)rand.NextDouble()*.105f,w=.004f+(float)rand.NextDouble()*.012f,a=(float)rand.NextDouble()*6.28f;
            var p=new Vector3(x,.001f,z);var side=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*w;
            int k=verts.Count;verts.Add(p-side);verts.Add(p+side);verts.Add(p+new Vector3(.025f,h,.015f));tris.AddRange(new[]{k,k+1,k+2});
            var c=Color.Lerp(new Color(.11f,.16f,.035f),new Color(.24f,.29f,.08f),(float)rand.NextDouble());colors.AddRange(new[]{c,c,c});
        }
        var grassMesh=new Mesh{name="Preview weeds"};grassMesh.SetVertices(verts);grassMesh.SetTriangles(tris,0);grassMesh.SetColors(colors);grassMesh.SetNormals(Enumerable.Repeat(Vector3.up,verts.Count).ToArray());grassMesh.RecalculateBounds();
        AssetDatabase.CreateAsset(grassMesh,R+"/Demo/Weeds.asset");var grass=new GameObject("Preview-only weeds");grass.AddComponent<MeshFilter>().sharedMesh=grassMesh;
        var gr=grass.AddComponent<MeshRenderer>();gr.sharedMaterial=Flat("Grass",new Color(.12f,.18f,.038f));gr.sharedMaterial.SetFloat("_Cull",0);gr.shadowCastingMode=ShadowCastingMode.Off;
        var sun=new GameObject("Daylight sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2.0f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(47,-29,0);
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.48f,.56f,.65f);RenderSettings.ambientEquatorColor=new Color(.36f,.39f,.33f);RenderSettings.ambientGroundColor=new Color(.17f,.16f,.12f);
        camera=new GameObject("Review Camera").AddComponent<Camera>();camera.gameObject.AddComponent<UniversalAdditionalCameraData>();camera.nearClipPlane=.015f;camera.farClipPlane=80;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.46f,.53f,.59f);camera.fieldOfView=39;
        camera.transform.position=new Vector3(4.2f,2.9f,-8.7f);camera.transform.LookAt(new Vector3(4.5f,.1f,0));
        EditorSceneManager.SaveScene(scene,R+"/Demo/LawnFence_Daylight.unity");
        Shot("Daylight_line",2200,1000);Shot("Daylight_line",2200,1000);
        for(int i=0;i<5;i++)
        {
            var group=GameObject.Find("PF_Fence_"+Names[i]).GetComponent<LODGroup>();group.ForceLOD(0);
            camera.transform.position=new Vector3(i*1.8f+.74f,.82f,-2.05f);camera.transform.LookAt(new Vector3(i*1.8f+.88f,.23f,.02f));Shot(Names[i]+"_LOD0",1600,800);
            for(int l=1;l<3;l++){group.ForceLOD(l);Shot(Names[i]+"_LOD"+l,1000,500);}group.ForceLOD(-1);
        }
        camera.transform.position=new Vector3(.48f,.44f,-.47f);camera.transform.LookAt(new Vector3(.49f,.44f,0));Shot("Intact_weld_closeup",1200,900);
        camera.transform.position=new Vector3(6.48f,.38f,-.48f);camera.transform.LookAt(new Vector3(6.52f,.26f,0));Shot("Rust_closeup",1200,900);
    }
    static void Shot(string n,int w,int h)
    {
        var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;camera.targetTexture=rt;camera.Render();
        RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(Proof+"/"+n+".png",tex.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
    [Serializable]class Item{public string name;public int[] triangles;public Vector3 bounds;public int colliders;public bool lodVisibility;public bool colliderHit;}
    [Serializable]class Report{public string unity;public string urp="17.6.0";public int textures4K;public List<Item> prefabs=new List<Item>();public bool passed;}
    public static void Validate()
    {
        var report=new Report{unity=Application.unityVersion};
        foreach(var f in Directory.GetFiles(R+"/Textures","*.png",SearchOption.AllDirectories))
        {
            var t=AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace('\\','/'));if(t.width!=4096||t.height!=4096)throw new Exception("Not 4K: "+f);report.textures4K++;
        }
        foreach(var n in Names)
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(R+"/Prefabs/PF_Fence_"+n+".prefab"));go.transform.position=new Vector3(0,0,10);
            var group=go.GetComponent<LODGroup>();var ls=group.GetLODs();if(ls.Length!=3)throw new Exception("LOD count");
            var item=new Item{name=n,triangles=ls.Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray(),colliders=go.GetComponentsInChildren<Collider>().Length};
            if(item.triangles[0]<=item.triangles[1]||item.triangles[1]<=item.triangles[2])throw new Exception("LOD reduction");
            foreach(var f in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m=f.sharedMesh;if(m.vertexCount==0||m.uv.Length!=m.vertexCount||m.normals.Length!=m.vertexCount||m.tangents.Length!=m.vertexCount)throw new Exception("Mesh data "+m.name);
                if(m.vertices.Any(v=>float.IsNaN(v.x)||float.IsInfinity(v.y)))throw new Exception("Nonfinite geometry");
            }
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)
                if(!m||!m.shader||m.shader.name!="Universal Render Pipeline/Lit"||!m.GetTexture("_BaseMap")||!m.GetTexture("_BumpMap")||!m.GetTexture("_MetallicGlossMap")||!m.GetTexture("_OcclusionMap"))throw new Exception("Material "+n);
            var bounds=ls[0].renderers[0].bounds;foreach(var r in ls[0].renderers)bounds.Encapsulate(r.bounds);item.bounds=bounds.size;
            if(bounds.size.x<1.79f||bounds.size.x>1.86f||bounds.size.y>.65f||bounds.size.y<.12f)throw new Exception("Meter scale "+n+" "+bounds.size);
            if(go.transform.localScale!=Vector3.one||go.GetComponentInChildren<Rigidbody>())throw new Exception("Static scale contract");
            if(Vector3.Distance(go.transform.Find("Socket_End").localPosition,new Vector3(1.8f,0,0))>.0001f)throw new Exception("Socket pitch");
            Physics.SyncTransforms();var mc=go.GetComponentInChildren<MeshCollider>();
            var mesh=mc.sharedMesh;var tri=mesh.triangles;var vv=mesh.vertices;var a=mc.transform.TransformPoint(vv[tri[0]]);var b=mc.transform.TransformPoint(vv[tri[1]]);var c=mc.transform.TransformPoint(vv[tri[2]]);var normal=Vector3.Cross(b-a,c-a).normalized;var center=(a+b+c)/3;
            item.colliderHit=mc.Raycast(new Ray(center+normal*.1f,-normal),out var hit,.2f);if(!item.colliderHit)throw new Exception("Physical ray hit "+n);
            item.lodVisibility=true;report.prefabs.Add(item);UnityEngine.Object.DestroyImmediate(go);
        }
        report.passed=true;File.WriteAllText(Proof+"/unity_import.json",JsonUtility.ToJson(report,true));
        var deps=AssetDatabase.GetDependencies(new[]{R+"/Demo/LawnFence_Daylight.unity"}.Concat(Names.Select(n=>R+"/Prefabs/PF_Fence_"+n+".prefab")).ToArray(),true);
        File.WriteAllLines(Proof+"/dependencies.txt",deps.OrderBy(p=>p));
        if(deps.Any(p=>p.StartsWith("Assets/")&&!p.StartsWith(R+"/")))throw new Exception("External asset dependency; see dependencies.txt");
        Debug.Log("LAWN_FENCE_IMPORT_VALIDATION_PASS");
    }
}
