using System;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
public static class InventoryEquipmentSetup
{
    [MenuItem("Tools/Character Lab/Install Inventory Equipment")]
    public static void Install()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Остановите Play перед изменением prefab.");
        const string path="Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var owner=root.GetComponent<PlayerEquipment>()??root.AddComponent<PlayerEquipment>();var data=new SerializedObject(owner);
            var storage=data.FindProperty("equipment").objectReferenceValue as ItemStorage;
            if(storage==null)
            {
                var go=new GameObject("Wearable Equipment");go.transform.SetParent(root.transform,false);storage=go.AddComponent<ItemStorage>();go.AddComponent<EquipmentStorage>();
                var slots=new SerializedObject(storage);slots.FindProperty("slotCount").intValue=5;slots.ApplyModifiedPropertiesWithoutUndo();
                var id=go.AddComponent<SaveIdentity>();var identity=new SerializedObject(id);identity.FindProperty("persistentId").stringValue="player-wearable-equipment";identity.ApplyModifiedPropertiesWithoutUndo();
                data.FindProperty("equipment").objectReferenceValue=storage;data.ApplyModifiedPropertiesWithoutUndo();
            }
            // Только новый компонент/дочернее хранилище. Не переписывать движения/UAL/камеру/IDs и сценовые overrides.
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/CharacterTesting/Items/T2/TestBackpack.asset");if(item!=null)
        {
            var data=new SerializedObject(item);
            if(data.FindProperty("wearSlot").enumValueIndex==(int)WearSlot.None)
            {data.FindProperty("wearSlot").enumValueIndex=(int)WearSlot.Backpack;data.FindProperty("backpackSlots").intValue=12;data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(item);AssetDatabase.SaveAssetIfDirty(item);}
        }
        Debug.Log("INVENTORY_EQUIPMENT_INSTALLED: prefab + существующий тестовый рюкзак, без сохранения сцены.");
    }
}
