var mat=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Maps/Buildings/Textures/WeatheredCheckerFloor_01/M_CheckerFloor_World.mat");
var maps=new System.Collections.Generic.List<object>();foreach(var n in new[]{"_Brick_BaseColor","_Brick_Normal","_Brick_AO","_Brick_Roughness","_Brick_Metallic"}){var t=mat.GetTexture(n);if(!t)throw new Exception(n);maps.Add(new{slot=n,path=UnityEditor.AssetDatabase.GetAssetPath(t),width=t.width,height=t.height});}
return new{shaderErrors=UnityEditor.ShaderUtil.ShaderHasError(mat.shader),maps=maps,tile=mat.GetFloat("_Brick_Tile"),scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path};


