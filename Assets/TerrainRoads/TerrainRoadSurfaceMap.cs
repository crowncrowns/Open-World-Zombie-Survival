using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerrainRoads {
// Full-map road footprint, split into stable material regions. Never edits TerrainData.
[ExecuteAlways,DisallowMultipleComponent]
public sealed class TerrainRoadSurfaceMap : MonoBehaviour {
 [Serializable] public class MaterialOverride {public string sectionId;public Material material;}
 [Serializable] public class SectionInfo {public string id,kind;public float x,z,area;public int triangles;}
 public Terrain terrain;
 public TextAsset roadData,sectionData;
 public Material asphalt;
 public TextAsset curbData,curbSectionData;
 public Material curbMaterial;
 [Min(.01f)] public float curbHeight=.14f;
 public bool collision=true;
 [Min(.005f)] public float contactOffset=.02f;
 public List<MaterialOverride> materials=new();
 public List<RoadSurfaceConnection> connections=new();
 [NonSerialized] public RoadConnectionGeometry.Plan connectionPlan;
 [SerializeField,HideInInspector] GameObject generated;
 [NonSerialized] public string diagnostic;
 class Binding {public string id;public MeshRenderer renderer;public Material applied;}
 readonly List<Binding> bindings=new();
 bool building;
 [NonSerialized] GameObject currentGenerated;
 public static event Action<TerrainRoadSurfaceMap> Built;
 void OnEnable(){
  if(Application.isPlaying){Rebuild();return;}
#if UNITY_EDITOR
  UnityEditor.EditorApplication.delayCall+=()=>{if(this&&enabled)Rebuild();};
#endif
 }
 void OnDisable(){CaptureMaterials();}
 void OnDestroy(){var old=currentGenerated?currentGenerated:generated;if(old)Dispose(old);}
 public void CaptureMaterials(){foreach(var b in bindings){if(!b.renderer||b.renderer.sharedMaterial==b.applied)continue;
#if UNITY_EDITOR
  if(!Application.isPlaying)UnityEditor.Undo.RecordObject(this,"Материал участка дороги");
#endif
  var entry=materials.Find(m=>m.sectionId==b.id);if(entry==null){entry=new MaterialOverride{sectionId=b.id};materials.Add(entry);}entry.material=b.renderer.sharedMaterial;b.applied=entry.material;
#if UNITY_EDITOR
  if(!Application.isPlaying)UnityEditor.EditorUtility.SetDirty(this);
#endif
 }}
 [ContextMenu("Rebuild road surfaces")]
 public bool Rebuild(){
  if(building)return false;CaptureMaterials();building=true;GameObject staging=null;
  try{
   if(!terrain||!roadData||!sectionData||!asphalt)throw new InvalidOperationException("Назначьте Terrain, roadData, sectionData и Asphalt.");
   var plan=RoadConnectionGeometry.Prepare(this);
   var info=Newtonsoft.Json.JsonConvert.DeserializeObject<SectionInfo[]>(sectionData.text);
   staging=new GameObject("Generated full Terrain roads");staging.transform.SetParent(transform,false);
   var next=new List<Binding>();var groups=new Dictionary<Vector2Int,Transform>();int triangles=0;
   using var reader=new BinaryReader(new MemoryStream(roadData.bytes));if(reader.ReadInt32()!=1)throw new InvalidDataException("Road data version");int count=reader.ReadInt32();if(count!=info.Length)throw new InvalidDataException("Road section count");
   for(int part=0;part<count;part++){
    int index=reader.ReadInt32(),nv=reader.ReadInt32(),ni=reader.ReadInt32();if(index!=part||nv<3||ni%3!=0)throw new InvalidDataException("Road mesh header");
    var vertices=new Vector3[nv];var raw=new Vector3[nv];var uv=new Vector2[nv];var normals=new Vector3[nv];var origin=terrain.transform.position;var size=terrain.terrainData.size;
    for(int i=0;i<nv;i++){float x=reader.ReadSingle();reader.ReadSingle();float z=reader.ReadSingle();var world=new Vector3(x,0,z);world.y=terrain.SampleHeight(world)+origin.y+contactOffset;vertices[i]=staging.transform.InverseTransformPoint(world);uv[i]=new Vector2(x/4,z/4);normals[i]=staging.transform.InverseTransformDirection(terrain.terrainData.GetInterpolatedNormal((x-origin.x)/size.x,(z-origin.z)/size.z));}
    var indices=new int[ni];for(int i=0;i<ni;i++)indices[i]=reader.ReadInt32();triangles+=ni/3;
    var entry=info[part];var key=new Vector2Int(Mathf.FloorToInt(entry.x/200),Mathf.FloorToInt(entry.z/200));
    if(!groups.TryGetValue(key,out var parent)){var group=new GameObject($"Сектор X{key.x:00} Z{key.y:00} — 200 м");group.transform.SetParent(staging.transform,false);parent=group.transform;groups.Add(key,parent);}
    var go=new GameObject($"{(entry.kind=="junction"?"Узел":"Дорога")} {entry.id} — X{entry.x:0} Z{entry.z:0}");go.transform.SetParent(parent,false);
    var mesh=new Mesh{name="Road "+entry.id,indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.uv=uv;mesh.normals=normals;mesh.triangles=indices;mesh.RecalculateTangents();mesh.RecalculateBounds();
    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();var custom=materials.Find(m=>m.sectionId==entry.id);renderer.sharedMaterial=custom!=null&&custom.material?custom.material:asphalt;
    if(collision&&mesh.triangles.Length>0)go.AddComponent<MeshCollider>().sharedMesh=mesh;
    next.Add(new Binding{id=entry.id,renderer=renderer,applied=renderer.sharedMaterial});
   }
   int curbTriangles=BuildCurbs(staging,next,plan);
   plan?.Publish(staging,this,(id,renderer)=>next.Add(new Binding{id=id,renderer=renderer,applied=renderer.sharedMaterial}));
   var previous=currentGenerated?currentGenerated:generated;if(previous)Dispose(previous);generated=staging;currentGenerated=staging;staging=null;bindings.Clear();bindings.AddRange(next);connectionPlan=plan;diagnostic=$"{count} участков, {triangles} треугольников дорог, {curbTriangles} треугольников бордюров; TerrainData не изменён. Проездов: "+connections.Count+".";Built?.Invoke(this);return true;
  }catch(Exception e){if(staging)Dispose(staging);diagnostic=e.Message;Debug.LogWarning("Full Terrain roads: "+e.Message,this);return false;}finally{building=false;}
 }
 int BuildCurbs(GameObject parent,List<Binding> next,RoadConnectionGeometry.Plan plan){
  if(!curbData&&!curbSectionData)return 0;
  if(!curbData||!curbSectionData||!curbMaterial||!float.IsFinite(curbHeight)||curbHeight<=0)throw new InvalidOperationException("Назначьте данные и материал бордюров, высоту > 0.");
  var info=Newtonsoft.Json.JsonConvert.DeserializeObject<SectionInfo[]>(curbSectionData.text);
  var root=new GameObject("Бордюры — непрерывные внешние контуры");root.transform.SetParent(parent.transform,false);
  using var reader=new BinaryReader(new MemoryStream(curbData.bytes));if(reader.ReadInt32()!=1)throw new InvalidDataException("Curb data version");int count=reader.ReadInt32();if(count!=info.Length)throw new InvalidDataException("Curb section count");int triangles=0;
  for(int part=0;part<count;part++){
   int index=reader.ReadInt32(),nv=reader.ReadInt32(),ni=reader.ReadInt32();if(index!=part||nv<3||ni%3!=0)throw new InvalidDataException("Curb mesh header");
   var vertices=new Vector3[nv];var raw=new Vector3[nv];var uv=new Vector2[nv];
   for(int i=0;i<nv;i++){float x=reader.ReadSingle(),height=reader.ReadSingle(),z=reader.ReadSingle();raw[i]=new Vector3(x,height,z);var world=new Vector3(x,0,z);world.y=terrain.SampleHeight(world)+terrain.transform.position.y+contactOffset+height*(curbHeight/.14f);vertices[i]=root.transform.InverseTransformPoint(world);uv[i]=new Vector2(x,z);}
   var indices=new int[ni];for(int i=0;i<ni;i++)indices[i]=reader.ReadInt32();triangles+=ni/3;
   var entry=info[part];var go=new GameObject($"Бордюры {entry.id} — X{entry.x:0} Z{entry.z:0}");go.transform.SetParent(root.transform,false);
   Mesh mesh;if(plan!=null&&plan.Affects(entry))mesh=plan.CutCurbs(this,raw,indices,"Curbs "+entry.id,go.transform);else{mesh=new Mesh{name="Curbs "+entry.id,indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();}
   go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();var custom=materials.Find(m=>m.sectionId==entry.id);renderer.sharedMaterial=custom!=null&&custom.material?custom.material:curbMaterial;
   if(collision&&mesh.triangles.Length>0)go.AddComponent<MeshCollider>().sharedMesh=mesh;
   next.Add(new Binding{id=entry.id,renderer=renderer,applied=renderer.sharedMaterial});
  }
  return triangles;
 }
 static void Dispose(GameObject go){foreach(var f in go.GetComponentsInChildren<MeshFilter>(true)){if(f.sharedMesh){if(Application.isPlaying)Destroy(f.sharedMesh);else DestroyImmediate(f.sharedMesh);}}if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
}
}


