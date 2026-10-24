using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdvancedGrassPainter
{
    public sealed class GrassChunkManager : IDisposable
    {
        public sealed class Chunk
        {
            public Vector3 origin;
            public List<GrassPlant> plants = new List<GrassPlant>();
            public Mesh[] meshes = new Mesh[3];
            public Bounds bounds;
            public long bytes;
        }
        public readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        public long bytes;
        public void Build(GrassDataStorage data)
        {
            bytes = data.plants.Count * 112L;
            var grouped=new Dictionary<Vector2Int,Chunk>();
            foreach (var p in data.plants)
            {
                var key = new Vector2Int(Mathf.FloorToInt(p.position.x / data.chunkSize), Mathf.FloorToInt(p.position.z / data.chunkSize));
                if (!grouped.TryGetValue(key, out var c)) { c = new Chunk { origin = new Vector3(key.x * data.chunkSize, 0, key.y * data.chunkSize) }; grouped.Add(key, c); }
                c.plants.Add(p);
            }
            var stale=new List<Vector2Int>();foreach(var pair in chunks)if(!grouped.ContainsKey(pair.Key)){DestroyChunk(pair.Value);stale.Add(pair.Key);}foreach(var key in stale)chunks.Remove(key);
            foreach (var pair in grouped)
            {
                var c=pair.Value;
                if(chunks.TryGetValue(pair.Key,out var old))
                {
                    bool same=old.origin==c.origin&&old.plants.Count==c.plants.Count;
                    if(same)for(int i=0;i<c.plants.Count;i++)if(!Same(old.plants[i],c.plants[i])){same=false;break;}
                    if(same){bytes+=old.bytes;continue;}DestroyChunk(old);
                }
                for (int lod = 0; lod < 3; lod++) { c.meshes[lod] = GrassMeshGenerator.Build(c.plants, lod, c.origin); c.bytes += c.meshes[lod].vertexCount * 72L + c.meshes[lod].GetIndexCount(0) * 4L; }
                c.bounds = c.meshes[0].bounds; c.bounds.center += c.origin;
                bytes+=c.bytes;chunks[pair.Key]=c;
            }
        }
        static bool Same(GrassPlant a,GrassPlant b)=>a.position==b.position&&a.normal==b.normal&&a.seed==b.seed&&a.kind==b.kind&&a.height==b.height&&a.width==b.width&&a.yaw==b.yaw&&a.clearance==b.clearance&&a.wind==b.wind&&a.root==b.root&&a.tip==b.tip;
        static void DestroyChunk(Chunk c){foreach(var mesh in c.meshes)if(mesh){if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}}
        public void Dispose()
        {
            foreach (var c in chunks.Values) DestroyChunk(c);
            chunks.Clear(); bytes = 0;
        }
    }

    public sealed class GrassRenderingSystem : IDisposable
    {
        readonly GrassDataStorage data;
        public readonly GrassChunkManager manager = new GrassChunkManager();
        readonly Plane[] planes = new Plane[6];
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        bool dirty = true;
        public int submittedDraws, visibleChunks;
        public GrassRenderingSystem(GrassDataStorage data) { this.data = data; }
        public void Enable() { RenderPipelineManager.beginCameraRendering += Draw; Camera.onPreCull += DrawBuiltin; }
        public void Invalidate() { dirty = true; }
        public void RebuildIfNeeded() { if (!dirty) return; manager.Build(data); dirty = false; }
        void DrawBuiltin(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null) Draw(default, camera); }
        void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (!data || !data.isActiveAndEnabled || !data.material || !camera || camera.cameraType == CameraType.Preview || (camera.cullingMask & (1 << data.gameObject.layer)) == 0) return;
            RebuildIfNeeded(); GeometryUtility.CalculateFrustumPlanes(camera, planes);
            submittedDraws = 0; visibleChunks = 0; block.Clear();
            var wind = data.GetComponent<GrassWindController>();
            if (wind) wind.Apply(block); else { block.SetVector("_GrassWind", new Vector4(.82f, .57f, .5f, 1.4f)); block.SetVector("_GrassGust", new Vector4(.27f, .28f, .5f, 0)); block.SetFloat("_GrassTime",Application.isPlaying?Time.time:Time.realtimeSinceStartup); }
            float factor = data.quality == GrassQuality.Low ? .45f : data.quality == GrassQuality.Medium ? .7f : data.quality == GrassQuality.Ultra ? 1.35f : 1;
            float max = data.drawDistance * factor;
            foreach (var c in manager.chunks.Values)
            {
                float distance = Mathf.Sqrt(c.bounds.SqrDistance(camera.transform.position));
                if (distance > max || !GeometryUtility.TestPlanesAABB(planes, c.bounds)) continue;
                int lod = distance < max * .23f ? 0 : distance < max * .53f ? 1 : 2;
                var mesh = c.meshes[lod]; if (mesh.vertexCount == 0) continue;
                var rp = new RenderParams(data.material) { camera = camera, layer = data.gameObject.layer, worldBounds = c.bounds,
                    matProps = block, shadowCastingMode = data.castShadows ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off,
                    receiveShadows = true, renderingLayerMask = 1 };
                Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.Translate(c.origin));
                submittedDraws++; visibleChunks++;
            }
        }
        public void Dispose() { RenderPipelineManager.beginCameraRendering -= Draw; Camera.onPreCull -= DrawBuiltin; manager.Dispose(); }
    }
}
