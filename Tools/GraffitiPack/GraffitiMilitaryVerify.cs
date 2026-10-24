using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PostApocalypse.Graffiti;
using PostApocalypse.Graffiti.Editor;

public static class GraffitiMilitaryVerify
{
    static string Out=>Path.GetFullPath(Application.dataPath+"/../../../../Verification/GraffitiPack");
    static Texture2D Capture(Camera camera,string name,int w=900,int h=700)
    {
        var rt=RenderTexture.GetTemporary(w,h,24);var previous=RenderTexture.active;camera.aspect=(float)w/h;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var t=new Texture2D(w,h,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,w,h),0,0);t.Apply();
        if(name!=null)File.WriteAllBytes(Out+"/"+name+".png",t.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);return t;
    }
    public static void RefreshGallery()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/GraffitiPack/"))throw new Exception("Isolation only");
        GraffitiBuild.Demo();var camera=UnityEngine.Object.FindFirstObjectByType<Camera>();var image=Capture(camera,"Gallery_60_Daylight",2000,1350);UnityEngine.Object.DestroyImmediate(image);
    }
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/GraffitiPack/"))throw new Exception("Isolation only");
        GraffitiBuild.Validate();EditorSceneManager.OpenScene(GraffitiPainter.Root+"/Demo/GraffitiGallery.unity");
        var all=UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None);if(all.Length!=60)throw new Exception("Gallery count "+all.Length);
        var camera=UnityEngine.Object.FindFirstObjectByType<Camera>();var gallery=Capture(camera,"Gallery_60_Daylight",2000,1350);UnityEngine.Object.DestroyImmediate(gallery);
        var added=all.Where(p=>p.entry.category=="Military2022").OrderBy(p=>p.entry.number).ToArray();if(added.Length!=10)throw new Exception("Category count");
        camera.orthographicSize=1.7f;
        foreach(var p in added)
        {
            camera.transform.position=p.transform.position+Vector3.back*4;camera.transform.rotation=Quaternion.identity;
            var painted=Capture(camera,"Render_"+p.entry.number);p.GetComponent<Renderer>().enabled=false;var bare=Capture(camera,null);p.GetComponent<Renderer>().enabled=true;
            var a=painted.GetPixels();var b=bare.GetPixels();int changed=0;
            for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>.025f)changed++;
            if(changed<100)throw new Exception("Invisible new paint "+p.entry.number);
            if(a.Count(c=>c.r>.8f&&c.b>.8f&&c.g<.15f)>20)throw new Exception("Magenta shader "+p.entry.number);
            UnityEngine.Object.DestroyImmediate(painted);UnityEngine.Object.DestroyImmediate(bare);Debug.Log("MILITARY_RENDER_PASS="+p.entry.number);
        }
        File.WriteAllText(Out+"/military_checks.json","{\"totalPrefabs\":60,\"addedPrefabs\":10,\"category\":\"Military2022\",\"visiblePaintRenders\":10,\"galleryInstances\":60}");
        GraffitiAtlasVerify.Run();
        Debug.Log("GRAFFITI_MILITARY_PASS=10");
    }
}
