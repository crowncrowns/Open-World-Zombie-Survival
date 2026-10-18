#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Automatic sane texture import defaults and preservation of the wind hierarchy.</summary>
public sealed class MoscowTreeFBXImportSettings : AssetPostprocessor
{
    private bool InPack => assetPath.StartsWith("Assets/MoscowTrees/");
    private void OnPreprocessModel()
    {
        if (!InPack || !assetPath.Contains("/Models_FBX/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.preserveHierarchy = true;
        importer.optimizeGameObjects = false;
        importer.importAnimation = false; // Wind is runtime script-driven, not baked FBX clips.
        importer.useFileUnits = true;
        importer.globalScale = 1f;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = false;
    }
    private void OnPreprocessTexture()
    {
        if (!InPack || !assetPath.Contains("/Textures/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.alphaIsTransparency = assetPath.EndsWith("_Leaves.png");
        importer.sRGBTexture = true;
    }
}
#endif
