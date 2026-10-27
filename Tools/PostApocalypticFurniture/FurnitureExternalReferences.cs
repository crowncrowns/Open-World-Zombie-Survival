using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class FurnitureExternalReferences {
 [Serializable] class Reference {public int line;public string id,asset,field;}
 [Serializable] class Scan {public string scene,sceneSHA256;public Reference[] references;}
 [Serializable] class Report {public bool passed;public string scene,sceneSHA256;public int references,assets;public List<string> missing=new List<string>();}
 public static void Run(){
  if(!Application.dataPath.Replace('\\','/').Contains("/work/PostApocalypticFurniture/UnityProject/"))throw new Exception("Isolated furniture project only");
  string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../.."));
  var scan=JsonUtility.FromJson<Scan>(File.ReadAllText(Path.Combine(repo,"work/PostApocalypticFurniture/ProfessionalAudit/scene_reference_scan.json")));
  var report=new Report{scene=scan.scene,sceneSHA256=scan.sceneSHA256,references=scan.references.Length};
  foreach(var group in scan.references.GroupBy(r=>r.asset)){
   string path="Assets/PostApocalypticFurniture/"+group.Key.Replace('\\','/');var ids=new HashSet<long>();
   foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path)){
    Add(asset,ids);
    if(asset is GameObject go){foreach(var c in go.GetComponentsInChildren<Component>(true)){if(c!=null){Add(c,ids);Add(c.gameObject,ids);}}}
   }
   foreach(var r in group)if(r.id!="0"&&r.id!="100100000"&&!ids.Contains(long.Parse(r.id)))report.missing.Add(path+" fileID "+r.id+" scene line "+r.line);
   report.assets++;
  }
  report.passed=report.missing.Count==0;
  File.WriteAllText(Path.Combine(repo,"Verification/PostApocalypticFurniture/ProfessionalAudit/external_scene_references.json"),JsonUtility.ToJson(report,true));
  if(!report.passed)throw new Exception(string.Join("; ",report.missing));
  Debug.Log("FURNITURE_EXTERNAL_REFERENCES_PASS "+report.references+" refs in "+report.assets+" assets");EditorApplication.Exit(0);
 }
 static void Add(UnityEngine.Object obj,HashSet<long> ids){if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long id))ids.Add(id);}
}
