using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class StoveReopenEntry
{
    public static void Run()
    {
        StoveBuilder.Validate();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(StoveBuilder.Root+"/Prefabs/PF_PotbellyStove_Burning.prefab");
        foreach(var component in prefab.GetComponentsInChildren<Transform>(true))
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(component.gameObject)>0)throw new Exception("Missing child script");
        var maps=Directory.GetFiles(StoveBuilder.Root+"/Textures","*.png").Where(p=>!p.Contains("Opacity")).ToArray();
        foreach(var p in maps){var t=AssetDatabase.LoadAssetAtPath<Texture2D>(p);if(!t||t.width!=4096||t.height!=4096)throw new Exception("4K map "+p);}
        AssetDatabase.ExportPackage(StoveBuilder.Root,Path.GetFullPath("../../../outputs/PotbellyStove_Unity.unitypackage"),ExportPackageOptions.Recurse);
        File.WriteAllText(Path.Combine(StoveBuilder.Proof,"ColdReopen.json"),"{\"passed\":true,\"missingScripts\":0,\"maps4096\":"+maps.Length+",\"prefabLoaded\":true,\"unityPackageRefreshed\":true}");
        Debug.Log("STOVE_COLD_REOPEN_PASS");
    }
}
