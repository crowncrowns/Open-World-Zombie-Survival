using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace AdvancedGrassPainter.Editor
{
    // A ground collider may ALSO contain a wall. Ignoring that collider would allow grass through the wall.
    // This triangle index permits contact with the local ground plane, while testing every other nearby face.
    internal sealed class GrassSupportGeometry
    {
        struct Triangle {public Vector3 a,b,c;public Bounds bounds;}
        readonly List<Triangle> triangles=new List<Triangle>();
        readonly Dictionary<Vector3Int,List<int>> grid=new Dictionary<Vector3Int,List<int>>();
        readonly HashSet<int> nearby=new HashSet<int>();
        readonly bool valid;
        const float Cell=2;
        static Vector3Int Key(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/Cell),Mathf.FloorToInt(p.y/Cell),Mathf.FloorToInt(p.z/Cell));
        public GrassSupportGeometry(Mesh mesh,Matrix4x4 matrix)
        {
            try
            {
                using(var arrays=MeshUtility.AcquireReadOnlyMeshData(mesh))
                {
                    var data=arrays[0];using(var vertices=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))
                    {
                        data.GetVertices(vertices);
                        for(int sub=0;sub<data.subMeshCount;sub++)
                        {
                            var desc=data.GetSubMesh(sub);if(desc.topology!=MeshTopology.Triangles)continue;
                            using(var indices=new NativeArray<int>(desc.indexCount,Allocator.Temp))
                            {
                                data.GetIndices(indices,sub);
                                for(int i=0;i+2<indices.Length;i+=3)
                                {
                                    var t=new Triangle{a=matrix.MultiplyPoint3x4(vertices[indices[i]]),b=matrix.MultiplyPoint3x4(vertices[indices[i+1]]),c=matrix.MultiplyPoint3x4(vertices[indices[i+2]])};
                                    t.bounds=new Bounds(t.a,Vector3.zero);t.bounds.Encapsulate(t.b);t.bounds.Encapsulate(t.c);int id=triangles.Count;triangles.Add(t);
                                    var lo=Key(t.bounds.min);var hi=Key(t.bounds.max);
                                    for(int x=lo.x;x<=hi.x;x++)for(int y=lo.y;y<=hi.y;y++)for(int z=lo.z;z<=hi.z;z++)
                                    {var k=new Vector3Int(x,y,z);if(!grid.TryGetValue(k,out var list))grid[k]=list=new List<int>();list.Add(id);}
                                }
                            }
                        }
                    }
                }
                valid=triangles.Count>0;
            }
            catch(Exception e){Debug.LogWarning("Grass support mesh cannot be read; generation on it is blocked: "+e.Message);valid=false;}
        }
        public bool Intersects(Vector3 a,Vector3 b,float radius,Vector3 root,Vector3 normal)
        {
            if(!valid)return true;
            var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Expand(radius*2);var lo=Key(bounds.min);var hi=Key(bounds.max);nearby.Clear();
            for(int x=lo.x;x<=hi.x;x++)for(int y=lo.y;y<=hi.y;y++)for(int z=lo.z;z<=hi.z;z++)
                if(grid.TryGetValue(new Vector3Int(x,y,z),out var list))foreach(int id in list)nearby.Add(id);
            foreach(int id in nearby)
            {
                var t=triangles[id];if(!t.bounds.Intersects(bounds))continue;
                if(TriangleIntersects(a,b,radius,root,normal,t.a,t.b,t.c))return true;
            }
            return false;
        }
        static bool TriangleIntersects(Vector3 a,Vector3 b,float radius,Vector3 root,Vector3 normal,Vector3 x,Vector3 y,Vector3 z)
        {
            Vector3 face=Vector3.Cross(y-x,z-x).normalized;
            bool ground=Mathf.Abs(Vector3.Dot(face,normal))>.985f&&Mathf.Abs(Vector3.Dot(x-root,normal))<.015f&&Mathf.Abs(Vector3.Dot(y-root,normal))<.015f&&Mathf.Abs(Vector3.Dot(z-root,normal))<.015f;
            return !ground&&SegmentTriangleSquared(a,b,x,y,z)<radius*radius;
        }
        public static bool TerrainIntersects(TerrainCollider terrain,Vector3 a,Vector3 b,float radius,Vector3 root,Vector3 normal)
        {
            var data=terrain.terrainData;if(!data)return true;int resolution=data.heightmapResolution;
            Vector3 origin=terrain.transform.position;float dx=data.size.x/(resolution-1),dz=data.size.z/(resolution-1);
            int minX=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x)-radius-origin.x)/dx),0,resolution-2),maxX=Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.x,b.x)+radius-origin.x)/dx),0,resolution-2);
            int minZ=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z,b.z)-radius-origin.z)/dz),0,resolution-2),maxZ=Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.z,b.z)+radius-origin.z)/dz),0,resolution-2);
            for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
            {
                Vector3 p=origin+new Vector3(x*dx,data.GetHeight(x,z),z*dz),q=origin+new Vector3((x+1)*dx,data.GetHeight(x+1,z),z*dz),r=origin+new Vector3(x*dx,data.GetHeight(x,z+1),(z+1)*dz),s=origin+new Vector3((x+1)*dx,data.GetHeight(x+1,z+1),(z+1)*dz);
                // Both possible cell diagonals conservatively cover Terrain tessellation.
                if(TriangleIntersects(a,b,radius,root,normal,p,q,r)||TriangleIntersects(a,b,radius,root,normal,q,s,r)||TriangleIntersects(a,b,radius,root,normal,p,q,s)||TriangleIntersects(a,b,radius,root,normal,p,s,r))return true;
            }
            return false;
        }
        static float SegmentTriangleSquared(Vector3 a,Vector3 b,Vector3 x,Vector3 y,Vector3 z)
        {
            Vector3 d=b-a,e1=y-x,e2=z-x,p=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,p);
            if(Mathf.Abs(det)>1e-9f)
            {
                Vector3 q=a-x;float u=Vector3.Dot(q,p)/det;Vector3 cross=Vector3.Cross(q,e1);float v=Vector3.Dot(d,cross)/det,t=Vector3.Dot(e2,cross)/det;
                if(u>=0&&v>=0&&u+v<=1&&t>=0&&t<=1)return 0;
            }
            return Mathf.Min(PointTriangleSquared(a,x,y,z),PointTriangleSquared(b,x,y,z),SegmentSquared(a,b,x,y),SegmentSquared(a,b,y,z),SegmentSquared(a,b,z,x));
        }
        static float PointTriangleSquared(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return ap.sqrMagnitude;
            Vector3 bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return bp.sqrMagnitude;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return(p-(a+ab*(d1/(d1-d3)))).sqrMagnitude;
            Vector3 cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return cp.sqrMagnitude;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return(p-(a+ac*(d2/(d2-d6)))).sqrMagnitude;
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return(p-(b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6))))).sqrMagnitude;
            float denom=va+vb+vc;if(Mathf.Abs(denom)<1e-12f)return Mathf.Min(ap.sqrMagnitude,bp.sqrMagnitude,cp.sqrMagnitude);
            return(p-(a+ab*(vb/denom)+ac*(vc/denom))).sqrMagnitude;
        }
        static float SegmentSquared(Vector3 p,Vector3 q,Vector3 a,Vector3 b)
        {
            Vector3 d1=q-p,d2=b-a,r=p-a;float aa=Vector3.Dot(d1,d1),ee=Vector3.Dot(d2,d2),f=Vector3.Dot(d2,r),s,t;
            if(aa<1e-12f&&ee<1e-12f)return r.sqrMagnitude;
            if(aa<1e-12f){s=0;t=Mathf.Clamp01(f/ee);}
            else{float c=Vector3.Dot(d1,r);if(ee<1e-12f){t=0;s=Mathf.Clamp01(-c/aa);}else{float bb=Vector3.Dot(d1,d2),den=aa*ee-bb*bb;s=den>1e-12f?Mathf.Clamp01((bb*f-c*ee)/den):0;t=(bb*s+f)/ee;if(t<0){t=0;s=Mathf.Clamp01(-c/aa);}else if(t>1){t=1;s=Mathf.Clamp01((bb-c)/aa);}}}
            return(p+d1*s-a-d2*t).sqrMagnitude;
        }
    }
}
