using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;

/// <summary>Идемпотентное подключение CM-05 к авторскому игроку. Не переписывает выбранные автором настройки типов/слотов.</summary>
public static class SaveSlotsSetup
{
    [MenuItem("Tools/Character Lab/Set Up CM05 Save Slots")]
    public static void Run()
    {
        if(Application.isPlaying || EditorSceneManager.GetActiveScene().path!="Assets/CharacterTesting/CharacterTest.unity")throw new InvalidOperationException("Откройте CharacterTest вне Play.");
        const string path="Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            if(root.GetComponent<PlayerSaveSlots>()==null)root.AddComponent<PlayerSaveSlots>();
            if(root.GetComponent<PlayerSaveMenu>()==null)root.AddComponent<PlayerSaveMenu>();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        foreach(var motor in UnityEngine.Object.FindObjectsByType<FirstPersonController>())
        {
            if(motor.gameObject.scene!=EditorSceneManager.GetActiveScene())continue;
            if(motor.GetComponent<PlayerSaveSlots>()==null)motor.gameObject.AddComponent<PlayerSaveSlots>();
            if(motor.GetComponent<PlayerSaveMenu>()==null)motor.gameObject.AddComponent<PlayerSaveMenu>();
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        SaveSlotsValidation.Run();Debug.Log("CM05_SETUP_OK");
    }
}
