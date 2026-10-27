using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieSurvival.Furniture;
using ZombieSurvival.Furniture.Editor;

public static class FurnitureProfessionalReview
{
    const string Root="Assets/PostApocalypticFurniture";
    const string Before="Assets/FurnitureAuditBefore";
    static string Proof=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../Verification/PostApocalypticFurniture/ProfessionalAudit"));
    [Serializable] sealed class Frame {public int id;public string view,lighting,file;public Vector3 cameraPosition,target;public int magenta;}
    [Serializable] sealed class Summary {public bool passed;public int models,frames;public string unity;public List<Frame> images=new List<Frame>();public List<string> issues=new List<string>();public bool lightmapBaked;public int lightmaps,lightmapWitnessPixels;}
    static Summary report;
    static Camera camera;static Light sun,fill;
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/PostApocalypticFurniture/UnityProject/"))throw new Exception("Isolated furniture project only");
        Directory.CreateDirectory(Proof);report=new Summary{unity=Application.unityVersion};
        try {
            FurniturePackBuilder.ValidateAll();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.40f,.43f,.47f);
            StudioReflection();
            camera=new GameObject("Professional review camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.30f,.32f,.33f);camera.fieldOfView=36;camera.nearClipPlane=.015f;camera.farClipPlane=60;
            sun=new GameObject("Controlled key").AddComponent<Light>();sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;
            fill=new GameObject("Controlled fill").AddComponent<Light>();fill.type=LightType.Directional;fill.transform.rotation=Quaternion.Euler(25,140,0);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Review floor";floor.transform.position=new Vector3(0,-.075f,0);floor.transform.localScale=new Vector3(30,.15f,30);
            var floorMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMat.SetColor("_BaseColor",new Color(.38f,.36f,.32f));floor.GetComponent<Renderer>().sharedMaterial=floorMat;
            var catalog=JsonUtility.FromJson<FurniturePackBuilder.Catalog>(File.ReadAllText(Root+"/Documentation/catalog.json"));
            foreach(var row in catalog.assets) {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+row.category+"/"+row.slug+".prefab");
                var baseline=AssetDatabase.LoadAssetAtPath<GameObject>(Before+"/Prefabs/"+row.category+"/"+row.slug+".prefab");
                if(prefab==null||baseline==null)throw new Exception("Missing before/after prefab "+row.id);
                var current=UnityEngine.Object.Instantiate(prefab);var old=UnityEngine.Object.Instantiate(baseline);
                var group=current.GetComponent<LODGroup>();group.ForceLOD(0);old.GetComponent<LODGroup>().ForceLOD(0);
                current.GetComponent<FurnitureWear>().Apply();old.GetComponent<FurnitureWear>().Apply();
                Bounds bounds=BoundsOf(current);bounds.Encapsulate(BoundsOf(old));old.SetActive(false);
                ValidateMeshes(current,row.id);
                var dirs=new[]{new Vector3(0,.15f,1),new Vector3(0,.15f,-1),new Vector3(-1,.15f,0),new Vector3(1,.15f,0),new Vector3(0,1,.001f),new Vector3(1.3f,.83f,1.7f)};
                var names=new[]{"Front","Back","Left","Right","Top","Perspective"};
                Lighting("Day");
                for(int k=0;k<dirs.Length;k++) {
                    Place(bounds,dirs[k]);Capture(row.id,names[k],"Day","After");
                    if(k==5){current.SetActive(false);old.SetActive(true);Capture(row.id,names[k],"Day","Before");old.SetActive(false);current.SetActive(true);}
                }
                foreach(var light in new[]{"Overcast","Grazing"}){Lighting(light);Place(bounds,dirs[5]);Capture(row.id,"Perspective",light,"After");}
                Lighting("Day");
                var detail=current.GetComponentsInChildren<FurnitureMechanism>(true).FirstOrDefault()?.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name.EndsWith("_LOD0"));
                var detailBounds=detail!=null?detail.bounds:new Bounds(bounds.center+Vector3.up*bounds.extents.y*.2f,Vector3.one*Mathf.Min(.5f,bounds.size.magnitude*.45f));
                detailBounds.size=Vector3.Max(detailBounds.size,Vector3.one*.16f);Place(detailBounds,dirs[5]);Capture(row.id,"Detail","Day","After");
                for(int lod=1;lod<3;lod++){group.ForceLOD(lod);Place(bounds,dirs[5]);Capture(row.id,"LOD"+lod,"Day","After");}
                group.ForceLOD(0);
                var motions=current.GetComponentsInChildren<FurnitureMechanism>(true);
                if(motions.Length>0) {
                    for(int pass=0;pass<6;pass++)foreach(var motion in motions)
                        if(!motion.StableId.StartsWith("LooseSeat",StringComparison.Ordinal)&&!(new[]{1,2}.Contains(row.id)&&motion.StableId=="LinenSeat"))motion.SetOpen(true,true);
                    foreach(var cord in current.GetComponentsInChildren<FurnitureCordVisual>(true))cord.RebuildIfNeeded();
                    Place(BoundsOf(current),dirs[5]);Capture(row.id,"Mechanisms","Day","After");
                }
                UnityEngine.Object.DestroyImmediate(current);UnityEngine.Object.DestroyImmediate(old);report.models++;Debug.Log("FURNITURE_PROFESSIONAL_REVIEW "+row.id);
            }
            var shader=Shader.Find("PostApocalypticFurniture/Wear Lit");
            foreach(var message in ShaderUtil.GetShaderMessages(shader))if(message.severity.ToString()=="Error")report.issues.Add("Shader: "+message.message);
            if(new Material(shader).FindPass("Meta")<0)report.issues.Add("Missing Meta pass");
            BakeWitness();
            WaitForWitnessRender();
        } catch(Exception e){report.issues.Add(e.ToString());report.passed=false;File.WriteAllText(Path.Combine(Proof,"native_review.json"),JsonUtility.ToJson(report,true));Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static int witnessFrames,lastForeground;
    public static void RunBakeWitness()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/PostApocalypticFurniture/UnityProject/"))throw new Exception("Isolated furniture project only");
        report=JsonUtility.FromJson<Summary>(File.ReadAllText(Path.Combine(Proof,"native_review.json")));
        report.images.RemoveAll(f=>f.id==0);report.issues.RemoveAll(v=>v.Contains("Baked witness geometry is not visible"));report.lightmapWitnessPixels=0;report.passed=false;
        try {BakeWitness();WaitForWitnessRender();}catch(Exception e){Fail(e);}
    }
    static void WaitForWitnessRender(){witnessFrames=0;EditorApplication.update+=FinishWitness;EditorApplication.QueuePlayerLoopUpdate();}
    static void FinishWitness()
    {
        if(++witnessFrames<4){EditorApplication.QueuePlayerLoopUpdate();return;}
        EditorApplication.update-=FinishWitness;
        try {
            Capture(0,"LightmapWitness","Baked","After");report.lightmapWitnessPixels=lastForeground;
            foreach(var message in ShaderUtil.GetShaderMessages(Shader.Find("PostApocalypticFurniture/Wear Lit")))if(message.severity.ToString()=="Error")report.issues.Add("Post-bake shader: "+message.message);
            if(lastForeground<3000)report.issues.Add("Baked witness geometry is not visible: "+lastForeground);
            report.frames=report.images.Count;report.passed=report.models==200&&report.issues.Count==0&&report.lightmapBaked;
            File.WriteAllText(Path.Combine(Proof,"native_review.json"),JsonUtility.ToJson(report,true));
            if(!report.passed)throw new Exception(string.Join("; ",report.issues));
            Debug.Log("FURNITURE_PROFESSIONAL_REVIEW_PASS "+report.frames+" baked visible pixels "+lastForeground);EditorApplication.Exit(0);
        }catch(Exception e){Fail(e);}
    }
    static void Fail(Exception e){report.issues.Add(e.ToString());report.passed=false;File.WriteAllText(Path.Combine(Proof,"native_review.json"),JsonUtility.ToJson(report,true));Debug.LogException(e);EditorApplication.Exit(1);}
    static Bounds BoundsOf(GameObject go){var rr=go.GetComponentsInChildren<Renderer>(true).Where(r=>r.gameObject.activeInHierarchy&&r.name.EndsWith("_LOD0")).ToArray();if(rr.Length==0)throw new Exception("No LOD0");var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
    static void StudioReflection()
    {
        // Shared neutral HDR studio context for both sides of the comparison.
        // This is a material review environment, not a scene mirror simulation.
        const int n=64;var cube=new Cubemap(n,TextureFormat.RGBAHalf,false);
        foreach(CubemapFace face in new[]{CubemapFace.PositiveX,CubemapFace.NegativeX,CubemapFace.PositiveY,CubemapFace.NegativeY,CubemapFace.PositiveZ,CubemapFace.NegativeZ}) {
            var pixels=new Color[n*n];for(int y=0;y<n;y++)for(int x=0;x<n;x++) {
                float u=2*(x+.5f)/n-1,v=2*(y+.5f)/n-1;
                Vector3 d=face==CubemapFace.PositiveX?new Vector3(1,-v,-u):face==CubemapFace.NegativeX?new Vector3(-1,-v,u):face==CubemapFace.PositiveY?new Vector3(u,1,v):face==CubemapFace.NegativeY?new Vector3(u,-1,-v):face==CubemapFace.PositiveZ?new Vector3(u,-v,1):new Vector3(-u,-v,-1);d.Normalize();
                var c=Color.Lerp(new Color(.10f,.09f,.08f),new Color(.55f,.62f,.70f),Mathf.SmoothStep(0,1,d.y*.5f+.5f));
                float panel=Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,new Vector3(-.6f,.65f,.45f).normalized)),50);c+=new Color(2.3f,2.2f,2.0f)*panel;pixels[y*n+x]=c;
            }cube.SetPixels(pixels,face);
        }cube.Apply(false);RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=cube;RenderSettings.reflectionIntensity=1;
    }
    static void Place(Bounds b,Vector3 dir){camera.transform.position=b.center+dir.normalized*Mathf.Max(.55f,b.extents.magnitude*3.6f);camera.transform.LookAt(b.center);}
    static void Lighting(string kind){sun.transform.rotation=kind=="Grazing"?Quaternion.Euler(8,-65,0):Quaternion.Euler(45,-35,0);sun.intensity=kind=="Overcast"?.35f:2f;fill.intensity=kind=="Overcast"?1.2f:1f;RenderSettings.ambientLight=kind=="Grazing"?new Color(.20f,.22f,.25f):new Color(.40f,.43f,.47f);}
    static void Capture(int id,string view,string lighting,string stage)
    {
        string folder=Path.Combine(Proof,"Native",stage);Directory.CreateDirectory(folder);string path=Path.Combine(folder,$"{id:D3}_{view}_{lighting}.png");
        var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var tex=new Texture2D(512,512,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,512,512),0,0);tex.Apply();var pixels=tex.GetPixels32();int pink=pixels.Count(c=>c.r>220&&c.b>220&&c.g<45);
        var bg=pixels[0];lastForeground=pixels.Count(c=>Mathf.Abs(c.r-bg.r)+Mathf.Abs(c.g-bg.g)+Mathf.Abs(c.b-bg.b)>24);
        if(pink>8)report.issues.Add($"Magenta {id} {view} {lighting}: {pink}");
        File.WriteAllBytes(path,tex.EncodeToPNG());report.images.Add(new Frame{id=id,view=stage+"/"+view,lighting=lighting,file=path.Substring(Proof.Length+1).Replace('\\','/'),cameraPosition=camera.transform.position,target=camera.transform.position+camera.transform.forward,magenta=pink});
        camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
    }
    static void ValidateMeshes(GameObject go,int id)
    {
        foreach(var r in go.GetComponentsInChildren<MeshRenderer>(true)) {
            foreach(var material in r.sharedMaterials) {
                if(material==null||material.shader==null){report.issues.Add(id+" missing material/shader "+r.name);continue;}
                foreach(var property in material.shader.name=="PostApocalypticFurniture/Wear Lit"?new[]{"_BaseMap","_BumpMap","_MaskMap","_WearMap"}:new[]{"_BaseMap"})
                    if(material.GetTexture(property)==null)report.issues.Add(id+" missing texture "+material.name+" "+property);
            }
            if(r.transform.lossyScale.x*r.transform.lossyScale.y*r.transform.lossyScale.z<=0)report.issues.Add(id+" negative/zero renderer scale "+r.name);
            var m=r.GetComponent<MeshFilter>()?.sharedMesh;if(m==null){report.issues.Add(id+" Missing Mesh");continue;}
            if(m.uv.Length!=m.vertexCount||m.uv2.Length!=m.vertexCount||m.normals.Length!=m.vertexCount||m.tangents.Length!=m.vertexCount)report.issues.Add(id+" incomplete UV/normals/tangents "+r.name);
            if(m.normals.Any(n=>!float.IsFinite(n.x)||!float.IsFinite(n.y)||!float.IsFinite(n.z)||n.sqrMagnitude<.5f))report.issues.Add(id+" invalid normals "+r.name);
        }
    }
    static void BakeWitness()
    {
        // A small real CPU bake proves that Meta and LIGHTMAP_ON variants work.
        // It does not establish GI acceptance for a furnished gameplay level.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.customReflectionTexture=null;RenderSettings.defaultReflectionMode=DefaultReflectionMode.Skybox;
        var settings=new LightingSettings();settings.name="FurnitureProfessionalLightingSettings";var lm=typeof(LightingSettings).GetProperty("lightmapper");if(lm!=null)lm.SetValue(settings,Enum.Parse(lm.PropertyType,"ProgressiveCPU"));
        foreach(var pair in new[]{("directSampleCount",16),("indirectSampleCount",16),("environmentSampleCount",16),("maxBounces",1),("lightmapMaxSize",128)}){var property=typeof(LightingSettings).GetProperty(pair.Item1);if(property!=null&&property.CanWrite)property.SetValue(settings,pair.Item2);}
        var so=new SerializedObject(settings);
        foreach(var entry in new[]{("m_LightmapResolution",12f),("m_LightmapPadding",4f)}){var p=so.FindProperty(entry.Item1);if(p!=null){if(p.propertyType==SerializedPropertyType.Float)p.floatValue=entry.Item2;else if(p.propertyType==SerializedPropertyType.Integer)p.intValue=(int)entry.Item2;}}
        so.ApplyModifiedPropertiesWithoutUndo();settings.bakedGI=true;settings.realtimeGI=false;
        const string settingsPath="Assets/FurnitureProfessionalLightingSettings.asset";
        var previous=AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);
        if(previous!=null){EditorUtility.CopySerialized(settings,previous);UnityEngine.Object.DestroyImmediate(settings);settings=previous;}else AssetDatabase.CreateAsset(settings,settingsPath);
        Lightmapping.lightingSettings=settings;AssetDatabase.SaveAssets();
        var paths=new[]{Root+"/Materials/Wood.mat",Root+"/Materials/Steel.mat"};var renderers=new List<Renderer>();
        for(int k=0;k<2;k++){var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Lightmap witness "+k;cube.transform.position=new Vector3(k*1.3f,.5f,0);cube.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(paths[k]);GameObjectUtility.SetStaticEditorFlags(cube,StaticEditorFlags.ContributeGI);renderers.Add(cube.GetComponent<Renderer>());}
        var lamp=new GameObject("Baked witness sun").AddComponent<Light>();lamp.type=LightType.Directional;lamp.lightmapBakeType=LightmapBakeType.Baked;lamp.intensity=2;lamp.transform.rotation=Quaternion.Euler(50,25,0);
        string scene="Assets/FurnitureProfessionalBakeWitness.unity";EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),scene);
        report.lightmapBaked=Lightmapping.Bake()&&LightmapSettings.lightmaps.Length>0&&renderers.All(r=>r.lightmapIndex>=0&&r.lightmapIndex<LightmapSettings.lightmaps.Length);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),scene);
        report.lightmaps=LightmapSettings.lightmaps.Length;if(!report.lightmapBaked)report.issues.Add("Real lightmap bake witness failed");
        camera=new GameObject("Baked witness camera").AddComponent<Camera>();camera.transform.position=new Vector3(3,2,4);camera.transform.LookAt(new Vector3(.6f,.5f,0));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.22f,.26f);
    }
}
