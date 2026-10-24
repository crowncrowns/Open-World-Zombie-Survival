using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PostApocalypse.Graffiti.Editor;

public static class GraffitiAtlasVerify
{
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/GraffitiPack/"))throw new Exception("Isolation required");
        const string folder="Assets/GraffitiAtlasVerification";
        GraffitiAtlasBuilder.BuildTo(folder);
        var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{folder});var textures=AssetDatabase.FindAssets("t:Texture2D",new[]{folder});var materials=AssetDatabase.FindAssets("t:Material",new[]{folder});
        int expected=AssetDatabase.FindAssets("t:GraffitiEntry",new[]{GraffitiPainter.Root}).Length;int pages=Mathf.CeilToInt(expected/4f);
        if(prefabs.Length!=expected||textures.Length!=pages||materials.Length!=pages)throw new Exception($"Atlas counts {prefabs.Length}/{textures.Length}/{materials.Length}");
        foreach(var guid in prefabs)
        {
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var uv=go.GetComponent<MeshFilter>().sharedMesh.uv;
            if(uv.Any(p=>p.x<0||p.y<0||p.x>1||p.y>1)||!go.GetComponent<Renderer>().sharedMaterial)throw new Exception("Atlas prefab UV/reference");
        }
        File.WriteAllText(Path.GetFullPath(Application.dataPath+"/../../../../Verification/GraffitiPack/atlas_checks.json"),"{\"alternativePrefabs\":"+expected+",\"pages\":"+pages+",\"sharedMaterials\":"+pages+",\"referencesAndUV\":true,\"includedInMainPackage\":false}");
        Debug.Log("GRAFFITI_ATLAS_PASS="+expected+"/"+pages);
    }
}
