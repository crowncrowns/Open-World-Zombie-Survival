#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AdvancedGrassPainter;
using ZombieSurvival.ModularBuildings;

namespace ZombieSurvival.WorldAuthoring.Editor
{
    /// <summary>Explicit, vertical-only migration. BuildPlan never modifies Unity objects.</summary>
    public static class TerrainGrounding
    {
        [Serializable] public sealed class Record
        {
            public string objectPath, kind, assetPath, note;
            public Vector3 before, after;
            public float oldGround, newGround, oldBottomGap, expectedBottomGap;
            public int count;
        }
        [Serializable] sealed class Report { public List<Record> records; public int transforms, meshes, grassLayers; }
        internal sealed class Move { public Transform target; public Vector3 before, after, scale; public Quaternion rotation; }
        internal sealed class MeshChange { public MeshFilter filter; public Mesh original, derived; public Vector3[] vertices, originalVertices; public MeshCollider[] colliders; public Mesh[] colliderMeshes; public Record record; }
        internal sealed class GrassChange { public GrassDataStorage target; public GrassPlant[] before, after; public Vector3[] centersBefore, centersAfter; public Bounds[] boundsBefore, boundsAfter; }
        public sealed class Plan
        {
            internal Scene scene;
            internal readonly List<Move> moves = new List<Move>();
            internal readonly List<MeshChange> meshes = new List<MeshChange>();
            internal readonly List<GrassChange> grass = new List<GrassChange>();
            public readonly List<Record> Records = new List<Record>();
            bool applied;
            public int TransformCount => moves.Count;
            public int MeshCount => meshes.Count;
            public int GrassLayerCount => grass.Count;
            public string ToJson() => JsonUtility.ToJson(new Report { records=Records, transforms=moves.Count, meshes=meshes.Count, grassLayers=grass.Count },true);
            public void Apply(string undoName="Привязать окружение к рельефу")
            {
                Guard(scene); if(applied) throw new InvalidOperationException("План уже применён.");
                // Refuse stale plans rather than overwriting an author's intervening edits.
                foreach(var m in moves) if(!m.target || m.target.position!=m.before) throw new InvalidOperationException("Transform изменён после построения плана.");
                foreach(var m in meshes) if(!m.filter || m.filter.sharedMesh!=m.original) throw new InvalidOperationException("Mesh изменён после построения плана.");
                foreach(var g in grass) if(!g.target || g.target.plants.Count!=g.before.Length || g.target.strokes.Count!=g.centersBefore.Length || !g.target.plants.SequenceEqual(g.before)) throw new InvalidOperationException("Трава изменена после построения плана.");
                Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(undoName);
                applied=true;
                try
                {
                    foreach(var m in moves) { Undo.RecordObject(m.target,undoName); m.target.position=m.after; Dirty(m.target); }
                    if(meshes.Count>0) EnsureFolder("Assets/TerrainEditing/SmoothedGroundMeshes");
                    foreach(var m in meshes)
                    {
                        var copy=CloneReadable(m.original); copy.name=m.original.name+"_Grounded"; copy.hideFlags=HideFlags.None; copy.vertices=m.vertices; copy.RecalculateNormals(); if(copy.uv.Length==copy.vertexCount)copy.RecalculateTangents(); copy.RecalculateBounds();
                        string path=AssetDatabase.GenerateUniqueAssetPath("Assets/TerrainEditing/SmoothedGroundMeshes/"+Guid.NewGuid().ToString("N")+".asset");
                        AssetDatabase.CreateAsset(copy,path); m.derived=copy; m.record.assetPath=path;
                        Undo.RecordObject(m.filter,undoName); m.filter.sharedMesh=copy; Dirty(m.filter);
                        for(int i=0;i<m.colliders.Length;i++) { var c=m.colliders[i]; Undo.RecordObject(c,undoName); c.sharedMesh=copy; Dirty(c); }
                    }
                    foreach(var g in grass) { Undo.RecordObject(g.target,undoName); SetGrass(g,true); Dirty(g.target); }
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                catch { Restore(); throw; }
                finally { Undo.CollapseUndoOperations(group); }
            }
            public List<string> Verify()
            {
                var errors=new List<string>();
                if(!applied) { errors.Add("План не применён."); return errors; }
                foreach(var m in moves) if(!m.target || ((m.target.position-m.after).sqrMagnitude>0.00000025f || Quaternion.Angle(m.target.rotation,m.rotation)>.001f || m.target.localScale!=m.scale))errors.Add("Transform не соответствует плану: "+(m.target?Path(m.target):"missing"));
                foreach(var m in meshes)
                {
                    if(!m.filter || !m.derived || m.filter.sharedMesh!=m.derived) { errors.Add("Потеряна ссылка derived mesh."); continue; }
                    var actual=m.derived.vertices; var original=m.originalVertices;
                    if(actual.Length!=m.vertices.Length || original.Length!=actual.Length) { errors.Add("Изменено количество вершин: "+Path(m.filter.transform));continue; }
                    for(int i=0;i<actual.Length;i++)
                    {
                        var a=m.filter.transform.TransformPoint(actual[i]); var expected=m.filter.transform.TransformPoint(m.vertices[i]); var old=m.filter.transform.TransformPoint(original[i]);
                        if((a-expected).sqrMagnitude>0.000001f || Mathf.Abs(a.x-old.x)>.001f || Mathf.Abs(a.z-old.z)>.001f) { errors.Add("Mesh vertex/XZ не соответствует плану: "+Path(m.filter.transform)+" #"+i);break; }
                    }
                    for(int i=0;i<m.colliders.Length;i++)if(!m.colliders[i] || m.colliders[i].sharedMesh!=m.derived)errors.Add("Collider mesh не соответствует плану.");
                }
                foreach(var g in grass)
                {
                    if(!g.target || g.target.plants.Count!=g.before.Length) { errors.Add("Изменено количество растений.");continue; }
                    for(int i=0;i<g.before.Length;i++)
                    {
                        var a=g.target.plants[i]; var old=g.before[i]; var expected=g.after[i];
                        if(a.position.x!=old.position.x || a.position.z!=old.position.z || a.seed!=old.seed || !a.Equals(expected)) { errors.Add("Grass metadata/position не соответствует плану: "+Path(g.target.transform)+" #"+i);break; }
                    }
                    if(g.target.strokes.Count!=g.centersBefore.Length)errors.Add("Изменено количество мазков.");
                    else for(int i=0;i<g.target.strokes.Count;i++) { var stroke=g.target.strokes[i]; if(stroke!=null && (stroke.center!=g.centersAfter[i] || stroke.bounds!=g.boundsAfter[i]))errors.Add("Grass stroke не соответствует плану: "+i); }
                }
                return errors;
            }
            public void Restore()
            {
                Guard(scene); if(!applied)return;
                foreach(var m in moves) if(m.target) { m.target.position=m.before; Dirty(m.target); }
                foreach(var m in meshes) if(m.filter) { m.filter.sharedMesh=m.original; Dirty(m.filter); for(int i=0;i<m.colliders.Length;i++)if(m.colliders[i]) { m.colliders[i].sharedMesh=m.colliderMeshes[i]; Dirty(m.colliders[i]); } }
                foreach(var g in grass)if(g.target) { SetGrass(g,false); Dirty(g.target); }
                // Keep derived assets as explicit rollback evidence; never delete source or shared assets.
                EditorSceneManager.MarkSceneDirty(scene); applied=false;
            }
            static void SetGrass(GrassChange g,bool after)
            {
                g.target.plants=new List<GrassPlant>(after?g.after:g.before);
                for(int i=0;i<g.target.strokes.Count && i<g.centersBefore.Length;i++) { var s=g.target.strokes[i]; if(s==null)continue; s.center=after?g.centersAfter[i]:g.centersBefore[i]; s.bounds=after?g.boundsAfter[i]:g.boundsBefore[i]; }
                g.target.Changed();
            }
        }
        public static Plan BuildPlan(Scene scene,Terrain terrain,Func<Vector3,float> beforeHeight,Func<Vector3,float> afterHeight,Func<Vector3,Vector3> afterNormal=null)
        {
            Guard(scene); if(!terrain || terrain.gameObject.scene!=scene || beforeHeight==null || afterHeight==null)throw new ArgumentException("Нужен Terrain этой сцены и обе функции абсолютной высоты мира.");
            var plan=new Plan { scene=scene }; var roots=scene.GetRootGameObjects(); var selected=new HashSet<Transform>();
            var buildings=roots.SelectMany(r=>r.GetComponentsInChildren<GeneratedBuilding>(true)).ToArray();
            Func<Vector3,float> delta=p=> { float a=beforeHeight(p),b=afterHeight(p); if(!float.IsFinite(a)||!float.IsFinite(b))throw new InvalidOperationException("Невалидная высота Terrain."); return b-a; };
            Action<Transform,Vector3,string> move=(t,p,kind)=> { if(selected.Any(s=>t==s||t.IsChildOf(s)))return; selected.Add(t); var bounds=BoundsOf(t); float d=delta(p); plan.moves.Add(new Move {target=t,before=t.position,after=t.position+Vector3.up*d,rotation=t.rotation,scale=t.localScale}); plan.Records.Add(new Record {objectPath=Path(t),kind=kind,before=t.position,after=t.position+Vector3.up*d,oldGround=beforeHeight(p),newGround=afterHeight(p),oldBottomGap=bounds.min.y-beforeHeight(p),expectedBottomGap=bounds.min.y+d-afterHeight(p)}); };
            foreach(var b in buildings) move(b.transform,b.transform.TransformPoint(new Vector3((b.entrances-1)*BuildingFootprint.SectionWidth(b)*.5f,0,4.5f)),"generated-building");
            foreach(var root in roots)
            {
                var t=root.transform; string n=root.name;
                if(n=="EmptyBuildings" || n.StartsWith("Fences",StringComparison.Ordinal) || (n.IndexOf("Vidnoe",StringComparison.OrdinalIgnoreCase)>=0 && n.IndexOf("Autumn",StringComparison.OrdinalIgnoreCase)>=0))
                { foreach(Transform child in t) PlanChild(plan,child,selected,delta,move); }
                else if(n.StartsWith("Building_Shk_68_FINISH") || n.StartsWith("FINISH_Shk_68") || n.StartsWith("Gaevskogo") || n.StartsWith("KursHub")) move(t,BoundsOf(t).center,"manual-building");
                else if(n.StartsWith("PF_TrashBin") || n.StartsWith("Dumpster") || n=="CharacterTestPlayer")move(t,n=="CharacterTestPlayer"?t.position:BoundsOf(t).center,"rigid-prop/player");
                else if((n.StartsWith("Asphalt") && n.Contains("05")) || n.StartsWith("Left curbs",StringComparison.OrdinalIgnoreCase) || n.StartsWith("Leftcurbs",StringComparison.OrdinalIgnoreCase)) PlanMeshTree(plan,t,delta);
            }
            foreach(var road in roots.SelectMany(r=>r.GetComponentsInChildren<TerrainRoads.TerrainConformingRoad>(true)))
                if(road.isActiveAndEnabled && road.network && !road.network.isActiveAndEnabled && road.GetComponent<MeshFilter>()?.sharedMesh) PlanMeshTree(plan,road.transform,delta);
            foreach(var root in roots.Where(r=>r.name.StartsWith("G51_Z")))
            {
                var p=root.transform.position; var nearest=plan.moves.Where(m=>m.target.GetComponent<GeneratedBuilding>() || m.target.name.StartsWith("KursHub") || m.target.name.Contains("Shk_68") || m.target.name.StartsWith("Gaevskogo")).OrderBy(m=>DistanceXZ(BoundsOf(m.target),p)).FirstOrDefault();
                if(nearest!=null && DistanceXZ(BoundsOf(nearest.target),p)<3f) { float d=nearest.after.y-nearest.before.y; plan.moves.Add(new Move {target=root.transform,before=p,after=p+Vector3.up*d,rotation=root.transform.rotation,scale=root.transform.localScale}); plan.Records.Add(new Record {objectPath=Path(root.transform),kind="wall-graffiti",before=p,after=p+Vector3.up*d,note="Delta ближайшего здания"}); }
                else move(root.transform,p,"graffiti-position-fallback");
            }
            foreach(var layer in roots.SelectMany(r=>r.GetComponentsInChildren<GrassDataStorage>(true)))
            {
                var g=new GrassChange { target=layer,before=layer.plants.ToArray(),after=layer.plants.ToArray(),centersBefore=new Vector3[layer.strokes.Count],centersAfter=new Vector3[layer.strokes.Count],boundsBefore=new Bounds[layer.strokes.Count],boundsAfter=new Bounds[layer.strokes.Count] }; int skipped=0;
                for(int i=0;i<g.after.Length;i++) { var p=g.after[i]; if(p.position.y-beforeHeight(p.position)>3f){skipped++;continue;} p.position.y+=delta(p.position); if(afterNormal!=null) { var normal=afterNormal(p.position); if(!Finite(normal)||normal.sqrMagnitude<.001f)throw new InvalidOperationException("Невалидная нормаль."); p.normal=normal.normalized; } g.after[i]=p; }
                for(int i=0;i<layer.strokes.Count;i++) { var s=layer.strokes[i]; if(s==null)continue; g.centersBefore[i]=s.center; g.boundsBefore[i]=s.bounds; float d=s.center.y-beforeHeight(s.center)>3f?0:delta(s.center); g.centersAfter[i]=s.center+Vector3.up*d; var b=s.bounds; b.center+=Vector3.up*d; g.boundsAfter[i]=b; }
                plan.grass.Add(g); plan.Records.Add(new Record {objectPath=Path(layer.transform),kind="grass-world-records",count=g.before.Length,note="Roof plants skipped: "+skipped+"; seeds/count/XZ preserved"});
            }
            return plan;
        }
        static void PlanChild(Plan p,Transform t,HashSet<Transform> selected,Func<Vector3,float> delta,Action<Transform,Vector3,string> move)
        {
            if(selected.Any(s=>t==s||t.IsChildOf(s)))return;
            if(t.name.IndexOf("trees",StringComparison.OrdinalIgnoreCase)>=0 && t.childCount>0 && !PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) { foreach(Transform child in t) PlanChild(p,child,selected,delta,move); return; }
            var filters=t.GetComponentsInChildren<MeshFilter>(true); bool custom=filters.Length>0 && filters.All(f=>f.sharedMesh && AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith("Assets/Environment/VidnoeDecor/Meshes/",StringComparison.Ordinal));
            if(custom && !PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) PlanMeshTree(p,t,delta); else move(t,BoundsOf(t).center,"courtyard/fence-child");
        }
        static void PlanMeshTree(Plan p,Transform t,Func<Vector3,float> delta)
        {
            foreach(var f in t.GetComponentsInChildren<MeshFilter>(true))
            {
                if(p.meshes.Any(m=>m.filter==f))continue;
                if(!f.sharedMesh) { p.Records.Add(new Record {objectPath=Path(f.transform),kind="empty-mesh-skipped",note="Пустой placeholder сохранён без изменений"}); continue; }
                var originalVertices=ReadVertices(f.sharedMesh); var vertices=(Vector3[])originalVertices.Clone(); for(int i=0;i<vertices.Length;i++) { var w=f.transform.TransformPoint(vertices[i]); w.y+=delta(w); vertices[i]=f.transform.InverseTransformPoint(w); }
                var colliders=f.GetComponents<MeshCollider>().Where(c=>c.sharedMesh==f.sharedMesh).ToArray(); var record=new Record {objectPath=Path(f.transform),kind="derived-ground-mesh",count=vertices.Length,note="Исходный mesh не изменяется"};
                p.meshes.Add(new MeshChange {filter=f,original=f.sharedMesh,vertices=vertices,originalVertices=originalVertices,colliders=colliders,colliderMeshes=colliders.Select(c=>c.sharedMesh).ToArray(),record=record}); p.Records.Add(record);
            }
        }
        static Vector3[] ReadVertices(Mesh source)
        {
            using(var read=MeshUtility.AcquireReadOnlyMeshData(source))
            using(var vertices=new NativeArray<Vector3>(read[0].vertexCount,Allocator.Temp))
            {
                read[0].GetVertices(vertices);
                return vertices.ToArray();
            }
        }
        // Preserve the complete source vertex/index layout, including extra UVs, colors and
        // submesh baseVertex. Editor read-only access does not require importer Read/Write.
        static Mesh CloneReadable(Mesh source)
        {
            using(var read=MeshUtility.AcquireReadOnlyMeshData(source))
            {
                var src=read[0]; var writable=Mesh.AllocateWritableMeshData(1); bool disposed=false;
                Mesh result=null;
                try
                {
                    var dst=writable[0]; dst.SetVertexBufferParams(src.vertexCount,source.GetVertexAttributes());
                    for(int stream=0;stream<src.vertexBufferCount;stream++)
                        dst.GetVertexData<byte>(stream).CopyFrom(src.GetVertexData<byte>(stream));
                    int indexCount=src.indexFormat==IndexFormat.UInt16?src.GetIndexData<ushort>().Length:src.GetIndexData<uint>().Length;
                    dst.SetIndexBufferParams(indexCount,src.indexFormat);
                    dst.GetIndexData<byte>().CopyFrom(src.GetIndexData<byte>());
                    dst.subMeshCount=src.subMeshCount;
                    for(int i=0;i<src.subMeshCount;i++)dst.SetSubMesh(i,src.GetSubMesh(i),MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
                    result=new Mesh {name=source.name+"_Grounded",hideFlags=HideFlags.None};
                    Mesh.ApplyAndDisposeWritableMeshData(writable,result,MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices); disposed=true;
                    result.bounds=source.bounds;
                    result.bindposes=source.bindposes;
                    return result;
                }
                catch { if(result)UnityEngine.Object.DestroyImmediate(result); throw; }
                finally { if(!disposed)writable.Dispose(); }
            }
        }
        static void Guard(Scene s) { if(Application.isPlaying || !s.IsValid() || !s.isLoaded || EditorSceneManager.IsPreviewScene(s) || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Нужна загруженная обычная сцена в Edit Mode вне Prefab Stage."); }
        static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
        static void Dirty(UnityEngine.Object o) { EditorUtility.SetDirty(o); if(PrefabUtility.IsPartOfPrefabInstance(o))PrefabUtility.RecordPrefabInstancePropertyModifications(o); }
        static Bounds BoundsOf(Transform t) { var rs=t.GetComponentsInChildren<Renderer>(true); var b=rs.Length>0?rs[0].bounds:new Bounds(t.position,Vector3.zero); for(int i=1;i<rs.Length;i++)b.Encapsulate(rs[i].bounds); return b; }
        static float DistanceXZ(Bounds b,Vector3 p) { p.y=b.center.y; return Vector3.Distance(p,b.ClosestPoint(p)); }
        static string Path(Transform t)=>t.parent?Path(t.parent)+"/"+t.name:t.name;
        static void EnsureFolder(string path) { var parts=path.Split('/'); string at=parts[0]; for(int i=1;i<parts.Length;i++){string next=at+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(at,parts[i]);at=next;} }
    }
}
#endif
