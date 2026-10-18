using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Services;

public static class ServiceSessionsT4Setup
{
    private const string PlayerPath = "Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
    private const string RootName = "T4 - Сессии сервисов";
    [MenuItem("Tools/Character Lab/Set Up T4 Service Sessions")]
    public static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/CharacterTesting/CharacterTest.unity")
            throw new InvalidOperationException("Откройте CharacterTest в Edit Mode. Другие сцены setup не изменяет.");
        var player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            if (player.GetComponent<PlayerServiceSession>() == null) player.AddComponent<PlayerServiceSession>();
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        if (GameObject.Find(RootName) == null)
        {
            var root = new GameObject(RootName);
            CreateStation(root.transform, false, new Vector3(28, 0, -29));
            CreateStation(root.transform, true, new Vector3(23, 0, -29));
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        // Only assets created/edited by this setup are saved; unrelated dirty user assets are not flushed.
        Debug.Log("Т4: общий PlayerServiceSession и две тестовые станции готовы. Каталоги, сделки и ремонт не добавлены.");
    }
    private static void CreateStation(Transform parent, bool workbench, Vector3 position)
    {
        string path = "Assets/CharacterTesting/Prefabs/" + (workbench ? "T4WorkbenchSession.prefab" : "T4TraderSession.prefab");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            var wrapper = new GameObject(workbench ? "Т4 / Тест сессии верстака" : "Т4 / Тест торговой сессии");
            try
            {
                string modelPath = workbench ? "Assets/Polygon-Lite Survival Collection/Prefabs/SM_Workbench.prefab"
                    : "Assets/CharacterTesting/Prefabs/T4TraderTableVisual.prefab";
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), wrapper.transform);
                model.name = "Заменяемая модель";
                // Imported models use legacy shaders; the authored wrapper owns URP overrides.
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/CharacterTesting/Materials/Concrete.mat");
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
                ServicePoint point;
                if (workbench) point = wrapper.AddComponent<WorkbenchServicePoint>();
                else
                {
                    model.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    var box = wrapper.AddComponent<BoxCollider>(); box.center = new Vector3(0, .45f, 0); box.size = new Vector3(2.64f, .9f, 1.37f);
                    point = wrapper.AddComponent<TraderServicePoint>();
                }
                var so = new SerializedObject(point);
                so.FindProperty("displayName").stringValue = wrapper.name;
                so.FindProperty("accessCollider").objectReferenceValue = wrapper.GetComponentInChildren<Collider>();
                so.ApplyModifiedPropertiesWithoutUndo();
                if (!workbench)
                {
                    var anchor = new GameObject("Service visual (managed)"); anchor.transform.SetParent(wrapper.transform, false);
                    model.transform.SetParent(anchor.transform, false);
                    var visual = wrapper.AddComponent<ServiceVisual>(); var settings = new SerializedObject(visual);
                    settings.FindProperty("visualRoot").objectReferenceValue = anchor.transform;
                    settings.FindProperty("instance").objectReferenceValue = model;
                    settings.FindProperty("appliedPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    settings.FindProperty("visualPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    settings.FindProperty("rootCollider").objectReferenceValue = wrapper.GetComponent<BoxCollider>();
                    settings.ApplyModifiedPropertiesWithoutUndo(); visual.ApplyModel();
                }
                asset = PrefabUtility.SaveAsPrefabAsset(wrapper, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(wrapper); }
        }
        var station = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent); station.transform.position = position;
        var sign = new GameObject("Инструкция Т4: " + (workbench ? "верстак" : "торговая сессия"));
        sign.transform.SetParent(parent, false); sign.transform.position = position + new Vector3(0, 2, .6f);
        var source = sign.AddComponent<TextMesh>();
        source.text = "Т4 / " + (workbench ? "СЕССИЯ ВЕРСТАКА" : "ТОРГОВАЯ СЕССИЯ")
            + "\nE: открыть | Escape: закрыть\nПроверка доступа, без сделок и ремонта\nДистанция и стены закрывают сессию";
        WorldSpacePresentationSetup.ConfigurePlaque(source);
        sign.GetComponentInChildren<Canvas>().transform.SetPositionAndRotation(position + new Vector3(0, .035f, 1.6f), Quaternion.Euler(90, 180, 0));
    }
}
