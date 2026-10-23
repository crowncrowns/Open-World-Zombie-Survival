using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace TerrainRoads {
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class TerrainConformingRoad : MonoBehaviour {
 [Tooltip("Общая сеть владеет геометрией перекрёстков и бордюров; Rebuild перестраивает всю сеть.")] public TerrainRoadNetwork network;
 public SplineContainer route;
 public Terrain terrain;
 [Min(0)] public int splineIndex;
 [Min(.2f)] public float width = 6;
 [Min(.1f)] public float sampleStep = 1;
 [Min(0)] public float smoothingLength = 8;
 [Range(.02f,.05f)] public float verticalOffset = .035f;
 [Range(0,.3f)] public float maxCrossSlope = .08f;
 [Min(.1f)] public float textureRepeat = 4;
 [Min(.01f)] public float earthworkTolerance = .15f;
 public bool autoRefresh = true;
 public bool roadCollider;
 [SerializeField, HideInInspector] Mesh generatedMesh;
 [SerializeField, HideInInspector] MeshCollider generatedCollider;
 [NonSerialized] public string diagnostic = "Rebuild required";
 [NonSerialized] public readonly List<Vector3> earthwork = new List<Vector3>();
 public Mesh GeneratedMesh => generatedMesh;
 void OnEnable(){ if(network)return; if(Application.isPlaying) Rebuild();
#if UNITY_EDITOR
 if(!network)UnityEditor.EditorApplication.delayCall += DeferredRebuild;
#endif
 }
#if UNITY_EDITOR
 void DeferredRebuild(){if(this && !Application.isPlaying) Rebuild();}
#endif
 public bool Rebuild(){
  if(network)return network.Rebuild();
  earthwork.Clear();
  try { Build(); return true; }
  catch(Exception e){diagnostic=e.Message; GetComponent<MeshFilter>().sharedMesh=null; if(generatedCollider)generatedCollider.sharedMesh=null; return false;}
 }
 float Height(Vector3 p){
  var q=p-terrain.transform.position; var size=terrain.terrainData.size;
  if(q.x<0||q.z<0||q.x>size.x||q.z>size.z) throw new InvalidOperationException("Road footprint leaves the assigned Terrain.");
  if(terrain.terrainData.IsHole(Mathf.Clamp((int)(q.x/size.x*terrain.terrainData.holesResolution),0,terrain.terrainData.holesResolution-1),Mathf.Clamp((int)(q.z/size.z*terrain.terrainData.holesResolution),0,terrain.terrainData.holesResolution-1))) throw new InvalidOperationException("Terrain hole under road; bridge support is required.");
  return terrain.SampleHeight(p)+terrain.transform.position.y;
 }
 void Build(){
  if(GetComponent<SplineExtrude>())throw new InvalidOperationException("Use a separate road object: SplineExtrude would overwrite this MeshFilter.");
  if(!route || !terrain || !terrain.terrainData)throw new InvalidOperationException("Assign a SplineContainer and Terrain with TerrainData.");
  if(splineIndex<0||splineIndex>=route.Splines.Count||route.Splines[splineIndex].Count<2)throw new InvalidOperationException("Spline index is missing or has fewer than two knots.");
  if(width<.2f||sampleStep<.1f||textureRepeat<.1f||!float.IsFinite(width+sampleStep+smoothingLength+textureRepeat))throw new InvalidOperationException("Invalid dimensions.");
  bool closed=route.Splines[splineIndex].Closed;
  float length=route.CalculateLength(splineIndex);
  if(!float.IsFinite(length)||length<.1f||length/sampleStep>10000)throw new InvalidOperationException("Route too short or exceeds 10000 sections; increase sample step.");
  // Dense arc-length table decouples terrain sampling from spline parameter speed.
  int dense=Mathf.Clamp(Mathf.CeilToInt(length/sampleStep)*8,64,80000);
  var table=new Vector3[dense+1]; var dist=new float[dense+1];
  for(int i=0;i<=dense;i++){table[i]=(Vector3)route.EvaluatePosition(splineIndex,(float)i/dense); if(i>0)dist[i]=dist[i-1]+Vector3.Distance(table[i],table[i-1]);}
  int n=Mathf.Max(closed?8:2,Mathf.CeilToInt(dist[dense]/sampleStep));
  var c=new Vector3[n+1];var right=new Vector3[n+1];var raw=new float[n+1];var bank=new float[n+1];var y=new float[n+1];int cursor=1;
  for(int i=0;i<=n;i++){
   float d=dist[dense]*i/n;while(cursor<dense&&dist[cursor]<d)cursor++;
   c[i]=Vector3.Lerp(table[cursor-1],table[cursor],Mathf.InverseLerp(dist[cursor-1],dist[cursor],d));
  }
  for(int i=0;i<=n;i++){
   Vector3 tangent=c[closed?(i+1)%n:Mathf.Min(i+1,n)]-c[closed?(i+n-1)%n:Mathf.Max(i-1,0)];tangent.y=0;
   if(tangent.sqrMagnitude<.000001f)throw new InvalidOperationException("Duplicate points or vertical route.");
   right[i]=Vector3.Cross(Vector3.up,tangent.normalized);
   raw[i]=Height(c[i]);float a=Height(c[i]-right[i]*width*.5f), b=Height(c[i]+right[i]*width*.5f);
   bank[i]=Mathf.Clamp((b-a)/width,-maxCrossSlope,maxCrossSlope);
  }
  var rawBank=(float[])bank.Clone();
  int radius=Mathf.Clamp(Mathf.CeilToInt(smoothingLength/(dist[dense]/n)),0,128);
  for(int i=0;i<=n;i++){
   float sum=0,slope=0,wSum=0;
   for(int k=-radius;k<=radius;k++){int j=closed?((i+k)%n+n)%n:Mathf.Clamp(i+k,0,n);float w=radius+1-Mathf.Abs(k);sum+=raw[j]*w;slope+=rawBank[j]*w;wSum+=w;}
   y[i]=sum/wSum+verticalOffset; bank[i]=slope/wSum;
  }
  if(closed){c[n]=c[0];right[n]=right[0];y[n]=y[0];bank[n]=bank[0];}
  // Refuse inverted local strips and non-neighbour intersections before replacing mesh.
  for(int i=1;i<n;i++){
   Vector3 a=c[i]-c[i-1], b=c[i+1]-c[i]; a.y=b.y=0;
   float angle=Vector3.Angle(a,b)*Mathf.Deg2Rad;
   if(angle>.001f&&Mathf.Min(a.magnitude,b.magnitude)/angle<width*.6f)throw new InvalidOperationException("Turn radius is too small for this width near section "+i+". Widen the bend or reduce width.");
  }
  var v=new Vector3[(n+1)*5];var uv=new Vector2[v.Length];var world=new Vector3[v.Length];float travel=0;
  for(int i=0;i<=n;i++){
   if(i>0)travel+=Vector3.Distance(c[i],c[i-1]);bool needs=false;
   for(int j=0;j<5;j++){
    float side=(j/4f-.5f)*width;var p=c[i]+right[i]*side;p.y=y[i]+bank[i]*side;
    if(Mathf.Abs(p.y-verticalOffset-Height(p))>earthworkTolerance)needs=true;
    world[i*5+j]=p;v[i*5+j]=transform.InverseTransformPoint(p);uv[i*5+j]=new Vector2(j/4f,travel/textureRepeat);
   }
   if(needs)earthwork.Add(new Vector3(c[i].x,y[i],c[i].z));
  }
  for(int i=0;i<n;i++)for(int j=i+2;j<n;j++){
   if(closed&&i==0&&j==n-1)continue;
   if(Overlaps(world,i,j))throw new InvalidOperationException("Road self-intersection near sections "+i+" / "+j);
  }
  var tri=new int[n*24];int t=0;
  for(int i=0;i<n;i++)for(int j=0;j<4;j++){
   int a=i*5+j,b=a+5;
   if(Vector3.Cross(world[b]-world[a],world[a+1]-world[a]).y<=0||Vector3.Cross(world[b+1]-world[b],world[a+1]-world[b]).y<=0)throw new InvalidOperationException("Folded road section "+i+"; increase turn radius or reduce step/width.");
   tri[t++]=a;tri[t++]=b;tri[t++]=a+1;tri[t++]=a+1;tri[t++]=b;tri[t++]=b+1;
  }
  if(!generatedMesh){generatedMesh=new Mesh{name="Terrain road generated"};generatedMesh.hideFlags=HideFlags.DontSave;}
  generatedMesh.Clear();generatedMesh.indexFormat=IndexFormat.UInt32;generatedMesh.vertices=v;generatedMesh.uv=uv;generatedMesh.triangles=tri;generatedMesh.RecalculateNormals();generatedMesh.RecalculateTangents();generatedMesh.RecalculateBounds();
  if(closed){var normals=generatedMesh.normals;for(int j=0;j<5;j++)normals[j]=normals[n*5+j]=(normals[j]+normals[n*5+j]).normalized;generatedMesh.normals=normals;}
  GetComponent<MeshFilter>().sharedMesh=generatedMesh;
  if(roadCollider){if(!generatedCollider)generatedCollider=gameObject.AddComponent<MeshCollider>();generatedCollider.sharedMesh=null;generatedCollider.sharedMesh=generatedMesh;generatedCollider.enabled=true;}
  else if(generatedCollider){generatedCollider.enabled=false;generatedCollider.sharedMesh=null;}
  diagnostic=$"{n} sections. {earthwork.Count} sections need Terrain grading (orange markers).";
 }
 static Vector3 Corner(Vector3[] v,int section,int corner){return v[(section+(corner>=2?1:0))*5+(corner==1||corner==2?4:0)];}
 static bool Overlaps(Vector3[] v,int a,int b){
  for(int polygon=0;polygon<2;polygon++)for(int edge=0;edge<4;edge++){
   int section=polygon==0?a:b;var p=Corner(v,section,edge);var q=Corner(v,section,(edge+1)%4);var axis=new Vector2(-(q.z-p.z),q.x-p.x);if(axis.sqrMagnitude<.000001f)continue;axis.Normalize();
   float minA=float.PositiveInfinity,maxA=float.NegativeInfinity,minB=float.PositiveInfinity,maxB=float.NegativeInfinity;
   for(int k=0;k<4;k++){p=Corner(v,a,k);q=Corner(v,b,k);float x=p.x*axis.x+p.z*axis.y,y=q.x*axis.x+q.z*axis.y;minA=Mathf.Min(minA,x);maxA=Mathf.Max(maxA,x);minB=Mathf.Min(minB,y);maxB=Mathf.Max(maxB,y);}
   if(maxA<=minB+.001f||maxB<=minA+.001f)return false;
  }return true;
 }
 static float Side(Vector3 a,Vector3 b,Vector3 c)=>(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
 static bool Crosses(Vector3 a,Vector3 b,Vector3 c,Vector3 d)=>Side(a,b,c)*Side(a,b,d)<-.000001f&&Side(c,d,a)*Side(c,d,b)<-.000001f;
 void OnDestroy(){if(generatedMesh){if(Application.isPlaying)Destroy(generatedMesh);else DestroyImmediate(generatedMesh);}}
 void OnDrawGizmosSelected(){Gizmos.color=new Color(1,.5f,0);foreach(var p in earthwork)Gizmos.DrawWireSphere(p,.4f);}
}
}




