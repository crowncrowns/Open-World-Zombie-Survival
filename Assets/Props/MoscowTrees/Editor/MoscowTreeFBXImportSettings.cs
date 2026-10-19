#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Automatic sane texture import defaults and preservation of the wind hierarchy.</summary>
public sealed class MoscowTreeFBXImportSettings : AssetPostprocessor
{
    private bool InPack => assetPath.StartsWith("Assets/MoscowTrees/");
    public override uint GetVersion() => 2;
    private void OnPostprocessModel(GameObject root)
    {
        if (!InPack || !assetPath.Contains("/Models_FBX/")) return;
        string species = System.IO.Path.GetFileNameWithoutExtension(assetPath).Split('_')[0];
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            bool leaves = renderer.name.IndexOf("Leaves", System.StringComparison.OrdinalIgnoreCase) >= 0
                || renderer.name.IndexOf("Foliage", System.StringComparison.OrdinalIgnoreCase) >= 0;
            string materialPath = "Assets/MoscowTrees/Materials/" + species + (leaves ? "_Leaves.mat" : "_Bark.mat");
            context.DependsOnSourceAsset(materialPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) continue;
            var filter = renderer.GetComponent<MeshFilter>();
            int count = filter != null && filter.sharedMesh != null ? Mathf.Max(1, filter.sharedMesh.subMeshCount) : 1;
            var slots = new Material[count];
            for (int i = 0; i < count; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }
    }
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
