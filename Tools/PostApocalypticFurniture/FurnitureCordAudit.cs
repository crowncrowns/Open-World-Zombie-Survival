// Verification-only. Copy into the isolated Unity project's Assets/Editor.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Furniture;

public static class FurnitureCordAudit
{
    const string PrefabPath="Assets/PostApocalypticFurniture/Prefabs/Electronics/F158_Electronics_08.prefab";
    static string Proof=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../Verification/PostApocalypticFurniture/ProfessionalAudit"));
    [Serializable] sealed class Report {public bool passed;public string unity,prefab;public int[] triangles;public List<string> checks;}
    static readonly List<string> checks=new List<string>();
    static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);checks.Add(text);}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/PostApocalypticFurniture/UnityProject/"))
            throw new InvalidOperationException("Cord audit requires the isolated furniture Unity project.");
        Directory.CreateDirectory(Proof);checks.Clear();
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);Check(prefab!=null,"Phone158 prefab exists");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var cord=go.GetComponentInChildren<FurnitureCordVisual>(true);Check(cord!=null,"Phone158 has real procedural cord visual");
            var receiver=go.GetComponentsInChildren<FurnitureMechanism>(true).Single(m=>m.StableId=="Receiver");
            Check(cord.GetComponentsInChildren<Collider>(true).Length==0,"cord introduces no collider or gameplay physics");
            var lods=go.GetComponent<LODGroup>().GetLODs();Check(lods.Length==3,"phone has three root LOD levels");
            for(int i=0;i<3;i++)Check(lods[i].renderers.Contains(cord.CordRenderers[i]),"root LOD"+i+" owns corresponding cord renderer");
            var material=cord.CordRenderers[0].sharedMaterial;
            Check(material!=null&&cord.CordRenderers.All(r=>r.sharedMaterial==material),"all cord LODs share one authored material");
            receiver.SetOpen(false,true);cord.RebuildIfNeeded();VerifyEndpoints(cord,"closed");
            var triangles=cord.CurrentMeshes.Select(m=>m.triangles.Length/3).ToArray();
            Check(triangles[0]>triangles[1]&&triangles[1]>triangles[2]&&triangles[0]<2000,"three real optimized cord meshes below 2000 LOD0 triangles");
            foreach(var mesh in cord.CurrentMeshes)VerifyMesh(mesh,"closed");
            int revision=cord.Revision;Check(!cord.RebuildIfNeeded()&&cord.Revision==revision,"unchanged anchors do not rebuild mesh");
            var camera=SetupCamera();go.GetComponent<LODGroup>().ForceLOD(0);Capture(camera,"Phone158_ClosedCord.png");
            receiver.SetOpen(true);
            for(int i=0;i<80;i++){receiver.Step(.025f);cord.RebuildIfNeeded();VerifyEndpoints(cord,"lift step "+i,false);}
            Check(receiver.OpenFraction>.999f,"receiver reaches fully lifted state");VerifyEndpoints(cord,"open");
            foreach(var mesh in cord.CurrentMeshes)VerifyMesh(mesh,"open");
            Check(cord.Revision>revision,"moving receiver rebuilds real cord geometry");
            Check(cord.CordRenderers.All(r=>r.sharedMaterial==material),"lifting receiver creates no material clone");
            Capture(camera,"Phone158_OpenCord.png");
            go.SetActive(false);go.SetActive(true);cord.RebuildIfNeeded();VerifyEndpoints(cord,"reactivated open");
            Check(receiver.OpenFraction>.999f,"cord reactivation preserves receiver mechanism state");
            receiver.SetOpen(false);
            for(int i=0;i<80;i++){receiver.Step(.025f);cord.RebuildIfNeeded();VerifyEndpoints(cord,"return step "+i,false);}
            Check(receiver.OpenFraction<.001f,"receiver returns to cradle");VerifyEndpoints(cord,"returned");
            File.WriteAllText(Path.Combine(Proof,"phone158_cord_audit.json"),JsonUtility.ToJson(new Report{passed=true,unity=Application.unityVersion,prefab=PrefabPath,triangles=triangles,checks=checks},true));
            Debug.Log("FURNITURE_CORD158_PASS "+checks.Count);EditorApplication.Exit(0);
        } catch(Exception error) {File.WriteAllText(Path.Combine(Proof,"phone158_cord_failure.txt"),error.ToString());Debug.LogException(error);EditorApplication.Exit(1);}
    }
    static void VerifyEndpoints(FurnitureCordVisual cord,string label,bool record=true)
    {
        var meshes=cord.CurrentMeshes;
        for(int lod=0;lod<meshes.Length;lod++)
        {
            var filter=cord.CordRenderers[lod].GetComponent<MeshFilter>();var vertices=meshes[lod].vertices;
            float fixedGap=vertices.Min(v=>Vector3.Distance(filter.transform.TransformPoint(v),cord.FixedWorldAnchor));
            float movingGap=vertices.Min(v=>Vector3.Distance(filter.transform.TransformPoint(v),cord.MovingWorldAnchor));
            bool valid=fixedGap<.00001f&&movingGap<.00001f;
            if(!valid)throw new InvalidOperationException(label+" LOD"+lod+" cord detaches: fixedGap="+fixedGap+" movingGap="+movingGap);
            if(record)checks.Add(label+" LOD"+lod+" real mesh reaches both actual anchor points");
        }
    }
    static void VerifyMesh(Mesh mesh,string label)
    {
        var vertices=mesh.vertices;var normals=mesh.normals;var indices=mesh.triangles;
        Check(vertices.Length>100&&indices.Length>0&&normals.Length==vertices.Length,label+" "+mesh.name+" is a real nonempty tube");
        Check(mesh.uv.Length==vertices.Length&&mesh.uv2.Length==vertices.Length,label+" "+mesh.name+" has texture and separate packed lightmap UVs");
        int degenerate=0,opposed=0;float minDot=1f;
        for(int i=0;i<indices.Length;i+=3)
        {
            int a=indices[i],b=indices[i+1],c=indices[i+2];Vector3 cross=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
            if(cross.magnitude<1e-12f){degenerate++;continue;}
            float dot=Vector3.Dot(cross.normalized,(normals[a]+normals[b]+normals[c]).normalized);minDot=Mathf.Min(minDot,dot);if(dot<-.02f)opposed++;
        }
        Check(vertices.All(v=>Finite(v.x)&&Finite(v.y)&&Finite(v.z))&&normals.All(n=>Finite(n.x)&&Finite(n.y)&&Finite(n.z)),label+" "+mesh.name+" finite positions and normals");
        Check(degenerate==0,label+" "+mesh.name+" has no degenerate triangles");
        Check(opposed==0,label+" "+mesh.name+" winding agrees with explicit outward tube normals; minDot="+minDot);
    }
    static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    static Camera SetupCamera()
    {
        RenderSettings.ambientLight=new Color(.35f,.37f,.4f);
        var light=new GameObject("Cord audit sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2.1f;light.transform.rotation=Quaternion.Euler(37,-35,0);
        var fill=new GameObject("Cord audit fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fill.transform.rotation=Quaternion.Euler(15,135,0);
        var camera=new GameObject("Cord audit camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.28f,.3f,.32f);
        camera.fieldOfView=32;camera.nearClipPlane=.005f;camera.farClipPlane=10;camera.transform.position=new Vector3(-.48f,.36f,.58f);camera.transform.LookAt(new Vector3(-.035f,.145f,-.01f));return camera;
    }
    static void Capture(Camera camera,string file)
    {
        var rt=new RenderTexture(1024,768,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Proof,file),texture.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
    }
}
