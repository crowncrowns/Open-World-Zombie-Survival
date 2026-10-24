using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerrainRoads {
// A network owns its generated children only. TerrainData and source splines are read-only.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class TerrainRoadNetwork : MonoBehaviour {
 [Serializable] public class CurbAccess {
  [Tooltip("Расстояние от начала сплайна, м.")] public float from;
  [Tooltip("Расстояние от начала сплайна, м.")] public float to;
  public bool left=true,right=true;
  [Tooltip("Понижение до 2 см вместо полного разрыва.")] public bool lowered=true;
 }
 [Serializable] public class Link {
  public TerrainConformingRoad road;
  [Tooltip("Номер общего узла; -1 — свободный конец.")] public int start=-1,end=-1;
  public bool leftCurb=true,rightCurb=true;
  public List<CurbAccess> accesses=new();
 }
 [Serializable] public class Junction {
  public string label;
  [Tooltip("Общая позиция концов сплайнов в мировых координатах.")] public Vector3 position;
  [Min(2),Tooltip("Расстояние обрезки подходов, м.")] public float radius=7;
  [Tooltip("Поднять площадку дополнительно, м; Terrain не изменяется.")] public float lift;
 }
 public Terrain terrain;
 public Material asphalt,leftCurbMaterial,rightCurbMaterial;
 [Min(.05f)] public float curbWidth=.18f;
 [Min(.02f)] public float curbHeight=.14f;
 [Range(.05f,1f),Tooltip("Максимальный шаг сегментов скругления бордюра, м.")] public float curbCurveStep=.2f;
 [Min(.1f)] public float sampleStep=.65f;
 [Range(.02f,.15f)] public float clearance=.045f;
 public bool collision=true;
 [Min(.1f),Tooltip("Размер повтора текстуры, м.")] public float asphaltRepeat=4;
 public List<Junction> junctions=new();
 public List<Link> links=new();
 [SerializeField,HideInInspector] GameObject generated;
 [NonSerialized] public string diagnostic="Нажмите Rebuild сети";
 [NonSerialized] public List<Vector3> grading=new();
 bool building;
 class Port {public Link link;public bool start;public Vector3 centre,dir,left,right,leftTangent,rightTangent,leftOutward,rightOutward;public float y;}
 class Strip { public Link link;public Vector3[] p,r;public float[] d,y;public float length,from,to; }
 class Geometry {
  public List<Vector3> v=new();public List<Vector2> uv=new();public List<int> tri=new();
  readonly List<int> faceGroups=new();
  public void Triangle(Vector3 a,Vector3 b,Vector3 c){if(Vector3.Cross(b-a,c-a).y<0)(b,c)=(c,b);AddTriangle(a,b,c,0);}
  void AddTriangle(Vector3 a,Vector3 b,Vector3 c,int group){int n=v.Count;faceGroups.Add(group);faceGroups.Add(group);faceGroups.Add(group);v.Add(a);v.Add(b);v.Add(c);uv.Add(new(a.x/4,a.z/4));uv.Add(new(b.x/4,b.z/4));uv.Add(new(c.x/4,c.z/4));tri.Add(n);tri.Add(n+1);tri.Add(n+2);}
  public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
  public void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal,int group){
   if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0)AddTriangle(a,c,b,group);else AddTriangle(a,b,c,group);
   if(Vector3.Dot(Vector3.Cross(c-a,d-a),normal)<0)AddTriangle(a,d,c,group);else AddTriangle(a,c,d,group);
  }
  public Mesh Mesh(string name){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};if(name.StartsWith("Asphalt")){var map=new Dictionary<Vector3Int,int>();var verts=new List<Vector3>();var uvs=new List<Vector2>();var remap=new int[v.Count];for(int i=0;i<v.Count;i++){var p=v[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*1000),Mathf.RoundToInt(p.y*1000),Mathf.RoundToInt(p.z*1000));if(!map.TryGetValue(key,out int id)){id=verts.Count;map.Add(key,id);verts.Add(p);uvs.Add(uv[i]);}remap[i]=id;}m.SetVertices(verts);m.SetUVs(0,uvs);m.SetTriangles(tri.Select(i=>remap[i]).ToArray(),0);}else{m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tri,0);}m.RecalculateNormals();if(!name.StartsWith("Asphalt")){var normals=m.normals;var sums=new Dictionary<(Vector3Int,int),Vector3>();for(int i=0;i<v.Count;i++){var p=v[i];var key=(new Vector3Int(Mathf.RoundToInt(p.x*1000),Mathf.RoundToInt(p.y*1000),Mathf.RoundToInt(p.z*1000)),faceGroups[i]);sums.TryGetValue(key,out var sum);sums[key]=sum+normals[i];}for(int i=0;i<v.Count;i++){var p=v[i];var key=(new Vector3Int(Mathf.RoundToInt(p.x*1000),Mathf.RoundToInt(p.y*1000),Mathf.RoundToInt(p.z*1000)),faceGroups[i]);normals[i]=sums[key].normalized;}m.normals=normals;}m.RecalculateTangents();m.RecalculateBounds();return m;}
 }
 public float Height(Vector3 p){if(!terrain||!terrain.terrainData)throw new InvalidOperationException("Назначьте Terrain.");var q=p-terrain.transform.position;var d=terrain.terrainData;if(q.x<0||q.z<0||q.x>d.size.x||q.z>d.size.z)throw new InvalidOperationException("Сеть выходит за границы Terrain.");int x=Mathf.Clamp((int)(q.x/d.size.x*d.holesResolution),0,d.holesResolution-1),z=Mathf.Clamp((int)(q.z/d.size.z*d.holesResolution),0,d.holesResolution-1);if(d.IsHole(x,z))throw new InvalidOperationException("Под дорогой отверстие Terrain.");return terrain.SampleHeight(p)+terrain.transform.position.y;}
 void OnEnable(){
 if(Application.isPlaying){Rebuild();return;}
#if UNITY_EDITOR
 UnityEditor.EditorApplication.delayCall+=()=>{if(this)Rebuild();};
#endif
 }
 void OnDestroy(){if(generated)Dispose(generated);}
 public bool Rebuild(){
  if(building)return true;building=true;GameObject staging=null;
  try {
   if(!asphalt||!leftCurbMaterial||!rightCurbMaterial)throw new InvalidOperationException("Назначьте три материала.");
   if(sampleStep<.1f||curbWidth<=0||curbHeight<=0||curbCurveStep<.05f||!float.IsFinite(curbCurveStep))throw new InvalidOperationException("Некорректные размеры сети.");
   grading.Clear();var strips=new List<Strip>();var ports=junctions.Select(j=>new List<Port>()).ToArray();
   foreach(var l in links){var r=l.road;if(!r||!r.route)throw new InvalidOperationException("Отсутствует дорога/сплайн.");if(r.width<.5f||r.route.Splines[r.splineIndex].Closed)throw new InvalidOperationException("Нужен открытый сплайн шириной >=0,5м.");
    float len=r.route.CalculateLength(r.splineIndex);int dense=Mathf.Clamp(Mathf.CeilToInt(len/.2f),32,20000);var table=new Vector3[dense+1];var ds=new float[dense+1];
    for(int i=0;i<=dense;i++){table[i]=(Vector3)r.route.EvaluatePosition(r.splineIndex,(float)i/dense);if(i>0)ds[i]=ds[i-1]+Vector3.Distance(table[i],table[i-1]);}
    len=ds[dense];float a=l.start<0?0:Node(l.start).radius,b=len-(l.end<0?0:Node(l.end).radius);if(b-a<2)throw new InvalidOperationException(r.name+": слишком короткий подход.");
    foreach(bool start in new[]{true,false}){int id=start?l.start:l.end;if(id<0)continue;var node=Node(id);var endpoint=table[start?0:dense];if(Vector2.Distance(new(endpoint.x,endpoint.z),new(node.position.x,node.position.z))>.15f)throw new InvalidOperationException(r.name+": конец сплайна не совпадает с общим узлом "+id);}
    int n=Mathf.CeilToInt((b-a)/sampleStep);var s=new Strip{link=l,p=new Vector3[n+1],r=new Vector3[n+1],d=new float[n+1],y=new float[n+1],length=len,from=a,to=b};int cursor=1;
    for(int i=0;i<=n;i++){float d=Mathf.Lerp(a,b,(float)i/n);while(cursor<dense&&ds[cursor]<d)cursor++;s.p[i]=Vector3.Lerp(table[cursor-1],table[cursor],Mathf.InverseLerp(ds[cursor-1],ds[cursor],d));s.d[i]=d;}
    for(int i=0;i<=n;i++){var tangent=s.p[Mathf.Min(n,i+1)]-s.p[Mathf.Max(0,i-1)];tangent.y=0;if(tangent.sqrMagnitude<.00001f)throw new InvalidOperationException("Повторная точка сплайна.");s.r[i]=Vector3.Cross(Vector3.up,tangent.normalized);float bank=Mathf.Clamp((Height(s.p[i]+s.r[i]*r.width*.5f)-Height(s.p[i]-s.r[i]*r.width*.5f))/r.width,-r.maxCrossSlope,r.maxCrossSlope);s.y[i]=Height(s.p[i]);for(int cross=0;cross<=8;cross++){float side=r.width*(cross/8f-.5f);s.y[i]=Mathf.Max(s.y[i],Height(s.p[i]+s.r[i]*side)-bank*side);}s.y[i]+=clearance+.01f;s.r[i].y=bank;}
    // A local upper envelope smooths vertical profile without cutting into Terrain.
    var raw=(float[])s.y.Clone();int radius=Mathf.Clamp(Mathf.CeilToInt(r.smoothingLength/sampleStep),1,50);
    for(int i=0;i<=n;i++){float sum=0,w=0;for(int k=-radius;k<=radius;k++){float weight=radius+1-Mathf.Abs(k);sum+=raw[Mathf.Clamp(i+k,0,n)]*weight;w+=weight;}s.y[i]=Mathf.Max(raw[i],sum/w);}
    foreach(bool start in new[]{true,false}){int id=start?l.start:l.end;if(id<0)continue;int i=start?0:n;var direction=start?-Vector3.Cross(Vector3.up,s.r[i]):Vector3.Cross(Vector3.up,s.r[i]); // outward from node
     direction=start?(s.p[1]-s.p[0]).normalized:(s.p[n-1]-s.p[n]).normalized;direction.y=0;direction.Normalize();var right=Vector3.Cross(Vector3.up,direction);ports[id].Add(new Port{link=l,start=start,centre=s.p[i],dir=direction,left=s.p[i]-right*r.width*.5f,right=s.p[i]+right*r.width*.5f});}
    strips.Add(s);
   }
   var nodeY=new float[junctions.Count];var gradients=new Vector2[junctions.Count];
   for(int j=0;j<ports.Length;j++){
    if(ports[j].Count<2)throw new InvalidOperationException("Узел требует минимум два подхода.");var origin=junctions[j].position;
    var slope=new Vector2((Height(origin+Vector3.right*3)-Height(origin-Vector3.right*3))/6,(Height(origin+Vector3.forward*3)-Height(origin-Vector3.forward*3))/6);gradients[j]=slope;
    float PlaneOffset(Vector3 p)=>slope.x*(p.x-origin.x)+slope.y*(p.z-origin.z);
    float y=Height(origin)+clearance+.01f;
    foreach(var port in ports[j]){y=Mathf.Max(y,Height(port.left)-PlaneOffset(port.left)+clearance+.01f);y=Mathf.Max(y,Height(port.right)-PlaneOffset(port.right)+clearance+.01f);}
    foreach(var strip in strips){if(strip.link.start==j)for(int i=0;i<strip.p.Length&&strip.d[i]-strip.from<=12;i++)y=Mathf.Max(y,strip.y[i]-PlaneOffset(strip.p[i]));if(strip.link.end==j)for(int i=strip.p.Length-1;i>=0&&strip.to-strip.d[i]<=12;i--)y=Mathf.Max(y,strip.y[i]-PlaneOffset(strip.p[i]));}
    float extent=junctions[j].radius+ports[j].Max(p=>p.link.road.width)*.5f;
    for(float x=-extent;x<=extent;x+=.5f)for(float z=-extent;z<=extent;z+=.5f)y=Mathf.Max(y,Height(origin+new Vector3(x,0,z))-slope.x*x-slope.y*z+clearance+.01f);
    nodeY[j]=y+junctions[j].lift;
    foreach(var port in ports[j]){port.left.y=nodeY[j]+PlaneOffset(port.left);port.right.y=nodeY[j]+PlaneOffset(port.right);port.centre.y=nodeY[j]+PlaneOffset(port.centre);}
   }
   float PlaneY(int j,Vector3 p)=>nodeY[j]+gradients[j].x*(p.x-junctions[j].position.x)+gradients[j].y*(p.z-junctions[j].position.z);
   var asphaltGeo=new Geometry();var leftGeo=new Geometry();var rightGeo=new Geometry();
   foreach(var s in strips){int n=s.p.Length-1;var l=s.link;float half=l.road.width*.5f;
    for(int i=0;i<=n;i++){
     float ds=s.d[i]-s.from,de=s.to-s.d[i];float ws=l.start<0?0:1-Mathf.SmoothStep(0,1,Mathf.Clamp01(ds/12)),we=l.end<0?0:1-Mathf.SmoothStep(0,1,Mathf.Clamp01(de/12));
     if(ds<.001f&&l.start>=0){ws=1;we=0;}else if(de<.001f&&l.end>=0){ws=0;we=1;}else if(ws+we>1){float sum=ws+we;ws/=sum;we/=sum;}
     float wraw=1-ws-we;float y=s.y[i]*wraw,bank=s.r[i].y*wraw;
     if(l.start>=0){y+=PlaneY(l.start,s.p[i])*ws;bank+=(gradients[l.start].x*s.r[i].x+gradients[l.start].y*s.r[i].z)*ws;}
     if(l.end>=0){y+=PlaneY(l.end,s.p[i])*we;bank+=(gradients[l.end].x*s.r[i].x+gradients[l.end].y*s.r[i].z)*we;}
     s.p[i].y=y;s.r[i].y=bank;if(y-Height(s.p[i])>.2f)grading.Add(s.p[i]);
    }
    // Share the exact curb cross-sections and edge tangents with the junction.
    foreach(bool start in new[]{true,false}){int id=start?l.start:l.end;if(id<0)continue;var port=ports[id].Single(p=>p.link==l&&p.start==start);int end=start?0:n,adj=start?1:n-1;var outwardLeft=s.r[end]*(start?-1:1);var adjacentLeft=s.r[adj]*(start?-1:1);
     port.left=s.p[end]+outwardLeft*half;port.right=s.p[end]-outwardLeft*half;port.leftOutward=outwardLeft;port.rightOutward=-outwardLeft;
     port.leftTangent=(s.p[adj]+adjacentLeft*half)-port.left;port.rightTangent=(s.p[adj]-adjacentLeft*half)-port.right;port.leftTangent.y=port.rightTangent.y=0;port.leftTangent.Normalize();port.rightTangent.Normalize();
    }
    for(int i=0;i<n;i++){var a=s.p[i]-s.r[i]*half;var b=s.p[i]+s.r[i]*half;var c=s.p[i+1]+s.r[i+1]*half;var d=s.p[i+1]-s.r[i+1]*half;
     if(Vector3.Cross(d-a,b-a).y<=0||Vector3.Cross(c-d,b-d).y<=0)throw new InvalidOperationException(l.road.name+": складка полотна, уменьшите ширину.");asphaltGeo.Quad(a,d,c,b);
     if(l.leftCurb)Curb(leftGeo,a,d,-s.r[i],-s.r[i+1],CurbLevel(l,s.d[i],true),CurbLevel(l,s.d[i+1],true));
     if(l.rightCurb)Curb(rightGeo,b,c,s.r[i],s.r[i+1],CurbLevel(l,s.d[i],false),CurbLevel(l,s.d[i+1],false));
    }
   }
   for(int j=0;j<ports.Length;j++){
    var pp=ports[j].OrderBy(p=>Mathf.Atan2(p.dir.z,p.dir.x)).ToList();var boundary=new List<Vector3>();
    for(int k=0;k<pp.Count;k++){var p=pp[k];var q=pp[(k+1)%pp.Count];boundary.Add(p.right);boundary.Add(p.left);
     var a=p.left;var d=q.right;var start=-p.leftTangent;var finish=q.rightTangent;
     float chord=Vector2.Distance(new(a.x,a.z),new(d.x,d.z));float ca=chord/3,cb=chord/3;
     float det=start.x*finish.z-start.z*finish.x;
     if(Mathf.Abs(det)>.001f){var delta=d-a;float ta=(delta.x*finish.z-delta.z*finish.x)/det;float tb=(delta.x*start.z-delta.z*start.x)/det;
      if(ta>0&&tb<0){ca=Mathf.Min(ta*.55228475f,chord*.8f);cb=Mathf.Min(-tb*.55228475f,chord*.8f);}}
     var b=a+start*ca;var c=d-finish*cb;
     Vector3 Curve(float t){float u=1-t;var v=u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;v.y=PlaneY(j,v);return v;}
     Vector3 Derivative(float t){float u=1-t;var v=3*u*u*(b-a)+6*u*t*(c-b)+3*t*t*(d-c);v.y=0;return v.normalized;}
     float length=0;var last=a;for(int i=1;i<=64;i++){var v=Curve(i/64f);length+=Vector3.Distance(last,v);last=v;}
     int steps=Mathf.Clamp(Mathf.CeilToInt(length/curbCurveStep),2,512);var path=new List<Vector3>();var outward=new List<Vector3>();
     for(int i=0;i<=steps;i++){float t=(float)i/steps;var v=i==0?a:i==steps?d:Curve(t);var normal=Vector3.Cross(Vector3.up,Derivative(t));normal.y=gradients[j].x*normal.x+gradients[j].y*normal.z;
      if(i==0)normal=p.leftOutward;else if(i==steps)normal=q.rightOutward;
      path.Add(v);outward.Add(normal);if(i>0&&i<steps)boundary.Add(v);
     }
     // Both ends use the same profile as their neighbouring road-side curb.
     float h0=CurbLevel(p.link,p.start?junctions[j].radius:p.link.road.route.CalculateLength(p.link.road.splineIndex)-junctions[j].radius,p.start);
     float h1=CurbLevel(q.link,q.start?junctions[j].radius:q.link.road.route.CalculateLength(q.link.road.splineIndex)-junctions[j].radius,!q.start);
     bool enabled0=p.start?p.link.leftCurb:p.link.rightCurb,enabled1=q.start?q.link.rightCurb:q.link.leftCurb;
     if(!enabled0)h0=0;if(!enabled1)h1=0;
     for(int i=0;i<steps;i++)Curb(leftGeo,path[i],path[i+1],outward[i],outward[i+1],Mathf.Lerp(h0,h1,Mathf.SmoothStep(0,1,(float)i/steps)),Mathf.Lerp(h0,h1,Mathf.SmoothStep(0,1,(float)(i+1)/steps)));
    }
    try{Triangulate(boundary,asphaltGeo);}catch(Exception e){throw new InvalidOperationException("Узел "+j+" "+junctions[j].label+": "+e.Message);}
   }
   ValidateOverlap(asphaltGeo);
   staging=new GameObject("Generated road network");staging.transform.SetParent(transform,false);
   Publish(staging,"Asphalt — continuous network",asphaltGeo,asphalt);Publish(staging,"Left curbs and junction returns",leftGeo,leftCurbMaterial);Publish(staging,"Right curbs",rightGeo,rightCurbMaterial);
   if(generated)Dispose(generated);generated=staging;staging=null;
   foreach(var l in links){l.road.GetComponent<MeshRenderer>().enabled=false;var c=l.road.GetComponent<MeshCollider>();if(c)c.enabled=false;}
   diagnostic=$"{links.Count} участков, {junctions.Count} общих узлов; {asphaltGeo.tri.Count/3} треугольников асфальта. {grading.Count} отметок планировки. Terrain не изменён.";return true;
  }catch(Exception e){if(staging)Dispose(staging);diagnostic=e.Message;Debug.LogWarning("Road network: "+e.Message,this);return false;}finally{building=false;}
 }
 Junction Node(int index){if(index<0||index>=junctions.Count)throw new InvalidOperationException("Неверный индекс узла.");return junctions[index];}
 float CurbLevel(Link l,float d,bool left){float h=curbHeight;foreach(var a in l.accesses){if(!(left?a.left:a.right))continue;float fade=Mathf.Min(Mathf.Clamp01((d-a.from)/.7f),Mathf.Clamp01((a.to-d)/.7f));if(d>=a.from&&d<=a.to)h=Mathf.Min(h,Mathf.Lerp(curbHeight,a.lowered?.02f:0,fade));}return h;}
 void Curb(Geometry g,Vector3 a,Vector3 b,Vector3 outwardA,Vector3 outwardB,float ha,float hb){if(ha<=.001f&&hb<=.001f)return;var c=b+outwardB*curbWidth;var d=a+outwardA*curbWidth;var at=a+Vector3.up*ha;var bt=b+Vector3.up*hb;var ct=c+Vector3.up*hb;var dt=d+Vector3.up*ha;var outward=(outwardA+outwardB).normalized;g.Face(at,bt,ct,dt,Vector3.up,1);g.Face(a,b,bt,at,-outward,2);g.Face(d,dt,ct,c,outward,3);}
 void Publish(GameObject parent,string label,Geometry g,Material mat){var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent.transform,false);var mesh=g.Mesh(label);mesh.vertices=mesh.vertices.Select(v=>go.transform.InverseTransformPoint(v)).ToArray();mesh.uv=mesh.uv.Select(v=>v*(4/Mathf.Max(.1f,asphaltRepeat))).ToArray();mesh.RecalculateBounds();mesh.hideFlags=HideFlags.DontSave;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;if(collision&&g.tri.Count>0)go.AddComponent<MeshCollider>().sharedMesh=mesh;}
 static void Dispose(GameObject go){foreach(var f in go.GetComponentsInChildren<MeshFilter>()){var m=f.sharedMesh;if(m&&(m.hideFlags&HideFlags.DontSave)!=0){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}}if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
 static float Cross(Vector3 a,Vector3 b,Vector3 c)=>(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
 static void ValidateOverlap(Geometry g){var cells=new Dictionary<Vector2Int,List<int>>();var tested=new HashSet<long>();int count=g.tri.Count/3;for(int i=0;i<count;i++){var a=g.v[g.tri[i*3]];var b=g.v[g.tri[i*3+1]];var c=g.v[g.tri[i*3+2]];int minX=Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)/10),maxX=Mathf.FloorToInt(Mathf.Max(a.x,b.x,c.x)/10),minZ=Mathf.FloorToInt(Mathf.Min(a.z,b.z,c.z)/10),maxZ=Mathf.FloorToInt(Mathf.Max(a.z,b.z,c.z)/10);for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++){var key=new Vector2Int(x,z);if(!cells.TryGetValue(key,out var list)){list=new List<int>();cells.Add(key,list);}foreach(int j in list)if(tested.Add(((long)i<<32)|(uint)j)&&TriangleOverlap(g,i,j))throw new InvalidOperationException("Перекрытие асфальта: оформите пересечение общим узлом либо исправьте сплайн.");list.Add(i);}}}
 static bool TriangleOverlap(Geometry g,int i,int j){var origin=g.v[g.tri[i*3]];for(int polygon=0;polygon<2;polygon++)for(int k=0;k<3;k++){int t=polygon==0?i:j;var a=g.v[g.tri[t*3+k]];var b=g.v[g.tri[t*3+(k+1)%3]];var axis=new Vector2(-(b.z-a.z),b.x-a.x).normalized;float minA=float.PositiveInfinity,maxA=float.NegativeInfinity,minB=float.PositiveInfinity,maxB=float.NegativeInfinity;for(int p=0;p<3;p++){a=g.v[g.tri[i*3+p]]-origin;b=g.v[g.tri[j*3+p]]-origin;float u=a.x*axis.x+a.z*axis.y,v=b.x*axis.x+b.z*axis.y;minA=Mathf.Min(minA,u);maxA=Mathf.Max(maxA,u);minB=Mathf.Min(minB,v);maxB=Mathf.Max(maxB,v);}if(maxA<=minB+.001f||maxB<=minA+.001f)return false;}return true;}
 static void Triangulate(List<Vector3> p,Geometry g){for(int pass=0;pass<4;pass++)for(int i=p.Count-1;i>=0&&p.Count>3;i--){var a=p[(i+p.Count-1)%p.Count];var b=p[i];var c=p[(i+1)%p.Count];if((a-b).sqrMagnitude<.000001f||Mathf.Abs(Cross(a,b,c))<.0001f)p.RemoveAt(i);}float area=0;for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];area+=a.x*b.z-b.x*a.z;}var indices=Enumerable.Range(0,p.Count).ToList();if(area<0)indices.Reverse();int guard=0;while(indices.Count>2){bool found=false;for(int k=0;k<indices.Count;k++){int a=indices[(k+indices.Count-1)%indices.Count],b=indices[k],c=indices[(k+1)%indices.Count];if(Cross(p[a],p[b],p[c])<=.00001f)continue;bool inside=false;foreach(int q in indices)if(q!=a&&q!=b&&q!=c&&Cross(p[a],p[b],p[q])>-.00001f&&Cross(p[b],p[c],p[q])>-.00001f&&Cross(p[c],p[a],p[q])>-.00001f){inside=true;break;}if(inside)continue;g.Triangle(p[a],p[b],p[c]);indices.RemoveAt(k);found=true;break;}if(!found||++guard>10000)throw new InvalidOperationException("Самопересечение площадки узла — исправьте направления/радиус.");}}
 void OnDrawGizmosSelected(){Gizmos.color=new Color(1,.5f,0);foreach(var p in grading)Gizmos.DrawWireSphere(p,.3f);foreach(var j in junctions){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(j.position,1);}}
}
}
