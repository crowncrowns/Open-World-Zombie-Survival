using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;
using ZombieSurvival.Weapons;

public static class WeaponValidation
{
    [MenuItem("Tools/Zombie Survival/Проверить оружие")]
    public static void Run()
    {
        var errors=new List<string>();
        foreach(string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)); var data=item.Weapon;
            if(data==null) continue;
            if(item.MaxStack!=1) errors.Add(item.name+": оружие должно занимать отдельный слот");
            if(data.viewPrefab==null || data.attacks==null || data.attacks.Length==0) errors.Add(item.name+": не назначены модель/звуки атаки");
            if(data.kind==WeaponKind.Firearm && (data.Ammo==null || data.Ammo==item || data.Ammo.Weapon!=null || data.reloadSound==null)) errors.Add(item.name+": нужен отдельный ресурс патронов и звук перезарядки");
            if(data.range<=0 || data.cooldown<=0 || data.windup<0 || data.damage<0) errors.Add(item.name+": неверные дистанция/время/урон");
            if(float.IsNaN(data.durabilityCostPerUse) || float.IsInfinity(data.durabilityCostPerUse) || data.durabilityCostPerUse<0) errors.Add(item.name+": износ должен быть конечным неотрицательным числом, ед./применение");
        }
        foreach(var player in UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include))
        {
            var weapons=player.GetComponent<PlayerWeapons>(); if(weapons==null) { errors.Add(player.name+": нет PlayerWeapons"); continue; }
            if(new SerializedObject(weapons).FindProperty("viewCamera").objectReferenceValue==null) errors.Add(player.name+": не назначена камера оружия");
            var input=new SerializedObject(player).FindProperty("inputActions").objectReferenceValue as UnityEngine.InputSystem.InputActionAsset;
            foreach(var action in new[]{"Attack","Reload","Aim","Previous","Next"}) if(input==null || input.FindAction("Player/"+action)==null) errors.Add(player.name+": нет действия "+action);
        }
        if(errors.Count>0) throw new InvalidOperationException(string.Join("\n",errors));
        Debug.Log("WEAPON_VALIDATION_OK: weapon data, audio, camera and input.");
    }
}
