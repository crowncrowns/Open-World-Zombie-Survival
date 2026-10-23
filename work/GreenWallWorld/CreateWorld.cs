string f="Assets/Maps/Buildings/Textures/PeelingGreenStairwellWall_01",src="Assets/Maps/Buildings/Textures/WeatheredRedBrick_01/SG_Brick_World.shadergraph",dst=f+"/SG_GreenStairwellWall_World.shadergraph",mp=f+"/M_GreenStairwellWall_World.mat";
if(System.IO.File.Exists(dst)||System.IO.File.Exists(mp))throw new Exception("Refuse overwrite");
if(!UnityEditor.AssetDatabase.CopyAsset(src,dst))throw new Exception("Copy failed");
System.IO.File.WriteAllText(dst,System.IO.File.ReadAllText("work/GreenWallWorld/WorldGraph.txt"),new System.Text.UTF8Encoding(false));
UnityEditor.AssetDatabase.ImportAsset(dst,UnityEditor.ImportAssetOptions.ForceSynchronousImport|UnityEditor.ImportAssetOptions.ForceUpdate);
var sh=UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(dst);if(!sh)throw new Exception("Missing shader");
var mat=new Material(sh);mat.name="M_GreenStairwellWall_World";
foreach(var kv in new System.Collections.Generic.Dictionary<string,string>{{"_Brick_BaseColor","BaseColor"},{"_Brick_Normal","Normal_OpenGL"},{"_Brick_AO","AO"},{"_Brick_Roughness","Roughness"},{"_Brick_Metallic","Metallic"}})mat.SetTexture(kv.Key,UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(f+"/PGW01_"+kv.Value+".png"));
mat.SetFloat("_Brick_Tile",(1f/3f));mat.SetFloat("_Brick_Blend",1f);mat.SetFloat("_Brick_Smoothness_Mult",1f);
UnityEditor.AssetDatabase.CreateAsset(mat,mp);
return new{material=mp,shaderErrors=UnityEditor.ShaderUtil.ShaderHasError(sh),messages=UnityEditor.ShaderUtil.GetShaderMessages(sh).Select(m=>m.message).ToArray(),tile=mat.GetFloat("_Brick_Tile")};


