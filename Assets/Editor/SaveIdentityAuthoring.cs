using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;

/// <summary>Authoring only: observe new scene instances; never repair historical conflicts implicitly.</summary>
[InitializeOnLoad]
public static class SaveIdentityAuthoring
{
    [Serializable] public sealed class Change
    {
        public string ObjectId, Path, OldId, NewId;
    }
    public sealed class Report
    {
        public readonly List<Change> Changes = new List<Change>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Registrations = new List<string>();
        public override string ToString() => JsonUtility.ToJson(new ReportData { changes = Changes.ToArray(), errors = Errors.ToArray(), registrations = Registrations.ToArray() }, true);
    }
    [Serializable] private sealed class ReportData { public Change[] changes; public string[] errors, registrations; }
    private static HashSet<SaveIdentity> known = new HashSet<SaveIdentity>();
    private static HashSet<WorldSaveObject> knownRoots = new HashSet<WorldSaveObject>();
    private static bool initialized, queued, applying;

    [MenuItem("Tools/Zombie Survival/Save IDs/Отчёт без изменений")]
    private static void ReportActive() => Debug.Log(Repair(SceneManager.GetActiveScene(), true).ToString());
    [MenuItem("Tools/Zombie Survival/Save IDs/Исправить ID и регистрацию активной сцены")]
    private static void RepairActive() => Debug.Log(Repair(SceneManager.GetActiveScene(), false).ToString());

    static SaveIdentityAuthoring()
    {
        EditorApplication.delayCall += CaptureBaseline;
        EditorApplication.hierarchyChanged += Queue;
        EditorSceneManager.sceneOpened += (_, __) => CaptureBaseline();
        EditorSceneManager.sceneClosed += _ => CaptureBaseline();
        EditorSceneManager.sceneSaving += (_, __) => ProcessNewObjects();
        Undo.undoRedoPerformed += CaptureBaseline;
        EditorApplication.playModeStateChanged += state => { initialized = false; if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += CaptureBaseline; };
    }
    private static bool Allowed => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
    private static bool Eligible(GameObject go) => go != null && !EditorUtility.IsPersistent(go) && go.scene.IsValid() && go.scene.isLoaded
        && !EditorSceneManager.IsPreviewScene(go.scene) && PrefabStageUtility.GetPrefabStage(go) == null
        && (go.hideFlags & HideFlags.DontSave) == 0;
    private static SaveIdentity[] Identities() => UnityEngine.Object.FindObjectsByType<SaveIdentity>(FindObjectsInactive.Include).Where(i => Eligible(i.gameObject)).ToArray();
    private static bool PlayerTree(Component component) => component.GetComponentInParent<FirstPersonController>(true) != null
        || component.GetComponentsInParent<SaveIdentity>(true).Any(i => i.Id == "player");
    private static WorldSaveObject[] Roots() => UnityEngine.Object.FindObjectsByType<WorldSaveObject>(FindObjectsInactive.Include).Where(i => Eligible(i.gameObject)).ToArray();
    public static void CaptureBaseline()
    {
        if (!Allowed) { initialized = false; return; }
        known = new HashSet<SaveIdentity>(Identities()); knownRoots = new HashSet<WorldSaveObject>(Roots()); initialized = true;
    }
    private static void Queue()
    {
        if (applying || queued || !Allowed) return;
        queued = true; EditorApplication.delayCall += ProcessNewObjects;
    }
    public static void ProcessNewObjects()
    {
        queued = false; if (!Allowed || applying) return;
        if (!initialized) { CaptureBaseline(); return; }
        applying = true;
        try
        {
            var all = Identities(); var used = new HashSet<string>(all.Select(i => i.Id).Where(i => !string.IsNullOrEmpty(i)), StringComparer.Ordinal);
            foreach (var id in all.Where(i => !known.Contains(i)))
            {
                if (PlayerTree(id))
                { Debug.LogWarning("Копия игрока не получает автоматический SaveIdentity: player зарезервирован. Удалите лишнего игрока или настройте область явно.", id); continue; }
                string next; do { next = "scene-" + Guid.NewGuid().ToString("N"); } while (!used.Add(next));
                Assign(id, next);
            }
            var report = new Report();
            foreach (var root in Roots().Where(r => !knownRoots.Contains(r)))
            {
                if (PlayerTree(root))
                { report.Errors.Add("Копия игрока не регистрируется автоматически: " + Path(root)); continue; }
                Register(root, false, report);
            }
            foreach (var error in report.Errors) Debug.LogWarning(error);
        }
        finally { applying = false; CaptureBaseline(); }
    }
    private static void Assign(SaveIdentity id, string value)
    {
        Undo.RecordObject(id, "Назначить уникальный ID копии");
        var serialized = new SerializedObject(id); serialized.FindProperty("persistentId").stringValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(id); EditorUtility.SetDirty(id); EditorSceneManager.MarkSceneDirty(id.gameObject.scene);
    }
    private static string Path(Component c) => c.gameObject.scene.path + ":" + AnimationUtility.CalculateTransformPath(c.transform, null);
    private static WorldSaveRegistry[] Registries(Scene scene) => UnityEngine.Object.FindObjectsByType<WorldSaveRegistry>(FindObjectsInactive.Include).Where(r => Eligible(r.gameObject) && r.gameObject.scene == scene).ToArray();
    private static WorldSaveObject[] Listed(WorldSaveRegistry registry)
    {
        var p = new SerializedObject(registry).FindProperty("sceneObjects"); var result = new List<WorldSaveObject>();
        for (int i = 0; i < p.arraySize; i++) if (p.GetArrayElementAtIndex(i).objectReferenceValue is WorldSaveObject root) result.Add(root);
        return result.ToArray();
    }
    private static void Register(WorldSaveObject root, bool dryRun, Report report)
    {
        var registries = Registries(root.gameObject.scene);
        if (registries.Length != 1) { report.Errors.Add("Нужен ровно один WorldSaveRegistry для новой копии: " + Path(root)); return; }
        if (root.transform.parent != null && root.transform.parent.GetComponentInParent<WorldSaveObject>(true) != null)
        { report.Errors.Add("Вложенный WorldSaveObject не регистрируется: " + Path(root)); return; }
        var registry = registries[0]; if (Listed(registry).Contains(root)) return;
        report.Registrations.Add(Path(root)); if (dryRun) return;
        Undo.RecordObject(registry, "Зарегистрировать сохраняемую копию");
        var serialized = new SerializedObject(registry); var list = serialized.FindProperty("sceneObjects");
        int index = list.arraySize; list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = root; serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(registry); EditorUtility.SetDirty(registry); EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }
    /// <summary>Explicit historical repair. Ambiguous originals require caller-supplied identities; no hierarchy/name guess.</summary>
    public static Report Repair(Scene scene, bool dryRun = true, SaveIdentity[] preferredOriginals = null)
    {
        if (!Allowed || !scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) throw new InvalidOperationException("Исправление ID доступно только для открытой обычной сцены вне Play.");
        var report = new Report(); var all = Identities(); var preferred = new HashSet<SaveIdentity>(preferredOriginals ?? Array.Empty<SaveIdentity>());
        var registries = Registries(scene); var ordered = registries.Length == 1 ? Listed(registries[0]) : Array.Empty<WorldSaveObject>();
        var planned = new List<KeyValuePair<SaveIdentity, string>>(); var used = new HashSet<string>(all.Select(i => i.Id).Where(i => !string.IsNullOrEmpty(i)), StringComparer.Ordinal);
        foreach (var group in all.Where(i => i.gameObject.scene == scene).GroupBy(i => i.Id ?? "", StringComparer.Ordinal))
        {
            var entries = group.ToArray(); SaveIdentity original = null;
            if (!string.IsNullOrWhiteSpace(group.Key) && entries.Length == 1) continue;
            if (group.Key == "player" || entries.Any(i => i.GetComponent<FirstPersonController>() != null))
            { report.Errors.Add("Зарезервированный ID игрока требует ручного решения: " + group.Key); continue; }
            if (!string.IsNullOrWhiteSpace(group.Key))
            {
                var chosen = entries.Where(preferred.Contains).ToArray();
                if (chosen.Length == 1) original = chosen[0];
                else if (chosen.Length == 0)
                {
                    var owned = entries.Where(i => ordered.Contains(i.GetComponentInParent<WorldSaveObject>(true))).ToArray();
                    if (owned.Length == 1) original = owned[0];
                }
                if (original == null) { report.Errors.Add("Неоднозначный оригинал ID «" + group.Key + "»: передайте preferredOriginals. " + string.Join(", ", entries.Select(Path))); continue; }
            }
            foreach (var id in entries.Where(i => i != original))
            {
                string next; do { next = "scene-" + Guid.NewGuid().ToString("N"); } while (!used.Add(next));
                report.Changes.Add(new Change { ObjectId = GlobalObjectId.GetGlobalObjectIdSlow(id).ToString(), Path = Path(id), OldId = id.Id, NewId = next }); planned.Add(new KeyValuePair<SaveIdentity, string>(id, next));
            }
        }
        // Refuse the entire repair if even one original is ambiguous.
        if (report.Errors.Count != 0) return report;
        var sceneRoots = Roots().Where(r => r.gameObject.scene == scene).ToArray();
        foreach (var root in sceneRoots) Register(root, true, report);
        if (report.Errors.Count != 0 || dryRun) return report;
        report.Registrations.Clear();
        if (!dryRun) { foreach (var pair in planned) Assign(pair.Key, pair.Value); }
        foreach (var root in sceneRoots) Register(root, false, report);
        if (!dryRun) CaptureBaseline(); return report;
    }
}
