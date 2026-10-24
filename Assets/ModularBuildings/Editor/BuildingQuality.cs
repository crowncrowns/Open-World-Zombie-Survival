using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class BuildingQuality
    {
        public static void Check(GeneratedBuilding b,bool pristine,List<string> errors)
        {
            var p=b.profile;var transforms=b.GetComponentsInChildren<Transform>(true);
            var foundation=b.transform.Find("TerrainFoundation");
            if(!foundation||foundation.childCount!=4)errors.Add("Нет четырёх длинных цокольных модулей.");
            if(foundation)foreach(var renderer in foundation.GetComponentsInChildren<Renderer>()){
                var local=b.transform.InverseTransformPoint(new Vector3(renderer.bounds.center.x,renderer.bounds.min.y,renderer.bounds.center.z));
                if(Mathf.Abs(local.y+p.foundationDepth)>.002f)errors.Add("Цоколь не достигает заданной глубины.");
            }
            var modules=transforms.Where(t=>t.name=="StairwellModule").ToArray();
            if(modules.Length!=b.entrances*(b.floors-1))errors.Add("Нет исходных лестничных модулей на всех переходах.");
            foreach(var module in modules){
                if(UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(module.gameObject)!="Assets/Maps/Buildings/Stairs/Prefabs/Stairwell_3m_UShape.prefab")errors.Add("Лестница не связана с нужным исходным prefab.");
                if(module.Find("Stairs_Landing_02").gameObject.activeSelf)errors.Add("Дублирующая верхняя площадка не отключена.");
            }
            if(transforms.Any(t=>t.name=="PartyWall"))errors.Add("Осталась перегородка между секциями.");
            foreach(var level in transforms.Where(t=>t.name.StartsWith("Floor_",StringComparison.Ordinal)&&t.parent&&t.parent.name.StartsWith("Entrance_",StringComparison.Ordinal))){
                var slots=level.GetComponentsInChildren<ApartmentSlot>();
                if(slots.Length!=3)errors.Add(level.name+": нужно три квартирные двери.");
                if(slots.Where(s=>s.doorLeaf).Select(s=>s.doorLeaf.GetComponent<Renderer>().sharedMaterial).Distinct().Count()!=3)errors.Add(level.name+": нужны три варианта дверей.");
            }
            foreach(var slot in b.GetComponentsInChildren<ApartmentSlot>()){
                if(!slot.doorHinge||!slot.doorLeaf)continue;
                var foot=slot.transform.TransformPoint(new Vector3(0,.065f,-.65f));
                Capsule(b,foot,errors,"Подход к двери "+slot.slotId);
                if(pristine){
                    var hits=Physics.RaycastAll(slot.transform.TransformPoint(new Vector3(0,1.05f,-.7f)),slot.transform.forward,1.4f);
                    if(!hits.Any(h=>h.collider.transform.IsChildOf(slot.doorHinge)))errors.Add("Закрытая дверь не блокирует проём: "+slot.slotId);
                }
                var handles=slot.doorHinge.GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains("Handle")&&r.enabled).ToArray();
                if(handles.Length<2)errors.Add("Нет ручек с обеих сторон: "+slot.slotId);
                foreach(var r in handles){
                    var center=slot.transform.InverseTransformPoint(r.bounds.center);
                    if(Mathf.Abs(center.x)>.46f||center.y<.5f||center.y>1.5f)errors.Add("Ручка вне полотна: "+slot.slotId+"/"+r.name);
                }
            }
            var entrances=transforms.Where(t=>t.name=="EntranceDoor").ToArray();
            if(entrances.Length!=b.entrances)errors.Add("Нет входных дверей во всех подъездах.");
            foreach(var entry in entrances){
                var hinge=entry.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Door_Hinge");
                if(!hinge){errors.Add("Нет входной петли.");continue;}
                if(pristine&&Quaternion.Angle(hinge.localRotation,Quaternion.identity)>.01f)errors.Add("Входная дверь открыта в исходном доме.");
                if(pristine&&!Physics.RaycastAll(entry.TransformPoint(new Vector3(0,1.05f,-.7f)),entry.forward,1.4f).Any(h=>h.collider.transform.IsChildOf(hinge)))errors.Add("Входное полотно не блокирует проём.");
                if(entry.GetComponentsInChildren<Renderer>().Count(r=>r.name.StartsWith("Door_Handle",StringComparison.Ordinal))<2)errors.Add("Нет исходной входной фурнитуры.");
            }
            foreach(var renderer in b.GetComponentsInChildren<Renderer>())
                if(renderer.name.IndexOf("glass",StringComparison.OrdinalIgnoreCase)>=0&&renderer.sharedMaterials.Any(m=>m!=p.glassMaterial))errors.Add("Неверный материал стекла: "+renderer.name);
            var roof=b.transform.Find("Roof_Flat_2Percent");
            if(!roof){errors.Add("Нет почти плоской крыши.");return;}
            var cover=roof.Find("WeatherRoof");
            if(!cover||cover.GetComponent<Renderer>().sharedMaterial!=p.roofMaterial)errors.Add("Неверный материал кровли.");
            if(cover){
                var mesh=cover.GetComponent<MeshFilter>().sharedMesh;
                if(!mesh||mesh.vertexCount==0)errors.Add("Пустая кровля.");
                else{
                    var vertices=mesh.vertices;var triangles=mesh.triangles;
                    for(int i=0;i<triangles.Length;i+=3){
                        var normal=Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]);
                        if(normal.sqrMagnitude<1e-10f)errors.Add("Вырожденный треугольник кровли.");
                        if(normal.normalized.y>.5f&&normal.normalized.y<.9997f)errors.Add("Уклон кровли не соответствует почти плоскому шаблону 2%.");
                    }
                }
            }
            if(roof.Cast<Transform>().Count(t=>t.name.StartsWith("VentilationStack_E"))!=b.entrances||roof.Cast<Transform>().Count(t=>t.name.StartsWith("VentilationDuct_E"))!=b.entrances)errors.Add("Не хватает вентиляционных шахт/воздуховодов.");
            foreach(var curb in roof.GetComponentsInChildren<MeshFilter>().Where(m=>m.name=="FlashedCurb")){
                foreach(var vertex in curb.sharedMesh.vertices){
                    var point=roof.InverseTransformPoint(curb.transform.TransformPoint(vertex));
                    if(point.y<DemoAssetSetup.RoofHeight(point.z)-.0005f)errors.Add("Вентиляционный цоколь пересекает кровлю.");
                }
            }
            foreach(var t in roof.GetComponentsInChildren<Transform>().Where(t=>t.name=="MetalVentCap"||t.name=="ExistingVentAndPipe")){
                if(!t.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh&&m.sharedMesh.vertexCount>0))errors.Add("Пустой меш вентиляционного элемента: "+t.name);
                foreach(var r in t.GetComponentsInChildren<Renderer>()){
                    var point=roof.InverseTransformPoint(new Vector3(r.bounds.center.x,r.bounds.min.y,r.bounds.center.z));
                    if(point.y<DemoAssetSetup.RoofHeight(point.z))errors.Add("Вентиляционный элемент заглублён в крышу: "+t.name);
                }
                if(t.name=="ExistingVentAndPipe"){
                    var facing=t.Find("AirOutletDirection");
                    if(!facing||Vector3.Dot(facing.forward,-b.transform.forward)<.9999f)errors.Add("Решётка вентиляции не направлена к входам.");
                    if(!t.Find("ClosedDuctHousing"))errors.Add("Нет заполнителя вентиляции.");
                }
            }
            var coplanar=CoplanarAudit.Scan(b.gameObject);
            if(coplanar.unreadable.Count>0)errors.Add("Проверка поверхностей неполна: "+string.Join(";",coplanar.unreadable));
            if(coplanar.overlapPairs>0)errors.Add("Coplanar-перекрытия: "+coplanar.overlapPairs+"; "+string.Join(";",coplanar.conflicts.Take(4).Select(x=>x.Key)));
            for(int e=0;e<b.entrances-1;e++)for(int f=0;f<b.floors;f++)foreach(float z in new[]{0f,3f,6f,9f}){
                var foot=b.transform.TransformPoint(new Vector3(e*9+4.5f,f*3+.065f,z));
                Capsule(b,foot,errors,"Граница секций "+e+"/"+f+"/"+z);
                if(!Physics.RaycastAll(foot+Vector3.up*.1f,Vector3.down,.3f).Any(h=>h.collider.transform.IsChildOf(b.transform)))errors.Add("Разрыв пола между секциями.");
            }
        }
        public static void Capsule(GeneratedBuilding b,Vector3 foot,List<string> errors,string label)
        {
            if(Physics.OverlapCapsule(foot+Vector3.up*.3f,foot+Vector3.up*1.5f,.29f,~0,QueryTriggerInteraction.Ignore).Any(c=>c.transform.IsChildOf(b.transform)))errors.Add("Заблокирован проход: "+label);
        }
    }
}
