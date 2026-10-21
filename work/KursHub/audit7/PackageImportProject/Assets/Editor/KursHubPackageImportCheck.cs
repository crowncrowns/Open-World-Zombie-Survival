#if UNITY_EDITOR
#pragma warning disable 0649 // These fields are populated by JsonUtility from external manifests.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Temporary independent revision-8 archive check; existing Unity Library cache is retained, authored Kurs assets are absent before import.</summary>
public static class KursHubPackageImportCheck8
{
    private const string Folder = "Assets/Environment/KursHub";
    private const string Hub = Folder + "/KursHub.prefab";
    private const string Demo = Folder + "/Scenes/KursHubDemo.unity";
    [Serializable] private sealed class Config { public string receiverProjectPath; public string packagePath; public string reportPath; public string previewFolder; public string expectedPath; public int expectedPrefabCount; }
    [Serializable] private sealed class Entry { public string path; public string guid; public string contentSha256; }
    [Serializable] private sealed class Expected { public string packageSha256; public Entry[] entries; public int prefabCount; public int propCount; public int modelCount; public int pngCount; public int familyCount; public int nestedPropInstances; }
    [Serializable] private sealed class Props { public Prop[] props; }
    [Serializable] private sealed class Prop { public string id; public float[] boundsMin; public float[] boundsMax; public SourceLod[] lods; }
    [Serializable] private sealed class SourceLod { public int level; public int triangles; public string model; }
    [Serializable] private sealed class Surfaces { public Surface[] families; }
    [Serializable] private sealed class Surface { public string name; public int resolution; public float tiling; }
    [Serializable] private sealed class HubManifest { public int stage; public float width; public float depth; public float floorHeight; public Placement[] propInstances; public Placement[] interiorInstances; public Placement[] settlementInstances; }
    [Serializable] private sealed class Placement { public string id; public string prop; }
    [Serializable] private sealed class AssetCheck
    {
        public string path; public string guid; public int renderers; public int lodGroups; public long[] lodTriangles;
        public int enabledColliders; public int disabledColliders; public int missingUv2; public float[] boundsMin; public float[] boundsMax; public string[] errors;
    }
    [Serializable] private sealed class TextureCheck { public string path; public int width; public int height; public bool srgb; public string type; public int maximum; }
    [Serializable] private sealed class CaptureCheck { public string path; public string method; public int width; public int height; public float meanLuminance; public float luminanceVariance; public float magentaFraction; }
    [Serializable] private sealed class Report
    {
        public string status = "RUNNING"; public string unityVersion; public string urpVersion; public string pipeline;
        public string packagePath; public string packageSha256; public string generatedUtc; public int archiveEntries; public int guidChecks;
        public int prefabCount; public int propCount; public int modelCount; public int pngCount; public int familyCount; public int contentHashChecks; public int materialCount; public int independentCollisionChecks; public int nestedPropInstances;
        public string demoScene; public string[] dependencies; public string[] externalDependencies; public string[] errors; public string[] warnings;
        public AssetCheck[] assets; public TextureCheck[] textures; public CaptureCheck[] captures;
        public string limitation = "Fresh authored-asset archive import into a settings-only receiver using its cached Unity Library; static structure and native URP renders. No player/controller, NPC, baked lighting, populated FPS or photographic-reference acceptance is claimed.";
    }
    private static Config config;
    private static Report report;
    private static Expected expectedImport;
    private static readonly List<string> errors = new List<string>();
    private static readonly List<string> warnings = new List<string>();

    public static void ImportAndCheck()
    {
        config = JsonUtility.FromJson<Config>(File.ReadAllText("ValidationConfig8.json", Encoding.UTF8));
        report = new Report { unityVersion = Application.unityVersion, generatedUtc = DateTime.UtcNow.ToString("o"), packagePath = config.packagePath };
        Application.logMessageReceived += CaptureLog;
        try
        {
            errors.Clear(); warnings.Clear();
            Require(Path.GetFullPath(Path.GetDirectoryName(Application.dataPath)).TrimEnd(Path.DirectorySeparatorChar) == Path.GetFullPath(config.receiverProjectPath).TrimEnd(Path.DirectorySeparatorChar), "Wrong owned receiver project.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(Hub) == null, "Cached Kurs asset is present before archive import.");
            Require(Application.unityVersion == "6000.6.3f1", "Wrong Unity version: " + Application.unityVersion);
            report.urpVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UniversalRenderPipeline).Assembly)?.version;
            Require(report.urpVersion == "17.6.0", "Wrong URP version: " + report.urpVersion);
            Require(!Directory.Exists(Folder), "Project already contains Kurs assets; fresh import is required.");
            Require(File.Exists(config.packagePath), "Final .unitypackage does not exist.");
            Require(File.Exists(config.expectedPath), "Archive GUID binding has not been created.");
            var expected = JsonUtility.FromJson<Expected>(File.ReadAllText(config.expectedPath, Encoding.UTF8));
            report.packageSha256 = Hash(config.packagePath);
            Require(report.packageSha256 == expected.packageSha256, "Package differs from its expected GUID snapshot.");
            Require(expected.prefabCount == config.expectedPrefabCount && expected.prefabCount == expected.propCount + 1 && expected.propCount > 0, "Dynamic archive prefab/prop count mismatch.");
            expectedImport = expected;
            PreparePipeline();
            WriteReport();
            AssetDatabase.importPackageCompleted += Imported;
            AssetDatabase.importPackageFailed += ImportFailed;
            AssetDatabase.importPackageCancelled += ImportCancelled;
            // Exercise the requested real AssetDatabase archive importer. Unity
            // 6000.6 also exposes the replacement AssetPackage API.
#pragma warning disable 0618
            AssetDatabase.ImportPackage(config.packagePath, false);
#pragma warning restore 0618

        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    // Additional framing review only; the original fresh-import report/log remain intact.
    public static void CaptureImported()
    {
        config = JsonUtility.FromJson<Config>(File.ReadAllText("ValidationConfig8.json", Encoding.UTF8));
        config.reportPath = Path.Combine(Path.GetDirectoryName(config.reportPath),"package_import_additional_views8.json");
        config.previewFolder = Path.Combine(Path.GetDirectoryName(config.previewFolder),"package_import_additional_views8");
        report = new Report { unityVersion=Application.unityVersion,generatedUtc=DateTime.UtcNow.ToString("o"),packagePath=config.packagePath,
            limitation="Additional native URP camera framing review of the already imported package. The separate original package_import_validation8.json is the fresh archive import proof." };
        errors.Clear();warnings.Clear();Application.logMessageReceived += CaptureLog;
        try
        {
            Require(Path.GetFullPath(Path.GetDirectoryName(Application.dataPath))==Path.GetFullPath(config.receiverProjectPath),"Wrong owned receiver.");
            Require(Application.unityVersion=="6000.6.3f1","Wrong native Unity version.");
            report.urpVersion=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UniversalRenderPipeline).Assembly)?.version;
            Require(report.urpVersion=="17.6.0","Wrong native URP version.");
            expectedImport=JsonUtility.FromJson<Expected>(File.ReadAllText(config.expectedPath,Encoding.UTF8));
            report.packageSha256=Hash(config.packagePath);Require(report.packageSha256==expectedImport.packageSha256,"Package changed since binding.");
            PreparePipeline();CheckSceneAndCapture();Require(errors.Count==0,"Additional capture errors.");
            report.status="PASS";WriteReport();Debug.Log("KURS_PACKAGE_ADDITIONAL_VIEWS8_PASS");EditorApplication.Exit(0);
        }
        catch(Exception exception){Fail(exception);}
    }

    private static void Imported(string packageName)
    {
        UnsubscribeImport();
        // Complete only after the real importer finished and deferred refresh ran.
        EditorApplication.delayCall += ValidateImported;
    }
    private static void ImportFailed(string packageName, string message) { UnsubscribeImport(); Fail(new InvalidOperationException(packageName + ": " + message)); }
    private static void ImportCancelled(string packageName) { UnsubscribeImport(); Fail(new InvalidOperationException("Import cancelled: " + packageName)); }
    private static void UnsubscribeImport()
    {
        AssetDatabase.importPackageCompleted -= Imported;
        AssetDatabase.importPackageFailed -= ImportFailed;
        AssetDatabase.importPackageCancelled -= ImportCancelled;
    }
    private static void ValidateImported()
    {
        try
        {
            var expected = expectedImport;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            report.archiveEntries = expected.entries.Length;
            foreach (var entry in expected.entries)
            {
                Require(AssetDatabase.AssetPathToGUID(entry.path) == entry.guid, "GUID mismatch after import: " + entry.path);
                report.guidChecks++;
                if (!string.IsNullOrEmpty(entry.contentSha256))
                {
                    var physical = PhysicalPath(entry.path);
                    Require(File.Exists(physical) && Hash(physical) == entry.contentSha256, "Imported bytes differ from the archive: " + entry.path);
                    report.contentHashChecks++;
                }
            }
            Require(Directory.GetFiles(Folder, "*.cs", SearchOption.AllDirectories).Length == 0, "Deliverable unexpectedly contains authoring/gameplay scripts.");
            var prefabPaths = Directory.GetFiles(Folder, "*.prefab", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            report.prefabCount = prefabPaths.Length;
            Require(prefabPaths.Length == expected.prefabCount, "Imported prefab count differs from the actual final archive.");
            var props = JsonUtility.FromJson<Props>(File.ReadAllText(Folder + "/Props/KursHubPropsManifest.json", Encoding.UTF8));
            Require(props.props.Length == expected.propCount && props.props.Length + 1 == prefabPaths.Length, "Standalone prop manifest does not cover every archived prefab.");
            report.propCount = props.props.Length;
            var fbxPaths = Directory.GetFiles(Folder,"*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)).ToArray();
            report.modelCount = fbxPaths.Length;
            Require(report.modelCount == expected.modelCount,"Imported FBX count differs from the actual archive.");
            foreach (var modelPath in fbxPaths) Require(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath.Replace('\\','/')) != null,"FBX failed native Unity import: "+modelPath);
            var manifest = JsonUtility.FromJson<HubManifest>(File.ReadAllText(Folder + "/KursHubManifest.json", Encoding.UTF8));
            Require(manifest.stage == 8 && manifest.width == 93 && manifest.depth == 130 && Mathf.Abs(manifest.floorHeight - 5.5f) < .0001f, "Building dimensional contract changed.");
            Require(manifest.propInstances.Length + manifest.interiorInstances.Length + manifest.settlementInstances.Length == expected.nestedPropInstances,"Manifest placement count differs from archive binding.");
            report.assets = prefabPaths.Select(path => CheckAsset(path, props.props.FirstOrDefault(p => path.EndsWith("/" + p.id + ".prefab", StringComparison.Ordinal)))).ToArray();
            foreach (var asset in report.assets) errors.AddRange(asset.errors.Select(error => asset.path + ": " + error));
            report.dependencies = AssetDatabase.GetDependencies(prefabPaths.Append(Demo).ToArray(), true).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            report.externalDependencies = report.dependencies.Where(p => !AllowedDependency(p)).ToArray();
            Require(report.externalDependencies.Length == 0, "Package depends on assets outside its own folder / URP / builtins.");
            report.pngCount = Directory.GetFiles(Folder,"*",SearchOption.AllDirectories).Count(p=>p.EndsWith(".png",StringComparison.OrdinalIgnoreCase));
            Require(report.pngCount == expected.pngCount,"Imported PNG count differs from archive.");
            report.textures = CheckTextures();
            var allMaterials = prefabPaths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).SelectMany(a => a.GetComponentsInChildren<MeshRenderer>(true)).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            report.materialCount = allMaterials.Length;
            foreach (var material in allMaterials) CheckMaterial(material);
            CheckSceneAndCapture();
            Require(errors.Count == 0, "Validation accumulated errors.");
            Require(Hash(config.packagePath) == report.packageSha256,"Package changed during native import/validation.");
            report.status = "PASS";
            WriteReport();
            Debug.Log("KURS_PACKAGE_IMPORT8_PASS: " + report.prefabCount + " prefabs; " + report.guidChecks + " GUIDs; URP " + report.urpVersion);
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Fail(exception); }
    }
    private static void Fail(Exception exception)
    {
        errors.Add(exception.ToString()); report.status = "FAIL"; WriteReport();
        Debug.LogError("KURS_PACKAGE_IMPORT8_FAIL: " + exception.Message);
        EditorApplication.Exit(1);
    }

    private static void PreparePipeline()
    {
        const string settings = "Assets/PackageValidationSettings";
        if (!AssetDatabase.IsValidFolder(settings)) AssetDatabase.CreateFolder("Assets", "PackageValidationSettings");
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(settings + "/Renderer.asset");
        if (renderer == null) { renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); renderer.name = "Fresh Universal Renderer"; AssetDatabase.CreateAsset(renderer, settings + "/Renderer.asset"); }
        // Public URP 17.6 API, verified against the locally installed package.
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(settings + "/Pipeline.asset");
        var createPipeline = pipeline == null;
        if (createPipeline) pipeline = UniversalRenderPipelineAsset.Create(renderer);
        pipeline.name = "Fresh Package Validation URP";
        pipeline.supportsHDR = true; pipeline.msaaSampleCount = 1;
        if (createPipeline) AssetDatabase.CreateAsset(pipeline, settings + "/Pipeline.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        for (var level = 0; level < QualitySettings.names.Length; level++)
        {
            QualitySettings.SetQualityLevel(level, false); QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, false);
        QualitySettings.lodBias = 1; QualitySettings.maximumLODLevel = 0;
        EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
        AssetDatabase.SaveAssets();
        report.pipeline = GraphicsSettings.currentRenderPipeline?.GetType().FullName;
        Require(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset, "Fresh URP is not active.");
    }

    private static AssetCheck CheckAsset(string path, Prop prop)
    {
        var failures = new List<string>();
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Require(root != null, "Missing prefab: " + path);
        var result = new AssetCheck { path = path, guid = AssetDatabase.AssetPathToGUID(path) };
        var transforms = root.GetComponentsInChildren<Transform>(true);
        if (root.transform.localPosition != Vector3.zero || root.transform.localScale != Vector3.one || Quaternion.Angle(root.transform.localRotation, Quaternion.identity) > .001f) failures.Add("Root pivot transform is not identity.");
        if (transforms.Any(t => !Finite(t.localPosition) || !Finite(t.localScale) || t.localScale.x <= 0 || t.localScale.y <= 0 || t.localScale.z <= 0)) failures.Add("Nonfinite transform or nonpositive scale.");
        if (transforms.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) != 0) failures.Add("Missing scripts.");
        if (root.GetComponentsInChildren<Rigidbody>(true).Length != 0) failures.Add("Unexpected Rigidbody in static asset.");
        var renderers = root.GetComponentsInChildren<MeshRenderer>(true); result.renderers = renderers.Length;
        if (renderers.Length == 0) failures.Add("No render geometry.");
        foreach (var renderer in renderers)
        {
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) { failures.Add("Missing mesh: " + renderer.name); continue; }
            if (mesh.vertexCount == 0 || mesh.subMeshCount == 0) failures.Add("Empty mesh: " + renderer.name);
            if (!mesh.HasVertexAttribute(VertexAttribute.Normal) || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) failures.Add("Missing normals / UV0: " + renderer.name);
            if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord1)) result.missingUv2++;
            if (renderer.sharedMaterials.Length != mesh.subMeshCount || renderer.sharedMaterials.Any(m => m == null)) failures.Add("Missing/misaligned material slot: " + renderer.name);
            foreach (var material in renderer.sharedMaterials.Where(m => m != null))
                if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit" || !material.shader.isSupported) failures.Add("Missing or incompatible shader: " + material.name);
        }
        var shell = root.transform.Find("Shell");
        if (shell == null) failures.Add("Missing editable Shell hierarchy.");
        else
        {
            var bounds = LocalBounds(root.transform, shell.GetComponentsInChildren<MeshRenderer>(true));
            result.boundsMin = Values(bounds.min); result.boundsMax = Values(bounds.max);
            if (!Finite(bounds.size) || bounds.size.sqrMagnitude < .0000001f || bounds.size.x < 0 || bounds.size.y < 0 || bounds.size.z < 0) failures.Add("Invalid bounds.");
            if (prop != null)
                for (var axis = 0; axis < 3; axis++)
                    if (Mathf.Abs(bounds.min[axis] - prop.boundsMin[axis]) > .02f || Mathf.Abs(bounds.max[axis] - prop.boundsMax[axis]) > .02f) failures.Add("Metre dimensions differ from the source manifest, axis " + axis);
        }
        var colliders = root.GetComponentsInChildren<Collider>(true);
        result.enabledColliders = colliders.Count(c => c.enabled && ActiveInsidePrefab(c.transform,root.transform)); result.disabledColliders = colliders.Count(c => !c.enabled);
        if (colliders.OfType<MeshCollider>().Any(c => c.sharedMesh == null)) failures.Add("MeshCollider lost its mesh.");
        if (colliders.OfType<BoxCollider>().Any(c => !Finite(c.size) || c.size.x <= 0 || c.size.y <= 0 || c.size.z <= 0)) failures.Add("Invalid BoxCollider dimensions.");
        var groups = root.GetComponentsInChildren<LODGroup>(true); result.lodGroups = groups.Length;
        var ownGroup = root.GetComponent<LODGroup>();
        if ((path == Hub || prop?.lods?.Length == 3) && ownGroup == null) failures.Add("Missing required LODGroup.");
        var totals = new long[3];
        foreach (var group in groups)
        {
            var lods = group.GetLODs();
            if (lods.Length != 3) { failures.Add("LODGroup must have 3 levels: " + group.name); continue; }
            var counts = new long[3];
            var unique = new HashSet<Renderer>();
            for (var level = 0; level < 3; level++)
            {
                if (lods[level].renderers == null || lods[level].renderers.Length == 0 || lods[level].renderers.Any(r => r == null)) { failures.Add("LOD renderer reference lost: " + group.name); continue; }
                foreach (var renderer in lods[level].renderers)
                {
                    if (!renderer.transform.IsChildOf(group.transform) || !unique.Add(renderer)) failures.Add("LOD ownership / duplication: " + group.name);
                    if (level > 0 && renderer.GetComponentsInChildren<Collider>(true).Length != 0) failures.Add("Collider is attached to a reduced visual LOD: " + renderer.name);
                }
                counts[level] = Triangles(lods[level].renderers); totals[level] += counts[level];
            }
            if (!(counts[0] > counts[1] && counts[1] > counts[2] && counts[2] > 0)) failures.Add("LOD triangle counts do not descend: " + group.name);
            if (!(lods[0].screenRelativeTransitionHeight > lods[1].screenRelativeTransitionHeight && lods[1].screenRelativeTransitionHeight > lods[2].screenRelativeTransitionHeight && lods[2].screenRelativeTransitionHeight >= 0)) failures.Add("LOD thresholds are invalid: " + group.name);
        }
        result.lodTriangles = totals;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(root);
        try
        {
            var before = instance.GetComponentsInChildren<Collider>(true).Select(c => c.GetEntityId() + ":" + c.enabled + ":" + c.gameObject.activeInHierarchy).ToArray();
            foreach (var group in instance.GetComponentsInChildren<LODGroup>(true)) group.ForceLOD(2);
            var after = instance.GetComponentsInChildren<Collider>(true).Select(c => c.GetEntityId() + ":" + c.enabled + ":" + c.gameObject.activeInHierarchy).ToArray();
            if (!before.SequenceEqual(after)) failures.Add("Collision changed when forcing the lowest visual LOD.");
            report.independentCollisionChecks++;
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        if (result.missingUv2 > 0) failures.Add(result.missingUv2 + " meshes have no UV2.");
        result.errors = failures.Distinct().ToArray(); return result;
    }

    private static TextureCheck[] CheckTextures()
    {
        var surfaces = JsonUtility.FromJson<Surfaces>(File.ReadAllText(Folder + "/Textures/Audit7PBRManifest.json", Encoding.UTF8));
        Require(surfaces.families.Length == expectedImport.familyCount && surfaces.families.Any(s => s.name == "LeafGreen"), "PBR families differ from the archived manifest.");
        report.familyCount = surfaces.families.Length;
        var checks = new List<TextureCheck>();
        foreach (var surface in surfaces.families)
            foreach (var suffix in new[] { "BaseColor", "Normal", "Roughness", "Metallic", "Occlusion", "Height", "MetallicSmoothness" })
            {
                var path = Folder + "/Textures/" + surface.name + "_" + suffix + ".png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(texture != null && importer != null, "Missing PBR map: " + path);
                Require(texture.width == surface.resolution && texture.height == surface.resolution && importer.maxTextureSize == surface.resolution, "PBR map was downsized: " + path);
                Require(importer.sRGBTexture == (suffix == "BaseColor"), "Wrong color space: " + path);
                Require(importer.textureType == (suffix == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default), "Wrong normal / scalar map type: " + path);
                Require(importer.mipmapEnabled, "Missing texture mips: " + path);
                if (suffix == "Normal") Require(!importer.flipGreenChannel && !importer.convertToNormalmap, "Normal was flipped / regenerated: " + path);
                if (surface.name == "FirNeedles" && suffix == "BaseColor") Require(importer.alphaIsTransparency
                    && importer.mipMapsPreserveCoverage && Mathf.Abs(importer.alphaTestReferenceValue - .4f) < .0001f, "Fir mip cutout coverage was lost.");
                checks.Add(new TextureCheck { path = path, width = texture.width, height = texture.height, srgb = importer.sRGBTexture, type = importer.textureType.ToString(), maximum = importer.maxTextureSize });
            }
        return checks.ToArray();
    }

    private static void CheckMaterial(Material material)
    {
        Require(material.shader != null && material.shader.name == "Universal Render Pipeline/Lit", "Shader missing: " + material.name);
        var name = material.name; string family = name;
        if (name == "BlueCladding" || name == "MallBlue") family = "BluePaint";
        if (name == "Concrete" || name == "CreamPanel") family = "CreamConcrete";
        if (name == "Frame" || name == "SteelOxide") family = "RustedSteel";
        if (name == "Floor") family = "FloorConcrete";
        if (name == "Roof") family = "RoofMembrane";
        if (name == "Glass") family = "GlassGrime";
        var basePath = Folder + "/Textures/" + family + "_BaseColor.png";
        if (!File.Exists(basePath)) return;
        Require(material.GetTag("KursSurfaceVersion", false, "") == "7", "PBR migration missing: " + name);
        Require(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")) == basePath, "Base map lost: " + name);
        foreach (var slot in new[] { new[] { "_BumpMap", "Normal" }, new[] { "_MetallicGlossMap", "MetallicSmoothness" }, new[] { "_OcclusionMap", "Occlusion" } })
            Require(AssetDatabase.GetAssetPath(material.GetTexture(slot[0])) == Folder + "/Textures/" + family + "_" + slot[1] + ".png", "PBR slot lost: " + name + " " + slot[0]);
        if (name == "Glass" || name == "Hub8SkylightGlass" || name == "Hub8FloorWear") Require(material.GetFloat("_Surface") == 1 && material.GetFloat("_ZWrite") == 0, "Glass lost its transparent state.");
        if (name == "FirNeedles") Require(material.GetFloat("_Surface") == 0 && material.GetFloat("_ZWrite") == 1
            && material.GetFloat("_AlphaClip") == 1 && Mathf.Abs(material.GetFloat("_Cutoff") - .4f) < .0001f
            && material.IsKeywordEnabled("_ALPHATEST_ON"), "Frond atlas lost its opaque cutout / shadow state.");
        if (name == "ClothInk") Require(material.GetColor("_EmissionColor").maxColorComponent == 0, "Generated painted lettering still emits light.");
    }

    private static void CheckSceneAndCapture()
    {
        Require(File.Exists(Demo), "Demo scene is absent from the package.");
        var scene = EditorSceneManager.OpenScene(Demo, OpenSceneMode.Single); report.demoScene = Demo;
        var roots = scene.GetRootGameObjects();
        Require(roots.Sum(r => r.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))) == 0, "Missing script in demo.");
        var hub = roots.SingleOrDefault(r => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r) == Hub);
        Require(hub != null, "Demo does not contain the linked building prefab.");
        report.nestedPropInstances = hub.GetComponentsInChildren<Transform>(true).Count(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).StartsWith(Folder + "/Props/Prefabs/", StringComparison.Ordinal));
        Require(report.nestedPropInstances == expectedImport.nestedPropInstances, "Reusable prop instances differ from the archive manifest / were unpacked.");
        var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single();
        Require(camera.GetUniversalAdditionalCameraData() != null, "Demo camera has no native URP data.");
        Directory.CreateDirectory(config.previewFolder);
        report.captures = new[] {
            Capture(camera, "package_front", new Vector3(-90,43,-115), new Vector3(0,6,-1), 42),
            Capture(camera, "package_market", new Vector3(0,1.7f,-35), new Vector3(0,3.1f,-3), 76),
            Capture(camera, "package_kitchen", new Vector3(-29,1.65f,55.7f), new Vector3(-37,1.15f,51.7f), 75),
            Capture(camera, "package_clinic", new Vector3(33,7.15f,-16), new Vector3(36.7f,6.6f,-18.3f), 76)
        };
    }

    private static CaptureCheck Capture(Camera camera, string name, Vector3 position, Vector3 target, float fov)
    {
        camera.transform.position = position; camera.transform.LookAt(target); camera.fieldOfView = fov;
        var rt = new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32); rt.Create(); camera.targetTexture = rt;
        var previous = RenderTexture.active; var texture = new Texture2D(1600,1000,TextureFormat.RGB24,false);
        var method = "Camera.Render";
        try
        {
            camera.Render();
            if (RenderPipeline.SupportsRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest()))
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = rt }); method = "Native URP SingleCameraRequest";
            }
            RenderTexture.active = rt; texture.ReadPixels(new Rect(0,0,1600,1000),0,0); texture.Apply();
            var pixels = texture.GetPixels32(); double sum=0,sumSquared=0; var magenta=0;
            foreach(var pixel in pixels)
            {
                var luminance=(.2126*pixel.r+.7152*pixel.g+.0722*pixel.b)/255; sum+=luminance;sumSquared+=luminance*luminance;
                if(pixel.r>220 && pixel.b>220 && pixel.g<35)magenta++;
            }
            var mean=(float)(sum/pixels.Length); var variance=(float)(sumSquared/pixels.Length-mean*mean); var fraction=(float)magenta/pixels.Length;
            Require(mean>.015f && variance>.0001f,"Render is empty or nearly uniform: "+name); Require(fraction<.01f,"Pink shader-error render: "+name);
            var path=Path.Combine(config.previewFolder,name+".png"); File.WriteAllBytes(path,texture.EncodeToPNG());
            return new CaptureCheck { path=path,method=method,width=1600,height=1000,meanLuminance=mean,luminanceVariance=variance,magentaFraction=fraction };
        }
        finally { RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt); }
    }

    private static string PhysicalPath(string assetPath)
    {
        if(!assetPath.StartsWith("Packages/",StringComparison.Ordinal))return assetPath;
        var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
        Require(package!=null && assetPath.StartsWith(package.assetPath+"/",StringComparison.Ordinal),"Cannot resolve native package path: "+assetPath);
        return Path.Combine(package.resolvedPath,assetPath.Substring(package.assetPath.Length+1));
    }
    private static bool ActiveInsidePrefab(Transform child,Transform root)
    {
        for(var current=child;current!=null;current=current.parent)
        {
            if(!current.gameObject.activeSelf)return false;
            if(current==root)return true;
        }
        return false;
    }
    private static Bounds LocalBounds(Transform root, IEnumerable<MeshRenderer> renderers)
    {
        var first=true;var total=new Bounds();
        foreach(var renderer in renderers)
        {
            var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)continue;
            var bounds=mesh.bounds;var matrix=root.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
            for(var corner=0;corner<8;corner++)
            {
                var point=matrix.MultiplyPoint3x4(new Vector3((corner&1)==0?bounds.min.x:bounds.max.x,(corner&2)==0?bounds.min.y:bounds.max.y,(corner&4)==0?bounds.min.z:bounds.max.z));
                if(first){total=new Bounds(point,Vector3.zero);first=false;}else total.Encapsulate(point);
            }
        }
        return total;
    }
    private static long Triangles(IEnumerable<Renderer> renderers) => renderers.Where(r=>r!=null).Select(r=>r.GetComponent<MeshFilter>()?.sharedMesh).Where(m=>m!=null).Sum(m=>Enumerable.Range(0,m.subMeshCount).Sum(sm=>(long)m.GetIndexCount(sm)/3));
    private static float[] Values(Vector3 v) => new[]{v.x,v.y,v.z};
    private static bool Finite(Vector3 v) => !float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
    private static bool AllowedDependency(string path) => path.StartsWith(Folder+"/",StringComparison.Ordinal)||path.StartsWith("Packages/com.unity.render-pipelines.",StringComparison.Ordinal)||path.StartsWith("Packages/com.unity.shadergraph/",StringComparison.Ordinal)||path=="Resources/unity_builtin_extra"||path=="Library/unity default resources";
    private static string Hash(string path) { using(var algorithm=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-","").ToLowerInvariant(); }
    private static void CaptureLog(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);else if(type==LogType.Warning&&warnings.Count<100)warnings.Add(message); }
    private static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    private static void WriteReport() { report.errors=errors.Distinct().ToArray();report.warnings=warnings.Distinct().ToArray();Directory.CreateDirectory(Path.GetDirectoryName(config.reportPath));File.WriteAllText(config.reportPath,JsonUtility.ToJson(report,true),Encoding.UTF8); }
}
#endif
