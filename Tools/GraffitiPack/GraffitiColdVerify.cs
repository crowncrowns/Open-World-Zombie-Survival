using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using PostApocalypse.Graffiti;
using PostApocalypse.Graffiti.Editor;

public static class GraffitiColdVerify
{
    [Serializable] public class Manifest { public ManifestItem[] items; }
    [Serializable] public class ManifestItem { public int id; }
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/GraffitiPack/"))throw new Exception("Isolation required");
        var pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(GraffitiPainter.Root+"/Demo/IsolatedURP.asset");
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        var entries=AssetDatabase.FindAssets("t:GraffitiEntry",new[]{GraffitiPainter.Root}).Select(x=>AssetDatabase.LoadAssetAtPath<GraffitiEntry>(AssetDatabase.GUIDToAssetPath(x))).ToArray();
        int expected=JsonUtility.FromJson<Manifest>(File.ReadAllText(GraffitiPainter.Root+"/Documentation/catalog.json")).items.Length;
        if(entries.Length!=expected)throw new Exception("Cold count "+entries.Length);
        foreach(var entry in entries)
        {
            if(!entry.texture||!entry.prefab)throw new Exception("Cold reference "+entry.name);
            var go=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(entry.prefab));
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)>0||!go.GetComponent<MeshFilter>().sharedMesh||!go.GetComponent<Renderer>().sharedMaterial||!go.GetComponent<Renderer>().sharedMaterial.shader.isSupported)throw new Exception("Cold prefab "+entry.name);
            PrefabUtility.UnloadPrefabContents(go);
        }
        var scene=EditorSceneManager.OpenScene(GraffitiPainter.Root+"/Demo/GraffitiGallery.unity");
        int instances=UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None).Length;
        if(instances!=expected)throw new Exception("Cold gallery "+instances);
        File.WriteAllText(Path.GetFullPath(Application.dataPath+"/../../../../Verification/GraffitiPack/cold_import.json"),"{\"prefabsPassed\":"+entries.Length+",\"demoInstances\":"+instances+",\"pipeline\":\""+GraffitiPipeline.Current+"\",\"packageImportedInFreshProject\":true}");
        Debug.Log("GRAFFITI_COLD_IMPORT_PASS="+expected);
    }
}
