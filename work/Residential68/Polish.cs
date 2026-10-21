var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Residential68_Test/Residential_Building_Test.unity");var root=scene.GetRootGameObjects().Single();string folder="Assets/Residential68_Test";
var brick=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(folder+"/Approx_RedBrick.mat");brick.SetTextureScale("_BaseMap",UnityEngine.Vector2.one);brick.SetFloat("_Smoothness",.15f);UnityEditor.AssetDatabase.SaveAssetIfDirty(brick);
var cached=new System.Collections.Generic.Dictionary<string,UnityEngine.Mesh>();int count=0;
foreach(var mf in root.GetComponentsInChildren<UnityEngine.MeshFilter>().Where(m=>m.name.StartsWith("Wall_")&&m.sharedMesh)){
 var prefab=UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(mf.gameObject);if(!prefab)continue;
 string key=UnityEditor.AssetDatabase.GetAssetPath(mf.sharedMesh)+"_"+mf.name;
 if(!cached.TryGetValue(key,out var copy)){
  copy=UnityEngine.Object.Instantiate(mf.sharedMesh);copy.name="ApproxUV_"+mf.name;
  var verts=copy.vertices;var normals=copy.normals;var uv=new UnityEngine.Vector2[verts.Length];
  for(int i=0;i<verts.Length;i++){var p=prefab.transform.InverseTransformPoint(mf.transform.TransformPoint(verts[i]));var n=prefab.transform.InverseTransformDirection(mf.transform.TransformDirection(normals[i]));uv[i]=new UnityEngine.Vector2((Mathf.Abs(n.x)>Mathf.Abs(n.z)?p.z:p.x)/1.04f,p.y/.52f);}
  copy.uv=uv;UnityEditor.AssetDatabase.CreateAsset(copy,folder+"/ApproxUV_"+(count++)+".asset");cached[key]=copy;
 }
 mf.sharedMesh=copy;
}
var roof=root.GetComponentsInChildren<UnityEngine.MeshFilter>().Single(m=>m.name.StartsWith("Pitched roof"));var mesh=roof.sharedMesh;var src=mesh.vertices;var tri=mesh.triangles;var vv=tri.Select(i=>src[i]).ToArray();mesh.Clear();mesh.vertices=vv;mesh.triangles=Enumerable.Range(0,vv.Length).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();roof.GetComponent<UnityEngine.MeshCollider>().sharedMesh=mesh;UnityEditor.AssetDatabase.SaveAssetIfDirty(mesh);
var rm=roof.GetComponent<UnityEngine.Renderer>().sharedMaterial;rm.SetFloat("_Smoothness",.08f);UnityEditor.AssetDatabase.SaveAssetIfDirty(rm);
// Keep the existing façade meshes and sources; only add pane colliders to this scene.
int panes=0;foreach(var mf in root.GetComponentsInChildren<UnityEngine.MeshFilter>().Where(m=>m.name.Contains("Window_Glass")&&m.sharedMesh)){if(!mf.GetComponent<UnityEngine.Collider>()){mf.gameObject.AddComponent<UnityEngine.MeshCollider>().sharedMesh=mf.sharedMesh;panes++;}}
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new {newUVVariants=count,paneColliders=panes};

