using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdvancedCableTool.Editor
{
    // Query-only copies of visible triangles. Never adds a Collider to the source building.
    [InitializeOnLoad]
    public static class CableSurfaceQuery
    {
        sealed class Entry
        {
            public MeshRenderer source;
            public Mesh original, copy;
            public MeshCollider collider;
            public Matrix4x4 matrix;
        }
        static Scene preview;
        static readonly Dictionary<MeshRenderer, Entry> entries = new Dictionary<MeshRenderer, Entry>();
        static readonly Dictionary<Collider, Transform> supports = new Dictionary<Collider, Transform>();
        static readonly Collider[] overlaps = new Collider[32];
        static string failure;
        static CableSurfaceQuery()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
            Undo.undoRedoPerformed += Clear;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) Clear(); };
        }
        public static void Clear()
        {
            foreach (var e in entries.Values)
            { if (e.collider) UnityEngine.Object.DestroyImmediate(e.collider.gameObject); if (e.copy) UnityEngine.Object.DestroyImmediate(e.copy); }
            entries.Clear(); supports.Clear();
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            preview = default;
        }
        static bool Visible(MeshRenderer r, Scene scene)
        {
            if (!r || !r.enabled || !r.gameObject.activeInHierarchy || r.gameObject.scene != scene
                || EditorUtility.IsPersistent(r) || r.GetComponentInParent<CableGenerator>()
                || r.GetComponentInParent<Rigidbody>() || r.GetComponentInParent<Animator>()
                || SceneVisibilityManager.instance.IsHidden(r.gameObject)) return false;
            var group = r.GetComponentInParent<LODGroup>();
            if (group)
            {
                var lods = group.GetLODs();
                if (lods.Length > 0 && Array.IndexOf(lods[0].renderers, r) < 0) return false;
            }
            return true;
        }
        static void Refresh(Scene scene, int mask, Func<Bounds, bool> needed, bool force)
        {
            failure = null;
            if (!preview.IsValid()) { preview = EditorSceneManager.NewPreviewScene(); preview.name = "Cable surface query"; }
            var retired = new List<MeshRenderer>();
            foreach (var pair in entries)
                if (!pair.Key || pair.Key.gameObject.scene != scene) retired.Add(pair.Key);
            foreach (var key in retired)
            {
                var e = entries[key];
                if (e.collider) { supports.Remove(e.collider); UnityEngine.Object.DestroyImmediate(e.collider.gameObject); }
                if (e.copy) UnityEngine.Object.DestroyImmediate(e.copy); entries.Remove(key);
            }
            foreach (var e in entries.Values) if (e.collider) e.collider.enabled = false;
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>())
            {
                if (!Visible(r, scene) || (mask & (1 << r.gameObject.layer)) == 0 || !needed(r.bounds)) continue;
                MeshFilter filter = r.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                Mesh original = filter.sharedMesh; Matrix4x4 matrix = r.transform.localToWorldMatrix;
                if (entries.TryGetValue(r, out var cached) && cached.collider && !force && cached.original == original && cached.matrix == matrix)
                { cached.collider.gameObject.layer = r.gameObject.layer; cached.collider.enabled = true; continue; }
                if (cached != null)
                {
                    if (cached.collider) { supports.Remove(cached.collider); UnityEngine.Object.DestroyImmediate(cached.collider.gameObject); }
                    if (cached.copy) UnityEngine.Object.DestroyImmediate(cached.copy);
                    entries.Remove(r);
                }
                GameObject go = null; Mesh mesh = null;
                try
                {
                    // Editor API supports imported non-readable meshes; bake the complete matrix including shear/mirroring.
                    using (var arrays = MeshUtility.AcquireReadOnlyMeshData(original))
                    {
                        var data = arrays[0];
                        using (var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                        {
                            data.GetVertices(vertices); var world = new Vector3[vertices.Length];
                            for (int i = 0; i < world.Length; ++i) world[i] = matrix.MultiplyPoint3x4(vertices[i]);
                            var triangles = new List<int>();
                            for (int sub = 0; sub < data.subMeshCount; ++sub)
                            {
                                var desc = data.GetSubMesh(sub); if (desc.topology != MeshTopology.Triangles) continue;
                                using (var indices = new NativeArray<int>(desc.indexCount, Allocator.Temp))
                                {
                                    data.GetIndices(indices, sub);
                                    for (int i = 0; i + 2 < indices.Length; i += 3)
                                    {
                                        int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                                    }
                                }
                            }
                            if (triangles.Count == 0) continue;
                            mesh = new Mesh { name = "Cable query triangles", hideFlags = HideFlags.HideAndDontSave,
                                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                            mesh.vertices = world; mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
                        }
                    }
                    go = new GameObject("Cable surface proxy") { hideFlags = HideFlags.HideAndDontSave, layer = r.gameObject.layer };
                    SceneManager.MoveGameObjectToScene(go, preview);
                    var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh;
                    entries[r] = new Entry { source = r, original = original, copy = mesh, collider = mc, matrix = matrix };
                    supports[mc] = r.transform;
                }
                catch (Exception ex)
                {
                    if (go) UnityEngine.Object.DestroyImmediate(go); if (mesh) UnityEngine.Object.DestroyImmediate(mesh);
                    failure = "Не удалось проверить геометрию стены «" + r.name + "»: " + ex.Message;
                }
            }
            Physics.SyncTransforms();
        }
        public static Transform SourceTransform(RaycastHit hit) => hit.collider && supports.TryGetValue(hit.collider, out var source) ? source : hit.transform;
        public static bool Raycast(Scene scene, Ray ray, out RaycastHit hit)
        {
            const int mask = ~(1 << 2);
            Refresh(scene, mask, b => b.IntersectRay(ray), false);
            bool found = scene.GetPhysicsScene().Raycast(ray.origin, ray.direction, out hit, 10000, mask, QueryTriggerInteraction.Ignore);
            if (preview.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var visibleHit, found ? hit.distance + .001f : 10000, mask, QueryTriggerInteraction.Ignore))
            { hit = visibleHit; found = true; }
            return found;
        }
        public static bool Validate(CableGenerator cable, out string message, bool force = true)
        {
            message = null; if (!cable.collisionEnabled || cable.Points == null) return true;
            float radius = cable.diameter * (cable.cableType == CableType.Industrial ? .535f : .5f);
            return ValidatePath(cable.gameObject.scene, cable.Points, radius, cable.collisionMask, out message, force);
        }
        public static bool ValidatePath(Scene scene, Vector3[] points, float radius, int mask, out string message, bool force = false)
        {
            message = null; Bounds bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            bounds.Expand(radius * 2 + .01f);
            Refresh(scene, mask, b => b.Intersects(bounds), force);
            if (!string.IsNullOrEmpty(failure)) { message = failure; return false; }
            var physics = preview.GetPhysicsScene();
            for (int i = 1; i < points.Length; ++i)
            {
                Vector3 delta = points[i] - points[i - 1];
                if (physics.OverlapCapsule(points[i - 1], points[i], radius, overlaps, mask, QueryTriggerInteraction.Ignore) > 0
                    || physics.SphereCast(points[i - 1], radius, delta.normalized, out _, delta.magnitude, mask, QueryTriggerInteraction.Ignore)
                    || physics.SphereCast(points[i], radius, -delta.normalized, out _, delta.magnitude, mask, QueryTriggerInteraction.Ignore))
                { message = "Кабель пересекает видимую стену. Размещение отменено; добавьте промежуточную опору или уменьшите провисание."; return false; }
            }
            return true;
        }
    }
}
