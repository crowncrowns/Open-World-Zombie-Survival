using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Services;

public static class ServiceVisualChecks
{
    [MenuItem("Tools/Character Lab/Check Service Visual Prefabs")]
    private static void RunFromMenu() => Debug.Log(Run());
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Эта проверка работает в Edit Mode без изменения открытой сцены.");
        var root = PrefabUtility.LoadPrefabContents("Assets/CharacterTesting/Prefabs/T4TraderSession.prefab");
        var results = new List<string>();
        Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception("ServiceVisual FAILED: " + name); results.Add(name); };
        try
        {
            var visual = root.GetComponent<ServiceVisual>(); var point = root.GetComponent<TraderServicePoint>();
            var collider = root.GetComponent<BoxCollider>(); var originalPoint = point; var originalCollider = collider;
            var original = visual.VisualPrefab;
            var character = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bubba_The_Handyman/URP/Prefabs/Handyman_ver_1.prefab");
            check(character != null && visual.SetModel(character), "ready URP Handyman prefab accepted");
            check(visual.Instance != null && visual.Instance.transform.parent.childCount == 1, "one managed instance");
            check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual.Instance) == AssetDatabase.GetAssetPath(character), "nested prefab connection retained");
            check(collider.size.y > 1f && collider.size.y < 4f, "root access collider fits character height");
            check(Array.TrueForAll(visual.Instance.GetComponentsInChildren<Collider>(true), c => !c.enabled), "model colliders disabled; root remains physical");
            var animator = visual.Instance.GetComponentInChildren<Animator>(); var sourceAnimator = character.GetComponentInChildren<Animator>();
            check(animator != null && !animator.applyRootMotion && animator.avatar == sourceAnimator.avatar
                && animator.runtimeAnimatorController == sourceAnimator.runtimeAnimatorController, "avatar/controller retained, root motion off");
            var previousInstance = visual.Instance;
            check(visual.SetModel(character) && visual.Instance == previousInstance, "same assignment does not recreate instance");
            check(!visual.SetModel(root) && visual.Instance == previousInstance, "scene object rejected before old visual removal");
            check(!visual.SetModel(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/T4TraderSession.prefab"))
                && visual.Instance == previousInstance, "recursive service prefab rejected before creation");
            check(!visual.SetModel(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Enemies/ZombieMaleActor.prefab"))
                && visual.Instance == previousInstance, "script-rich NPC rejected before Awake/OnEnable");
            check(visual.SetModel(null) && visual.Instance == null && collider.enabled, "None clears visual only, root collider remains");
            check(visual.SetModel(original) && visual.Instance != null && visual.Instance.transform.parent.childCount == 1 && collider.size.y < 1.1f,
                "A-B-A restores table and collider without duplicates");
            check(point == originalPoint && collider == originalCollider, "root session point and collider identities unchanged");
            return string.Join("\n", results);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
