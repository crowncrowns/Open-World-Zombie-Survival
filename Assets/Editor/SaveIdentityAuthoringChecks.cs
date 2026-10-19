using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieSurvival.Persistence;

public static class SaveIdentityAuthoringChecks
{
    [MenuItem("Tools/Zombie Survival/Проверить ID сценовых копий")]
    private static void MenuRun() => Debug.Log(Run());
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); int pass = 0;
        GameObject original = null, clone = null, registryGo = null;
        try
        {
            registryGo = new GameObject("Identity check registry"); var registry = registryGo.AddComponent<WorldSaveRegistry>();
            original = new GameObject("Original"); var id = original.AddComponent<SaveIdentity>(); Set(id, "identity-check-original"); original.AddComponent<WorldSaveObject>();
            var child = new GameObject("Storage identity"); child.transform.SetParent(original.transform); var childId = child.AddComponent<SaveIdentity>(); Set(childId, "identity-check-child");
            var list = new SerializedObject(registry); list.FindProperty("sceneObjects").arraySize = 1; list.FindProperty("sceneObjects").GetArrayElementAtIndex(0).objectReferenceValue = original.GetComponent<WorldSaveObject>(); list.ApplyModifiedPropertiesWithoutUndo();
            SaveIdentityAuthoring.CaptureBaseline(); clone = UnityEngine.Object.Instantiate(original); var copy = clone.GetComponent<SaveIdentity>(); var copyChild = clone.GetComponentsInChildren<SaveIdentity>().First(i => i != copy);
            SaveIdentityAuthoring.ProcessNewObjects();
            Check(id.Id == "identity-check-original" && childId.Id == "identity-check-child", "original IDs preserved", ref pass);
            Check(copy.Id != id.Id && copyChild.Id != childId.Id && copy.Id != copyChild.Id, "root and child new IDs", ref pass);
            var authored = new SerializedObject(registry).FindProperty("sceneObjects"); Check(authored.arraySize == 2, "new root registered once", ref pass);
            string redoId = copy.Id; SaveIdentityAuthoring.ProcessNewObjects(); Check(copy.Id == redoId && new SerializedObject(registry).FindProperty("sceneObjects").arraySize == 2, "idempotent automatic pass", ref pass);
            Set(copy, id.Id); Set(copyChild, childId.Id); SaveIdentityAuthoring.CaptureBaseline(); SaveIdentityAuthoring.ProcessNewObjects(); Check(copy.Id == id.Id, "historical duplicates never auto-repaired", ref pass);
            var ambiguousRegistered = SaveIdentityAuthoring.Repair(scene, false);
            Check(ambiguousRegistered.Errors.Count == 2 && copy.Id == id.Id && copyChild.Id == childId.Id, "multiple registered owners refuse without preference", ref pass);
            var dry = SaveIdentityAuthoring.Repair(scene, true, new[] { id, childId }); Check(dry.Errors.Count == 0 && dry.Changes.Count == 2 && copy.Id == id.Id, "dry-run no mutation", ref pass);
            var repair = SaveIdentityAuthoring.Repair(scene, false, new[] { id, childId }); Check(repair.Errors.Count == 0 && id.Id == "identity-check-original" && copy.Id != id.Id && copyChild.Id != childId.Id, "explicit original preserved in repair", ref pass);
            Check(SaveIdentityAuthoring.Repair(scene, true).Changes.Count == 0, "repair idempotent", ref pass);
            Set(copy, "ambiguous"); Set(copyChild, "ambiguous"); var rejected = SaveIdentityAuthoring.Repair(scene, false); Check(rejected.Errors.Count == 1 && copy.Id == "ambiguous" && copyChild.Id == "ambiguous", "ambiguous same-root collision refuses", ref pass);
            SaveIdentityAuthoring.CaptureBaseline();
            var player = new GameObject("Reserved player copy"); Set(player.AddComponent<SaveIdentity>(), "player"); player.AddComponent<WorldSaveObject>();
            var playerChild = new GameObject("Reserved child"); playerChild.transform.SetParent(player.transform); var reservedChildId = playerChild.AddComponent<SaveIdentity>(); Set(reservedChildId, "reserved-child");
            SaveIdentityAuthoring.ProcessNewObjects();
            Check(reservedChildId.Id == "reserved-child" && new SerializedObject(registry).FindProperty("sceneObjects").arraySize == 2, "player copy tree neither reidentified nor registered", ref pass);
            UnityEngine.Object.DestroyImmediate(player);
            var empty = new GameObject("New empty ID").AddComponent<SaveIdentity>(); SaveIdentityAuthoring.ProcessNewObjects();
            Check(!string.IsNullOrEmpty(empty.Id) && empty.GetComponent<WorldSaveObject>() == null, "empty new ID assigned without adding world-save contract", ref pass);
            Undo.ClearUndo(empty);
            SaveIdentityAuthoring.CaptureBaseline(); var instant = UnityEngine.Object.Instantiate(original);
            string temp = System.IO.Path.Combine(System.IO.Path.GetFullPath("Temp"), "IdentityAuthoring-" + Guid.NewGuid().ToString("N") + ".unity");
            try
            {
                Check(EditorSceneManager.SaveScene(scene, temp), "temporary scene saved", ref pass);
                Check(instant.GetComponent<SaveIdentity>().Id != id.Id && !scene.isDirty && new SerializedObject(registry).FindProperty("sceneObjects").arraySize == 3, "sceneSaving flushes queued instance before serialization", ref pass);
            }
            finally { foreach (var i in instant.GetComponentsInChildren<SaveIdentity>()) Undo.ClearUndo(i); if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); }
            return "SaveIdentity authoring: " + pass + " PASS (temporary additive Edit scene saved to Temp and removed; user scenes not saved).";
        }
        finally
        {
            if (original != null) { Undo.ClearUndo(original.GetComponent<SaveIdentity>()); foreach (var i in original.GetComponentsInChildren<SaveIdentity>()) Undo.ClearUndo(i); }
            if (clone != null) foreach (var i in clone.GetComponentsInChildren<SaveIdentity>()) Undo.ClearUndo(i);
            if (registryGo != null) Undo.ClearUndo(registryGo.GetComponent<WorldSaveRegistry>());
            EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); SaveIdentityAuthoring.CaptureBaseline();
        }
    }
    private static void Set(SaveIdentity identity, string value) { var s = new SerializedObject(identity); s.FindProperty("persistentId").stringValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Check(bool valid, string name, ref int count) { if (!valid) throw new InvalidOperationException("SaveIdentity check failed: " + name); count++; }
}
