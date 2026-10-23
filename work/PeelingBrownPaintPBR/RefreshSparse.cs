string f="Assets/Maps/Buildings/Textures/PeelingBrownPaint_01";
foreach(var p in System.IO.Directory.GetFiles(f,"*.png"))UnityEditor.AssetDatabase.ImportAsset(p.Replace('\\','/'),UnityEditor.ImportAssetOptions.ForceUpdate);
var m=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(f+"/M_PeelingBrownPaint_World.mat");
return new{material=m.name,tile=m.GetFloat("_Brick_Tile"),normal=UnityEditor.AssetDatabase.GetAssetPath(m.GetTexture("_Brick_Normal")),shaderErrors=UnityEditor.ShaderUtil.ShaderHasError(m.shader)};
