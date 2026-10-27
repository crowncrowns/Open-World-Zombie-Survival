#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ZombieSurvival.ModularBuildings.Editor
{
    /// <summary>Suppress only the unused L entrance adjacent to an occupied L2; apartment source geometry is untouched.</summary>
    public static class BuildingApartmentDoorCleanup
    {
        public const string SealName="SuppressedUnusedApartmentDoorSeal";
        sealed class Candidate { public ApartmentSlot slot; public Transform assembly, wall; public Bounds opening; }
        static Bounds LocalBounds(Transform root,Renderer renderer)
        {
            var matrix=root.worldToLocalMatrix*renderer.transform.localToWorldMatrix;var b=renderer.localBounds;Bounds result=default;bool first=true;
            for(int i=0;i<8;i++){var p=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}return result;
        }
        static List<Candidate> Candidates(GeneratedBuilding b,List<string> errors)
        {
            var result=new List<Candidate>();if(!b||!b.profile){errors.Add("Нужен дом с профилем.");return result;}
            foreach(var l in b.GetComponentsInChildren<ApartmentSlot>(true).Where(s=>s.slotId!=null&&s.slotId.EndsWith("/L",StringComparison.Ordinal)))
            {
                var l2=l.transform.parent.GetComponentsInChildren<ApartmentSlot>(true).FirstOrDefault(s=>s.transform.parent==l.transform.parent&&s.slotId==l.slotId+"2");
                if(!l2||l2.GetComponentsInChildren<GeneratedApartment>(true).Length==0)continue;
                if(!l.manualContent||l.manualContent.childCount!=0||l.GetComponentsInChildren<GeneratedApartment>(true).Length!=0){errors.Add(l.slotId+": L содержит пользовательский контент, очистка запрещена.");continue;}
                var assembly=l.transform.Find("ApartmentDoor");var wall=l.transform.Find("ApartmentDoorWall");
                if(!assembly||!wall||!l.doorHinge||!l.doorLeaf||!l.doorHinge.IsChildOf(assembly)||!l.doorLeaf.IsChildOf(assembly)){errors.Add(l.slotId+": неожиданная структура двери.");continue;}
                if((wall.localPosition-Vector3.zero).sqrMagnitude>.00000001f && (wall.localPosition-new Vector3(0,0,-.01f)).sqrMagnitude>.00000001f){errors.Add(l.slotId+": пользовательский сдвиг стены, очистка запрещена.");continue;}
                if(Quaternion.Angle(wall.localRotation,Quaternion.identity)>.001f || wall.localScale!=Vector3.one){errors.Add(l.slotId+": пользовательский поворот/масштаб стены.");continue;}
                string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(assembly.gameObject);
                if(!b.profile.apartmentDoors.Any(p=>p&&AssetDatabase.GetAssetPath(p)==path)){errors.Add(l.slotId+": неизвестный источник двери.");continue;}
                foreach(var c in assembly.GetComponentsInChildren<Component>(true))
                {
                    if(!c){errors.Add(l.slotId+": missing script.");continue;}
                    if(!PrefabUtility.GetCorrespondingObjectFromSource(c)&&!(c is ZombieSurvival.Interaction.DoorInteractable))errors.Add(l.slotId+": добавленный компонент/объект двери "+c.GetType().Name);
                }
                var rs=wall.GetComponentsInChildren<Renderer>(true);var left=rs.SingleOrDefault(r=>r.name=="Wall_Door_Left");var right=rs.SingleOrDefault(r=>r.name=="Wall_Door_Right");var top=rs.SingleOrDefault(r=>r.name=="Wall_Door_Top");
                if(!left||!right||!top){errors.Add(l.slotId+": не распознан проём стены.");continue;}
                var lb=LocalBounds(l.transform,left);var rb=LocalBounds(l.transform,right);var tb=LocalBounds(l.transform,top);
                float minX=lb.max.x,maxX=rb.min.x,bottom=Mathf.Min(lb.min.y,rb.min.y),height=tb.min.y-bottom,depth=Mathf.Min(lb.size.z,rb.size.z);
                if(maxX-minX<.7f||maxX-minX>1.5f||height<1.8f||height>2.5f||depth<.02f||depth>.5f){errors.Add(l.slotId+": неожиданные размеры проёма.");continue;}
                var opening=new Bounds(new Vector3((minX+maxX)/2,bottom+height/2,(lb.center.z+rb.center.z)/2),new Vector3(maxX-minX+.004f,height+.004f,depth));
                opening.center+=new Vector3(0,0,-.01f-wall.localPosition.z);
                var seal=l.transform.Find(SealName);
                if(seal)
                {
                    var components=seal.GetComponents<Component>();
                    if(seal.childCount!=0||components.Length!=4||!seal.GetComponent<MeshFilter>()||!seal.GetComponent<MeshRenderer>()||!seal.GetComponent<BoxCollider>())errors.Add(l.slotId+": неизвестный объект на месте seal.");
                }
                result.Add(new Candidate{slot=l,assembly=assembly,wall=wall,opening=opening});
            }
            if(result.Count>0&&!b.profile.stairWallMaterial)errors.Add("Нет материала стен подъезда.");return result;
        }
        public static List<string> Preflight(GeneratedBuilding b){var errors=new List<string>();Candidates(b,errors);return errors;}
        public static int Apply(GeneratedBuilding b,bool recordUndo=true)
        {
            if(Application.isPlaying||EditorUtility.IsPersistent(b))throw new InvalidOperationException("Только экземпляр дома в Edit.");
            var errors=new List<string>();var candidates=Candidates(b,errors);if(errors.Count>0)throw new InvalidOperationException(string.Join(";",errors));int count=0;
            foreach(var c in candidates)
            {
                if(c.wall.localPosition!=new Vector3(0,0,-.01f)){if(recordUndo)Undo.RecordObject(c.wall,"Устранить наложение стены L");c.wall.localPosition=new Vector3(0,0,-.01f);EditorUtility.SetDirty(c.wall);PrefabUtility.RecordPrefabInstancePropertyModifications(c.wall);}
                var seal=c.slot.transform.Find(SealName);
                if(!seal)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=SealName;go.transform.SetParent(c.slot.transform,false);go.transform.localPosition=c.opening.center;go.transform.localScale=c.opening.size;go.layer=c.slot.gameObject.layer;
                    go.GetComponent<MeshRenderer>().sharedMaterial=b.profile.stairWallMaterial;
                    if(recordUndo)Undo.RegisterCreatedObjectUndo(go,"Закрыть лишний вход L");seal=go.transform;
                }
                if(seal.localPosition!=c.opening.center){if(recordUndo)Undo.RecordObject(seal,"Согласовать заглушку L");seal.localPosition=c.opening.center;EditorUtility.SetDirty(seal);PrefabUtility.RecordPrefabInstancePropertyModifications(seal);}
                if(c.assembly.gameObject.activeSelf){if(recordUndo)Undo.RecordObject(c.assembly.gameObject,"Убрать лишнюю дверь L");c.assembly.gameObject.SetActive(false);count++;}
                EditorUtility.SetDirty(c.assembly.gameObject);PrefabUtility.RecordPrefabInstancePropertyModifications(c.assembly.gameObject);
            }
            if(recordUndo&&candidates.Count>0&&b.gameObject.scene.IsValid())UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(b.gameObject.scene);
            return count;
        }
        public static List<string> Validate(GeneratedBuilding b)
        {
            var errors=new List<string>();var candidates=Candidates(b,errors);
            foreach(var c in candidates)
            {
                var seal=c.slot.transform.Find(SealName);
                if((c.wall.localPosition-new Vector3(0,0,-.01f)).sqrMagnitude>.00000001f)errors.Add(c.slot.slotId+": стена L не смещена в вестибюль на1см.");
                if(c.assembly.gameObject.activeSelf)errors.Add(c.slot.slotId+": лишняя дверь L активна.");
                if(!seal){errors.Add(c.slot.slotId+": проём L не закрыт.");continue;}
                var box=seal.GetComponent<BoxCollider>();var renderer=seal.GetComponent<MeshRenderer>();
                if(!seal.gameObject.activeSelf||!box||!box.enabled||box.isTrigger||box.center!=Vector3.zero||box.size!=Vector3.one||!renderer||!renderer.enabled||renderer.sharedMaterial!=b.profile.stairWallMaterial||(seal.localPosition-c.opening.center).sqrMagnitude>.000001f||(seal.localScale-c.opening.size).sqrMagnitude>.000001f||Quaternion.Angle(seal.localRotation,Quaternion.identity)>.01f)errors.Add(c.slot.slotId+": seal geometry/material/collider не соответствует проёму.");
            }
            return errors;
        }
    }
}
#endif

