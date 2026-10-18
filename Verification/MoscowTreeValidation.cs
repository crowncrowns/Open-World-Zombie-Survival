#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class MoscowTreeValidation
{
    [Serializable] public class Metric { public string name; public int meshes; public int triangles; public float height; }
    [Serializable] public class Metrics { public Metric[] items; }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var paths = Directory.GetFiles("Assets/MoscowTrees/Models_FBX", "*.fbx", SearchOption.AllDirectories);
            Check(paths.Length == 96, "Expected 96 FBX");
            int meshes=0;
            var expected=JsonUtility.FromJson<Metrics>(File.ReadAllText("expected_metrics.json")).items;
            foreach (var path in paths)
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/'));
                Check(model != null, "Missing model "+path);
                var filters=model.GetComponentsInChildren<MeshFilter>(true);
                Check(filters.Length > 0 && filters.All(f=>f.sharedMesh != null && f.sharedMesh.vertexCount > 0), "Empty meshes "+path);
                Check(model.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Wind_Trunk"), "Missing trunk pivot "+path);
                meshes+=filters.Length;
                var metric=expected.First(m=>m.name==Path.GetFileNameWithoutExtension(path));
                Check(filters.Length==metric.meshes && filters.Sum(f=>f.sharedMesh.triangles.Length/3)==metric.triangles,"Geometry count "+path);
                var instance=UnityEngine.Object.Instantiate(model);
                try {
                    var rs=instance.GetComponentsInChildren<Renderer>(true); var bounds=rs[0].bounds;
                    foreach(var r in rs) bounds.Encapsulate(r.bounds);
                    Check(Mathf.Abs(bounds.size.y-metric.height)<0.01f,"Scale/up axis "+path+" height="+bounds.size.y+" expected="+metric.height);
                } finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            MoscowTreeFBXSetup.Build();
            var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/MoscowTrees/Prefabs"});
            Check(prefabs.Length==32,"Expected 32 prefabs");
            foreach(var guid in prefabs)
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var obj=UnityEngine.Object.Instantiate(asset);
                try
                {
                    var group=obj.GetComponent<LODGroup>();
                    Check(group!=null && group.lodCount==3,"LODGroup "+asset.name);
                    foreach(var lod in group.GetLODs())
                        Check(lod.renderers.Length>0 && lod.renderers.All(r=>r!=null),"Empty LOD "+asset.name);
                    Check(obj.GetComponent<CapsuleCollider>()!=null,"Collider "+asset.name);
                    var wind=obj.GetComponent<MoscowTreeWind>();
                    Check(wind!=null,"Wind "+asset.name);
                    foreach(var renderer in obj.GetComponentsInChildren<Renderer>(true))
                        foreach(var mat in renderer.sharedMaterials)
                            Check(mat!=null && mat.mainTexture!=null,"Material/texture "+renderer.name);
                    var pivot=obj.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Wind_Trunk");
                    wind.CacheRig(); var rest=pivot.localRotation;
                    var so=new SerializedObject(wind);
                    so.FindProperty("animateInEditMode").boolValue=true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    wind.SendMessage("LateUpdate");
                    Check(Quaternion.Angle(rest,pivot.localRotation)>0.00001f,"Wind did not move "+asset.name);
                    so.Update(); so.FindProperty("enableWind").boolValue=false; so.ApplyModifiedPropertiesWithoutUndo();
                    wind.SendMessage("LateUpdate");
                    Check(Quaternion.Angle(rest,pivot.localRotation)<0.001f,"Wind reset "+asset.name);
                }
                finally { UnityEngine.Object.DestroyImmediate(obj); }
            }
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            Check(pipeline!=null && pipeline.GetType().Name.Contains("Universal"), "Expected project URP settings");
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/MoscowTrees/Materials"}))
            {
                var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Check(mat.shader.name=="Universal Render Pipeline/Lit" && !ShaderUtil.ShaderHasError(mat.shader),"URP shader "+mat.name);
                if(mat.name.EndsWith("_Leaves")) Check(mat.GetFloat("_Cull")==0 && mat.GetFloat("_AlphaClip")==1,"Leaf alpha/cull "+mat.name);
            }
            MoscowTreePreview.Render();
            var report="Unity "+Application.unityVersion+", URP "+pipeline.name+": 96 FBX, "+meshes+" mesh renderers, 32 prefabs, LODs, colliders, textures, URP shaders and wind motion/reset PASS. GPU preview rendered.";
            File.WriteAllText("validation.txt",report); Debug.Log(report); EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); File.WriteAllText("validation.txt",e.ToString()); EditorApplication.Exit(1); }
    }
}
#endif
