using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Editor-only quality configuration; collision is independent of visual LOD.</summary>
public static class KursHubQualitySetup
{
    private const string Folder = "Assets/Environment/KursHub";
    private const string Lod1Name = "Quality LOD1";
    private const string Lod2Name = "Quality LOD2";
    private const string BuildingLod1Name = "Quality Building LOD1";
    private const string BuildingLod2Name = "Quality Building LOD2";
    private const string CollisionName = "Quality collision";
    private const string CollisionManifestPath = Folder + "/Props/KursHubColliderManifest.json";
    public const string DemoPath = Folder + "/Scenes/KursHubDemo.unity";

    [Serializable] private sealed class Chunk { public string name; public string material; public bool hidden; }
    [Serializable] private sealed class BuildingManifest { public Chunk[] chunks; }
    [Serializable] private sealed class CollisionBox { public string source; public float[] centre; public float[] size; public float[] rotation; }
    [Serializable] private sealed class CollisionChunk { public string name; public bool allBoxes; public int remainingSolids; public CollisionBox[] boxes; }
    [Serializable] private sealed class CollisionProp { public string id; public CollisionChunk[] chunks; }
    [Serializable] private sealed class CollisionManifest { public CollisionProp[] props; }
    private static CollisionManifest collisionCache;
    private static long collisionCacheStamp;
    private static long collisionCacheLength;
    [Serializable] private sealed class AssetAudit
    {
        public string path;
        public string guid;
        public int renderers;
        public long allLevelTriangles;
        public long lod0Triangles;
        public long lod1Triangles;
        public long lod2Triangles;
        public int lodGroups;
        public int enabledMeshColliders;
        public int enabledBoxColliders;
        public int retainedDisabledMeshColliders;
        public int invalidTransforms;
        public int missingScripts;
        public int missingMaterialSlots;
        public int incompatibleShaders;
        public int meshesWithoutUv0;
        public int meshesWithoutUv2;
        public int missingNormals;
        public string[] errors;
    }
    [Serializable] private sealed class AuditReport
    {
        public string unityVersion;
        public string renderPipeline;
        public string generatedUtc;
        public string scope;
        public AssetAudit[] assets;
        public string[] dependencies;
        public string[] externalDependencies;
        public string[] warnings;
    }

    /// <summary>
    /// Use a primitive only for an exact cuboid or a wholly proved compound.
    /// Existing MeshCollider IDs are retained, disabled when boxes are used.
    /// </summary>
    public static void ConfigureCollider(MeshRenderer renderer, bool required, GameObject propRoot = null, string propId = null)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        var filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("Нет mesh: " + renderer.name);
        var colliders = renderer.GetComponents<Collider>();
        if (colliders.Any(c => !(c is MeshCollider) && !(c is BoxCollider))
            || colliders.OfType<MeshCollider>().Count() > 1 || colliders.OfType<BoxCollider>().Count() > 1)
            throw new InvalidOperationException("Неизвестная авторская коллизия сохранена без замены: " + renderer.name);
        if (!required)
        {
            if (colliders.Length != 0)
                throw new InvalidOperationException("Неожиданная коллизия на декоративной части: " + renderer.name);
            return;
        }
        var meshCollider = renderer.GetComponent<MeshCollider>();
        if (meshCollider == null) meshCollider = renderer.gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = filter.sharedMesh;
        meshCollider.convex = false;
        meshCollider.isTrigger = false;
        var box = renderer.GetComponent<BoxCollider>();
        var proof = ProvenBoxes(propId, renderer.name);
        if (proof != null)
        {
            if (propRoot == null) throw new InvalidOperationException("Нет корня для compound collision: " + renderer.name);
            var container = EnsureChild(propRoot.transform, CollisionName);
            container.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); container.localScale = Vector3.one;
            var parent = EnsureChild(container, renderer.name);
            parent.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); parent.localScale = Vector3.one;
            parent.gameObject.SetActive(true);
            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in proof.boxes)
            {
                ValidateBoxRecord(item);
                if (!expected.Add(item.source)) throw new InvalidOperationException("Повторное имя OBB: " + item.source);
                var node = EnsureChild(parent, item.source);
                if (node.GetComponents<Component>().Any(c => !(c is Transform) && !(c is BoxCollider)))
                    throw new InvalidOperationException("Авторский объект в compound collision: " + item.source);
                node.gameObject.SetActive(true);
                node.localPosition = ArrayVector(item.centre);
                node.localRotation = ArrayQuaternion(item.rotation);
                node.localScale = Vector3.one;
                var shape = node.GetComponent<BoxCollider>();
                if (shape == null) shape = node.gameObject.AddComponent<BoxCollider>();
                shape.center = Vector3.zero; shape.size = ArrayVector(item.size);
                shape.enabled = true; shape.isTrigger = false;
                shape.sharedMaterial = meshCollider.sharedMaterial; shape.contactOffset = meshCollider.contactOffset;
            }
            // Preserve every earlier component/fileID if a later proof changes.
            for (int i = 0; i < parent.childCount; i++)
                if (!expected.Contains(parent.GetChild(i).name)) parent.GetChild(i).gameObject.SetActive(false);
            if (box != null) box.enabled = false;
            meshCollider.enabled = false;
        }
        else if (IsExactCuboid(filter.sharedMesh))
        {
            if (box == null) box = renderer.gameObject.AddComponent<BoxCollider>();
            box.center = filter.sharedMesh.bounds.center;
            box.size = filter.sharedMesh.bounds.size;
            box.isTrigger = false; box.enabled = true;
            box.sharedMaterial = meshCollider.sharedMaterial;
            box.contactOffset = meshCollider.contactOffset;
            meshCollider.enabled = false;
        }
        else
        {
            meshCollider.enabled = true;
            // A previously proved cuboid may receive new authored geometry later.
            // Keep its component ID but restore exact mesh collision in that case.
            if (box != null) box.enabled = false;
        }
        if (proof == null && propRoot != null)
        {
            var old = CompoundParent(propRoot, renderer.name);
            if (old != null) old.gameObject.SetActive(false);
        }
        ValidateCollider(renderer, required, propRoot, propId);
    }

    public static void ValidateCollider(MeshRenderer renderer, bool required, GameObject propRoot = null, string propId = null)
    {
        var colliders = renderer.GetComponents<Collider>();
        if (!required)
        {
            if (colliders.Length != 0) throw new InvalidOperationException("Лишняя коллизия: " + renderer.name);
            return;
        }
        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        var mc = renderer.GetComponent<MeshCollider>();
        var box = renderer.GetComponent<BoxCollider>();
        if (colliders.Any(c => !(c is MeshCollider) && !(c is BoxCollider))
            || mc == null || mc.sharedMesh != mesh || mc.isTrigger || mc.convex)
            throw new InvalidOperationException("Неверная статичная коллизия: " + renderer.name);
        var proof = ProvenBoxes(propId, renderer.name);
        if (proof != null)
        {
            var parent = propRoot == null ? null : CompoundParent(propRoot, renderer.name);
            if (mc.enabled || (box != null && box.enabled) || parent == null || !parent.gameObject.activeSelf)
                throw new InvalidOperationException("Не включена доказанная compound collision: " + renderer.name);
            if (!IdentityPose(parent) || !IdentityPose(parent.parent) || !parent.parent.gameObject.activeSelf)
                throw new InvalidOperationException("Изменена поза контейнера compound collision: " + renderer.name);
            var active = parent.GetComponentsInChildren<BoxCollider>(false);
            if (active.Length != proof.boxes.Length || active.Any(c => !c.enabled || c.isTrigger))
                throw new InvalidOperationException("Неполная compound collision: " + renderer.name);
            foreach (var item in proof.boxes)
            {
                ValidateBoxRecord(item);
                var node = DirectChild(parent, item.source);
                var shape = node == null ? null : node.GetComponent<BoxCollider>();
                if (shape == null || node.childCount != 0 || Vector3.Distance(node.localPosition, ArrayVector(item.centre)) > .0001f
                    || Quaternion.Angle(node.localRotation, ArrayQuaternion(item.rotation)) > .01f
                    || Vector3.Distance(node.localScale, Vector3.one) > .0001f || shape.center.sqrMagnitude > .00000001f
                    || Vector3.Distance(shape.size, ArrayVector(item.size)) > .0001f)
                    throw new InvalidOperationException("OBB не совпадает с Blender proof: " + renderer.name + "/" + item.source);
            }
            ValidateObbContainment(renderer, propRoot.transform, proof.boxes);
            return;
        }
        var inactive = propRoot == null ? null : CompoundParent(propRoot, renderer.name);
        if (inactive != null && inactive.gameObject.activeSelf)
            throw new InvalidOperationException("Прежняя compound collision должна быть выключена: " + renderer.name);
        if (colliders.Count(c => c.enabled) != 1)
            throw new InvalidOperationException("Неоднозначная рабочая коллизия: " + renderer.name);
        if (box != null && box.enabled)
        {
            if (mc.enabled || box.isTrigger || !IsExactCuboid(mesh)
                || Vector3.Distance(box.center, mesh.bounds.center) > .0001f
                || Vector3.Distance(box.size, mesh.bounds.size) > .0001f)
                throw new InvalidOperationException("BoxCollider не доказан геометрией: " + renderer.name);
        }
        else if (!mc.enabled) throw new InvalidOperationException("Отключена рабочая коллизия: " + renderer.name);
    }

    private static CollisionChunk ProvenBoxes(string propId, string chunkName)
    {
        if (string.IsNullOrEmpty(propId) || !File.Exists(CollisionManifestPath)) return null;
        var file = new FileInfo(CollisionManifestPath);
        if (collisionCache == null || collisionCacheStamp != file.LastWriteTimeUtc.Ticks || collisionCacheLength != file.Length)
        {
            collisionCache = JsonUtility.FromJson<CollisionManifest>(File.ReadAllText(CollisionManifestPath));
            collisionCacheStamp = file.LastWriteTimeUtc.Ticks; collisionCacheLength = file.Length;
        }
        var manifest = collisionCache;
        if (manifest?.props == null) throw new InvalidOperationException("Повреждён collision proof manifest.");
        var records = manifest.props.Where(p => p.id == propId).ToArray();
        if (records.Length == 0) return null; // A newly authored prop without a proof retains exact mesh collision.
        if (records.Length != 1) throw new InvalidOperationException("Повторный prop в collision proof: " + propId);
        var chunks = records[0].chunks?.Where(c => c.name == chunkName).ToArray();
        if (chunks == null) return null; // Legacy source-only proofs cannot safely replace grouped chunk collision.
        if (chunks.Length > 1) throw new InvalidOperationException("Повторный collision chunk: " + propId + "/" + chunkName);
        if (chunks.Length == 0) return null;
        var record = chunks[0];
        return record.allBoxes && record.remainingSolids == 0 && record.boxes != null && record.boxes.Length > 0 && record.boxes.Length <= 12 ? record : null;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        var node = DirectChild(parent, name);
        if (node == null) { node = new GameObject(name).transform; node.SetParent(parent, false); }
        node.gameObject.isStatic = true;
        return node;
    }

    private static Transform CompoundParent(GameObject root, string chunk)
    {
        var parent = DirectChild(root.transform, CollisionName);
        return parent == null ? null : DirectChild(parent, chunk);
    }

    private static Vector3 ArrayVector(float[] a) => new Vector3(a[0], a[1], a[2]);
    private static Quaternion ArrayQuaternion(float[] a) => new Quaternion(a[0], a[1], a[2], a[3]);
    private static bool IdentityPose(Transform t) => t != null && t.localPosition.sqrMagnitude < .00000001f
        && Vector3.Distance(t.localScale, Vector3.one) < .0001f && Quaternion.Angle(t.localRotation, Quaternion.identity) < .01f;
    private static void ValidateBoxRecord(CollisionBox box)
    {
        if (box == null || string.IsNullOrEmpty(box.source) || box.source.Contains("/") || box.source.Contains("\\")
            || box.centre?.Length != 3 || box.size?.Length != 3 || box.rotation?.Length != 4
            || !Finite(ArrayVector(box.centre)) || !FinitePositive(ArrayVector(box.size))
            || box.rotation.Any(v => float.IsNaN(v) || float.IsInfinity(v))
            || Mathf.Abs(box.rotation.Sum(v => v * v) - 1) > .001f)
            throw new InvalidOperationException("Некорректная доказанная OBB из Blender.");
    }

    private static void ValidateObbContainment(MeshRenderer renderer, Transform root, CollisionBox[] boxes)
    {
        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        using (var dataArray = MeshUtility.AcquireReadOnlyMeshData(mesh))
        using (var vertices = new NativeArray<Vector3>(dataArray[0].vertexCount, Allocator.Temp))
        {
            dataArray[0].GetVertices(vertices);
            foreach (var vertex in vertices)
            {
                var point = root.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                bool contained = boxes.Any(box =>
                {
                    var p = Quaternion.Inverse(ArrayQuaternion(box.rotation)) * (point - ArrayVector(box.centre));
                    var half = ArrayVector(box.size) * .5f + Vector3.one * .002f;
                    return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
                });
                if (!contained) throw new InvalidOperationException("Импортированная геометрия выходит за доказанные OBB: " + renderer.name);
            }
        }
    }

    public static void ConfigureProp(GameObject root, string id)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Сборка LOD доступна только вне Play.");
        var shell = DirectChild(root.transform, "Shell");
        if (shell == null) throw new InvalidOperationException("Нет Shell: " + id);
        var lod0 = shell.GetComponentsInChildren<MeshRenderer>(true);
        var model1 = Folder + "/Props/Models/LOD/" + id + "_LOD1.fbx";
        var model2 = Folder + "/Props/Models/LOD/" + id + "_LOD2.fbx";
        if (!HasLodPair(model1, model2)) return;
        var materials = lod0.ToDictionary(r => r.name, r => r.sharedMaterials, StringComparer.Ordinal);
        var lod1 = ConfigureLodModel(root.transform, Lod1Name, model1, materials, null);
        var lod2 = ConfigureLodModel(root.transform, Lod2Name, model2, materials, null);
        var counts = new[] { Triangles(lod0), Triangles(lod1), Triangles(lod2) };
        if (counts[0] <= 0 || counts[1] <= 0 || counts[2] <= 0 || counts[1] >= counts[0] || counts[2] >= counts[1])
            throw new InvalidOperationException("LOD обязан последовательно снижать треугольники: " + id + " " + string.Join("/", counts));
        var group = root.GetComponent<LODGroup>();
        if (group == null) group = root.AddComponent<LODGroup>();
        ConfigureGroup(group, lod0, lod1, lod2, .45f, .18f, .012f);
        ValidateProp(root, id);
    }

    public static void ValidateProp(GameObject root, string id)
    {
        var model1 = Folder + "/Props/Models/LOD/" + id + "_LOD1.fbx";
        var model2 = Folder + "/Props/Models/LOD/" + id + "_LOD2.fbx";
        if (!HasLodPair(model1, model2))
        {
            if (root.GetComponent<LODGroup>() != null)
                throw new InvalidOperationException("LODGroup без исходных LOD-моделей: " + id);
            return;
        }
        ValidateGroup(root, "Shell", Lod1Name, Lod2Name, model1, model2);
    }

    /// <summary>The building LOD never owns props, guard colliders or gameplay objects.</summary>
    public static void ConfigureBuilding(GameObject root)
    {
        var model1 = Folder + "/Models/LOD/KursHubShell_LOD1.fbx";
        var model2 = Folder + "/Models/LOD/KursHubShell_LOD2.fbx";
        if (!HasLodPair(model1, model2)) return;
        var shell = DirectChild(root.transform, "Shell");
        if (shell == null) throw new InvalidOperationException("Нет Shell здания.");
        var manifest = JsonUtility.FromJson<BuildingManifest>(File.ReadAllText(Folder + "/KursHubManifest.json"));
        var chunks = manifest.chunks.ToDictionary(c => c.name, StringComparer.Ordinal);
        var lod0 = shell.GetComponentsInChildren<MeshRenderer>(true).Where(r => !chunks[r.name].hidden).ToArray();
        var materials = shell.GetComponentsInChildren<MeshRenderer>(true)
            .ToDictionary(r => r.name, r => new[] { AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + chunks[r.name].material + ".mat") }, StringComparer.Ordinal);
        var hidden = new HashSet<string>(chunks.Values.Where(c => c.hidden).Select(c => c.name), StringComparer.Ordinal);
        var lod1 = ConfigureLodModel(root.transform, BuildingLod1Name, model1, materials, hidden);
        var lod2 = ConfigureLodModel(root.transform, BuildingLod2Name, model2, materials, hidden);
        var counts = new[] { Triangles(lod0), Triangles(lod1), Triangles(lod2) };
        if (counts[1] <= 0 || counts[2] <= 0 || counts[1] >= counts[0] || counts[2] >= counts[1])
            throw new InvalidOperationException("LOD здания не снижает сложность: " + string.Join("/", counts));
        var group = root.GetComponent<LODGroup>();
        if (group == null) group = root.AddComponent<LODGroup>();
        ConfigureGroup(group, lod0, lod1, lod2, .65f, .25f, .005f);
        ValidateBuilding(root);
    }

    public static void ValidateBuilding(GameObject root)
    {
        var model1 = Folder + "/Models/LOD/KursHubShell_LOD1.fbx";
        var model2 = Folder + "/Models/LOD/KursHubShell_LOD2.fbx";
        if (HasLodPair(model1, model2))
            ValidateGroup(root, "Shell", BuildingLod1Name, BuildingLod2Name, model1, model2);
    }

    private static bool HasLodPair(string model1, string model2)
    {
        var a = File.Exists(model1); var b = File.Exists(model2);
        if (a != b) throw new InvalidOperationException("Неполная пара LOD: " + model1 + " / " + model2);
        return a;
    }

    private static MeshRenderer[] ConfigureLodModel(Transform parent, string name, string path,
        Dictionary<string, Material[]> materials, HashSet<string> hidden)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Не импортирован LOD FBX: " + path);
        bool changed = importer.globalScale != 1 || !importer.bakeAxisConversion || !importer.preserveHierarchy
            || importer.importCameras || importer.importLights || importer.importAnimation || importer.addCollider
            || importer.isReadable || importer.materialImportMode != ModelImporterMaterialImportMode.None || !importer.generateSecondaryUV
            || !Mathf.Approximately(importer.secondaryUVPackMargin, 1)
            || importer.secondaryUVMarginMethod != ModelImporterSecondaryUVMarginMethod.Manual;
        if (changed)
        {
            importer.globalScale = 1; importer.bakeAxisConversion = true; importer.preserveHierarchy = true;
            importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
            importer.addCollider = false; importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.generateSecondaryUV = true;
            importer.secondaryUVPackMargin = 1;
            importer.secondaryUVMarginMethod = ModelImporterSecondaryUVMarginMethod.Manual;
            importer.SaveAndReimport();
        }
        var child = DirectChild(parent, name);
        if (child == null)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            child = ((GameObject)PrefabUtility.InstantiatePrefab(model)).transform;
            child.SetParent(parent, false); child.name = name;
        }
        else if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) != path)
            throw new InvalidOperationException("Авторский LOD-объект имеет другой источник: " + name);
        child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); child.localScale = Vector3.one;
        if (child.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Визуальные LOD не должны содержать коллизию: " + path);
        var renderers = child.GetComponentsInChildren<MeshRenderer>(true);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var renderer in renderers)
        {
            if (!names.Add(renderer.name) || !materials.TryGetValue(renderer.name, out var palette) || palette.Any(m => m == null))
                throw new InvalidOperationException("LOD/material manifest не совпадают: " + path + "/" + renderer.name);
            renderer.sharedMaterials = palette;
            renderer.enabled = hidden == null || !hidden.Contains(renderer.name);
            renderer.gameObject.isStatic = true;
            renderer.shadowCastingMode = palette.Any(m => m.name == "Glass" || m.name == "LampGlass" || m.name == "DirtStain")
                ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }
        child.gameObject.isStatic = true;
        return renderers.Where(r => hidden == null || !hidden.Contains(r.name)).ToArray();
    }

    private static void ConfigureGroup(LODGroup group, MeshRenderer[] a, MeshRenderer[] b, MeshRenderer[] c, float x, float y, float z)
    {
        group.fadeMode = LODFadeMode.CrossFade; group.animateCrossFading = true;
        var lods = new[] { new LOD(x, a), new LOD(y, b), new LOD(z, c) };
        for (int i = 0; i < lods.Length; i++) lods[i].fadeTransitionWidth = .15f;
        group.SetLODs(lods); group.RecalculateBounds();
    }

    private static void ValidateGroup(GameObject root, string shellName, string name1, string name2, string path1, string path2)
    {
        var group = root.GetComponent<LODGroup>();
        var a = DirectChild(root.transform, shellName); var b = DirectChild(root.transform, name1); var c = DirectChild(root.transform, name2);
        if (group == null || a == null || b == null || c == null)
            throw new InvalidOperationException("Неполная структура LOD: " + root.name);
        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(b.gameObject) != path1
            || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(c.gameObject) != path2)
            throw new InvalidOperationException("Потеряна ссылка на LOD FBX: " + root.name);
        if (b.GetComponentsInChildren<Collider>(true).Length != 0 || c.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Коллизия зависит от визуального LOD: " + root.name);
        var lods = group.GetLODs();
        if (lods.Length != 3 || lods.Any(l => l.renderers == null || l.renderers.Length == 0 || l.renderers.Any(r => r == null))
            || lods[0].screenRelativeTransitionHeight <= lods[1].screenRelativeTransitionHeight
            || lods[1].screenRelativeTransitionHeight <= lods[2].screenRelativeTransitionHeight)
            throw new InvalidOperationException("Неверные переходы LOD: " + root.name);
        var levels = new[] { a, b, c };
        for (int i = 0; i < 3; i++)
            foreach (var renderer in lods[i].renderers)
                if (!renderer.transform.IsChildOf(levels[i]))
                    throw new InvalidOperationException("LOD захватывает посторонний объект: " + root.name + "/" + renderer.name);
        var counts = lods.Select(l => Triangles(l.renderers)).ToArray();
        if (counts[1] >= counts[0] || counts[2] >= counts[1])
            throw new InvalidOperationException("LOD не снижает число треугольников: " + root.name);
    }

    private static bool IsExactCuboid(Mesh mesh)
    {
        long total = 0; for (int i = 0; i < mesh.subMeshCount; i++) total += mesh.GetIndexCount(i);
        if (total != 36 || mesh.bounds.size.x <= 0 || mesh.bounds.size.y <= 0 || mesh.bounds.size.z <= 0) return false;
        using (var dataArray = MeshUtility.AcquireReadOnlyMeshData(mesh))
        {
            var data = dataArray[0];
            using (var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
            {
                data.GetVertices(vertices);
                var bounds = mesh.bounds; float epsilon = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) * .00001f + .000001f;
                var corners = new HashSet<int>();
                foreach (var vertex in vertices)
                {
                    int key = 0;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        if (Mathf.Abs(vertex[axis] - bounds.max[axis]) <= epsilon) key |= 1 << axis;
                        else if (Mathf.Abs(vertex[axis] - bounds.min[axis]) > epsilon) return false;
                    }
                    corners.Add(key);
                }
                if (corners.Count != 8) return false;
                var areas = new float[6];
                for (int sm = 0; sm < data.subMeshCount; sm++)
                {
                    var descriptor = data.GetSubMesh(sm);
                    if (descriptor.topology != MeshTopology.Triangles) return false;
                    using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                    {
                        data.GetIndices(indices, sm);
                        for (int i = 0; i < indices.Length; i += 3)
                        {
                            var v0=vertices[indices[i]]; var v1=vertices[indices[i+1]]; var v2=vertices[indices[i+2]];
                            int plane = -1;
                            for (int axis=0;axis<3;axis++)
                            {
                                if (Mathf.Abs(v0[axis]-bounds.min[axis])<=epsilon && Mathf.Abs(v1[axis]-bounds.min[axis])<=epsilon && Mathf.Abs(v2[axis]-bounds.min[axis])<=epsilon) plane=axis*2;
                                if (Mathf.Abs(v0[axis]-bounds.max[axis])<=epsilon && Mathf.Abs(v1[axis]-bounds.max[axis])<=epsilon && Mathf.Abs(v2[axis]-bounds.max[axis])<=epsilon) plane=axis*2+1;
                            }
                            if (plane<0) return false;
                            areas[plane]+=Vector3.Cross(v1-v0,v2-v0).magnitude*.5f;
                        }
                    }
                }
                var expected = new[] { bounds.size.y*bounds.size.z, bounds.size.y*bounds.size.z, bounds.size.x*bounds.size.z, bounds.size.x*bounds.size.z, bounds.size.x*bounds.size.y, bounds.size.x*bounds.size.y };
                for (int i=0;i<6;i++) if (Mathf.Abs(areas[i]-expected[i])>Mathf.Max(expected[i]*.0001f,.000001f)) return false;
                return true;
            }
        }
    }

    private static Transform DirectChild(Transform parent, string name)
    {
        var matches = Enumerable.Range(0,parent.childCount).Select(parent.GetChild).Where(t=>t.name==name).ToArray();
        if (matches.Length>1) throw new InvalidOperationException("Повторное имя: " + parent.name + "/" + name);
        return matches.Length==0 ? null : matches[0];
    }

    private static long Triangles(IEnumerable<Renderer> renderers)
    {
        long count=0;
        foreach(var renderer in renderers)
        {
            var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if(mesh==null) continue;
            for(int sm=0;sm<mesh.subMeshCount;sm++) count+=(long)mesh.GetIndexCount(sm)/3;
        }
        return count;
    }

    [MenuItem("Tools/Character Lab/Audit Kurs Hub Quality")]
    public static void Audit()
    {
        var output = OutputDirectory(); Directory.CreateDirectory(output + "/REPORTS");
        var paths = Directory.GetFiles(Folder+"/Props/Prefabs","*.prefab").Select(p=>p.Replace('\\','/')).OrderBy(p=>p,StringComparer.Ordinal).Append(KursHubSetup.PrefabPath).ToArray();
        var audits = paths.Select(AuditAsset).ToArray();
        var dependencies = AssetDatabase.GetDependencies(paths,true).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        var external = dependencies.Where(p=>!AllowedDependency(p)).ToArray();
        var warnings = new List<string>();
        if(audits.Any(a=>a.meshesWithoutUv2>0)) warnings.Add("Some meshes do not have UV2. Baked lighting must not be claimed validated for those meshes.");
        if(File.Exists(CollisionManifestPath) && JsonUtility.FromJson<CollisionManifest>(File.ReadAllText(CollisionManifestPath))?.props?.Any(p=>p.chunks==null)==true)
            warnings.Add("Legacy collision records lack per-chunk proof. Those chunks retain exact MeshCollider collision.");
        if(audits.Any(a=>a.lodGroups==0 && a.lod0Triangles>200)) warnings.Add("Some significant assets have no LODGroup; inspect the per-asset report.");
        if(external.Length>0) warnings.Add("Dependencies outside KursHub must be reviewed before packaging.");
        var report=new AuditReport { unityVersion=Application.unityVersion,renderPipeline=GraphicsSettings.currentRenderPipeline?.GetType().FullName,
            generatedUtc=DateTime.UtcNow.ToString("o"),scope="KursHub authored building and standalone props only; renderers include all LOD levels, collision counts include enabled components only.",
            assets=audits,dependencies=dependencies,externalDependencies=external,warnings=warnings.ToArray() };
        File.WriteAllText(output+"/REPORTS/Unity_Asset_Audit.json",JsonUtility.ToJson(report,true),Encoding.UTF8);
        var rows=new List<string>{"Asset | LOD0 tris | LOD1 tris | LOD2 tris | Mesh / Box colliders | LOD groups | UV2 missing | Issues","--- | ---: | ---: | ---: | ---: | ---: | ---: | ---"};
        rows.AddRange(audits.Select(a=>Path.GetFileName(a.path)+" | "+a.lod0Triangles+" | "+a.lod1Triangles+" | "+a.lod2Triangles+" | "+a.enabledMeshColliders+" / "+a.enabledBoxColliders+" | "+a.lodGroups+" | "+a.meshesWithoutUv2+" | "+(a.errors.Length==0?"None":string.Join("; ",a.errors))));
        File.WriteAllLines(output+"/REPORTS/Unity_Asset_Audit.md",rows,Encoding.UTF8);
        if(audits.Any(a=>a.errors.Length>0)) throw new InvalidOperationException("Аудит ассетов обнаружил ошибки; см. Unity_Asset_Audit.json.");
        if(Application.unityVersion != "6000.6.3f1")
            throw new InvalidOperationException("Целевая версия проверки — Unity 6000.6.3f1; фактически " + Application.unityVersion);
        if(GraphicsSettings.currentRenderPipeline==null || !GraphicsSettings.currentRenderPipeline.GetType().FullName.Contains("Universal"))
            throw new InvalidOperationException("Для проверки требуется целевой URP-проект.");
        Debug.Log("KURS_QUALITY_AUDIT_OK: "+audits.Length+" prefabs, Unity "+Application.unityVersion);
    }

    private static AssetAudit AuditAsset(string path)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(asset==null) throw new InvalidOperationException("Нет префаба: "+path);
        var renderers=asset.GetComponentsInChildren<MeshRenderer>(true);
        var groups=asset.GetComponentsInChildren<LODGroup>(true);
        var meshes=renderers.Select(r=>r.GetComponent<MeshFilter>()?.sharedMesh).Where(m=>m!=null).Distinct().ToArray();
        var result=new AssetAudit { path=path,guid=AssetDatabase.AssetPathToGUID(path),renderers=renderers.Length,allLevelTriangles=Triangles(renderers),lodGroups=groups.Length,
            enabledMeshColliders=asset.GetComponentsInChildren<MeshCollider>(true).Count(c=>c.enabled && ActiveInsidePrefab(c.transform,asset.transform)),enabledBoxColliders=asset.GetComponentsInChildren<BoxCollider>(true).Count(c=>c.enabled && ActiveInsidePrefab(c.transform,asset.transform)),
            retainedDisabledMeshColliders=asset.GetComponentsInChildren<MeshCollider>(true).Count(c=>!c.enabled),
            invalidTransforms=asset.GetComponentsInChildren<Transform>(true).Count(t=>!FinitePositive(t.localScale) || !Finite(t.localPosition)),
            missingScripts=asset.GetComponentsInChildren<MonoBehaviour>(true).Count(c=>c==null),missingMaterialSlots=renderers.Sum(r=>r.sharedMaterials.Count(m=>m==null)),
            incompatibleShaders=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Count(m=>m.shader==null || m.shader.name!="Universal Render Pipeline/Lit"),
            meshesWithoutUv0=meshes.Count(m=>!m.HasVertexAttribute(VertexAttribute.TexCoord0)),meshesWithoutUv2=meshes.Count(m=>!m.HasVertexAttribute(VertexAttribute.TexCoord1)),
            missingNormals=meshes.Count(m=>!m.HasVertexAttribute(VertexAttribute.Normal)) };
        var owned = new HashSet<Renderer>();
        foreach(var group in groups)
        {
            var lods=group.GetLODs();
            for(int i=0;i<lods.Length;i++)
            {
                var valid=lods[i].renderers.Where(r=>r!=null).ToArray();
                foreach(var r in valid) owned.Add(r);
                if(i==0)result.lod0Triangles+=Triangles(valid);
                if(i==1)result.lod1Triangles+=Triangles(valid);
                if(i==2)result.lod2Triangles+=Triangles(valid);
            }
        }
        result.lod0Triangles+=Triangles(renderers.Where(r=>!owned.Contains(r)));
        var errors=new List<string>();
        if(result.invalidTransforms>0) errors.Add("Non-finite transform or non-positive scale");
        if(result.missingScripts>0)errors.Add("Missing scripts");
        if(result.missingMaterialSlots>0)errors.Add("Missing material slot");
        if(result.incompatibleShaders>0)errors.Add("Unexpected shader");
        if(result.meshesWithoutUv0>0)errors.Add("Missing UV0");
        if(result.missingNormals>0)errors.Add("Missing normals");
        if(renderers.Any(r=>r.GetComponent<MeshFilter>()?.sharedMesh==null))errors.Add("Missing mesh reference");
        if(string.IsNullOrEmpty(result.guid))errors.Add("Missing asset GUID");
        if(asset.GetComponentsInChildren<Rigidbody>(true).Length!=0)errors.Add("Unexpected Rigidbody");
        try
        {
            if(path==KursHubSetup.PrefabPath) ValidateBuilding(asset);
            else
            {
                var id=Path.GetFileNameWithoutExtension(path);
                ValidateProp(asset,id);
                var shell=DirectChild(asset.transform,"Shell");
                if(shell==null) throw new InvalidOperationException("Нет Shell: "+id);
                foreach(var renderer in shell.GetComponentsInChildren<MeshRenderer>(true))
                    ValidateCollider(renderer,renderer.GetComponent<MeshCollider>()!=null,asset,id);
            }
        }
        catch(Exception e){errors.Add(e.Message);}
        result.errors=errors.ToArray(); return result;
    }

    private static bool ActiveInsidePrefab(Transform node, Transform root)
    {
        // Persistent prefab assets are outside an active scene, so their
        // activeInHierarchy is false even when every authored parent is on.
        for (var current=node;current!=null;current=current.parent)
        {
            if (!current.gameObject.activeSelf) return false;
            if (current==root) return true;
        }
        return false;
    }

    private static bool Finite(Vector3 v) => !float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
    private static bool FinitePositive(Vector3 v) => Finite(v)&&v.x>0&&v.y>0&&v.z>0;
    private static string OutputDirectory()
    {
        var specified=Environment.GetEnvironmentVariable("KURS_FINAL_OUTPUT");
        return Path.GetFullPath(string.IsNullOrWhiteSpace(specified)?"FINAL_ASSETS":specified);
    }

    private static bool AllowedDependency(string path) => path.StartsWith(Folder+"/",StringComparison.Ordinal)
        || path.StartsWith("Packages/com.unity.render-pipelines.",StringComparison.Ordinal)
        || path.StartsWith("Packages/com.unity.shadergraph/",StringComparison.Ordinal)
        || path == "Resources/unity_builtin_extra" || path == "Library/unity default resources";

    [MenuItem("Tools/Character Lab/Create Kurs Hub Standalone Demo")]
    public static void CreateDemo()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Демонстрационная сцена создаётся вне Play.");
        if(!AssetDatabase.IsValidFolder(Folder+"/Scenes"))AssetDatabase.CreateFolder(Folder,"Scenes");
        var previous=SceneManager.GetActiveScene();
        var demo=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(demo);
            var hub=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KursHubSetup.PrefabPath),demo);
            hub.name="KursHub";hub.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Demo Ground";floor.isStatic=true;
            floor.transform.position=new Vector3(0,-.2f,0);floor.transform.localScale=new Vector3(260,.2f,300);
            floor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Floor.mat");
            var sun=new GameObject("Daylight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.8f;sun.shadows=LightShadows.Soft;
            sun.transform.rotation=Quaternion.Euler(40,-35,0);sun.color=new Color(1,.94f,.85f);
            RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.18f,.21f,.25f);
            var camera=new GameObject("Demo Camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(-90,43,-115);camera.transform.LookAt(new Vector3(0,6,-1));
            camera.fieldOfView=42;camera.nearClipPlane=.08f;camera.farClipPlane=700;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.36f,.4f,.45f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            if(!EditorSceneManager.SaveScene(demo,DemoPath))throw new InvalidOperationException("Не сохранена демонстрационная сцена.");
        }
        finally
        {
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(demo,true);
        }
        AssetDatabase.SaveAssets();Debug.Log("KURS_DEMO_SCENE_OK: "+DemoPath);
    }

    [MenuItem("Tools/Character Lab/Export Kurs Hub Quality Package")]
    public static void AuditAndExport()
    {
        Audit();
        if (!File.Exists(DemoPath)) CreateDemo();
        var output=OutputDirectory();Directory.CreateDirectory(output);
        var paths=AssetDatabase.FindAssets("",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!AssetDatabase.IsValidFolder(p)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        var dependencies=AssetDatabase.GetDependencies(paths,true);
        var unexpected=dependencies.Where(p=>!AllowedDependency(p)).ToArray();
        if(unexpected.Length!=0)throw new InvalidOperationException("Пакет зависит от посторонних project assets: "+string.Join(", ",unexpected));
        var package=output+"/KursHub_Final.unitypackage";
        AssetDatabase.ExportPackage(paths,package,ExportPackageOptions.IncludeDependencies);
        if(!File.Exists(package)||new FileInfo(package).Length==0)throw new InvalidOperationException("Unity package не создан.");
        File.WriteAllLines(output+"/REPORTS/Unity_Package_Assets.txt",paths,Encoding.UTF8);
        File.WriteAllLines(output+"/REPORTS/Unity_Package_Dependencies.txt",dependencies.OrderBy(p=>p,StringComparer.Ordinal),Encoding.UTF8);
        File.WriteAllText(output+"/REPORTS/Unity_Package_Report.txt","UNITY_PACKAGE_EXPORT_OK\nUnity="+Application.unityVersion+"\nPackage="+package+"\nAssetCount="+paths.Length+"\nStandalone demo="+DemoPath+"\nRequired package=com.unity.render-pipelines.universal 17.6.0\nNo gameplay scripts, CharacterTest scene or player prefab are included.\n",Encoding.UTF8);
        File.WriteAllText(output+"/REPORTS/Unity_Import_Instructions.md",
            "# Importing KursHub\n\nUse Unity 6000.6.3f1 with Universal RP 17.6.0 installed and an active URP renderer. Import KursHub_Final.unitypackage with all dependencies selected; keep the included .meta/GUID files. The package retains the canonical Assets/Environment/KursHub paths, so nested prefab, material and model references remain valid. Open "+DemoPath+" or place "+KursHubSetup.PrefabPath+" in your scene at unit scale.\n\nThe demo contains a static hub, a ground collider, camera and daylight. It does not include a player controller, NPC logic, NavMesh, gameplay or saved-world state. Existing CharacterTest lighting and gameplay are not part of the package. Visual LOD groups do not own colliders; collision remains present at every visual distance.\n\nLoose FBX files are standard geometry interchange exports. The Unity package is the authoritative import route for the configured URP materials, LOD groups, colliders and reusable nested props.\n",Encoding.UTF8);
        Debug.Log("KURS_QUALITY_PACKAGE_OK: "+package);
    }
}
