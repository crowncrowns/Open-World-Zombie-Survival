using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using ZombieSurvival.AI;
using ZombieSurvival.Economy;
using ZombieSurvival.Interaction;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;
using ZombieSurvival.Services;

public static class UserLocationChecks
{
    public static string LastReport { get; private set; }
    public static bool Completed { get; private set; }
    private static void Set(UnityEngine.Object owner, string field, object value)
    {
        var so = new SerializedObject(owner); var p = so.FindProperty(field);
        if (value is bool flag) p.boolValue = flag; else if (value is string text) p.stringValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    public static string CheckCopy()
    {
        var originals = UnityEngine.Object.FindObjectsByType<SaveIdentity>(FindObjectsInactive.Include).ToDictionary(i => i, i => i.Id);
        var source = UnityEngine.Object.FindObjectsByType<Trader>(FindObjectsInactive.Exclude).First();
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        try
        {
            var copy = TraderCopyTools.Duplicate(source).GetComponent<Trader>(); copy.ValidateConfiguration();
            bool independent = copy.Stocks.Zip(source.Stocks, (a, b) => a.storage != b.storage && a.profile == b.profile && a.storage.transform.IsChildOf(copy.transform)).All(x => x)
                && copy.GetComponent<TraderBudget>().Funds != source.GetComponent<TraderBudget>().Funds;
            var ids = UnityEngine.Object.FindObjectsByType<SaveIdentity>(FindObjectsInactive.Include);
            bool unique = ids.Select(i => i.Id).Distinct().Count() == ids.Length;
            bool preserved = originals.All(k => k.Key.Id == k.Value);
            UnityEngine.Object.FindFirstObjectByType<WorldSaveRegistry>().ValidateCoverage();
            if (!independent || !unique || !preserved) throw new InvalidOperationException("Copy isolation / ID preservation failed");
            return "PASS full trader duplication: private stores/wallet; shared profiles; unique IDs; originals preserved; registry valid";
        }
        finally { Undo.RevertAllDownToGroup(group); SaveIdentityAuthoring.CaptureBaseline(); }
    }

    public static IEnumerator RunPlay()
    {
        Completed = false; LastReport = "Running";
        if (!Application.isPlaying || !Application.dataPath.Contains("_CodexRecovery")) throw new InvalidOperationException("Recovery Play only");
        var results = new List<string>(); var scenario = Scenario(results);
        while (true)
        {
            bool more; try { more = scenario.MoveNext(); } catch (Exception e) { results.Add("FAIL " + e); break; }
            if (!more) break; yield return scenario.Current;
        }
        (scenario as IDisposable)?.Dispose(); LastReport = string.Join("\n", results); Completed = true;
    }
    private static IEnumerator Scenario(List<string> results)
    {
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); results.Add("PASS " + label); }
        var motor = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
        var actor = motor.GetComponent<PlayerInteractor>(); var session = motor.GetComponent<PlayerServiceSession>();
        var save = motor.GetComponent<PlayerSaveSystem>(); var registry = UnityEngine.Object.FindFirstObjectByType<WorldSaveRegistry>();
        Set(motor, "requireApplicationFocus", false); Set(motor, "requireCursorCapture", false);
        Set(session, "requireApplicationFocus", false); Set(actor, "allowWhileAirborne", true);
        motor.GetComponent<PlayerSaveSlots>().enabled = false; Set(save, "fileName", "user-location-isolated-check.json");
        for (int i = 0; i < 12; i++) yield return null;
        var mall = UnityEngine.Object.FindObjectsByType<Trader>(FindObjectsInactive.Exclude).Where(t => t.transform.position.z < -90).ToArray();
        Check(mall.Length == 3 && mall.Select(t => t.Profile.Id).Distinct().Count() == 3, "three functional mall traders, one of each type");
        var wallet = motor.GetComponent<Wallet>(); wallet.TryCredit(100000);
        foreach (var trader in mall)
        {
            session.Close(); var cc = motor.GetComponent<CharacterController>(); cc.enabled = false;
            motor.transform.SetPositionAndRotation(new Vector3(trader.transform.position.x - 2, .03f, trader.transform.position.z), Quaternion.Euler(0, 90, 0));
            cc.enabled = true; Physics.SyncTransforms(); for (int i = 0; i < 8; i++) yield return null;
            var point = trader.GetComponent<TraderServicePoint>();
            Check(point.CanAccess(actor, out var reason), trader.name + " physical access: " + reason);
            Check(point.Interact(actor), trader.name + " normal E interaction opens session");
            Check(session.IsOpen && !motor.AcceptingGameplayInput, "menu owns gameplay lock");
            var stock = trader.ActiveStock;
            var item = Enumerable.Range(0, stock.SlotCount).Select(stock.GetSlot).First(s => s != null && !s.IsEmpty);
            var other = mall.Where(t => t != trader).ToDictionary(t => t, t => t.GetComponent<TraderBudget>().Funds.Balance);
            Check(TradeService.TryQuote(session, trader, TradeDirection.Buy, item, 1, out var buy, out reason) && TradeService.TryExecute(buy, out reason), trader.name + " buys original item: " + reason);
            var bag = motor.GetComponent<ItemStorage>();
            var purchased = Enumerable.Range(0, bag.SlotCount).Select(bag.GetSlot).First(s => s != null && !s.IsEmpty && s.Definition == item.Definition);
            Check(TradeService.TryQuote(session, trader, TradeDirection.Sell, purchased, 1, out var sell, out reason) && TradeService.TryExecute(sell, out reason), trader.name + " sells purchased type back: " + reason);
            Check(other.All(k => k.Key.GetComponent<TraderBudget>().Funds.Balance == k.Value), "other trader budgets unchanged");
            session.Close(); Check(!session.IsOpen, "session closes");
        }
        var bench = GameObject.Find(ForestRouteSetup.RootName).transform.Find("Kurs workbench");
        var controller = motor.GetComponent<CharacterController>(); controller.enabled = false;
        motor.transform.SetPositionAndRotation(bench.position + Vector3.left * 2, Quaternion.Euler(0, 90, 0)); controller.enabled = true;
        Physics.SyncTransforms(); for (int i = 0; i < 8; i++) yield return null;
        Check(bench.GetComponent<WorkbenchServicePoint>().Interact(actor), "mall workbench opens normal session"); session.Close();
        registry.ValidateCoverage(); var world = registry.CaptureState();
        Check(world.objects.Length > 0, "full scene capture validates every actor, trader and door");
        Check(save.TrySave(), "complete player and world save");
        var balances = mall.ToDictionary(t => t.GetComponent<SaveIdentity>().Id, t => t.GetComponent<TraderBudget>().Funds.Balance);
        bool loaded = save.TryLoad(); Check(loaded, "complete player and world load: " + save.Status);
        Check(mall.All(t => t.GetComponent<TraderBudget>().Funds.Balance == balances[t.GetComponent<SaveIdentity>().Id]), "all three mall budgets survive load exactly");
        foreach (var zombie in GameObject.Find(ForestRouteSetup.RootName).GetComponentsInChildren<EnemyBrain>())
        {
            var agent = zombie.GetComponent<NavMeshAgent>();
            Check(agent.enabled && agent.isOnNavMesh, zombie.name + " bound to navigation");
        }
    }
}
