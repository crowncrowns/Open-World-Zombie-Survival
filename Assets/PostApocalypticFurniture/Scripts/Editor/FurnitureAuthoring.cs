using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Furniture;
using ZombieSurvival.Persistence;

namespace ZombieSurvival.Furniture.Editor
{
    /// <summary>Explicit authoring commands. They never save a scene, change original IDs or bypass registry validation.</summary>
    public static class FurnitureAuthoring
    {
        [MenuItem("Tools/Environment/Furniture/Зарегистрировать выбранную мебель в сохранении мира")]
        public static void RegisterSelected()
        {
            var selected = Selected();
            if (selected.Length == 0) throw new InvalidOperationException("Выберите экземпляры мебели в обычной открытой сцене вне Play.");
            var registries = UnityEngine.Object.FindObjectsByType<WorldSaveRegistry>(FindObjectsInactive.Include);
            var allIds = UnityEngine.Object.FindObjectsByType<SaveIdentity>(FindObjectsInactive.Include);
            var plan = new Dictionary<FurnitureAssembly, WorldSaveRegistry>();
            // Complete the selection preflight before assigning even one ID.
            foreach (var assembly in selected)
            {
                var scene = assembly.gameObject.scene;
                var local = registries.Where(r => Eligible(r.gameObject) && r.gameObject.scene == scene).ToArray();
                if (local.Length != 1) throw new InvalidOperationException("Для «" + scene.name + "» нужен ровно один существующий WorldSaveRegistry.");
                if (assembly.transform.parent != null && assembly.transform.parent.GetComponentInParent<WorldSaveObject>(true) != null)
                    throw new InvalidOperationException("Мебель не должна быть вложена в другой WorldSaveObject: " + assembly.name);
                var identity = assembly.GetComponent<SaveIdentity>();
                if (identity != null && !string.IsNullOrWhiteSpace(identity.Id)
                    && allIds.Any(other => other != identity && Eligible(other.gameObject) && other.gameObject.scene == scene && other.Id == identity.Id))
                    throw new InvalidOperationException("Повтор существующего ID у «" + assembly.name + "». Используйте Tools/Zombie Survival/Save IDs и явно выберите оригинал.");
                var serialized = new SerializedObject(local[0]); var list = serialized.FindProperty("sceneObjects");
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    throw new InvalidOperationException("В WorldSaveRegistry есть отсутствующая ссылка. Исправьте состав реестра до добавления мебели.");
                plan.Add(assembly, local[0]);
            }
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Зарегистрировать выбранную мебель");
            foreach (var pair in plan)
            {
                var assembly = pair.Key;
                if (assembly.GetComponent<FurnitureWorldSaveAdapter>() == null) Undo.AddComponent<FurnitureWorldSaveAdapter>(assembly.gameObject);
                var identity = assembly.GetComponent<SaveIdentity>();
                if (string.IsNullOrWhiteSpace(identity.Id))
                {
                    Undo.RecordObject(identity, "Назначить ID мебели");
                    var id = new SerializedObject(identity); id.FindProperty("persistentId").stringValue = "scene-" + Guid.NewGuid().ToString("N");
                    id.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(identity); EditorUtility.SetDirty(identity);
                }
                var root = assembly.GetComponent<WorldSaveObject>(); var registry = pair.Value;
                var serialized = new SerializedObject(registry); var list = serialized.FindProperty("sceneObjects");
                bool listed = Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == root);
                if (!listed)
                {
                    Undo.RecordObject(registry, "Добавить мебель в реестр");
                    int index = list.arraySize; list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = root;
                    serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(registry); EditorUtility.SetDirty(registry);
                }
                EditorSceneManager.MarkSceneDirty(assembly.gameObject.scene);
            }
            Undo.CollapseUndoOperations(undo); CaptureExistingIdentityBaseline();
            Debug.Log("Зарегистрировано предметов мебели: " + selected.Length + ". Сцены помечены изменёнными; сохраните их обычным способом. После изменения состава области нужен совместимый новый снимок мира.");
        }

        [MenuItem("Tools/Environment/Furniture/Варьировать износ выбранной мебели")]
        public static void VarySelectedWear()
        {
            var selected = Selected();
            if (selected.Length == 0) throw new InvalidOperationException("Выберите экземпляры мебели в обычной сцене вне Play.");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Варьировать износ мебели");
            foreach (var assembly in selected)
            {
                var wear = assembly.GetComponent<FurnitureWear>();
                if (wear == null) continue;
                Undo.RecordObject(wear, "Варьировать износ мебели");
                string identity = assembly.GetComponent<SaveIdentity>()?.Id;
                if (string.IsNullOrWhiteSpace(identity)) identity = GlobalObjectId.GetGlobalObjectIdSlow(assembly).ToString();
                int seed = StableHash(identity + "|" + wear.Seed + "|furniture-wear");
                uint stream = unchecked((uint)seed);
                float dirt = Mathf.Lerp(.12f, .85f, Next(ref stream));
                float fade = Mathf.Lerp(.08f, .7f, Next(ref stream));
                float damage = Mathf.Lerp(.05f, .9f, Next(ref stream));
                Color color = Color.Lerp(new Color(.45f,.40f,.31f), new Color(.70f,.64f,.52f), Next(ref stream));
                wear.SetWear(seed, dirt, fade, damage, color);
                PrefabUtility.RecordPrefabInstancePropertyModifications(wear); EditorUtility.SetDirty(wear);
                EditorSceneManager.MarkSceneDirty(assembly.gameObject.scene);
            }
            Undo.CollapseUndoOperations(undo);
        }
        static bool Eligible(GameObject value) => value != null && !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorUtility.IsPersistent(value) && value.scene.IsValid() && value.scene.isLoaded
            && !EditorSceneManager.IsPreviewScene(value.scene) && PrefabStageUtility.GetPrefabStage(value) == null;
        static FurnitureAssembly[] Selected()
        {
            var values = new HashSet<FurnitureAssembly>();
            foreach (var selection in Selection.gameObjects)
            {
                var parent = selection.GetComponentInParent<FurnitureAssembly>(true);
                if (parent != null && Eligible(parent.gameObject)) values.Add(parent);
                foreach (var child in selection.GetComponentsInChildren<FurnitureAssembly>(true)) if (Eligible(child.gameObject)) values.Add(child);
            }
            return values.ToArray();
        }
        static int StableHash(string text)
        { unchecked { uint value = 2166136261u; foreach (char c in text) { value ^= c; value *= 16777619u; } return (int)value; } }
        static float Next(ref uint value)
        { value = unchecked(value * 1664525u + 1013904223u); return (value >> 8) / 16777215f; }
        static void CaptureExistingIdentityBaseline()
        {
            var type = Type.GetType("SaveIdentityAuthoring, Assembly-CSharp-Editor", false);
            type?.GetMethod("CaptureBaseline", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
    }

    [CustomEditor(typeof(FurnitureWear))]
    public sealed class FurnitureWearInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Обновить маски износа")) ((FurnitureWear)target).Apply();
            EditorGUILayout.HelpBox("Износ хранится на экземпляре через MaterialPropertyBlock. Все LOD используют те же PBR-материалы. Команда Tools/Environment/Furniture варьирует выбранные экземпляры и поддерживает Undo.", MessageType.Info);
        }
    }
}
