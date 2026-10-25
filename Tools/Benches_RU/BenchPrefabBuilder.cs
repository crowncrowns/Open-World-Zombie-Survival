using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class BenchPrefabBuilder
{
    private const string Root = "Assets/Environment/Props/Benches_RU";
    private const string ModelDir = Root + "/Models";
    private const string TextureDir = Root + "/Textures";
    private const string MaterialDir = Root + "/Materials";
    private const string PrefabDir = Root + "/Prefabs";
    private const string SceneDir = Root + "/Scenes";
    private const string DemoScenePath = SceneDir + "/Demo_Benches_RU.unity";
    private const string WorkValidationPath = "../../Verification";

    [Serializable]
    private class BenchResult
    {
        public string name;
        public int lod0Triangles;
        public int lod1Triangles;
        public int lod2Triangles;
        public float lod1Ratio;
        public float lod2Ratio;
        public float minGroundY;
        public int rendererCount;
        public int materialSlotCount;
        public int missingMaterialSlots;
        public int colliderCount;
        public float[] rootScale;
        public string[] materialNames;
        public bool hasLightmapUv;
        public bool hasLodGroup;
    }

    [Serializable]
    private class TextureResult
    {
        public string file;
        public int width;
        public int height;
        public bool imported;
    }

    [Serializable]
    private class ValidationReport
    {
        public string result;
        public string unityVersion;
        public string renderPipeline;
        public int fbxCount;
        public int prefabCount;
        public int textureCount;
        public int materialCount;
        public bool sceneSaved;
        public bool scenePreviewRendered;
        public List<BenchResult> benches = new List<BenchResult>();
        public List<TextureResult> textures = new List<TextureResult>();
        public List<string> errors = new List<string>();
        public List<string> notes = new List<string>();
    }

    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    private static readonly float[] BenchLengths = { 1.78f, 1.68f, 1.72f, 1.82f, 1.52f };
    private static Shader SelectedShader;

    public static void BuildAndValidate()
    {
        RequireIsolatedProject();
        var report = new ValidationReport
        {
            unityVersion = Application.unityVersion,
            renderPipeline = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.GetType().Name
                : "Built-in Render Pipeline (no active SRP in GraphicsSettings)"
        };
        Directory.CreateDirectory(MaterialDir);
        Directory.CreateDirectory(PrefabDir);
        Directory.CreateDirectory(SceneDir);
        ConfigureTextureImporters(report);
        CreateMaterials(report);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var fbxPaths = Directory.GetFiles(ModelDir, "Bench_RU_*.fbx", SearchOption.TopDirectoryOnly)
            .Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        report.fbxCount = fbxPaths.Length;
        if (report.fbxCount != 15)
            report.errors.Add("Expected 15 FBX files; found " + report.fbxCount);

        foreach (var fbxPath in fbxPaths)
        {
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                report.errors.Add("Unity could not load FBX: " + fbxPath);
                continue;
            }
            var prefab = BuildPrefab(model, fbxPath, report);
            if (prefab != null)
            {
                var result = ValidatePrefab(prefab, Path.GetFileNameWithoutExtension(fbxPath), report);
                if (result != null)
                    report.benches.Add(result);
            }
        }

        report.prefabCount = Directory.GetFiles(PrefabDir, "Bench_RU_*.prefab", SearchOption.TopDirectoryOnly).Length;
        report.materialCount = Directory.GetFiles(MaterialDir, "*.mat", SearchOption.TopDirectoryOnly).Length;
        CreateDemoScene(report);
        report.materialCount = Directory.GetFiles(MaterialDir, "*.mat", SearchOption.TopDirectoryOnly).Length;
        report.sceneSaved = File.Exists(DemoScenePath);
        report.result = report.errors.Count == 0 && report.prefabCount == 15 && report.benches.Count == 15
            ? "PASS"
            : "FAIL";

        var validationDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Verification"));
        Directory.CreateDirectory(validationDir);
        File.WriteAllText(Path.Combine(validationDir, "UnityValidation.json"), JsonUtility.ToJson(report, true), Encoding.UTF8);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("BENCH_RU_VALIDATION_" + report.result + ": " + report.fbxCount + " FBX, " + report.prefabCount +
                  " prefabs, " + report.materialCount + " materials, " + report.textures.Count + " PBR textures; pipeline=" + report.renderPipeline);
        if (report.errors.Count > 0)
            throw new InvalidOperationException(string.Join("\n", report.errors));
    }

    public static void ColdReopenAudit()
    {
        RequireIsolatedProject();
        var report = new ValidationReport
        {
            unityVersion = Application.unityVersion,
            renderPipeline = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.GetType().Name
                : "Built-in Render Pipeline (no active SRP in GraphicsSettings)"
        };
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (!File.Exists(DemoScenePath))
            report.errors.Add("Saved demonstration scene is missing after cold reopen.");
        else
        {
            var scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            var placed = scene.GetRootGameObjects().Where(go => go.name.StartsWith("Bench_RU_", StringComparison.Ordinal)).ToArray();
            if (placed.Length != 15)
                report.errors.Add("Cold-opened demo scene contains " + placed.Length + " bench prefab roots, expected 15.");
            report.sceneSaved = scene.IsValid() && scene.isLoaded;
            foreach (var root in placed)
            {
                var prefabName = root.name;
                var result = ValidatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + prefabName + ".prefab"), prefabName, report);
                if (result != null) report.benches.Add(result);
            }
            var camera = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            report.scenePreviewRendered = camera != null && RenderPreview(camera, report);
            if (camera != null) RenderCloseups(camera,report);
            report.notes.Add("Offline single-camera previews temporarily disable GPU Resident Drawer in the isolated capture context. The separate RuntimeFrame_GPUResident.png uses real Play frames with InstancedDrawing enabled.");
        }
        report.fbxCount = Directory.GetFiles(ModelDir, "Bench_RU_*.fbx", SearchOption.TopDirectoryOnly).Length;
        report.prefabCount = Directory.GetFiles(PrefabDir, "Bench_RU_*.prefab", SearchOption.TopDirectoryOnly).Length;
        report.materialCount = Directory.GetFiles(MaterialDir, "*.mat", SearchOption.TopDirectoryOnly).Length;
        report.textureCount = Directory.GetFiles(TextureDir, "*.png", SearchOption.TopDirectoryOnly).Length;
        foreach (var path in Directory.GetFiles(TextureDir,"*.png",SearchOption.TopDirectoryOnly))
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\','/'));
            if (texture==null || texture.width!=4096 || texture.height!=4096)
                report.errors.Add("Cold-open texture is not 4096x4096: "+path);
            else report.textures.Add(new TextureResult {file=Path.GetFileName(path),width=texture.width,height=texture.height,imported=true});
            var importer=AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;
            if (importer==null || importer.GetDefaultPlatformTextureSettings().maxTextureSize!=4096)
                report.errors.Add("Cold-open texture default platform limit differs from 4096: "+path);
        }
        var selectedPipeline=QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
        report.renderPipeline=selectedPipeline != null ? selectedPipeline.GetType().Name+" / "+selectedPipeline.name : "Built-in";
        report.result = report.errors.Count == 0 && report.fbxCount == 15 && report.prefabCount == 15 && report.benches.Count == 15
            ? "PASS"
            : "FAIL";
        var validationDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Verification"));
        Directory.CreateDirectory(validationDir);
        File.WriteAllText(Path.Combine(validationDir, "ColdReopen.json"), JsonUtility.ToJson(report, true), Encoding.UTF8);
        Debug.Log("BENCH_RU_COLD_REOPEN_" + report.result + ": scene=" + report.sceneSaved + ", placed=" + report.benches.Count);
        if (report.errors.Count > 0)
            throw new InvalidOperationException(string.Join("\n", report.errors));
    }

    private static void ConfigureTextureImporters(ValidationReport report)
    {
        var paths = Directory.GetFiles(TextureDir, "*.png", SearchOption.TopDirectoryOnly)
            .Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        report.textureCount = paths.Length;
        foreach (var path in paths)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                var file = Path.GetFileName(path);
                var desiredType = file.Contains("_Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                var platform = importer.GetDefaultPlatformTextureSettings();
                var changed = importer.textureType != desiredType || importer.sRGBTexture != file.Contains("_BaseColor") ||
                    importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Trilinear ||
                    !importer.mipmapEnabled || platform.maxTextureSize != 4096 || importer.anisoLevel != 8 ||
                    platform.textureCompression != TextureImporterCompression.CompressedHQ;
                importer.textureType = file.Contains("_Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = file.Contains("_BaseColor");
                importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.mipmapEnabled = true;
                platform.maxTextureSize = 4096;
                platform.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SetPlatformTextureSettings(platform);
                importer.anisoLevel = 8;
                if (changed) importer.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                report.errors.Add("Texture did not import: " + path);
            else
            {
                report.textures.Add(new TextureResult { file = Path.GetFileName(path), width = tex.width, height = tex.height, imported = true });
                if (tex.width != 4096 || tex.height != 4096) report.errors.Add("Imported texture is not 4096x4096: " + path);
            }
        }
    }

    private static void CreateMaterials(ValidationReport report)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        var pipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
        var isUrp = pipeline is UniversalRenderPipelineAsset && shader != null;
        if (!isUrp)
            shader = Shader.Find("Standard");
        if (shader == null)
        {
            report.errors.Add("Neither URP/Lit nor Standard shader is available.");
            return;
        }
        SelectedShader = shader;
        report.renderPipeline = isUrp ? pipeline.GetType().Name + " / " + pipeline.name : "Built-in Render Pipeline / Standard shader";
        report.notes.Add("Material shader selected from the active GraphicsSettings render pipeline.");

        var specs = new[]
        {
            new MaterialSpec("M_WoodPaint_Green", "WoodGreen", Color.white, 0.0f, 0.34f),
            new MaterialSpec("M_WoodPaint_Blue", "WoodBlue", Color.white, 0.0f, 0.34f),
            new MaterialSpec("M_WoodPaint_Brown", "WoodBrown", Color.white, 0.0f, 0.30f),
            new MaterialSpec("M_WoodWeathered", "WoodRaw", Color.white, 0.0f, 0.26f),
            new MaterialSpec("M_WoodRotten", "WoodRaw", new Color(0.53f,0.48f,0.37f), 0.0f, 0.16f),
            new MaterialSpec("M_SteelPaint_Green", "SteelPaint", new Color(0.42f,0.56f,0.32f), 0.28f, 0.39f),
            new MaterialSpec("M_SteelPaint_Blue", "SteelPaint", new Color(0.45f,0.60f,0.92f), 0.27f, 0.39f),
            new MaterialSpec("M_SteelPaint_Gray", "SteelPaint", new Color(0.56f,0.58f,0.57f), 0.30f, 0.42f),
            new MaterialSpec("M_SteelBare", "SteelBare", Color.white, 0.92f, 0.56f),
            new MaterialSpec("M_SteelRust", "SteelRust", Color.white, 0.28f, 0.16f),
            new MaterialSpec("M_CastIron", "CastIron", new Color(0.83f,0.84f,0.80f), 0.74f, 0.26f),
            new MaterialSpec("M_Concrete_Porous", "Concrete", new Color(0.98f,0.98f,0.95f), 0.0f, 0.12f),
            new MaterialSpec("M_Moss", null, new Color(0.105f,0.17f,0.07f), 0.0f, 0.04f),
            new MaterialSpec("M_Dirt_Dark", null, new Color(0.115f,0.092f,0.065f), 0.0f, 0.04f),
            new MaterialSpec("M_Graffiti_FadedRed", null, new Color(0.46f,0.12f,0.09f), 0.0f, 0.18f),
            new MaterialSpec("M_Sticker_OldPaper", null, new Color(0.50f,0.43f,0.30f), 0.0f, 0.10f),
            new MaterialSpec("M_Bolt_Galvanized", "SteelBare", Color.white, 0.92f, 0.62f),
            new MaterialSpec("M_Crack_Shadow", null, new Color(0.045f,0.032f,0.023f), 0.0f, 0.03f),
        };
        foreach (var spec in specs)
        {
            var path = MaterialDir + "/" + spec.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = spec.name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
                material.shader = shader;
            SetColor(material, spec.color);
            SetFloat(material, "_Metallic", spec.metallic);
            SetFloat(material, "_Smoothness", spec.texture != null ? 1f : spec.smoothness);
            SetFloat(material, "_GlossMapScale", 1f);
            SetFloat(material, "_Glossiness", spec.smoothness);
            SetFloat(material, "_BumpScale", 0.2f);
            SetFloat(material, "_OcclusionStrength", 1.0f);
            SetTexture(material, "_MainTex", "_BaseMap", LoadTexture(spec.texture, "BaseColor"));
            SetTexture(material, "_BumpMap", "_BumpMap", LoadTexture(spec.texture, "Normal"));
            SetTexture(material, "_MetallicGlossMap", "_MetallicGlossMap", LoadTexture(spec.texture, "MetallicSmoothness"));
            SetTexture(material, "_OcclusionMap", "_OcclusionMap", LoadTexture(spec.texture, "AO"));
            if (spec.texture != null) { material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICGLOSSMAP"); }
            Materials[spec.name] = material;
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        if (Materials.Count != specs.Length)
            report.errors.Add("Some materials could not be created.");
    }

    private static Texture2D LoadTexture(string prefix, string suffix)
    {
        if (string.IsNullOrEmpty(prefix)) return null;
        var path = TextureDir + "/Tex_" + prefix + "_" + suffix + ".png";
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private static void SetTexture(Material material, string standardProperty, string urpProperty, Texture texture)
    {
        if (texture == null) return;
        if (material.HasProperty(standardProperty)) material.SetTexture(standardProperty, texture);
        if (material.HasProperty(urpProperty)) material.SetTexture(urpProperty, texture);
    }

    private sealed class MaterialSpec
    {
        public readonly string name;
        public readonly string texture;
        public readonly Color color;
        public readonly float metallic;
        public readonly float smoothness;
        public MaterialSpec(string name, string texture, Color color, float metallic, float smoothness)
        { this.name = name; this.texture = texture; this.color = color; this.metallic = metallic; this.smoothness = smoothness; }
    }

    private static GameObject BuildPrefab(GameObject model, string fbxPath, ValidationReport report)
    {
        var name = Path.GetFileNameWithoutExtension(fbxPath);
        var numberText = name.Substring("Bench_RU_".Length, 2);
        var kind = int.Parse(numberText);
        var root = new GameObject(name);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        var visual = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (visual == null)
        {
            report.errors.Add("Could not instantiate FBX: " + fbxPath);
            UnityEngine.Object.DestroyImmediate(root);
            return null;
        }
        visual.name = "Visual_LODMeshes";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        var renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
        var lodRenderers = new Dictionary<int, Renderer>();
        foreach (var renderer in renderers)
        {
            var label = renderer.name.ToUpperInvariant();
            var lod = label.Contains("LOD2") ? 2 : label.Contains("LOD1") ? 1 : label.Contains("LOD0") ? 0 : -1;
            if (lod >= 0) lodRenderers[lod] = renderer;
            var source = renderer.sharedMaterials;
            var replacements = new Material[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var key = source[i] != null ? source[i].name : string.Empty;
                if (Materials.TryGetValue(key, out var replacement))
                    replacements[i] = replacement;
                else
                {
                    report.errors.Add(name + ": no project material matches imported slot '" + key + "' on " + renderer.name);
                    replacements[i] = Materials.Values.FirstOrDefault();
                }
            }
            renderer.sharedMaterials = replacements;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
        if (lodRenderers.Count != 3 || !lodRenderers.ContainsKey(0) || !lodRenderers.ContainsKey(1) || !lodRenderers.ContainsKey(2))
            report.errors.Add(name + ": expected imported MeshRenderer children named LOD0, LOD1 and LOD2.");

        var lodGroup = root.AddComponent<LODGroup>();
        lodGroup.fadeMode = LODFadeMode.None;
        lodGroup.animateCrossFading = false;
        lodGroup.localReferencePoint = new Vector3(0, 0.44f, 0);
        lodGroup.size = 0.87f;
        if (lodRenderers.Count == 3)
        {
            lodGroup.SetLODs(new[]
            {
                new LOD(0.15f, new[] { lodRenderers[0] }),
                new LOD(0.05f, new[] { lodRenderers[1] }),
                new LOD(0.012f, new[] { lodRenderers[2] }),
            });
            lodGroup.RecalculateBounds();
        }
        AddCompoundColliders(root, kind, name.Contains("Wear03"), lodRenderers.ContainsKey(0) ? lodRenderers[0] : null);
        GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic |
            StaticEditorFlags.NavigationStatic);
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

        var prefabPath = PrefabDir + "/" + name + ".prefab";
        var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        if (saved == null)
            report.errors.Add("Prefab save failed: " + prefabPath);
        return saved;
    }

    private static void AddCompoundColliders(GameObject root, int kind, bool destroyed, Renderer lod0)
    {
        var length = BenchLengths[kind - 1];
        if (AddWoodColliders(root, lod0) == 0)
        {
            AddBox(root, "Seat collision", new Vector3(0,0.444f,0), new Vector3(length - 0.14f,0.060f,0.445f));
            if (kind != 5) AddBox(root, "Back collision", new Vector3(0,0.710f,-0.215f), new Vector3(length - 0.16f,0.34f,0.090f));
        }
        if (kind == 3)
        {
            AddBox(root, "Concrete support left", new Vector3(-0.60f,0.2075f,0), new Vector3(0.34f,0.415f,0.43f));
            AddBox(root, "Concrete support right", new Vector3(0.60f,0.2075f,0), new Vector3(0.34f,0.415f,0.43f));
        }
        else if (kind == 5)
        {
            AddBox(root, "Low support left", new Vector3(-0.56f,0.20f,0), new Vector3(0.12f,0.40f,0.40f));
            AddBox(root, "Low support right", new Vector3(0.56f,0.20f,0), new Vector3(0.12f,0.40f,0.40f));
        }
        else
        {
            var depth = kind == 4 ? 0.70f : kind == 1 ? 0.62f : 0.55f;
            AddBox(root, "Frame support left", new Vector3(-length * 0.40f,0.215f,0), new Vector3(0.12f,0.43f,depth));
            AddBox(root, "Frame support right", new Vector3(length * 0.40f,0.215f,0), new Vector3(0.12f,0.43f,depth));
        }
    }

    private static int AddWoodColliders(GameObject root, Renderer renderer)
    {
        if (renderer == null) return 0;
        var filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return 0;
        var mesh = filter.sharedMesh;
        var vertices = mesh.vertices;
        var ids = new Dictionary<Vector3Int,int>();
        var parents = new List<int>();
        var groups = new Dictionary<int,Bounds>();
        Func<int,int> find = null;
        find = index => parents[index] == index ? index : (parents[index] = find(parents[index]));
        Func<Vector3,int> getId = point =>
        {
            var key = new Vector3Int(Mathf.RoundToInt(point.x*10000),Mathf.RoundToInt(point.y*10000),Mathf.RoundToInt(point.z*10000));
            if (ids.TryGetValue(key,out var old)) return old;
            var id = parents.Count; parents.Add(id); ids.Add(key,id); return id;
        };
        var samples = new List<KeyValuePair<int,Vector3>>();
        for (var sub = 0; sub < mesh.subMeshCount; sub++)
        {
            if (sub >= renderer.sharedMaterials.Length || !renderer.sharedMaterials[sub].name.StartsWith("M_Wood",StringComparison.Ordinal)) continue;
            var tris = mesh.GetTriangles(sub);
            for (var index=0; index<tris.Length; index+=3)
            {
                var triIds = new int[3];
                for (var corner=0; corner<3; corner++)
                {
                    var point=root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[tris[index+corner]]));
                    triIds[corner]=getId(point); samples.Add(new KeyValuePair<int,Vector3>(triIds[corner],point));
                }
                for (var corner=1;corner<3;corner++)
                {
                    var a=find(triIds[0]); var b=find(triIds[corner]);
                    parents[Math.Max(a,b)]=Math.Min(a,b);
                }
            }
        }
        foreach (var sample in samples)
        {
            var id=find(sample.Key);
            if (groups.TryGetValue(id,out var bounds)) { bounds.Encapsulate(sample.Value); groups[id]=bounds; }
            else groups[id]=new Bounds(sample.Value,Vector3.zero);
        }
        var count=0;
        foreach (var bounds in groups.Values)
        {
            if (bounds.size.x < 0.20f) continue;
            var size=bounds.size;
            size.y=Mathf.Max(size.y,0.025f); size.z=Mathf.Max(size.z,0.025f);
            AddBox(root,"Individual timber collision",bounds.center,size); count++;
        }
        return count;
    }

    private static void AddBox(GameObject owner, string name, Vector3 center, Vector3 size)
    {
        var collider = owner.AddComponent<BoxCollider>();
        collider.center = center;
        collider.size = size;
        collider.enabled = true;
    }

    private static BenchResult ValidatePrefab(GameObject prefab, string name, ValidationReport report)
    {
        var result = new BenchResult
        {
            name = name,
            rootScale = new[] { prefab.transform.localScale.x, prefab.transform.localScale.y, prefab.transform.localScale.z },
            minGroundY = float.PositiveInfinity,
            hasLightmapUv = true,
            colliderCount = prefab.GetComponents<Collider>().Length,
            hasLodGroup = prefab.GetComponent<LODGroup>() != null
        };
        if ((prefab.transform.localScale - Vector3.one).sqrMagnitude > 1e-6f)
            report.errors.Add(name + ": root scale is not (1,1,1).");
        if (!result.hasLodGroup)
            report.errors.Add(name + ": LODGroup is missing.");
        if (result.colliderCount < 3)
            report.errors.Add(name + ": expected a compound collider with at least 3 BoxColliders.");
        if (prefab.GetComponent<Rigidbody>() != null)
            report.errors.Add(name + ": unexpected Rigidbody on static bench.");

        var lod = prefab.GetComponent<LODGroup>();
        if (lod != null)
        {
            var entries = lod.GetLODs();
            if (entries.Length != 3)
                report.errors.Add(name + ": expected 3 LOD levels; got " + entries.Length);
            if (entries.Length == 3)
            {
                result.lod0Triangles = CountTriangles(entries[0].renderers);
                result.lod1Triangles = CountTriangles(entries[1].renderers);
                result.lod2Triangles = CountTriangles(entries[2].renderers);
                result.lod1Ratio = Ratio(result.lod1Triangles, result.lod0Triangles);
                result.lod2Ratio = Ratio(result.lod2Triangles, result.lod0Triangles);
                var minimum = name == "Bench_RU_05_Wear03" ? 12000 : 15000;
                var maximum = name.StartsWith("Bench_RU_04_",StringComparison.Ordinal) ? 41000 : 40000;
                if (name == "Bench_RU_05_Wear03") report.notes.Add("Destroyed backless bench has fewer triangles because its missing timber and fittings are actually removed.");
                if (result.lod0Triangles > 40000 && maximum == 41000) report.notes.Add(name+": real perforations and four grounded legs account for a slight overrun of the approximate 40k target.");
                if (result.lod0Triangles < minimum || result.lod0Triangles > maximum)
                    report.errors.Add(name + ": LOD0 triangle count is outside the 15k-40k modelling target: " + result.lod0Triangles);
                if (result.lod1Ratio < 0.38f || result.lod1Ratio > 0.54f)
                    report.errors.Add(name + ": LOD1 triangle ratio is outside 0.40-0.50 target: " + result.lod1Ratio.ToString("F3"));
                if (result.lod2Ratio < 0.10f || result.lod2Ratio > 0.21f)
                    report.errors.Add(name + ": LOD2 triangle ratio is outside 0.10-0.20 target: " + result.lod2Ratio.ToString("F3"));
            }
        }

        var temp = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (temp != null)
        {
            var renderers = temp.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer).ToArray();
            result.rendererCount = renderers.Length;
            var materialNames = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    result.materialSlotCount++;
                    if (material == null)
                    {
                        result.missingMaterialSlots++;
                        continue;
                    }
                    materialNames.Add(material.name);
                    var expectsPbrMaps = material.name.StartsWith("M_Wood", StringComparison.Ordinal) ||
                                          material.name.StartsWith("M_Steel", StringComparison.Ordinal) ||
                                          material.name.StartsWith("M_Cast", StringComparison.Ordinal) ||
                                          material.name.StartsWith("M_Concrete", StringComparison.Ordinal) ||
                                          material.name.StartsWith("M_Bolt", StringComparison.Ordinal);
                    if (expectsPbrMaps && material.HasProperty("_MainTex") && material.GetTexture("_MainTex") == null)
                        report.errors.Add(name + ": missing base color texture in " + material.name);
                    if (expectsPbrMaps && material.HasProperty("_MetallicGlossMap") && material.GetTexture("_MetallicGlossMap") == null)
                        report.errors.Add(name + ": missing metallic/smoothness map in " + material.name);
                    if (expectsPbrMaps && material.GetTexture("_BumpMap") == null) report.errors.Add(name + ": missing normal map in " + material.name);
                    if (expectsPbrMaps && material.GetTexture("_OcclusionMap") == null) report.errors.Add(name + ": missing AO map in " + material.name);
                    if (!material.shader.name.StartsWith("Universal Render Pipeline/",StringComparison.Ordinal)) report.errors.Add(name+": material does not use the target URP shader: "+material.name);
                }
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    var mesh = filter.sharedMesh;
                    result.hasLightmapUv &= mesh.uv2 != null && mesh.uv2.Length == mesh.vertexCount;
                    var tr = filter.transform;
                    foreach (var vertex in mesh.vertices)
                    {
                        var world = tr.TransformPoint(vertex);
                        if (world.y < result.minGroundY)
                            result.minGroundY = world.y;
                    }
                }
            }
            result.materialNames = materialNames.ToArray();
            if (result.missingMaterialSlots > 0)
                report.errors.Add(name + ": has " + result.missingMaterialSlots + " null material slots.");
            if (!result.hasLightmapUv)
                report.errors.Add(name + ": imported meshes have no UV2 lightmap channel.");
            if (Mathf.Abs(result.minGroundY) > 0.012f)
                report.errors.Add(name + ": lowest mesh vertex is not at ground pivot; y=" + result.minGroundY.ToString("F4"));
            UnityEngine.Object.DestroyImmediate(temp);
        }
        return result;
    }

    private static float Ratio(int a, int b) => b == 0 ? 0 : (float)a / b;

    private static int CountTriangles(Renderer[] renderers)
    {
        var count = 0;
        foreach (var renderer in renderers)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var mesh = filter.sharedMesh;
            for (var i = 0; i < mesh.subMeshCount; i++)
                count += (int)mesh.GetIndexCount(i) / 3;
        }
        return count;
    }

    private static void CreateDemoScene(ValidationReport report)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Neutral comparison floor";
        ground.transform.position = new Vector3(0,-0.09f,0);
        ground.transform.localScale = new Vector3(13.0f,0.18f,13.5f);
        var groundCollider = ground.GetComponent<Collider>();
        if (groundCollider != null) UnityEngine.Object.DestroyImmediate(groundCollider);
        var floorMatPath = MaterialDir + "/M_Demo_Concrete.mat";
        var floorMat = AssetDatabase.LoadAssetAtPath<Material>(floorMatPath);
        if (floorMat == null)
        {
            floorMat = new Material(SelectedShader) { name = "M_Demo_Concrete" };
            SetColor(floorMat,new Color(0.28f,0.29f,0.28f));
            floorMat.SetFloat("_Glossiness",0.22f);
            AssetDatabase.CreateAsset(floorMat,floorMatPath);
        }
        ground.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
        floorMat.shader = SelectedShader;
        SetColor(floorMat, new Color(0.37f,0.38f,0.39f));
        EditorUtility.SetDirty(floorMat);

        for (var wear = 1; wear <= 3; wear++)
        {
            var label = CreateLabel("Степень износа " + wear.ToString("00"), new Vector3((wear-2)*2.9f,0.02f,5.45f));
            label.transform.localScale = Vector3.one * 0.76f;
        }
        for (var kind = 1; kind <= 5; kind++)
        {
            for (var wear = 1; wear <= 3; wear++)
            {
                var name = "Bench_RU_" + kind.ToString("00") + "_Wear" + wear.ToString("00");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + name + ".prefab");
                if (prefab == null)
                {
                    report.errors.Add("Demo scene is missing prefab " + name);
                    continue;
                }
                var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                instance.name = name;
                instance.transform.position = new Vector3((wear-2)*2.9f, 0.0f, (3-kind)*2.05f);
                CreateLabel("" + kind.ToString("00"), new Vector3((wear-2)*2.9f,0.02f,(3-kind)*2.05f+0.67f)).transform.localScale = Vector3.one * 0.36f;
            }
        }

        var lightObject = new GameObject("Comparison directional light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1.0f,0.91f,0.78f);
        light.intensity = 1.8f;
        light.shadowBias = 0.08f;
        light.shadowNormalBias = 0.3f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(48f,-28f,0f);
        var fillObject = new GameObject("Soft comparison fill");
        var fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional; fill.intensity = 0.38f;
        fill.color = new Color(0.78f,0.86f,1.0f); fill.shadows = LightShadows.None;
        fillObject.transform.rotation = Quaternion.Euler(30,145,0);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f,0.46f,0.51f);
        RenderSettings.ambientEquatorColor = new Color(0.30f,0.31f,0.29f);
        RenderSettings.ambientGroundColor = new Color(0.12f,0.12f,0.11f);
        RenderSettings.reflectionIntensity = 0.25f;

        var cameraObject = new GameObject("Demo overview camera");
        var camera = cameraObject.AddComponent<Camera>();
        if (SelectedShader.name.StartsWith("Universal", StringComparison.Ordinal)) cameraObject.AddComponent<UniversalAdditionalCameraData>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.20f,0.23f,0.25f);
        camera.fieldOfView = 51f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100f;
        cameraObject.transform.position = new Vector3(0,7.0f,11.5f);
        cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0,0.25f,0)-cameraObject.transform.position, Vector3.up);
        camera.tag = "MainCamera";
        camera.allowHDR = true;

        EditorSceneManager.SaveScene(scene, DemoScenePath);
        report.scenePreviewRendered = RenderPreview(camera, report);
        RenderCloseups(camera, report);
    }

    private static TextMesh CreateLabel(string text, Vector3 position)
    {
        var go = new GameObject("Label_" + text);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0,180,0);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 48;
        tm.characterSize = 0.07f;
        tm.color = new Color(0.72f,0.76f,0.75f);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) tm.font = font;
        return tm;
    }

    private static void RenderCloseups(Camera camera, ValidationReport report)
    {
        var labels = camera.gameObject.scene.GetRootGameObjects().Where(go => go.name.StartsWith("Label_", StringComparison.Ordinal)).ToArray();
        foreach (var label in labels) label.SetActive(false);
        var roots = camera.gameObject.scene.GetRootGameObjects().Where(go => go.name.StartsWith("Bench_RU_", StringComparison.Ordinal)).ToArray();
        var position = camera.transform.position;
        var rotation = camera.transform.rotation;
        foreach (var target in roots)
        {
            foreach (var other in roots) other.SetActive(other == target);
            var aim = target.transform.position + Vector3.up * 0.43f;
            camera.transform.position = aim + new Vector3(1.65f, 1.05f, 2.25f);
            camera.transform.LookAt(aim);
            RenderPreview(camera, report, target.name + ".png", 1200, 800);
            RenderPreview(camera, report, "LOD1_"+target.name+".png",1200,800,1);
            RenderPreview(camera, report, "LOD2_"+target.name+".png",1200,800,2);
            if (target.name.EndsWith("Wear02",StringComparison.Ordinal))
            {
                aim = target.transform.position + new Vector3(0,0.46f,0);
                camera.transform.position = aim + new Vector3(0.40f,0.32f,0.70f);
                camera.transform.LookAt(aim);
                RenderPreview(camera,report,"Detail_"+target.name+".png",1600,1000);
            }
        }
        foreach (var root in roots) root.SetActive(true);
        foreach (var label in labels) label.SetActive(true);
        camera.transform.SetPositionAndRotation(position, rotation);
    }

    private static void RequireIsolatedProject()
    {
        if (!Application.dataPath.Replace('\\','/').EndsWith("/work/Benches_RU/UnityProject/Assets", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bench builder is restricted to work/Benches_RU/UnityProject; it must not replace a user scene.");
    }

    private static bool RenderPreview(Camera camera, ValidationReport report, string filename = "Demo_Benches_RU.png", int width = 1600, int height = 1100, int forceLod = 0)
    {
        var capturePipeline=QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        var residentMode=capturePipeline!=null ? capturePipeline.gpuResidentDrawerMode : GPUResidentDrawerMode.Disabled;
        try
        {
            if(capturePipeline!=null)
            {
                capturePipeline.gpuResidentDrawerMode=GPUResidentDrawerMode.Disabled;
                GPUResidentDrawer.ReinitializeIfNeeded();
            }
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            var pixels = new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            var lodGroups = UnityEngine.Object.FindObjectsOfType<LODGroup>();
            var suppressed = new List<Renderer>();
            foreach (var group in lodGroups) group.ForceLOD(forceLod);
            foreach (var group in lodGroups)
                foreach (var level in group.GetLODs().Where((entry,index)=>index!=forceLod))
                    foreach (var renderer in level.renderers) { renderer.forceRenderingOff = true; suppressed.Add(renderer); }
            camera.targetTexture = target;
            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset)
            {
                // The first request after a cold open initializes material GPU buffers.
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            }
            else camera.Render();
            foreach (var renderer in suppressed) renderer.forceRenderingOff = false;
            foreach (var group in lodGroups) group.ForceLOD(-1);
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
            pixels.Apply(false,false);
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","Verification"));
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir,filename),pixels.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(pixels);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            return true;
        }
        catch (Exception ex)
        {
            report.errors.Add("Demo preview render failed: " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
        finally
        {
            if(capturePipeline!=null)
            {
                capturePipeline.gpuResidentDrawerMode=residentMode;
                GPUResidentDrawer.ReinitializeIfNeeded();
            }
        }
    }
}
