using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Economy;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;
using ZombieSurvival.Services;

/// <summary>Real format-3 save/load checks; only the isolated T12 integration scene and disposable save file.</summary>
public static class TraderT12PersistenceChecks
{
    public static bool Completed { get; private set; }
    public static string LastReport { get; private set; }
    private static readonly FieldInfo Clock = typeof(Trader.Stock).GetField("restockElapsed", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo Advance = typeof(Trader).GetMethod("AdvanceRestock", BindingFlags.Instance | BindingFlags.NonPublic);
    private static double Elapsed(Trader.Stock row) => (double)Clock.GetValue(row);
    private static void Set(UnityEngine.Object target, string field, int value)
    { var so = new SerializedObject(target); so.FindProperty(field).intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static string Items(ItemStorage storage) => string.Join(";", Enumerable.Range(0, storage.SlotCount).Select(i =>
    { var s = storage.GetSlot(i); return s == null || s.IsEmpty ? "-" : s.InstanceId + ":" + s.Quantity + ":" + s.Durability + ":" + s.LoadedRounds; }));

    public static IEnumerator RunPlay()
    {
        Completed = false; LastReport = "Running";
        if (!Application.isPlaying || !Application.dataPath.Contains("_CodexRecovery") || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "T12Integration")
        { Completed = true; LastReport = "FAIL: isolated T12Integration Play required"; yield break; }
        var reports = new List<string>();
        var routine = Scenario(reports);
        while (true)
        {
            bool next;
            try { next = routine.MoveNext(); }
            catch (Exception error) { reports.Add("FAIL " + error); break; }
            if (!next) break;
            yield return routine.Current;
        }
        (routine as IDisposable)?.Dispose();
        LastReport = string.Join("\n", reports); Completed = true;
    }

    private static IEnumerator Scenario(List<string> reports)
    {
        void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); reports.Add("PASS " + label); }
        var motor = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        var trader = UnityEngine.Object.FindAnyObjectByType<Trader>();
        var save = motor.GetComponent<PlayerSaveSystem>();
        Check(Path.GetFileName(save.SavePath) == "trader-t12-integration.json", "только отдельный тестовый файл");
        for (int i = 0; i < 12; i++) yield return null;
        Check(save.CanSaveNow(out var why), "штатный игрок готов к Save: " + why);
        var guns = trader.Stocks.Single(r => r.profile.Id == "weapons_ammo");
        var bag = motor.GetComponent<ItemStorage>(); var wallet = motor.GetComponent<Wallet>();
        var funds = trader.GetComponent<TraderBudget>().Funds;
        var adapter = trader.GetComponent<WorldTraderSaveAdapter>();
        Check(trader.TrySelectProfile(guns.profile), "выбран оружейный профиль");
        var weapon = Enumerable.Range(0, guns.storage.SlotCount).Select(guns.storage.GetSlot).First(s => s != null && s.Definition.Weapon != null);
        int index = Enumerable.Range(0, guns.storage.SlotCount).First(i => guns.storage.GetSlot(i) == weapon);
        Check(guns.storage.TrySetDurability(index, 47), "исходное оружие изношено");
        guns.storage.Reload(weapon);
        var ammo = guns.profile.Catalog.First(e => e.item.Category == ItemCategory.Ammunition).item;
        int ammoIndex = Enumerable.Range(0, guns.storage.SlotCount).First(i => guns.storage.GetSlot(i)?.Definition == ammo);
        Check(guns.storage.TransferTo(bag, ammoIndex) > 0, "часть запаса перенесена штатным API");
        wallet.TryCredit(4321); funds.TryCredit(5678);
        foreach (var row in trader.Stocks) Clock.SetValue(row, (double)trader.RestockIntervalSeconds - 5);
        string stockBefore = string.Join("|", trader.Stocks.Select(r => Items(r.storage))), bagBefore = Items(bag);
        string id = weapon.InstanceId; int rounds = weapon.LoadedRounds;
        double[] clocks = trader.Stocks.Select(Elapsed).ToArray(); long cash = wallet.Balance, budget = funds.Balance;
        try
        {
            Check(save.TrySave(), "реальный формат3 записан: " + save.Status);
            Check(File.Exists(save.SavePath), "файл с общим снимком существует");
            Check((bool)Advance.Invoke(trader, new object[] { 6d }), "поставка после сохранения");
            trader.TrySelectProfile(trader.Stocks.First(r => r != guns).profile);
            wallet.TryCredit(19); funds.TryCredit(23); Set(trader, "saleMarkupPercent", 20);
            Check(save.TryLoad(), "реальный формат3 загружен: " + save.Status);
            Check(trader.Profile == guns.profile && string.Join("|", trader.Stocks.Select(r => Items(r.storage))) == stockBefore && Items(bag) == bagBefore,
                "загрузка восстанавливает профиль и все товары без дубликатов");
            Check(wallet.Balance == cash && funds.Balance == budget, "рубли игрока и бюджет восстановлены вместе с вещами");
            Check(trader.Stocks.Select(Elapsed).SequenceEqual(clocks), "все таймеры восстановлены точно");
            var restored = Enumerable.Range(0, guns.storage.SlotCount).Select(guns.storage.GetSlot).First(s => s?.InstanceId == id);
            Check(restored.Durability == 47 && restored.LoadedRounds == rounds, "ID, износ и заряженный магазин пережили Save/Load");
            Check(trader.SaleMarkupPercent == 20, "загрузка не откатывает авторские настройки цены");
            Check((bool)Advance.Invoke(trader, new object[] { 4d }) && guns.storage.Count(ammo) < guns.profile.Catalog.First(e => e.item == ammo).quantity,
                "после загрузки поставка не приходит раньше срока");
            Check((bool)Advance.Invoke(trader, new object[] { 1d }) && guns.storage.Count(ammo) == guns.profile.Catalog.First(e => e.item == ammo).quantity,
                "после загрузки поставка приходит по оставшемуся игровому времени");
            Check(guns.storage.Contains(restored) && restored.Durability == 47 && restored.LoadedRounds == rounds && funds.Balance == budget,
                "поставка не ремонтирует старое оружие и не пополняет бюджет");
            string metadata = adapter.Capture();
            string legacy = Regex.Replace(metadata.Replace("\"economyVersion\":1,", ""), ",\"restockElapsed\":[0-9.Ee+\\-]+", "");
            adapter.Validate(legacy, null); string goods = string.Join("|", trader.Stocks.Select(r => Items(r.storage)));
            adapter.ApplySilent(legacy); adapter.Publish();
            Check(trader.Stocks.All(r => Elapsed(r) == 0) && goods == string.Join("|", trader.Stocks.Select(r => Items(r.storage))),
                "старые метаданные без таймера мигрируют в0 без выдачи товаров");
            bool Reject(string json) { try { adapter.Validate(json, null); return false; } catch (InvalidOperationException) { return true; } }
            Check(Reject(metadata.Replace("\"economyVersion\":1", "\"economyVersion\":2")), "неизвестная будущая версия отвергается");
            string invalid = Regex.Replace(metadata, "\"restockElapsed\":[0-9.Ee+\\-]+", "\"restockElapsed\":-1");
            Check(Reject(invalid) && trader.Stocks.All(r => Elapsed(r) == 0), "отрицательный таймер отвергается до мутаций");
            Check(save.TrySave() && save.TryLoad(), "повторный Save/Load после миграции не выдаёт товары повторно");
        }
        finally
        {
            Set(trader, "saleMarkupPercent", 0);
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(save.SavePath + suffix)) File.Delete(save.SavePath + suffix);
        }
    }
}
