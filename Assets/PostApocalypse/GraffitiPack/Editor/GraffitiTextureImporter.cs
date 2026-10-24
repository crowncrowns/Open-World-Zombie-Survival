using UnityEditor;

namespace PostApocalypse.Graffiti.Editor
{
    public sealed class GraffitiTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(GraffitiPainter.Root + "/Textures/")) return;
            var t=(TextureImporter)assetImporter;
            t.textureType=TextureImporterType.Default;
            t.sRGBTexture=true;t.alphaSource=TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency=true;t.mipmapEnabled=true;
            t.wrapMode=UnityEngine.TextureWrapMode.Clamp;
            t.filterMode=UnityEngine.FilterMode.Trilinear;
            t.anisoLevel=4;t.npotScale=TextureImporterNPOTScale.None;t.maxTextureSize=4096;
            t.textureCompression=TextureImporterCompression.CompressedHQ;
            t.isReadable=false;
            t.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name="Standalone",overridden=true,maxTextureSize=4096,format=TextureImporterFormat.BC7,compressionQuality=100 });
        }
    }
}
