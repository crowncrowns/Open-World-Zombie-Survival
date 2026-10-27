// Isolated Editor Play benchmark and native visual proof; never installed in the user's project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Furniture;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public static class FurnitureStressChecks
{
    const string Pending = "FurnitureStressChecks.pending";
    const string Root = "Assets/PostApocalypticFurniture";
    static IEnumerator routine;
    static int lastFrame;
    static double began;
    static readonly List<Counter> counters = new List<Counter>();
    static readonly List<string> checks = new List<string>();
    static string Proof => Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../Verification/PostApocalypticFurniture"));
    static Camera camera;
    static RenderTexture target;
    [Serializable] public sealed class Statistics { public int samples; public double mean,p95,min,max; }
    [Serializable] public sealed class CounterResult { public string name,unit,source,unavailableReason; public bool available; public Statistics values; }
    [Serializable] public sealed class ViewResult {
        public string name; public Vector3 cameraPosition,cameraTarget; public int warmupFrames,measuredFrames;
        public Statistics cameraRenderCpuMs,editorPlayFrameIntervalMs; public List<CounterResult> counters;
        public int[] predictedLodCounts; public string lodCountsMeaning;
    }
    [Serializable] public sealed class ProbeResult { public float distance; public int predictedLod; public int[] meshTriangles; public ViewResult measurement; }
    [Serializable] public sealed class Report {
        public bool passed; public string unity,measurementContext; public int furnitureInstances,apartments,width,height;
        public List<ViewResult> views = new List<ViewResult>(); public List<ProbeResult> lodProbe = new List<ProbeResult>();
        public List<string> checks; public string[] unavailableCounters;
    }
    sealed class Counter {
        public string name,unit; public bool available; public double multiplier = 1;
        public ProfilerRecorder recorder; public readonly List<double> values = new List<double>();
    }
    static FurnitureStressChecks() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        Guard(); Directory.CreateDirectory(Proof); SessionState.SetBool(Pending,true);
        EditorSceneManager.OpenScene(Root+"/DemoScenes/Furniture_StressTest.unity",OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    static void Guard()
    { if (!Application.dataPath.Replace('\\','/').Contains("/work/PostApocalypticFurniture/UnityProject/")) throw new InvalidOperationException("Furniture stress checks require the isolated work/PostApocalypticFurniture/UnityProject."); }
    static void OnPlay(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false)) return;
        checks.Clear(); routine = Scenario(); lastFrame = -1; began = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup-began > 600) throw new TimeoutException("Furniture stress benchmark exceeded 10 minutes.");
            if (routine.MoveNext()) return;
            SessionState.SetBool(Pending,false); EditorApplication.update -= Tick; DisposeRecorders();
            Debug.Log("FURNITURE_STRESS_PASS " + checks.Count); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Proof,"unity_stress_failure.txt"),error.ToString()); Debug.LogException(error);
            SessionState.SetBool(Pending,false); EditorApplication.update -= Tick; DisposeRecorders(); EditorApplication.Exit(1);
        }
    }
    static void Check(bool valid,string label)
    { if (!valid) throw new InvalidOperationException(label); checks.Add(label); }
    static Statistics Stats(IEnumerable<double> input)
    {
        var values = input.OrderBy(v=>v).ToArray(); if (values.Length==0) return new Statistics();
        return new Statistics{samples=values.Length,mean=values.Average(),min=values[0],max=values[values.Length-1],p95=values[Mathf.Clamp(Mathf.CeilToInt(values.Length*.95f)-1,0,values.Length-1)]};
    }
    static void StartRecorders()
    {
        var available = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(available);
        var desired = new[]{
            ("Draw Calls Count","calls",1d),("Batches Count","batches",1d),("SetPass Calls Count","calls",1d),
            ("Triangles Count","triangles",1d),("Vertices Count","vertices",1d),("Main Thread","ms",1e-6d),
            ("GC Allocated In Frame","bytes",1d),("GC Reserved Memory","bytes",1d),("GC Used Memory","bytes",1d)};
        foreach (var item in desired)
        {
            var counter = new Counter{name=item.Item1,unit=item.Item2,multiplier=item.Item3};
            foreach (var handle in available)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle); if (description.Name != item.Item1) continue;
                counter.recorder = ProfilerRecorder.StartNew(description.Category,description.Name,1); counter.available=counter.recorder.Valid; break;
            }
            counters.Add(counter);
        }
    }
    static void DisposeRecorders()
    { foreach (var counter in counters) if (counter.available) counter.recorder.Dispose(); counters.Clear(); }
    static double RenderFrame()
    {
        long start = Stopwatch.GetTimestamp(); camera.Render();
        return (Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;
    }
    static IEnumerator Measure(ViewResult view,int warmup,int samples)
    {
        view.warmupFrames=warmup; view.measuredFrames=samples;
        for (int i=0;i<warmup;i++) { RenderFrame(); yield return null; }
        foreach (var counter in counters) counter.values.Clear();
        var renderTimes = new List<double>(); var intervals = new List<double>();
        for (int i=0;i<samples;i++)
        {
            renderTimes.Add(RenderFrame()); intervals.Add(Time.unscaledDeltaTime*1000d);
            foreach (var counter in counters) if (counter.available && counter.recorder.Count>0) {
                double value=counter.recorder.LastValue*counter.multiplier;
                if(value==0&&counter.name=="Draw Calls Count")value=UnityStats.drawCalls;
                if(value==0&&counter.name=="SetPass Calls Count")value=UnityStats.setPassCalls;
                counter.values.Add(value);
            }
            yield return null;
        }
        view.cameraRenderCpuMs=Stats(renderTimes); view.editorPlayFrameIntervalMs=Stats(intervals);
        view.counters=counters.Select(c=>{bool zeroRenderCounter=(c.name=="Draw Calls Count"||c.name=="Batches Count"||c.name=="SetPass Calls Count")&&c.values.Count>0&&c.values.All(v=>v==0);return new CounterResult{name=c.name,unit=c.unit,source="ProfilerRecorder, with UnityStats fallback for render submission counters",available=c.available&&c.values.Count>0&&!zeroRenderCounter,unavailableReason=zeroRenderCounter?"Batch Editor supplied only zeros while triangles were rendered; submission count is not verified":null,values=Stats(zeroRenderCounter?new List<double>():c.values)};}).ToList();
    }
    static void Point(Vector3 position,Vector3 destination)
    {
        camera.transform.position=position; Vector3 direction=destination-position;
        camera.transform.rotation=Quaternion.LookRotation(direction,Mathf.Abs(Vector3.Dot(direction.normalized,Vector3.up))>.97f?Vector3.forward:Vector3.up);
    }
    static int PredictedLod(LODGroup group)
    {
        float size=group.size*Mathf.Max(Mathf.Abs(group.transform.lossyScale.x),Mathf.Abs(group.transform.lossyScale.y),Mathf.Abs(group.transform.lossyScale.z));
        float distance=Vector3.Distance(camera.transform.position,group.transform.TransformPoint(group.localReferencePoint));
        float denominator=camera.orthographic?camera.orthographicSize*2f:2f*Mathf.Max(.001f,distance)*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
        float relative=size*QualitySettings.lodBias/Mathf.Max(.001f,denominator);var lods=group.GetLODs();
        for(int i=0;i<lods.Length;i++) if(relative>=lods[i].screenRelativeTransitionHeight) return i;
        return lods.Length;
    }
    static void Capture(string file)
    {
        camera.Render(); var previous=RenderTexture.active; RenderTexture.active=target;
        var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Proof,file),texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=previous;
    }
    static IEnumerator Scenario()
    {
        Guard(); StartRecorders();
        var furniture=UnityEngine.Object.FindObjectsByType<FurnitureAssembly>(FindObjectsInactive.Include);
        Check(furniture.Length==288,"12 apartments contain 288 authored furniture instances");
        var registry=UnityEngine.Object.FindObjectsByType<WorldSaveRegistry>(FindObjectsInactive.Include).Single();
        var before=registry.CaptureState();string beforeJson=JsonUtility.ToJson(before);
        Check(before.objects.Length==288,"all 288 furniture roots are registered in existing world-save registry");
        camera=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Single(c=>c.name=="ViewCamera");
        var motor=UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Exclude).Single();motor.enabled=false;
        var hud=camera.GetComponentInParent<FurnitureDemoHUD>(); if(hud!=null)hud.enabled=false;
        target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
        camera.fieldOfView=60;camera.farClipPlane=200;camera.nearClipPlane=.04f;
        var report=new Report{unity=Application.unityVersion,furnitureInstances=furniture.Length,apartments=12,width=target.width,height=target.height,
            measurementContext="Unity Editor Play. One explicit native Camera.Render per sampled frame at 1280x720. cameraRenderCpuMs measures CPU time around Camera.Render; it does not measure GPU completion or standalone game FPS. editorPlayFrameIntervalMs includes Editor scheduling. Main Thread profiler counter covers the Editor process. No world-streaming or build benchmark."};
        var positions=new[]{new Vector3(1,2.2f,1),new Vector3(4,1.65f,-4),new Vector3(27,17,-18),new Vector3(51,3,-32)};
        var targets=new[]{new Vector3(14,1.2f,-10),new Vector3(5,1,-8),new Vector3(27,0,-18),new Vector3(39,1,-24)};
        var names=new[]{"Entrance diagonal","Apartment eye level","Apartment overview","Far corner"};
        var groups=furniture.Select(f=>f.GetComponent<LODGroup>()).Where(g=>g!=null).ToArray();
        for(int v=0;v<positions.Length;v++)
        {
            Point(positions[v],targets[v]);var counts=new int[4];foreach(var group in groups) counts[Mathf.Clamp(PredictedLod(group),0,3)]++;
            var view=new ViewResult{name=names[v],cameraPosition=positions[v],cameraTarget=targets[v],predictedLodCounts=counts,
                lodCountsMeaning="LOD0/1/2/below cull threshold predicted from authored screen-height thresholds, size, camera and current LOD bias. These counts do not claim actual occlusion visibility."};
            var measure=Measure(view,120,90);while(measure.MoveNext())yield return measure.Current;
            report.views.Add(view);Capture("Stress_View"+(v+1)+".png");
        }
        Check(report.views.Sum(v=>v.cameraRenderCpuMs.samples)==360,"360 measured native camera renders across four views after 120 warmup frames per view");
        Check(beforeJson==JsonUtility.ToJson(registry.CaptureState()),"benchmark leaves every registered furniture transform mechanism device and wear state unchanged");
        var sofaPath=AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs/Sofas"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).First();
        var sofa=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sofaPath));sofa.transform.position=new Vector3(1000,0,1000);
        var lod=sofa.GetComponent<LODGroup>();Check(lod!=null&&lod.lodCount==3,"isolated sofa probe has three genuine LODs");
        int[] triangles=lod.GetLODs().Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();
        Check(triangles[0]>triangles[1]&&triangles[1]>triangles[2],"sofa source triangle counts decrease across three LODs");
        camera.fieldOfView=15;var bounds=new Bounds();bool first=true;foreach(var r in lod.GetLODs()[0].renderers){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
        float worldSize=lod.size*Mathf.Max(Mathf.Abs(lod.transform.lossyScale.x),Mathf.Abs(lod.transform.lossyScale.y),Mathf.Abs(lod.transform.lossyScale.z));
        var desiredScreenHeights=new[]{.80f,.40f,.15f};
        var distances=desiredScreenHeights.Select(h=>worldSize*QualitySettings.lodBias/(2f*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)*h)).ToArray();
        Vector3 reference=lod.transform.TransformPoint(lod.localReferencePoint);
        for(int i=0;i<distances.Length;i++)
        {
            Vector3 position=reference+new Vector3(1,.6f,1.7f).normalized*distances[i];Point(position,reference);
            var view=new ViewResult{name="Isolated sofa native automatic LOD "+i,cameraPosition=position,cameraTarget=bounds.center};
            var measure=Measure(view,35,40);while(measure.MoveNext())yield return measure.Current;
            int predicted=PredictedLod(lod);Check(predicted==i,"native sofa probe distance "+distances[i]+" m matches automatic LOD"+i+" threshold (no ForceLOD)");
            report.lodProbe.Add(new ProbeResult{distance=distances[i],predictedLod=predicted,meshTriangles=triangles,measurement=view});
            Capture("LOD_Probe"+i+"_"+distances[i]+"m.png");
        }
        var triangleResults=report.lodProbe.Select(p=>p.measurement.counters.FirstOrDefault(c=>c.name=="Triangles Count"&&c.available)).ToArray();
        if(triangleResults.All(c=>c!=null)) Check(triangleResults[0].values.mean>triangleResults[1].values.mean&&triangleResults[1].values.mean>triangleResults[2].values.mean,
            "native rendered triangle profiler counter decreases with automatic sofa LOD transitions");
        UnityEngine.Object.DestroyImmediate(sofa);
        CreateWearProof(sofaPath);
        report.unavailableCounters=report.views.SelectMany(v=>v.counters).Where(c=>!c.available).Select(c=>c.name).Distinct().ToArray();report.checks=checks;report.passed=true;
        File.WriteAllText(Path.Combine(Proof,"unity_stress_checks.json"),JsonUtility.ToJson(report,true));
        camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
    }
    static void CreateWearProof(string sofaPath)
    {
        var clones=new List<GameObject>();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(sofaPath);
        var firstMeshes=prefab.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh).ToArray();
        var firstMaterials=prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).ToArray();
        for(int i=0;i<20;i++)
        {
            var go=UnityEngine.Object.Instantiate(prefab);clones.Add(go);go.transform.position=new Vector3(1000+(i%5)*2.7f,0,1000-(i/5)*2.0f);go.GetComponent<LODGroup>().ForceLOD(0);
            float dirt=((i*47)%101)/100f,fade=((i*31+13)%101)/100f,damage=((i*19+7)%101)/100f;
            go.GetComponent<FurnitureWear>().SetWear(101+i*7919,dirt,fade,damage,Color.Lerp(new Color(.38f,.29f,.22f),new Color(.74f,.66f,.48f),(i%7)/6f));
            Check(firstMeshes.SequenceEqual(go.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh))
                &&firstMaterials.SequenceEqual(go.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials)),
                "wear example "+(i+1)+" reuses exact source meshes and materials");
        }
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Wear examples ground";ground.transform.position=new Vector3(1005.4f,-.025f,997);ground.transform.localScale=new Vector3(18,.05f,12);
        ground.GetComponent<Renderer>().sharedMaterial=GameObject.Find("Пол").GetComponent<Renderer>().sharedMaterial;
        camera.orthographic=true;camera.orthographicSize=4.35f;Point(new Vector3(1005.4f,12,1011),new Vector3(1005.4f,.25f,997));
        var previous=target;camera.targetTexture=null;target=new RenderTexture(2048,1280,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
        Capture("Sofa_20_ProceduralWear_LOD0.png");camera.targetTexture=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);target=previous;
        foreach(var clone in clones)UnityEngine.Object.DestroyImmediate(clone);UnityEngine.Object.DestroyImmediate(ground);
    }
}
