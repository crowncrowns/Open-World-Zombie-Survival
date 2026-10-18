using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Services;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;

public static class TraderT5Checks
{
    private static void Require(bool value, string label, List<string> results)
    { if (!value) throw new InvalidOperationException("Т5: " + label); results.Add("PASS " + label); }
    [MenuItem("Tools/Character Lab/Check T5 Trader Assets")]
    private static void Menu() => Debug.Log(RunEdit());
    public static string RunEdit()
    {
        var results = new List<string>();
        var root = PrefabUtility.LoadPrefabContents(TraderT5Setup.PrefabPath);
        try
        {
            var trader = root.GetComponent<Trader>(); trader.ValidateConfiguration();
            Require(trader.Stocks.Count == 3 && trader.Stocks.All(s => Enumerable.Range(0, s.storage.SlotCount).All(i => s.storage.GetSlot(i) == null || s.storage.GetSlot(i).IsEmpty)), "3 пустых авторских хранилища без запечённых экземпляров", results);
            Require(root.GetComponents<ZombieSurvival.Interaction.Interactable>().Length == 1 && root.GetComponents<TraderServicePoint>().Length == 1, "одна точка E", results);
            Require(root.GetComponent<WorldTraderSaveAdapter>() != null && root.GetComponent<WorldWalletSaveAdapter>() != null, "единые адаптеры мира и бюджета", results);
            Require(TraderProfile.TryScalePrice(long.MaxValue, 100, out var exact) && exact == long.MaxValue && !TraderProfile.TryScalePrice(long.MaxValue, 101, out _), "long overflow и точный 100%", results);
            Require(!TraderProfile.TryScalePrice(-1, 100, out _) && !TraderProfile.TryScalePrice(10, -1, out _), "отрицательные цены отклонены", results);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return string.Join("\n", results);
    }
    public static string RunPlay()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play checks запускаются только в отдельной тестовой копии проекта.");
        var results = new List<string>();
        var parent = new GameObject("T5 isolated checks"); parent.SetActive(false);
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(TraderT5Setup.PrefabPath);
            var a = UnityEngine.Object.Instantiate(source, parent.transform).GetComponent<Trader>();
            var b = UnityEngine.Object.Instantiate(source, parent.transform).GetComponent<Trader>();
            a.InitializeStock(); b.InitializeStock();
            parent.SetActive(true);
            var weapons = a.Stocks.Single(s => s.profile.Id == "weapons_ammo"); var gear = a.Stocks.Single(s => s.profile.Id == "equipment");
            var food = a.Stocks.Single(s => s.profile.Id == "food_medicine");
            var budget = a.GetComponent<ZombieSurvival.Economy.TraderBudget>(); budget.Funds.TryCredit(700);
            a.TrySelectProfile(weapons.profile); var pistol = weapons.storage.GetSlot(0); string pistolId = pistol.InstanceId;
            weapons.storage.TrySetDurability(0, 47); weapons.storage.Reload(pistol);
            int rounds = pistol.LoadedRounds;
            var recipient = new GameObject("T5 transfer destination"); recipient.transform.SetParent(parent.transform); var bag = recipient.AddComponent<ItemStorage>();
            Require(weapons.storage.TransferTo(bag, 0) == 1 && ReferenceEquals(bag.GetSlot(0), pistol), "извлечение исходного экземпляра без Copy", results);
            a.TrySelectProfile(gear.profile); a.TrySelectProfile(weapons.profile);
            Require(!weapons.storage.Contains(pistol) && bag.GetSlot(0).InstanceId == pistolId && budget.Funds.Balance == 700, "A→B→A не пополняет запас и бюджет", results);
            Require(b.Stocks.Single(s => s.profile == weapons.profile).storage.GetSlot(0).InstanceId != pistolId, "два NPC имеют разные InstanceId", results);
            Require(bag.TransferTo(weapons.storage, 0) == 1 && weapons.storage.Contains(pistol) && pistol.Durability == 47 && pistol.LoadedRounds == rounds, "возврат реального экземпляра сохраняет ID/прочность/магазин", results);
            a.TrySelectProfile(food.profile); a.TrySelectProfile(weapons.profile);
            Require(weapons.storage.Contains(pistol), "вернувшийся экземпляр переживает смену профиля", results);
            var gearItem = gear.storage.GetSlot(0); gear.storage.TrySetDurability(0, 35); gear.storage.TrySetDurability(1, 80);
            Require(gear.storage.GetSlot(0).Durability == 35 && gear.storage.GetSlot(1).Durability == 80 && gear.storage.GetSlot(0).InstanceId != gear.storage.GetSlot(1).InstanceId, "одинаковый тип с разной прочностью", results);
            var adapter = a.GetComponent<WorldTraderSaveAdapter>(); string saved = adapter.Capture(); adapter.Validate(saved, new SavedWorldObject { id = "test" });
            a.TrySelectProfile(food.profile); adapter.ApplySilent(saved); adapter.Publish();
            Require(a.Profile == weapons.profile && weapons.storage.Contains(pistol), "metadata restore не создаёт и не заменяет товары", results);
            bool refused = false; try { adapter.Validate(saved.Replace("\"initialized\":true", "\"initialized\":false"), new SavedWorldObject { id = "test" }); } catch (InvalidOperationException) { refused = true; }
            Require(refused && weapons.storage.Contains(pistol), "malformed initialized отказ без изменения слотов", results);
            parent.SetActive(true);
            long version = a.CatalogVersion; weapons.storage.TrySetDurability(1, 60);
            Require(!a.ValidateSelection(a.Profile, version, out _), "изменение stock инвалидирует selection", results);
            version = a.CatalogVersion; adapter.ApplySilent(saved); adapter.Publish();
            Require(!a.ValidateSelection(a.Profile, version, out _), "Publish инвалидирует прежний token", results);
            version = a.CatalogVersion; a.enabled = false;
            Require(!a.ValidateSelection(a.Profile, version, out _), "disabled Trader запрещает выбор", results);
            a.enabled = true; a.InitializeStock();
            Require(weapons.storage.Contains(pistol) && gear.storage.GetSlot(0).Durability == 35, "повторный enable/Initialize не сбрасывает товары", results);
        }
        finally { UnityEngine.Object.Destroy(parent); }
        return string.Join("\n", results);
    }
    public static string RunWorld(Trader authored)
    {
        var results = new List<string>();
        var registryRoot = new GameObject("T5 isolated world registry");
        var registry = registryRoot.AddComponent<WorldSaveRegistry>();
        var type = ScriptableObject.CreateInstance<WorldPrefabDefinition>();
        var definition = new SerializedObject(type); definition.FindProperty("prefabId").stringValue = "t5-trader-test";
        definition.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(TraderT5Setup.PrefabPath); definition.ApplyModifiedPropertiesWithoutUndo();
        var config = new SerializedObject(registry); config.FindProperty("sceneObjects").arraySize = 1;
        config.FindProperty("sceneObjects").GetArrayElementAtIndex(0).objectReferenceValue = authored.GetComponent<WorldSaveObject>();
        config.FindProperty("prefabCatalog").arraySize = 1; config.FindProperty("prefabCatalog").GetArrayElementAtIndex(0).objectReferenceValue = type; config.ApplyModifiedPropertiesWithoutUndo();
        var created = registry.Spawn(type, new Vector3(5,0,0), Quaternion.identity).GetComponent<Trader>();
        Require(created.Stocks.All(r => Enumerable.Range(0,r.storage.SlotCount).Any(i => r.storage.GetSlot(i) != null && !r.storage.GetSlot(i).IsEmpty)), "штатный Spawn выдаёт запас до первого Capture", results);
        var state = registry.CaptureState(); var before = created.Profile;
        created.TrySelectProfile(created.Stocks.First(r => r.profile != before).profile);
        created.GetComponent<ZombieSurvival.Economy.TraderBudget>().Funds.TryCredit(100);
        using (var restore = registry.PrepareRestore(state)) { restore.CommitSilent(); restore.Publish(); }
        Require(created.Profile == before && created.GetComponent<ZombieSurvival.Economy.TraderBudget>().Funds.Balance == 0, "общий WorldRestorePlan восстанавливает профиль и бюджет", results);
        return string.Join("\n", results);
    }
}
