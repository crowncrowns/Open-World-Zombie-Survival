using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using ZombieSurvival.AI;
using ZombieSurvival.Combat;
using ZombieSurvival.Interaction;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;

/// <summary>Read-only authoring checks. Path checks need the route's separate meshes baked first.</summary>
public static class ForestRouteChecks
{
    [MenuItem("Tools/First Expedition/Check Route (Read Only)")]
    private static void Menu() => Debug.Log(Run());
    public static string Run()
    {
        var scene = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Single(s => s.path == ForestRouteSetup.ScenePath);
        var route = scene.GetRootGameObjects().Single(g => g.name == ForestRouteSetup.RootName);
        var lines = new List<string>();
        Action<bool, string> check = (ok, label) => { lines.Add((ok ? "PASS " : "FAIL ") + label); };
        var roots = route.GetComponentsInChildren<WorldSaveObject>(true);
        var ids = UnityEngine.Object.FindObjectsByType<SaveIdentity>(FindObjectsInactive.Include).Where(i => i.gameObject.scene == scene).ToArray();
        check(ids.All(i => !string.IsNullOrWhiteSpace(i.Id)) && ids.Select(i => i.Id).Distinct().Count() == ids.Length, "IDs всей сцены непустые и уникальные");
        var registry = UnityEngine.Object.FindObjectsByType<WorldSaveRegistry>(FindObjectsInactive.Include).Single(r => r.gameObject.scene == scene);
        var list = new SerializedObject(registry).FindProperty("sceneObjects");
        var registered = new HashSet<UnityEngine.Object>();
        for (int i = 0; i < list.arraySize; i++) registered.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
        check(roots.All(r => registered.Contains(r)), "Все новые сохраняемые корни зарегистрированы");
        check(route.GetComponentsInChildren<DoorInteractable>(true).Length == 6, "Шесть настоящих интерактивных дверей в двух домах");
        foreach (var door in route.GetComponentsInChildren<DoorInteractable>(true))
            check(door.GetComponent<WorldDoorSaveAdapter>() != null && door.GetComponent<BoxCollider>() != null && door.GetComponent<NavMeshObstacle>()?.carving == true,
                door.transform.parent.parent.name + "/" + door.name + ": solid/save/carve");
        check(route.GetComponentsInChildren<StorageContainer>(true).Length == 2, "Два контейнера с общим ItemStorage");
        foreach (var container in route.GetComponentsInChildren<StorageContainer>(true))
        {
            var store = container.GetComponent<ItemStorage>();
            check(store != null && store.GetComponent<SaveIdentity>() != null, container.name + ": сохраняемое хранилище");
        }
        check(route.GetComponentsInChildren<EnemyBrain>(true).Length == 5, "Пять экземпляров общего зомби: три снаружи и два внутри");
        foreach (string name in new[] { "Entrance ally left", "Entrance ally right" })
        {
            var guard = route.transform.Find(name);
            check(guard != null && guard.GetComponent<ActorIdentity>()?.Faction == ActorFaction.Survivor
                && guard.GetComponent<CombatTarget>() == null && guard.GetComponent<EnemyBrain>() == null, name + ": союзник без боевого AI/цели");
            if (guard == null) continue;
            check(guard.GetComponentsInChildren<Animator>(true).Any(a => a.avatar != null && a.avatar.isValid && a.avatar.isHuman && a.runtimeAnimatorController != null && !a.applyRootMotion),
                name + ": Humanoid idle без root motion");
        }
        foreach (var renderer in route.GetComponentsInChildren<Renderer>(true))
        {
            bool valid = renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported);
            check(valid, "Материалы " + renderer.name);
        }
        var surfaces = route.GetComponentsInChildren<NavMeshSurface>(true);
        check(surfaces.Length == 2 && surfaces.Any(s => s.agentTypeID == 0) && surfaces.Any(s => s.agentTypeID == ForestRouteSetup.CrawlAgentType), "Две собственные поверхности standing/crawl");
        foreach (var surface in surfaces) check(surface.navMeshData != null && surface.collectObjects == CollectObjects.Volume
            && surface.useGeometry == NavMeshCollectGeometry.PhysicsColliders, surface.name + ": запечённый локальный Physics volume");
        return string.Join("\n", lines);
    }

    public static string Paths()
    {
        var scene = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Single(s => s.path == ForestRouteSetup.ScenePath);
        var route = scene.GetRootGameObjects().Single(g => g.name == ForestRouteSetup.RootName);
        var lines = new List<string>();
        foreach (int type in new[] { 0, ForestRouteSetup.CrawlAgentType })
        {
            var filter = new NavMeshQueryFilter { agentTypeID = type, areaMask = NavMesh.AllAreas };
            foreach (string name in new[] { "Supply house", "Medical house" })
            {
                var house = route.transform.Find(name);
                Vector3 from = house.TransformPoint(new Vector3(0, .05f, 4.8f));
                Vector3 to = house.TransformPoint(new Vector3(0, .05f, 2));
                var path = new NavMeshPath();
                bool ok = NavMesh.SamplePosition(from, out var start, .6f, filter) && NavMesh.SamplePosition(to, out var end, .6f, filter)
                    && NavMesh.CalculatePath(start.position, end.position, filter, path) && path.status == NavMeshPathStatus.PathComplete;
                lines.Add((ok ? "PASS " : "FAIL ") + name + ": porch→entry agent=" + type);
            }
        }
        return string.Join("\n", lines);
    }
}
