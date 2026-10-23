string p="Assets/Maps/Buildings/Textures/PeelingGreenStairwellWall_01/SG_GreenStairwellWall_World.shadergraph";
string text=System.IO.File.ReadAllText(p),old="Out = float3(World.x, frac(World.y / 3.0) * 3.0, World.z);",neo="Out = float3(World.x, frac((World.y - 1.875 + 0.001) / 3.0) * 3.0, World.z);";
if(!text.Contains(old))throw new Exception("Unexpected graph body");
System.IO.File.Copy(p,"work/GreenWallWorld/BeforeHeightFix.shadergraph",true);
System.IO.File.WriteAllText(p,text.Replace(old,neo),new System.Text.UTF8Encoding(false));
UnityEditor.AssetDatabase.ImportAsset(p,UnityEditor.ImportAssetOptions.ForceSynchronousImport|UnityEditor.ImportAssetOptions.ForceUpdate);
var s=UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(p);
return new{shaderErrors=UnityEditor.ShaderUtil.ShaderHasError(s),messages=UnityEditor.ShaderUtil.GetShaderMessages(s).Select(m=>m.message).ToArray(),baseHeight=1.875,storey=3,boundary=3.374};
