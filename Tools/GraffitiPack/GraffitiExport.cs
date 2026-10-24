using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PostApocalypse.Graffiti.Editor;

public static class GraffitiExport
{
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/GraffitiPack/"))throw new Exception("Isolation required");
        AssetDatabase.Refresh();AssetDatabase.SaveAssets();
        AssetDatabase.ExportPackage(GraffitiPainter.Root,Path.GetFullPath(Application.dataPath+"/../../../../outputs/GraffitiPack_URP.unitypackage"),ExportPackageOptions.Recurse);
        Debug.Log("GRAFFITI_EXPORT_PASS");
    }
}
