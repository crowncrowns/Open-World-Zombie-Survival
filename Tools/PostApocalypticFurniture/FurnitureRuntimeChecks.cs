// Verification-only: copy into the isolated project's Assets/Editor, never into the delivered pack.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ZombieSurvival.Furniture;
using ZombieSurvival.Interaction;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;

[InitializeOnLoad]
public static class FurnitureRuntimeChecks
{
    const string Pending = "FurnitureRuntimeChecks.pending";
    const string ColdPending = "FurnitureRuntimeChecks.cold";
    const string Root = "Assets/PostApocalypticFurniture";
    static readonly List<string> checks = new List<string>();
    static IEnumerator routine;
    static int lastFrame;
    static double began;
    static ColdFixture coldFixture;
    static string Proof => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../Verification/PostApocalypticFurniture"));
    [Serializable] sealed class Report { public bool passed; public string unity; public int prefabCount; public List<string> checks; }
    [Serializable] sealed class ColdFixture {
        public int marker,writerProcessId; public long writerProcessStartUtcTicks;
        public string furniturePath,radioPath,motionId,deviceId,scope; public string[] ids;
    }
    [Serializable] sealed class ColdReport {
        public bool passed; public string unity,furniturePrefab,radioPrefab,snapshotFile;
        public int writerProcessId,readerProcessId; public long writerProcessStartUtcTicks,readerProcessStartUtcTicks;
        public List<string> checks;
    }
    static FurnitureRuntimeChecks() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        Guard(); Directory.CreateDirectory(Proof); SessionState.SetBool(Pending, true); SessionState.SetBool(ColdPending,false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.isPlaying = true;
    }
    public static void RunCold()
    {
        Guard(); Directory.CreateDirectory(Proof);
        if (!File.Exists(Path.Combine(Proof,"cold_saved_world.json")) || !File.Exists(Path.Combine(Proof,"cold_fixture.json")))
            throw new InvalidOperationException("Run the normal furniture runtime checks in a previous Unity process first.");
        SessionState.SetBool(Pending,true);SessionState.SetBool(ColdPending,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;
    }
    static void Guard()
    {
        string path = Application.dataPath.Replace('\\', '/');
        if (!path.Contains("/work/PostApocalypticFurniture/")) throw new InvalidOperationException("Furniture checks require the isolated work/PostApocalypticFurniture project.");
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        checks.Clear(); routine = SessionState.GetBool(ColdPending,false)?ColdScenario():Scenario(); began = EditorApplication.timeSinceStartup; lastFrame = -1; EditorApplication.update += Tick;
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup - began > 480) throw new TimeoutException("Furniture runtime checks exceeded 8 minutes.");
            if (routine.MoveNext()) return;
            bool cold=SessionState.GetBool(ColdPending,false);
            if(cold) {
                using(var process=System.Diagnostics.Process.GetCurrentProcess())
                    File.WriteAllText(Path.Combine(Proof,"unity_cold_restore.json"),JsonUtility.ToJson(new ColdReport {
                        passed=true,unity=Application.unityVersion,furniturePrefab=coldFixture.furniturePath,radioPrefab=coldFixture.radioPath,
                        snapshotFile="cold_saved_world.json",writerProcessId=coldFixture.writerProcessId,readerProcessId=process.Id,
                        writerProcessStartUtcTicks=coldFixture.writerProcessStartUtcTicks,readerProcessStartUtcTicks=process.StartTime.ToUniversalTime().Ticks,checks=checks },true));
            } else File.WriteAllText(Path.Combine(Proof, "unity_runtime_checks.json"), JsonUtility.ToJson(new Report
                { passed = true, unity = Application.unityVersion, prefabCount = Paths().Length, checks = checks }, true));
            SessionState.SetBool(Pending, false); EditorApplication.update -= Tick;
            Debug.Log((cold?"FURNITURE_COLD_STATE_RESTORE_PASS ":"FURNITURE_RUNTIME_PASS ") + checks.Count); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Proof, SessionState.GetBool(ColdPending,false)?"unity_cold_restore_failure.txt":"unity_runtime_failure.txt"), error.ToString());
            Debug.LogException(error); SessionState.SetBool(Pending, false); EditorApplication.update -= Tick; EditorApplication.Exit(1);
        }
    }
    static string[] Paths() => Directory.GetFiles(Root + "/Prefabs", "*.prefab", SearchOption.AllDirectories).OrderBy(p => p).ToArray();
    static void Check(bool value, string label)
    { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < .0002f;
    static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .0002f;
    static void Set(UnityEngine.Object target, string field, object value)
    {
        var so = new SerializedObject(target); var property = so.FindProperty(field);
        if (property == null) throw new MissingFieldException(target.GetType().Name, field);
        if (value is bool flag) property.boolValue = flag;
        else if (value is string text) property.stringValue = text;
        else if (value is UnityEngine.Object obj) property.objectReferenceValue = obj;
        else throw new ArgumentException(field);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    static IEnumerable Wait(float seconds)
    { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    static IEnumerator Scenario()
    {
        Guard();
        var paths = Paths(); Check(paths.Length == 200, "200 separate real prefabs");
        var floorObject=new GameObject("External floor · top Y=0");floorObject.transform.position=new Vector3(0,-.05f,0);
        var floor=floorObject.AddComponent<BoxCollider>();floor.size=new Vector3(20,.1f,20);Physics.SyncTransforms();
        int radios = 0, mechanisms = 0;
        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            Check(prefab != null, "Prefab loads " + path);
            var go = UnityEngine.Object.Instantiate(prefab); var assembly = go.GetComponent<FurnitureAssembly>();
            var closedRenderers=go.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.EndsWith("_LOD0")&&r.gameObject.activeInHierarchy).ToArray();
            Check(closedRenderers.Length>0,go.name+" closed LOD0 geometry available for floor placement");
            float closedMinimumY=closedRenderers.Min(r=>r.bounds.min.y);go.transform.position-=Vector3.up*closedMinimumY;Physics.SyncTransforms();
            Check(true,go.name+" placed with closed visible LOD0 minimum at actual floor Y=0; root offset="+(-closedMinimumY).ToString("G6"));
            Check(assembly != null && assembly.ValidateState(assembly.CaptureState()), go.name + " exact valid initial state");
            var initialState = assembly.CaptureState();
            var originalRootPosition = go.transform.position; var originalRootRotation = go.transform.rotation;
            foreach (var motion in go.GetComponentsInChildren<FurnitureMechanism>(true))
            {
                mechanisms++;
                assembly.RestoreStateSilently(initialState);
                AssertFloorClear(motion,floor,"closed initial pose");
                PrepareConditions(motion, true, new HashSet<FurnitureMechanism>());
                var pivot = motion.Pivot; Vector3 initialPosition = pivot.localPosition; Quaternion initialRotation = pivot.localRotation;
                Check(motion.SetOpen(true, true), go.name + "/" + motion.StableId + " prerequisites allow movement");
                Check(!Near(pivot.localPosition, initialPosition) || Quaternion.Angle(pivot.localRotation, initialRotation) > .001f,
                    go.name + "/" + motion.StableId + " moves own pivot");
                Check(Near(go.transform.position, originalRootPosition) && Quaternion.Angle(go.transform.rotation, originalRootRotation) < .001f,
                    go.name + "/" + motion.StableId + " root stays fixed");
                Check(motion.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled&&c.gameObject.activeInHierarchy),
                    go.name + "/" + motion.StableId + " moving collision follows pivot hierarchy");
                PrepareConditions(motion, false, new HashSet<FurnitureMechanism>());
                motion.SetOpen(false); motion.Step(.1f);
                var closingState=motion.CaptureState();
                if(!(motion.OpenFraction>0f&&motion.OpenFraction<1f&&!motion.IsBlocked))
                    throw new InvalidOperationException(go.name+"/"+motion.StableId+" smooth partial closing failed; state="+JsonUtility.ToJson(closingState)+" obstruction="+motion.LastObstruction);
                Check(true,go.name+"/"+motion.StableId+" smooth partial closing without external obstruction");
                float intermediate = motion.OpenFraction;
                PrepareConditions(motion, true, new HashSet<FurnitureMechanism>());
                bool reversalAccepted=motion.SetOpen(true);var reversalCommand=motion.CaptureState();motion.Step(.1f);var reversalResult=motion.CaptureState();
                if(!(reversalAccepted&&motion.TargetOpen&&Near(reversalCommand.velocity,0f)&&motion.OpenFraction>=intermediate-.0001f&&!motion.IsBlocked))
                    throw new InvalidOperationException(go.name+"/"+motion.StableId+" reverse during motion failed; closing="+JsonUtility.ToJson(closingState)
                        +" command="+JsonUtility.ToJson(reversalCommand)+" result="+JsonUtility.ToJson(reversalResult)+" obstruction="+motion.LastObstruction);
                Check(true,go.name+"/"+motion.StableId+" accepted reverse stops previous direction and accelerates toward new endpoint");
                var saved = motion.CaptureState();
                PrepareConditions(motion, false, new HashSet<FurnitureMechanism>());
                motion.SetOpen(false, true); motion.RestoreStateSilently(saved);
                Check(Near(motion.OpenFraction, saved.position) && motion.TargetOpen == saved.targetOpen,
                    go.name + "/" + motion.StableId + " partial pose plus destination restored");
                go.SetActive(false); go.SetActive(true);
                Check(Near(motion.OpenFraction, saved.position), go.name + "/" + motion.StableId + " disable-enable keeps state");
                PrepareConditions(motion, false, new HashSet<FurnitureMechanism>());
                motion.SetOpen(false, true);
                assembly.RestoreStateSilently(initialState);
                PrepareConditions(motion,true,new HashSet<FurnitureMechanism>());
                bool fullOpenAccepted=motion.SetOpen(true);
                for(int step=0;step<80;step++){motion.Step(.025f);AssertFloorClear(motion,floor,"smooth opening step "+step);}
                if(!(fullOpenAccepted&&motion.OpenFraction>.999f&&!motion.IsBlocked))
                    throw new InvalidOperationException(go.name+"/"+motion.StableId+" complete smooth open route failed; state="+JsonUtility.ToJson(motion.CaptureState())+" obstruction="+motion.LastObstruction);
                Check(true,go.name+"/"+motion.StableId+" complete 80-step smooth opening reaches endpoint without floor penetration");
                PrepareConditions(motion,false,new HashSet<FurnitureMechanism>());
                bool fullCloseAccepted=motion.SetOpen(false);
                for(int step=0;step<80;step++){motion.Step(.025f);AssertFloorClear(motion,floor,"smooth closing step "+step);}
                if(!(fullCloseAccepted&&motion.OpenFraction<.001f&&!motion.IsBlocked))
                    throw new InvalidOperationException(go.name+"/"+motion.StableId+" complete smooth close route failed; state="+JsonUtility.ToJson(motion.CaptureState())+" obstruction="+motion.LastObstruction);
                Check(true,go.name+"/"+motion.StableId+" complete 80-step smooth closing reaches endpoint without floor penetration");
            }
            assembly.RestoreStateSilently(initialState);
            var definition=JsonUtility.FromJson<ZombieSurvival.Furniture.Editor.FurniturePackBuilder.Catalog>(File.ReadAllText(Root+"/Documentation/catalog.json")).assets.Single(r=>r.slug==assembly.AssetId);
            foreach(var control in go.GetComponentsInChildren<FurnitureControl>(true)) {
                var authored=definition.parts.Single(p=>p.name==control.StableId);var settings=new SerializedObject(control);
                var wantedAxis=new Vector3(authored.axis[0],authored.axis[1],authored.axis[2]).normalized;
                Check(Vector3.Dot(settings.FindProperty("controlAxis").vector3Value,wantedAxis)>.9999f&&settings.FindProperty("rotary").boolValue==(Mathf.Abs(authored.amount)>1f),go.name+"/"+control.StableId+" production control uses authored axis and button/rotary geometry");
                if(control.StableId.StartsWith("BandKey",StringComparison.Ordinal)) {
                    int wanted=int.Parse(control.StableId.Substring(7));Check(control.Interact(null)&&control.Device.Channel==wanted,go.name+"/"+control.StableId+" actual F-action control selects its own preset station");
                }
            }
            foreach (var sound in go.GetComponentsInChildren<AudioSource>(true)) sound.Stop();
            foreach (var device in go.GetComponentsInChildren<FurnitureDevice>(true))
            {
                var beforeEmission = NonDeviceEmission(go);
                if (device.Kind == FurnitureDeviceKind.Radio)
                {
                    radios++; Check(device.StationCount >= 3, go.name + " multiple radio channels");
                    Check(device.Source != null && device.Source.spatialBlend == 1f && device.Source.maxDistance <= 20f,
                        go.name + " bounded 3D radio source");
                }
                device.SetPower(true); device.CycleVolume(); device.CycleBrightness(); device.CycleChannel(); device.Tune();
                Check(beforeEmission.SequenceEqual(NonDeviceEmission(go)), go.name + " device emission leaves wood fabric and other material slots untouched");
                var saved = device.CaptureState(); device.SetPower(false); device.RestoreStateSilently(saved);
                Check(device.IsOn && device.Channel == saved.channel && Near(device.Volume, saved.volume)
                    && Near(device.Brightness, saved.brightness) && Near(device.Tuning, saved.tuning), go.name + " device exact state restore");
                Check(go.GetComponentsInChildren<AudioSource>(true).All(s => !s.isPlaying), go.name + " silent restore has no audio side effects");
                device.RefreshPresentation(true);
                go.SetActive(false); go.SetActive(true);
                Check(device.IsOn && Near(device.Volume, saved.volume), go.name + " device disable-enable retains state");
                device.SetPower(false);
            }
            var wear = go.GetComponent<FurnitureWear>();
            Check(wear != null, go.name + " procedural instance wear");
            var materials = go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
            wear.SetWear(723, .8f, .6f, .7f, new Color(.5f, .4f, .3f));
            var wearState = wear.CaptureState();
            wear.SetWear(4, 0, 0, 0, Color.white); wear.RestoreStateSilently(wearState);
            Check(wear.Seed == 723 && Near(wear.Dirt, .8f) && Near(wear.Damage, .7f), go.name + " wear seed and degrees restore");
            Check(materials.SequenceEqual(go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)),
                go.name + " wear creates no material instances");
            var state = assembly.CaptureState();
            Check(assembly.ValidateState(state), go.name + " modified snapshot validates");
            state.assetId += "-corrupt";
            Check(!assembly.ValidateState(state), go.name + " foreign asset state rejected");
            var adapter = go.GetComponent<FurnitureWorldSaveAdapter>();
            Check(adapter != null && go.GetComponent<WorldSaveObject>() != null && go.GetComponent<SaveIdentity>() != null,
                go.name + " existing atomic world-save adapter present");
            UnityEngine.Object.DestroyImmediate(go);
            yield return null;
        }
        UnityEngine.Object.DestroyImmediate(floorObject);
        Check(radios == 10 && mechanisms > 0, "10 radio models and operational furniture mechanisms");
        VerifyInterlocks();
        VerifyChairBed();
        VerifyCapsuleObstructions();
        VerifySaturatedOwnCollisionQuery();
        VerifyCompartmentLighting(paths);
        var controls = VerifyControlAxes(); while (controls.MoveNext()) yield return controls.Current;
        var world = VerifyWorldTransaction(paths); while (world.MoveNext()) yield return world.Current;
        var input = VerifyFInput(); while (input.MoveNext()) yield return input.Current;
    }
    static void AssertFloorClear(FurnitureMechanism motion,BoxCollider floor,string phase)
    {
        Physics.SyncTransforms();
        foreach(var collider in motion.GetComponentsInChildren<Collider>(true))
        {
            if(!collider.enabled||collider.isTrigger||!collider.gameObject.activeInHierarchy)continue;
            if(!Physics.ComputePenetration(collider,collider.transform.position,collider.transform.rotation,
                floor,floor.transform.position,floor.transform.rotation,out Vector3 direction,out float depth))continue;
            // Ten micrometres only absorbs float contact noise at an authored exact Y=0 touch;
            // millimetre-scale proxy/route penetration remains an unconditional failure.
            if(depth<=.00001f)continue;
            throw new InvalidOperationException(motion.GetComponentInParent<FurnitureAssembly>().name+"/"+motion.StableId
                +" actual floor penetration during "+phase+": depth="+depth+" direction="+direction+" collider="+collider.name
                +" type="+collider.GetType().Name+" bounds="+collider.bounds+" rootPosition="+motion.transform.root.position
                +" state="+JsonUtility.ToJson(motion.CaptureState())+" obstruction="+motion.LastObstruction);
        }
    }
    static void VerifySaturatedOwnCollisionQuery()
    {
        var root=new GameObject("Temporary saturated own-collider proof");root.transform.position=new Vector3(300,1,300);
        root.AddComponent<FurnitureAssembly>().Configure("saturation-proof","Проверка коллайдеров","Proof");
        var pivot=new GameObject("Moving part");pivot.transform.SetParent(root.transform,false);pivot.AddComponent<BoxCollider>().size=Vector3.one*.2f;
        for(int i=0;i<70;i++){var own=new GameObject("Own compound "+i);own.transform.SetParent(root.transform,false);own.AddComponent<BoxCollider>().size=Vector3.one*3f;}
        var motion=pivot.AddComponent<FurnitureMechanism>();motion.Configure("saturated",pivot.transform,Vector3.forward,1f,false,"Ящик");Physics.SyncTransforms();
        motion.SetOpen(true);for(int step=0;step<80;step++)motion.Step(.025f);
        Check(!motion.IsBlocked&&motion.OpenFraction>.999f,"saturated query containing 70 own compound colliders does not invent an external obstruction");
        motion.SetOpen(false,true);var obstacle=new GameObject("External obstacle after saturation");obstacle.transform.position=root.transform.position+Vector3.forward*.45f;
        obstacle.AddComponent<BoxCollider>().size=new Vector3(.4f,.4f,.1f);Physics.SyncTransforms();motion.SetOpen(true);
        for(int step=0;step<80;step++)motion.Step(.025f);
        Check(motion.IsBlocked&&motion.OpenFraction>.05f&&motion.OpenFraction<.4f,
            "complete-query fallback still detects external collider among saturated own contacts");
        UnityEngine.Object.DestroyImmediate(obstacle);UnityEngine.Object.DestroyImmediate(root);
    }
    static IEnumerator VerifyControlAxes()
    {
        var root=new GameObject("Temporary authored control axes");root.transform.position=new Vector3(300,1,300);
        var device=root.AddComponent<FurnitureDevice>();device.Configure(FurnitureDeviceKind.General,null,null,Array.Empty<Light>(),Array.Empty<Renderer>());
        var button=new GameObject("Top button");button.transform.SetParent(root.transform,false);button.transform.localPosition=new Vector3(.1f,.2f,.3f);
        var push=button.AddComponent<FurnitureControl>();push.Configure("top-power",device,FurnitureControlKind.Power,"Связь");push.ConfigureGeometry(Vector3.up,false,.004f);
        Vector3 rest=button.transform.localPosition;Check(push.Interact(null),"top-mounted F control accepts existing interaction contract");
        Vector3 displacement=button.transform.localPosition-rest;
        Check(displacement.y<-.001f&&Mathf.Abs(displacement.x)<.00001f&&Mathf.Abs(displacement.z)<.00001f,"top button presses along authored Y instead of a fixed Z axis");
        var knob=new GameObject("Top rotary volume");knob.transform.SetParent(root.transform,false);
        var volume=knob.AddComponent<FurnitureControl>();volume.Configure("top-volume",device,FurnitureControlKind.Volume,"Громкость");volume.ConfigureGeometry(Vector3.up,true,28f);
        Quaternion before=knob.transform.localRotation;Check(volume.Interact(null),"top rotary volume accepts F interaction");
        Check(Quaternion.Angle(before,knob.transform.localRotation)>20f&&Vector3.Angle(knob.transform.up,root.transform.up)<.01f,"top rotary control turns about authored Y axis");
        var brightness=new GameObject("Rotary brightness");brightness.transform.SetParent(root.transform,false);
        var dimmer=brightness.AddComponent<FurnitureControl>();dimmer.Configure("dimmer",device,FurnitureControlKind.Brightness,"Яркость");dimmer.ConfigureGeometry(Vector3.forward,true,28f);
        before=brightness.transform.localRotation;Check(dimmer.Interact(null),"rotary dimmer accepts F interaction");
        Check(Quaternion.Angle(before,brightness.transform.localRotation)>20f,"brightness regulator visibly rotates with device setting");
        UnityEngine.Object.DestroyImmediate(root);
        yield return null;
    }
    static void VerifyCompartmentLighting(string[] paths)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(paths.Single(p=>p.Contains("F134_Refrigerators")));
        var go=UnityEngine.Object.Instantiate(prefab);var assembly=go.GetComponent<FurnitureAssembly>();
        var doors=go.GetComponentsInChildren<FurnitureMechanism>(true);var lights=go.GetComponentsInChildren<Light>(true);
        Check(doors.Length==2&&lights.Length>0,"two-door refrigerator has real internal lamp");
        doors[0].SetOpen(true,true);doors[1].SetOpen(true,true);assembly.RefreshCompartmentLighting();
        Check(lights.All(l=>l.enabled),"opening refrigerator doors enables internal lighting");
        doors[0].SetOpen(false,true);assembly.RefreshCompartmentLighting();
        Check(lights.All(l=>l.enabled),"closing one refrigerator door leaves lamp on while another remains open");
        var saved=assembly.CaptureState();doors[1].SetOpen(false,true);assembly.RefreshCompartmentLighting();
        Check(lights.All(l=>!l.enabled),"closing every refrigerator door disables internal lighting");
        assembly.RestoreStateSilently(saved);assembly.PublishRestoredState();
        Check(lights.All(l=>l.enabled),"world-state publication derives refrigerator light from restored door states");
        UnityEngine.Object.DestroyImmediate(go);
    }
    static void VerifyCapsuleObstructions()
    {
        for (int direction=0;direction<3;direction++)
        {
            var root=new GameObject("Temporary capsule motion proof");root.transform.position=new Vector3(200,1,200);
            root.AddComponent<FurnitureAssembly>().Configure("capsule-proof-"+direction,"Капсула","Proof");
            var pivot=new GameObject("Pivot");pivot.transform.SetParent(root.transform,false);
            var shape=new GameObject("Moving capsule");shape.transform.SetParent(pivot.transform,false);
            shape.transform.localRotation=Quaternion.Euler(23,37,31);shape.transform.localScale=new Vector3(.65f,1.7f,1.2f);
            var capsule=shape.AddComponent<CapsuleCollider>();capsule.radius=.1f;capsule.height=.5f;capsule.direction=direction;capsule.center=new Vector3(.03f,.02f,-.01f);
            var motion=pivot.AddComponent<FurnitureMechanism>();motion.Configure("capsule",pivot.transform,Vector3.forward,1f,false,"Подвижная капсула");
            Physics.SyncTransforms();var bounds=capsule.bounds;
            var obstacle=new GameObject("External obstruction");obstacle.transform.position=new Vector3(bounds.center.x,bounds.center.y,bounds.max.z+.25f);
            var box=obstacle.AddComponent<BoxCollider>();box.size=new Vector3(bounds.size.x+1f,bounds.size.y+1f,.12f);Physics.SyncTransforms();
            motion.SetOpen(true);motion.Step(.4f);
            Check(motion.IsBlocked&&motion.OpenFraction>.04f&&motion.OpenFraction<.36f,
                "scaled rotated capsule direction "+direction+" stops before external collider");
            bool penetrates=Physics.ComputePenetration(capsule,capsule.transform.position,capsule.transform.rotation,
                box,box.transform.position,box.transform.rotation,out Vector3 penetrationDirection,out float penetrationDepth);
            if(penetrates)
                throw new InvalidOperationException("capsule direction "+direction+" actual native collider still penetrates blocker: depth="+penetrationDepth
                    +" separationDirection="+penetrationDirection+" scale="+capsule.transform.lossyScale+" localHeight="+capsule.height+" localRadius="+capsule.radius
                    +" localCentre="+capsule.center+" rotation="+capsule.transform.rotation+" capsuleBounds="+capsule.bounds+" blockerBounds="+box.bounds
                    +" boundsZGap="+(box.bounds.min.z-capsule.bounds.max.z)+" state="+JsonUtility.ToJson(motion.CaptureState())+" obstruction="+motion.LastObstruction);
            Check(true,"capsule direction "+direction+" retains native ComputePenetration-safe collider pose with zero allowed overlap");
            motion.SetOpen(false);for(int step=0;step<12;step++)motion.Step(.1f);
            Check(!motion.IsBlocked&&motion.OpenFraction<.001f,"blocked capsule direction "+direction+" reverses and returns closed");
            obstacle.SetActive(false);motion.SetOpen(true);for(int step=0;step<12;step++)motion.Step(.1f);
            Check(!motion.IsBlocked&&motion.OpenFraction>.999f,"capsule direction "+direction+" reaches endpoint after obstruction removal");
            UnityEngine.Object.DestroyImmediate(obstacle);UnityEngine.Object.DestroyImmediate(root);
        }
    }
    static Color[] NonDeviceEmission(GameObject root)
    {
        var values = new List<Color>(); var block = new MaterialPropertyBlock();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || materials[i].name == "Emission" || materials[i].name == "Screen") continue;
                renderer.GetPropertyBlock(block,i); values.Add(block.GetColor("_EmissionColor"));
            }
        }
        return values.ToArray();
    }
    static void PrepareConditions(FurnitureMechanism motion, bool opening, HashSet<FurnitureMechanism> visiting)
    {
        if (!visiting.Add(motion)) throw new InvalidOperationException("Cyclic furniture action prerequisites at " + motion.StableId);
        foreach (var rule in motion.Interlocks)
        {
            if (rule == null || !rule.Applies(opening)) continue;
            if (rule.other == null) throw new InvalidOperationException("Missing interlock reference");
            bool endpoint = rule.minimum > .5f ? true : rule.maximum < .5f ? false : rule.other.TargetOpen;
            PrepareConditions(rule.other, endpoint, visiting);
            if (!rule.other.SetOpen(endpoint, true)) throw new InvalidOperationException("Cannot prepare dependency " + rule.other.StableId);
        }
        visiting.Remove(motion);
    }
    static void VerifyInterlocks()
    {
        var root = new GameObject("Temporary interlock proof"); var assembly = root.AddComponent<FurnitureAssembly>(); assembly.Configure("interlock-proof","Стол","Proof");
        var gateNode = new GameObject("Gate"); gateNode.transform.SetParent(root.transform);
        var leafNode = new GameObject("Leaf"); leafNode.transform.SetParent(root.transform);
        var gate = gateNode.AddComponent<FurnitureMechanism>(); gate.Configure("gate",gateNode.transform,Vector3.up,90f,true,"Опора");
        var leaf = leafNode.AddComponent<FurnitureMechanism>(); leaf.Configure("leaf",leafNode.transform,Vector3.forward,90f,true,"Крыло");
        leaf.ConfigureInterlocks(new[]{new FurnitureInterlock{other=gate,minimum=.95f,maximum=1f,message="Сначала сложите опору"}});
        gate.ConfigureInterlocks(new[]{new FurnitureInterlock{other=leaf,whenOpening=false,whenClosing=true,minimum=0f,maximum=.05f,message="Сначала поднимите крыло"}});
        string before = JsonUtility.ToJson(assembly.CaptureState());
        Check(!leaf.SetOpen(true) && JsonUtility.ToJson(assembly.CaptureState()) == before,
            "interlock refusal changes no target fraction velocity or saved flag");
        Check(leaf.GetPrompt(null).Contains("опору"), "interlock exposes Russian ordering feedback");
        Check(gate.SetOpen(true,true) && leaf.SetOpen(true), "ordered gate then leaf accepted");
        Check(!gate.SetOpen(false), "interlock checks dependency destination before it starts moving");
        leaf.Step(.1f); var valid = assembly.CaptureState();
        Check(assembly.ValidateState(valid), "legal moving interlock state accepted");
        var invalid = JsonUtility.FromJson<FurnitureState>(JsonUtility.ToJson(valid));
        invalid.mechanisms.Single(m=>m.id=="gate").targetOpen = false;
        Check(!assembly.ValidateState(invalid) && gate.TargetOpen,
            "future conflicting mechanism destinations refused without touching live state");
        leaf.SetOpen(false,true); Check(gate.SetOpen(false,true), "leaf reset then support reset accepted");
        assembly.RestoreStateSilently(valid);
        Check(gate.TargetOpen && leaf.TargetOpen && leaf.OpenFraction > 0f && assembly.ValidateState(assembly.CaptureState()),
            "valid partial interlocked mechanism restores with its future destination");
        UnityEngine.Object.DestroyImmediate(root);
    }
    static void VerifyChairBed()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Armchairs/F019_Armchairs_09.prefab");
        var go=UnityEngine.Object.Instantiate(prefab);go.transform.position=new Vector3(400,0,400);
        var assembly=go.GetComponent<FurnitureAssembly>();var initial=assembly.CaptureState();
        var floorObject=new GameObject("Chair-bed route floor");floorObject.transform.position=new Vector3(400,-.05f,400);var floor=floorObject.AddComponent<BoxCollider>();floor.size=new Vector3(5,.1f,5);
        var order=new[]{"BedExtension","BedFootExtension","BedRaise","BedFootRaise","BedFootLegs"};
        var motions=order.Select(id=>go.GetComponentsInChildren<FurnitureMechanism>(true).Single(m=>m.StableId==id)).ToArray();
        Check(!motions[4].SetOpen(true)&&!motions[2].SetOpen(true),"chair-bed blocks support or lift before nested carriers are extended");
        for(int index=0;index<motions.Length;index++) {
            var motion=motions[index];Check(motion.Interact(null),"chair-bed F action accepts ordered opening "+motion.StableId);
            for(int step=0;step<100;step++){motion.Step(.025f);AssertFloorClear(motion,floor,"chair-bed ordered opening");}
            Check(motion.OpenFraction>.999f&&!motion.IsBlocked,"chair-bed ordered full motion reaches endpoint "+motion.StableId);
        }
        Check(!motions[0].SetOpen(false),"chair-bed refuses to retract main carrier with raised mattress/support");
        Check(!go.GetComponentsInChildren<FurnitureMechanism>(true).Single(m=>m.StableId=="LooseSeat").SetOpen(true),"chair-bed blocks loose seat from sliding into the unfolded middle cushion");
        var expanded=assembly.CaptureState();Check(assembly.ValidateState(expanded),"complete expanded chair-bed snapshot validates");
        var sleeping=go.GetComponentsInChildren<MeshRenderer>(true).Where(r=>new[]{"Body_LOD0","LooseSeat_LOD0","BedRaise_LOD0","BedFootRaise_LOD0"}.Contains(r.name)).ToArray();
        var middle=sleeping.Single(r=>r.name=="BedRaise_LOD0").bounds;var foot=sleeping.Single(r=>r.name=="BedFootRaise_LOD0").bounds;
        var seat=sleeping.Single(r=>r.name=="LooseSeat_LOD0").bounds;
        Check(foot.max.z-seat.min.z>1.80f&&Mathf.Abs(middle.max.y-foot.max.y)<.012f&&Mathf.Abs(seat.max.y-middle.max.y)<.025f,"three sleeping sections form adult length with aligned padded surfaces");
        var leg=go.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="BedFootLegs_LOD0");Check(Mathf.Abs(leg.bounds.min.y)<.002f,"unfolded chair-bed support touches the actual floor");
        var positions=motions.Select(m=>m.Pivot.position).ToArray();assembly.RestoreStateSilently(initial);assembly.RestoreStateSilently(JsonUtility.FromJson<FurnitureState>(JsonUtility.ToJson(expanded)));
        Check(motions.Select(m=>m.Pivot.position).Zip(positions,(a,b)=>Near(a,b)).All(v=>v),"nested chair-bed hierarchy restores exact world poses through existing state schema");
        foreach(var motion in motions.Reverse()) {
            Check(motion.Interact(null),"chair-bed F action accepts reverse folding "+motion.StableId);
            for(int step=0;step<100;step++){motion.Step(.025f);AssertFloorClear(motion,floor,"chair-bed ordered closing");}
            Check(motion.OpenFraction<.001f&&!motion.IsBlocked,"chair-bed returns to its closed envelope "+motion.StableId);
        }
        Check(assembly.ValidateState(assembly.CaptureState()),"refolded chair-bed state remains valid");
        UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(floorObject);
    }
    static IEnumerator VerifyWorldTransaction(string[] paths)
    {
        var selected = paths.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/'))).ToArray();
        var furniturePrefab=selected.First(p => p.GetComponent<FurnitureAssembly>().Category == "Wardrobes");
        var radioPrefab=selected.First(p => p.GetComponentInChildren<FurnitureDevice>(true)?.Kind == FurnitureDeviceKind.Radio);
        var furniture = UnityEngine.Object.Instantiate(furniturePrefab);
        var radio = UnityEngine.Object.Instantiate(radioPrefab);
        radio.transform.position = new Vector3(10f, 0f, 0f);
        var roots = new[] { furniture.GetComponent<WorldSaveObject>(), radio.GetComponent<WorldSaveObject>() };
        for (int i = 0; i < roots.Length; i++) Set(roots[i].GetComponent<SaveIdentity>(), "persistentId", "furniture-test-" + i);
        var registry = new GameObject("Temporary furniture registry").AddComponent<WorldSaveRegistry>();
        var so = new SerializedObject(registry); var values = so.FindProperty("sceneObjects"); values.arraySize = roots.Length;
        for (int i = 0; i < roots.Length; i++) values.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
        so.FindProperty("scopeId").stringValue = "furniture-isolated-proof"; so.ApplyModifiedPropertiesWithoutUndo();
        var motion = furniture.GetComponentInChildren<FurnitureMechanism>(true); motion.SetOpen(true, true);
        var device = radio.GetComponentInChildren<FurnitureDevice>(true); device.SetPower(true); device.CycleChannel(); device.CycleVolume();
        var wear = furniture.GetComponent<FurnitureWear>(); wear.SetWear(918, .76f, .41f, .59f, new Color(.48f,.44f,.36f));
        var snapshot = registry.CaptureState();
        string json = JsonUtility.ToJson(snapshot); snapshot = JsonUtility.FromJson<WorldSceneState>(json);
        File.WriteAllText(Path.Combine(Proof,"cold_saved_world.json"),json);
        using(var process=System.Diagnostics.Process.GetCurrentProcess()) {
            var fixture=new ColdFixture {marker=631,furniturePath=AssetDatabase.GetAssetPath(furniturePrefab),radioPath=AssetDatabase.GetAssetPath(radioPrefab),
                motionId=motion.StableId,deviceId=device.StableId,scope=registry.ScopeId,ids=roots.Select(r=>r.Id).ToArray(),
                writerProcessId=process.Id,writerProcessStartUtcTicks=process.StartTime.ToUniversalTime().Ticks};
            File.WriteAllText(Path.Combine(Proof,"cold_fixture.json"),JsonUtility.ToJson(fixture,true));
        }
        Check(true,"real world snapshot and exact fixture prefab paths written for separate-process cold restore");
        motion.SetOpen(false, true); device.SetPower(false); wear.SetWear(0, 0, 0, 0, Color.white);
        using (var plan = registry.PrepareRestore(snapshot)) { plan.CommitSilent(); plan.Publish(); }
        Check(motion.TargetOpen && Near(motion.OpenFraction, 1f) && device.IsOn && device.Channel == 1 && wear.Seed == 918 && Near(wear.Dirt,.76f),
            "real WorldSaveRegistry JSON round trip restores mechanism radio and wear atomically");
        using (var plan = registry.PrepareRestore(snapshot)) { plan.CommitSilent(); plan.Publish(); }
        Check(UnityEngine.Object.FindObjectsByType<WorldSaveObject>(FindObjectsInactive.Include).Count(r => !r.gameObject.scene.name.StartsWith("Save staging ")) == 2,
            "repeated world load retains two authored roots without duplicate furniture");
        var corrupt = JsonUtility.FromJson<WorldSceneState>(json); corrupt.objects[0].blocks[0].json = "{}";
        float before = motion.OpenFraction; bool rejected = false;
        try { using (var plan = registry.PrepareRestore(corrupt)) { } } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && Near(motion.OpenFraction, before) && device.IsOn && wear.Seed == 918,
            "invalid furniture block refused before live changes");
        UnityEngine.Object.DestroyImmediate(furniture); UnityEngine.Object.DestroyImmediate(radio); UnityEngine.Object.DestroyImmediate(registry.gameObject);
        yield return null;
    }
    static IEnumerator ColdScenario()
    {
        Guard();
        coldFixture=JsonUtility.FromJson<ColdFixture>(File.ReadAllText(Path.Combine(Proof,"cold_fixture.json")));
        Check(coldFixture!=null&&coldFixture.marker==631&&coldFixture.ids!=null&&coldFixture.ids.Length==2,
            "cold fixture metadata from writer process is complete");
        using(var process=System.Diagnostics.Process.GetCurrentProcess())
            Check(process.Id!=coldFixture.writerProcessId||process.StartTime.ToUniversalTime().Ticks!=coldFixture.writerProcessStartUtcTicks,
                "reader is a different Unity process from snapshot writer");
        var furniturePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(coldFixture.furniturePath);
        var radioPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(coldFixture.radioPath);
        Check(furniturePrefab!=null&&radioPrefab!=null,"same saved fixture prefab types load after cold process start");
        var furniture=UnityEngine.Object.Instantiate(furniturePrefab);var radio=UnityEngine.Object.Instantiate(radioPrefab);radio.transform.position=new Vector3(-12,0,0);
        var roots=new[]{furniture.GetComponent<WorldSaveObject>(),radio.GetComponent<WorldSaveObject>()};
        for(int i=0;i<roots.Length;i++)Set(roots[i].GetComponent<SaveIdentity>(),"persistentId",coldFixture.ids[i]);
        var registry=new GameObject("Cold furniture registry").AddComponent<WorldSaveRegistry>();
        var so=new SerializedObject(registry);so.FindProperty("scopeId").stringValue=coldFixture.scope;var list=so.FindProperty("sceneObjects");list.arraySize=2;
        for(int i=0;i<roots.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=roots[i];so.ApplyModifiedPropertiesWithoutUndo();
        var motion=furniture.GetComponentsInChildren<FurnitureMechanism>(true).Single(m=>m.StableId==coldFixture.motionId);
        var device=radio.GetComponentsInChildren<FurnitureDevice>(true).Single(d=>d.StableId==coldFixture.deviceId);
        var wear=furniture.GetComponent<FurnitureWear>();
        Check(furniture.GetComponentsInChildren<FurnitureMechanism>(true).All(m=>!m.TargetOpen&&Near(m.OpenFraction,0f))
            &&radio.GetComponentsInChildren<FurnitureDevice>(true).All(d=>!d.IsOn&&d.Channel==0),
            "fresh cold-instantiated prefabs start closed and powered off");
        Vector3 closedPosition=motion.Pivot.localPosition;Quaternion closedRotation=motion.Pivot.localRotation;
        string snapshotJson=File.ReadAllText(Path.Combine(Proof,"cold_saved_world.json"));var snapshot=JsonUtility.FromJson<WorldSceneState>(snapshotJson);
        using(var plan=registry.PrepareRestore(snapshot)){plan.CommitSilent();plan.Publish();}
        Check(motion.TargetOpen&&Near(motion.OpenFraction,1f),"disk world snapshot restores opened furniture in new Unity process");
        Check(!Near(motion.Pivot.localPosition,closedPosition)||Quaternion.Angle(motion.Pivot.localRotation,closedRotation)>.01f,
            "cold restore applies actual moving transform rather than only logical open flag");
        Check(device.IsOn&&device.Channel==1&&Near(device.Volume,.7f),"cold restore recovers powered radio channel and volume");
        Check(wear.Seed==918&&Near(wear.Dirt,.76f)&&Near(wear.Fade,.41f)&&Near(wear.Damage,.59f),
            "cold restore recovers procedural wear seed and exact dirt fade damage degrees");
        Check(Near(radio.transform.position,new Vector3(10,0,0))&&roots.Select(r=>r.Id).SequenceEqual(coldFixture.ids),
            "cold world load restores saved root pose while retaining stable authored IDs");
        using(var plan=registry.PrepareRestore(JsonUtility.FromJson<WorldSceneState>(snapshotJson))){plan.CommitSilent();plan.Publish();}
        yield return null;
        Check(UnityEngine.Object.FindObjectsByType<WorldSaveObject>(FindObjectsInactive.Include)
            .Count(r=>!r.gameObject.scene.name.StartsWith("Save staging "))==2,
            "second disk load in cold reader leaves exactly two authored roots without duplicates");
        Check(motion.TargetOpen&&Near(motion.OpenFraction,1f)&&device.IsOn&&device.Channel==1&&wear.Seed==918,
            "repeat cold load preserves opened furniture radio and wear");
        UnityEngine.Object.DestroyImmediate(furniture);UnityEngine.Object.DestroyImmediate(radio);UnityEngine.Object.DestroyImmediate(registry.gameObject);
        yield return null;
    }
    static IEnumerator VerifyFInput()
    {
        var originalInputSettings=InputSystem.settings;
        var originalInputFlags=originalInputSettings.hideFlags;
        var testInputSettings=UnityEngine.Object.Instantiate(originalInputSettings);
        if(originalInputFlags==HideFlags.HideAndDontSave)originalInputSettings.hideFlags=HideFlags.DontSave;
        testInputSettings.hideFlags=HideFlags.HideAndDontSave;
        testInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        testInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testInputSettings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        InputSystem.settings=testInputSettings;
        var original = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        Check(original != null, "existing project input asset available");
        var interact = original.FindAction("Player/Interact");
        Check(interact != null && interact.bindings.Any(b => b.effectivePath == "<Keyboard>/f"), "existing Interact keyboard binding is F");
        var actor = new GameObject("Temporary real PlayerInteractor"); actor.SetActive(false);
        var camera = new GameObject("Assigned view camera").AddComponent<Camera>(); camera.transform.SetParent(actor.transform); camera.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        var motor = actor.AddComponent<FirstPersonController>(); Set(motor,"inputActions",original); Set(motor,"requireApplicationFocus",false);
        Set(motor,"requireCursorCapture",false); Set(motor,"captureCursorOnEnable",false);
        var capsule = actor.GetComponent<CharacterController>(); capsule.height = 1.8f; capsule.radius = .3f; capsule.center = new Vector3(0f,.9f,0f);
        var selector = actor.AddComponent<PlayerInteractor>(); Set(selector,"viewCamera",camera); Set(selector,"allowWhileAirborne",true);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Temporary floor"; ground.transform.position = new Vector3(0,-.05f,0); ground.transform.localScale = new Vector3(8,.1f,8);
        var item = new GameObject("Temporary F input target"); item.AddComponent<FurnitureAssembly>().Configure("F-test","Дверца","Proof");
        var pivot = GameObject.CreatePrimitive(PrimitiveType.Cube); pivot.transform.SetParent(item.transform); pivot.transform.position = new Vector3(0,1.65f,1.5f); pivot.transform.localScale = new Vector3(.5f,.5f,.08f);
        var action = pivot.AddComponent<FurnitureMechanism>(); action.Configure("F-door",pivot.transform,Vector3.up,95f,true,"Дверца");
        actor.SetActive(true);
        var keyboard = InputSystem.AddDevice<Keyboard>("FurnitureVirtualKeyboard");keyboard.MakeCurrent();InputSystem.EnableDevice(keyboard); InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        try
        {
            foreach (var ignored in Wait(.3f)) yield return ignored;
            Check(ReferenceEquals(selector.Target,action) && selector.IsAvailable && selector.BindingLabel.ToUpperInvariant() == "F", "real PlayerInteractor selects door and shows F binding");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));
            foreach (var ignored in Wait(.12f)) yield return ignored;
            var runtimeAction=motor.FindGameplayAction("Interact");
            Check(action.TargetOpen, "virtual Input System F press reaches real Interact and opens furniture; key="+keyboard.fKey.isPressed+" enabled="+keyboard.enabled+" action="+runtimeAction?.phase+" pressed="+runtimeAction?.IsPressed()+" available="+selector.IsAvailable+" target="+selector.Target?.DisplayName);
            foreach (var ignored in Wait(.35f)) yield return ignored;
            Check(action.TargetOpen, "held F does not repeat or close furniture");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            foreach (var ignored in Wait(.3f)) yield return ignored;
            // Point at the moving leaf's new centre before requesting the second interaction.
            camera.transform.LookAt(pivot.GetComponent<Collider>().bounds.center);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));
            foreach (var ignored in Wait(.12f)) yield return ignored;
            Check(!action.TargetOpen, "second released-and-pressed F reverses furniture through PlayerInteractor");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(item); UnityEngine.Object.DestroyImmediate(ground);
            InputSystem.settings=originalInputSettings;originalInputSettings.hideFlags=originalInputFlags;if(testInputSettings!=null)UnityEngine.Object.DestroyImmediate(testInputSettings);
        }
    }
}
