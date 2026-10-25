using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace ZombieSurvival.ModularBuildings.Editor
{
 public static class ExistingBuildingEnvelopeRepair
 {
  [Serializable] public sealed class Result{public int houses,solidInserts,windows,preservedObjects;public float bottom,top;public bool preserved;public string[] names;}
  public static Result Apply(GeneratedBuilding[] buildings){
   if(buildings==null||buildings.Length==0)throw new InvalidOperationException("Нет выбранных домов.");var scene=buildings[0].gameObject.scene;
   if(buildings.Any(b=>!b||b.gameObject.scene!=scene||!b.profile||EditorUtility.IsPersistent(b)))throw new InvalidOperationException("Нужны экземпляры домов одной сцены.");
   var groups=new Dictionary<GeneratedBuilding,Transform[]>();var solids=new HashSet<Transform>();var editable=new HashSet<MeshFilter>();
   foreach(var b in buildings){
    if(b.transform.Find("BaseTrim"))throw new InvalidOperationException(b.name+": цоколь уже добавлен. Повторная операция остановлена.");
    groups[b]=b.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="EndWindows").ToArray();
    if(groups[b].Length!=b.floors*2)throw new InvalidOperationException(b.name+": не распознаны все оконные торцы.");
    foreach(var g in groups[b]){foreach(var name in new[]{"Solid_0","Solid_3"}){var t=g.Find(name);if(!t||Mathf.Min(Mathf.Abs(Mathf.Abs(t.localPosition.x)-4.6f),Mathf.Abs(Mathf.Abs(t.localPosition.x)-4.4f))>.001f)throw new InvalidOperationException("Нестандартная крайняя вставка: "+b.name);solids.Add(t);}foreach(var f in g.GetComponentsInChildren<MeshFilter>(true))editable.Add(f);}
   }
   var owners=new HashSet<Object>(buildings.Cast<Object>());var roots=new HashSet<Object>(buildings.Select(b=>(Object)b.transform));
   Func<Object,string> state=o=>{string json=EditorJsonUtility.ToJson(o);
    if(roots.Contains(o))json=Regex.Replace(json,"\"m_Children\":\\[[^\\]]*\\]","\"m_Children\":[]");
    if(o is Transform t&&solids.Contains(t))json=Regex.Replace(json,"\"m_LocalPosition\":\\{\"x\":[^,]+,","\"m_LocalPosition\":{\"x\":0,");
    if(o is MeshFilter f&&editable.Contains(f))json=Regex.Replace(json,"\"m_Mesh\":\\{[^}]*\\}","\"m_Mesh\":{}");
    if(owners.Contains(o)){json=Regex.Replace(json,"\"exteriorEnvelopeVersion\":[0-9]+","\"exteriorEnvelopeVersion\":0");json=Regex.Replace(json,"\"baseTrimMaterialOverride\":\\{[^}]*\\}","\"baseTrimMaterialOverride\":{}");}
    using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json)));
   };
   var before=new Dictionary<Object,string>();foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)){before[t.gameObject]=state(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c)before[c]=state(c);}
   Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Устранить щели и добавить нижнюю отделку");var added=new List<GameObject>();
   try{
    Undo.RecordObjects(solids.Cast<Object>().Concat(editable.Cast<Object>()).Concat(owners).ToArray(),"Исправить стыки торцов и добавить цоколь");
    foreach(var b in buildings){
     var source=b.profile.solidWall.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Wall_Solid_Mesh").sharedMesh;
     foreach(var g in groups[b])foreach(var name in new[]{"Solid_0","Solid_3"}){var t=g.Find(name);t.localPosition=new Vector3(name=="Solid_0"?-4.4f:4.4f,t.localPosition.y,t.localPosition.z);t.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Wall_Solid_Mesh").sharedMesh=source;}
     var targets=new HashSet<MeshFilter>(groups[b].SelectMany(t=>t.GetComponentsInChildren<MeshFilter>(true)));
     for(int pass=0;pass<3;pass++){SeamUnion.Resolve(b.gameObject,targets);var a=CoplanarAudit.Scan(b.gameObject,true);if(!a.conflicts.Keys.Any(k=>k.Contains("/EndWindows/")))break;if(pass==2)throw new InvalidOperationException("Наложения исправленных торцевых модулей: "+b.name);}
     var band=BuildingBaseTrim.Add(b);added.Add(band);
     foreach(var t in solids.Where(t=>t.IsChildOf(b.transform)))PrefabUtility.RecordPrefabInstancePropertyModifications(t);
     foreach(var f in targets)PrefabUtility.RecordPrefabInstancePropertyModifications(f);PrefabUtility.RecordPrefabInstancePropertyModifications(b);
    }
    foreach(var go in added)Undo.RegisterCreatedObjectUndo(go,"Добавить контрастный цоколь");
    Physics.SyncTransforms();var errors=new List<string>();foreach(var b in buildings){FacadeDetailChecks.CheckEndWindows(b,errors);BuildingBaseTrim.Validate(b,errors);}if(errors.Count>0)throw new InvalidOperationException(string.Join(";",errors.Take(12)));
    foreach(var pair in before)if(!pair.Key||state(pair.Key)!=pair.Value)throw new InvalidOperationException("Постороннее изменение: "+(pair.Key?pair.Key.name:"удалённый объект"));
    Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);
    var bounds=BuildingBaseTrim.LocalBounds(buildings[0].transform,added[0].GetComponentsInChildren<MeshFilter>());
    return new Result{houses=buildings.Length,solidInserts=solids.Count,windows=groups.Sum(p=>p.Value.Length*2),preservedObjects=before.Count,bottom=bounds.min.y,top=bounds.max.y,preserved=true,names=buildings.Select(b=>b.name).ToArray()};
   }catch{Undo.RevertAllDownToGroup(undo);foreach(var go in added)if(go)Object.DestroyImmediate(go);throw;}
  }
 }
}
