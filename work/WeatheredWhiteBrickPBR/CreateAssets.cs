string src="Assets/Maps/Buildings/Textures/WeatheredRedBrick_01";
string dst="Assets/Maps/Buildings/Textures/WeatheredWhiteBrick_01";
string sg=src+"/SG_Brick_World.shadergraph",dg=dst+"/SG_Brick_World_White.shadergraph";
if(UnityEditor.AssetDatabase.IsValidFolder(dst)||System.IO.Directory.Exists(dst))throw new Exception("Refuse existing folder overwrite");
var sourceShader=UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(sg);
var sourceMaterial=Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m=>m.shader==sourceShader&&UnityEditor.AssetDatabase.GetAssetPath(m)==sg);
bool temporary=false;if(!sourceMaterial){sourceMaterial=new Material(sourceShader);temporary=true;}
UnityEditor.AssetDatabase.CreateFolder("Assets/Maps/Buildings/Textures","WeatheredWhiteBrick_01");
var textureMap=new System.Collections.Generic.Dictionary<string,string>();
var guidMap=new System.Collections.Generic.Dictionary<string,string>();
foreach(string file in System.IO.Directory.GetFiles(src,"*.png")){
 string old=file.Replace('\\','/');string neo=dst+"/"+System.IO.Path.GetFileName(old).Replace("WBR01_","WWB01_");
 if(!UnityEditor.AssetDatabase.CopyAsset(old,neo))throw new Exception("Copy failed: "+old);
 textureMap[old]=neo;guidMap[UnityEditor.AssetDatabase.AssetPathToGUID(old)]=UnityEditor.AssetDatabase.AssetPathToGUID(neo);
}
System.IO.File.WriteAllBytes(dst+"/WWB01_BaseColor.png",System.IO.File.ReadAllBytes("work/WeatheredWhiteBrickPBR/WhiteBrick_BaseColor.png"));
UnityEditor.AssetDatabase.ImportAsset(dst+"/WWB01_BaseColor.png",UnityEditor.ImportAssetOptions.ForceSynchronousImport|UnityEditor.ImportAssetOptions.ForceUpdate);
if(!UnityEditor.AssetDatabase.CopyAsset(sg,dg))throw new Exception("Graph copy failed");
string originalGraph=System.IO.File.ReadAllText(sg);string graph=System.IO.File.ReadAllText(dg);
foreach(var pair in guidMap)graph=graph.Replace(pair.Key,pair.Value);
System.IO.File.WriteAllText(dg,graph,new System.Text.UTF8Encoding(false));
UnityEditor.AssetDatabase.ImportAsset(dg,UnityEditor.ImportAssetOptions.ForceSynchronousImport|UnityEditor.ImportAssetOptions.ForceUpdate);
var shader=UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(dg);if(!shader)throw new Exception("Copied graph shader not imported");
var mat=new Material(sourceMaterial);mat.name="M_WhiteBrick_World";mat.hideFlags=HideFlags.None;mat.shader=shader;
for(int i=0;i<sourceShader.GetPropertyCount();i++){
 string name=sourceShader.GetPropertyName(i);if(sourceShader.GetPropertyType(i)!=UnityEngine.Rendering.ShaderPropertyType.Texture)continue;
 string p=UnityEditor.AssetDatabase.GetAssetPath(sourceMaterial.GetTexture(name));
 if(textureMap.TryGetValue(p,out string replacement))mat.SetTexture(name,UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(replacement));
}
UnityEditor.AssetDatabase.CreateAsset(mat,dst+"/M_WhiteBrick_World.mat");
var mismatches=new System.Collections.Generic.List<string>();
for(int i=0;i<sourceShader.GetPropertyCount();i++){
 string name=sourceShader.GetPropertyName(i);var type=sourceShader.GetPropertyType(i);
 if(!mat.HasProperty(name)){mismatches.Add("Missing "+name);continue;}
 if(type==UnityEngine.Rendering.ShaderPropertyType.Float||type==UnityEngine.Rendering.ShaderPropertyType.Range){if(mat.GetFloat(name)!=sourceMaterial.GetFloat(name))mismatches.Add(name);}
 else if(type==UnityEngine.Rendering.ShaderPropertyType.Color||type==UnityEngine.Rendering.ShaderPropertyType.Vector){if(mat.GetVector(name)!=sourceMaterial.GetVector(name))mismatches.Add(name);}
 else if(type==UnityEngine.Rendering.ShaderPropertyType.Texture){
  string old=UnityEditor.AssetDatabase.GetAssetPath(sourceMaterial.GetTexture(name));string actual=UnityEditor.AssetDatabase.GetAssetPath(mat.GetTexture(name));string expected=textureMap.TryGetValue(old,out string mapped)?mapped:old;
  if(actual!=expected)mismatches.Add("Texture "+name);
  if(mat.GetTextureScale(name)!=sourceMaterial.GetTextureScale(name)||mat.GetTextureOffset(name)!=sourceMaterial.GetTextureOffset(name))mismatches.Add("Texture transform "+name);
 }
}
string reverse=graph;foreach(var p in guidMap)reverse=reverse.Replace(p.Value,p.Key);
var report=new{material=dst+"/M_WhiteBrick_World.mat",graph=dg,sourceGraphSettingsPreserved=reverse==originalGraph,propertyMismatches=mismatches.ToArray(),tile=mat.GetFloat("_Brick_Tile"),blend=mat.GetFloat("_Brick_Blend"),smoothnessMultiplier=mat.GetFloat("_Brick_Smoothness_Mult"),textures=textureMap.Count,shaderHasErrors=UnityEditor.ShaderUtil.ShaderHasError(shader),shaderMessages=UnityEditor.ShaderUtil.GetShaderMessages(shader).Select(m=>new {m.message,severity=m.severity.ToString()}).ToArray()};
System.IO.File.WriteAllText("work/WeatheredWhiteBrickPBR/shader-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
if(temporary)UnityEngine.Object.DestroyImmediate(sourceMaterial);
return report;
