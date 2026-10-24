using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PostApocalypse.Graffiti;
using PostApocalypse.Graffiti.Editor;

public static class GraffitiBuild
{
    const string R=GraffitiPainter.Root;
    [Serializable] public class Item { public int id; public string title,category,file;public float width; }
    [Serializable] public class Catalog { public Item[] items; }
    static void Guard()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/GraffitiPack/"))throw new Exception("Isolated work/GraffitiPack project only");
    }
    public static void Build()
    {
        Guard();
        Directory.CreateDirectory(R+"/Materials");Directory.CreateDirectory(R+"/Prefabs");Directory.CreateDirectory(R+"/Catalog");Directory.CreateDirectory(R+"/Demo");
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(R+"/Demo/IsolatedRenderer.asset");
        if(!renderer){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,R+"/Demo/IsolatedRenderer.asset");}
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(R+"/Demo/IsolatedURP.asset");
        if(!pipeline){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,R+"/Demo/IsolatedURP.asset");}
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(R+"/Documentation/catalog.json"));
        foreach(var item in catalog.items)
        {
            if(AssetDatabase.LoadAssetAtPath<GraffitiEntry>($"{R}/Catalog/{item.file}.asset"))continue;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>($"{R}/Textures/{item.category}/{item.file}.png");
            if(!texture)throw new Exception("Missing texture "+item.file);
            string readable=item.file+"_"+string.Concat(item.title.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
            var mat=GraffitiPipeline.MakeMaterial(texture);mat.name=readable;AssetDatabase.CreateAsset(mat,$"{R}/Materials/{readable}.mat");
            var entry=ScriptableObject.CreateInstance<GraffitiEntry>();entry.number=item.id;entry.title=item.title;entry.category=item.category;entry.width=item.width;entry.texture=texture;
            AssetDatabase.CreateAsset(entry,$"{R}/Catalog/{item.file}.asset");
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=item.file+"_"+item.title;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale=new Vector3(item.width,item.width/entry.Aspect,1);
            var mr=go.GetComponent<MeshRenderer>();mr.sharedMaterial=mat;mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=true;
            var marker=go.AddComponent<GraffitiPlacement>();marker.entry=entry;
            entry.prefab=PrefabUtility.SaveAsPrefabAsset(go,$"{R}/Prefabs/{readable}.prefab");
            EditorUtility.SetDirty(entry);UnityEngine.Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets();
        Demo();
        Validate();
        AssetDatabase.ExportPackage(R,Path.GetFullPath(Application.dataPath+"/../../../../outputs/GraffitiPack_URP.unitypackage"),ExportPackageOptions.Recurse);
        Debug.Log("GRAFFITI_BUILD_PASS");
    }
    public static void Demo()
    {
        Guard();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.22f,.24f,.27f);
        var wallMat=AssetDatabase.LoadAssetAtPath<Material>(R+"/Demo/Wall.mat");
        if(!wallMat){wallMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));wallMat.SetColor("_BaseColor",new Color(.39f,.41f,.37f));wallMat.SetFloat("_Smoothness",.05f);AssetDatabase.CreateAsset(wallMat,R+"/Demo/Wall.mat");}
        var entries=AssetDatabase.FindAssets("t:GraffitiEntry",new[]{R}).Select(x=>AssetDatabase.LoadAssetAtPath<GraffitiEntry>(AssetDatabase.GUIDToAssetPath(x))).OrderBy(x=>x.number).ToArray();
        foreach(var entry in entries)
        {
            int i=entry.number-1;int rows=Mathf.CeilToInt(entries.Length/10f);Vector3 pos=new Vector3((i%10)*3.5f,(rows-1-i/10)*3.5f+1.5f,0);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name=$"Wall_{entry.number:00}_3x3m";wall.isStatic=true;wall.transform.position=pos;wall.transform.localScale=new Vector3(3,3,.2f);wall.GetComponent<Renderer>().sharedMaterial=wallMat;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);go.transform.position=pos+Vector3.back*.104f;
            // A landscape module can host tall work at an appropriate uniformly reduced size.
            float fit=Mathf.Min(1,2.85f/Mathf.Max(entry.width,entry.width/entry.Aspect));go.transform.localScale*=fit;
        }
        var lamp=new GameObject("Daylight").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.1f;lamp.transform.rotation=Quaternion.Euler(35,-30,0);
        int rowCount=Mathf.CeilToInt(entries.Length/10f);
        var camera=new GameObject("Catalog Camera").AddComponent<Camera>();camera.transform.position=new Vector3(15.75f,((rowCount-1)*3.5f+3)/2,-34);camera.transform.rotation=Quaternion.identity;camera.orthographic=true;camera.orthographicSize=Mathf.Max(12.2f,rowCount*1.75f+.8f);camera.backgroundColor=new Color(.11f,.13f,.14f);camera.clearFlags=CameraClearFlags.SolidColor;
        EditorSceneManager.SaveScene(scene,R+"/Demo/GraffitiGallery.unity");
    }
    public static void Finish()
    {
        Guard();var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(R+"/Demo/IsolatedURP.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        foreach(var id in AssetDatabase.FindAssets("t:GraffitiEntry",new[]{R}))
        {
            var e=AssetDatabase.LoadAssetAtPath<GraffitiEntry>(AssetDatabase.GUIDToAssetPath(id));
            var go=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(e.prefab));go.transform.localScale=new Vector3(e.width,e.width/e.Aspect,1);PrefabUtility.SaveAsPrefabAsset(go,AssetDatabase.GetAssetPath(e.prefab));PrefabUtility.UnloadPrefabContents(go);
        }
        AssetDatabase.SaveAssets();Demo();Validate();Debug.Log("GRAFFITI_FINISH_PASS");
    }
    public static void Validate()
    {
        Guard();int count=0;
        foreach(var id in AssetDatabase.FindAssets("t:GraffitiEntry",new[]{R}))
        {
            var e=AssetDatabase.LoadAssetAtPath<GraffitiEntry>(AssetDatabase.GUIDToAssetPath(id));
            if(!e.texture||!e.prefab)throw new Exception("Reference "+e.name);
            var go=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(e.prefab));
            var mr=go.GetComponent<MeshRenderer>();
            if(!mr||!mr.sharedMaterial||!mr.sharedMaterial.shader||!mr.sharedMaterial.shader.isSupported||mr.sharedMaterial.GetTexture("_BaseMap")!=e.texture||GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)>0)throw new Exception("Prefab "+e.name);
            if(go.GetComponent<Collider>()||mr.shadowCastingMode!=ShadowCastingMode.Off)throw new Exception("Rendering "+e.name);
            var ti=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(e.texture));
            if(!ti.sRGBTexture||!ti.mipmapEnabled||!ti.alphaIsTransparency)throw new Exception("Import "+e.name);
            PrefabUtility.UnloadPrefabContents(go);count++;
        }
        int expected=JsonUtility.FromJson<Catalog>(File.ReadAllText(R+"/Documentation/catalog.json")).items.Length;
        if(count!=expected)throw new Exception("Count "+count+" / "+expected);
        string json="{\"prefabsPassed\":"+count+",\"pipeline\":\""+GraffitiPipeline.Current+"\",\"nativeDecalFeature\":"+GraffitiPipeline.HasDecals.ToString().ToLowerInvariant()+",\"unity\":\""+Application.unityVersion+"\"}";
        File.WriteAllText(Path.GetFullPath(Application.dataPath+"/../../../../Verification/GraffitiPack/unity_validation.json"),json);
        Debug.Log("GRAFFITI_PREFABS_PASS="+count);
    }
}
