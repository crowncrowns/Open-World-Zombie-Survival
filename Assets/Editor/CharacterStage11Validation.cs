using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;
using ZombieSurvival.Persistence;

public static class CharacterStage11Validation
{
    [MenuItem("Tools/Character Lab/Check Stage 11 Character")]
    public static void Run()
    {
        var errors=new List<string>();var activeScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var ids=new HashSet<string>();
        foreach(var storage in UnityEngine.Object.FindObjectsByType<ItemStorage>(FindObjectsInactive.Include).Where(s=>s.gameObject.scene==activeScene))
        {
            var id=storage.GetComponent<SaveIdentity>();if(id==null || string.IsNullOrWhiteSpace(id.Id) || !ids.Add(id.Id))errors.Add(storage.name+": нужен уникальный SaveIdentity");
        }
        foreach(var player in UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include).Where(p=>p.gameObject.scene==activeScene))
            if(player.GetComponent<PlayerSaveSystem>()==null || player.GetComponent<PlayerNeeds>()==null)errors.Add(player.name+": нет системы потребностей/сохранения");
        foreach(var save in UnityEngine.Object.FindObjectsByType<PlayerSaveSystem>(FindObjectsInactive.Include).Where(s=>s.gameObject.scene==activeScene))
        {
            var so=new SerializedObject(save);var array=so.FindProperty("itemCatalog");var types=new HashSet<string>();
            for(int i=0;i<array.arraySize;i++)
            {
                var item=array.GetArrayElementAtIndex(i).objectReferenceValue as ItemDefinition;
                if(item==null || string.IsNullOrWhiteSpace(item.Id) || !types.Add(item.Id))errors.Add("Неверный каталог предметов");
            }
            foreach(string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
                if(!types.Contains(AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)).Id))errors.Add("Каталог сохранений не включает тип "+AssetDatabase.GUIDToAssetPath(guid));
            if(save.GetComponent<PlayerNeeds>()==null || save.GetComponent<PlayerStamina>()==null)errors.Add(save.name+": отсутствуют потребности/выносливость");
        }
        foreach(string guid in AssetDatabase.FindAssets("t:ConsumableDefinition"))
        {
            var c=AssetDatabase.LoadAssetAtPath<ConsumableDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if(c.Item==null || c.Item.IsIndividual || c.Food+c.Water+c.Healing<=0)errors.Add(c.name+": неверный эффект употребления");
        }
        foreach(var type in new[]{typeof(PlayerNeeds),typeof(ConsumableDefinition),typeof(PlayerSaveSystem),typeof(SaveIdentity),typeof(PlayerCameraMotion),typeof(SurvivalTestConsole)})
            foreach(var field in type.GetFields(BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly))
                if(field.GetCustomAttribute<SerializeField>()!=null && !Regex.IsMatch(field.GetCustomAttribute<TooltipAttribute>()?.tooltip??"","[А-Яа-я]"))errors.Add(type.Name+"."+field.Name+": нет русского Tooltip");
        if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
        Debug.Log("STAGE11_VALIDATION_OK: stable storage IDs, complete catalog, consumption data and Russian tooltips.");
    }
}
