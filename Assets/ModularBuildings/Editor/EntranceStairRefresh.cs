using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace ZombieSurvival.ModularBuildings.Editor
{
 [InitializeOnLoad] public static class EntranceStairRefresh
 {
  sealed class State{public int observed,applied;public double changed;}
  static readonly Dictionary<GeneratedEntranceAccess,State> states=new Dictionary<GeneratedEntranceAccess,State>();static double next;static int terrainRevision;
  static EntranceStairRefresh(){EditorApplication.update+=Update;TerrainCallbacks.heightmapChanged+=(t,r,s)=>terrainRevision++;}
  static int Signature(GeneratedBuilding b,GeneratedEntranceAccess a){unchecked{int h=b.transform.localToWorldMatrix.GetHashCode();h=h*31+b.profile.entranceStairWidth.GetHashCode();h=h*31+b.profile.entranceMaxRiser.GetHashCode();h=h*31+b.profile.entranceTreadDepth.GetHashCode();h=h*31+b.profile.entranceLandingDepth.GetHashCode();h=h*31+b.profile.entranceMaxRun.GetHashCode();h=h*31+terrainRevision;h=h*31+(a.terrain?a.terrain.transform.localToWorldMatrix.GetHashCode():0);h=h*31+(a.terrain?a.terrain.GetHashCode():0);h=h*31+(a.terrain&&a.terrain.terrainData?a.terrain.terrainData.GetHashCode():0);h=h*31+Terrain.activeTerrains.Length;return h;}}
  static void Update(){
   double now=EditorApplication.timeSinceStartup;if(now<next||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;next=now+.35;
   foreach(var a in UnityEngine.Object.FindObjectsByType<GeneratedEntranceAccess>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)){
    var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene==a.gameObject.scene)continue;
    if(a.uniformGeometry||!a.autoFit||EditorUtility.IsPersistent(a)||!a.gameObject.scene.IsValid()||EditorSceneManager.IsPreviewScene(a.gameObject.scene))continue;
    var b=a.transform.parent?a.transform.parent.GetComponent<GeneratedBuilding>():null;if(!b||!b.profile||!b.profile.autoFitEntranceStairs)continue;
    int signature=Signature(b,a);if(!states.TryGetValue(a,out var state)){state=new State{observed=signature,applied=signature,changed=now};states[a]=state;if(a.fittedBuildingMatrix!=b.transform.localToWorldMatrix||a.fittedWidth!=b.profile.entranceStairWidth||a.fittedRiser!=b.profile.entranceMaxRiser||a.fittedTread!=b.profile.entranceTreadDepth||a.fittedLanding!=b.profile.entranceLandingDepth)state.applied=~signature;
     foreach(var route in a.routes)if(route.path!=null&&route.path.Length>0){var end=b.transform.TransformPoint(route.path[route.path.Length-1]);var terrain=BuildingEntranceStairs.TerrainAt(b,end,a.terrain);if((!route.fitted&&terrain)||(route.fitted&&(!terrain||Mathf.Abs(end.y-(terrain.SampleHeight(end)+terrain.transform.position.y)-.02f)>.05f)))state.applied=~signature;}
    }
    if(state.observed!=signature){state.observed=signature;state.changed=now;continue;}
    if(state.applied==signature||now-state.changed<.75)continue;state.applied=signature;
    try{BuildingEntranceStairs.Refit(b,a.terrain,true,false);EditorUtility.SetDirty(a);EditorSceneManager.MarkSceneDirty(b.gameObject.scene);SceneView.RepaintAll();}
    catch(Exception e){a.terrainFitted=false;a.fitFailed=true;a.diagnostic="Подгонка лестниц остановлена: "+e.Message;EditorUtility.SetDirty(a);Debug.LogWarning(a.diagnostic,a);}
   }
   foreach(var key in states.Keys.Where(k=>!k).ToArray())states.Remove(key);
  }
  public static void CheckNow(){next=0;Update();}
 }
}
