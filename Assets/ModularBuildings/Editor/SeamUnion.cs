using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ZombieSurvival.ModularBuildings.Editor
{
    // A planar union, not an epsilon displacement: remove only the covered part and interpolate UV/normals.
    // The first owner keeps its surface; later coplanar surfaces are cut exactly at its edge.
    public static class SeamUnion
    {
        const string Folder="Assets/ModularBuildings/LibraryV3/SeamMeshes";
        struct Vertex
        {
            public Vector3 p,n;public Vector2 uv,uv2;public Vector4 tangent;public Color color;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.LerpUnclamped(a.p,b.p,t),n=Vector3.LerpUnclamped(a.n,b.n,t),uv=Vector2.LerpUnclamped(a.uv,b.uv,t),uv2=Vector2.LerpUnclamped(a.uv2,b.uv2,t),tangent=Vector4.LerpUnclamped(a.tangent,b.tangent,t),color=Color.LerpUnclamped(a.color,b.color,t)};
        }
        sealed class Cover
        {
            public Vector3 a,b,c,n;public float d;public Vector2 u,v,w;public Rect bounds;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static string Plane(Vector3 n,float d)=>Mathf.RoundToInt(n.x*10000)+","+Mathf.RoundToInt(n.y*10000)+","+Mathf.RoundToInt(n.z*10000)+","+Mathf.RoundToInt(d*1000);
        static int Axis(Vector3 n){int a=Mathf.Abs(n.x)>Mathf.Abs(n.y)?0:1;return Mathf.Abs(n.z)>Mathf.Abs(n[a])?2:a;}
        static Rect Bounds(Vector2 a,Vector2 b,Vector2 c){var min=Vector2.Min(a,Vector2.Min(b,c));var max=Vector2.Max(a,Vector2.Max(b,c));return Rect.MinMaxRect(min.x,min.y,max.x,max.y);}
        static int Priority(MeshFilter f)
        {
            string path=f.name.ToLowerInvariant();
            if(path.Contains("glass"))return 100;
            if(path.Contains("frame"))return 0;
            if(path=="weatherroof")return 1;
            if(path.Contains("wall"))return 5;
            if(path.Contains("floor")||path.Contains("landing"))return 30;
            return 15;
        }
        public static int Resolve(GameObject root)
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var planes=new Dictionary<string,List<Cover>>();int changed=0;
            var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh&&f.GetComponent<MeshRenderer>()&&f.GetComponent<MeshRenderer>().enabled).OrderBy(Priority).ToArray();
            foreach(var filter in filters){
                var source=filter.sharedMesh;var positions=source.vertices;var normals=source.normals;var uv=source.uv;var uv2=source.uv2;var tangents=source.tangents;var colors=source.colors;
                var matrix=root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;var inverse=matrix.inverse;
                var output=new List<Vertex>();var submeshes=new List<int>[source.subMeshCount];bool modified=false;
                for(int sub=0;sub<source.subMeshCount;sub++){
                    submeshes[sub]=new List<int>();var indices=source.GetTriangles(sub);
                    for(int i=0;i<indices.Length;i+=3){
                        var original=new List<Vertex>();
                        foreach(int index in new[]{indices[i],indices[i+1],indices[i+2]})original.Add(new Vertex{p=matrix.MultiplyPoint3x4(positions[index]),n=normals.Length>index?normals[index]:Vector3.up,uv=uv.Length>index?uv[index]:Vector2.zero,uv2=uv2.Length>index?uv2[index]:Vector2.zero,tangent=tangents.Length>index?tangents[index]:new Vector4(1,0,0,1),color=colors.Length>index?colors[index]:Color.white});
                        var normal=Vector3.Cross(original[1].p-original[0].p,original[2].p-original[0].p);if(normal.sqrMagnitude<1e-12f)continue;normal.Normalize();float distance=Vector3.Dot(normal,original[0].p);int axis=Axis(normal);
                        var key=Plane(normal,distance);if(!planes.TryGetValue(key,out var covers)){covers=new List<Cover>();planes[key]=covers;}
                        var originalBounds=Bounds(CoplanarAudit.Project(original[0].p,axis),CoplanarAudit.Project(original[1].p,axis),CoplanarAudit.Project(original[2].p,axis));
                        var fragments=new List<List<Vertex>>{original};
                        // Only prior accepted faces can occlude; within-mesh duplicates are handled as well.
                        foreach(var cover in covers){
                            if(!originalBounds.Overlaps(cover.bounds))continue;
                            if(Vector3.Dot(normal,cover.n)<.999999f||Mathf.Abs(Vector3.Dot(normal,cover.a)-distance)>.00015f||Mathf.Abs(Vector3.Dot(normal,cover.b)-distance)>.00015f||Mathf.Abs(Vector3.Dot(normal,cover.c)-distance)>.00015f)continue;
                            var next=new List<List<Vertex>>();
                            foreach(var fragment in fragments){
                                var a=CoplanarAudit.Project(fragment[0].p,axis);bool overlap=false;
                                for(int k=1;k<fragment.Count-1;k++)if(CoplanarAudit.IntersectionArea(a,CoplanarAudit.Project(fragment[k].p,axis),CoplanarAudit.Project(fragment[k+1].p,axis),cover.u,cover.v,cover.w)>1e-6f){overlap=true;break;}
                                if(!overlap){next.Add(fragment);continue;}
                                modified=true;next.AddRange(Subtract(fragment,cover,axis));
                            }
                            fragments=next;if(fragments.Count==0)break;
                        }
                        var accepted=new List<Cover>();
                        foreach(var fragment in fragments)for(int k=1;k<fragment.Count-1;k++){
                            var a=fragment[0];var b=fragment[k];var c=fragment[k+1];if(Vector3.Cross(b.p-a.p,c.p-a.p).sqrMagnitude<1e-12f)continue;
                            int start=output.Count;output.Add(a);output.Add(b);output.Add(c);submeshes[sub].AddRange(new[]{start,start+1,start+2});
                            var u=CoplanarAudit.Project(a.p,axis);var v=CoplanarAudit.Project(b.p,axis);var w=CoplanarAudit.Project(c.p,axis);
                            accepted.Add(new Cover{a=a.p,b=b.p,c=c.p,n=normal,d=distance,u=u,v=v,w=w,bounds=Bounds(u,v,w)});
                        }covers.AddRange(accepted);
                    }
                }
                if(!modified)continue;changed++;
                if(output.Count==0){filter.GetComponent<MeshRenderer>().enabled=false;continue;}
                var mesh=new Mesh{name="Joined_"+source.name,indexFormat=output.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};
                mesh.SetVertices(output.Select(v=>inverse.MultiplyPoint3x4(v.p)).ToList());mesh.SetNormals(output.Select(v=>v.n.normalized).ToList());mesh.SetUVs(0,output.Select(v=>v.uv).ToList());
                if(uv2.Length>0)mesh.SetUVs(1,output.Select(v=>v.uv2).ToList());if(tangents.Length>0)mesh.SetTangents(output.Select(v=>v.tangent).ToList());if(colors.Length>0)mesh.SetColors(output.Select(v=>v.color).ToList());
                mesh.subMeshCount=submeshes.Length;for(int s=0;s<submeshes.Length;s++)mesh.SetTriangles(submeshes[s],s);mesh.RecalculateBounds();
                string path=Folder+"/Joined_"+Hash(mesh)+".asset";var cached=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(cached){Object.DestroyImmediate(mesh);mesh=cached;}else AssetDatabase.CreateAsset(mesh,path);
                filter.sharedMesh=mesh;
            }
            return changed;
        }
        static IEnumerable<List<Vertex>> Subtract(List<Vertex> polygon,Cover cover,int axis)
        {
            var result=new List<List<Vertex>>();var inside=polygon;var triangle=new[]{cover.u,cover.v,cover.w};float sign=Mathf.Sign(Cross(cover.v-cover.u,cover.w-cover.u));
            for(int edge=0;edge<3&&inside.Count>0;edge++){
                var p=triangle[edge];var q=triangle[(edge+1)%3];var keep=new List<Vertex>();var outside=new List<Vertex>();
                for(int i=0;i<inside.Count;i++){
                    var a=inside[i];var b=inside[(i+1)%inside.Count];float da=sign*Cross(q-p,CoplanarAudit.Project(a.p,axis)-p),db=sign*Cross(q-p,CoplanarAudit.Project(b.p,axis)-p);
                    bool ain=da>=0,bin=db>=0;if(ain)keep.Add(a);else outside.Add(a);
                    if(ain!=bin){var intersection=Vertex.Lerp(a,b,da/(da-db));keep.Add(intersection);outside.Add(intersection);}
                }
                if(outside.Count>=3)result.Add(outside);inside=keep;
            }
            return result;
        }
        static string Hash(Mesh mesh)
        {
            using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory)){
                foreach(var p in mesh.vertices){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
                foreach(var p in mesh.uv){writer.Write(p.x);writer.Write(p.y);}
                foreach(var n in mesh.normals){writer.Write(n.x);writer.Write(n.y);writer.Write(n.z);}
                foreach(var p in mesh.uv2){writer.Write(p.x);writer.Write(p.y);}
                foreach(var t in mesh.tangents){writer.Write(t.x);writer.Write(t.y);writer.Write(t.z);writer.Write(t.w);}
                foreach(var c in mesh.colors){writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);writer.Write(c.a);}
                for(int s=0;s<mesh.subMeshCount;s++){writer.Write(-s-1);foreach(int i in mesh.GetTriangles(s))writer.Write(i);}writer.Flush();
                using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(memory.ToArray())).Replace("-","").Substring(0,32);
            }
        }
    }
}
