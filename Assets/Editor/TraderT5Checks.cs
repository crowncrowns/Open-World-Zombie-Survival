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
        var playerSource = PrefabUtility.LoadPrefabContents("Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab");
        try { Require(playerSource.GetComponents<PlayerServiceSession>().Length == 1 && playerSource.GetComponent<PlayerServiceSession>().enabled, "исходный игрок содержит одну включённую сессию сервиса", results); }
        finally { PrefabUtility.UnloadPrefabContents(playerSource); }
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
            var ammo = weapons.profile.Catalog.Single(e => e.item.Category == ItemCategory.Ammunition).item;
            bag.TryAddAll(ammo, bag.FreeCapacity(ammo));
            int ammoIndex = Enumerable.Range(0, weapons.storage.SlotCount).First(i => weapons.storage.GetSlot(i)?.Definition == ammo);
            int total = weapons.storage.Count(ammo) + bag.Count(ammo);
            Require(weapons.storage.TransferTo(bag, ammoIndex, 10) == 0 && total == weapons.storage.Count(ammo) + bag.Count(ammo), "полный запас отказывает без потери ресурсов", results);
            bag.TryExtract(0, 1, out var loose);
            Require(weapons.storage.TransferTo(bag, ammoIndex, 10) == 1 && total == weapons.storage.Count(ammo) + bag.Count(ammo) + loose.Quantity, "частичная вместимость сохраняет сумму split/merge", results);
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
    public static string RunAuthoredPlayers(UnityEngine.SceneManagement.Scene scene)
    {
        if (Application.isPlaying || scene.path != "Assets/CharacterTesting/CharacterTest.unity") throw new InvalidOperationException("Проверка авторского игрока — в открытой CharacterTest вне Play.");
        var players = UnityEngine.Object.FindObjectsByType<ZombieSurvival.Interaction.PlayerInteractor>(FindObjectsInactive.Include).Where(p => p.gameObject.scene == scene && p.GetComponent<ZombieSurvival.Player.FirstPersonController>() != null).ToArray();
        var results = new List<string>();
        Require(players.Length > 0 && players.All(p => p.GetComponents<PlayerServiceSession>().Length == 1 && p.GetComponent<PlayerServiceSession>().enabled), "каждый реальный сценовый игрок содержит одну включённую сессию сервиса", results);
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
    public static string RunSavedWorld(Trader trader, ZombieSurvival.Player.FirstPersonController motor)
    {
        if (!Application.isPlaying || Application.productName != "T5-Isolated-Checks") throw new InvalidOperationException("Только изолированная копия с отдельным persistentDataPath.");
        var results = new List<string>(); var player = motor.gameObject;
        var stamina = player.GetComponent<ZombieSurvival.Player.PlayerStamina>() ?? player.AddComponent<ZombieSurvival.Player.PlayerStamina>();
        typeof(ZombieSurvival.Player.FirstPersonController).GetField("stamina", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(motor, stamina);
        var save = player.GetComponent<PlayerSaveSystem>() ?? player.AddComponent<PlayerSaveSystem>();
        var root = player.GetComponent<WorldSaveObject>() ?? player.AddComponent<WorldSaveObject>();
        if (player.GetComponent<ZombieSurvival.Progression.PlayerProgression>() == null)
        {
            var progression = player.AddComponent<ZombieSurvival.Progression.PlayerProgression>(); progression.enabled = false;
            var progressionData = new SerializedObject(progression); progressionData.FindProperty("tree").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ZombieSurvival.Progression.SkillTreeDefinition>("Assets/CharacterTesting/Progression/CharacterSkillTree.asset");
            progressionData.ApplyModifiedPropertiesWithoutUndo(); progression.enabled = true;
        }
        var id = new SerializedObject(player.GetComponent<SaveIdentity>()); id.FindProperty("persistentId").stringValue = "player"; id.ApplyModifiedPropertiesWithoutUndo();
        var registry = UnityEngine.Object.FindAnyObjectByType<WorldSaveRegistry>();
        var registryData = new SerializedObject(registry); var objects = registryData.FindProperty("sceneObjects");
        if (!Enumerable.Range(0, objects.arraySize).Any(i => objects.GetArrayElementAtIndex(i).objectReferenceValue == root))
        { int added = objects.arraySize++; objects.GetArrayElementAtIndex(added).objectReferenceValue = root; registryData.ApplyModifiedPropertiesWithoutUndo(); }
        string filename = "t5-check-" + Guid.NewGuid().ToString("N") + ".json";
        var settings = new SerializedObject(save); settings.FindProperty("fileName").stringValue = filename;
        var catalog = settings.FindProperty("itemCatalog"); var types = trader.Stocks.SelectMany(s => s.profile.Catalog).Select(e => e.item).Distinct().ToArray(); catalog.arraySize = types.Length;
        for (int i = 0; i < types.Length; i++) catalog.GetArrayElementAtIndex(i).objectReferenceValue = types[i]; settings.ApplyModifiedPropertiesWithoutUndo();
        var gear = trader.Stocks.Single(s => s.profile.Id == "equipment"); var guns = trader.Stocks.Single(s => s.profile.Id == "weapons_ammo");
        gear.storage.TrySetDurability(0, 35); gear.storage.TrySetDurability(1, 80); guns.storage.TrySetDurability(0, 47); guns.storage.Reload(guns.storage.GetSlot(0));
        string gearId = gear.storage.GetSlot(0).InstanceId, weaponId = guns.storage.GetSlot(0).InstanceId; int rounds = guns.storage.GetSlot(0).LoadedRounds;
        trader.TrySelectProfile(gear.profile); var funds = trader.GetComponent<ZombieSurvival.Economy.TraderBudget>().Funds;
        if (funds.Balance < 500) funds.TryCredit(500 - funds.Balance); else if (funds.Balance > 500) funds.TryDebit(funds.Balance - 500);
        try
        {
            Require(save.TrySave(), "общий PlayerSaveSystem сохраняет Т5: " + save.Status, results);
            Require(gear.storage.TransferTo(player.GetComponent<ItemStorage>(), 0) == 1, "тестовый перенос исходной вещи после Save", results);
            trader.TrySelectProfile(trader.Stocks.Single(s => s.profile.Id == "food_medicine").profile); funds.TryDebit(100);
            Require(save.TryLoad(), "общий PlayerSaveSystem загружает Т5: " + save.Status, results);
            Require(trader.Profile == gear.profile && funds.Balance == 500 && gear.storage.GetSlot(0).InstanceId == gearId
                && gear.storage.GetSlot(0).Durability == 35 && gear.storage.GetSlot(1).Durability == 80
                && guns.storage.GetSlot(0).InstanceId == weaponId && guns.storage.GetSlot(0).Durability == 47 && guns.storage.GetSlot(0).LoadedRounds == rounds
                && Enumerable.Range(0, player.GetComponent<ItemStorage>().SlotCount).All(i => player.GetComponent<ItemStorage>().GetSlot(i) == null || player.GetComponent<ItemStorage>().GetSlot(i).IsEmpty),
                "единый commit возвращает профиль/рубли/ID/прочность/магазин/местонахождение без копии", results);
        }
        finally
        {
            foreach (var suffix in new[] { "", ".bak", ".tmp" }) if (System.IO.File.Exists(save.SavePath + suffix)) System.IO.File.Delete(save.SavePath + suffix);
        }
        return string.Join("\n", results);
    }
}
