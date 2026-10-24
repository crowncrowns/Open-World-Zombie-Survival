using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieSurvival.EnvironmentProps;

[InitializeOnLoad]
public static class DumpsterPlayChecks
{
    const string Key="DumpsterPlayChecks.Running";
    static float start;static int phase;static GameObject[] roots;static List<string> checks;static Camera camera;static Vector3[] positions;static float observed;
    static string Proof=>Path.GetFullPath("../../../Verification/Dumpster");
    static DumpsterPlayChecks(){EditorApplication.playModeStateChanged+=State;}
    public static void Run()
    {
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(DumpsterBuilder.Root+"/Demo/Dumpster_Preview.unity");EditorApplication.isPlaying=true;
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            checks=new List<string>();phase=0;start=Time.time;
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.32f,.36f,.40f));RenderSettings.ambientProbe=ambient;
            roots=UnityEngine.Object.FindObjectsByType<DumpsterPresentation>(FindObjectsSortMode.None).OrderBy(p=>p.name).Select(p=>p.gameObject).ToArray();
            positions=roots.Select(r=>r.transform.position).ToArray();camera=UnityEngine.Object.FindFirstObjectByType<Camera>();
            EditorApplication.update+=Tick;
        }
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);checks.Add(message);}
    static void Capture(GameObject target,string name,Vector3 view)
    {
        var renderers=roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).ToArray();var saved=renderers.Select(r=>r.forceRenderingOff).ToArray();foreach(var r in renderers)r.forceRenderingOff=!r.transform.IsChildOf(target.transform);
        bool open=name.Contains("_Open");camera.orthographic=true;camera.orthographicSize=open?1.5f:1.03f;
        camera.transform.position=target.transform.position+view;camera.transform.LookAt(target.transform.position+new Vector3(0,open?1.1f:.73f,0));
        var rt=RenderTexture.GetTemporary(1400,1400,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.Render();var prior=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(1400,1400,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,1400),0,0);tex.Apply();File.WriteAllBytes(Proof+"/Unity_"+name+".png",tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);RenderTexture.active=prior;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);
        for(int i=0;i<renderers.Length;i++)renderers[i].forceRenderingOff=saved[i];
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        try
        {
            float elapsed=Time.time-start;
            if(phase==0 && elapsed>.5f)
            {
                foreach(var go in roots)
                {
                    Require(go.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Closed"),go.name+" initial Closed");
                    Require(Mathf.Abs(Mathf.DeltaAngle(go.transform.Find("Lid_Pivot").localEulerAngles.x,0))<.1f,go.name+" closed hinge 0 degrees");
                    Capture(go,go.name+"_Closed",new Vector3(2.1f,1.85f,2.9f));
                    go.GetComponent<DumpsterPresentation>().SetOpen(true);
                }
                phase=1;start=Time.time;observed=0;
            }
            else if(phase==1)
            {
                if(elapsed>.15f&&elapsed<.8f) foreach(var go in roots){float a=Mathf.Abs(Mathf.DeltaAngle(go.transform.Find("Lid_Pivot").localEulerAngles.x,0));if(a>2&&a<103)observed++;}
                if(elapsed<1.5f)return;
                Require(observed>0,"Actual intermediate Play-mode hinge movement");
                foreach(var go in roots)
                {
                    Require(go.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Open"),go.name+" Open state");
                    File.AppendAllText(Proof+"/hinge_diagnostic.txt",go.name+" openEuler="+go.transform.Find("Lid_Pivot").localEulerAngles+" rot="+go.transform.Find("Lid_Pivot").localRotation+"\n");
                    Require(Quaternion.Angle(go.transform.Find("Lid_Pivot").localRotation,Quaternion.Euler(-105,0,0))<.1f,go.name+" open hinge -105 degrees");
                    Capture(go,go.name+"_Open",new Vector3(2.1f,2.5f,2.9f));go.GetComponent<DumpsterPresentation>().SetOpen(false);
                }
                phase=2;start=Time.time;
            }
            else if(phase==2 && elapsed>1.3f)
            {
                foreach(var go in roots)
                {
                    Require(go.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Closed"),go.name+" returned Closed");
                    Require(Mathf.Abs(Mathf.DeltaAngle(go.transform.Find("Lid_Pivot").localEulerAngles.x,0))<.1f,go.name+" closed after cycle");go.GetComponent<DumpsterPresentation>().SetOpen(true);
                }
                phase=3;start=Time.time;
            }
            else if(phase==3 && elapsed>1.4f)
            {
                foreach(var go in roots){Require(go.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Open"),go.name+" repeated open");go.GetComponent<DumpsterPresentation>().SetOpen(false);}
                phase=4;start=Time.time;
            }
            else if(phase==4 && elapsed>1.3f)
            {
                for(int i=0;i<roots.Length;i++)
                {
                    var go=roots[i];Require(go.transform.position==positions[i],go.name+" root unchanged");Require(Mathf.Abs(Mathf.DeltaAngle(go.transform.Find("Lid_Pivot").localEulerAngles.x,0))<.1f,go.name+" repeated close");
                    var group=go.GetComponent<LODGroup>();for(int l=0;l<3;l++){group.ForceLOD(l);Capture(go,go.name+"_LOD"+l,new Vector3(2.1f,1.85f,2.9f));}group.ForceLOD(-1);
                    Capture(go,go.name+"_Rear",new Vector3(-2.1f,1.85f,-2.9f));
                    Physics.SyncTransforms();Require(Physics.Raycast(go.transform.position+new Vector3(0,.7f,2),Vector3.back,out var hit,2.0f),go.name+" front collision");
                    Require(hit.collider.transform.IsChildOf(go.transform),go.name+" collision belongs to asset");
                    // Evaluate complete lid meshes through their sweep against the bag upper envelope.
                    var pivot=go.transform.Find("Lid_Pivot");var lid=pivot.Find("Dumpster_Lid/Dumpster_Lid_LOD0").GetComponent<MeshFilter>();
                    float min=100,minRim=100;
                    for(int a=0;a<=105;a+=3)
                    {
                        pivot.localRotation=Quaternion.Euler(-a,0,0);
                        foreach(var v in lid.sharedMesh.vertices)
                        {
                            var point=go.transform.InverseTransformPoint(lid.transform.TransformPoint(v));min=Mathf.Min(min,point.y);
                            // Underside stiffeners legitimately enter the empty mouth. Rear hinge contact is intentional.
                            if((Mathf.Abs(point.x)>.625f||Mathf.Abs(point.z)>.495f)&&point.z>-.54f)minRim=Mathf.Min(minRim,point.y);
                        }
                    }
                    pivot.localRotation=Quaternion.identity;
                    var bag=go.transform.Find("Garbage_Bags");if(bag){float top=bag.GetComponentsInChildren<Renderer>().Max(r=>r.bounds.max.y-go.transform.position.y);Require(min>top+.02f,go.name+" swept lid above bags; clearance="+(min-top));}
                    Require(minRim>1.219f,go.name+" swept lid clears rim footprint; minimum="+minRim);
                    var presentation=go.GetComponent<DumpsterPresentation>();var body=go.transform.Find("Dumpster_Body/Dumpster_Body_LOD0").GetComponent<Renderer>();var materials=body.sharedMaterials;
                    presentation.SetVariation(100,.15f,.1f,3,1);var p0=go.transform.Find("Decals_Stickers").GetChild(0).localPosition;
                    Capture(go,go.name+"_VariationLight",new Vector3(2.1f,1.85f,2.9f));
                    presentation.SetVariation(200,.85f,.4f,5,2);var p1=go.transform.Find("Decals_Stickers").GetChild(0).localPosition;
                    Capture(go,go.name+"_VariationDirty",new Vector3(2.1f,1.85f,2.9f));
                    Require(p0!=p1,go.name+" decal placement varies by seed");Require(materials.SequenceEqual(body.sharedMaterials),go.name+" variation shares materials");
                    var block=new MaterialPropertyBlock();int material=Array.FindIndex(body.sharedMaterials,m=>m.shader.name=="ZombieSurvival/Dumpster Surface");body.GetPropertyBlock(block,material);Require(Mathf.Abs(block.GetFloat("_DirtAmount")-.85f)<.001f,go.name+" independent dirt mask amount");
                    presentation.SetVariation(100,.15f,.1f,3,1);Require(go.transform.Find("Decals_Stickers").GetChild(0).localPosition==p0,go.name+" variation deterministic");
                }
                File.WriteAllText(Proof+"/unity_play_checks.json","{\"passed\":true,\"checks\":"+Newtonless(checks)+"}");
                SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Debug.Log("DUMPSTER_PLAY_PASS "+checks.Count);EditorApplication.Exit(0);
            }
        }
        catch(Exception e){File.WriteAllText(Proof+"/unity_play_failure.txt",e.ToString());SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static string Newtonless(List<string> values)=>"["+string.Join(",",values.Select(s=>"\""+s.Replace("\\","\\\\").Replace("\"","\\\"")+"\""))+"]";
}
