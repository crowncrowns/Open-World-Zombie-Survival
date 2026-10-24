using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZombieSurvival.ModularBuildings.Editor
{
 public static class BuildingBaseTrim
 {
  public const string DefaultMaterial="Assets/Maps/Buildings/Textures/WeatheredPlinth_01/M_WeatheredPlinth_01_URP.mat";
  public static Material MaterialFor(GeneratedBuilding b,Material requested=null)=>requested?requested:b.baseTrimMaterialOverride?b.baseTrimMaterialOverride:b.profile.baseTrimMaterial?b.profile.baseTrimMaterial:AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterial);
  public static Bounds LocalBounds(Transform root,IEnumerable<MeshFilter> filters){
   bool first=true;var bounds=new Bounds();
   foreach(var f in filters){if(!f.sharedMesh)continue;var box=f.sharedMesh.bounds;var m=CoplanarAudit.RelativeMatrix(f.transform,root);
    for(int i=0;i<8;i++){var p=m.MultiplyPoint3x4(box.center+Vector3.Scale(box.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
   }if(first)throw new InvalidOperationException("Не найдена геометрия для обмера цоколя.");return bounds;
  }
  public static Bounds Foundation(GeneratedBuilding b){var f=b.transform.Find("TerrainFoundation");if(!f)throw new InvalidOperationException("Нет основания дома.");return LocalBounds(b.transform,f.GetComponentsInChildren<MeshFilter>(true));}
  public static float WindowBottom(GeneratedBuilding b){
   var frames=BuildingExteriorMaterials.Floors(b).Where(t=>t.name=="Floor_1").SelectMany(t=>t.GetComponentsInChildren<MeshFilter>(true)).Where(f=>f.name=="Interactive_Window_Fixed_Frame"||f.name=="Interactive_Window_Sill").ToArray();
   return frames.Length>0?LocalBounds(b.transform,frames).min.y:.9f;
  }
  public static List<Vector2> DoorGaps(GeneratedBuilding b){
   var gaps=new List<Vector2>();
   foreach(var floor in BuildingExteriorMaterials.Floors(b).Where(t=>t.name=="Floor_1"))foreach(Transform module in floor){
    if(!module.name.StartsWith("Front_",StringComparison.Ordinal))continue;var door=module.Find("EntranceDoor");if(!door)continue;
    var frames=door.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Door_Frame_",StringComparison.Ordinal)).ToArray();
    if(frames.Length>0){var bounds=LocalBounds(b.transform,frames);gaps.Add(new Vector2(bounds.min.x-.02f,bounds.max.x+.02f));}
    else{float x=CoplanarAudit.RelativeMatrix(module,b.transform).MultiplyPoint3x4(Vector3.zero).x;gaps.Add(new Vector2(x-.52f,x+.52f));}
   }return gaps.OrderBy(x=>x.x).ToList();
  }
  public static GameObject Add(GeneratedBuilding b,Material requested=null){
   if(!b||!b.profile)throw new InvalidOperationException("Выберите дом с профилем.");
   if(b.transform.Find("BaseTrim"))throw new InvalidOperationException("Цоколь уже существует; повторное добавление остановлено.");
   var material=MaterialFor(b,requested);if(!material)throw new InvalidOperationException("Не найден материал цоколя.");
   if(material==BuildingExteriorMaterials.EffectiveMaterial(b))throw new InvalidOperationException("Выберите для цоколя материал другого цвета, чем фасад.");
   var bounds=Foundation(b);float bottom=bounds.min.y,top=WindowBottom(b)-Mathf.Clamp(b.profile.baseTrimWindowClearance,.02f,.3f),p=Mathf.Clamp(b.profile.baseTrimProjection,.005f,.05f);
   if(top<=0||bottom>=0)throw new InvalidOperationException("Неверные уровни окон/основания для цоколя.");
   float xmin=bounds.min.x-p,xmax=bounds.max.x+p,zmin=bounds.min.z-p,zmax=bounds.max.z+p;
   var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
   void Face(Vector3 a,Vector3 c,Vector3 d,Vector3 e,int axis){int n=vertices.Count;foreach(var v in new[]{a,c,d,e}){vertices.Add(v);uv.Add(axis==0?new Vector2(v.z/3,v.y/1.5f):axis==1?new Vector2(v.x/3,v.z/3):new Vector2(v.x/3,v.y/1.5f));}triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
   void Box(float x0,float x1,float y0,float y1,float z0,float z1){if(x1-x0<.0001f||y1-y0<.0001f||z1-z0<.0001f)return;
    Face(new Vector3(x0,y0,z0),new Vector3(x0,y1,z0),new Vector3(x1,y1,z0),new Vector3(x1,y0,z0),2);
    Face(new Vector3(x0,y0,z1),new Vector3(x1,y0,z1),new Vector3(x1,y1,z1),new Vector3(x0,y1,z1),2);
    Face(new Vector3(x0,y0,z1),new Vector3(x0,y1,z1),new Vector3(x0,y1,z0),new Vector3(x0,y0,z0),0);
    Face(new Vector3(x1,y0,z0),new Vector3(x1,y1,z0),new Vector3(x1,y1,z1),new Vector3(x1,y0,z1),0);
    Face(new Vector3(x0,y1,z0),new Vector3(x0,y1,z1),new Vector3(x1,y1,z1),new Vector3(x1,y1,z0),1);
    Face(new Vector3(x0,y0,z1),new Vector3(x0,y0,z0),new Vector3(x1,y0,z0),new Vector3(x1,y0,z1),1);
   }
   // Front is continuous below ground-floor level, with explicit door gaps above it.
   float innerFront=bounds.min.z,innerRear=bounds.max.z;
   Box(xmin,xmax,bottom,0,zmin,innerFront);float cursor=xmin;
   foreach(var gap in DoorGaps(b)){float a=Mathf.Clamp(gap.x,xmin,xmax),c=Mathf.Clamp(gap.y,xmin,xmax);Box(cursor,a,0,top,zmin,innerFront);cursor=Mathf.Max(cursor,c);}
   Box(cursor,xmax,0,top,zmin,innerFront);
   Box(xmin,xmax,bottom,top,innerRear,zmax);
   Box(xmin,bounds.min.x,bottom,top,innerFront,innerRear);
   Box(bounds.max.x,xmax,bottom,top,innerFront,innerRear);
   var mesh=new Mesh{name="BaseTrim"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
   string signature=string.Join(";",vertices.Select(v=>v.x.ToString("R",CultureInfo.InvariantCulture)+","+v.y.ToString("R",CultureInfo.InvariantCulture)+","+v.z.ToString("R",CultureInfo.InvariantCulture)));
   string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(signature))).Replace("-","");
   const string folder="Assets/ModularBuildings/Generated/BaseTrimMeshes";Directory.CreateDirectory(folder);AssetDatabase.Refresh();string path=folder+"/Base_"+hash+".asset";var cached=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(cached){UnityEngine.Object.DestroyImmediate(mesh);mesh=cached;}else AssetDatabase.CreateAsset(mesh,path);
   var go=new GameObject("BaseTrim",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(b.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
   b.baseTrimMaterialOverride=material;b.exteriorEnvelopeVersion=1;return go;
  }
  public static void ApplyMaterial(GeneratedBuilding b,Material requested){
   if(!b||!b.profile)throw new InvalidOperationException("Выберите дом.");var group=b.transform.Find("BaseTrim");if(!group)throw new InvalidOperationException("У этого дома пока нет нового цоколя.");
   var material=MaterialFor(b,requested);if(!material||material==BuildingExteriorMaterials.EffectiveMaterial(b))throw new InvalidOperationException("Нужен отдельный материал цоколя.");
   var renderers=group.GetComponentsInChildren<MeshRenderer>(true);Undo.RecordObjects(renderers.Cast<UnityEngine.Object>().Concat(new UnityEngine.Object[]{b}).ToArray(),"Материал цоколя");
   foreach(var r in renderers){r.sharedMaterial=material;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}b.baseTrimMaterialOverride=material;PrefabUtility.RecordPrefabInstancePropertyModifications(b);EditorSceneManager.MarkSceneDirty(b.gameObject.scene);
  }
  public static void Validate(GeneratedBuilding b,List<string> errors){
   var group=b.transform.Find("BaseTrim");if(!group){errors.Add("Нет контрастного нижнего цоколя.");return;}
   var bounds=LocalBounds(b.transform,group.GetComponentsInChildren<MeshFilter>(true));var foundation=Foundation(b);float expected=WindowBottom(b)-Mathf.Clamp(b.profile.baseTrimWindowClearance,.02f,.3f);
   if(Mathf.Abs(bounds.min.y-foundation.min.y)>.001f)errors.Add("Низ цоколя не совпадает с низом основных стен.");
   if(Mathf.Abs(bounds.max.y-expected)>.001f)errors.Add("Цоколь не выдерживает зазор до окон.");
   if(group.GetComponentsInChildren<MeshRenderer>().Any(r=>!r.sharedMaterial||r.sharedMaterial==BuildingExteriorMaterials.EffectiveMaterial(b)))errors.Add("Цоколь должен иметь отдельный материал.");
   foreach(var gap in DoorGaps(b)){var from=b.transform.TransformPoint(new Vector3((gap.x+gap.y)/2,.4f,foundation.min.z-.5f));if(FacadeDetailChecks.RenderRay(group,from,b.transform.forward,1))errors.Add("Цоколь перекрывает входной проём.");}
  }
 }
}
