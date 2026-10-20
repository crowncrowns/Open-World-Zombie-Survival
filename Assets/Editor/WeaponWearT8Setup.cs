using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieSurvival.Weapons;

/// <summary>Дополняет существующий оружейный полигон, не открывает и не сохраняет сцены.</summary>
public static class WeaponWearT8Setup
{
    public const string GroupName = "T8 - Износ оружия";
    [MenuItem("Tools/Character Lab/Set Up T8 Weapon Wear (No Save)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Т8 настраивается вне Play.");
        var scene = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
            .Single(s => s.path == "Assets/CharacterTesting/CharacterTest.unity");
        if (scene.GetRootGameObjects().Any(r => r.name == GroupName)) return;
        var itemPaths = new[] { "ArmyPistolItem", "CampingAxeItem", "Ammo9mmItem" };
        var items = itemPaths.Select(name => AssetDatabase.LoadAssetAtPath<ZombieSurvival.Inventory.ItemDefinition>("Assets/Weapons/Definitions/" + name + ".asset")).ToArray();
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Weapons/RangeFurniture.mat");
        if (items.Any(i => i == null) || material == null) throw new InvalidOperationException("Т8 требует существующие предметы и материал оружейного полигона.");
        Physics.SyncTransforms();
        for (int i = 0; i < 3; i++)
            if (Physics.OverlapBox(new Vector3(26 + i * 2, 1, -25), new Vector3(.35f, .8f, .35f)).Any(c => c.gameObject.scene == scene))
                throw new InvalidOperationException("Место Т8 X26/28/30,Z−25 занято; объекты не переставлены.");
        int groupId = Undo.GetCurrentGroup();
        var root = new GameObject(GroupName); SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Добавить тест износа Т8");
        for (int i = 0; i < 3; i++)
        {
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = i == 0 ? "Т8 - Тестовый пистолет" : i == 1 ? "Т8 - Тестовый топор" : "Т8 - Патроны";
            button.transform.SetParent(root.transform, false); button.transform.position = new Vector3(26 + i * 2, .65f, -25);
            button.transform.localScale = new Vector3(.5f, 1.3f, .5f); button.GetComponent<Renderer>().sharedMaterial = material;
            var console = button.AddComponent<WeaponWearTestConsole>(); var data = new SerializedObject(console);
            data.FindProperty("item").objectReferenceValue = items[i]; data.FindProperty("quantity").intValue = 12;
            data.FindProperty("initialDurability").floatValue = 1; data.FindProperty("displayName").stringValue = button.name;
            data.ApplyModifiedPropertiesWithoutUndo();
            var sign = new GameObject("Т8 / Инструкция " + i); sign.transform.SetParent(root.transform, false);
            var text = sign.AddComponent<TextMesh>();
            text.text = i == 0 ? "Т8 / ПИСТОЛЕТ\nE — новый тестовый экземпляр\nПрочность 1: два выстрела\nTab — экипировать | R — зарядить"
                : i == 1 ? "Т8 / ТОПОР\nE — новый тестовый экземпляр\nПрочность 1: один удар\nСломанная вещь остаётся в рюкзаке"
                : "Т8 / ПАТРОНЫ\nE — выдать 12 патронов\nИзнос: выстрел или готовый удар\nОтменённый замах не изнашивает";
            WorldSpacePresentationSetup.ConfigurePlaque(text);
            sign.GetComponentInChildren<Canvas>().transform.SetPositionAndRotation(new Vector3(26 + i * 2, .035f, -26.8f), Quaternion.Euler(90, 0, 0));
        }
        Undo.CollapseUndoOperations(groupId); EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Т8: три тестовые E-консоли X26/28/30,Z−25. Новые повреждённые экземпляры, не ремонт. Сцена не сохранена.");
    }
}
