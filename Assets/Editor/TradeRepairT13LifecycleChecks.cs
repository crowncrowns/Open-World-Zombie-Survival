using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Combat;
using ZombieSurvival.Economy;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;
using ZombieSurvival.Services;

/// <summary>Destructive lifecycle scenarios run only in the disposable integration scene.</summary>
public static class TradeRepairT13LifecycleChecks
{
    public static bool Completed { get; private set; }
    public static string LastReport { get; private set; }
    public static IEnumerator RunPlay()
    {
        Completed=false; LastReport="Running";
        if(!Application.isPlaying || !Application.dataPath.Contains("_CodexRecovery") || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="T13Integration")
        {Completed=true;LastReport="FAIL isolated T13Integration required";yield break;}
        var lines=new List<string>();var routine=Scenario(lines);
        while(true){bool next;try{next=routine.MoveNext();}catch(Exception e){lines.Add("FAIL "+e);break;}if(!next)break;yield return routine.Current;}
        (routine as IDisposable)?.Dispose();Completed=true;LastReport=string.Join("\n",lines);
    }
    private static IEnumerator Scenario(List<string> lines)
    {
        void Check(bool pass,string text){if(!pass)throw new InvalidOperationException(text);lines.Add("PASS "+text);}
        var player=UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        var session=player.GetComponent<PlayerServiceSession>();var save=player.GetComponent<PlayerSaveSystem>();
        var trader=UnityEngine.Object.FindAnyObjectByType<Trader>();var bench=UnityEngine.Object.FindAnyObjectByType<RepairWorkbench>();
        var data=new SerializedObject(save);string originalFile=data.FindProperty("fileName").stringValue;
        data.FindProperty("fileName").stringValue="trade-repair-t13-lifecycle.json";data.ApplyModifiedPropertiesWithoutUndo();
        string path=save.SavePath;GameObject unknown=null;
        var inventory=player.GetComponent<ItemStorage>();var wallet=player.GetComponent<Wallet>();
        // Load creates inactive geometry in a separate staging scene until the end of the frame.
        // Compare only the actual game area, as PlayerSaveSystem does.
        string Snapshot()=>wallet.Balance+"|"+trader.GetComponent<TraderBudget>().Funds.Balance+"|"+string.Join(";",UnityEngine.Object.FindObjectsByType<ItemStorage>(FindObjectsInactive.Include).Where(s=>s.gameObject.scene==player.gameObject.scene).OrderBy(s=>s.GetComponent<SaveIdentity>()?.Id).Select(s=>
            string.Join(",",Enumerable.Range(0,s.SlotCount).Select(i=>{var v=s.GetSlot(i);return v==null||v.IsEmpty?"-":v.InstanceId+":"+v.Quantity+":"+v.Durability+":"+v.LoadedRounds;}))));
        void Move(Vector3 position){session.Close();player.transform.position=position;Physics.SyncTransforms();}
        try
        {
            Move(Vector3.zero);yield return null;yield return null;
            Check(save.TrySave()&&save.TrySave(),"baseline and explicit backup saved through full registry: "+save.Status);
            Check(session.TryOpen(trader.GetComponent<TraderServicePoint>()),"trade opened before distance loss");
            player.transform.position+=Vector3.left*20;Physics.SyncTransforms();yield return null;yield return null;
            Check(!session.IsOpen&&player.AcceptingGameplayInput,"distance loss closes trade and releases control");
            Move(Vector3.zero);yield return null;yield return null;
            Check(session.TryOpen(trader.GetComponent<TraderServicePoint>()),"trade reopened before disable");
            trader.enabled=false;yield return null;yield return null;
            Check(!session.IsOpen&&player.AcceptingGameplayInput,"disabled Trader closes trade and releases control");trader.enabled=true;
            yield return null;yield return null;
            Check(session.TryOpen(trader.GetComponent<TraderServicePoint>()),"trade reopened before world removal");
            trader.GetComponent<WorldSaveObject>().RemoveFromWorld();yield return null;yield return null;
            Check(!session.IsOpen&&player.AcceptingGameplayInput,"removed NPC closes trade and releases control");
            Check(save.TryLoad(),"same snapshot restores removed NPC: "+save.Status);yield return null;yield return null;
            Check(trader.gameObject.activeInHierarchy&&!trader.GetComponent<WorldSaveObject>().IsRemoved,"NPC restored on original scene reference");
            Move(new Vector3(6,0,0));yield return null;yield return null;
            Check(session.TryOpen(bench.GetComponent<WorkbenchServicePoint>()),"workbench opened before disable");
            bench.enabled=false;yield return null;yield return null;
            Check(!session.IsOpen&&player.AcceptingGameplayInput,"disabled repair component closes menu and releases control");bench.enabled=true;
            Move(Vector3.zero);yield return null;yield return null;
            Check(session.TryOpen(trader.GetComponent<TraderServicePoint>()),"trade opened before death");
            var health=player.GetComponent<Health>();
            Check(health.ApplyDamage(new DamageInfo(health.Maximum*10,null,player.transform.position,DamageKind.Environment)),"real lethal damage applied");
            yield return null;yield return null;
            Check(health.IsDead&&!session.IsOpen&&!save.CanSaveNow(out _),"death closes service and blocks save");
            Check(save.TryLoad(),"load after death restores alive snapshot: "+save.Status);yield return null;yield return null;
            Check(!health.IsDead&&player.AcceptingGameplayInput,"load after death restores playable control");
            Check(session.TryOpen(trader.GetComponent<TraderServicePoint>()),"trade reopened before rejected load");
            string before=Snapshot();string file=File.ReadAllText(path);
            File.WriteAllText(path,"corrupt T13 disposable test");
            Check(!save.TryLoad()&&Snapshot()==before&&session.IsOpen&&!player.GameplayInputEnabled,"corrupt file leaves money, items and current service intact; no silent backup fallback");
            Check(save.TryLoadSlot(SaveSlotKind.Quick,0,true)&&Snapshot()==before&&!session.IsOpen,"explicit backup restores exact economy and closes old service");
            File.WriteAllText(path,file);yield return null;yield return null;
            unknown=new GameObject("T13 unregistered wallet");unknown.AddComponent<Wallet>();
            Check(!save.TrySave()&&File.ReadAllText(path)==file,"unregistered wallet refuses save and preserves previous file");
            UnityEngine.Object.Destroy(unknown);unknown=null;yield return null;
            Check(save.TryLoad()&&Snapshot()==before,"valid file still restores exact economy after refused operations");
            Move(new Vector3(6,0,0));yield return null;yield return null;
            Check(session.TryOpen(bench.GetComponent<WorkbenchServicePoint>()),"workbench opened before destruction");
            UnityEngine.Object.Destroy(bench.gameObject);yield return null;yield return null;
            Check(!session.IsOpen&&player.AcceptingGameplayInput,"destroyed workbench closes menu without stuck input");
        }
        finally
        {
            session.Close();if(unknown!=null)UnityEngine.Object.Destroy(unknown);
            foreach(string suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);
            data=new SerializedObject(save);data.FindProperty("fileName").stringValue=originalFile;data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
