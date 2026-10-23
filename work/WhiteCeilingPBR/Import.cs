string f="Assets/Maps/Buildings/Textures/WhiteStairwellCeiling_01";
UnityEditor.AssetDatabase.Refresh();
foreach(string p in System.IO.Directory.GetFiles(f,"*.png")){
var t=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(p);t.textureType=p.Contains("Normal_OpenGL")?UnityEditor.TextureImporterType.NormalMap:UnityEditor.TextureImporterType.Default;t.sRGBTexture=p.Contains("BaseColor");t.wrapMode=TextureWrapMode.Repeat;t.wrapModeU=TextureWrapMode.Repeat;t.wrapModeV=TextureWrapMode.Repeat;t.mipmapEnabled=true;t.filterMode=FilterMode.Trilinear;t.anisoLevel=8;t.maxTextureSize=2048;t.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed;t.SaveAndReimport();}
return "Imported world textures";

