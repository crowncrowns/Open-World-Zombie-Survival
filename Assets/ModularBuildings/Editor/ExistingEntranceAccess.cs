using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace ZombieSurvival.ModularBuildings.Editor
{
 public static class ExistingEntranceAccess
 {
  [Serializable]public sealed class Result{public int houses,entrances,steps,preservedObjects;public bool preserved,terrainFitted;public string[] names;}
  public static Result Apply(GeneratedBuilding[] houses){
   if(houses==null||houses.Length==0)throw new InvalidOperationException("Нет выбранных домов.");var scene=houses[0].gameObject.scene;if(houses.Any(b=>!b||b.gameObject.scene!=scene||b.transform.Find("EntranceAccess")))throw new InvalidOperationException("У дома уже есть входные лестницы; используйте отдельную подгонку.");
   var transforms=new HashSet<Object>(houses.Select(b=>(Object)b.transform));var owners=new HashSet<Object>(houses.Cast<Object>());
   Func<Object,string> hash=o=>{var s=EditorJsonUtility.ToJson(o);if(transforms.Contains(o))s=Regex.Replace(s,"\"m_Children\":\\[[^\\]]*\\]","\"m_Children\":[]");if(owners.Contains(o))s=Regex.Replace(s,"\"entranceAccessRuleVersion\":[0-9]+","\"entranceAccessRuleVersion\":0");using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));};
   var before=new Dictionary<Object,string>();foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)){before[t.gameObject]=hash(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c)before[c]=hash(c);}
   Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Добавить входные лестницы с доступом к Terrain");var added=new List<GeneratedEntranceAccess>();
   try{
    foreach(var b in houses)added.Add(BuildingEntranceStairs.Refit(b,null,true,true));
    foreach(var pair in before)if(!pair.Key||hash(pair.Key)!=pair.Value)throw new InvalidOperationException("Изменился посторонний объект: "+(pair.Key?pair.Key.name:"удалён"));
    Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);
    return new Result{houses=houses.Length,entrances=added.Sum(a=>a.routes.Length),steps=added.Sum(a=>a.routes.Sum(r=>r.steps)),preservedObjects=before.Count,preserved=true,terrainFitted=added.All(a=>a.terrainFitted),names=houses.Select(b=>b.name).ToArray()};
   }catch{Undo.RevertAllDownToGroup(group);throw;}
  }
  public static Result ReplaceWithUniform(GeneratedBuilding[] houses){
   if(houses==null||houses.Length==0)throw new InvalidOperationException("Нет домов.");var scene=houses[0].gameObject.scene;
   if(houses.Any(b=>!b||b.gameObject.scene!=scene||!b.transform.Find("EntranceAccess")))throw new InvalidOperationException("Ожидались существующие лестницы в одной сцене.");
   var metadata=new HashSet<Object>();var meshes=new HashSet<Object>();var enabled=new HashSet<Object>();var owners=new HashSet<Object>(houses.Cast<Object>());
   foreach(var b in houses){var root=b.transform.Find("EntranceAccess");var access=root.GetComponent<GeneratedEntranceAccess>();if(!access)throw new InvalidOperationException("Неизвестный владелец лестниц.");metadata.Add(access);
    for(int i=1;i<=b.entrances;i++)foreach(string name in new[]{"Entrance_"+i,"Rails_"+i}){var t=root.Find(name);if(!t)throw new InvalidOperationException("Не найдена лестница "+name);meshes.Add(t.GetComponent<MeshFilter>());meshes.Add(t.GetComponent<MeshCollider>());enabled.Add(t.GetComponent<MeshRenderer>());enabled.Add(t.GetComponent<MeshCollider>());}
   }
   Func<Object,string> hash=o=>{var s=EditorJsonUtility.ToJson(o);if(meshes.Contains(o))s=Regex.Replace(s,"\"m_Mesh\":\\{[^}]*\\}","\"m_Mesh\":{}");if(enabled.Contains(o))s=Regex.Replace(s,"\"m_Enabled\":(?:true|false|[01])","\"m_Enabled\":1");if(owners.Contains(o))s=Regex.Replace(s,"\"entranceAccessRuleVersion\":[0-9]+","\"entranceAccessRuleVersion\":0");using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));};
   var before=new Dictionary<Object,string>();foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)){before[t.gameObject]=hash(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c&&!metadata.Contains(c))before[c]=hash(c);}
   Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Одинаковые высокие входные лестницы");var result=new List<GeneratedEntranceAccess>();
   try{foreach(var b in houses)result.Add(BuildingEntranceStairs.Refit(b,null,true,true));foreach(var pair in before)if(!pair.Key||hash(pair.Key)!=pair.Value)throw new InvalidOperationException("Изменился посторонний объект: "+(pair.Key?pair.Key.name:"удалён"));
    Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);
    return new Result{houses=houses.Length,entrances=result.Sum(a=>a.routes.Length),steps=result.Sum(a=>a.routes.Sum(r=>r.steps)),preservedObjects=before.Count,preserved=true,terrainFitted=false,names=houses.Select(b=>b.name).ToArray()};
   }catch{Undo.RevertAllDownToGroup(group);throw;}
  }
  [Serializable]public sealed class RailResult{public int houses,railings,preservedObjects;public bool preserved;}
  public static RailResult RepairRailings(GeneratedBuilding[] houses){
   if(houses==null||houses.Length==0)throw new InvalidOperationException("Нет домов.");var scene=houses[0].gameObject.scene;var meshes=new HashSet<Object>();
   foreach(var b in houses){if(!b||b.gameObject.scene!=scene)throw new InvalidOperationException("Дома из разных сцен.");for(int i=1;i<=b.entrances;i++){var t=b.transform.Find("EntranceAccess/Rails_"+i);if(!t)throw new InvalidOperationException("Нет перил.");meshes.Add(t.GetComponent<MeshFilter>());meshes.Add(t.GetComponent<MeshCollider>());}}
   Func<Object,string> hash=o=>{var s=EditorJsonUtility.ToJson(o);if(meshes.Contains(o))s=Regex.Replace(s,"\"m_Mesh\":\\{[^}]*\\}","\"m_Mesh\":{}");using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));};
   var before=new Dictionary<Object,string>();foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)){before[t.gameObject]=hash(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c)before[c]=hash(c);}
   Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Исправить геометрию перил");
   try{foreach(var b in houses)BuildingEntranceStairs.RepairRailings(b,true);foreach(var pair in before)if(!pair.Key||hash(pair.Key)!=pair.Value)throw new InvalidOperationException("Изменился посторонний объект: "+(pair.Key?pair.Key.name:"удалён"));Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);return new RailResult{houses=houses.Length,railings=houses.Sum(b=>b.entrances),preservedObjects=before.Count,preserved=true};}catch{Undo.RevertAllDownToGroup(group);throw;}
  }
 }
}
