using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PostApocalypse.Graffiti;
using PostApocalypse.Graffiti.Editor;

public static class GraffitiImpactBuild
{
    const string Root = GraffitiPainter.Root;
    static void Guard()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/work/GraffitiPack/"))
            throw new InvalidOperationException("Graffiti impact assets may only be built in work/GraffitiPack");
    }
    public static void Build()
    {
        Guard();
        var texturePaths = Directory.GetFiles(Root + "/Textures/Impacts", "BulletImpact_*.png").OrderBy(x=>x).ToArray();
        if (texturePaths.Length < 3) throw new Exception("Expected three bullet impact textures");
        foreach (var file in texturePaths)
        {
            var assetPath = file.Replace('\\','/');
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (!texture) throw new Exception("Missing bullet impact texture " + assetPath);
            var name = Path.GetFileNameWithoutExtension(file);
            Directory.CreateDirectory(Root + "/Impacts");
            Directory.CreateDirectory(Root + "/Materials/Impacts");
            Directory.CreateDirectory(Root + "/Prefabs/Impacts");
            var matPath = Root + "/Materials/Impacts/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat)
            {
                mat = GraffitiPipeline.MakeMaterial(texture); mat.name = name;
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var prefabPath = Root + "/Prefabs/Impacts/" + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab) continue;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.GetComponent<MeshRenderer>().receiveShadows = true;
            go.AddComponent<GraffitiPlacement>();
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("GRAFFITI_IMPACTS_BUILD_PASS " + texturePaths.Length);
    }
}
