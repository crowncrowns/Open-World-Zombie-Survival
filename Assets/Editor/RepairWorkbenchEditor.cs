using UnityEditor;
using UnityEngine;
using ZombieSurvival.Services;

/// <summary>Authoring assistance only; never adds physics or saves scenes on validation.</summary>
[CustomEditor(typeof(RepairWorkbench))]
public sealed class RepairWorkbenchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var bench = (RepairWorkbench)target;
        var point = bench.GetComponent<WorkbenchServicePoint>();
        if (bench.Policy == null)
            EditorGUILayout.HelpBox("Нет правил ремонта. Назначьте RepairPolicy или восстановите Resources/RepairPolicies/Default.", MessageType.Error);
        if (point == null)
        {
            EditorGUILayout.HelpBox("Требуется WorkbenchServicePoint для взаимодействия через E.", MessageType.Error);
            return;
        }
        var pointData = new SerializedObject(point);
        var assigned = pointData.FindProperty("accessCollider").objectReferenceValue as Collider;
        var collider = assigned;
        if (collider == null)
            foreach (var candidate in bench.GetComponentsInChildren<Collider>())
                if (!candidate.isTrigger) { collider = candidate; break; }
        bool valid = collider != null && collider.enabled && !collider.isTrigger
            && (EditorUtility.IsPersistent(bench) || collider.gameObject.activeInHierarchy) && collider.transform.IsChildOf(bench.transform);
        if (!valid)
        {
            EditorGUILayout.HelpBox("Нет пригодного физического коллайдера доступа. Нужен включённый Collider на этом объекте или его дочерней модели, без Is Trigger. Существующая неверная явная ссылка имеет приоритет над автоматическим поиском.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("Создать коллайдер доступа по модели"))
                    AddAccessCollider(bench);
        }
        else if (bench.Policy != null)
            EditorGUILayout.HelpBox("Готов к общей сессии: подойти, посмотреть на модель и нажать E. Дистанция, коллайдер и слои препятствий настраиваются в WorkbenchServicePoint. Окно подключается автоматически.", MessageType.Info);
        if (GUILayout.Button("Показать настройки доступа"))
        { Selection.activeGameObject = point.gameObject; EditorGUIUtility.PingObject(point); }
    }

    // Explicit Undoable author action; original model colliders and pose are preserved.
    public static BoxCollider AddAccessCollider(RepairWorkbench bench)
    {
        if (bench == null || Application.isPlaying) return null;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Создать доступ к верстаку");
        var box = Undo.AddComponent<BoxCollider>(bench.gameObject);
        Bounds bounds = default;
        bool any = false;
        foreach (var renderer in bench.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            var world = renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = world.center + Vector3.Scale(world.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = bench.transform.InverseTransformPoint(corner);
                if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                else bounds.Encapsulate(local);
            }
        }
        box.center = any ? bounds.center : new Vector3(0, .5f, 0);
        var size = any ? bounds.size : Vector3.one;
        box.size = Vector3.Max(size, Vector3.one * .05f);
        var point = bench.GetComponent<WorkbenchServicePoint>();
        var data = new SerializedObject(point);
        data.FindProperty("accessCollider").objectReferenceValue = box;
        data.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        Undo.CollapseUndoOperations(group);
        return box;
    }
}
