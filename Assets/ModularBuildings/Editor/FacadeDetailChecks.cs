using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class FacadeDetailChecks
    {
        public static bool IsForbiddenWindow(string path)
        {
            var name=System.IO.Path.GetFileNameWithoutExtension(path);
            return name=="Wall_Window_Static"||name=="Wall_Window_Static_Variant";
        }
        static string Relative(Transform t,Transform root){if(t==root)return "";string path=t.name;while(t.parent&&t.parent!=root){t=t.parent;path=t.name+"/"+path;}return path;}
        static string F(float v)=>Mathf.Abs(v)<.0000001f?"0":v.ToString("F6",CultureInfo.InvariantCulture);
        static string V(Vector3 v)=>F(v.x)+","+F(v.y)+","+F(v.z);
        static string Q(Quaternion q){if(q.w<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);return F(q.x)+","+F(q.y)+","+F(q.z)+","+F(q.w);}
        public static string DoorSignature(GameObject root,bool meshReferences=true,Material exteriorDoorMaterial=null)
        {
            var text=new StringBuilder();
            foreach(var t in root.GetComponentsInChildren<Transform>(true).OrderBy(t=>Relative(t,root.transform),StringComparer.Ordinal)){
                text.AppendLine(Relative(t,root.transform)+"|"+V(t.localPosition)+"|"+Q(t.name=="Door_Hinge"?Quaternion.identity:t.localRotation)+"|"+V(t.localScale)+"|"+t.gameObject.activeSelf);
                var r=t.GetComponent<MeshRenderer>();if(r){text.AppendLine("materials="+string.Join(";",r.sharedMaterials.Select(m=>{var effective=exteriorDoorMaterial&&BuildingExteriorMaterials.IsDoorSurface(r)?exteriorDoorMaterial:m;return effective?AssetDatabase.GetAssetPath(effective)+"/"+effective.name:"MISSING";})));
                    if(meshReferences){var filter=t.GetComponent<MeshFilter>();var mesh=filter?filter.sharedMesh:null;if(mesh){AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long id);text.AppendLine("mesh="+guid+"/"+id+"/"+mesh.name);}else text.AppendLine("MISSING MESH");}
                }
                foreach(var c in t.GetComponents<Collider>()){
                    string shape="";if(c is BoxCollider box)shape=V(box.center)+"/"+V(box.size);else if(c is CapsuleCollider capsule)shape=V(capsule.center)+"/"+F(capsule.radius)+"/"+F(capsule.height)+"/"+capsule.direction;else if(c is SphereCollider sphere)shape=V(sphere.center)+"/"+F(sphere.radius);else if(c is MeshCollider mc)shape=AssetDatabase.GetAssetPath(mc.sharedMesh)+"/"+mc.convex;
                    text.AppendLine(c.GetType().Name+"/"+shape+"/"+c.enabled+"/"+c.isTrigger);
                }
            }
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","");
        }
        public static void Check(GeneratedBuilding b,bool pristine,List<string> errors)
        {
            var p=b.profile;var source=DemoAssetSetup.ReferenceEntrance();var facadeMaterial=BuildingExteriorMaterials.EffectiveMaterial(b);
            if(DoorSignature(p.entranceDoor)!=DoorSignature(source))errors.Add("Входной шаблон не совпадает с Building_Shk_68_FINISH.");
            foreach(var t in b.GetComponentsInChildren<Transform>(true)){
                var path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                if(IsForbiddenWindow(path))errors.Add("Запрещённый оконный prefab: "+path);
                if(t.name=="EntranceDoor"&&DoorSignature(t.gameObject,false)!=DoorSignature(source,false,b.exteriorIncludesEntranceDoors?facadeMaterial:null))errors.Add("Поза/материалы/фурнитура входной двери изменены: "+t.parent.parent.name);
            }
            var corners=b.transform.Find("FacadeCornerClosures");
            if(!corners||corners.childCount!=4){errors.Add("Не закрыты четыре наружных угла.");return;}
            foreach(Transform corner in corners){
                var renderer=corner.GetComponent<MeshRenderer>();
                if(!renderer||renderer.sharedMaterial!=facadeMaterial)errors.Add("Неправильный материал углового элемента.");
                for(float y=-p.foundationDepth+.1f;y<b.floors*3-.1f;y+=.5f){
                    float sx=corner.localPosition.x<0?-1:1,sz=corner.localPosition.z<0?-1:1;
                    var point=new Vector3(corner.localPosition.x,y,corner.localPosition.z);var from=b.transform.TransformPoint(point+new Vector3(sx*.8f,0,sz*.8f));var direction=b.transform.TransformDirection(new Vector3(-sx,0,-sz).normalized);
                    if(!RenderRay(corner,from,direction,2))errors.Add("Видимый разрыв угла на высоте "+y+": "+corner.name);
                }
            }
            foreach(var r in BuildingExteriorMaterials.Walls(b)){
                if(r.sharedMaterials.Any(m=>m!=facadeMaterial))errors.Add("Неверная отделка фасадного модуля: "+r.name);
                if(!b.exteriorMaterialOverride&&p.requireWorldSpaceFacade&&r.sharedMaterials.Any(m=>!m||m.shader!=p.approvedWorldFacadeShader))errors.Add("На фасаде отсутствует утверждённый World Space Mapping shader.");
            }
            if(b.exteriorIncludesEntranceDoors)foreach(var r in BuildingExteriorMaterials.Doors(b))
                if(r.sharedMaterials.Any(m=>m!=facadeMaterial))errors.Add("Неверная отделка входной двери: "+r.name);
            if(b.endWindowRuleVersion>=1)CheckEndWindows(b,errors);
            if(b.exteriorEnvelopeVersion>=1)BuildingBaseTrim.Validate(b,errors);
            if(b.entranceAccessRuleVersion>=1)BuildingEntranceStairs.Validate(b,errors);
            foreach(var level in b.GetComponentsInChildren<Transform>().Where(t=>t.parent&&t.parent.name.StartsWith("Entrance_")&&t.name.StartsWith("Floor_"))){
                if(p.section.sectionWidth==15){
                    if(level.Cast<Transform>().Count(t=>t.name.StartsWith("Front_"))!=5||level.Cast<Transform>().Count(t=>t.name.StartsWith("Rear_"))!=5)errors.Add("Нужны два фасадных модуля по обе стороны подъезда.");
                    foreach(float x in new[]{-6f,-3f,3f,6f})foreach(float z in new[]{0f,3f,6f,9f}){
                        var foot=level.TransformPoint(new Vector3(x,.065f,z));
                        BuildingQuality.Capsule(b,foot,errors,"Расширенное место для квартиры");
                        if(!Physics.RaycastAll(foot+Vector3.up*.1f,Vector3.down,.3f).Any(h=>h.collider.transform.IsChildOf(b.transform)))errors.Add("Нет перекрытия в расширенной зоне.");
                    }
                }
                var lining=level.Find("EntranceInteriorLining");if(!lining){errors.Add("Нет облицовки входной стены: "+level.name);continue;}
                if(lining.GetComponentsInChildren<Transform>().Any(t=>t.name=="Door_Hinge"||t.name=="Door_Leaf"))errors.Add("Облицовка содержит лишнюю дверь.");
                foreach(var r in lining.GetComponentsInChildren<MeshRenderer>())if(r.sharedMaterials.Any(m=>m!=p.stairWallMaterial))errors.Add("Облицовка не соответствует отделке подъезда.");
                foreach(float x in new[]{-.9f,.9f})foreach(float y in new[]{.5f,1.5f,2.6f}){
                    var origin=level.TransformPoint(new Vector3(x,y,0));var hits=Physics.RaycastAll(origin,level.TransformDirection(Vector3.back),2).Where(h=>h.collider.transform.IsChildOf(b.transform)).OrderBy(h=>h.distance).ToArray();
                    if(hits.Length==0){errors.Add("Разрыв внутренней облицовки.");continue;}
                    var r=hits[0].collider.GetComponent<Renderer>();if(!r||r.sharedMaterials.Any(m=>m!=p.stairWallMaterial))errors.Add("Из подъезда видна наружная отделка: "+level.parent.name+"/"+level.name+"/"+x+"/"+y);
                }
            }
        }
        public static void CheckEndWindows(GeneratedBuilding b,List<string> errors)
        {
            for(int f=1;f<=b.floors;f++)foreach(bool left in new[]{true,false}){
                var side=b.transform.Find("Entrance_"+(left?1:b.entrances)+"/Floor_"+f+"/"+(left?"LeftEnd":"RightEnd"));
                var container=side?side.Find("EndWindows"):null;if(!container)container=side;
                var windows=container?container.Cast<Transform>().Where(t=>t.name.StartsWith("Window_",StringComparison.Ordinal)).ToArray():new Transform[0];
                if(windows.Length<2)errors.Add("Нужны минимум два окна на торце каждого этажа: "+(left?"левый":"правый")+" / этаж "+f);
                if(container&&windows.Length>=2)foreach(float x in new[]{-3f,3f})foreach(float dx in new[]{-.005f,0,.005f})foreach(float y in new[]{.15f,.6f,1.5f,2.7f}) {
                    var from=container.TransformPoint(new Vector3(x+dx,y,.7f));
                    if(!RenderRay(container,from,-container.forward,1.4f))errors.Add("Щель на стыке торцевых модулей: "+side.parent.name+"/"+side.name+" x="+x+" y="+y);
                }
                foreach(var window in windows){
                    var wall=window.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name=="Wall_Window");
                    if(!window.gameObject.activeInHierarchy||!wall||!wall.enabled||!window.GetComponentsInChildren<MeshRenderer>().Any(r=>r.name.IndexOf("glass",StringComparison.OrdinalIgnoreCase)>=0&&r.enabled))errors.Add("Торцевое окно отсутствует или выключено.");
                    if(Vector3.Distance(window.localScale,Vector3.one)>.001f)errors.Add("Торцевое окно растянуто.");
                    if(Vector3.Dot(window.forward,b.transform.right*(left?-1:1))<.999f)errors.Add("Торцевое окно повёрнуто внутрь.");
                    if(!Physics.RaycastAll(window.TransformPoint(new Vector3(0,1.5f,-.6f)),window.forward,1.2f).Any(h=>h.collider.transform.IsChildOf(window)))errors.Add("Нет коллизии торцевого окна.");
                }
            }
        }
        public static bool RenderRay(Transform target,Vector3 origin,Vector3 direction,float maxDistance)
        {
            foreach(var filter in target.GetComponentsInChildren<MeshFilter>()){
                var r=filter.GetComponent<MeshRenderer>();if(!r||!r.enabled||!filter.sharedMesh)continue;
                var vertices=filter.sharedMesh.vertices;var indices=filter.sharedMesh.triangles;
                for(int i=0;i<indices.Length;i+=3){
                    var a=filter.transform.TransformPoint(vertices[indices[i]]);var u=filter.transform.TransformPoint(vertices[indices[i+1]])-a;var v=filter.transform.TransformPoint(vertices[indices[i+2]])-a;
                    var p=Vector3.Cross(direction,v);float determinant=Vector3.Dot(u,p);if(Mathf.Abs(determinant)<1e-8f)continue;
                    var s=origin-a;float x=Vector3.Dot(s,p)/determinant;if(x<-.00001f||x>1.00001f)continue;var q=Vector3.Cross(s,u);float y=Vector3.Dot(direction,q)/determinant;if(y<-.00001f||x+y>1.00001f)continue;
                    float d=Vector3.Dot(v,q)/determinant;if(d>=0&&d<=maxDistance)return true;
                }
            }return false;
        }
    }
}
