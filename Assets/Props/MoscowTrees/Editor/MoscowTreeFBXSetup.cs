#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turn 96 included FBX model assets into 32 editable prefab assets with LODGroup,
/// tree trunk collider, bark/cutout leaf materials and Inspector-controllable wind.
/// Menu: Tools > Moscow Trees > Build All 32 FBX Wind Prefabs
/// </summary>
public static class MoscowTreeFBXSetup
{
    private const string BasePath = "Assets/MoscowTrees";
    private const string ModelPath = BasePath + "/Models_FBX";
    private const string TexturePath = BasePath + "/Textures";
    private const string MaterialPath = BasePath + "/Materials";
    private const string PrefabPath = BasePath + "/Prefabs";
    private static readonly float[] LodThresholds = { 0.55f, 0.20f, 0.035f };

    [MenuItem("Tools/Moscow Trees/Build All 32 FBX Wind Prefabs")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(ModelPath + "/LOD0"))
        {
            ShowDialog("Moscow Trees", "Не найдены FBX. Распакуй папку Assets/MoscowTrees в корень Unity-проекта.", "OK");
            return;
        }
        EnsureFolder(MaterialPath);
        EnsureFolder(PrefabPath);
        var assets = AssetDatabase.FindAssets("t:Model", new[] { ModelPath + "/LOD0" });
        int success = 0, errors = 0;
        foreach (string guid in assets)
        {
            string path0 = AssetDatabase.GUIDToAssetPath(guid);
            if (!path0.EndsWith("_LOD0.fbx", StringComparison.OrdinalIgnoreCase)) continue;
            string name = Path.GetFileNameWithoutExtension(path0).Replace("_LOD0", "");
            string species = name.Split('_')[0];
            string path1 = path0.Replace("/LOD0/", "/LOD1/").Replace("_LOD0.fbx", "_LOD1.fbx");
            string path2 = path0.Replace("/LOD0/", "/LOD2/").Replace("_LOD0.fbx", "_LOD2.fbx");
            GameObject[] models = {
                AssetDatabase.LoadAssetAtPath<GameObject>(path0),
                AssetDatabase.LoadAssetAtPath<GameObject>(path1),
                AssetDatabase.LoadAssetAtPath<GameObject>(path2)
            };
            if (models[0] == null || models[1] == null || models[2] == null)
            {
                Debug.LogError("MoscowTrees: missing FBX LOD: " + name);
                errors++; continue;
            }
            var bark = GetMaterial(species, false);
            var leaf = GetMaterial(species, true);
            if (bark == null || leaf == null) { errors++; continue; }
            GameObject root = new GameObject(name);
            try
            {
                var lods = new LOD[3];
                for (int lod = 0; lod < 3; lod++)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(models[lod]);
                    if (instance == null) instance = UnityEngine.Object.Instantiate(models[lod]);
                    // Store renderer/LOD links locally; imported FBX object IDs can change on reimport.
                    if (PrefabUtility.IsPartOfPrefabInstance(instance))
                        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    instance.transform.SetParent(root.transform, false);
                    instance.name = "LOD" + lod;
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer r in renderers)
                    {
                        bool isLeaf = r.name.IndexOf("Leaves", StringComparison.OrdinalIgnoreCase) >= 0
                            || r.name.IndexOf("Foliage", StringComparison.OrdinalIgnoreCase) >= 0;
                        var materials = new Material[Math.Max(1, r.sharedMaterials.Length)];
                        for (int i = 0; i < materials.Length; i++) materials[i] = isLeaf ? leaf : bark;
                        r.sharedMaterials = materials;
                        r.shadowCastingMode = isLeaf ? UnityEngine.Rendering.ShadowCastingMode.TwoSided : UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                    lods[lod] = new LOD(LodThresholds[lod], renderers);
                }
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                var wind = root.AddComponent<MoscowTreeWind>();
                wind.CacheRig();
                var collider = root.AddComponent<CapsuleCollider>();
                Bounds b = new Bounds(Vector3.zero, Vector3.one);
                bool first = true;
                foreach (Renderer renderer in lods[0].renderers)
                {
                    if (first) { b = renderer.bounds; first = false; }
                    else b.Encapsulate(renderer.bounds);
                }
                float height = first ? 8f : b.size.y;
                collider.direction = 1; // world/up in prefab rest pose
                collider.height = Mathf.Max(1.6f, height * 0.38f);
                collider.radius = Mathf.Clamp(height * 0.022f, 0.12f, 0.65f);
                collider.center = new Vector3(0, collider.height * 0.5f, 0);
                FitTrunkCollider(root);
                string prefabName = PrefabPath + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabName);
                success++;
            }
            catch (Exception e)
            {
                errors++;
                Debug.LogError("MoscowTrees: failed to build " + name + " : " + e);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Moscow Trees: created {success} prefabs / errors {errors}. Location: {PrefabPath}");
        ShowDialog("Moscow Trees", $"Собрано префабов: {success}. Ошибок: {errors}.\nПапка: {PrefabPath}", "OK");
    }

    private static void ShowDialog(string title, string message, string ok)
    {
        if (!Application.isBatchMode) EditorUtility.DisplayDialog(title, message, ok);
    }

    public static void FitTrunkCollider(GameObject root)
    {
        var collider = root.GetComponent<CapsuleCollider>();
        if (collider == null) return;
        var lod0 = root.transform.Find("LOD0");
        if (lod0 == null) return;
        foreach (var filter in lod0.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.name.StartsWith("Trunk_Mesh", StringComparison.OrdinalIgnoreCase) || filter.sharedMesh == null) continue;
            var vertices = filter.sharedMesh.vertices;
            var points = new List<Vector3>(vertices.Length);
            float bottom = float.MaxValue, top = float.MinValue;
            foreach (var vertex in vertices)
            {
                var p = root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                points.Add(p); bottom = Mathf.Min(bottom, p.y); top = Mathf.Max(top, p.y);
            }
            var basePoints = points.FindAll(p => p.y <= bottom + (top-bottom)*0.025f);
            if (basePoints.Count < 3) return;
            float minX=basePoints[0].x, maxX=minX, minZ=basePoints[0].z, maxZ=minZ;
            foreach (var p in basePoints) { minX=Mathf.Min(minX,p.x); maxX=Mathf.Max(maxX,p.x); minZ=Mathf.Min(minZ,p.z); maxZ=Mathf.Max(maxZ,p.z); }
            var center = new Vector3((minX+maxX)*.5f, 0, (minZ+maxZ)*.5f);
            float radius=0;
            foreach (var p in basePoints) radius=Mathf.Max(radius, new Vector2(p.x-center.x,p.z-center.z).magnitude);
            collider.radius = Mathf.Max(.05f, radius);
            collider.height = Mathf.Max(collider.height, 2f*collider.radius);
            collider.center = new Vector3(center.x,bottom+collider.height*.5f,center.z);
            return;
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent)) AssetDatabase.CreateFolder(parent, name);
    }

    private static Material GetMaterial(string species, bool isLeaf)
    {
        string materialName = species + (isLeaf ? "_Leaves" : "_Bark");
        string materialAsset = MaterialPath + "/" + materialName + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialAsset);
        string textureAsset = TexturePath + "/" + species + (isLeaf ? "_Leaves.png" : "_Bark.png");
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAsset);
        if (tex == null)
        {
            Debug.LogError("Missing tree texture: " + textureAsset);
            return null;
        }
        string[] shaders = {
            "Universal Render Pipeline/Lit",
            "HDRP/Lit",
            "Standard"
        };
        Shader shader = null;
        // Prefer the pipeline that is actually active in this project.
        bool srp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        string srpType = srp ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().FullName : "";
        if (srp && srpType.IndexOf("HDRender", StringComparison.OrdinalIgnoreCase) >= 0)
            shader = Shader.Find("HDRP/Lit");
        else if (srp)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        else
            shader = Shader.Find(isLeaf ? "MoscowTrees/Leaves Cutout Builtin" : "Standard");
        if (shader == null)
        {
            foreach (string s in shaders) { shader = Shader.Find(s); if (shader != null) break; }
        }
        if (shader == null) { Debug.LogError("No supported surface shader found"); return null; }
        bool create = mat == null;
        if (create) mat = new Material(shader) { name = materialName };
        else mat.shader = shader;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", tex);
        if (isLeaf)
        {
            mat.EnableKeyword("_ALPHATEST_ON");
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
            if (mat.HasProperty("_AlphaCutoffEnable")) mat.SetFloat("_AlphaCutoffEnable", 1f);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.45f);
            if (mat.HasProperty("_AlphaCutoff")) mat.SetFloat("_AlphaCutoff", 0.45f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", 1f);
            if (mat.HasProperty("_CullMode")) mat.SetFloat("_CullMode", 0f);
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 1f); // Built-in Standard cutout
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f); // Opaque + alpha clip
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.doubleSidedGI = true;
        }
        if (create) AssetDatabase.CreateAsset(mat, materialAsset);
        else EditorUtility.SetDirty(mat);
        return mat;
    }
}
#endif
