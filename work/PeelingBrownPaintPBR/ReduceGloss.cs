string p="Assets/Maps/Buildings/Textures/PeelingBrownPaint_01/M_PeelingBrownPaint_World.mat";
var m=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p);
var t=(Texture2D)m.GetTexture("_Brick_Metallic");
float before=m.GetFloat("_Brick_Smoothness_Mult");m.SetFloat("_Brick_Smoothness_Mult",.2f);UnityEditor.EditorUtility.SetDirty(m);UnityEditor.AssetDatabase.SaveAssetIfDirty(m);
return new{metallicMap=UnityEditor.AssetDatabase.GetAssetPath(t),smoothnessBefore=before,smoothnessAfter=m.GetFloat("_Brick_Smoothness_Mult"),shaderErrors=UnityEditor.ShaderUtil.ShaderHasError(m.shader)};
