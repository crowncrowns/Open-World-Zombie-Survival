string folder="Assets/Maps/Buildings/Textures/SpalledCorniceBlocks_01";
var m=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(folder+"/M_SpalledCorniceBlocks_01_URP.mat");
var maps=new System.Collections.Generic.List<object>();
foreach(var p in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap","_ParallaxMap"}){var t=m.GetTexture(p);if(t==null)throw new Exception(p+" missing");var ti=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(t));maps.Add(new {slot=p,width=t.width,height=t.height,type=ti.textureType.ToString(),u=ti.wrapModeU.ToString(),v=ti.wrapModeV.ToString()});}
return new {shader=m.shader.name,maps=maps};
