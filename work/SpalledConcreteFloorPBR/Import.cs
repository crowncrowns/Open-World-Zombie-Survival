string folder="Assets/Maps/Buildings/Textures/SpalledConcreteFloor_01";
UnityEditor.AssetDatabase.Refresh();
foreach(string file in System.IO.Directory.GetFiles(folder,"*.png")){
 string path=file.Replace('\\','/');var ti=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
 ti.textureType=path.Contains("Normal_OpenGL")?UnityEditor.TextureImporterType.NormalMap:UnityEditor.TextureImporterType.Default;
 ti.sRGBTexture=path.Contains("BaseColor");ti.wrapMode=UnityEngine.TextureWrapMode.Repeat;ti.filterMode=UnityEngine.FilterMode.Trilinear;ti.anisoLevel=8;ti.mipmapEnabled=true;ti.maxTextureSize=2048;ti.npotScale=UnityEditor.TextureImporterNPOTScale.None;
 ti.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed;
 if(path.Contains("MetallicSmoothness")){ti.alphaSource=UnityEditor.TextureImporterAlphaSource.FromInput;ti.alphaIsTransparency=false;}
 ti.SaveAndReimport();
}
string mp=folder+"/M_SpalledConcreteFloor_01_URP.mat";if(System.IO.File.Exists(mp))throw new Exception("Refuse material overwrite");
var mat=new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit"));mat.name="M_SpalledConcreteFloor_01_URP";
UnityEngine.Texture Tex(string suffix)=>UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture>(folder+"/SCF01_"+suffix+".png");
mat.SetTexture("_BaseMap",Tex("BaseColor"));mat.SetColor("_BaseColor",UnityEngine.Color.white);
mat.SetTexture("_BumpMap",Tex("Normal_OpenGL"));mat.SetFloat("_BumpScale",1f);mat.EnableKeyword("_NORMALMAP");
mat.SetTexture("_MetallicGlossMap",Tex("MetallicSmoothness"));mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",1);mat.SetFloat("_GlossMapScale",1);mat.SetFloat("_SmoothnessTextureChannel",0);mat.EnableKeyword("_METALLICSPECGLOSSMAP");
mat.SetTexture("_OcclusionMap",Tex("AO"));mat.SetFloat("_OcclusionStrength",.65f);mat.EnableKeyword("_OCCLUSIONMAP");
mat.SetTexture("_ParallaxMap",Tex("Height16"));mat.SetFloat("_Parallax",.005f);mat.EnableKeyword("_PARALLAXMAP");
UnityEditor.AssetDatabase.CreateAsset(mat,mp);
return new {material=mp,shader=mat.shader.name,maps=System.IO.Directory.GetFiles(folder,"*.png").Length,sceneUnchanged=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path};





