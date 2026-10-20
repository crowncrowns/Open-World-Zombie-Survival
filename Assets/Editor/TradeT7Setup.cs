using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Persistence;
using ZombieSurvival.Services;

public static class TradeT7Setup
{
    public const string PrefabPath = "Assets/CharacterTesting/Trading/T7EquipmentTrader.prefab";
    private const string ScenePath = "Assets/CharacterTesting/CharacterTest.unity";
    private const string GroupName = "T7 - Торговец снаряжением";

    private static void SetId(SaveIdentity id, string value)
    {
        var data = new SerializedObject(id);
        data.FindProperty("persistentId").stringValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Character Lab/Create T7 Equipment Trader")]
    public static void CreateAsset()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Создавайте префаб вне Play.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(TraderT5Setup.PrefabPath);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bubba_The_Handyman/URP/Prefabs/Handyman_ver_3.prefab");
        if (source == null || model == null) throw new InvalidOperationException("Не найден исходный торговец или URP-модель Handyman3.");
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var npc = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            npc.name = "T7EquipmentTrader";
            var trader = npc.GetComponent<Trader>();
            var profile = trader.Stocks.Single(row => row.profile.Id == "equipment").profile;
            if (!trader.TrySelectProfile(profile)) throw new InvalidOperationException("Нет профиля снаряжения.");
            var appearance = npc.GetComponent<ServiceVisual>();
            var pose = new SerializedObject(appearance);
            pose.FindProperty("localPosition").vector3Value = Vector3.zero;
            pose.FindProperty("localEulerAngles").vector3Value = Vector3.zero;
            pose.FindProperty("localScale").vector3Value = Vector3.one;
            pose.FindProperty("materialOverride").objectReferenceValue = null;
            pose.ApplyModifiedPropertiesWithoutUndo();
            if (!appearance.SetModel(model)) throw new InvalidOperationException(appearance.ValidationMessage);
            var point = new SerializedObject(npc.GetComponent<TraderServicePoint>());
            point.FindProperty("displayName").stringValue = "Торговец снаряжением";
            point.ApplyModifiedPropertiesWithoutUndo();
            SetId(npc.GetComponent<SaveIdentity>(), "t7-equipment-authoring");
            foreach (var row in trader.Stocks) SetId(row.storage.GetComponent<SaveIdentity>(), "t7-equipment-stock-" + row.profile.Id);
            // SaveAsPrefabAsset retains the base link: this is a reusable variant, not another trader implementation.
            if (PrefabUtility.SaveAsPrefabAsset(npc, PrefabPath) == null) throw new InvalidOperationException("Не удалось сохранить вариант торговца.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    [MenuItem("Tools/Character Lab/Set Up T7 Traders (No Save)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Добавляйте торговца вне Play.");
        var scene = Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Select(UnityEngine.SceneManagement.SceneManager.GetSceneAt).Single(s => s.path == ScenePath);
        if (!scene.isLoaded) throw new InvalidOperationException("Сначала откройте CharacterTest.");
        ServiceSessionsT4Setup.EnsurePlayerPrefab();
        ServiceSessionsT4Setup.EnsureScenePlayers(scene);
        var registry = UnityEngine.Object.FindObjectsByType<WorldSaveRegistry>(FindObjectsInactive.Include).Single(r => r.gameObject.scene == scene);
        var group = scene.GetRootGameObjects().FirstOrDefault(r => r.name == GroupName);
        if (group == null)
        {
            Physics.SyncTransforms();
            if (Physics.OverlapBox(new Vector3(16, 1.05f, 19), new Vector3(.8f, 1, .8f)).Any(c => c.gameObject.scene == scene))
                throw new InvalidOperationException("Точка третьего торговца X16/Z19 занята. Ничего не переставлено.");
            CreateAsset();
            group = new GameObject(GroupName);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(group, scene);
            Undo.RegisterCreatedObjectUndo(group, "Добавить третьего торговца");
            var npc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), group.transform);
            npc.name = "Торговец снаряжением";
            npc.transform.SetPositionAndRotation(new Vector3(16, 0, 19), Quaternion.Euler(0, 180, 0));
            foreach (var identity in npc.GetComponentsInChildren<SaveIdentity>(true)) SetId(identity, "t7-" + Guid.NewGuid().ToString("N"));
            var sign = new GameObject("Т7 / Инструкция"); sign.transform.SetParent(group.transform, false);
            var text = sign.AddComponent<TextMesh>(); text.text = Caption(npc.GetComponent<Trader>());
            WorldSpacePresentationSetup.ConfigurePlaque(text);
            sign.GetComponentInChildren<Canvas>().transform.SetPositionAndRotation(new Vector3(16, .035f, 17.2f), Quaternion.Euler(90, 180, 0));
        }
        var target = group.GetComponentInChildren<WorldSaveObject>(true);
        if (target == null) throw new InvalidOperationException("В существующей группе отсутствует торговец. Не создаётся второй NPC.");
        var config = new SerializedObject(registry); var objects = config.FindProperty("sceneObjects");
        if (!Enumerable.Range(0, objects.arraySize).Any(i => objects.GetArrayElementAtIndex(i).objectReferenceValue == target))
        {
            Undo.RecordObject(registry, "Зарегистрировать третьего торговца");
            int index = objects.arraySize++;
            objects.GetArrayElementAtIndex(index).objectReferenceValue = target;
            config.ApplyModifiedProperties();
        }
        // Update only these existing instruction texts, preserving the user's NPC appearance, pose and specialization.
        var oldGroup = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "T5 - Профили торговцев");
        if (oldGroup != null) for (int i = 1; i <= 2; i++)
        {
            var npc = oldGroup.Find("T5 Trader " + i);
            var sign = oldGroup.GetComponentsInChildren<TextMesh>(true).FirstOrDefault(t => t.name == "Т5 / Инструкция " + i);
            if (npc != null && sign != null) UpdateCaption(sign.gameObject, Caption(npc.GetComponent<Trader>()));
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Т7: три специализации, третий NPC X16/Z19. Прежние модели, параметры, ID и бюджеты сохранены; сцена не записывалась.");
    }
    private static string Caption(Trader trader) => "ТОРГОВЛЯ / " + trader.Profile.DisplayName
        + "\nE — открыть | Escape — закрыть\nВыберите вещь и количество\nПокупка / продажа за рубли";
    private static void UpdateCaption(GameObject sign, string caption)
    {
        var source = sign.GetComponent<TextMesh>();
        if (source != null && source.text != caption) { Undo.RecordObject(source, "Обновить инструкцию торговли"); source.text = caption; EditorUtility.SetDirty(source); }
        foreach (var label in sign.GetComponentsInChildren<TMPro.TMP_Text>(true))
            if (label.text != caption) { Undo.RecordObject(label, "Обновить инструкцию торговли"); label.text = caption; EditorUtility.SetDirty(label); }
    }
}
