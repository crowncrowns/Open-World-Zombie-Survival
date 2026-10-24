using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PostApocalypse.Graffiti;
using PostApocalypse.Graffiti.Editor;

public static class GraffitiVerify
{
    static readonly List<string> passed=new List<string>();
    static string Output=>Path.GetFullPath(Application.dataPath+"/../../../../Verification/GraffitiPack");
    static void Check(bool test,string name) {if(!test)throw new Exception("FAIL "+name);passed.Add(name);Debug.Log("PASS "+name);}
    static Texture2D Capture(Camera camera,string name,int width=1400,int height=900)
    {
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
        File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);return image;
    }
    static double Mean(Texture2D t)=>t.GetPixels().Average(p=>(double)(p.r+p.g+p.b)/3);
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/GraffitiPack/"))throw new Exception("Isolated project only");
        Directory.CreateDirectory(Output);GraffitiBuild.Validate();
        EditorSceneManager.OpenScene(GraffitiPainter.Root+"/Demo/GraffitiGallery.unity");
        var entries=AssetDatabase.FindAssets("t:GraffitiEntry",new[]{GraffitiPainter.Root}).Select(x=>AssetDatabase.LoadAssetAtPath<GraffitiEntry>(AssetDatabase.GUIDToAssetPath(x))).OrderBy(x=>x.number).ToArray();
        int expected=JsonUtility.FromJson<GraffitiBuild.Catalog>(File.ReadAllText(GraffitiPainter.Root+"/Documentation/catalog.json")).items.Length;
        Check(entries.Length==expected,expected+" catalogue entries");Check(GraffitiPipeline.Current=="URP","URP auto detection");Check(!GraffitiPipeline.HasDecals,"No feature: Quad fallback selected");
        var camera=UnityEngine.Object.FindFirstObjectByType<Camera>();
        var all=Capture(camera,"Gallery_Daylight",2000,1100);
        Check(all.GetPixels().Count(c=>c.r>.8f&&c.b>.8f&&c.g<.15f)<20,"Gallery contains no magenta shader pixels");UnityEngine.Object.DestroyImmediate(all);
        camera.orthographicSize=1.8f;
        for(int i=0;i<entries.Length;i++)
        {
            var e=entries[i];int rows=Mathf.CeilToInt(entries.Length/10f);var target=new Vector3(i%10*3.5f,(rows-1-i/10)*3.5f+1.5f,-.1f);
            camera.transform.position=target+Vector3.back*4;camera.transform.rotation=Quaternion.identity;
            var image=Capture(camera,"Render_"+e.number.ToString("00"),800,600);
            var rendererForEntry=UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None).Single(p=>p.entry==e).GetComponent<Renderer>();
            rendererForEntry.enabled=false;var background=Capture(camera,"Background_"+e.number.ToString("00"),800,600);rendererForEntry.enabled=true;
            var paintedPixels=image.GetPixels();var wallPixels=background.GetPixels();int changed=0;
            for(int p=0;p<paintedPixels.Length;p++)if(Mathf.Abs(paintedPixels[p].r-wallPixels[p].r)+Mathf.Abs(paintedPixels[p].g-wallPixels[p].g)+Mathf.Abs(paintedPixels[p].b-wallPixels[p].b)>.025f)changed++;
            Check(changed>100,"Rendered visible paint "+e.number.ToString("00"));UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(background);
        }
        var point=GameObject.Find("Wall_31_3x3m").transform.position+Vector3.back*.1f;camera.transform.position=point+Vector3.back*4;camera.transform.rotation=Quaternion.identity;
        var day=Capture(camera,"Light_Day",900,900);double dayMean=Mean(day);UnityEngine.Object.DestroyImmediate(day);
        var lamp=UnityEngine.Object.FindFirstObjectByType<Light>();lamp.enabled=false;
        var ambient=RenderSettings.ambientLight;RenderSettings.ambientLight=Color.black;RenderSettings.reflectionIntensity=0;
        var night=Capture(camera,"Light_Zero",900,900);double nightMean=Mean(night);UnityEngine.Object.DestroyImmediate(night);
        Check(nightMean<dayMean*.35,"No unlit/emission glow at zero light");
        lamp.enabled=true;RenderSettings.ambientLight=ambient;
        foreach(float angle in new[]{30f,65f,80f})
        {
            camera.transform.position=point+Quaternion.Euler(0,angle,0)*Vector3.back*4;camera.transform.LookAt(point);
            var image=Capture(camera,"Angle_"+angle.ToString("0"),900,900);UnityEngine.Object.DestroyImmediate(image);
        }
        Check(Vector3.Dot(GraffitiPainter.SurfaceRotation(Vector3.left,0)*Vector3.back,Vector3.left)>.9999f,"Local Quad normal aligns to arbitrary wall normal");
        var wall=GameObject.Find("Wall_31_3x3m");Physics.SyncTransforms();
        Check(GraffitiPainter.Eligible(wall.GetComponent<Collider>(),1),"Static wall eligible");
        Check(!GraffitiPainter.Eligible(wall.GetComponent<Collider>(),2),"LayerMask rejects excluded geometry");
        var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.position=new Vector3(80,0,0);
        obstacle.isStatic=false;Check(GraffitiPainter.Eligible(obstacle.GetComponent<Collider>(),1),"Nonstatic authored geometry supported");
        obstacle.isStatic=true;obstacle.AddComponent<Rigidbody>().isKinematic=true;Check(!GraffitiPainter.Eligible(obstacle.GetComponent<Collider>(),1),"Rigidbody rejected");UnityEngine.Object.DestroyImmediate(obstacle);
        Check(GraffitiPainter.Unsupported(point,Quaternion.identity,2,2,1,false)==0,"49 support samples on continuous wall");
        Check(GraffitiPainter.Unsupported(new Vector3(70,0,0),Quaternion.identity,2,2,1,false)==49,"49 missing samples detect opening");
        int before=UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None).Length;
        Undo.IncrementCurrentGroup();var placed=GraffitiPainter.Place(entries[30],point+Vector3.back*.003f,Quaternion.identity,1.4f,.42f,wall.transform);
        Undo.FlushUndoRecordObjects();
        Check(placed.GetComponent<GraffitiPlacement>().paintedByTool,"Placement marked for tool deletion");
        var block=new MaterialPropertyBlock();placed.GetComponent<Renderer>().GetPropertyBlock(block);Check(Mathf.Abs(block.GetColor("_BaseColor").a-.42f)<.001f,"Opacity stored in property block");
        Check(Mathf.Abs(placed.transform.localScale.y-1.4f/entries[30].Aspect)<.0001f,"Aspect preserved in world metre scale");
        Undo.PerformUndo();Check(UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None).Length==before,"Undo removes placement");
        Undo.PerformRedo();Check(UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None).Length==before+1,"Redo restores placement");
        Undo.IncrementCurrentGroup();GraffitiPainter.DeletePainted();Check(UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None).Length==before,"Mass deletion retains authored gallery instances");
        Undo.PerformUndo();Check(UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None).Length==before+1,"Mass deletion undo");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/GraffitiPlacementSaveCheck.unity");
        EditorSceneManager.OpenScene("Assets/GraffitiPlacementSaveCheck.unity");
        var restored=UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None).Single(p=>p.paintedByTool);
        Check(Mathf.Abs(restored.opacity-.42f)<.001f&&Mathf.Abs(restored.transform.localScale.x-1.4f)<.001f,"Placement opacity, ownership and transform survive scene save/reopen");
        var restoredBlock=new MaterialPropertyBlock();restored.GetComponent<Renderer>().GetPropertyBlock(restoredBlock);Check(Mathf.Abs(restoredBlock.GetColor("_BaseColor").a-.42f)<.001f,"Reopened placement restores opacity block");
        wall=GameObject.Find("Wall_31_3x3m");
        foreach(var p in UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None))if(p.paintedByTool)UnityEngine.Object.DestroyImmediate(p.gameObject);
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(GraffitiPainter.Root+"/Demo/IsolatedRenderer.asset");
        var feature=ScriptableObject.CreateInstance<DecalRendererFeature>();feature.name="VerificationOnlyDecal";renderer.rendererFeatures.Add(feature);AssetDatabase.AddObjectToAsset(feature,renderer);EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssets();
        Check(GraffitiPipeline.HasDecals,"Native feature detection");
        var projected=GraffitiPainter.Place(entries[30],point+Vector3.back*.003f,Quaternion.identity,2,.8f,wall.transform,false);
        GraffitiPipeline.Project(projected,entries[30],2,.8f,false);
        Check(projected.GetComponent<DecalProjector>()&&!projected.GetComponent<Renderer>().enabled,"Supported URP uses native projector");
        Check(Mathf.Abs(projected.GetComponent<DecalProjector>().fadeFactor-.8f)<.001f,"Native projector opacity");
        UnityEngine.Object.DestroyImmediate(projected);renderer.rendererFeatures.Remove(feature);UnityEngine.Object.DestroyImmediate(feature,true);EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(GraffitiPainter.Root+"/Demo/GraffitiGallery.unity");
        File.WriteAllText(Output+"/technical_checks.json",JsonUtility.ToJson(new Result{passed=passed.ToArray(),dayMean=(float)dayMean,zeroLightMean=(float)nightMean},true));
        AssetDatabase.ExportPackage(GraffitiPainter.Root,Path.GetFullPath(Application.dataPath+"/../../../../outputs/GraffitiPack_URP.unitypackage"),ExportPackageOptions.Recurse);
        Debug.Log("GRAFFITI_TECHNICAL_PASS="+passed.Count);
    }
    [Serializable] public class Result {public string[] passed;public float dayMean,zeroLightMean;}
}
