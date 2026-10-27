#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace ZombieSurvival.ModularBuildings.Editor
{
    /// <summary>Render-only material partition; no collider mesh or flight changes.</summary>
    public static class BuildingPublicFloorFinish
    {
        const string MaterialPath="Assets/Maps/Buildings/Textures/WeatheredCheckerFloor_01/M_CheckerFloor_World.mat";
        const string Folder="Assets/ModularBuildings/Generated/PublicFloorFinishMeshes";
        sealed class Target {public MeshFilter filter;public Rect[] regions;public bool entireTop;}
        struct Vertex
        {
            public Vector3 p,n;public Vector2 uv,uv1;public Color color;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),uv1=Vector2.Lerp(a.uv1,b.uv1,t),color=Color.Lerp(a.color,b.color,t)};
        }
        static IEnumerable<Target> Targets(GeneratedBuilding b)
        {
            var slabs=b.transform.Find("ContinuousFloorSlabs");if(slabs)
            foreach(Transform slab in slabs)
            {
                int floor=Mathf.RoundToInt(slab.localPosition.y/3);if(floor<0||floor>=Math.Min(4,b.floors))continue;
                var f=slab.GetComponent<MeshFilter>();if(!f)continue;float width=BuildingFootprint.SectionWidth(b);
                yield return new Target{filter=f,regions=Enumerable.Range(0,b.entrances).Select(e=>Rect.MinMaxRect(e*width-1.5f,-1.3f,e*width+1.5f,4.5f)).ToArray()};
            }
            foreach(var t in b.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Stairs_Landing_01"&&t.gameObject.activeInHierarchy))
            {var f=t.GetComponent<MeshFilter>();if(f)yield return new Target{filter=f,entireTop=true,regions=Array.Empty<Rect>()};}
        }
        static List<Vertex> Clip(List<Vertex> poly,int axis,float bound,bool greater)
        {
            var result=new List<Vertex>();if(poly.Count==0)return result;var last=poly[poly.Count-1];float old=greater?last.p[axis]-bound:bound-last.p[axis];
            foreach(var next in poly){float now=greater?next.p[axis]-bound:bound-next.p[axis];if((old>=0)!=(now>=0))result.Add(Vertex.Lerp(last,next,old/(old-now)));if(now>=0)result.Add(next);last=next;old=now;}return result;
        }
        static Mesh Build(Mesh source,Rect[] rects,bool entireTop)
        {
            var positions=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var uv1=new List<Vector2>();var colors=new List<Color>();var sub=new List<int>[source.subMeshCount+1];for(int i=0;i<sub.Length;i++)sub[i]=new List<int>();
            void Add(List<Vertex> polygon,int material)
            {
                if(polygon.Count<3)return;int start=positions.Count;foreach(var v in polygon){positions.Add(v.p);normals.Add(v.n);uv.Add(v.uv);uv1.Add(v.uv1);colors.Add(v.color);}for(int i=1;i+1<polygon.Count;i++)if(Vector3.Cross(polygon[i].p-polygon[0].p,polygon[i+1].p-polygon[0].p).sqrMagnitude>1e-14f)sub[material].AddRange(new[]{start,start+i,start+i+1});
            }
            using(var arrays=MeshUtility.AcquireReadOnlyMeshData(source))
            using(var vertices=new NativeArray<Vector3>(arrays[0].vertexCount,Allocator.Temp))
            using(var ns=new NativeArray<Vector3>(arrays[0].vertexCount,Allocator.Temp))
            using(var tex=new NativeArray<Vector2>(arrays[0].vertexCount,Allocator.Temp))
            using(var tex1=new NativeArray<Vector2>(arrays[0].vertexCount,Allocator.Temp))
            using(var col=new NativeArray<Color>(arrays[0].vertexCount,Allocator.Temp))
            {
                var d=arrays[0];d.GetVertices(vertices);bool hn=source.HasVertexAttribute(VertexAttribute.Normal),hu=source.HasVertexAttribute(VertexAttribute.TexCoord0),hu1=source.HasVertexAttribute(VertexAttribute.TexCoord1),hc=source.HasVertexAttribute(VertexAttribute.Color);if(hn)d.GetNormals(ns);if(hu)d.GetUVs(0,tex);if(hu1)d.GetUVs(1,tex1);if(hc)d.GetColors(col);
                for(int s=0;s<d.subMeshCount;s++)
                {
                    var desc=d.GetSubMesh(s);if(desc.topology!=MeshTopology.Triangles)throw new InvalidOperationException("Пол должен иметь треугольные подмеши.");
                    using(var indices=new NativeArray<int>(desc.indexCount,Allocator.Temp))
                    {
                        d.GetIndices(indices,s);
                        for(int i=0;i+2<indices.Length;i+=3)
                        {
                            var triangle=new List<Vertex>();for(int k=0;k<3;k++){int q=indices[i+k];triangle.Add(new Vertex{p=vertices[q],n=hn?ns[q]:Vector3.up,uv=hu?tex[q]:new Vector2(vertices[q].x,vertices[q].z),uv1=hu1?tex1[q]:Vector2.zero,color=hc?col[q]:Color.white});}
                            var normal=Vector3.Cross(triangle[1].p-triangle[0].p,triangle[2].p-triangle[0].p).normalized;
                            if(normal.y<.999f){Add(triangle,s);continue;}
                            if(entireTop){Add(triangle,source.subMeshCount);continue;}
                            var remaining=new List<List<Vertex>>{triangle};
                            foreach(var r in rects)
                            {
                                var next=new List<List<Vertex>>();foreach(var polygon in remaining)
                                {
                                    var inside=polygon;
                                    foreach(var edge in new[]{new Vector3(0,r.xMin,1),new Vector3(0,r.xMax,0),new Vector3(2,r.yMin,1),new Vector3(2,r.yMax,0)})
                                    {
                                        int axis=(int)edge.x;bool greater=edge.z>0;var outside=Clip(inside,axis,edge.y,!greater);if(outside.Count>=3)next.Add(outside);inside=Clip(inside,axis,edge.y,greater);if(inside.Count<3)break;
                                    }
                                    Add(inside,source.subMeshCount);
                                }
                                remaining=next;
                            }
                            foreach(var rest in remaining)Add(rest,s);
                        }
                    }
                }
            }
            var mesh=new Mesh{name="Public floor top partition",indexFormat=IndexFormat.UInt32};mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetUVs(1,uv1);mesh.SetColors(colors);mesh.subMeshCount=sub.Length;for(int i=0;i<sub.Length;i++)mesh.SetTriangles(sub[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();
            string hash;using(var sha=SHA256.Create())using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){foreach(var p in positions){w.Write(p.x);w.Write(p.y);w.Write(p.z);}foreach(var n in normals){w.Write(n.x);w.Write(n.y);w.Write(n.z);}foreach(var t in uv){w.Write(t.x);w.Write(t.y);}foreach(var t in uv1){w.Write(t.x);w.Write(t.y);}foreach(var c in colors){w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}foreach(var list in sub){w.Write(list.Count);foreach(int q in list)w.Write(q);}w.Flush();hash=BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","");}
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder(BuildingGenerator.Folder,"PublicFloorFinishMeshes");string path=Folder+"/PublicTop_"+hash+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){UnityEngine.Object.DestroyImmediate(mesh);return existing;}AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        public static int Apply(GeneratedBuilding b,bool recordUndo=true)
        {
            if(!b||!b.profile||Application.isPlaying||EditorUtility.IsPersistent(b))throw new InvalidOperationException("Нужен экземпляр дома в Edit.");var checker=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);if(!checker)throw new InvalidOperationException("Нет checker material.");
            var targets=Targets(b).ToArray();int changed=0;
            foreach(var t in targets)
            {
                var f=t.filter;var renderer=f.GetComponent<MeshRenderer>();if(!renderer||!f.sharedMesh)throw new InvalidOperationException("Нет пола/renderer.");var marker=f.GetComponent<GeneratedPublicFloorFinish>();
                var source=marker?marker.originalMesh:f.sharedMesh;var materials=marker?marker.originalMaterials:renderer.sharedMaterials;
                if(!source||materials==null||materials.Length!=source.subMeshCount)throw new InvalidOperationException("Не совпадают исходные подмеши пола и материалы.");
                var finished=Build(source,t.regions,t.entireTop);
                if(marker&&f.sharedMesh==finished&&renderer.sharedMaterials.SequenceEqual(materials.Concat(new[]{checker})))continue;
                if(recordUndo)Undo.RecordObjects(new UnityEngine.Object[]{f,renderer},"Клетчатый пол общественной зоны");
                if(!marker)marker=recordUndo?Undo.AddComponent<GeneratedPublicFloorFinish>(f.gameObject):f.gameObject.AddComponent<GeneratedPublicFloorFinish>();else if(recordUndo)Undo.RecordObject(marker,"Обновить отделку пола");
                marker.originalMesh=source;marker.originalMaterials=materials.ToArray();marker.finishedMesh=finished;marker.checker=checker;marker.checkerSubmesh=source.subMeshCount;
                f.sharedMesh=finished;renderer.sharedMaterials=materials.Concat(new[]{checker}).ToArray();
                foreach(var o in new UnityEngine.Object[]{f,renderer,marker}){EditorUtility.SetDirty(o);PrefabUtility.RecordPrefabInstancePropertyModifications(o);}changed++;
            }
            if(recordUndo&&changed>0)UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(b.gameObject.scene);return changed;
        }
        public static List<string> Validate(GeneratedBuilding b)
        {
            var errors=new List<string>();foreach(var t in Targets(b))
            {
                var marker=t.filter.GetComponent<GeneratedPublicFloorFinish>();var renderer=t.filter.GetComponent<MeshRenderer>();
                if(!marker||!marker.originalMesh||!marker.finishedMesh||t.filter.sharedMesh!=marker.finishedMesh||!renderer||renderer.sharedMaterials.Length!=marker.finishedMesh.subMeshCount||renderer.sharedMaterials[marker.checkerSubmesh]!=marker.checker){errors.Add("Нет правильной отделки общественного пола: "+t.filter.name);continue;}
                var collider=t.filter.GetComponent<MeshCollider>();if(collider&&collider.sharedMesh!=marker.originalMesh)errors.Add("Изменена физическая геометрия пола: "+t.filter.name);
                var vertices=marker.finishedMesh.vertices;var indices=marker.finishedMesh.GetTriangles(marker.checkerSubmesh);
                for(int i=0;i+2<indices.Length;i+=3){var a=vertices[indices[i]];var c=vertices[indices[i+1]];var d=vertices[indices[i+2]];if(Vector3.Cross(c-a,d-a).normalized.y<.999f)errors.Add("Checker на нетоповой грани.");if(!t.entireTop&&new[]{a,c,d}.Any(v=>!t.regions.Any(r=>v.x>=r.xMin-.001f&&v.x<=r.xMax+.001f&&v.z>=r.yMin-.001f&&v.z<=r.yMax+.001f)))errors.Add("Checker вне общественного пола.");}
            }
            return errors;
        }
        [MenuItem("Tools/Zombie Survival/Город — обновить клетчатый пол выбранного дома")]
        static void UpdateSelected(){var b=Selection.activeGameObject?Selection.activeGameObject.GetComponentInParent<GeneratedBuilding>():null;if(!b)throw new InvalidOperationException("Выберите дом.");Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply(b,true);var e=Validate(b);if(e.Count>0)throw new InvalidOperationException(string.Join(";",e));Undo.CollapseUndoOperations(group);}catch{Undo.RevertAllDownToGroup(group);throw;}}
    }
}
#endif
