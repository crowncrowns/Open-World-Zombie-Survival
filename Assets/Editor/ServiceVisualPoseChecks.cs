using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieSurvival.Services;

public static class ServiceVisualPoseChecks
{
    [MenuItem("Tools/Character Lab/Check Service Visual Placement")]
    private static void RunFromMenu() => Debug.Log(Run());
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Проверка выполняется в Edit Mode, без запуска пользовательских сцен.");
        string temporary = "Assets/CharacterTesting/Prefabs/__ServiceVisualPoseCheck_" + Guid.NewGuid().ToString("N") + ".prefab";
        var root = PrefabUtility.LoadPrefabContents("Assets/CharacterTesting/Prefabs/T4TraderSession.prefab");
        var preview = EditorSceneManager.NewPreviewScene();
        var passed = new List<string>();
        Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception("ServiceVisual pose FAILED: " + name); passed.Add(name); };
        try
        {
            var visual = root.GetComponent<ServiceVisual>();
            var handyman = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bubba_The_Handyman/URP/Prefabs/Handyman_ver_4.prefab");
            visual.SetModel(handyman);
            root.transform.SetPositionAndRotation(new Vector3(7, 2, -3), Quaternion.Euler(0, 35, 0));
            var position = new Vector3(1.2f, .15f, -.8f); var rotation = Quaternion.Euler(0, 180, 0); var scale = new Vector3(.9f, 1.1f, 1.2f);
            visual.Instance.transform.localPosition = position; visual.Instance.transform.localRotation = rotation; visual.Instance.transform.localScale = scale;
            var so = new SerializedObject(visual); so.FindProperty("poseInitialized").boolValue = false;
            so.FindProperty("localPosition").vector3Value = Vector3.zero; so.FindProperty("localEulerAngles").vector3Value = Vector3.zero;
            so.FindProperty("localScale").vector3Value = Vector3.one; so.ApplyModifiedPropertiesWithoutUndo();
            check(visual.ApplyModel() && Pose(visual, position, rotation, scale), "legacy existing instance captures actual Handyman4 yaw180 and offsets first");
            visual.enabled = false; visual.enabled = true;
            check(visual.ApplyModel() && visual.ApplyModel() && Pose(visual, position, rotation, scale), "enable and repeated Apply preserve authored pose");
            so = new SerializedObject(visual); so.FindProperty("localEulerAngles").vector3Value = new Vector3(0, 120, 0); so.ApplyModifiedPropertiesWithoutUndo();
            check(visual.ApplyModel() && Pose(visual, position, Quaternion.Euler(0, 120, 0), scale), "explicit absolute Inspector rotation is applied");
            visual.Instance.transform.localRotation = rotation;
            so = new SerializedObject(visual); so.FindProperty("materialOverride").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/CharacterTesting/Materials/Concrete.mat"); so.ApplyModifiedPropertiesWithoutUndo();
            check(visual.ApplyModel() && Pose(visual, position, rotation, scale), "material replacement captures and retains manual pose");
            var table = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/T4TraderTableVisual.prefab");
            check(visual.SetModel(table) && Pose(visual, position, rotation, scale)
                && root.transform.position == new Vector3(7, 2, -3) && Quaternion.Angle(root.transform.rotation, Quaternion.Euler(0, 35, 0)) < .001f,
                "prefab swap preserves absolute placement and service root");
            var before = visual.Instance;
            so = new SerializedObject(visual); so.FindProperty("localScale").vector3Value = Vector3.zero; so.ApplyModifiedPropertiesWithoutUndo();
            check(!visual.ApplyModel() && !visual.ApplyModel() && visual.Instance == before && Pose(visual, position, rotation, scale), "zero scale and repeated invalid Apply leave instance intact");
            so = new SerializedObject(visual); so.FindProperty("localScale").vector3Value = scale; so.ApplyModifiedPropertiesWithoutUndo();
            check(visual.ApplyModel() && Pose(visual, position, rotation, scale), "valid settings resume without pose reset after refusal");
            PrefabUtility.SaveAsPrefabAsset(root, temporary); PrefabUtility.UnloadPrefabContents(root); root = null;
            root = PrefabUtility.LoadPrefabContents(temporary); visual = root.GetComponent<ServiceVisual>();
            check(visual.ApplyModel() && Pose(visual, position, rotation, scale), "serialized snapshots and pose survive prefab save/reload");
            var legacy = new GameObject("Legacy no-instance check"); SceneManager.MoveGameObjectToScene(legacy, preview);
            var noInstance = legacy.AddComponent<ServiceVisual>(); so = new SerializedObject(noInstance);
            so.FindProperty("poseInitialized").boolValue = false; so.FindProperty("localEulerAngles").vector3Value = new Vector3(0, 30, 0);
            so.FindProperty("localScale").vector3Value = Vector3.one * 2; so.ApplyModifiedPropertiesWithoutUndo();
            var expected = Quaternion.Euler(0, 30, 0) * table.transform.localRotation;
            check(noInstance.SetModel(table) && noInstance.ApplyModel() && Pose(noInstance, Vector3.zero, expected, table.transform.localScale * 2), "legacy no-instance relative settings convert exactly once");
            return string.Join("\n", passed);
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
            EditorSceneManager.ClosePreviewScene(preview);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(temporary) != null) AssetDatabase.DeleteAsset(temporary);
        }
    }
    private static bool Pose(ServiceVisual visual, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var t = visual.Instance == null ? null : visual.Instance.transform;
        return t != null && Vector3.Distance(t.localPosition, position) < .0001f && Quaternion.Angle(t.localRotation, rotation) < .01f
            && Vector3.Distance(t.localScale, scale) < .0001f;
    }
}
