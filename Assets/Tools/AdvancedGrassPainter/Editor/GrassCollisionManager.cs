using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdvancedGrassPainter.Editor
{
    // Query-only local physics world: no source colliders, transforms or importer flags are modified.
    public sealed class GrassCollisionManager : IDisposable
    {
        sealed class Entry
        {
            public Component source;
            public Collider collider;
            public Matrix4x4 matrix;
            public Bounds bounds;
            public int stamp, layer;
            public float clearance;
            public Mesh baked;
            public bool conservative;
            public GrassSupportGeometry support;
        }
        readonly Scene scene;
        readonly PhysicsScene physics;
        readonly Dictionary<Component, Entry> entries = new Dictionary<Component, Entry>();
        readonly Dictionary<Collider, Entry> byCollider = new Dictionary<Collider, Entry>();
        readonly Dictionary<Vector3Int, List<Entry>> grid = new Dictionary<Vector3Int, List<Entry>>();
        readonly List<Entry> largeEntries = new List<Entry>();
        readonly HashSet<Entry> candidates = new HashSet<Entry>();
        readonly List<Bounds> changes = new List<Bounds>();
        Collider[] overlap = new Collider[64];
        const float Cell = 8;
        public int ConservativeProxies { get; private set; }
        public int Count => entries.Count;
        public GrassCollisionManager()
        {
            scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            scene.name = "Grass Query " + Guid.NewGuid().ToString("N");
            physics = scene.GetPhysicsScene();
            if(physics==Physics.defaultPhysicsScene)throw new InvalidOperationException("Grass query preview must have an isolated physics scene.");
        }
        static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));
        static bool Include(Component source)
        {
            if (!source || !source.gameObject.activeInHierarchy || !source.gameObject.scene.IsValid() || source.gameObject.scene.name.StartsWith("Grass Query ") || EditorUtility.IsPersistent(source)) return false;
            if ((source.hideFlags & HideFlags.DontSave) != 0 || source.GetComponentInParent<GrassDataStorage>()) return false;
            if(UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(source.gameObject.scene))return false;
            var obstacle = source.GetComponentInParent<GrassObstacle>();
            return !obstacle || !obstacle.ignore;
        }
        public IReadOnlyList<Bounds> Refresh(bool force = false)
        {
            changes.Clear(); var seen = new HashSet<Component>(); ConservativeProxies = 0;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Collider>())
                if (Include(c) && c.enabled && !c.isTrigger) Visit(c, seen, force);
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>())
            {
                if (!Include(r) || !r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                var marker = r.GetComponentInParent<GrassObstacle>();
                // Always include visible meshes: collider coverage cannot be inferred from one collider on a parent.
                if (marker && !marker.useRenderGeometry && r.GetComponentInParent<Collider>()) continue;
                Visit(r, seen, force);
            }
            var removed = new List<Component>();
            foreach (var pair in entries) if (!seen.Contains(pair.Key)) { changes.Add(pair.Value.bounds); Destroy(pair.Value); removed.Add(pair.Key); }
            foreach (var id in removed) entries.Remove(id);
            if (changes.Count > 0)
            {
                grid.Clear();largeEntries.Clear(); byCollider.Clear();
                foreach (var e in entries.Values)
                {
                    byCollider.Add(e.collider, e); var a = Key(e.bounds.min); var b = Key(e.bounds.max);
                    // Very large grounds are handled by physics projection, not a duplicated grid entry per cell.
                    if ((long)(b.x-a.x+1)*(b.y-a.y+1)*(b.z-a.z+1) > 50000){largeEntries.Add(e);continue;}
                    for (int x=a.x;x<=b.x;x++) for(int y=a.y;y<=b.y;y++) for(int z=a.z;z<=b.z;z++)
                    { var k = new Vector3Int(x,y,z); if(!grid.TryGetValue(k,out var list)) grid[k]=list=new List<Entry>(); list.Add(e); }
                }
                Physics.SyncTransforms(); physics.Simulate(.001f);
            }
            ConservativeProxies=0;foreach(var e in entries.Values)if(e.conservative)ConservativeProxies++;
            return changes;
        }
        void Visit(Component source, HashSet<Component> seen, bool force)
        {
            var id = source; seen.Add(id);
            var marker = source.GetComponentInParent<GrassObstacle>();
            int stamp = EditorJsonUtility.ToJson(source).GetHashCode() ^ (marker ? EditorJsonUtility.ToJson(marker).GetHashCode() : 0);
            var sourceFilter=source.GetComponent<MeshFilter>();
            if (source is MeshRenderer && sourceFilter && sourceFilter.sharedMesh) stamp ^= sourceFilter.sharedMesh.GetEntityId().GetHashCode();
            Mesh sourceMesh=source is MeshCollider sourceCollider?sourceCollider.sharedMesh:source is MeshRenderer&&sourceFilter?sourceFilter.sharedMesh:null;
            if(sourceMesh){stamp^=sourceMesh.bounds.GetHashCode()^sourceMesh.vertexCount;string path=AssetDatabase.GetAssetPath(sourceMesh);if(!string.IsNullOrEmpty(path))stamp^=AssetDatabase.GetAssetDependencyHash(path).GetHashCode();}
            if(source is SkinnedMeshRenderer skinned)foreach(var bone in skinned.bones)if(bone)stamp^=bone.localToWorldMatrix.GetHashCode();
            var matrix = source.transform.localToWorldMatrix;
            if (!force && entries.TryGetValue(id, out var old) && old.matrix == matrix && old.stamp == stamp && old.layer == source.gameObject.layer) return;
            if (entries.TryGetValue(id, out old)) { changes.Add(old.bounds); Destroy(old); entries.Remove(id); }
            var go = new GameObject("Grass query proxy") { hideFlags = HideFlags.HideAndDontSave, layer = source.gameObject.layer };
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation); go.transform.localScale = source.transform.lossyScale;
            Collider clone;
            Mesh baked = null;
            Vector3 axisX=matrix.GetColumn(0),axisY=matrix.GetColumn(1),axisZ=matrix.GetColumn(2);
            bool conservative=Mathf.Abs(Vector3.Dot(axisX.normalized,axisY.normalized))>.0001f||Mathf.Abs(Vector3.Dot(axisY.normalized,axisZ.normalized))>.0001f||Mathf.Abs(Vector3.Dot(axisX.normalized,axisZ.normalized))>.0001f;
            if(conservative)
            {
                Bounds bounds=source is Collider shape?shape.bounds:((Renderer)source).bounds;
                go.transform.SetPositionAndRotation(bounds.center,Quaternion.identity);go.transform.localScale=Vector3.one;var box=go.AddComponent<BoxCollider>();box.size=bounds.size;clone=box;
            }
            else if (source is Collider original)
            {
                clone = (Collider)go.AddComponent(source.GetType()); EditorUtility.CopySerialized(original, clone); clone.enabled = true;
            }
            else
            {
                Mesh mesh = null;
                if (source is SkinnedMeshRenderer skin) { baked = new Mesh(); skin.BakeMesh(baked); mesh = baked; }
                else { var filter = source.GetComponent<MeshFilter>(); if (filter) mesh = filter.sharedMesh; }
                if (mesh && mesh.vertexCount > 0)
                {
                    var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; clone = mc;
                }
                else
                {
                    var box = go.AddComponent<BoxCollider>(); Bounds bounds = ((Renderer)source).bounds;
                    go.transform.SetPositionAndRotation(bounds.center, Quaternion.identity); go.transform.localScale = Vector3.one;
                    box.size = bounds.size; clone = box; conservative=true;
                }
            }
            var entry = new Entry { source = source, collider = clone, matrix = matrix, stamp = stamp, layer = source.gameObject.layer,
                clearance = marker ? marker.additionalClearance : 0, baked = baked, conservative=conservative };
            Physics.SyncTransforms(); entry.bounds = clone.bounds; entries.Add(id, entry); changes.Add(entry.bounds);
        }
        void Destroy(Entry e) { if (e.collider) UnityEngine.Object.DestroyImmediate(e.collider.gameObject); if(e.baked) UnityEngine.Object.DestroyImmediate(e.baked); }
        public bool Project(Vector3 point, GrassSettings s, out RaycastHit hit)
        {
            if (!physics.Raycast(point + Vector3.up * s.projection, Vector3.down, out hit, s.projection * 2, s.surfaceLayers, QueryTriggerInteraction.Ignore)) return false;
            if(byCollider.TryGetValue(hit.collider,out var entry)&&entry.conservative)return false;
            return Vector3.Angle(Vector3.up, hit.normal) <= s.maxSlope;
        }
        public bool RaycastSurface(Ray ray, GrassSettings s, out RaycastHit hit) => physics.Raycast(ray.origin,ray.direction,out hit,10000,s.surfaceLayers,QueryTriggerInteraction.Ignore);
        static bool InMask(Entry e, LayerMask mask) => (mask.value & (1 << e.layer)) != 0;
        bool SameSurface(Entry e, RaycastHit surface)
        {
            if (e.collider == surface.collider) return true;
            if (!byCollider.TryGetValue(surface.collider, out var ground)) return false;
            return e.source.transform == ground.source.transform;
        }
        void Nearby(Bounds bounds)
        {
            candidates.Clear(); var a=Key(bounds.min); var b=Key(bounds.max);
            for(int x=a.x;x<=b.x;x++) for(int y=a.y;y<=b.y;y++) for(int z=a.z;z<=b.z;z++)
                if(grid.TryGetValue(new Vector3Int(x,y,z),out var list)) foreach(var e in list) if(e.bounds.Intersects(bounds)) candidates.Add(e);
            foreach(var e in largeEntries)if(e.bounds.Intersects(bounds))candidates.Add(e);
        }
        public float EdgeWeight(Vector3 root, RaycastHit surface, GrassSettings s)
        {
            var box=new Bounds(root,Vector3.one * (s.obstacleFade+s.clearance+2)); Nearby(box);
            float nearest=s.obstacleFade+s.clearance;
            foreach(var e in candidates)
            {
                if(!InMask(e,s.obstacleLayers)||SameSurface(e,surface))continue;
                // ClosestPoint on non-convex meshes is unsupported: use actual collider rays from a small radial fan.
                if(e.collider is MeshCollider mc && !mc.convex)
                {
                    for(int i=0;i<16;i++) {float a=i*Mathf.PI/8;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                        if(e.collider.Raycast(new Ray(root+Vector3.up*.08f,d),out var h,nearest)) nearest=Mathf.Min(nearest,h.distance-e.clearance);}
                }
                else nearest=Mathf.Min(nearest,Vector3.Distance(root+Vector3.up*.08f,e.collider.ClosestPoint(root+Vector3.up*.08f))-e.clearance);
            }
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(s.clearance,s.clearance+s.obstacleFade,nearest));
        }
        bool Occupied(Vector3 a, Vector3 b, float radius, RaycastHit support, LayerMask mask)
        {
            int count;
            while (true)
            {
                count = physics.OverlapCapsule(a,b,radius,overlap,mask,QueryTriggerInteraction.Ignore);
                if(count < overlap.Length) break;
                Array.Resize(ref overlap,overlap.Length*2);
            }
            for(int i=0;i<count;i++)
            {
                if(!byCollider.TryGetValue(overlap[i],out var e))continue;
                if(SameSurface(e,support))
                {
                    // Contact at the root is allowed; elevated folds/walls in the SAME ground mesh are not.
                    if(e.collider.Raycast(new Ray(a,(b-a).normalized),out _,Vector3.Distance(a,b))) return true;
                    if(e.collider is MeshCollider ground&&!ground.convex&&ground.sharedMesh)
                    {
                        if(e.support==null)e.support=new GrassSupportGeometry(ground.sharedMesh,e.collider.transform.localToWorldMatrix);
                        if(e.support.Intersects(a,b,radius,support.point,support.normal))return true;
                    }
                    if(e.collider is TerrainCollider terrain&&GrassSupportGeometry.TerrainIntersects(terrain,a,b,radius,support.point,support.normal))return true;
                    if(Mathf.Min(Vector3.Dot(a-support.point,support.normal),Vector3.Dot(b-support.point,support.normal))>radius+.015f)return true;
                    continue;
                }
                return true;
            }
            return false;
        }
        bool InsideClosedMesh(Vector3 point, Entry e)
        {
            if(!(e.collider is MeshCollider mc) || mc.convex || !e.bounds.Contains(point)) return false;
            // Count all crossings in one direction, including backfaces, without changing the user's final physics setting.
            bool backfaces=Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces=true;
                var direction=new Vector3(.931f,.173f,.321f).normalized; var origin=point; float length=e.bounds.size.magnitude+1; int crossings=0;
                for(int i=0;i<256;i++)
                {
                    if(!mc.Raycast(new Ray(origin,direction),out var h,length))break;
                    crossings++; origin=h.point+direction*.0001f; length-=h.distance+.0001f;
                }
                return crossings>=256||crossings%2!=0;
            }
            finally { Physics.queriesHitBackfaces=backfaces; }
        }
        public bool Accept(GrassPlant p, RaycastHit support, GrassSettings s)
        {
            if(s.overhead>0 && physics.Raycast(p.position+p.normal*.025f,Vector3.up,out _,s.overhead,s.obstacleLayers,QueryTriggerInteraction.Ignore))return false;
            var region = new Bounds(p.position+Vector3.up*p.height*.5f,Vector3.one*(p.height*3+p.clearance*2+2)); Nearby(region);
            float extra=0;
            foreach(var e in candidates)
            {
                if(!InMask(e,s.obstacleLayers)||SameSurface(e,support))continue;
                extra=Mathf.Max(extra,e.clearance);
                if(InsideClosedMesh(p.position+p.normal*.025f,e))return false;
            }
            for(int i=0;i<GrassMeshGenerator.BladeCount(p);i++)
            {
                var blade=GrassMeshGenerator.GetBlade(p,i);
                for(int lod=0;lod<3;lod++)
                {
                    int segments=lod==0?GrassMeshGenerator.Segments:lod==1?3:2;
                    for(int j=0;j<segments;j++)
                    {
                    float t0=j/(float)segments,t1=(j+1)/(float)segments;
                    Vector3 a=blade.Center(t0),b=blade.Center(t1);
                    if(j==0)a+=p.normal*.012f;
                    // Covers ribbon width, fold, curve/chord error AND every possible GPU wind displacement.
                    float radius=blade.HalfWidth(t0)*1.05f+p.clearance+extra+p.wind*t1*t1+blade.height*.006f;
                    if(Occupied(a,b,Mathf.Max(.002f,radius),support,s.obstacleLayers))return false;
                    }
                }
            }
            return true;
        }
        public void Dispose()
        {
            foreach(var e in entries.Values)Destroy(e); entries.Clear(); grid.Clear();largeEntries.Clear(); byCollider.Clear();
            if(scene.IsValid())UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}

