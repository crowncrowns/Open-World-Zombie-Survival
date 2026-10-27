using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdvancedCableTool;
using AdvancedCableTool.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// This builder is deliberately outside the delivered package. Never run on the host scene.
[InitializeOnLoad]
public static class CableVerification
{
    const string Root = "Assets/AdvancedCableTool";
    static readonly string Evidence = Path.GetFullPath("../../../Verification/AdvancedCableTool");
    static readonly List<string> checks = new List<string>();
    static string checkFile = "editor_checks.txt";
    static int playFrames;
    static Vector3 initialFree;
    static CableGenerator hanging;
    static CableVerification() { EditorApplication.playModeStateChanged += PlayState; }
    static void Guard()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/work/AdvancedCableTool/UnityProject/Assets"))
            throw new InvalidOperationException("Verification is restricted to work/AdvancedCableTool/UnityProject");
        Directory.CreateDirectory(Evidence);
    }
    static void Check(bool ok, string message)
    {
        checks.Add((ok ? "PASS " : "FAIL ") + message);
        File.AppendAllText(Path.Combine(Evidence,checkFile),(ok?"PASS ":"FAIL ")+message+Environment.NewLine);
        if (!ok) throw new Exception(message);
    }
    public static void Build()
    {
        Guard(); checks.Clear();
        File.WriteAllText(Path.Combine(Evidence,checkFile),"");
        SessionState.SetBool("CableVerification.Failed",false);
        if(File.Exists(Path.Combine(Evidence,"failure.txt")))File.Delete(Path.Combine(Evidence,"failure.txt"));
        try
        {
            foreach (string sub in new[] { "Materials", "Prefabs", "Demo", "Demo/Meshes", "Demo/RenderSettings" }) Directory.CreateDirectory(Root + "/" + sub);
            AssetDatabase.Refresh();
            ConfigureTextures(); CreateMaterials(); ConfigurePipeline(); BuildDemo(); EditorChecks();
            AssetDatabase.SaveAssets();
            SessionState.SetBool("CableVerification.Play", true); EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(Evidence,"failure.txt"), ex.ToString()); Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    public static void ChecksOnly()
    {
        Guard(); checks.Clear();
        File.WriteAllText(Path.Combine(Evidence,checkFile),"");
        try { EditorSceneManager.OpenScene(Root+"/Demo/CableDemo.unity"); EditorChecks(); EditorApplication.Exit(0); }
        catch(Exception ex){File.WriteAllText(Path.Combine(Evidence,"failure.txt"),ex.ToString());Debug.LogException(ex);EditorApplication.Exit(1);}
    }
    public static void ExportOnly()
    {
        Guard(); AssetDatabase.Refresh();
        AssetDatabase.ExportPackage(Root,Path.Combine(Evidence,"AdvancedCableTool.unitypackage"),ExportPackageOptions.Recurse);
        EditorApplication.Exit(0);
    }
    static void ConfigureTextures()
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] {Root + "/Textures"}))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                bool change=ti.textureType!=(path.Contains("/Normal/")?TextureImporterType.NormalMap:TextureImporterType.Default)
                    || ti.sRGBTexture!=path.Contains("/BaseColor/") || ti.wrapMode!=TextureWrapMode.Repeat || ti.maxTextureSize!=2048
                    || ti.textureCompression!=TextureImporterCompression.CompressedHQ || ti.anisoLevel!=8;
                ti.textureType = path.Contains("/Normal/") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = path.Contains("/BaseColor/"); ti.wrapMode = TextureWrapMode.Repeat;
                ti.maxTextureSize = 2048; ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.anisoLevel = 8; if(change)ti.SaveAndReimport();
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        Check(AssetDatabase.FindAssets("t:Texture2D",new[]{Root+"/Textures"}).Length == 108,"108 real 2048 PBR maps imported");
    }
    static void CreateMaterials()
    {
        var library=AssetDatabase.LoadAssetAtPath<CableMaterialLibrary>(Root+"/Materials/CableLibrary.asset");
        if(!library){library=ScriptableObject.CreateInstance<CableMaterialLibrary>();AssetDatabase.CreateAsset(library,Root+"/Materials/CableLibrary.asset");}
        foreach (CableType type in Enum.GetValues(typeof(CableType))) foreach (CableWear wear in Enum.GetValues(typeof(CableWear)))
        {
            string name = type + "_" + wear, path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
            mat.SetTexture("_BaseMap",Tex("BaseColor",name)); mat.SetTexture("_BumpMap",Tex("Normal",name));
            mat.SetTexture("_MetallicGlossMap",Tex("MetallicSmoothness",name)); mat.SetTexture("_OcclusionMap",Tex("AO",name));
            mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.EnableKeyword("_OCCLUSIONMAP");
            mat.SetFloat("_Smoothness",1); mat.SetFloat("_Metallic",1); mat.SetFloat("_BumpScale",.6f); EditorUtility.SetDirty(mat);
            library.variants[(int)type*3+(int)wear]=mat;
        }
        var copper=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/ExposedCopper.mat");
        if(!copper){copper=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(copper,Root+"/Materials/ExposedCopper.mat");}
        copper.SetTexture("_BaseMap",Tex("BaseColor","Damaged_Used"));copper.SetTexture("_BumpMap",Tex("Normal","Steel_New"));
        copper.color=new Color(2.5f,1.6f,.9f);copper.SetFloat("_Metallic",.92f);copper.SetFloat("_Smoothness",.55f);copper.EnableKeyword("_NORMALMAP");
        library.copper=copper;EditorUtility.SetDirty(library);EditorUtility.SetDirty(copper);
    }
    static Texture2D Tex(string channel,string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+channel+"/"+name+".png");
    static void ConfigurePipeline()
    {
        string p = Root + "/Demo/RenderSettings/";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(p+"CableDemoRenderer.asset");
        if (!renderer) { renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(renderer,p+"CableDemoRenderer.asset"); }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(p+"CableDemoURP.asset");
        if (!pipeline) { pipeline = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline,p+"CableDemoURP.asset"); }
        pipeline.msaaSampleCount = 4; pipeline.shadowDistance = 80;
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        PlayerSettings.colorSpace = ColorSpace.Linear;
    }
    static GameObject Block(string name,Vector3 pos,Vector3 scale, Material material = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = pos; go.transform.localScale = scale;
        if (material) go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }
    static CableGenerator Span(string name,Vector3 a,Transform supportA,Vector3? b,Transform supportB,float length,CableType type,bool physics = false)
    {
        var c = CableToolWindow.CreateCable(a,supportA,b,supportB,length,type); c.name = name; c.diameter = type == CableType.Industrial ? .11f : type == CableType.Steel ? .065f : .05f;
        c.segmentCount = 80; c.physicsSegmentLimit = 64; c.physicsEnabled = physics; c.windStrength = physics ? 2 : 0; c.Rebuild(); return c;
    }
    static void BuildDemo()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.45f,.52f,.6f); RenderSettings.ambientEquatorColor = new Color(.22f,.25f,.28f); RenderSettings.ambientGroundColor = new Color(.13f,.12f,.1f);
        var concrete = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Demo concrete" }; concrete.color = new Color(.25f,.28f,.3f);
        string matPath=Root+"/Demo/RenderSettings/Concrete.mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(existing){Object.DestroyImmediate(concrete);concrete=existing;}else AssetDatabase.CreateAsset(concrete,matPath);
        Block("Ground",new Vector3(0,-.25f,0),new Vector3(40,.5f,32),concrete);
        var p1=Block("Столб A",new Vector3(-14,3,-8),new Vector3(.35f,6,.35f),concrete);
        var p2=Block("Столб B",new Vector3(-3,3,-8),new Vector3(.35f,6,.35f),concrete);
        Span("01 · Между столбами",new Vector3(-14,5.7f,-7.7f),p1.transform,new Vector3(-3,5.7f,-7.7f),p2.transform,12.5f,CableType.Electric);
        var wall1=Block("Здание A",new Vector3(3,2,-8),new Vector3(2,4,4),concrete);
        var wall2=Block("Здание B",new Vector3(15,2,-8),new Vector3(2,4,4),concrete);
        Span("02 · Провисание между зданиями",new Vector3(3,3.8f,-5.9f),wall1.transform,new Vector3(15,3.8f,-5.9f),wall2.transform,13.8f,CableType.Soviet);
        var wall3=Block("Стена свободного конца",new Vector3(-14,2,1),new Vector3(1,4,3),concrete);
        Span("03 · Свободный конец · ветер",new Vector3(-13.4f,3.6f,1),wall3.transform,null,null,2.5f,CableType.Damaged,true);
        var fallen=Span("04 · Оборванный кабель на земле",new Vector3(-5,1.2f,1),null,new Vector3(1,1.2f,1),null,7,CableType.Industrial,true);
        fallen.DetachAll(); fallen.Rebuild(); fallen.editorSimulation=true;
        for(int i=0;i<140;i++) fallen.PreviewStep(.02f);
        fallen.CommitShape(); fallen.editorSimulation=false; fallen.Rebuild();
        var pa=Block("Траверса A",new Vector3(5,2.5f,1),new Vector3(.3f,5,2),concrete);
        var pb=Block("Траверса B",new Vector3(15,2.5f,1),new Vector3(.3f,5,2),concrete);
        for(int i=0;i<5;i++) Span("05 · Пучок / "+(i+1),new Vector3(5,4.6f,.2f+i*.3f),pa.transform,new Vector3(15,4.6f,.2f+i*.3f),pb.transform,11+i*.12f,CableType.Power);
        var ma=Block("Опора цепи A",new Vector3(-12,2,10),new Vector3(.3f,4,.3f),concrete);
        var mm=Block("Опора цепи M",new Vector3(0,2.5f,10),new Vector3(.3f,5,.3f),concrete);
        var mb=Block("Опора цепи B",new Vector3(12,2,10),new Vector3(.3f,4,.3f),concrete);
        var multi=Span("06 · Три опоры / стальной трос",new Vector3(-12,3.8f,10.3f),ma.transform,new Vector3(12,3.8f,10.3f),mb.transform,27,CableType.Steel);
        var anchor=new CableAnchor(); anchor.Attach(new Vector3(0,4.8f,10.3f),mm.transform); multi.anchors.Insert(1,anchor); multi.Rebuild();
        var sun=new GameObject("Sun").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=2; sun.shadows=LightShadows.Soft; sun.transform.rotation=Quaternion.Euler(48,-32,0);
        var cam=new GameObject("Camera").AddComponent<Camera>(); cam.tag="MainCamera"; cam.transform.position=new Vector3(25,19,-28); cam.transform.LookAt(new Vector3(0,2,1)); cam.fieldOfView=52; cam.nearClipPlane=.05f; cam.backgroundColor=new Color(.12f,.16f,.21f); cam.clearFlags=CameraClearFlags.SolidColor;
        foreach(var c in Object.FindObjectsByType<CableGenerator>(FindObjectsSortMode.None)) c.CommitShape();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),Root+"/Demo/CableDemo.unity");
        string[] names={"ElectricCable","IndustrialCable","SteelWire","BrokenCable"}; CableType[] types={CableType.Electric,CableType.Industrial,CableType.Steel,CableType.Damaged};
        for(int i=0;i<4;i++)
        {
            var c=Span(names[i],new Vector3(0,3,0),null,i==3?(Vector3?)null:new Vector3(5,3,0),null,6,types[i],i==3);
            CableToolWindow.SavePrefabAt(c,Root+"/Prefabs/"+names[i]+".prefab"); Object.DestroyImmediate(c.gameObject);
        }
        Check(Object.FindObjectsByType<CableGenerator>(FindObjectsSortMode.None).Length==10,"Demo: six scenarios / ten independent cables");
        Capture(cam,"Demo_Overview",1600,1000);
        int ci=0;
        foreach(var c in Object.FindObjectsByType<CableGenerator>(FindObjectsSortMode.None).OrderBy(c=>c.name))
        {
            Bounds bounds=c.transform.Find("Cable_LOD0").GetComponent<Renderer>().bounds;
            cam.transform.position=bounds.center+new Vector3(1,.65f,-1.1f)*Mathf.Max(1.5f,bounds.size.magnitude*.65f); cam.transform.LookAt(bounds.center);
            Capture(cam,"Cable_"+(++ci).ToString("D2"),1200,800);
        }
        cam.transform.position=new Vector3(25,19,-28); cam.transform.LookAt(new Vector3(0,2,1));
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
    static void Capture(Camera camera,string name,int w,int h)
    {
        var rt=new RenderTexture(w,h,24); camera.targetTexture=rt; camera.Render();
        var old=RenderTexture.active; RenderTexture.active=rt; var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply(); File.WriteAllBytes(Path.Combine(Evidence,name+".png"),tex.EncodeToPNG());
        camera.targetTexture=null; RenderTexture.active=old; rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
    }
    static void EditorChecks()
    {
        Vector3 start=new Vector3(0,4,-13),end=new Vector3(6,5,-13); float measured=0;Vector3 prev=start;
        for(int i=1;i<=1024;i++) {var p=CableCatenary.Point(start,end,8,Physics.gravity,i/1024f);measured+=Vector3.Distance(prev,p);prev=p;}
        Check(Mathf.Abs(measured-8)<.002f,"Catenary arc length 8m, unequal-height anchors, error <2mm");
        var support=Block("Verification support",start-Vector3.up*.3f,Vector3.one*.2f);
        var c=Span("Verification Cable",start,support.transform,end,null,8,CableType.Electric);
        Check(c.Points.Length==81 && c.GetComponent<LODGroup>().lodCount==3,"Real tube mesh and three LODs");
        Check(c.transform.Find("Cable_Clamps").GetChild(0).GetComponent<MeshFilter>().sharedMesh.vertexCount>200,"Clamps use real shared circular band geometry and mounting tabs");
        var mf=c.transform.Find("Cable_LOD0").GetComponent<MeshFilter>();
        Check(mf.sharedMesh.vertexCount>900 && mf.sharedMesh.normals.Length==mf.sharedMesh.vertexCount,"Tube normals, indexed geometry");
        var mesh=mf.sharedMesh;var tri=mesh.triangles;var vertices=mesh.vertices;var normal=Vector3.Cross(vertices[tri[1]]-vertices[tri[0]],vertices[tri[2]]-vertices[tri[0]]).normalized;
        Check(Vector3.Dot(normal,mesh.normals[tri[0]])>.8f,"Tube triangles face outward");
        c.wearLevel=CableWear.Damaged;c.EnsureUpdated();Check(c.material==c.materialLibrary.Get(c.cableType,CableWear.Damaged),"Wear selection updates preset material");
        c.wearLevel=CableWear.Used;c.EnsureUpdated();
        c.cableType=CableType.Damaged;c.Rebuild();Check(mesh.subMeshCount==2,"Damaged cable has separate exposed-copper surface");
        c.cableType=CableType.Electric;c.Rebuild();Check(mesh.subMeshCount==1 && mf.GetComponent<Renderer>().sharedMaterials.Length==1,"Returning from damaged type resets material slots");
        float originalLength=c.cableLength; support.transform.position+=Vector3.up*.5f;c.EnsureUpdated();
        Check(Vector3.Distance(c.Points[0],start+Vector3.up*.5f)<.001f && c.cableLength==originalLength,"Moving Transform support A updates shape without changing rest length");
        c.anchors[1].Attach(end+Vector3.up*.4f);c.Rebuild();Check(Vector3.Distance(c.Points[c.Points.Length-1],end+Vector3.up*.4f)<.001f,"Moving anchor B");
        Undo.IncrementCurrentGroup();Undo.RecordObject(c,"Cable diameter check"); c.diameter=.1f; c.Rebuild(); Undo.FlushUndoRecordObjects();Undo.IncrementCurrentGroup();Undo.PerformUndo(); c.Rebuild();
        Check(Mathf.Abs(c.diameter-.05f)<.001f,"Undo diameter");Undo.PerformRedo();c.Rebuild();Check(Mathf.Abs(c.diameter-.1f)<.001f,"Redo diameter");
        c.diameter=.06f;c.cableLength=9;c.Rebuild();Check(Mathf.Abs(c.MeasuredLength-9)<.025f,"Length changes catenary");
        float uvEnd=mf.sharedMesh.uv[80*13].y; Check(Mathf.Abs(uvEnd-c.MeasuredLength*c.uvTiling)<.01f,"UV V = physical arc length times tiling");
        var disposable=new GameObject("Deleted support B"); disposable.transform.position=end;c.anchors[1].Attach(end,disposable.transform);c.Rebuild();Object.DestroyImmediate(disposable);c.EnsureUpdated();
        Check(!c.anchors[1].attached && c.Points.Length>0,"Deleting support B leaves existing cable intact");
        c.physicsEnabled=true;c.editorSimulation=true;c.Rebuild();Vector3 before=c.Points[c.Points.Length-1];
        for(int i=0;i<30;i++) c.PreviewStep(.02f);
        Check(Vector3.Distance(before,c.Points[c.Points.Length-1])>.02f,"Free end responds to gravity / wind");
        Check(Vector3.Distance(c.Points[0],c.anchors[0].Resolve())<.001f,"Verlet first end remains pinned");
        Physics.SyncTransforms();
        bool groundHit=Physics.Raycast(new Vector3(10,10,-13),Vector3.down,out RaycastHit ground,30);
        Check(groundHit && Mathf.Abs(ground.point.y)<.01f,"Verification ground Collider ray: "+(groundHit?ground.collider.name+" Y="+ground.point.y:"MISS"));
        c.DetachAll();c.Rebuild();
        var debug=new List<string>();
        for(int i=0;i<180;i++){c.PreviewStep(.02f);if(i%10==0)debug.Add(i+" min="+c.Points.Min(p=>p.y)+" minPos="+c.Points.OrderBy(p=>p.y).First()+" max="+c.Points.Max(p=>p.y)+" length="+c.MeasuredLength+" mask="+c.collisionMask.value+" collision="+c.collisionEnabled);}
        File.WriteAllLines(Path.Combine(Evidence,"contact_diagnostics.txt"),debug);
        float min=c.Points.Min(p=>p.y);Check(min>=c.diameter*.5f-.004f,"Detached cable collides with ground; minimum Y="+min.ToString("F4"));
        Check(Mathf.Abs(c.MeasuredLength-c.cableLength)/c.cableLength<.035f,"Verlet length constraint relative error below 3.5 percent after contact");
        c.CommitShape();c.physicsEnabled=false;c.editorSimulation=false;c.Rebuild();Vector3 frozen=c.Points[20];
        c.Rebuild();Check(Vector3.Distance(frozen,c.Points[20])<.001f,"Static freeze preserves simulated shape");
        Object.DestroyImmediate(c.gameObject);Object.DestroyImmediate(support);
        // The same placement entry point used by SceneGUI; raycast hits, not fabricated hit data.
        var surface=Block("Click surface",new Vector3(0,4,-13),new Vector3(8,8,.3f)); Physics.SyncTransforms();
        Physics.Raycast(new Vector3(-2,6,-16),Vector3.forward,out RaycastHit hitA,5);
        Physics.Raycast(new Vector3(2,6,-16),Vector3.forward,out RaycastHit hitB,5);
        var window=ScriptableObject.CreateInstance<CableToolWindow>();window.BeginDrawing();
        int count=Object.FindObjectsByType<CableGenerator>(FindObjectsSortMode.None).Length;
        Check(window.PlaceHit(hitA),"First Scene placement click accepted");var created=Selection.activeGameObject.GetComponent<CableGenerator>();
        Check(created && !created.anchors[1].attached,"One-click cable remains visible / free end");
        Check(window.PlaceHit(hitB) && created.anchors[1].attached,"Second Scene placement click attaches end B");
        Check(Object.FindObjectsByType<CableGenerator>(FindObjectsSortMode.None).Length==count+1,"Two clicks create one cable");
        Physics.Raycast(new Vector3(0,7,-16),Vector3.forward,out RaycastHit hitM,5);
        window.BeginIntermediate();window.PlaceHit(hitM);Check(created.anchors.Count==3,"Intermediate support inserted through Scene placement handler");
        created.DetachEndB();created.Rebuild();window.BeginReattach();window.PlaceHit(hitB);Check(created.anchors[2].attached,"Freed B can be reattached through Scene handler");
        created.anchors.Clear();created.Rebuild();Check(created.anchors.All(a=>!a.attached) && created.Points.Length>0,"Deleting all authoring anchors preserves a fully detached cable");
        Object.DestroyImmediate(created.gameObject);
        window.SetBundle(5,.12f,.2f);window.BeginDrawing();window.PlaceHit(hitA);window.PlaceHit(hitB);
        var bundle=Object.FindObjectsByType<CableGenerator>(FindObjectsSortMode.None).Where(v=>v.name.StartsWith("Cable Electric")).ToArray();
        Check(bundle.Length==5,"Five parallel wires created by two-click handler");
        Check(bundle.Select(v=>v.cableLength).Distinct().Count()>1,"Parallel wires have deterministic slack variation");
        var duplicate=Span("Copy target",new Vector3(-2,7,-15),null,new Vector3(2,7,-15),null,5,CableType.Steel);
        Vector3 originalAnchor=duplicate.anchors[0].Resolve();duplicate.CopySettingsFrom(bundle[0]);duplicate.Rebuild();
        Check(duplicate.material==bundle[0].material && duplicate.anchors[0].Resolve()==originalAnchor,"Copy settings preserves target anchors");
        Object.DestroyImmediate(duplicate.gameObject);
        Selection.objects=new Object[]{bundle[0].gameObject,bundle[1].gameObject};CableToolWindow.JoinSelection();
        Check(bundle[0].anchors[bundle[0].anchors.Count-1].target==bundle[1].anchors[0].target,"Join cables shares a junction Transform");
        var junction=bundle[1].anchors[0].target;
        Selection.objects=new Object[]{bundle[2].gameObject,bundle[3].gameObject};
        Undo.IncrementCurrentGroup();CableToolWindow.BatchSelectionAt(Root+"/Demo/Meshes/VerificationBatch.asset");Undo.IncrementCurrentGroup();
        Check(!bundle[2].gameObject.activeSelf && !bundle[3].gameObject.activeSelf,"Static batch disables original cables");
        var batch=Selection.activeGameObject;Check(batch.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount>1000,"Static batch is persistent mesh geometry");
        Undo.PerformUndo();Check(bundle[2].gameObject.activeSelf && bundle[3].gameObject.activeSelf,"Undo static batch restores originals");
        foreach(var cable in bundle)Object.DestroyImmediate(cable.gameObject);
        if(batch)Object.DestroyImmediate(batch);if(junction)Object.DestroyImmediate(junction.gameObject);
        // A deliberately blocked fixed span must report an obstacle.
        var obstacle=Block("Verification obstacle",new Vector3(0,6,-14),new Vector3(.4f,6,2));Physics.SyncTransforms();
        var blocked=Span("Blocked route",new Vector3(-2,6,-14),null,new Vector3(2,6,-14),null,4.1f,CableType.Electric);
        Check(!blocked.ValidatePlacement() && blocked.Status.Contains("препятствие"),"Static cable intersection is detected and reported");
        Object.DestroyImmediate(blocked.gameObject);Object.DestroyImmediate(obstacle);
        Object.DestroyImmediate(window);Object.DestroyImmediate(surface);
        EditorSceneManager.OpenScene(Root+"/Demo/CableDemo.unity");
        Check(Object.FindObjectsByType<CableGenerator>(FindObjectsSortMode.None).Length==10,"Scene save / reopen preserves all ten cables");
    }
    static void PlayState(PlayModeStateChange change)
    {
        if(!SessionState.GetBool("CableVerification.Play",false))return;
        if(change==PlayModeStateChange.EnteredPlayMode)
        {
            playFrames=0;hanging=Object.FindObjectsByType<CableGenerator>(FindObjectsSortMode.None).First(c=>c.name.StartsWith("03"));hanging.EnsureUpdated();initialFree=hanging.Points[hanging.Points.Length-1];
            EditorApplication.update+=PlayTick;
        }
        if(change==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("CableVerification.Play",false);
            File.WriteAllText(Path.Combine(Evidence,"play_complete.txt"),SessionState.GetString("CableVerification.Result","MISSING"));
            AssetDatabase.ExportPackage(Root,Path.Combine(Evidence,"AdvancedCableTool.unitypackage"),ExportPackageOptions.Recurse);
            EditorApplication.Exit(SessionState.GetBool("CableVerification.Failed",false)?1:0);
        }
    }
    static void PlayTick()
    {
        if(!EditorApplication.isPlaying)return;
        ++playFrames;
        if(playFrames<350 || Time.time<4)return;
        try
        {
            Check(hanging.IsSimulating,"Actual Play Mode invokes cable FixedUpdate");
            Check(Vector3.Distance(initialFree,hanging.Points[hanging.Points.Length-1])>.01f,"Play Mode free end motion");
            Check(Vector3.Distance(hanging.Points[0],hanging.anchors[0].Resolve())<.001f,"Play Mode anchor pin");
            float simulationBefore=hanging.SimulationTime;
            for(int i=0;i<12;i++)hanging.Simulate(.02f);
            Check(hanging.SimulationTime-simulationBefore<=.0401f,"Play Mode caps catch-up simulation at two steps per rendered frame");
            hanging.AddImpulse(hanging.Points[hanging.Points.Length-1],Vector3.right*3,1);
            var cam=Camera.main; Capture(cam,"Play_Overview",1600,1000);
            SessionState.SetString("CableVerification.Result","PASS: real Play Mode, 350 editor updates, physics active, anchors and free end verified.");
        }
        catch(Exception ex){SessionState.SetBool("CableVerification.Failed",true);SessionState.SetString("CableVerification.Result",ex.ToString());}
        EditorApplication.update-=PlayTick;EditorApplication.ExitPlaymode();
    }
    public static void Cold()
    {
        Guard();checks.Clear();
        checkFile="cold_checks.txt";File.WriteAllText(Path.Combine(Evidence,checkFile),"");
        try
        {
            EditorSceneManager.OpenScene(Root+"/Demo/CableDemo.unity");
            foreach(var c in Object.FindObjectsByType<CableGenerator>(FindObjectsSortMode.None))
            {c.EnsureUpdated();Check(c.Points.Length>8 && c.material && c.transform.Find("Cable_LOD0").GetComponent<MeshFilter>().sharedMesh,"Cold scene: "+c.name);}
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var c=go.GetComponent<CableGenerator>();c.EnsureUpdated();
                Check(c.material && c.Points.Length>8,"Cold prefab: "+Path.GetFileName(path));
                if(c.anchors.All(a=>a.attached))
                {c.cableLength+=1;c.Rebuild();Check(Mathf.Abs(c.MeasuredLength-c.cableLength)<.03f,"Cold prefab remains editable: "+Path.GetFileName(path));}
                Object.DestroyImmediate(go);
            }
            File.WriteAllLines(Path.Combine(Evidence,"cold_checks.txt"),checks);
            AssetDatabase.ExportPackage(Root,Path.Combine(Evidence,"AdvancedCableTool.unitypackage"),ExportPackageOptions.Recurse);
            EditorApplication.Exit(0);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(Evidence,"cold_failure.txt"),ex.ToString());EditorApplication.Exit(1);}
    }
    public static void Gallery()
    {
        Guard();
        EditorSceneManager.OpenScene(Root+"/Demo/CableDemo.unity");
        Camera cam=Camera.main;
        foreach(var go in SceneManager.GetActiveScene().GetRootGameObjects())if(go!=cam.gameObject && !go.GetComponent<Light>())go.SetActive(false);
        cam.transform.position=new Vector3(.15f,2.8f,-2.1f);cam.transform.LookAt(new Vector3(0,2.5f,0));cam.fieldOfView=50;
        foreach(CableType type in Enum.GetValues(typeof(CableType)))foreach(CableWear wear in Enum.GetValues(typeof(CableWear)))
        {
            var c=Span("Material review",new Vector3(-1,3,0),null,new Vector3(1,3,0),null,2.5f,type);
            c.diameter=type==CableType.Industrial?.16f:.1f;c.wearLevel=wear;c.segmentCount=128;c.clamps=false;c.Rebuild();
            Capture(cam,"Material_"+type+"_"+wear,1200,800);Object.DestroyImmediate(c.gameObject);
        }
        AssetDatabase.ExportPackage(Root,Path.Combine(Evidence,"AdvancedCableTool.unitypackage"),ExportPackageOptions.Recurse);
        EditorApplication.Exit(0);
    }
    public static void OpenReview()
    {
        Guard();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Demo/RenderSettings/Concrete.mat");
        Block("UI Proof Ground",new Vector3(0,-.25f,0),new Vector3(24,.5f,16),mat);
        Block("UI Proof Support A",new Vector3(-3,3,0),new Vector3(1.5f,6,1),mat);
        Block("UI Proof Support B",new Vector3(3,3,0),new Vector3(1.5f,6,1),mat);
        var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(30,-30,0);light.intensity=2;
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Assets/CableUIReview.unity");
        var view=SceneView.GetWindow<SceneView>();view.LookAt(new Vector3(0,3,0),Quaternion.identity,7,true,true);view.Repaint();
        var window=EditorWindow.GetWindow<CableToolWindow>("Advanced Cable Tool");window.position=new Rect(100,100,450,770);window.Show();
    }
}
