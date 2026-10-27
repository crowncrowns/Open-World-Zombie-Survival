#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class BuildingInteriorLining
    {
        const string Inside="Assets/Maps/Buildings/Walls/Prefabs/Inside/";
        const string Cache="Assets/ModularBuildings/Generated/InteriorLiningMeshes";
        public sealed class Row
        {
            public GeneratedApartment apartment;public Transform module;public GameObject template;public Material material;
            public float min,max,plane;public readonly List<string> notes=new List<string>();
        }
        public sealed class LiningPlan
        {
            public GeneratedBuilding building;public readonly List<Row> rows=new List<Row>();public readonly List<string> errors=new List<string>();
            public string ToJson()=>Newtonsoft.Json.JsonConvert.SerializeObject(new{building=building?building.buildingId:null,errors,rows=rows.Select(r=>new{apartment=r.apartment.apartmentId,module=Path(r.module),template=AssetDatabase.GetAssetPath(r.template),material=AssetDatabase.GetAssetPath(r.material),min=r.min,max=r.max,plane=r.plane,notes=r.notes})},Newtonsoft.Json.Formatting.Indented);
        }
        sealed class Wall {public Bounds bounds;public Material material;public string name;}
        static string Path(Transform t)=>t.parent?Path(t.parent)+"/"+t.name:t.name;
        static Bounds TransformBounds(Bounds b,Matrix4x4 m)
        {Bounds result=default;bool first=true;for(int i=0;i<8;i++){var p=m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}return result;}
        static Matrix4x4 Relative(Transform from,Transform to)
        {
            var common=to;while(common && from!=common && !from.IsChildOf(common))common=common.parent;
            if(common)return CoplanarAudit.RelativeMatrix(to,common).inverse*CoplanarAudit.RelativeMatrix(from,common);
            return to.worldToLocalMatrix*from.localToWorldMatrix;
        }
        static Bounds BoundsIn(Transform target,Renderer r)=>TransformBounds(r.localBounds,Relative(r.transform,target));
        static bool Owned(Transform t)=>t.GetComponentInParent<GeneratedInteriorLining>();
        static IEnumerable<Renderer> Walls(GeneratedApartment a)=>a.GetComponentsInChildren<Renderer>(true).Where(r=>!Owned(r.transform)&&r.enabled&&(r.name.IndexOf("Wall",StringComparison.OrdinalIgnoreCase)>=0||r.name.IndexOf("Partition",StringComparison.OrdinalIgnoreCase)>=0)&&r.name.IndexOf("Frame",StringComparison.OrdinalIgnoreCase)<0);
        public static LiningPlan Plan(GeneratedBuilding b)
        {
            var plan=new LiningPlan{building=b};if(!b||!b.profile){plan.errors.Add("Нужен дом с профилем.");return plan;}
            foreach(var a in b.GetComponentsInChildren<GeneratedApartment>(true))
            {
                var regions=a.occupiedRegions;int effectiveFloor=a.floor;
                if(regions==null||regions.Length==0)
                {
                    if(a.prefabInstance){plan.errors.Add(a.apartmentId+": нет occupiedRegions у source-квартиры.");continue;}
                    var actual=Walls(a).Select(r=>BoundsIn(b.transform,r)).Where(x=>x.size.y>=2.45f).ToArray();
                    if(actual.Length==0){plan.errors.Add(a.apartmentId+": legacy без геометрии стен.");continue;}
                    var bounds=actual[0];foreach(var piece in actual.Skip(1))bounds.Encapsulate(piece);
                    if(bounds.size.x<=0||bounds.size.z<=0){plan.errors.Add(a.apartmentId+": legacy bounds некорректны.");continue;}
                    var slot=a.GetComponentInParent<ApartmentSlot>();
                    if(!slot){plan.errors.Add(a.apartmentId+": legacy без ApartmentSlot для определения этажа.");continue;}
                    effectiveFloor=Mathf.RoundToInt(Relative(slot.transform,b.transform).MultiplyPoint3x4(Vector3.zero).y/3);
                    if(a.buildingLocalBounds.size.x>0 && a.buildingLocalBounds.size.z>0)bounds=a.buildingLocalBounds;
                    if(effectiveFloor<0||effectiveFloor>=b.floors){plan.errors.Add(a.apartmentId+": legacy этаж вне дома.");continue;}
                    regions=new[]{new Rect(bounds.min.x,bounds.min.z,bounds.size.x,bounds.size.z)};
                }
                foreach(var floor in BuildingExteriorMaterials.Floors(b).Where(f=>Mathf.Abs(Relative(f,b.transform).MultiplyPoint3x4(Vector3.zero).y-effectiveFloor*3)<.01f))
                foreach(Transform module in floor)
                {
                    if(!(module.name.StartsWith("Front_")||module.name.StartsWith("Rear_")||module.name=="LeftEnd"||module.name=="RightEnd"))continue;
                    var candidates=module.name=="LeftEnd"||module.name=="RightEnd"?module.Cast<Transform>().Where(t=>t.name.StartsWith("Window_")):new[]{module}.AsEnumerable();
                    foreach(var window in candidates)PlanWindow(plan,a,window,regions,effectiveFloor);
                }
            }
            return plan;
        }
        static Vector2 TemplateDepthBounds(GameObject template)
        {
            bool any=false;float min=float.PositiveInfinity,max=float.NegativeInfinity;
            foreach(var filter in template.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh)continue;
                var bounds=TransformBounds(filter.sharedMesh.bounds,Relative(filter.transform,template.transform));
                min=Mathf.Min(min,bounds.min.z);max=Mathf.Max(max,bounds.max.z);any=true;
            }
            if(!any||!float.IsFinite(min)||!float.IsFinite(max)||max-min<=0||max-min>.15f)throw new InvalidOperationException("Некорректная толщина шаблона облицовки: "+AssetDatabase.GetAssetPath(template));
            return new Vector2(min,max);
        }
        static void PlanWindow(LiningPlan plan,GeneratedApartment a,Transform module,Rect[] regions,int effectiveFloor)
        {
            var b=plan.building;var inverse=Relative(b.transform,module);
            // All module normals face outside. Negative local Z is the room side.
            var moduleWalls=module.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Wall",StringComparison.Ordinal)).Select(r=>BoundsIn(module,r)).ToArray();
            if(moduleWalls.Length==0)return;
            float externalMinZ=moduleWalls.Min(w=>w.min.z);
            float plane=externalMinZ-.055f; // Only used for room-side search before selecting the template.
            var intervals=new List<Vector2>();
            foreach(var region in regions)
            {
                var r=TransformBounds(new Bounds(new Vector3(region.center.x,effectiveFloor*3+1.5f,region.center.y),new Vector3(region.width,3,region.height)),inverse);
                if(r.min.z<=plane+.12f&&r.max.z>=plane-.12f&&Mathf.Min(r.max.x,1.5f)-Mathf.Max(r.min.x,-1.5f)>.005f)intervals.Add(new Vector2(r.min.x,r.max.x));
            }
            if(intervals.Count==0)return;
            float start=intervals.Min(i=>i.x),end=intervals.Max(i=>i.y);
            var walls=new List<Wall>();
            foreach(var r in Walls(a))
            {
                var bounds=BoundsIn(module,r);
                if(bounds.size.y<2.45f||bounds.size.x>.55f||bounds.size.z<.15f||bounds.max.z<plane-1f||bounds.min.z>plane-.2f)continue;
                var mats=r.sharedMaterials.Distinct().ToArray();if(mats.Length!=1||!mats[0]){plan.errors.Add(a.apartmentId+": неоднозначный материал боковой стены "+r.name);continue;}
                walls.Add(new Wall{bounds=bounds,material=mats[0],name=Path(r.transform)});
            }
            var cuts=new List<float>{start,end};foreach(var w in walls){if(w.bounds.min.x>start&&w.bounds.min.x<end)cuts.Add(w.bounds.min.x);if(w.bounds.max.x>start&&w.bounds.max.x<end)cuts.Add(w.bounds.max.x);}
            cuts=cuts.OrderBy(x=>x).Distinct().ToList();
            string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(module.gameObject)??"";
            string file=path.Contains("TripleWindow")?"Wall_Window_Triple_Interactive.prefab":path.Contains("BalconyExit")?"Wall_BalconyExit_Interactive.prefab":"Wall_Window_Base.prefab";
            var template=AssetDatabase.LoadAssetAtPath<GameObject>(Inside+file);if(!template){plan.errors.Add("Нет template "+file);return;}
            for(int i=0;i+1<cuts.Count;i++)
            {
                float lo=cuts[i],hi=cuts[i+1],mid=(lo+hi)/2;
                if(hi-lo<.3f||Mathf.Min(hi,1.5f)-Mathf.Max(lo,-1.5f)<=.005f||walls.Any(w=>mid>w.bounds.min.x-.001f&&mid<w.bounds.max.x+.001f))continue;
                var left=walls.Where(w=>w.bounds.max.x<=lo+.01f).OrderByDescending(w=>w.bounds.max.x).FirstOrDefault();
                var right=walls.Where(w=>w.bounds.min.x>=hi-.01f).OrderBy(w=>w.bounds.min.x).FirstOrDefault();
                Material material=left?.material??right?.material;
                if(left!=null&&right!=null&&left.material!=right.material){plan.errors.Add(a.apartmentId+": разные материалы room-facing границ "+left.name+" / "+right.name+" module="+Path(module)+" u="+lo+".."+hi);continue;}
                if(!material){plan.errors.Add(a.apartmentId+": не найдена боковая стена для "+Path(module)+" u="+lo+".."+hi);continue;}
                var row=new Row{apartment=a,module=module,template=template,material=material,min=lo,max=hi,plane=externalMinZ-TemplateDepthBounds(template).y};row.notes.Add("left="+(left?.name??"footprint")+";right="+(right?.name??"footprint"));plan.rows.Add(row);
            }
        }
        public static List<string> Preflight(GeneratedBuilding b)=>Plan(b).errors;
        struct Vertex {public Vector3 p,n;public Vector2 uv;public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)};}
        static List<Vertex> Clip(List<Vertex> polygon,int axis,float bound,bool greater)
        {
            var result=new List<Vertex>();if(polygon.Count==0)return result;var last=polygon[polygon.Count-1];float ld=greater?last.p[axis]-bound:bound-last.p[axis];
            foreach(var next in polygon){float nd=greater?next.p[axis]-bound:bound-next.p[axis];if((ld>=0)!=(nd>=0))result.Add(Vertex.Lerp(last,next,ld/(ld-nd)));if(nd>=0)result.Add(next);last=next;ld=nd;}return result;
        }
        static Mesh Skin(Row row)
        {
            var positions=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var indices=new List<int>();
            void Polygon(List<Vertex> poly)
            {
                poly=Clip(poly,0,row.min,true);poly=Clip(poly,0,row.max,false);poly=Clip(poly,1,0,true);poly=Clip(poly,1,3,false);if(poly.Count<3)return;
                int offset=positions.Count;foreach(var v in poly){positions.Add(v.p);normals.Add(v.n);uvs.Add(v.uv);}for(int i=1;i+1<poly.Count;i++){indices.Add(offset);indices.Add(offset+i);indices.Add(offset+i+1);}
            }
            foreach(var f in row.template.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!f.sharedMesh)continue;var matrix=Relative(f.transform,row.template.transform);var normalMatrix=matrix.inverse.transpose;
                using(var data=MeshUtility.AcquireReadOnlyMeshData(f.sharedMesh))using(var vs=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp))using(var ns=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp))using(var uv=new NativeArray<Vector2>(data[0].vertexCount,Allocator.Temp))
                {
                    data[0].GetVertices(vs);bool hasNormals=f.sharedMesh.HasVertexAttribute(VertexAttribute.Normal),hasUV=f.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0);if(hasNormals)data[0].GetNormals(ns);if(hasUV)data[0].GetUVs(0,uv);
                    for(int sub=0;sub<data[0].subMeshCount;sub++){var desc=data[0].GetSubMesh(sub);if(desc.topology!=MeshTopology.Triangles)throw new InvalidOperationException("Template topology unsupported");using(var ix=new NativeArray<int>(desc.indexCount,Allocator.Temp)){data[0].GetIndices(ix,sub);for(int j=0;j+2<ix.Length;j+=3){var poly=new List<Vertex>();for(int k=0;k<3;k++){int q=ix[j+k];var p=matrix.MultiplyPoint3x4(vs[q]);p.z+=row.plane;poly.Add(new Vertex{p=p,n=hasNormals?normalMatrix.MultiplyVector(ns[q]).normalized:Vector3.forward,uv=hasUV?uv[q]:new Vector2(p.x,p.y)});}Polygon(poly);}}}
                }
            }
            void Filler(float low,float high)
            {
                if(high-low<.001f)return;float z=positions.Count>0?positions.Min(v=>v.z):row.plane;Polygon(new List<Vertex>{new Vertex{p=new Vector3(low,0,z),n=Vector3.back,uv=new Vector2(low,0)},new Vertex{p=new Vector3(low,3,z),n=Vector3.back,uv=new Vector2(low,3)},new Vertex{p=new Vector3(high,3,z),n=Vector3.back,uv=new Vector2(high,3)},new Vertex{p=new Vector3(high,0,z),n=Vector3.back,uv=new Vector2(high,0)}});
            }
            // Union coverage of EVERY coplanar facade module before adding any room-end
            // filler. A neighbouring window must never be covered by this window's filler.
            var coverage=new List<Vector2>();var neighbours=new List<Transform>();
            var building=row.apartment.GetComponentInParent<GeneratedBuilding>();
            foreach(var t in building.GetComponentsInChildren<Transform>(true))
            {
                bool facade=t.name.StartsWith("Front_",StringComparison.Ordinal)||t.name.StartsWith("Rear_",StringComparison.Ordinal)||(t.name.StartsWith("Window_",StringComparison.Ordinal)&&t.parent&&(t.parent.name=="LeftEnd"||t.parent.name=="RightEnd"));
                if(!facade||Vector3.Dot(t.forward,row.module.forward)<.999f)continue;
                var p=Relative(t,row.module).MultiplyPoint3x4(Vector3.zero);if(Mathf.Abs(p.z)>.03f||Mathf.Abs(p.y)>.03f||p.x+1.5f<=row.min||p.x-1.5f>=row.max)continue;
                neighbours.Add(t);coverage.Add(new Vector2(Mathf.Max(row.min,p.x-1.5f),Mathf.Min(row.max,p.x+1.5f)));
            }
            if(neighbours.Count>0 && neighbours.OrderBy(Path,StringComparer.Ordinal).First()==row.module)
            {
                float cursor=row.min;foreach(var segment in coverage.OrderBy(x=>x.x)){if(segment.x>cursor)Filler(cursor,segment.x);cursor=Mathf.Max(cursor,segment.y);}if(cursor<row.max)Filler(cursor,row.max);
            }
            if(indices.Count==0)throw new InvalidOperationException("Empty interior skin "+Path(row.module));
            var mesh=new Mesh{name="InteriorRoomSkin",indexFormat=IndexFormat.UInt32};mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh.RecalculateTangents();
            string hash;using(var sha=SHA256.Create()){using(var ms=new MemoryStream())using(var writer=new BinaryWriter(ms)){foreach(var v in positions){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}foreach(var n in normals){writer.Write(n.x);writer.Write(n.y);writer.Write(n.z);}foreach(var uv in uvs){writer.Write(uv.x);writer.Write(uv.y);}foreach(var i in indices)writer.Write(i);writer.Flush();hash=BitConverter.ToString(sha.ComputeHash(ms.ToArray())).Replace("-","");}}
            EnsureFolder(Cache);string path=Cache+"/Skin_"+hash+".asset";var cached=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(cached){UnityEngine.Object.DestroyImmediate(mesh);return cached;}AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        public static int Apply(GeneratedBuilding b,bool recordUndo=true)=>Apply(Plan(b),recordUndo);
        public static int Apply(LiningPlan plan,bool recordUndo=true)
        {
            if(Application.isPlaying||!plan.building||EditorUtility.IsPersistent(plan.building))throw new InvalidOperationException("Нужен экземпляр дома в Edit.");if(plan.errors.Count>0)throw new InvalidOperationException(string.Join("\n",plan.errors));
            // Prepare all derived meshes before modifying any scene content.
            var meshes=plan.rows.Select(Skin).ToArray();var old=plan.building.GetComponentsInChildren<GeneratedInteriorLining>(true);
            foreach(var marker in old)if(!Verified(marker))throw new InvalidOperationException("Неизвестное содержимое owned lining: "+marker.name);
            foreach(var marker in old){if(recordUndo)Undo.DestroyObjectImmediate(marker.gameObject);else UnityEngine.Object.DestroyImmediate(marker.gameObject);}
            for(int i=0;i<plan.rows.Count;i++)
            {
                var row=plan.rows[i];var go=new GameObject("OwnedInteriorLining — "+row.module.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(row.apartment.transform,false);go.transform.SetPositionAndRotation(row.module.position,row.module.rotation);go.transform.localScale=Vector3.one;
                go.GetComponent<MeshFilter>().sharedMesh=meshes[i];go.GetComponent<MeshRenderer>().sharedMaterial=row.material;var marker=go.AddComponent<GeneratedInteriorLining>();marker.apartment=row.apartment;marker.facadeModule=row.module;marker.roomMaterial=row.material;marker.template=row.template;marker.meshes=new[]{meshes[i]};marker.interval=new Vector2(row.min,row.max);
                if(recordUndo)Undo.RegisterCreatedObjectUndo(go,"Внутренняя облицовка комнаты");
            }
            if(recordUndo)UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(plan.building.gameObject.scene);return plan.rows.Count;
        }
        static bool Verified(GeneratedInteriorLining m)
        {
            if(!m||!m.apartment||m.transform.parent!=m.apartment.transform||!m.facadeModule||!m.roomMaterial||!m.template||m.meshes==null||m.meshes.Length!=1||!m.meshes[0]||!AssetDatabase.GetAssetPath(m.meshes[0]).StartsWith(Cache+"/Skin_",StringComparison.Ordinal)||m.transform.childCount!=0)return false;
            string source=AssetDatabase.GetAssetPath(m.template);if(source!=Inside+"Wall_Window_Base.prefab"&&source!=Inside+"Wall_BalconyExit_Interactive.prefab"&&source!=Inside+"Wall_Window_Triple_Interactive.prefab")return false;
            if(m.apartment.prefabInstance && m.transform.IsChildOf(m.apartment.prefabInstance.transform))return false;
            if(m.facadeModule.GetComponentInParent<GeneratedBuilding>()!=m.apartment.GetComponentInParent<GeneratedBuilding>() || !float.IsFinite(m.interval.x)||!float.IsFinite(m.interval.y)||m.interval.y<=m.interval.x)return false;
            var components=m.GetComponents<Component>();return components.Length==4&&m.GetComponent<MeshFilter>()?.sharedMesh==m.meshes[0]&&m.GetComponent<MeshRenderer>()?.sharedMaterial==m.roomMaterial;
        }
        public static bool IsVerifiedOwned(Component component)=>component && component.GetComponent<GeneratedInteriorLining>() is GeneratedInteriorLining marker && Verified(marker) && (component is Renderer||component is MeshFilter);
        public static List<string> Validate(GeneratedBuilding b)
        {
            var errors=new List<string>();var plan=Plan(b);errors.AddRange(plan.errors);var markers=b.GetComponentsInChildren<GeneratedInteriorLining>(true);
            if(markers.Length!=plan.rows.Count)errors.Add("Количество облицовок не соответствует текущему плану комнат.");
            foreach(var m in markers)
            {
                if(!Verified(m)){errors.Add("Невалидная owned облицовка "+m.name);continue;}
                var row=plan.rows.FirstOrDefault(r=>r.apartment==m.apartment&&r.module==m.facadeModule&&Mathf.Abs(r.min-m.interval.x)<.001f&&Mathf.Abs(r.max-m.interval.y)<.001f);
                if(row==null||row.material!=m.roomMaterial){errors.Add("Материал/интервал облицовки устарел: "+m.name);continue;}
                if(Vector3.Distance(m.transform.position,m.facadeModule.position)>.001f||Quaternion.Angle(m.transform.rotation,m.facadeModule.rotation)>.01f){errors.Add("Сдвинута облицовка проёма "+m.name);continue;}
                var vertices=m.meshes[0].vertices;var triangles=m.meshes[0].triangles;
                var outer=m.facadeModule.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Wall",StringComparison.Ordinal)).Select(r=>BoundsIn(m.facadeModule,r)).ToArray();
                if(vertices.Length==0||outer.Length==0)errors.Add("Нет геометрии для контроля прилегания: "+m.name);
                else
                {
                    float actualMaxZ=vertices.Max(v=>v.z),expectedMaxZ=outer.Min(w=>w.min.z);
                    float air=Mathf.Max(0,expectedMaxZ-actualMaxZ),penetration=Mathf.Max(0,actualMaxZ-expectedMaxZ);
                    if(air>.00005f||penetration>.00005f)errors.Add("Облицовка не встык со стеной: "+m.name+" actualMaxZ="+actualMaxZ.ToString("R")+" wallMinZ="+expectedMaxZ.ToString("R")+" air="+air.ToString("R")+" penetration="+penetration.ToString("R"));
                }
                if(vertices.Any(v=>v.x<m.interval.x-.001f||v.x>m.interval.y+.001f||v.y<-.001f||v.y>3.001f))errors.Add("Облицовка выходит за интервал комнаты: "+m.name);
                // Test all coplanar openings against every skin, including another module's
                // window: prevents end-fillers from sealing a neighbouring opening.
                foreach(var other in b.GetComponentsInChildren<Transform>(true))
                {
                    bool facade=other.name.StartsWith("Front_",StringComparison.Ordinal)||other.name.StartsWith("Rear_",StringComparison.Ordinal)||(other.name.StartsWith("Window_",StringComparison.Ordinal)&&other.parent&&(other.parent.name=="LeftEnd"||other.parent.name=="RightEnd"));
                    if(!facade||Vector3.Dot(other.forward,m.facadeModule.forward)<.999f)continue;
                    var p=Relative(other,m.facadeModule).MultiplyPoint3x4(Vector3.zero);if(Mathf.Abs(p.z)>.03f||Mathf.Abs(p.y)>.03f)continue;
                    foreach(float x in new[]{-.25f,0,.25f})foreach(float y in new[]{1.2f,1.8f})
                    {
                        var q=new Vector2(p.x+x,y);bool blocked=false;
                        for(int i=0;i+2<triangles.Length;i+=3){var v0=vertices[triangles[i]];var v1=vertices[triangles[i+1]];var v2=vertices[triangles[i+2]];if(Mathf.Abs(Vector3.Cross(v1-v0,v2-v0).z)<.000001f)continue;if(InTriangle(q,new Vector2(v0.x,v0.y),new Vector2(v1.x,v1.y),new Vector2(v2.x,v2.y))){blocked=true;break;}}
                        if(blocked)errors.Add("Облицовка перекрывает проём "+Path(other)+" sample="+q);
                    }
                }
            }
            // Probe continuous coverage above openings, including both interval edges.
            var valid=markers.Where(Verified).ToArray();var vertexCache=valid.ToDictionary(m=>m,m=>m.meshes[0].vertices);var indexCache=valid.ToDictionary(m=>m,m=>m.meshes[0].triangles);
            foreach(var row in plan.rows)
            {
                var samples=new List<float>{row.min+.01f,row.max-.01f};for(float u=row.min+.075f;u<row.max;u+=.15f)samples.Add(u);
                foreach(float u in samples)
                {
                    if(u<=row.min+.001f||u>=row.max-.001f)continue;bool covered=false;
                    foreach(var marker in valid.Where(m=>m.apartment==row.apartment&&Vector3.Dot(m.facadeModule.forward,row.module.forward)>.999f))
                    {
                        var q=Relative(row.module,marker.facadeModule).MultiplyPoint3x4(new Vector3(u,2.8f,0));if(Mathf.Abs(q.z)>.03f)continue;
                        var vs=vertexCache[marker];var ix=indexCache[marker];
                        for(int i=0;i+2<ix.Length;i+=3){var a=vs[ix[i]];var c=vs[ix[i+1]];var d=vs[ix[i+2]];if(Mathf.Abs(Vector3.Cross(c-a,d-a).z)<.000001f)continue;if(InTriangle(new Vector2(q.x,q.y),new Vector2(a.x,a.y),new Vector2(c.x,c.y),new Vector2(d.x,d.y))){covered=true;break;}}
                        if(covered)break;
                    }
                    if(!covered)errors.Add("Щель облицовки над проёмами: "+Path(row.module)+" u="+u.ToString("R"));
                }
            }
            return errors;
        }
        static bool InTriangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
        {float Cross(Vector2 u,Vector2 v)=>u.x*v.y-u.y*v.x;float s=Cross(b-a,p-a),t=Cross(c-b,p-b),u=Cross(a-c,p-c);return (s>=-.000001f&&t>=-.000001f&&u>=-.000001f)||(s<=.000001f&&t<=.000001f&&u<=.000001f);}
        static void EnsureFolder(string path){string at="Assets";foreach(var part in path.Split('/').Skip(1)){string next=at+"/"+part;if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(at,part);at=next;}}
        [MenuItem("Tools/Zombie Survival/Город — обновить внутреннюю отделку выбранного дома")]
        static void UpdateSelected(){var b=Selection.activeGameObject?Selection.activeGameObject.GetComponentInParent<GeneratedBuilding>():null;if(!b)throw new InvalidOperationException("Выберите дом.");Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply(b,true);Undo.CollapseUndoOperations(group);}catch{Undo.RevertAllDownToGroup(group);throw;}}
    }
}
#endif
