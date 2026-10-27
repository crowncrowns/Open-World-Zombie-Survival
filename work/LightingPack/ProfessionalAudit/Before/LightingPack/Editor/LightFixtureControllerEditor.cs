using UnityEditor;
using UnityEngine;
using PostApocalypse.Lighting;

[CustomEditor(typeof(LightFixtureController)),CanEditMultipleObjects]
public sealed class LightFixtureControllerEditor : Editor
{
    double next;
    void OnEnable(){EditorApplication.update+=Preview;Undo.undoRedoPerformed+=ApplyTargets;}
    void OnDisable(){EditorApplication.update-=Preview;Undo.undoRedoPerformed-=ApplyTargets;}
    void ApplyTargets()
    {foreach(var item in targets)if(item is LightFixtureController c&&c.gameObject.scene.IsValid())c.ApplyNow();SceneView.RepaintAll();}
    void Preview()
    {
        if(Application.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.05;
        foreach(var item in targets)if(item is LightFixtureController c&&c.previewEffectsInEditor&&c.gameObject.scene.IsValid()) {c.ApplyNow();SceneView.RepaintAll();}
    }
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();DrawDefaultInspector();
        if(EditorGUI.EndChangeCheck())foreach(var t in targets)if(t is LightFixtureController c&&c.gameObject.scene.IsValid()) {Undo.RecordObjects(c.lights.SelectObjects(),"Fixture settings");c.ApplyNow();EditorUtility.SetDirty(c);}
        var controller=(LightFixtureController)target;
        if(controller.bakeType!=LightmapBakeType.Realtime)EditorGUILayout.HelpBox("Запечённый свет не меняется вместе с режимом/яркостью в Play. Динамические режимы регулируют только realtime-часть.",MessageType.Info);
    }
}
static class FixtureEditorObjects
{
    public static Object[] SelectObjects(this LightFixtureController.LightBinding[] lights)
    {var values=new System.Collections.Generic.List<Object>();foreach(var l in lights)if(l.light)values.Add(l.light);return values.ToArray();}
}
