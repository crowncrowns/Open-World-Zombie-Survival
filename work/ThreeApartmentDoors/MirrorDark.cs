string p="Assets/Maps/Buildings/Textures/ApartmentDoor_DarkUpholstery_01/M_ApartmentDoor_DarkUpholstery_01_URP.mat";
var m=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p);if(!m)throw new Exception("Material missing");
System.IO.File.Copy(p,"work/ThreeApartmentDoors/DarkMaterialBeforeMirror.mat",true);
foreach(var n in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap","_ParallaxMap"}){m.SetTextureScale(n,new Vector2(-1,1));m.SetTextureOffset(n,new Vector2(1,0));}
UnityEditor.EditorUtility.SetDirty(m);UnityEditor.AssetDatabase.SaveAssetIfDirty(m);
return new{material=p,scale=m.GetTextureScale("_BaseMap").ToString(),offset=m.GetTextureOffset("_BaseMap").ToString(),shaderErrors=UnityEditor.ShaderUtil.ShaderHasError(m.shader)};
