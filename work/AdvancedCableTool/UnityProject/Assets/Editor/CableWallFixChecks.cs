using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AdvancedCableTool;
using AdvancedCableTool.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CableWallFixChecks
{
    static readonly string Output = Path.GetFullPath("../../../Verification/AdvancedCableTool/WallAndPerformanceFix");
    static readonly List<string> results = new List<string>();
    static void Guard()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/work/AdvancedCableTool/UnityProject/Assets")) throw new Exception("Isolated cable project only");
        Directory.CreateDirectory(Output);
    }
    static void Check(bool valid, string name)
    { results.Add((valid ? "PASS " : "FAIL ") + name); File.WriteAllLines(Path.Combine(Output,"wall_checks.txt"),results); if (!valid) throw new Exception(name); }
    static GameObject Box(string name, Vector3 position, Vector3 scale, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;go.transform.position = position;go.transform.localScale = scale;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    static void Scene()
    {
        CableSurfaceQuery.Clear(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Box("Ground",new Vector3(0,-.25f,0),new Vector3(30,.5f,30)); Physics.SyncTransforms();
    }
    public static void Baseline() { Measure("baseline"); }
    public static void Optimized() { Measure("optimized"); }
    static void Measure(string name)
    {
        Guard(); Scene();
        try
        {
            var cables = new List<CableGenerator>();
            for (int i = 0; i < 3; ++i)
            {
                var c = CableToolWindow.CreateCable(new Vector3(i*3-3,4,0),null,null,null,6);
                c.physicsEnabled = true;c.editorSimulation = true;c.physicsSegmentLimit = 64;c.windStrength = 2;c.Rebuild();cables.Add(c);
            }
            for (int i=0;i<15;i++) foreach(var c in cables)c.PreviewStep(.02f);
            double[] times=new double[50]; var watch=new Stopwatch();
            for(int i=0;i<times.Length;i++)
            {watch.Restart();foreach(var c in cables)c.PreviewStep(.02f);watch.Stop();times[i]=watch.Elapsed.TotalMilliseconds;}
            Array.Sort(times);
            File.WriteAllText(Path.Combine(Output,name+"_cpu.json"),"{\"three_cables\":3,\"segments_each\":64,\"steps\":50,\"mean_ms\":"+times.Average().ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+",\"median_ms\":"+times[25].ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+",\"p95_ms\":"+times[47].ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"}");
            EditorApplication.Exit(0);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(Output,"failure.txt"),ex.ToString());EditorApplication.Exit(1);}
    }
    public static void Walls()
    {
        Guard();results.Clear();Scene();
        try
        {
            var wall=Box("Visible wall without Collider",new Vector3(0,3,0),new Vector3(.15f,6,8),false);
            var a=Box("Support A",new Vector3(-3,4,0),new Vector3(.5f,6,.5f));
            var b=Box("Support B",new Vector3(3,4,0),new Vector3(.5f,6,.5f));Physics.SyncTransforms();
            Check(CableSurfaceQuery.Raycast(SceneManager.GetActiveScene(),new Ray(new Vector3(-8,5,2),Vector3.right),out var visualHit),"Raycast finds visible mesh without Collider");
            Check(CableSurfaceQuery.SourceTransform(visualHit)==wall.transform,"Surface binding resolves to original wall Transform, not query proxy");
            var blocked=CableToolWindow.CreateCable(new Vector3(-3,5,-1),null,new Vector3(3,5,-1),null,7);
            Check(blocked.ValidatePlacement(),"Reproduction: original physics-only validation misses renderer-only wall");
            Check(!CableSurfaceQuery.Validate(blocked,out _),"New validation blocks renderer-only wall");Object.DestroyImmediate(blocked.gameObject);
            var window=ScriptableObject.CreateInstance<CableToolWindow>();
            typeof(CableToolWindow).GetField("length",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(window,4f);
            window.BeginDrawing();
            Check(CableSurfaceQuery.Raycast(SceneManager.GetActiveScene(),new Ray(new Vector3(-3,5,-10),Vector3.forward),out var hitA),"Pick support A");
            Check(window.PlaceHit(hitA),"First placement creates valid hanging cable");
            var cable=Selection.activeGameObject.GetComponent<CableGenerator>();var before=(Vector3[])cable.Points.Clone();float originalLength=cable.cableLength;
            Check(CableSurfaceQuery.Raycast(SceneManager.GetActiveScene(),new Ray(new Vector3(3,5,-10),Vector3.forward),out var hitB),"Pick support B");
            Check(!window.PlaceHit(hitB),"Second click through wall rejected");
            Check(!cable.anchors[1].attached && cable.cableLength==originalLength,"Rejected click restores free B and original length");
            Check(before.Length==cable.Points.Length && before.Zip(cable.Points,(p,q)=>Vector3.Distance(p,q)).Max()<.0001f,"Rejected click restores every previous vertex of cable path");
            Check(CableSurfaceQuery.Validate(cable,out _),"Restored hanging cable does not remain through the wall");
            window.BeginReattach();Check(!window.PlaceHit(hitB) && !cable.anchors[1].attached,"Reattach also rejects blocked wall route");
            Object.DestroyImmediate(cable.gameObject);Object.DestroyImmediate(window);
            var valid=CableToolWindow.CreateCable(new Vector3(-3,5,-3),null,new Vector3(-3,5,3),null,7);
            var tube=valid.transform.Find("Cable_LOD0").GetComponent<MeshFilter>().sharedMesh;
            Check(tube.tangents.Length==tube.vertexCount && tube.tangents.All(t=>!float.IsNaN(t.x) && Mathf.Abs(new Vector3(t.x,t.y,t.z).magnitude-1)<.001f),"Optimized mesh still supplies finite normalized normal-map tangents");
            var snapshot=CableEditing.Capture(valid);Vector3 oldEnd=valid.anchors[1].Resolve();
            valid.anchors[1].Attach(new Vector3(3,5,3));valid.cableLength=10;
            Check(!CableEditing.Commit(valid,snapshot,out _),"Moving existing anchor through wall is rejected");
            Check(valid.anchors[1].Resolve()==oldEnd && valid.cableLength==7,"Anchor and settings roll back together");
            Object.DestroyImmediate(valid.gameObject);
            var reroute=CableToolWindow.CreateCable(new Vector3(-3,5,-1),null,new Vector3(3,5,-1),null,6.4f);
            Selection.activeGameObject=reroute.gameObject;var routeWindow=ScriptableObject.CreateInstance<CableToolWindow>();routeWindow.BeginIntermediate();
            Check(CableSurfaceQuery.Raycast(SceneManager.GetActiveScene(),new Ray(new Vector3(-.074f,10,-1),Vector3.down),out var cornerA),"Pick first wall-top corner");
            Check(!routeWindow.PlaceHit(cornerA) && reroute.anchors.Count==2,"Incomplete wall bypass stays in preview without committing a crossing");
            Check(CableSurfaceQuery.Raycast(SceneManager.GetActiveScene(),new Ray(new Vector3(.074f,10,-1),Vector3.down),out var cornerB),"Pick second wall-top corner");
            Check(routeWindow.PlaceHit(cornerB) && reroute.anchors.Count==4 && CableSurfaceQuery.Validate(reroute,out _),"Two staged supports commit a complete collision-free wall bypass");
            Object.DestroyImmediate(routeWindow);Object.DestroyImmediate(reroute.gameObject);
            Check(wall.GetComponents<Collider>().Length==0,"No Collider was added to user's source wall");
            Object.DestroyImmediate(wall);Object.DestroyImmediate(a);Object.DestroyImmediate(b);CableSurfaceQuery.Clear();
            // Four planar strips leave an actual doorway, not a solid Bounds box.
            var vertices=new List<Vector3>();var triangles=new List<int>();
            Action<float,float,float,float> quad=(x0,x1,y0,y1)=>{int n=vertices.Count;vertices.AddRange(new[]{new Vector3(x0,y0,0),new Vector3(x1,y0,0),new Vector3(x1,y1,0),new Vector3(x0,y1,0)});triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});};
            quad(-3,-1,0,5);quad(1,3,0,5);quad(-1,1,3,5);
            var doorway=new GameObject("Non-readable wall with opening");var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);
            doorway.AddComponent<MeshFilter>().sharedMesh=mesh;doorway.AddComponent<MeshRenderer>();
            Check(!mesh.isReadable,"Fixture uses a genuinely non-readable mesh");
            var opening=CableToolWindow.CreateCable(new Vector3(0,2,-2),null,new Vector3(0,2,2),null,4.2f);
            Check(CableSurfaceQuery.Validate(opening,out _),"Cable through real doorway remains allowed");Object.DestroyImmediate(opening.gameObject);
            foreach(int sign in new[]{-1,1})
            {
                var crossing=CableToolWindow.CreateCable(new Vector3(2,2,sign*2),null,new Vector3(2,2,-sign*2),null,4.2f);
                Check(!CableSurfaceQuery.Validate(crossing,out _),"Single-sided wall blocks from direction "+sign);Object.DestroyImmediate(crossing.gameObject);
            }
            doorway.transform.localScale=new Vector3(-1,1,1);
            var mirrored=CableToolWindow.CreateCable(new Vector3(-2,2,-2),null,new Vector3(-2,2,2),null,4.2f);
            Check(!CableSurfaceQuery.Validate(mirrored,out _),"Mirrored wall still blocks");
            doorway.layer=8;mirrored.collisionMask=~(1<<8);Check(CableSurfaceQuery.Validate(mirrored,out _),"Collision mask excludes wall explicitly");
            Object.DestroyImmediate(mirrored.gameObject);CableSurfaceQuery.Clear();Object.DestroyImmediate(doorway);Object.DestroyImmediate(mesh);
            EditorApplication.Exit(0);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(Output,"failure.txt"),ex.ToString());UnityEngine.Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
