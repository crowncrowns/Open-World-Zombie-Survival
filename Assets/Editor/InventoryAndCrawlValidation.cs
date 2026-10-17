using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using ZombieSurvival.AI;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;

public static class InventoryAndCrawlValidation
{
    [MenuItem("Tools/Zombie Survival/Проверить инвентарь и низкую навигацию")]
    public static void Run()
    {
        var errors = new List<string>(); var ids = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id)) errors.Add(item.name + ": пустой или повторяющийся Item ID");
            if (item.WorldPrefab == null || item.WorldPrefab.GetComponent<WorldItem>() == null || item.WorldPrefab.GetComponent<Collider>() == null)
                errors.Add(item.name + ": нужен мировой префаб с WorldItem и Collider");
        }
        foreach (var world in UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsSortMode.None))
            if (world.Item == null || world.Quantity <= 0) errors.Add(world.name + ": не назначен предмет или количество");
        foreach (var player in UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
        {
            if (player.GetComponent<ItemStorage>() == null || player.GetComponent<PlayerInventory>() == null || player.GetComponent<PlayerNoiseEmitter>() == null)
                errors.Add(player.name + ": не хватает инвентаря или источника шума");
        }
        foreach (var crawl in UnityEngine.Object.FindObjectsByType<EnemyCrawlPursuit>(FindObjectsSortMode.None))
        {
            int type = new SerializedObject(crawl).FindProperty("crawlAgentType").intValue;
            var settings = NavMesh.GetSettingsByID(type);
            if (type == crawl.GetComponent<NavMeshAgent>().agentTypeID && !Application.isPlaying || settings.agentTypeID == -1 || settings.agentHeight > .85f)
                errors.Add(crawl.name + ": неверный тип низкого агента");
            bool surfaceFound = false;
            foreach (var surface in UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
                if (surface.agentTypeID == type && surface.navMeshData != null) surfaceFound = true;
            if (!surfaceFound) errors.Add(crawl.name + ": в открытой сцене нет запечённой низкой поверхности");
        }
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        Debug.Log("INVENTORY_CRAWL_VALIDATION_OK: definitions, IDs, world pickups, player components and low navigation surfaces.");
    }
}
