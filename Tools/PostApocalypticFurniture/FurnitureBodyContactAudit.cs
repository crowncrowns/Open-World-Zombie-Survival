// Verification only. Native shape sampling complements source vertex and OBB checks.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Furniture;
public static class FurnitureBodyContactAudit
{
    [Serializable] sealed class Contact { public string asset,part,moving,fixedPart;public int movingIndex,bodyIndex;public float fraction,depth,closedDepth; }
    [Serializable] sealed class Report {public string context;public int prefabs,mechanisms,poses;public List<Contact> contacts=new List<Contact>();}
    public static void Run() {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/PostApocalypticFurniture/UnityProject/"))throw new Exception("Isolated furniture project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var report=new Report{context="Native Box/Capsule penetration against each prefab's static Body only. Twenty-one authored progress samples. Contacts up to closed baseline plus 2 mm are not listed; this is a modelling diagnostic, not an all-world collision acceptance."};
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/PostApocalypticFurniture/Prefabs"})){
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));report.prefabs++;
            var body=go.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Body");var fixedShapes=body.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy).ToArray();
            var assembly=go.GetComponent<FurnitureAssembly>();var initial=assembly.CaptureState();
            foreach(var motion in go.GetComponentsInChildren<FurnitureMechanism>(true)){
                assembly.RestoreStateSilently(initial);Prepare(motion,new HashSet<FurnitureMechanism>());
                report.mechanisms++;var moving=motion.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy).ToArray();
                var saved=motion.CaptureState();var baseline=new float[moving.Length,fixedShapes.Length];Physics.SyncTransforms();
                for(int a=0;a<moving.Length;a++)for(int b=0;b<fixedShapes.Length;b++)if(Physics.ComputePenetration(moving[a],moving[a].transform.position,moving[a].transform.rotation,fixedShapes[b],fixedShapes[b].transform.position,fixedShapes[b].transform.rotation,out _,out float depth))baseline[a,b]=depth;
                var found=new Dictionary<string,Contact>();
                for(int step=0;step<=20;step++){var pose=motion.CaptureState();pose.position=step/20f;pose.velocity=0;pose.targetOpen=true;motion.RestoreStateSilently(pose);Physics.SyncTransforms();report.poses++;
                    for(int a=0;a<moving.Length;a++)for(int b=0;b<fixedShapes.Length;b++){
                        if(!Physics.ComputePenetration(moving[a],moving[a].transform.position,moving[a].transform.rotation,fixedShapes[b],fixedShapes[b].transform.position,fixedShapes[b].transform.rotation,out _,out float depth)||depth<=baseline[a,b]+.002f)continue;
                        string key=a+":"+b;if(found.TryGetValue(key,out var old)&&old.depth>=depth)continue;
                        found[key]=new Contact{asset=go.GetComponent<FurnitureAssembly>().AssetId,part=motion.StableId,moving=moving[a].GetType().Name,fixedPart=fixedShapes[b].GetType().Name,movingIndex=a,bodyIndex=b,fraction=pose.position,depth=depth,closedDepth=baseline[a,b]};
                    }
                }
                report.contacts.AddRange(found.Values);motion.RestoreStateSilently(saved);
            }
            UnityEngine.Object.DestroyImmediate(go);
        }
        string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../Verification/PostApocalypticFurniture/native_body_contacts.json"));File.WriteAllText(path,JsonUtility.ToJson(report,true));Debug.Log("FURNITURE_NATIVE_BODY_CONTACT_DIAGNOSTIC "+report.contacts.Count);EditorApplication.Exit(0);
    }
    static void Prepare(FurnitureMechanism motion,HashSet<FurnitureMechanism> visited) {
        if(!visited.Add(motion))return;
        var property=new SerializedObject(motion).FindProperty("interlocks");
        for(int i=0;i<property.arraySize;i++) {
            var row=property.GetArrayElementAtIndex(i);if(!row.FindPropertyRelative("whenOpening").boolValue)continue;
            var other=row.FindPropertyRelative("other").objectReferenceValue as FurnitureMechanism;if(other==null)continue;
            Prepare(other,visited);var state=other.CaptureState();state.position=row.FindPropertyRelative("minimum").floatValue>.5f?1:0;state.targetOpen=state.position>.5f;state.velocity=0;other.RestoreStateSilently(state);
        }
    }
}
