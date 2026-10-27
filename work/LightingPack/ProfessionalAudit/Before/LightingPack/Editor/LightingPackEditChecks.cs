using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PostApocalypse.Lighting;

public static class LightingPackEditChecks
{
    public static void Run()
    {
        LightingPackBuilder.Guard();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var checks=new List<string>();
        void Check(bool pass,string message){if(!pass)throw new Exception(message);checks.Add(message);}
        var f=LightingPackBuilder.Data.fixtures[0];var source=AssetDatabase.LoadAssetAtPath<GameObject>(LightingPackBuilder.PathFor(f));
        var a=((GameObject)PrefabUtility.InstantiatePrefab(source)).GetComponent<LightFixtureController>();
        var b=((GameObject)PrefabUtility.InstantiatePrefab(source)).GetComponent<LightFixtureController>();b.transform.position=Vector3.right;
        a.intensity=2;a.lightColor=Color.red;b.intensity=6;b.lightColor=Color.blue;a.ApplyNow();b.ApplyNow();
        PrefabUtility.RecordPrefabInstancePropertyModifications(a);PrefabUtility.RecordPrefabInstancePropertyModifications(b);
        Check(Mathf.Abs(a.lights[0].light.intensity-2*a.lights[0].weight)<.0001f,"Edit intensity applies to real light");
        Check(Mathf.Abs(b.lights[0].light.intensity-6*b.lights[0].weight)<.0001f,"Edit duplicate intensity independent");
        foreach(var e in a.emitters)
        {
            var block=new MaterialPropertyBlock();e.renderer.GetPropertyBlock(block,e.materialIndex);
            Check(block.GetColor("_EmissionColor")==Color.red*(2*e.weight),"Edit all LOD emission slots follow intensity/color");
            block.SetFloat("_OtherSystemValue",.37f);e.renderer.SetPropertyBlock(block,e.materialIndex);a.ApplyNow();e.renderer.GetPropertyBlock(block,e.materialIndex);
            Check(Mathf.Abs(block.GetFloat("_OtherSystemValue")-.37f)<.0001f,"MPB preserves other component properties");
        }
        foreach(var e in b.emitters){var block=new MaterialPropertyBlock();e.renderer.GetPropertyBlock(block,e.materialIndex);Check(block.GetColor("_EmissionColor")==Color.blue*(6*e.weight),"Edit duplicate glow independent");}
        Check(a.emitters.All(e=>e.renderer.sharedMaterials[e.materialIndex].GetColor("_EmissionColor").maxColorComponent==0),"Edit does not mutate shared emission");
        a.lightEnabled=false;a.ApplyNow();Check(a.lights.All(l=>!l.light.enabled),"Edit power off disables real sources");
        foreach(var e in a.emitters){var block=new MaterialPropertyBlock();e.renderer.GetPropertyBlock(block,e.materialIndex);Check(block.GetColor("_EmissionColor").maxColorComponent==0,"Edit power off zero emission");}
        a.lightEnabled=true;a.ApplyNow();Check(Mathf.Abs(a.EffectiveIntensity-2)<.0001f,"Edit power restore retains intensity");
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.RegisterCompleteObjectUndo(a,"Fixture intensity test");a.intensity=7;EditorUtility.SetDirty(a);PrefabUtility.RecordPrefabInstancePropertyModifications(a);Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);a.ApplyNow();Undo.PerformUndo();a.ApplyNow();Check(Mathf.Abs(a.intensity-2)<.0001f&&Mathf.Abs(a.EffectiveIntensity-2)<.0001f,"Editor Undo restores settings and light; intensity="+a.intensity+" effective="+a.EffectiveIntensity);
        Undo.PerformRedo();a.ApplyNow();Check(Mathf.Abs(a.intensity-7)<.0001f&&Mathf.Abs(a.EffectiveIntensity-7)<.0001f,"Editor Redo restores settings and light");
        a.mode=LightFixtureController.OperatingMode.Flickering;a.previewEffectsInEditor=false;Check(a.EvaluateFactor(1)==1&&a.EvaluateFactor(2)==1,"Edit effects remain stable with preview off");
        a.previewEffectsInEditor=true;Check(Mathf.Abs(a.EvaluateFactor(1)-a.EvaluateFactor(2))>.0001f,"Edit preview permits animated flicker");a.previewEffectsInEditor=false;a.mode=LightFixtureController.OperatingMode.Normal;
        a.bakeType=LightmapBakeType.Baked;a.ApplyNow();Check(a.lights.All(l=>l.light.lightmapBakeType==LightmapBakeType.Baked),"Editor Baked type setting (GI bake not tested)");a.bakeType=LightmapBakeType.Mixed;a.ApplyNow();Check(a.lights.All(l=>l.light.lightmapBakeType==LightmapBakeType.Mixed),"Editor Mixed type setting (GI bake not tested)");a.bakeType=LightmapBakeType.Realtime;a.ApplyNow();
        PrefabUtility.RecordPrefabInstancePropertyModifications(a);PrefabUtility.RecordPrefabInstancePropertyModifications(b);
        Directory.CreateDirectory("Assets/LightingPackValidation");
        var path="Assets/LightingPackValidation/EditorOverrides.unity";EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),path);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.OpenScene(path);
        var instances=UnityEngine.Object.FindObjectsByType<LightFixtureController>(FindObjectsSortMode.None).OrderBy(c=>c.transform.position.x).ToArray();
        Check(instances.Length==2&&instances[0].intensity==7&&instances[1].intensity==6,"Scene reload preserves independent prefab intensity overrides");
        Check(instances[0].lightColor==Color.red&&instances[1].lightColor==Color.blue,"Scene reload preserves independent color overrides");
        var result=new LightingPackBuilder.Result{passed=true,unity=Application.unityVersion,urp="17.6.0",checks=checks};
        File.WriteAllText(LightingPackBuilder.Proof+"/unity_edit_checks.json",JsonUtility.ToJson(result,true));Debug.Log("LIGHTING_EDIT_PASS "+checks.Count);
    }
    public static void ColdAndGallery()
    {
        LightingPackBuilder.Validate();
        File.Copy(LightingPackBuilder.Proof+"/unity_import.json",LightingPackBuilder.Proof+"/unity_cold.json",true);
        Run();LightingPackChecks.CaptureGallery();Debug.Log("LIGHTING_COLD_AND_GALLERY_PASS");
    }
}
