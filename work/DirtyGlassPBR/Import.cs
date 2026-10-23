string f="Assets/Maps/Buildings/Textures/DirtyHazyGlass_01",mp=f+"/M_Glass_DirtyHazy.mat";
UnityEditor.AssetDatabase.Refresh();
foreach(var file in System.IO.Directory.GetFiles(f,"*.png")){string p=file.Replace('\\','/');var t=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(p);t.textureType=p.Contains("Normal_OpenGL")?UnityEditor.TextureImporterType.NormalMap:UnityEditor.TextureImporterType.Default;t.sRGBTexture=p.Contains("BaseColor");t.wrapMode=TextureWrapMode.Repeat;t.mipmapEnabled=true;t.filterMode=FilterMode.Trilinear;t.anisoLevel=8;t.maxTextureSize=1024;t.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed;t.alphaSource=UnityEditor.TextureImporterAlphaSource.FromInput;t.alphaIsTransparency=false;t.SaveAndReimport();}
if(System.IO.File.Exists(mp))throw new Exception("Refuse overwrite");
var source=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Maps/Interior/Materials/M_Glass_Clear.mat");var m=new Material(source);m.name="M_Glass_DirtyHazy";
Texture Tex(string n)=>UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(f+"/DHG01_"+n+".png");
m.SetTexture("_BaseMap",Tex("BaseColor"));m.SetColor("_BaseColor",Color.white);m.SetTexture("_BumpMap",Tex("Normal_OpenGL"));m.SetFloat("_BumpScale",.15f);m.EnableKeyword("_NORMALMAP");m.SetTexture("_MetallicGlossMap",Tex("MetallicSmoothness"));m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",1);m.SetFloat("_SmoothnessTextureChannel",0);m.SetFloat("_GlossMapScale",1);
UnityEditor.AssetDatabase.CreateAsset(m,mp);
return new{material=mp,shader=m.shader.name,transparent=m.GetFloat("_Surface"),queue=m.renderQueue,zwrite=m.GetFloat("_ZWrite"),shaderErrors=UnityEditor.ShaderUtil.ShaderHasError(m.shader)};
