using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Persistence;

public static class WorldSavingValidation
{
    [MenuItem("Tools/Zombie Survival/Проверить сохранение мира")]
    public static void Run()
    {
        var registries=UnityEngine.Object.FindObjectsByType<WorldSaveRegistry>(FindObjectsInactive.Include);
        foreach(var group in registries.GroupBy(r=>r.gameObject.scene))
        {
            if(group.Count()!=1)throw new InvalidOperationException("В сцене должен быть один WorldSaveRegistry.");
            group.First().ValidateCoverage();
        }
        if(registries.Length==0)throw new InvalidOperationException("В открытой сцене нет реестра мира.");
        Debug.Log("CM04_VALIDATION_OK: ID, область, каталог, адаптеры, хранилища и все кошельки.");
    }
}
