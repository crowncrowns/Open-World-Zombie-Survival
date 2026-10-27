using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdvancedCableTool
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CableGenerator : MonoBehaviour
    {
        public CableType cableType;
        public CableWear wearLevel = CableWear.Used;
        [Min(.002f)] public float diameter = .035f;
        [Min(.05f)] public float cableLength = 8;
        [Min(0)] public float slack = 1;
        public Vector3 gravity = new Vector3(0, -9.81f, 0);
        [Min(0)] public float windStrength = 1;
        public Vector3 windDirection = Vector3.right;
        [Range(0, 1)] public float stiffness = .9f;
        [Range(0, 10)] public float damping = 1;
        [Range(8, 256)] public int segmentCount = 64;
        public bool physicsEnabled;
        public bool collisionEnabled = true;
        public LayerMask collisionMask = ~(1 << 2);
        [Min(.01f)] public float uvTiling = 5;
        public Material material;
        public CableMaterialLibrary materialLibrary;
        public bool usePresetMaterial = true;
        public bool clamps = true;
        [Min(1)] public float simulationDistance = 60;
        public Transform simulationObserver;
        [Range(8, 256)] public int physicsSegmentLimit = 64;
        public static int GlobalSegmentBudget = 4096;
        public List<CableAnchor> anchors = new List<CableAnchor>();
        [SerializeField, HideInInspector] Vector3[] savedShape;
        [SerializeField, HideInInspector] float savedShapeLength;
        [SerializeField, HideInInspector] List<int> pinIndices = new List<int>();
        [SerializeField, HideInInspector] string status;
        [SerializeField, HideInInspector] bool initializedAnchors;
        public string Status => status;
        public Vector3[] Points => solver != null ? solver.positions : points;
        public float SimulationTime { get; private set; }
        public bool IsSimulating { get; private set; }
        public float MeasuredLength { get { float l = 0; var p = Points; if (p != null) for (int i = 1; i < p.Length; ++i) l += Vector3.Distance(p[i-1], p[i]); return l; } }
        CablePhysics solver;
        Vector3[] points, anchorCache;
        bool[] pinned;
        float[] rest;
        CableMeshBuilder[] builders;
        MeshFilter[] filters;
        MeshRenderer[] renderers;
        LODGroup group;
        bool dirty = true;
        bool meshDirty;
        static int budgetFrame = -1, budgetCursor;
        static readonly List<CableGenerator> activeCables = new List<CableGenerator>();
        int budgetGrantedFrame = -1;
        int simulationFrame = -1, simulationStepsThisFrame;
        readonly Collider[] validationHits = new Collider[32];
        float lastSlack;
        float lastBuiltLength;
        Material lastMaterial;
        Material[] materialSlots;
        readonly Material[] singleMaterialSlot = new Material[1];
        [NonSerialized] public bool editorSimulation;

        void OnEnable() { dirty = true; lastSlack = slack; if (!activeCables.Contains(this)) activeCables.Add(this); }
        void OnValidate()
        {
            diameter = Mathf.Clamp(diameter, .002f, 2); cableLength = Mathf.Clamp(cableLength, .05f, 10000);
            segmentCount = Mathf.Clamp(segmentCount, 8, 256); slack = Mathf.Max(0, slack);
            if (Mathf.Abs(slack - lastSlack) > .00001f) { cableLength = Mathf.Max(.05f, cableLength + slack - lastSlack); lastSlack = slack; }
            dirty = true;
        }
        public void Rebuild() { dirty = true; EnsureUpdated(); }
        public void RestoreAuthoredShape()
        {
            solver?.Dispose(); solver = null; points = null; lastBuiltLength = 0;
            dirty = true; EnsureUpdated();
        }
        void Update() { EnsureUpdated(); }
        void LateUpdate() { if (Application.isPlaying && meshDirty) UpdateMeshes(true); }
        void FixedUpdate() { if (Application.isPlaying) Simulate(Time.fixedDeltaTime); }
        public void PreviewStep(float dt) { Physics.SyncTransforms(); EnsureUpdated(); Simulate(Mathf.Clamp(dt, .001f, .0333f)); if (IsSimulating) UpdateMeshes(); }
        public void CommitShape()
        {
            if (Points == null) return;
            savedShape = new Vector3[Points.Length];
            savedShapeLength = cableLength;
            for (int i = 0; i < Points.Length; ++i) savedShape[i] = transform.InverseTransformPoint(Points[i]);
        }
        public void DetachEndB() { if (anchors.Count > 0) { anchors[anchors.Count - 1].Detach(); dirty = true; } }
        public void DetachAll() { foreach (var a in anchors) a.Detach(); dirty = true; }
        public void AttachEndB(Vector3 position, Transform support)
        {
            if (anchors.Count < 2) anchors.Add(new CableAnchor());
            anchors[anchors.Count - 1].Attach(position, support); dirty = true;
        }
        public void AddImpulse(Vector3 point, Vector3 velocity, float radius = 1) { solver?.AddImpulse(point, velocity, radius); }
        public void CopySettingsFrom(CableGenerator source)
        {
            cableType = source.cableType; wearLevel = source.wearLevel; diameter = source.diameter;
            cableLength = source.cableLength; slack = source.slack; lastSlack = slack; gravity = source.gravity;
            windStrength = source.windStrength; windDirection = source.windDirection; stiffness = source.stiffness;
            damping = source.damping; segmentCount = source.segmentCount; physicsEnabled = source.physicsEnabled;
            collisionEnabled = source.collisionEnabled; collisionMask = source.collisionMask; uvTiling = source.uvTiling;
            material = source.material; materialLibrary = source.materialLibrary; usePresetMaterial = source.usePresetMaterial;
            clamps = source.clamps; simulationDistance = source.simulationDistance;
            physicsSegmentLimit = source.physicsSegmentLimit; dirty = true;
        }
        public void EnsureUpdated()
        {
            if (usePresetMaterial && materialLibrary) material = materialLibrary.Get(cableType, wearLevel);
            if (material != lastMaterial) { dirty = true; lastMaterial = material; }
            if (anchors.Count == 0)
            {
                var shape = Points;
                Vector3 first = shape != null && shape.Length > 0 ? shape[0] : transform.position + Vector3.up * 3;
                Vector3 last = shape != null && shape.Length > 0 ? shape[shape.Length - 1] : first + gravity.normalized * cableLength;
                anchors.Add(new CableAnchor { attached = !initializedAnchors, worldPoint = first });
                anchors.Add(new CableAnchor { attached = false, worldPoint = last });
            }
            if (anchors.Count == 1) anchors.Add(new CableAnchor { attached = false, worldPoint = anchors[0].Resolve() + gravity.normalized * cableLength });
            initializedAnchors = true;
            bool moved = anchorCache == null || anchorCache.Length != anchors.Count;
            if (moved) anchorCache = new Vector3[anchors.Count];
            for (int i = 0; i < anchors.Count; ++i)
            {
                bool was = anchors[i].attached; Vector3 p = anchors[i].Resolve();
                if (p != anchorCache[i] || was != anchors[i].attached) moved = true;
                anchorCache[i] = p;
            }
            if (moved && solver != null && (Application.isPlaying || editorSimulation) && !dirty)
            {
                for (int i = 0; i < anchors.Count && i < pinIndices.Count; ++i)
                { int index = pinIndices[i]; solver.pinned[index] = anchors[i].attached; solver.pins[index] = anchorCache[i]; }
                UpdateClamps(); ValidatePlacement();
            }
            else if (moved) dirty = true;
            if (!dirty) return;
            if (savedShape != null && Mathf.Abs(savedShapeLength - cableLength) > .0001f) savedShape = null;
            bool lengthChanged = lastBuiltLength > 0 && Mathf.Abs(lastBuiltLength - cableLength) > .0001f;
            Vector3[] prior = !lengthChanged && Points != null ? (Vector3[])Points.Clone() : null;
            solver?.Dispose(); solver = null;
            BuildCurve(prior); EnsureRenderers(); UpdateMeshes(); ValidatePlacement(); UpdateClamps(); dirty = false; lastBuiltLength = cableLength; lastSlack = slack;
        }
        void BuildCurve(Vector3[] prior)
        {
            if (anchors.Count == 0)
            {
                anchors.Add(new CableAnchor { worldPoint = transform.position + Vector3.up * 3 });
                anchors.Add(new CableAnchor { attached = false, worldPoint = transform.position });
            }
            if (anchors.Count == 1) anchors.Add(new CableAnchor { attached = false, worldPoint = anchors[0].worldPoint + gravity.normalized * cableLength });
            int spans = anchors.Count - 1;
            int n = Mathf.Max(spans * 2, Mathf.Min(segmentCount, physicsEnabled ? physicsSegmentLimit : segmentCount));
            n = Mathf.CeilToInt((float)n / spans) * spans;
            int perSpan = n / spans;
            points = new Vector3[n + 1]; pinned = new bool[n + 1]; rest = new float[n]; pinIndices.Clear();
            float chordSum = 0;
            for (int s = 0; s < spans; ++s) chordSum += Vector3.Distance(anchorCache[s], anchorCache[s + 1]);
            int attachedCount = 0; foreach (var a in anchors) if (a.attached) attachedCount++;
            for (int s = 0; s < spans; ++s)
            {
                Vector3 a = anchorCache[s], b = anchorCache[s + 1];
                float chord = Vector3.Distance(a, b);
                float length = cableLength * (chordSum > .0001f ? chord / chordSum : 1f / spans);
                for (int j = 0; j <= perSpan; ++j)
                {
                    int index = s * perSpan + j; float t = (float)j / perSpan;
                    bool frozen = !physicsEnabled && savedShape != null && savedShape.Length == points.Length;
                    if (frozen)
                        for (int ai = 0; ai < anchors.Count; ++ai)
                            if (anchors[ai].attached && Vector3.Distance(transform.TransformPoint(savedShape[ai * perSpan]), anchorCache[ai]) > .002f) { frozen = false; break; }
                    if (frozen) points[index] = transform.TransformPoint(savedShape[index]);
                    else if (anchors[s].attached && anchors[s + 1].attached) points[index] = CableCatenary.Point(a, b, length, gravity, t);
                    else if (prior != null && prior.Length > 1) points[index] = SampleShape(prior, (float)index / n);
                    else if (savedShape != null && savedShape.Length > 1) points[index] = transform.TransformPoint(SampleShape(savedShape, (float)index / n));
                    else if (anchors[s].attached) points[index] = a + gravity.normalized * length * t;
                    else if (anchors[s + 1].attached) points[index] = b + gravity.normalized * length * (1 - t);
                    else points[index] = Vector3.Lerp(a, b, t) + gravity.normalized * Mathf.Sin(t * Mathf.PI) * length * .1f;
                    if (j < perSpan) rest[index] = length / perSpan;
                }
            }
            for (int i = 0; i < anchors.Count; ++i)
            {
                int index = i * perSpan; pinIndices.Add(index); pinned[index] = anchors[i].attached;
                if (pinned[index]) points[index] = anchorCache[i];
            }
            if (prior == null && savedShape == null && attachedCount == 1 && collisionEnabled)
            {
                Physics.SyncTransforms();
                int fixedIndex = Array.FindIndex(pinned, p => p);
                // Construct a collision-safe initial free tail, turning along the contact plane.
                for (int directionSign = -1; directionSign <= 1; directionSign += 2)
                {
                    Vector3 direction = gravity.sqrMagnitude > .0001f ? gravity.normalized : Vector3.down;
                    for (int index = fixedIndex + directionSign; index >= 0 && index < points.Length; index += directionSign)
                    {
                        int previousIndex = index - directionSign;
                        float segmentLength = rest[Mathf.Min(index, previousIndex)];
                        Vector3 origin = points[previousIndex];
                        if (gameObject.scene.GetPhysicsScene().SphereCast(origin, diameter * .5f + .002f, direction, out RaycastHit hit, segmentLength, collisionMask, QueryTriggerInteraction.Ignore))
                        {
                            Vector3 tangent = Vector3.ProjectOnPlane(Vector3.right, hit.normal).normalized;
                            if (tangent.sqrMagnitude < .01f) tangent = Vector3.ProjectOnPlane(Vector3.forward, hit.normal).normalized;
                            points[index] = origin + direction * Mathf.Max(0, hit.distance - .002f) + hit.normal * .002f + tangent * Mathf.Max(0, segmentLength - hit.distance);
                            direction = tangent;
                        }
                        else points[index] = origin + direction * segmentLength;
                    }
                }
            }
            if (physicsEnabled || editorSimulation) { solver = new CablePhysics(); solver.Reset(points, pinned, rest); }
        }
        static Vector3 SampleShape(Vector3[] shape, float fraction)
        {
            float f = Mathf.Clamp01(fraction) * (shape.Length - 1); int i = Mathf.FloorToInt(f);
            return Vector3.Lerp(shape[i], shape[Mathf.Min(i + 1, shape.Length - 1)], f - i);
        }
        public void Simulate(float dt)
        {
            IsSimulating = false;
            if (!physicsEnabled || solver == null || (!Application.isPlaying && !editorSimulation)) return;
            Transform observer = simulationObserver;
            if (!observer && Application.isPlaying && Camera.main) observer = Camera.main.transform;
            if (observer && Vector3.Distance(observer.position, transform.TransformPoint(filters[0].sharedMesh.bounds.center)) > simulationDistance) return;
            if (Application.isPlaying)
            {
                if (simulationFrame != Time.frameCount) { simulationFrame = Time.frameCount; simulationStepsThisFrame = 0; }
                // A slow frame must not launch an unbounded FixedUpdate catch-up spiral.
                if (simulationStepsThisFrame >= 2) return;
                ScheduleBudget();
                if (budgetGrantedFrame != Time.frameCount) return;
                ++simulationStepsThisFrame;
            }
            SimulationTime += dt; solver.Step(this, dt); IsSimulating = true;
            meshDirty = true;
            if (solver.congested) status = "Контакт ограничен геометрией: добавьте промежуточную опору или увеличьте детализацию.";
        }
        static void ScheduleBudget()
        {
            if (budgetFrame == Time.frameCount) return;
            budgetFrame = Time.frameCount;
            int remaining = Mathf.Max(0, GlobalSegmentBudget), count = activeCables.Count;
            for (int visited = 0; visited < count; ++visited)
            {
                if (count == 0) return;
                int index = budgetCursor % count; budgetCursor = (index + 1) % count;
                var c = activeCables[index];
                if (!c || !c.physicsEnabled || c.solver == null) continue;
                Transform observer = c.simulationObserver;
                if (!observer && Camera.main) observer = Camera.main.transform;
                if (observer && Vector3.Distance(observer.position, c.solver.positions[c.solver.positions.Length / 2]) > c.simulationDistance) continue;
                int cost = c.solver.positions.Length - 1;
                if (cost > remaining) { budgetCursor = index; break; }
                remaining -= cost; c.budgetGrantedFrame = Time.frameCount;
            }
        }
        void EnsureRenderers()
        {
            if (builders != null && filters != null && filters[0]) return;
            builders = new CableMeshBuilder[3]; filters = new MeshFilter[3]; renderers = new MeshRenderer[3];
            LOD[] lods = new LOD[3];
            for (int i = 0; i < 3; ++i)
            {
                Transform child = transform.Find("Cable_LOD" + i);
                if (!child) { var go = new GameObject("Cable_LOD" + i); go.transform.SetParent(transform, false); child = go.transform; }
                child.localPosition = Vector3.zero; child.localRotation = Quaternion.identity; child.localScale = Vector3.one;
                filters[i] = child.GetComponent<MeshFilter>(); if (!filters[i]) filters[i] = child.gameObject.AddComponent<MeshFilter>();
                renderers[i] = child.GetComponent<MeshRenderer>(); if (!renderers[i]) renderers[i] = child.gameObject.AddComponent<MeshRenderer>();
                builders[i] = new CableMeshBuilder();
                lods[i] = new LOD(new[] { .12f, .045f, .009f }[i], new Renderer[] { renderers[i] });
            }
            group = GetComponent<LODGroup>(); if (!group) group = gameObject.AddComponent<LODGroup>(); group.SetLODs(lods);
        }
        void UpdateMeshes(bool runtime = false)
        {
            if (Points == null || builders == null) return;
            for (int i = 0; i < 3; ++i)
            {
                // The active renderer updates immediately. Invisible lower LODs only need a
                // periodic refresh, and a rendered frame without a physics step needs none.
                if (runtime && i > 0 && !renderers[i].isVisible && Time.frameCount % 8 != 0) continue;
                filters[i].sharedMesh = builders[i].Build(Points, transform, diameter, 1 << i,
                    i == 0 ? 12 : i == 1 ? 8 : 5, uvTiling, cableType == CableType.Steel, cableType == CableType.Damaged, cableType == CableType.Industrial);
                if (cableType == CableType.Damaged)
                {
                    if (materialSlots == null || materialSlots.Length != 2) materialSlots = new Material[2];
                    materialSlots[0] = material; materialSlots[1] = materialLibrary && materialLibrary.copper ? materialLibrary.copper : material;
                    renderers[i].sharedMaterials = materialSlots;
                }
                else { singleMaterialSlot[0] = material; renderers[i].sharedMaterials = singleMaterialSlot; }
            }
            group.RecalculateBounds();
            meshDirty = false;
        }
        public bool ValidatePlacement()
        {
            if (!Application.isPlaying) Physics.SyncTransforms();
            status = "";
            float minimum = 0;
            for (int i = 1; i < anchors.Count; ++i)
                if (anchors[i-1].attached && anchors[i].attached) minimum += Vector3.Distance(anchors[i-1].Resolve(), anchors[i].Resolve());
            if (minimum > cableLength + .001f) { status = "Опоры дальше заданной длины. Увеличьте Cable Length: кабель физически не может соединить их."; return false; }
            if (!collisionEnabled || Points == null) return true;
            PhysicsScene scene = gameObject.scene.GetPhysicsScene();
            for (int i = 1; i < Points.Length; ++i)
            {
                Vector3 delta = Points[i] - Points[i-1];
                if (scene.OverlapCapsule(Points[i-1], Points[i], diameter * .5f, validationHits, collisionMask, QueryTriggerInteraction.Ignore) > 0
                    || scene.SphereCast(Points[i-1], diameter * .5f, delta.normalized, out _, delta.magnitude, collisionMask, QueryTriggerInteraction.Ignore))
                { status = "Путь пересекает препятствие. Добавьте промежуточную опору; размещение не подтверждено."; return false; }
            }
            return true;
        }
        void UpdateClamps()
        {
            Transform root = transform.Find("Cable_Clamps");
            if (!root) { var go = new GameObject("Cable_Clamps"); go.transform.SetParent(transform, false); root = go.transform; }
            for (int i = root.childCount - 1; i >= 0; --i) root.GetChild(i).gameObject.SetActive(false);
            if (!clamps) return;
            for (int i = 0; i < anchors.Count; ++i)
            {
                if (!anchors[i].attached) continue;
                Transform clamp;
                if (i < root.childCount) clamp = root.GetChild(i);
                else
                {
                    GameObject go = new GameObject("Mount clamp " + i);
                    go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>();
                    go.transform.SetParent(root, false); clamp = go.transform;
                }
                clamp.gameObject.SetActive(true); clamp.position = anchors[i].Resolve();
                int index = pinIndices[i]; Vector3 tangent = Points[Mathf.Min(index + 1, Points.Length - 1)] - Points[Mathf.Max(0, index - 1)];
                if (tangent.sqrMagnitude > .00001f) clamp.rotation = Quaternion.FromToRotation(Vector3.up, tangent.normalized);
                clamp.localScale = Vector3.one * diameter;
                Material metal = materialLibrary ? materialLibrary.Get(CableType.Steel, CableWear.Used) : material;
                clamp.GetComponent<MeshFilter>().sharedMesh = CableMountMesh.Get();
                clamp.GetComponent<Renderer>().sharedMaterial = metal;
                Transform plate = clamp.Find("Mounting tab");
                if (!plate)
                {
                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Mounting tab";
                    Collider collider = go.GetComponent<Collider>(); if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
                    go.transform.SetParent(clamp, false); plate = go.transform;
                }
                plate.localPosition = new Vector3(0, 0, .7f); plate.localRotation = Quaternion.identity;
                plate.localScale = new Vector3(1.9f, .22f, .5f); plate.GetComponent<Renderer>().sharedMaterial = metal;
            }
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            foreach (var a in anchors) { Gizmos.color = a.attached ? Color.cyan : Color.yellow; Gizmos.DrawWireSphere(a.Resolve(), Mathf.Max(.05f, diameter)); }
        }
        void OnDisable() { activeCables.Remove(this); solver?.Dispose(); solver = null; IsSimulating = false; }
        void OnDestroy() { if (builders != null) foreach (var b in builders) b?.Dispose(); }
    }
}
