using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Economy;

/// <summary>Explicit test funds, separate from real trade. Does not reset NPCs or balances.</summary>
public static class TradeT6Setup
{
    [MenuItem("Tools/Character Lab/Set Up T6 Trade Test (No Save)")]
    public static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/CharacterTesting/CharacterTest.unity")
            throw new InvalidOperationException("Нужна CharacterTest в Edit Mode.");
        ServiceSessionsT4Setup.EnsurePlayerPrefab();
        ServiceSessionsT4Setup.EnsureScenePlayers(scene);
        const string rootName = "T6 - Проверка торговли";
        foreach (var root in scene.GetRootGameObjects()) if (root.name == rootName) return;
        var group = new GameObject(rootName);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(group, scene);
        Undo.RegisterCreatedObjectUndo(group, "Добавить тест торговли Т6");
        var terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        terminal.name = "Тест: начислить 10 000 руб.";
        terminal.transform.SetParent(group.transform, false);
        terminal.transform.position = new Vector3(20, .45f, 19);
        terminal.transform.localScale = new Vector3(.55f, .9f, .55f);
        terminal.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CharacterTesting/Materials/Concrete.mat");
        var component = terminal.AddComponent<WalletTestTerminal>();
        var serialized = new SerializedObject(component);
        serialized.FindProperty("action").intValue = (int)WalletTestAction.Credit;
        serialized.FindProperty("amount").longValue = 10000;
        serialized.FindProperty("displayName").stringValue = terminal.name;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var sign = new GameObject("Т6 / Напольная инструкция");
        sign.transform.SetParent(group.transform, false);
        var source = sign.AddComponent<TextMesh>();
        source.text = "Т6 / ТЕСТ ТОРГОВЛИ\nE на консоли: +10 000 руб.\nУ NPC: E, выбрать предмет и количество\nБюджет NPC пополняется покупками";
        WorldSpacePresentationSetup.ConfigurePlaque(source);
        sign.GetComponentInChildren<Canvas>().transform.SetPositionAndRotation(new Vector3(20, .035f, 17.2f), Quaternion.Euler(90, 180, 0));
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Т6: явная денежная консоль добавлена. Сцена не сохранялась; существующие деньги и торговцы не сброшены.");
    }
}
