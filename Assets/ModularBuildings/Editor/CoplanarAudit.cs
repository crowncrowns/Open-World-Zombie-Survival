using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ZombieSurvival.ModularBuildings.Editor
{
    // Positive-area, same-facing triangle intersections, not merely coincident bounds or shared edges.
    public static class CoplanarAudit
    {
        public sealed class Report
        {
            public int renderers,triangles,overlapPairs;
            public readonly List<string> unreadable=new List<string>();
            public readonly List<string> samples=new List<string>();
            public readonly Dictionary<string,int> conflicts=new Dictionary<string,int>();
            public override string ToString()=>"renderers="+renderers+" triangles="+triangles+" overlaps="+overlapPairs+" unreadable="+unreadable.Count+"\n"+string.Join("\n",conflicts.Select(x=>x.Key+" : "+x.Value))+"\n"+string.Join("\n",unreadable)+"\n"+string.Join("\n",samples);
        }
        sealed class Tri
        {
            public Vector3 a,b,c,n;public float d;public Vector2 u,v,w;public Rect bounds;public string owner;public int index;
        }
        internal static Vector2 Project(Vector3 p,int axis)=>axis==0?new Vector2(p.y,p.z):axis==1?new Vector2(p.x,p.z):new Vector2(p.x,p.y);
        static string Path(Transform t,Transform root){var s=t.name;while(t.parent&&t.parent!=root){t=t.parent;s=t.name+"/"+s;}return s;}
        public static Report Scan(GameObject root,bool stableLocalCoordinates=false)
        {
            var report=new Report();var planes=new Dictionary<string,List<Tri>>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>()){
                var r=filter.GetComponent<MeshRenderer>();if(!r||!r.enabled||!filter.sharedMesh)continue;report.renderers++;
                Vector3[] vertices;int[] indices;
                try{vertices=filter.sharedMesh.vertices;indices=filter.sharedMesh.triangles;}catch(Exception ex){report.unreadable.Add(Path(filter.transform,root.transform)+": "+ex.Message);continue;}
                var matrix=stableLocalCoordinates?RelativeMatrix(filter.transform,root.transform):root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int i=0;i<indices.Length;i+=3){
                    var a=matrix.MultiplyPoint3x4(vertices[indices[i]]);var b=matrix.MultiplyPoint3x4(vertices[indices[i+1]]);var c=matrix.MultiplyPoint3x4(vertices[indices[i+2]]);
                    var cross=Vector3.Cross(b-a,c-a);if(cross.sqrMagnitude<1e-12f)continue;var n=cross.normalized;float d=Vector3.Dot(n,a);
                    int axis=Mathf.Abs(n.x)>Mathf.Abs(n.y)?0:1;if(Mathf.Abs(n.z)>Mathf.Abs(n[axis]))axis=2;
                    var u=Project(a,axis);var v=Project(b,axis);var w=Project(c,axis);
                    var min=Vector2.Min(u,Vector2.Min(v,w));var max=Vector2.Max(u,Vector2.Max(v,w));
                    var t=new Tri{a=a,b=b,c=c,n=n,d=d,u=u,v=v,w=w,bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y),owner=Path(filter.transform,root.transform),index=i/3};
                    string key=Mathf.RoundToInt(n.x*10000)+","+Mathf.RoundToInt(n.y*10000)+","+Mathf.RoundToInt(n.z*10000)+","+Mathf.RoundToInt(d*1000);
                    if(!planes.TryGetValue(key,out var bucket)){bucket=new List<Tri>();planes[key]=bucket;}bucket.Add(t);report.triangles++;
                }
            }
            foreach(var bucket in planes.Values){
                bucket.Sort((a,b)=>a.bounds.xMin.CompareTo(b.bounds.xMin));
                for(int i=0;i<bucket.Count;i++)for(int j=i+1;j<bucket.Count;j++){
                    var a=bucket[i];var b=bucket[j];if(b.bounds.xMin>=a.bounds.xMax-1e-6f)break;
                    if(b.bounds.yMin>=a.bounds.yMax-1e-6f||b.bounds.yMax<=a.bounds.yMin+1e-6f||Vector3.Dot(a.n,b.n)<.999999f)continue;
                    if(Mathf.Abs(Vector3.Dot(a.n,b.a)-a.d)>.00015f||Mathf.Abs(Vector3.Dot(a.n,b.b)-a.d)>.00015f||Mathf.Abs(Vector3.Dot(a.n,b.c)-a.d)>.00015f)continue;
                    float overlapArea=IntersectionArea(a.u,a.v,a.w,b.u,b.v,b.w);if(overlapArea<1e-6f)continue;
                    report.overlapPairs++;
                    var key=string.CompareOrdinal(a.owner,b.owner)<=0?a.owner+" <-> "+b.owner:b.owner+" <-> "+a.owner;
                    if(!report.conflicts.ContainsKey(key))report.conflicts[key]=0;report.conflicts[key]++;
                    if(report.samples.Count<30)report.samples.Add(key+" area="+overlapArea.ToString("G9")+" A="+a.a.ToString("F7")+a.b.ToString("F7")+a.c.ToString("F7")+" B="+b.a.ToString("F7")+b.b.ToString("F7")+b.c.ToString("F7"));
                }
            }
            return report;
        }
        // Avoid subtracting kilometre-scale world coordinates when inspecting a local building.
        internal static Matrix4x4 RelativeMatrix(Transform child,Transform root)
        {
            var matrix=Matrix4x4.identity;
            for(var t=child;t&&t!=root;t=t.parent)matrix=Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale)*matrix;
            return matrix;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        internal static float IntersectionArea(Vector2 a,Vector2 b,Vector2 c,Vector2 x,Vector2 y,Vector2 z)
        {
            var polygon=new List<Vector2>{a,b,c};var clip=new[]{x,y,z};float sign=Mathf.Sign(Cross(y-x,z-x));
            for(int edge=0;edge<3&&polygon.Count>0;edge++){
                var start=clip[edge];var end=clip[(edge+1)%3];var result=new List<Vector2>();
                for(int i=0;i<polygon.Count;i++){
                    var p=polygon[i];var q=polygon[(i+1)%polygon.Count];float dp=sign*Cross(end-start,p-start),dq=sign*Cross(end-start,q-start);
                    bool pin=dp>=-1e-7f,qin=dq>=-1e-7f;if(pin)result.Add(p);if(pin!=qin)result.Add(Vector2.LerpUnclamped(p,q,dp/(dp-dq)));
                }polygon=result;
            }
            if(polygon.Count<3)return 0;var origin=polygon[0];double area=0,perimeter=0;
            for(int i=0;i<polygon.Count;i++){area+=Cross(polygon[i]-origin,polygon[(i+1)%polygon.Count]-origin);perimeter+=(polygon[i]-polygon[(i+1)%polygon.Count]).magnitude;}
            area=Math.Abs(area)*.5;
            // Transform/mesh round-trips are float32. A sub-20-micron edge sliver is a shared edge,
            // not an overlapping material patch; do not manufacture microscopic tessellation there.
            if(perimeter>0&&2*area/perimeter<.00002)return 0;
            return (float)area;
        }
    }
}
