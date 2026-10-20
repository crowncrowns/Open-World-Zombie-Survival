using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Services;

/// <summary>Explicit authoring for the user's October 20 layout. Never opens or saves a scene.</summary>
public static class UserLocationSetup
{
    public static void FixVisualCopies()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ForestRouteSetup.ScenePath || EditorSceneManager.IsPreviewScene(scene))
            throw new InvalidOperationException("Откройте CharacterTest вне Play и Prefab Mode.");
        foreach (var visual in UnityEngine.Object.FindObjectsByType<ServiceVisual>(FindObjectsInactive.Include).Where(v => v.gameObject.scene == scene))
        {
            var trader = visual.GetComponent<Trader>();
            if (trader == null || visual.Instance == null) continue;
            // Only direct extra prefab roots in this managed anchor are known visual copies.
            // Never convert rigs, bones, props or unrelated scene characters by name.
            var anchor = visual.Instance.transform.parent;
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(visual.Instance);
            if (source == null || anchor == null || anchor.parent != trader.transform) continue;
            var extras = anchor.Cast<Transform>().Where(t => t.gameObject != visual.Instance
                && PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject) == source
                && Vector3.Distance(t.position, visual.Instance.transform.position) > 20).ToArray();
            foreach (var extra in extras)
            {
                var position = extra.position; var rotation = extra.rotation; var scale = extra.lossyScale;
                var copy = TraderCopyTools.Duplicate(trader);
                var copiedVisual = copy.GetComponent<ServiceVisual>();
                var copiedAnchor = copiedVisual.Instance.transform.parent;
                foreach (var unwanted in copiedAnchor.Cast<Transform>().Where(t => t.gameObject != copiedVisual.Instance).ToArray())
                    Undo.DestroyObjectImmediate(unwanted.gameObject);
                copy.transform.SetParent(ForestRouteSetup.EnsureRouteRoot().transform, true);
                // Anchor and wrapper are identity-scaled in this authored layout. Preserve the visible world pose.
                copy.transform.SetPositionAndRotation(position, rotation * Quaternion.Inverse(copiedVisual.Instance.transform.localRotation));
                copy.transform.localScale = Vector3.one;
                copiedVisual.Instance.transform.position = position;
                copiedVisual.Instance.transform.rotation = rotation;
                copiedVisual.Instance.transform.localScale = scale;
                copiedVisual.CaptureManualPose(); copiedVisual.ApplyModel();
                copy.name = trader.Profile.Id == "weapons_ammo" ? "ТЦ — оружие и патроны" : "ТЦ — еда и медицина";
                Undo.DestroyObjectImmediate(extra.gameObject);
            }
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/First Expedition/Populate Current User Layout (No Save)")]
    public static void Run()
    {
        if (SceneManager.GetActiveScene().path != ForestRouteSetup.ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Откройте CharacterTest вне Play.");
        FixVisualCopies();
        // The old zombie capsule extended below its navigation feet. Terrain exposed the
        // penetration during load validation. Keep the root/rig/IDs and lift only its collider.
        foreach (var brain in UnityEngine.Object.FindObjectsByType<ZombieSurvival.AI.EnemyBrain>(FindObjectsInactive.Include)
            .Where(b => b.gameObject.scene == SceneManager.GetActiveScene()))
        {
            var body = brain.GetComponent<CapsuleCollider>(); if (body == null || body.direction != 1) continue;
            var so = new SerializedObject(body); var center = body.center; center.y = Mathf.Max(center.y, body.height * .5f + .05f);
            so.FindProperty("m_Center").vector3Value = center; so.ApplyModifiedProperties();
        }
        ForestRouteSetup.CreateHouseAsset();
        var route = ForestRouteSetup.EnsureRouteRoot();
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/ApocalypseHouse_Unity/Materials/darkwood.mat");
        var concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/KursHub/Materials/Concrete.mat");
        ForestRouteSetup.AddGround(route.transform, "Kurs east footpath", new Vector3(38, -.15f, -83), new Vector3(36, .3f, 6), concrete);
        if (route.transform.Find("Supply house") == null)
        {
            var oldHouse = GameObject.Find("Apocalypse_House_Unity_OPTIMIZED");
            if (oldHouse == null) throw new InvalidOperationException("Исходный дом пользователя отсутствует.");
            ForestRouteSetup.AddHouse(route.transform, "Supply house", oldHouse.transform.position, false, wood);
            route.transform.Find("Supply house").rotation = oldHouse.transform.rotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(route.transform.Find("Supply house"));
            // Retain the original imported object and placement; its single OBJ cannot have moving doors.
            foreach (var renderer in oldHouse.GetComponentsInChildren<Renderer>(true))
            { Undo.RecordObject(renderer, "Скрыть заменённую статическую оболочку дома"); renderer.enabled = false; }
        }
        ForestRouteSetup.AddHouse(route.transform, "Medical house", new Vector3(113, .025f, -77), true, wood);
        foreach (string name in new[] { "Supply house", "Medical house" })
        {
            var house = route.transform.Find(name);
            AddRamp(house, "Porch access ramp", new Vector3(0, -.158f, 5.5f), new Vector3(1.6f, .12f, 2.4f), 12, wood);
            AddRamp(house, "Door threshold ramp", new Vector3(0, .015f, 3), new Vector3(1.2f, .12f, 1.6f), -5.36f, wood);
        }
        AddPickup(route.transform.Find("Supply house"), "Loose food", new Vector3(-1.5f, .5f, 1.25f), "Assets/CharacterTesting/Items/CannedFood.asset", 2);
        AddPickup(route.transform.Find("Medical house"), "Loose medicine", new Vector3(-1.5f, .5f, 1.25f), "Assets/CharacterTesting/Progression/MedicalKit.asset", 1);
        ForestRouteSetup.AddZombie(route.transform, "Supply house zombie", new Vector3(87, .3f, -113), 4);
        ForestRouteSetup.AddZombie(route.transform, "Medical house zombie", new Vector3(108, .03f, -85), 4);
        ForestRouteSetup.AddZombie(route.transform, "Forest edge zombie", new Vector3(115, .03f, -63), 5);
        ForestRouteSetup.AddZombie(route.transform, "Supply house indoor zombie", route.transform.Find("Supply house").TransformPoint(new Vector3(-1.5f, .05f, .1f)), 1);
        ForestRouteSetup.AddZombie(route.transform, "Medical house indoor zombie", route.transform.Find("Medical house").TransformPoint(new Vector3(-1.5f, .05f, .1f)), 1);
        ForestRouteSetup.AddGuard(route.transform, "Entrance ally left", "Assets/Survivalist/Prefab/Survivalist (1).prefab", new Vector3(-7, 0, -94));
        ForestRouteSetup.AddGuard(route.transform, "Entrance ally right", "Assets/Survivalist/Prefab/Survivalist (2).prefab", new Vector3(7, 0, -94));
        if (route.transform.Find("Kurs workbench") == null)
        {
            var bench = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/T4WorkbenchSession.prefab"), route.transform);
            bench.name = "Kurs workbench"; bench.transform.SetPositionAndRotation(new Vector3(21, 0, -136), Quaternion.Euler(0, 270, 0));
            Undo.RegisterCreatedObjectUndo(bench, "Добавить верстак в ТЦ");
        }
        ForestRouteSetup.AddPlaque(route.transform, "Route instructions", new Vector3(39, .045f, -83), "ПЕРВАЯ ВЫЛАЗКА\nВосток: жилой двор → склад → медпункт → лес\nE — двери и добыча\nТорговцы и верстак — внутри ТЦ");
        ForestRouteSetup.AddPlaque(route.transform, "Supply instructions", new Vector3(83, .3f, -113.5f), "СКЛАД\nE — двери и припасы");
        ForestRouteSetup.AddPlaque(route.transform, "Medical instructions", new Vector3(113, .045f, -86), "МЕДПУНКТ\nE — двери и медикаменты\nДальше на север — лес");
        foreach (var type in new[] { 0, ForestRouteSetup.CrawlAgentType })
        {
            var surface = ForestRouteSetup.AddSurface(route.transform, type);
            surface.center = new Vector3(72, 6, -109); surface.size = new Vector3(180, 26, 150);
        }
        SaveIdentityAuthoring.ProcessNewObjects();
        ForestRouteSetup.RegisterNewRoots(SceneManager.GetActiveScene(), route);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void AddPickup(Transform house, string name, Vector3 position, string itemPath, int amount)
    {
        if (house.Find(name) != null) return;
        var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
        var existingTable = house.Find(name + " shelf");
        var table = existingTable != null ? existingTable.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube); table.name = name + " shelf";
        table.transform.SetParent(house, false); table.transform.localPosition = position - Vector3.up * .18f;
        table.transform.localScale = new Vector3(.7f, .35f, .5f);
        if (definition == null || definition.WorldPrefab == null) throw new InvalidOperationException("Нет исходного pickup: " + itemPath);
        var pickup = (GameObject)PrefabUtility.InstantiatePrefab(definition.WorldPrefab, house); pickup.name = name;
        pickup.transform.SetParent(house, false); pickup.transform.localPosition = position + Vector3.up * .08f;
        pickup.GetComponent<WorldItem>().Initialize(definition, amount);
        if (existingTable == null) Undo.RegisterCreatedObjectUndo(table, "Добавить полку");
        Undo.RegisterCreatedObjectUndo(pickup, "Добавить добычу");
    }

    private static void AddRamp(Transform house, string name, Vector3 position, Vector3 scale, float angle, Material material)
    {
        if (house.Find(name) != null) return;
        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube); ramp.name = name; ramp.transform.SetParent(house, false);
        ramp.transform.localPosition = position; ramp.transform.localRotation = Quaternion.Euler(angle, 0, 0); ramp.transform.localScale = scale;
        ramp.GetComponent<Renderer>().sharedMaterial = material; Undo.RegisterCreatedObjectUndo(ramp, "Добавить доступный порог дома");
    }
}
