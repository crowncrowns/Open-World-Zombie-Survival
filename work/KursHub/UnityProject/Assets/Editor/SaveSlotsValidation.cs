using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Persistence;
using ZombieSurvival.Inventory;

public static class SaveSlotsValidation
{
    [MenuItem("Tools/Character Lab/Check CM05 Save Slots")]
    public static void Run()
    {
        int players=0;
        foreach(var saves in UnityEngine.Object.FindObjectsByType<PlayerSaveSystem>())
        {
            if(saves.gameObject.scene!=UnityEngine.SceneManagement.SceneManager.GetActiveScene())continue;
            players++;var slots=saves.GetComponent<PlayerSaveSlots>();var menu=saves.GetComponent<PlayerSaveMenu>();
            if(slots==null || menu==null)throw new InvalidOperationException("Игроку нужны PlayerSaveSlots и PlayerSaveMenu.");
            if(saves.SlotPath(SaveSlotKind.Auto,0)==saves.SavePath || saves.SlotPath(SaveSlotKind.Manual,0)==saves.SavePath)throw new InvalidOperationException("Файлы слотов пересекаются.");
        }
        if(players==0)throw new InvalidOperationException("В открытой сцене нет PlayerSaveSystem.");
        foreach(var type in new[]{typeof(PlayerSaveSlots),typeof(PlayerSaveMenu)})
            foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.NonPublic))
                if(field.GetCustomAttribute<SerializeField>()!=null && (field.GetCustomAttribute<TooltipAttribute>()==null || field.GetCustomAttribute<InspectorNameAttribute>()==null))
                    throw new InvalidOperationException(type.Name+"."+field.Name+": нужны русские имя и Tooltip.");
        if(typeof(ItemDefinition).GetField("allowInHotbar",BindingFlags.Instance|BindingFlags.NonPublic)?.GetCustomAttribute<TooltipAttribute>()==null)throw new InvalidOperationException("Не найден параметр допуска типа в хотбар.");
        Debug.Log("CM05_VALIDATION_OK: компоненты, раздельные пути слотов, русские настройки и допуск типов в хотбар.");
    }
}
