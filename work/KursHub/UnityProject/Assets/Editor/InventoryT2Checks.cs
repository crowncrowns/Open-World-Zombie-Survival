using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Inventory;

public static class InventoryT2Checks
{
    private static readonly List<string> passed = new List<string>();
    private static void Check(bool condition, string name) { if (!condition) throw new Exception("T2 FAILED: " + name); passed.Add(name); }
    private static ItemStorage Store(Transform parent, string name, int size)
    {
        var go = new GameObject(name); go.transform.SetParent(parent); var storage = go.AddComponent<ItemStorage>();
        var so = new SerializedObject(storage); so.FindProperty("slotCount").intValue=size; so.ApplyModifiedPropertiesWithoutUndo(); return storage;
    }
    private static ItemDefinition Load(string path) => AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
    private static int Index(ItemStorage s, ItemStack item) { for(int i=0;i<s.SlotCount;i++) if(s.GetSlot(i)==item) return i; return -1; }
    [MenuItem("Tools/Character Lab/Check T2 Inventory Contracts")]
    public static string Run()
    {
        passed.Clear(); var root=new GameObject("T2 contract checks (temporary)");
        const string probe=InventoryT2Setup.Root+"/T2SerializationProbe.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(probe)!=null) { UnityEngine.Object.DestroyImmediate(root); throw new Exception("Probe path occupied; refusing overwrite."); }
        try
        {
            var gear=Load(InventoryT2Setup.GearPath); var food=Load("Assets/CharacterTesting/Items/CannedFood.asset"); var cloth=Load("Assets/CharacterTesting/Items/Cloth.asset");
            var gun=Load("Assets/Weapons/Definitions/ArmyPistolItem.asset"); var ammo=Load("Assets/Weapons/Definitions/Ammo9mmItem.asset");
            var a=Store(root.transform,"a",3); var b=Store(root.transform,"b",3);
            var first=new ItemStack(gear,1,35); var second=new ItemStack(gear,1,80);
            Check(a.TryAddStack(first)&&a.TryAddStack(second)&&a.GetSlot(0)==first&&first.InstanceId!=second.InstanceId,"same type: original references, distinct IDs and durability35/80");
            Check(!a.TryAddStack(first)&&!b.TryAddStack(first)&&!b.TryAddStack(first.Copy(1)),"owned originals and Copy snapshots cannot duplicate");
            var id=first.InstanceId; int events=0;
            a.Changed+=()=>{ if(!a.Contains(first)&&b.Contains(first))events++; };
            Check(a.TransferTo(b,0)==1&&b.GetSlot(0)==first&&first.InstanceId==id&&first.Durability==35&&events==1,"transfer committed on both sides before Changed");
            Check(b.TransferTo(a,0)==1&&a.GetSlot(0)==first&&second.Durability==80,"return from chest preserves exact instances");
            Check(!a.TrySetDurability(0,float.NaN)&&!a.TrySetDurability(0,float.PositiveInfinity),"invalid durability rejected");
            a.TrySetDurability(0,0); var roundtrip=JsonUtility.FromJson<ItemStack>(JsonUtility.ToJson(first));
            Check(roundtrip.Durability==0&&roundtrip.InstanceId==id,"Unity JSON roundtrip preserves explicit broken zero and ID");
            a.TrySetDurability(0,35);
            string old=JsonUtility.ToJson(new ItemStack(gun,1)); old=Regex.Replace(old,",?\"durability\":[^,}]+",""); old=Regex.Replace(old,",?\"stateVersion\":[^,}]+","");
            var legacy=JsonUtility.FromJson<ItemStack>(old); Check(legacy.Durability==100,"legacy missing durability fields migrates to full");
            string batch=JsonUtility.ToJson(legacy).Replace("\"quantity\":1","\"quantity\":3").Replace("\"loadedRounds\":0","\"loadedRounds\":7");
            var legacyStorage=Store(root.transform,"legacy",1);
            JsonUtility.FromJsonOverwrite("{\"slotCount\":1,\"slots\":["+batch+"]}",legacyStorage);
            Check(legacyStorage.SlotCount==3&&legacyStorage.Count(gun)==3&&Enumerable.Range(0,3).Sum(i=>legacyStorage.GetSlot(i).LoadedRounds)==7&&Enumerable.Range(0,3).Select(i=>legacyStorage.GetSlot(i).InstanceId).Distinct().Count()==3,"serialized legacy batch3 ->3 IDs,3 slots, total rounds7 once");
            var serialized=PrefabUtility.SaveAsPrefabAsset(legacyStorage.gameObject,probe);
            var loaded=PrefabUtility.LoadPrefabContents(probe);
            try { var s=loaded.GetComponent<ItemStorage>(); Check(s.Count(gun)==3&&s.GetSlot(0).Durability==100&&s.GetSlot(0).LoadedRounds==7,"real prefab save/load preserves migrated count, durability and magazine"); }
            finally { PrefabUtility.UnloadPrefabContents(loaded); }
            var res=Store(root.transform,"resources",2); var limited=Store(root.transform,"limited",1);
            Check(res.TryAddAll(food,9)&&limited.TryAddAll(food,4)&&res.TransferTo(limited,0,6)==1&&res.Count(food)+limited.Count(food)==13,"partial capacity transfer conserves13 food");
            Check(res.TransferTo(limited,0)==0&&!limited.TryAddAll(food,1)&&res.Count(food)+limited.Count(food)==13,"full capacity refusal preserves quantity");
            var x=Store(root.transform,"cloth source",2); var y=Store(root.transform,"cloth destination",2); x.TryAddAll(cloth,9);
            var sourceId=x.GetSlot(0).InstanceId; Check(x.TransferTo(y,0,3)==3&&x.GetSlot(0).InstanceId==sourceId&&y.GetSlot(0).InstanceId!=sourceId,"resource split creates new identity and preserves source");
            var receiving=y.GetSlot(0).InstanceId; Check(x.TransferTo(y,0)==6&&y.GetSlot(0).InstanceId==receiving&&y.Count(cloth)==9,"resource merge keeps recipient ID and9 cloth");
            var weapons=Store(root.transform,"weapon regression",4); weapons.TryAddAll(gun,1); weapons.TryAddAll(ammo,20); var pistol=weapons.GetSlot(0);
            Check(weapons.Reload(pistol)==12&&weapons.SpendRound(pistol)&&pistol.LoadedRounds==11&&weapons.Count(ammo)==8,"existing reload12/spend1 preserves ammo19");
            weapons.TrySetDurability(0,47); string gunId=pistol.InstanceId;
            Check(weapons.TryExtract(0,1,out var extracted)&&extracted==pistol,"extract hands off original gun");
            var world=new GameObject("world").AddComponent<WorldItem>(); world.transform.SetParent(root.transform); world.Initialize(extracted);
            // Pickup adapter is checked in Play; here the identical payload is imported through the same storage API.
            Check(b.TryAddStack(extracted)&&extracted.InstanceId==gunId&&extracted.Durability==47&&extracted.LoadedRounds==11,"world initialization and import preserve ID/durability47/magazine11");
            Check(ammo.MaxStack==60&&food.MaxStack==5&&cloth.MaxStack==10&&gear.MaxStack==1,"existing resource limits60/5/10; gear1");
            return string.Join("\n",passed);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); if(AssetDatabase.LoadAssetAtPath<GameObject>(probe)!=null)AssetDatabase.DeleteAsset(probe); }
    }
}

