#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class MoscowTreePreview
{
    public static void Render()
    {
        ShaderUtil.allowAsyncCompilation=false;
        string[] species={"Birch","Oak","Pine","Spruce","Maple","Linden","Aspen","Rowan"};
        for(int i=0;i<species.Length;i++)
        {
            var p=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MoscowTrees/Prefabs/"+species[i]+"_TallMature.prefab");
            var o=Object.Instantiate(p,new Vector3((i%4)*18,0,(i/4)*24),Quaternion.identity);
            o.GetComponent<LODGroup>().ForceLOD(0);
            o.GetComponent<MoscowTreeWind>().enabled=false;
        }
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.55f,.6f,.65f);
        var light=new GameObject("Preview Sun").AddComponent<Light>();
        light.type=LightType.Directional; light.intensity=2; light.transform.rotation=Quaternion.Euler(40,-35,0);
        var cam=new GameObject("Preview Camera").AddComponent<Camera>();
        cam.transform.position=new Vector3(65,39,-78); cam.transform.LookAt(new Vector3(27,10,12));
        cam.orthographic=true; cam.orthographicSize=33; cam.farClipPlane=300;
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.18f,.22f,.26f);
        cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var rt=new RenderTexture(1600,1000,24); rt.Create(); cam.targetTexture=rt;
        var request=new UniversalRenderPipeline.SingleCameraRequest { destination=rt };
        RenderPipeline.SubmitRenderRequest(cam,request);
        RenderPipeline.SubmitRenderRequest(cam,request);
        var prior=RenderTexture.active; RenderTexture.active=rt;
        var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); tex.Apply();
        File.WriteAllBytes("preview.png",tex.EncodeToPNG()); RenderTexture.active=prior;
        Object.DestroyImmediate(tex); cam.targetTexture=null; rt.Release(); Object.DestroyImmediate(rt);
    }
}
#endif
