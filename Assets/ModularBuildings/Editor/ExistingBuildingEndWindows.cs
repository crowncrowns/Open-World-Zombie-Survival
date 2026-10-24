using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace ZombieSurvival.ModularBuildings.Editor
{
    // Additive instance retrofit. Original wall objects and all manual descendants remain intact.
    public static class ExistingBuildingEndWindows
    {
        sealed class Plan
        {
            public GeneratedBuilding building;public Transform side;public bool left;
            public MeshRenderer original;public Collider[] colliders;public Transform reference;
        }
        [Serializable] public sealed class Result
        {
            public int houses,sides,windows,preservedObjects;
            public string[] names;
            public bool existingObjectsUnchanged,undoRedoVerified;
        }
        static List<Plan> PlanChanges(GeneratedBuilding[] buildings)
        {
            var plans=new List<Plan>();
            foreach(var b in buildings){
                if(!b||!b.profile||EditorUtility.IsPersistent(b))throw new InvalidOperationException("Нужен экземпляр дома в сцене.");
                for(int f=1;f<=b.floors;f++)foreach(bool left in new[]{true,false}){
                    var floor=b.transform.Find("Entrance_"+(left?1:b.entrances)+"/Floor_"+f);
                    var side=floor?floor.Find(left?"LeftEnd":"RightEnd"):null;
                    if(!side)throw new InvalidOperationException(b.name+": не найден торец этажа "+f);
                    if(side.Find("EndWindows")||side.Cast<Transform>().Count(t=>t.name.StartsWith("Window_",StringComparison.Ordinal))>=2)continue;
                    if(Vector3.Distance(side.localScale,new Vector3(11.6f/3,1,1))>.002f||Quaternion.Angle(side.localRotation,Quaternion.Euler(0,90,0))>.01f)
                        throw new InvalidOperationException(b.name+": у торца нестандартная форма. Автоматическая замена остановлена, ручные правки сохранены.");
                    var solid=side.Find("Wall_Solid_Mesh");var renderer=solid?solid.GetComponent<MeshRenderer>():null;
                    if(!renderer||renderer.sharedMaterials.Length!=1||!renderer.sharedMaterial)throw new InvalidOperationException(b.name+": не распознана исходная торцевая стена.");
                    var colliders=solid.GetComponents<Collider>();
                    if(colliders.Length!=1||!(colliders[0] is BoxCollider))throw new InvalidOperationException(b.name+": нестандартная коллизия торца.");
                    plans.Add(new Plan{building=b,side=side,left=left,original=renderer,colliders=colliders,reference=floor.Find("Front_0")});
                }
            }
            return plans;
        }
        static string Serialized(Object obj,HashSet<Object> sides,HashSet<Object> toggles,HashSet<Object> buildings)
        {
            string json=EditorJsonUtility.ToJson(obj);
            if(sides.Contains(obj))json=Regex.Replace(json,"\"m_Children\":\\[[^\\]]*\\]","\"m_Children\":[]");
            if(toggles.Contains(obj))json=Regex.Replace(json,"\"m_Enabled\":(true|false|[0-9]+)","\"m_Enabled\":0");
            if(buildings.Contains(obj))json=Regex.Replace(json,"\"endWindowRuleVersion\":[0-9]+","\"endWindowRuleVersion\":0");
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json)));
        }
        static Dictionary<Object,string> Capture(Scene scene,HashSet<Object> sides,HashSet<Object> toggles,HashSet<Object> buildings)
        {
            var map=new Dictionary<Object,string>();
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)){
                map[t.gameObject]=Serialized(t.gameObject,sides,toggles,buildings);
                foreach(var c in t.GetComponents<Component>())if(c)map[c]=Serialized(c,sides,toggles,buildings);
            }
            return map;
        }
        public static Result Apply(GeneratedBuilding[] buildings,bool verifyUndoRedo=false)
        {
            if(buildings==null||buildings.Length==0)throw new InvalidOperationException("Не выбраны дома.");
            var scene=buildings[0].gameObject.scene;
            if(buildings.Any(b=>!b||b.gameObject.scene!=scene))throw new InvalidOperationException("Выберите дома одной сцены.");
            var plans=PlanChanges(buildings);
            if(plans.Count==0)return new Result{houses=buildings.Length,names=buildings.Select(b=>b.name).ToArray(),existingObjectsUnchanged=true};
            var sides=new HashSet<Object>(plans.Select(x=>(Object)x.side));
            var toggles=new HashSet<Object>(plans.Select(x=>(Object)x.original).Concat(plans.SelectMany(x=>x.colliders).Cast<Object>()));
            var owners=new HashSet<Object>(buildings.Cast<Object>());
            var before=Capture(scene,sides,toggles,owners);
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Добавить торцевые окна без пересоздания домов");
            var createdRoots=new List<GameObject>();
            try{
                foreach(var b in buildings)Undo.RecordObject(b,"Правило торцевых окон");
                foreach(var plan in plans){
                    var go=new GameObject("EndWindows");createdRoots.Add(go);go.transform.SetParent(plan.side,false);
                    go.transform.localRotation=Quaternion.Euler(0,plan.left?180:0,0);
                    go.transform.localScale=new Vector3(1/plan.side.localScale.x,1,1);
                    try{BuildingGenerator.AddEndWallModules(plan.building.profile,go.transform);}
                    catch{Object.DestroyImmediate(go);throw;}
                    var reference=plan.reference?plan.reference.GetComponentsInChildren<MeshRenderer>(true).GroupBy(r=>r.name).ToDictionary(g=>g.Key,g=>g.First().sharedMaterials):new Dictionary<string,Material[]>();
                    foreach(var r in go.GetComponentsInChildren<MeshRenderer>(true)){
                        if(r.name=="Wall_Window"||r.name=="Wall_Solid_Mesh")r.sharedMaterials=Enumerable.Repeat(plan.original.sharedMaterial,r.sharedMaterials.Length).ToArray();
                        else if(reference.TryGetValue(r.name,out var materials)&&materials.Length==r.sharedMaterials.Length)r.sharedMaterials=materials;
                    }
                    Undo.RecordObject(plan.original,"Отключить прежнюю сплошную стену");plan.original.enabled=false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(plan.original);
                    foreach(var collider in plan.colliders){Undo.RecordObject(collider,"Отключить прежнюю сплошную коллизию");collider.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
                }
                foreach(var b in buildings){b.endWindowRuleVersion=1;PrefabUtility.RecordPrefabInstancePropertyModifications(b);}
                foreach(var b in buildings){
                    var editable=new HashSet<MeshFilter>(plans.Where(p=>p.building==b).SelectMany(p=>p.side.Find("EndWindows").GetComponentsInChildren<MeshFilter>(true)));
                    for(int pass=0;pass<3;pass++){
                        SeamUnion.Resolve(b.gameObject,editable);
                        var audit=CoplanarAudit.Scan(b.gameObject,true);
                        if(!audit.conflicts.Keys.Any(k=>k.Contains("/EndWindows/")))break;
                        if(pass==2)throw new InvalidOperationException("Остались пересечения новых торцевых модулей: "+b.name);
                    }
                }
                foreach(var go in createdRoots)Undo.RegisterCreatedObjectUndo(go,"Добавить оконные модули торца");
                Physics.SyncTransforms();
                var errors=new List<string>();foreach(var b in buildings)FacadeDetailChecks.CheckEndWindows(b,errors);
                if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors.Take(12)));
                foreach(var pair in before)if(!pair.Key||Serialized(pair.Key,sides,toggles,owners)!=pair.Value)
                    throw new InvalidOperationException("Изменился объект вне разрешённых полей: "+(pair.Key?pair.Key.name:"удалён"));
                Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
                if(verifyUndoRedo){
                    Undo.PerformUndo();
                    if(plans.Any(p=>p.side.Find("EndWindows")||!p.original.enabled||p.colliders.Any(c=>!c.enabled)))throw new InvalidOperationException("Undo не восстановил торцы.");
                    Undo.PerformRedo();Physics.SyncTransforms();
                    if(plans.Any(p=>!p.side.Find("EndWindows")||p.original.enabled||p.colliders.Any(c=>c.enabled)))throw new InvalidOperationException("Redo не восстановил оконные торцы.");
                    foreach(var pair in before)if(!pair.Key||Serialized(pair.Key,sides,toggles,owners)!=pair.Value)throw new InvalidOperationException("Undo/Redo изменил посторонний объект.");
                }
                EditorSceneManager.MarkSceneDirty(scene);
                return new Result{houses=buildings.Length,sides=plans.Count,windows=plans.Count*2,preservedObjects=before.Count,names=buildings.Select(b=>b.name).ToArray(),existingObjectsUnchanged=true,undoRedoVerified=verifyUndoRedo};
            }catch{Undo.RevertAllDownToGroup(group);foreach(var go in createdRoots)if(go)Object.DestroyImmediate(go);throw;}
        }
    }
}
