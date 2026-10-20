using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Persistence;
using ZombieSurvival.Services;

public static class TradeT7Checks
{
    [MenuItem("Tools/Character Lab/Check T7 Trading Scene")]
    private static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Проверка авторской сцены выполняется вне Play.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/CharacterTesting/CharacterTest.unity");
        if (!scene.isLoaded) throw new InvalidOperationException("Откройте CharacterTest.");
        var results = new List<string>();
        void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Т7: " + message); results.Add("PASS " + message); }
        var traders = UnityEngine.Object.FindObjectsByType<Trader>(FindObjectsInactive.Include).Where(t => t.gameObject.scene == scene).ToArray();
        Check(traders.Length >= 3, "в сцене не менее трёх торговцев");
        Check(new[] { "weapons_ammo", "equipment", "food_medicine" }.All(id => traders.Any(t => t.Profile != null && t.Profile.Id == id)), "представлены все три специализации");
        var registry = UnityEngine.Object.FindObjectsByType<WorldSaveRegistry>(FindObjectsInactive.Include).Single(r => r.gameObject.scene == scene);
        var roots = new SerializedObject(registry).FindProperty("sceneObjects");
        foreach (var trader in traders)
        {
            trader.ValidateConfiguration();
            Check(trader.GetComponents<TraderServicePoint>().Length == 1 && trader.GetComponents<ServiceVisual>().Length == 1, trader.name + ": одна точка доступа и заменяемая модель");
            Check(trader.GetComponent<WorldTraderSaveAdapter>() != null && trader.GetComponent<WorldWalletSaveAdapter>() != null
                && Enumerable.Range(0, roots.arraySize).Any(i => roots.GetArrayElementAtIndex(i).objectReferenceValue == trader.GetComponent<WorldSaveObject>()), trader.name + ": зарегистрирован с адаптерами товаров и денег");
            var visual = trader.GetComponent<ServiceVisual>();
            Check(visual.Instance != null && visual.Instance.GetComponentsInChildren<Renderer>(true).Any()
                && visual.Instance.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m != null)), trader.name + ": модель и все материалы назначены");
        }
        var allIdentities = UnityEngine.Object.FindObjectsByType<SaveIdentity>(FindObjectsInactive.Include).Where(id => id.gameObject.scene == scene).ToArray();
        var identities = traders.SelectMany(t => t.GetComponentsInChildren<SaveIdentity>(true)).ToArray();
        Check(identities.All(id => !string.IsNullOrWhiteSpace(id.Id) && allIdentities.Count(other => other.Id == id.Id) == 1), "ID торговцев уникальны и не пересекаются с остальной сценой");
        var variant = AssetDatabase.LoadAssetAtPath<GameObject>(TradeT7Setup.PrefabPath);
        Check(variant != null && PrefabUtility.GetPrefabAssetType(variant) == PrefabAssetType.Variant, "новый торговец — повторно используемый Prefab Variant");
        Check(variant.GetComponent<Trader>().Profile.Id == "equipment", "вариант настроен на снаряжение");
        results.Add(TraderT5Checks.RunAuthoredPlayers(scene));
        return string.Join("\n", results);
    }
}
