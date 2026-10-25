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
using Object=UnityEngine.Object;

namespace ZombieSurvival.ModularBuildings.Editor
{
 public static class BuildingEntranceStairs
 {
  const float Contact=.02f;
  sealed class Draft
  {
   public readonly List<Vector3> v=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();public readonly List<int> tri=new List<int>();
   public void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int n=v.Count;var normal=Vector3.Cross(b-a,c-a).normalized;foreach(var p in new[]{a,b,c,d}){v.Add(p);uv.Add(Mathf.Abs(normal.y)>.5f?new Vector2(p.x,p.z):Mathf.Abs(normal.x)>.5f?new Vector2(p.z,p.y):new Vector2(p.x,p.y));}tri.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
   public void Box(float x0,float x1,float y0,float y1,float z0,float z1){if(y1-y0<.002f)y0=y1-.1f;
    Face(new Vector3(x0,y0,z0),new Vector3(x0,y1,z0),new Vector3(x1,y1,z0),new Vector3(x1,y0,z0));Face(new Vector3(x0,y0,z1),new Vector3(x1,y0,z1),new Vector3(x1,y1,z1),new Vector3(x0,y1,z1));
    Face(new Vector3(x0,y0,z1),new Vector3(x0,y1,z1),new Vector3(x0,y1,z0),new Vector3(x0,y0,z0));Face(new Vector3(x1,y0,z0),new Vector3(x1,y1,z0),new Vector3(x1,y1,z1),new Vector3(x1,y0,z1));
    Face(new Vector3(x0,y1,z0),new Vector3(x0,y1,z1),new Vector3(x1,y1,z1),new Vector3(x1,y1,z0));Face(new Vector3(x0,y0,z1),new Vector3(x0,y0,z0),new Vector3(x1,y0,z0),new Vector3(x1,y0,z1));
   }
   public void Surface(float x0,float x1,float z0,float z1,int nx,int nz,Func<float,float,float> height){
    for(int j=0;j<nz;j++)for(int i=0;i<nx;i++){float xa=Mathf.Lerp(x0,x1,i/(float)nx),xb=Mathf.Lerp(x0,x1,(i+1f)/nx),za=Mathf.Lerp(z0,z1,j/(float)nz),zb=Mathf.Lerp(z0,z1,(j+1f)/nz);Face(new Vector3(xa,height(xa,za),za),new Vector3(xa,height(xa,zb),zb),new Vector3(xb,height(xb,zb),zb),new Vector3(xb,height(xb,za),za));}
   }
   public Mesh Asset(string label){var m=new Mesh{name=label};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tri,0);if(v.Count>0){m.RecalculateNormals();m.RecalculateTangents();}m.RecalculateBounds();
    var text=string.Join(";",v.Select(p=>p.x.ToString("R",CultureInfo.InvariantCulture)+","+p.y.ToString("R",CultureInfo.InvariantCulture)+","+p.z.ToString("R",CultureInfo.InvariantCulture)))+"|"+string.Join(",",tri);
    string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");const string folder="Assets/ModularBuildings/Generated/EntranceAccessMeshes";Directory.CreateDirectory(folder);AssetDatabase.Refresh();string path=folder+"/Access_"+hash+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){Object.DestroyImmediate(m);return old;}AssetDatabase.CreateAsset(m,path);return m;
   }
  }
  sealed class Plan{public Draft stairs=new Draft(),rails=new Draft();public GeneratedEntranceAccess.Route data;public Terrain terrain;}
  static bool Covers(Terrain t,Vector3 p){if(!t||!t.terrainData)return false;var q=p-t.transform.position;var s=t.terrainData.size;return q.x>=0&&q.z>=0&&q.x<=s.x&&q.z<=s.z;}
  public static Terrain TerrainAt(GeneratedBuilding b,Vector3 point,Terrain preferred=null){if(Covers(preferred,point))return preferred;return Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).FirstOrDefault(t=>t.gameObject.scene==b.gameObject.scene&&Covers(t,point));}
  static Plan Make(GeneratedBuilding b,int entrance,float x,Terrain preferred,bool strict){
   // One fixed shape for every entrance; Terrain never changes vertices or step count.
   var p=b.profile;float width=Mathf.Max(1.2f,p.entranceStairWidth),riser=Mathf.Clamp(p.entranceMaxRiser,.1f,.19f),tread=Mathf.Max(.28f,p.entranceTreadDepth),landing=Mathf.Max(1.2f,p.entranceLandingDepth),height=Mathf.Clamp(p.entranceStairHeight,1f,40f);
   var floor=b.transform.Find("Entrance_"+entrance+"/Floor_1/MainLandingAndOpenFloor");if(!floor)throw new InvalidOperationException("Не найдена площадка подъезда "+entrance);
   var floorBox=BuildingBaseTrim.LocalBounds(b.transform,floor.GetComponentsInChildren<MeshFilter>(true));float threshold=floorBox.max.y,inside=floorBox.min.z,front=BuildingBaseTrim.Foundation(b).min.z-Mathf.Clamp(p.baseTrimProjection,.005f,.05f);
   var plan=new Plan();var path=new List<Vector3>();plan.terrain=TerrainAt(b,b.transform.TransformPoint(new Vector3(x,threshold,front)),preferred);
   float top=threshold+.005f,z=front-landing,bottom=top-height-.2f;
   int count=Mathf.CeilToInt(height/riser);float maxActual=height/count;
   plan.stairs.Surface(x-.45f,x+.45f,front,inside,2,4,(xx,zz)=>top);
   plan.stairs.Box(x-width/2,x+width/2,bottom,top,z,front);
   path.Add(new Vector3(x,top,inside));path.Add(new Vector3(x,top,front));path.Add(new Vector3(x,top,z));
   for(int k=1;k<=count;k++){
    float next=z-tread,y=top-maxActual*k;
    plan.stairs.Box(x-width/2,x+width/2,bottom,y,next,z);path.Add(new Vector3(x,y,(z+next)/2));z=next;
    if(k%12==0&&k<count){float end=z-1.2f;plan.stairs.Box(x-width/2,x+width/2,bottom,y,end,z);z=end;path.Add(new Vector3(x,y,z));}
   }
   float apron=z-.6f;plan.stairs.Box(x-width/2,x+width/2,bottom,top-height,apron,z);path.Add(new Vector3(x,top-height,apron));
   var railLine=new List<Vector3>{new Vector3(x,top,front-.08f),new Vector3(x,top,front-landing)};
   // Handrails stop at the centre of the third tread; the complete stair flight stays unchanged.
   float railEnd=Mathf.Min(3,count)-.5f;railLine.Add(new Vector3(x,top-maxActual*railEnd,front-landing-tread*railEnd));BuildRails(plan,railLine,x,width);
   plan.data=new GeneratedEntranceAccess.Route{entrance=entrance,steps=count,fitted=false,maxRiser=maxActual,treadDepth=tread,groundContactError=0,path=path.ToArray()};return plan;
  }
  // Rectangular tube swept with shared miter rings: every visible face points outwards.
  static void BuildRails(Plan plan,List<Vector3> baseLine,float centerX,float width){
   var points=new List<Vector3>();foreach(var p in baseLine){if(points.Count>1){var a=(points[points.Count-1]-points[points.Count-2]).normalized;var b=(p-points[points.Count-1]).normalized;if(Vector3.Dot(a,b)>.99999f)points.RemoveAt(points.Count-1);}points.Add(p);}
   float FloorAt(float x,float z){float y=float.NegativeInfinity;for(int n=0;n<plan.stairs.v.Count;n+=4){var a=plan.stairs.v[n];var b=plan.stairs.v[n+1];var c=plan.stairs.v[n+2];var d=plan.stairs.v[n+3];if(Mathf.Abs(a.y-b.y)>.0001f||Mathf.Abs(a.y-c.y)>.0001f||Mathf.Abs(a.y-d.y)>.0001f)continue;if(x>=Mathf.Min(a.x,b.x,c.x,d.x)-.0001f&&x<=Mathf.Max(a.x,b.x,c.x,d.x)+.0001f&&z>=Mathf.Min(a.z,b.z,c.z,d.z)-.0001f&&z<=Mathf.Max(a.z,b.z,c.z,d.z)+.0001f)y=Mathf.Max(y,a.y);}if(float.IsNegativeInfinity(y))throw new InvalidOperationException("Стойка перил не опирается на ступень.");return y;}
   void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward){if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0)plan.rails.Face(d,c,b,a);else plan.rails.Face(a,b,c,d);}
   foreach(int side in new[]{-1,1}){
    float x=centerX+side*(width/2-.04f);var centers=points.Select(p=>new Vector3(x,p.y+.95f,p.z)).ToArray();var rings=new Vector3[centers.Length][];
    for(int i=0;i<centers.Length;i++){var before=(centers[i]-centers[Mathf.Max(0,i-1)]).normalized;var after=(centers[Mathf.Min(centers.Length-1,i+1)]-centers[i]).normalized;if(before==Vector3.zero)before=after;if(after==Vector3.zero)after=before;
     var n0=new Vector3(0,-before.z,before.y);var n1=new Vector3(0,-after.z,after.y);var miter=(n0+n1).normalized;var offset=miter*(.03f/Vector3.Dot(miter,n0));var q=centers[i];rings[i]=new[]{q-Vector3.right*.03f-offset,q+Vector3.right*.03f-offset,q+Vector3.right*.03f+offset,q-Vector3.right*.03f+offset};
    }
    for(int i=0;i<centers.Length-1;i++)for(int j=0;j<4;j++){int k=(j+1)%4;var a=rings[i][j];var b=rings[i+1][j];var c=rings[i+1][k];var d=rings[i][k];Face(a,b,c,d,(a+b+c+d)/4-(centers[i]+centers[i+1])/2);}
    Face(rings[0][0],rings[0][1],rings[0][2],rings[0][3],centers[0]-centers[1]);int last=rings.Length-1;Face(rings[last][0],rings[last][1],rings[last][2],rings[last][3],centers[last]-centers[last-1]);
    float priorPost=float.PositiveInfinity;
    for(int i=0;i<centers.Length-1;i++){
     float dz=centers[i].z-centers[i+1].z;if(dz<.16f)continue;int count=Mathf.Max(1,Mathf.CeilToInt(dz/.9f));
     for(int j=0;j<=count;j++){float z=Mathf.Lerp(centers[i].z-.08f,centers[i+1].z+.08f,j/(float)count);if(priorPost-z<.45f)continue;priorPost=z;
      // The sloping post cap meets the tube underside; it cannot poke through the top.
      var lower0=rings[i][0];var lower1=rings[i+1][0];float Under(float zz)=>Mathf.LerpUnclamped(lower0.y,lower1.y,(zz-lower0.z)/(lower1.z-lower0.z));
      float x0=x-.02f,x1=x+.02f,z0=z-.02f,z1=z+.02f,y0=FloorAt(x,z),a0=Under(z0),a1=Under(z1);
      var bl=new Vector3(x0,y0,z0);var br=new Vector3(x1,y0,z0);var fl=new Vector3(x0,y0,z1);var fr=new Vector3(x1,y0,z1);var tl=new Vector3(x0,a0,z0);var tr=new Vector3(x1,a0,z0);var ul=new Vector3(x0,a1,z1);var ur=new Vector3(x1,a1,z1);
      Face(bl,tl,tr,br,Vector3.back);Face(fl,fr,ur,ul,Vector3.forward);Face(fl,ul,tl,bl,Vector3.left);Face(br,tr,ur,fr,Vector3.right);Face(tl,ul,ur,tr,Vector3.up);Face(fl,bl,br,fr,Vector3.down);
     }
    }
   }
  }
  public static void RepairRailings(GeneratedBuilding b,bool recordUndo=true){
   if(!b||!b.profile||!b.transform.Find("EntranceAccess"))throw new InvalidOperationException("Не найдены входные лестницы.");var root=b.transform.Find("EntranceAccess");var owned=root.GetComponent<GeneratedEntranceAccess>();if(!owned||!owned.uniformGeometry)throw new InvalidOperationException("Ожидались одинаковые фиксированные лестницы.");
   var gaps=BuildingBaseTrim.DoorGaps(b);if(gaps.Count!=b.entrances)throw new InvalidOperationException("Не найдены входы.");var plans=gaps.Select((g,i)=>Make(b,i+1,(g.x+g.y)/2,owned.terrain,false)).ToArray();
   foreach(var p in plans){var floor=root.Find("Entrance_"+p.data.entrance);if(!floor||floor.GetComponent<MeshFilter>().sharedMesh!=p.stairs.Asset("Entrance stairs"))throw new InvalidOperationException("Ступени изменены вручную; перила не заменены.");}
   foreach(var p in plans){var rail=root.Find("Rails_"+p.data.entrance);if(!rail||!rail.GetComponent<MeshRenderer>().sharedMaterial)throw new InvalidOperationException("Не найдены исходные перила/материал.");Publish(root,rail.name,p.rails.Asset("Entrance rails"),rail.GetComponent<MeshRenderer>().sharedMaterial,recordUndo);}
   if(recordUndo){Undo.FlushUndoRecordObjects();EditorSceneManager.MarkSceneDirty(b.gameObject.scene);}Physics.SyncTransforms();
  }
  static void Headroom(GeneratedBuilding b,Plan plan,GeneratedEntranceAccess owned){foreach(var q in plan.data.path){var foot=b.transform.TransformPoint(q);var terrain=TerrainAt(b,foot,plan.terrain);if(!terrain||foot.y<terrain.SampleHeight(foot)+terrain.transform.position.y-.02f)continue;foreach(var c in Physics.OverlapCapsule(foot+Vector3.up*.65f,foot+Vector3.up*1.55f,.27f,~0,QueryTriggerInteraction.Ignore)){
    if(c is TerrainCollider||c is CharacterController||c.attachedRigidbody||(owned&&c.transform.IsChildOf(owned.transform)))continue;
    if(c.GetComponentsInParent<Transform>(true).Any(t=>t.name=="EntranceDoor"))continue;
    throw new InvalidOperationException("Проход к подъезду "+plan.data.entrance+" перекрыт объектом "+c.name);
   }}}
  static void Publish(Transform root,string name,Mesh mesh,Material material,bool undo){var child=root.Find(name);if(!child){var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));child=go.transform;child.SetParent(root,false);if(undo)Undo.RegisterCreatedObjectUndo(go,"Входная лестница");}
   var filter=child.GetComponent<MeshFilter>();var renderer=child.GetComponent<MeshRenderer>();var collider=child.GetComponent<MeshCollider>();if(undo)Undo.RecordObjects(new Object[]{filter,renderer,collider},"Подогнать лестницу к Terrain");filter.sharedMesh=mesh;collider.sharedMesh=null;if(mesh.vertexCount>0)collider.sharedMesh=mesh;if(!renderer.sharedMaterial)renderer.sharedMaterial=material;renderer.enabled=mesh.vertexCount>0;collider.enabled=mesh.vertexCount>0;PrefabUtility.RecordPrefabInstancePropertyModifications(filter);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
  }
  public static GeneratedEntranceAccess Refit(GeneratedBuilding b,Terrain terrain=null,bool strict=true,bool recordUndo=true){
   if(!b||!b.profile||EditorUtility.IsPersistent(b))throw new InvalidOperationException("Выберите экземпляр дома в сцене.");
   if(Vector3.Distance(b.transform.lossyScale,Vector3.one)>.001f||Vector3.Angle(b.transform.up,Vector3.up)>.01f)throw new InvalidOperationException("Для подгонки лестниц нужен вертикальный дом с масштабом 1.");
   var root=b.transform.Find("EntranceAccess");var access=root?root.GetComponent<GeneratedEntranceAccess>():null;if(root&&!access)throw new InvalidOperationException("Группа EntranceAccess содержит ручные объекты и не принадлежит генератору.");
   if(root&&(root.localPosition.sqrMagnitude>.000001f||Quaternion.Angle(root.localRotation,Quaternion.identity)>.01f||Vector3.Distance(root.localScale,Vector3.one)>.001f))throw new InvalidOperationException("Группа лестниц перемещена вручную; автоматическая подгонка остановлена.");
   var gaps=BuildingBaseTrim.DoorGaps(b);if(gaps.Count!=b.entrances)throw new InvalidOperationException("Не найдены все входные проёмы.");
   var plans=gaps.Select((g,i)=>Make(b,i+1,(g.x+g.y)/2,terrain?terrain:access?access.terrain:null,strict)).ToArray();Physics.SyncTransforms();foreach(var plan in plans)Headroom(b,plan,access);
   bool created=!root;if(created){var go=new GameObject("EntranceAccess");root=go.transform;root.SetParent(b.transform,false);access=go.AddComponent<GeneratedEntranceAccess>();}
   try{
    if(recordUndo)Undo.RecordObjects(new Object[]{access,b},"Входные лестницы");
    var material=b.profile.entranceStairMaterial?b.profile.entranceStairMaterial:b.profile.stair.GetComponentInChildren<MeshRenderer>().sharedMaterial;
    var rail=b.profile.entranceRailMaterial?b.profile.entranceRailMaterial:b.profile.entranceDoor.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name.StartsWith("Door_Hinge",StringComparison.Ordinal)).sharedMaterial;
    foreach(var plan in plans){Publish(root,"Entrance_"+plan.data.entrance,plan.stairs.Asset("Entrance stairs"),material,recordUndo&&!created);Publish(root,"Rails_"+plan.data.entrance,plan.rails.Asset("Entrance rails"),rail,recordUndo&&!created);}
    access.fitFailed=false;access.routes=plans.Select(p=>p.data).ToArray();access.terrain=terrain?terrain:plans.Select(p=>p.terrain).FirstOrDefault(t=>t);access.autoFit=false;access.uniformGeometry=true;access.fixedHeight=Mathf.Clamp(b.profile.entranceStairHeight,1f,40f);access.terrainFitted=false;access.fittedBuildingMatrix=b.transform.localToWorldMatrix;access.fittedWidth=b.profile.entranceStairWidth;access.fittedRiser=b.profile.entranceMaxRiser;access.fittedTread=b.profile.entranceTreadDepth;access.fittedLanding=b.profile.entranceLandingDepth;
    access.diagnostic="Одинаковые входные лестницы: запас вниз "+access.fixedHeight.ToString("0.##")+" м, "+plans[0].data.steps+" ступеней у каждого входа. Форма не зависит от Terrain; лишнее скрывается под землёй.";
    b.entranceAccessRuleVersion=2;if(recordUndo&&created)Undo.RegisterCreatedObjectUndo(root.gameObject,"Добавить входные лестницы");if(recordUndo){Undo.FlushUndoRecordObjects();PrefabUtility.RecordPrefabInstancePropertyModifications(access);PrefabUtility.RecordPrefabInstancePropertyModifications(b);if(!EditorSceneManager.IsPreviewScene(b.gameObject.scene))EditorSceneManager.MarkSceneDirty(b.gameObject.scene);}Physics.SyncTransforms();return access;
   }catch{if(created&&root)Object.DestroyImmediate(root.gameObject);throw;}
  }
  public static void Validate(GeneratedBuilding b,List<string> errors){var root=b.transform.Find("EntranceAccess");var a=root?root.GetComponent<GeneratedEntranceAccess>():null;if(!a||a.routes.Length!=b.entrances){errors.Add("Не у всех подъездов есть входная лестница.");return;}
   if(a.fitFailed)errors.Add(a.diagnostic);
   if(a.uniformGeometry&&(a.autoFit||a.fixedHeight<1||a.routes.Select(r=>r.steps).Distinct().Count()!=1))errors.Add("Нарушена одинаковая форма входных лестниц.");
   if(!a.uniformGeometry&&a.terrainFitted&&a.fittedBuildingMatrix!=b.transform.localToWorldMatrix)errors.Add("После перемещения дома подгоните входные лестницы к Terrain.");
   foreach(var r in a.routes){if(r.steps<1||r.maxRiser>Mathf.Clamp(b.profile.entranceMaxRiser,.1f,.19f)+.001f||r.path==null||r.path.Length<4)errors.Add("Неверные ступени у подъезда "+r.entrance);var t=root.Find("Entrance_"+r.entrance);if(!t||!t.GetComponent<MeshCollider>()||!t.GetComponent<MeshCollider>().sharedMesh)errors.Add("Нет коллизии входной лестницы.");if(r.fitted&&r.groundContactError>.05f)errors.Add("Лестница не доходит до Terrain.");}
  }
 }
}
