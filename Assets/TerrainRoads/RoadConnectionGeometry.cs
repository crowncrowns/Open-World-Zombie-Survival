using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Clipper2Lib;
namespace TerrainRoads {
[Serializable] public sealed class RoadSurfaceConnection {
 public string id=Guid.NewGuid().ToString("N"),label="Проезд";
 public bool enabled=true,extension,openEnd=true;
 public Vector3 start,end,startOut,endOut,controlA,controlB;
 [Range(.8f,20)] public float width=4;
 [Range(0,4)] public float junctionRadius=1;
}
// Polygon operations are in XZ, using 0.1 mm integer coordinates. TerrainData is read-only.
public static class RoadConnectionGeometry {
 public const double Scale=10000;
 public struct Edge {public Vector3 a,b,outward;public Edge(Vector3 a,Vector3 b){this.a=a;this.b=b;outward=new Vector3(b.z-a.z,0,a.x-b.x).normalized;}}
 public static Point64 P(Vector3 v)=>new Point64((long)Math.Round(v.x*Scale),(long)Math.Round(v.z*Scale));
 public static Vector3 V(Point64 p)=>new Vector3((float)(p.X/Scale),0,(float)(p.Y/Scale));
 static Path64 Positive(params Vector3[] p){var path=new Path64();foreach(var v in p)path.Add(P(v));if(!Clipper.IsPositive(path))path.Reverse();return path;}
 public static bool Contains(Paths64 paths,Vector3 p){int winding=0;var point=P(p);foreach(var path in paths){var hit=Clipper.PointInPolygon(point,path);if(hit==PointInPolygonResult.IsOn)return true;if(hit==PointInPolygonResult.IsInside)winding+=Clipper.IsPositive(path)?1:-1;}return winding!=0;}
 public static Vector3 Evaluate(RoadSurfaceConnection c,float t){var a=c.start-c.startOut*Mathf.Min(2,c.width*.5f);var b=c.extension?c.end:c.end-c.endOut*Mathf.Min(2,c.width*.5f);var ca=c.controlA;var cb=c.controlB;a.y=b.y=ca.y=cb.y=0;float u=1-t;return u*u*u*a+3*u*u*t*ca+3*u*t*t*cb+t*t*t*b;}
 public static List<Vector3> Sample(RoadSurfaceConnection c){
  if(!float.IsFinite(c.width)||c.width<.8f||c.width>20||!float.IsFinite(c.junctionRadius)||c.junctionRadius<0||c.junctionRadius>4)throw new InvalidOperationException("Ширина 0.8–20 м, радиус примыкания 0–4 м.");
  if(Vector2.Distance(new Vector2(c.start.x,c.start.z),new Vector2(c.end.x,c.end.z))<2||Vector2.Distance(new Vector2(c.start.x,c.start.z),new Vector2(c.end.x,c.end.z))>250)throw new InvalidOperationException("Длина проезда должна быть от 2 до 250 м.");
  foreach(var v in new[]{c.start,c.end,c.controlA,c.controlB,c.startOut,c.endOut})if(!float.IsFinite(v.x+v.y+v.z))throw new InvalidOperationException("Некорректные координаты проезда.");
  float estimate=Vector3.Distance(c.start,c.controlA)+Vector3.Distance(c.controlA,c.controlB)+Vector3.Distance(c.controlB,c.end);int n=Mathf.Clamp(Mathf.CeilToInt(estimate/.35f),8,4096);var points=new List<Vector3>();
  for(int i=0;i<=n;i++){var p=Evaluate(c,i/(float)n);p.y=0;if(points.Count==0||Vector3.Distance(points[points.Count-1],p)>.005f)points.Add(p);}
  for(int i=1;i<points.Count-1;i++){var a=points[i]-points[i-1];var b=points[i+1]-points[i];float angle=Vector3.Angle(a,b)*Mathf.Deg2Rad;if(angle>.001f&&Mathf.Min(a.magnitude,b.magnitude)/angle<c.width*.55f)throw new InvalidOperationException(c.label+": слишком тесный изгиб; раздвиньте ручки или уменьшите ширину.");}
  for(int i=0;i<points.Count-1;i++)for(int j=i+2;j<points.Count-1;j++)if(Cross(points[i],points[i+1],points[j])*Cross(points[i],points[i+1],points[j+1])<0&&Cross(points[j],points[j+1],points[i])*Cross(points[j],points[j+1],points[i+1])<0)throw new InvalidOperationException("Проезд пересекает сам себя.");
  return points;
 }
 static float Cross(Vector3 a,Vector3 b,Vector3 c)=>(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
 public static Bounds BoundsOf(Paths64 paths){var b=new Bounds();bool first=true;foreach(var p in paths)foreach(var q in p){var v=V(q);if(first){b=new Bounds(v,Vector3.zero);first=false;}else b.Encapsulate(v);}b.Expand(new Vector3(.02f,10000,.02f));return b;}
 public static Plan Prepare(TerrainRoadSurfaceMap map){
  var active=map.connections.Where(c=>c!=null&&c.enabled).ToArray();if(active.Length==0)return null;if(active.Length>128)throw new InvalidOperationException("Не более 128 соединений на карту.");
  var footprints=new Dictionary<RoadSurfaceConnection,Paths64>();var strokes=new Paths64();float radius=0;foreach(var c in active){var pts=new Path64();foreach(var p in Sample(c))pts.Add(P(p));var footprint=Clipper.InflatePaths(new Paths64{pts},c.width*.5*Scale,JoinType.Round,EndType.Round,2,.02*Scale);footprints[c]=footprint;strokes.AddRange(footprint);radius=Mathf.Max(radius,c.junctionRadius);}
  strokes=Clipper.Union(strokes,FillRule.NonZero);var bounds=BoundsOf(strokes);bounds.Expand(new Vector3(radius*4+12,0,radius*4+12));
  var subject=new Paths64();using(var r=new BinaryReader(new MemoryStream(map.roadData.bytes))){if(r.ReadInt32()!=1)throw new InvalidDataException("Road format");int count=r.ReadInt32();for(int part=0;part<count;part++){r.ReadInt32();int nv=r.ReadInt32(),ni=r.ReadInt32();var v=new Vector3[nv];for(int i=0;i<nv;i++)v[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());for(int i=0;i<ni;i+=3){var a=v[r.ReadInt32()];var b=v[r.ReadInt32()];var c=v[r.ReadInt32()];a.y=b.y=c.y=0;var bb=new Bounds(a,Vector3.zero);bb.Encapsulate(b);bb.Encapsulate(c);bb.Expand(new Vector3(.001f,10000,.001f));if(bounds.Intersects(bb))subject.Add(Positive(a,b,c));}}}
  var original=Clipper.Union(subject,FillRule.NonZero);if(original.Count==0)throw new InvalidOperationException("Выберите край существующей дороги.");
  foreach(var c in active){var other=new Paths64();foreach(var pair in footprints)if(pair.Key!=c)other.AddRange(pair.Value);var support=Clipper.Union(original,other,FillRule.NonZero);if(!Contains(support,c.start-c.startOut*.15f))throw new InvalidOperationException("Первый край не прилегает к исходной дороге. Выберите его заново.");if(!c.extension&&!Contains(support,c.end-c.endOut*.15f))throw new InvalidOperationException("Второй край не прилегает к исходной дороге.");}
  var union=Clipper.Union(original,strokes,FillRule.NonZero);
  if(radius>.001f){var round=Clipper.InflatePaths(union,radius*Scale,JoinType.Round,EndType.Polygon,2,.02*Scale);round=Clipper.InflatePaths(round,-radius*Scale,JoinType.Round,EndType.Polygon,2,.02*Scale);var mask=Clipper.InflatePaths(strokes,(radius*2+.5)*Scale,JoinType.Round,EndType.Polygon,2,.02*Scale);union=Clipper.Union(union,Clipper.Intersect(round,mask,FillRule.NonZero),FillRule.NonZero);}
  var added=Clipper.Difference(union,original,FillRule.NonZero);if(added.Count==0)throw new InvalidOperationException("Проезд целиком внутри существующего покрытия.");
  return new Plan{original=original,added=added,cutMask=Clipper.InflatePaths(Clipper.Union(strokes,added,FillRule.NonZero),.35*Scale,JoinType.Round,EndType.Polygon,2,.01*Scale),outline=Clipper.Union(original,added,FillRule.NonZero),bounds=BoundsOf(added),connections=active};
 }
 public sealed class MeshBuilder {
  public readonly List<Vector3> vertices=new();public readonly List<int> triangles=new();
  public void Tri(Vector3 a,Vector3 b,Vector3 c){if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-12f)return;int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);}
  public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal){if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0){Tri(a,c,b);Tri(a,d,c);}else{Tri(a,b,c);Tri(a,c,d);}}
  public Mesh Mesh(string name,Transform transform){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(vertices.Select(transform.InverseTransformPoint).ToArray());m.SetTriangles(triangles,0);m.SetUVs(0,vertices.Select(p=>new Vector2(p.x/4,p.z/4)).ToArray());m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();return m;}
 }
 public static Vector3 Ground(TerrainRoadSurfaceMap map,Vector3 p,float lift=0){var t=map.terrain;var q=p-t.transform.position;var d=t.terrainData;if(q.x<0||q.z<0||q.x>d.size.x||q.z>d.size.z)throw new InvalidOperationException("Проезд выходит за границы Terrain.");int x=Mathf.Clamp((int)(q.x/d.size.x*d.holesResolution),0,d.holesResolution-1),z=Mathf.Clamp((int)(q.z/d.size.z*d.holesResolution),0,d.holesResolution-1);if(d.IsHole(x,z))throw new InvalidOperationException("Под проездом отверстие Terrain.");p.y=t.SampleHeight(p)+t.transform.position.y+map.contactOffset+lift;return p;}
 public sealed class Plan {
  public Paths64 original,added,outline,cutMask;public Bounds bounds;public RoadSurfaceConnection[] connections;
  public bool Affects(TerrainRoadSurfaceMap.SectionInfo info){return Mathf.Abs(info.x-bounds.center.x)<=bounds.extents.x+102&&Mathf.Abs(info.z-bounds.center.z)<=bounds.extents.z+102;}
  // Reuse a spatial edge index when clipping thousands of curb segments.
  sealed class MaskIndex {
   public readonly List<(Point64 a,Point64 b)> edges=new();
   readonly Dictionary<Vector2Int,List<int>> cells=new();readonly Dictionary<int,List<int>> rows=new();
   static int Cell(double v)=>(int)Math.Floor(v/(Scale*25));
   public MaskIndex(Paths64 paths){foreach(var path in paths)for(int i=0;i<path.Count;i++){
    var a=path[i];var b=path[(i+1)%path.Count];int id=edges.Count;edges.Add((a,b));
    int x0=Cell(Math.Min(a.X,b.X)),x1=Cell(Math.Max(a.X,b.X)),z0=Cell(Math.Min(a.Y,b.Y)),z1=Cell(Math.Max(a.Y,b.Y));
    for(int z=z0;z<=z1;z++){if(!rows.TryGetValue(z,out var row))rows[z]=row=new List<int>();row.Add(id);for(int x=x0;x<=x1;x++){var key=new Vector2Int(x,z);if(!cells.TryGetValue(key,out var cell))cells[key]=cell=new List<int>();cell.Add(id);}}
   }}
   public IEnumerable<(Point64 a,Point64 b)> Near(Vector3 a,Vector3 b){var found=new HashSet<int>();var aa=P(a);var bb=P(b);for(int x=Cell(Math.Min(aa.X,bb.X));x<=Cell(Math.Max(aa.X,bb.X));x++)for(int z=Cell(Math.Min(aa.Y,bb.Y));z<=Cell(Math.Max(aa.Y,bb.Y));z++)if(cells.TryGetValue(new Vector2Int(x,z),out var cell))foreach(int id in cell)if(found.Add(id))yield return edges[id];}
   public bool Inside(Vector3 p){var q=P(p);int winding=0;if(!rows.TryGetValue(Cell(q.Y),out var row))return false;foreach(int id in row){var (a,b)=edges[id];long cross=(b.X-a.X)*(q.Y-a.Y)-(b.Y-a.Y)*(q.X-a.X);if(cross==0&&q.X>=Math.Min(a.X,b.X)&&q.X<=Math.Max(a.X,b.X)&&q.Y>=Math.Min(a.Y,b.Y)&&q.Y<=Math.Max(a.Y,b.Y))return true;if(a.Y<=q.Y){if(b.Y>q.Y&&cross>0)winding++;}else if(b.Y<=q.Y&&cross<0)winding--;}return winding!=0;}
  }
  readonly Dictionary<Paths64,MaskIndex> masks=new();
  IEnumerable<(float,float)> Spans(Vector3 a,Vector3 b,Paths64 mask,bool keepInside){
   if(!masks.TryGetValue(mask,out var index))masks[mask]=index=new MaskIndex(mask);var ts=new List<float>{0,1};var d=b-a;
   foreach(var edge in index.Near(a,b)){var c=V(edge.a);var e=V(edge.b)-c;float det=d.x*e.z-d.z*e.x;if(Mathf.Abs(det)<1e-9f)continue;var q=c-a;float t=(q.x*e.z-q.z*e.x)/det,u=(q.x*d.z-q.z*d.x)/det;if(t>1e-6f&&t<.999999f&&u>=-.00001f&&u<=1.00001f)ts.Add(t);}
   ts.Sort();for(int i=0;i<ts.Count-1;i++){if(ts[i+1]-ts[i]<.000001f)continue;bool inside=index.Inside(Vector3.Lerp(a,b,(ts[i]+ts[i+1])*.5f));if(inside==keepInside)yield return(ts[i],ts[i+1]);}
  }
  sealed class CurbQuad {public Vector3 a,b,c,d;public CurbQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){this.a=a;this.b=b;this.c=c;this.d=d;}}
  readonly List<CurbQuad> retainedCurbs=new(),pendingCurbs=new();bool resolvingCurbs;
  void ResolveCurbs(TerrainRoadSurfaceMap map,MeshBuilder g){
   const double precision=1000000;var all=new List<CurbQuad>(retainedCurbs);int first=all.Count;all.AddRange(pendingCurbs);
   var cells=new Dictionary<Vector2Int,List<int>>();var shapes=new Path64[all.Count];var rectangles=new Rect[all.Count];
   Path64 Shape(CurbQuad q)=>new Path64(new[]{q.a,q.b,q.c,q.d}.Select(v=>new Point64((long)Math.Round(v.x*precision),(long)Math.Round(v.z*precision))));
   var ids=new Dictionary<Point64,int>();var scales=new List<float>();int Vertex(Vector3 v){var key=P(v);if(!ids.TryGetValue(key,out int id)){id=scales.Count;ids[key]=id;scales.Add(1);}return id;}
   var ai=new int[pendingCurbs.Count];var bi=new int[pendingCurbs.Count];for(int i=0;i<pendingCurbs.Count;i++){ai[i]=Vertex(pendingCurbs[i].a);bi[i]=Vertex(pendingCurbs[i].b);}
   var original=pendingCurbs.Select(q=>new CurbQuad(q.a,q.b,q.c,q.d)).ToArray();
   for(int i=0;i<all.Count;i++){var q=all[i];shapes[i]=Shape(q);float x0=Mathf.Min(q.a.x,q.b.x,q.c.x,q.d.x),x1=Mathf.Max(q.a.x,q.b.x,q.c.x,q.d.x),z0=Mathf.Min(q.a.z,q.b.z,q.c.z,q.d.z),z1=Mathf.Max(q.a.z,q.b.z,q.c.z,q.d.z);rectangles[i]=Rect.MinMaxRect(x0,z0,x1,z1);for(int x=Mathf.FloorToInt(x0/5);x<=Mathf.FloorToInt(x1/5);x++)for(int z=Mathf.FloorToInt(z0/5);z<=Mathf.FloorToInt(z1/5);z++){var key=new Vector2Int(x,z);if(!cells.TryGetValue(key,out var list))cells[key]=list=new List<int>();list.Add(i);}}
   var pairs=new HashSet<(int,int)>();foreach(var list in cells.Values)for(int i=0;i<list.Count;i++)if(list[i]>=first)for(int j=0;j<i;j++){int a=Mathf.Max(list[i],list[j]),b=Mathf.Min(list[i],list[j]);if(a>=first&&rectangles[a].Overlaps(rectangles[b]))pairs.Add((a,b));}
   bool finished=false;for(int iteration=0;iteration<26;iteration++){
    for(int i=0;i<pendingCurbs.Count;i++){var q=pendingCurbs[i];var source=original[i];q.c=q.b+(source.c-q.b)*scales[bi[i]];q.d=q.a+(source.d-q.a)*scales[ai[i]];shapes[first+i]=Shape(q);}
    var bad=new HashSet<int>();foreach(var (a,b) in pairs){var cut=Clipper.Intersect(new Paths64{shapes[a]},new Paths64{shapes[b]},FillRule.NonZero);double area=cut.Sum(p=>Math.Abs(Clipper.Area(p)))/(precision*precision);if(area<=.000001)continue;bad.Add(ai[a-first]);bad.Add(bi[a-first]);if(b>=first){bad.Add(ai[b-first]);bad.Add(bi[b-first]);}}
    if(bad.Count==0){finished=true;break;}foreach(int id in bad)scales[id]*=.5f;
   }if(!finished)throw new InvalidOperationException("Не удалось устранить наложение бордюров примыкания.");
   resolvingCurbs=true;try{foreach(var q in pendingCurbs){if(!masks.TryGetValue(outline,out var index))masks[outline]=index=new MaskIndex(outline);if(index.Inside((q.a+q.b+q.c+q.d)*.25f))continue;Curb(g,map,q.a,q.b,q.d,q.c);}}finally{resolvingCurbs=false;}
  }
  void Curb(MeshBuilder g,TerrainRoadSurfaceMap map,Vector3 a,Vector3 b,Vector3 outerA,Vector3 outerB,bool validate=false){
   if(validate){if(!masks.TryGetValue(outline,out var index))masks[outline]=index=new MaskIndex(outline);float Signed(Vector3 u,Vector3 v)=>u.x*v.z-u.z*v.x;for(int i=0;i<24;i++){bool folded=Signed(b-a,outerB-a)>=-1e-10f||Signed(outerB-a,outerA-a)>=-1e-10f;bool inside=index.Inside((a+b+outerA+outerB)*.25f)||index.Inside((a+b+outerB)/3)||index.Inside((a+outerB+outerA)/3);if(!folded&&!inside)break;outerA=Vector3.Lerp(a,outerA,.5f);outerB=Vector3.Lerp(b,outerB,.5f);}if((outerA-a).sqrMagnitude<.000004f||(outerB-b).sqrMagnitude<.000004f||Signed(b-a,outerB-a)>=-1e-10f||Signed(outerB-a,outerA-a)>=-1e-10f||index.Inside((a+b+outerA+outerB)*.25f)||index.Inside((a+b+outerB)/3)||index.Inside((a+outerB+outerA)/3))return;}
   if(validate){pendingCurbs.Add(new CurbQuad(a,b,outerB,outerA));return;}
   if(!resolvingCurbs){var area=new Bounds(a,Vector3.zero);area.Encapsulate(b);area.Expand(new Vector3(1,10000,1));if(area.Intersects(bounds))retainedCurbs.Add(new CurbQuad(a,b,outerB,outerA));}
   var aa=Ground(map,a);var bb=Ground(map,b);var cc=Ground(map,outerB);var dd=Ground(map,outerA);var at=aa+Vector3.up*map.curbHeight;var bt=bb+Vector3.up*map.curbHeight;var ct=cc+Vector3.up*map.curbHeight;var dt=dd+Vector3.up*map.curbHeight;var outwards=(outerA-a+outerB-b).normalized;
   g.Quad(at,bt,ct,dt,Vector3.up);g.Quad(aa,bb,bt,at,-outwards);g.Quad(dd,dt,ct,cc,outwards);
  }
  public Mesh CutCurbs(TerrainRoadSurfaceMap map,Vector3[] raw,int[] indices,string name,Transform transform){
   var g=new MeshBuilder();for(int i=0;i+5<indices.Length;i+=6){var a=raw[indices[i]];var b=raw[indices[i+1]];var c=raw[indices[i+2]];var d=raw[indices[i+5]];if(a.y<.1f||b.y<.1f||c.y<.1f||d.y<.1f)continue;a.y=b.y=c.y=d.y=0;
    var bb=new Bounds(a,Vector3.zero);bb.Encapsulate(b);bb.Expand(new Vector3(.5f,10000,.5f));if(!bb.Intersects(bounds)){Curb(g,map,a,b,d,c);continue;}
    foreach(var span in Spans(a,b,cutMask,false))Curb(g,map,Vector3.Lerp(a,b,span.Item1),Vector3.Lerp(a,b,span.Item2),Vector3.Lerp(d,c,span.Item1),Vector3.Lerp(d,c,span.Item2));
   }return g.Mesh(name,transform);
  }
  MeshBuilder Surface(TerrainRoadSurfaceMap map){
   var g=new MeshBuilder();var t=map.terrain;var origin=t.transform.position;var size=t.terrainData.size;float sx=size.x/(t.terrainData.heightmapResolution-1),sz=size.z/(t.terrainData.heightmapResolution-1);var cells=new HashSet<Vector2Int>();
   foreach(var path in added){if(!Clipper.IsPositive(path))continue;var b=BoundsOf(new Paths64{path});int x0=Mathf.FloorToInt((b.min.x-origin.x)/sx),x1=Mathf.FloorToInt((b.max.x-origin.x)/sx),z0=Mathf.FloorToInt((b.min.z-origin.z)/sz),z1=Mathf.FloorToInt((b.max.z-origin.z)/sz);for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++){cells.Add(new Vector2Int(x,z));if(cells.Count>100000)throw new InvalidOperationException("Слишком большая область проезда.");}}
   foreach(var cell in cells){float x=origin.x+cell.x*sx,z=origin.z+cell.y*sz;var a=new Vector3(x,0,z);var b=new Vector3(x+sx,0,z);var c=new Vector3(x+sx,0,z+sz);var d=new Vector3(x,0,z+sz);
    foreach(var plane in new[]{Positive(a,b,c),Positive(a,c,d)}){var cut=Clipper.Intersect(added,new Paths64{plane},FillRule.NonZero);if(cut.Count==0)continue;var result=Clipper.Triangulate(cut,out var triangles);if(result!=TriangulateResult.success)throw new InvalidOperationException("Не удалось триангулировать примыкание: "+result);foreach(var triangle in triangles){var aa=Ground(map,V(triangle[0]));var bb=Ground(map,V(triangle[1]));var cc=Ground(map,V(triangle[2]));if(Vector3.Cross(bb-aa,cc-aa).y<0)(bb,cc)=(cc,bb);g.Tri(aa,bb,cc);}}
   }return g;
  }
  MeshBuilder NewCurbs(TerrainRoadSurfaceMap map){
   var g=new MeshBuilder();foreach(var path in outline){if(path.Count<3)continue;for(int i=0;i<path.Count;i++){
    var prev=V(path[(i+path.Count-1)%path.Count]);var a=V(path[i]);var b=V(path[(i+1)%path.Count]);var next=V(path[(i+2)%path.Count]);var dir=(b-a).normalized;if((b-a).sqrMagnitude<1e-10)continue;var n=new Vector3(dir.z,0,-dir.x);Vector3 Normal(Vector3 p,Vector3 q){var v=(q-p).normalized;return new Vector3(v.z,0,-v.x);}var na=Normal(prev,a);var nb=Normal(b,next);var ma=(na+n)/Mathf.Max(.5f,1+Vector3.Dot(na,n));var mb=(n+nb)/Mathf.Max(.5f,1+Vector3.Dot(n,nb));
    foreach(var span in Spans(a,b,cutMask,true)){var p=Vector3.Lerp(a,b,span.Item1);var q=Vector3.Lerp(a,b,span.Item2);bool freeCap=connections.Any(c=>c.extension&&c.openEnd&&Vector3.Distance((p+q)*.5f,c.end)<c.width*.5f+.1f&&Vector3.Dot(n,(c.end-c.controlB).normalized)>.15f);if(freeCap)continue;int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(p,q)/.4f));for(int k=0;k<count;k++){float u=Mathf.Lerp(span.Item1,span.Item2,k/(float)count),v=Mathf.Lerp(span.Item1,span.Item2,(k+1)/(float)count);var pa=Vector3.Lerp(a,b,u);var pb=Vector3.Lerp(a,b,v);Curb(g,map,pa,pb,pa+Vector3.Lerp(ma,mb,u)*.18f,pb+Vector3.Lerp(ma,mb,v)*.18f,true);}}
   }}ResolveCurbs(map,g);return g;
  }
  public void Publish(GameObject staging,TerrainRoadSurfaceMap map,Action<string,MeshRenderer> bind){
   void Add(string id,string name,MeshBuilder builder,Material fallback){var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(staging.transform,false);var mesh=builder.Mesh(name,go.transform);go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();var custom=map.materials.Find(m=>m.sectionId==id);r.sharedMaterial=custom!=null&&custom.material?custom.material:fallback;if(map.collision&&mesh.triangles.Length>0)go.AddComponent<MeshCollider>().sharedMesh=mesh;bind(id,r);}
   Add("connection_surface","Проезды — объединённое покрытие",Surface(map),map.asphalt);Add("connection_curbs","Проезды — бордюры и примыкания",NewCurbs(map),map.curbMaterial);
  }
 }
 // Boundary selection uses the actual front edge of the authored curb, never a material seam.
 public sealed class EdgeIndex {
  readonly Dictionary<Vector2Int,List<Edge>> cells=new();readonly Plan plan;
  public EdgeIndex(TerrainRoadSurfaceMap map){plan=map.connectionPlan;if(plan!=null)foreach(var path in plan.outline)for(int i=0;i<path.Count;i++){var a=V(path[i]);var b=V(path[(i+1)%path.Count]);if(!Contains(plan.original,(a+b)*.5f))Add(new Edge(a,b));}using var r=new BinaryReader(new MemoryStream(map.curbData.bytes));r.ReadInt32();int count=r.ReadInt32();for(int part=0;part<count;part++){r.ReadInt32();int nv=r.ReadInt32(),ni=r.ReadInt32();var v=new Vector3[nv];for(int i=0;i<nv;i++)v[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());var tr=new int[ni];for(int i=0;i<ni;i++)tr[i]=r.ReadInt32();for(int i=0;i+5<ni;i+=6){var a=v[tr[i]];var b=v[tr[i+1]];var c=v[tr[i+2]];var d=v[tr[i+5]];if(a.y<.1f||b.y<.1f||c.y<.1f||d.y<.1f)continue;a.y=b.y=0;Add(new Edge(a,b));}}}
  void Add(Edge e){var key=new Vector2Int(Mathf.FloorToInt((e.a.x+e.b.x)/50),Mathf.FloorToInt((e.a.z+e.b.z)/50));if(!cells.TryGetValue(key,out var list)){list=new List<Edge>();cells.Add(key,list);}list.Add(e);}
  public bool Nearest(Vector3 p,float distance,out Vector3 point,out Vector3 outward){point=default;outward=default;bool found=false;p.y=0;var key=new Vector2Int(Mathf.FloorToInt(p.x/25),Mathf.FloorToInt(p.z/25));for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)if(cells.TryGetValue(key+new Vector2Int(x,z),out var list))foreach(var e in list){if(plan!=null&&Contains(plan.cutMask,(e.a+e.b)*.5f+e.outward*.02f)&&Contains(plan.outline,(e.a+e.b)*.5f-e.outward*.02f))continue;var d=e.b-e.a;float t=Mathf.Clamp01(Vector3.Dot(p-e.a,d)/Mathf.Max(.0000001f,d.sqrMagnitude));var q=e.a+d*t;float gap=Vector3.Distance(q,p);if(gap>distance)continue;distance=gap;point=q;outward=e.outward;found=true;}return found;}
 }
}
}




