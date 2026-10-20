using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using ZombieSurvival.AI;
using ZombieSurvival.Combat;
using ZombieSurvival.Interaction;
using ZombieSurvival.Inventory;
using ZombieSurvival.Persistence;
using ZombieSurvival.Services;

/// <summary>Authoring additions only: no scene opening/saving and no replacement of existing route objects.</summary>
public static class ForestRouteSetup
{
    public const string ScenePath = "Assets/CharacterTesting/CharacterTest.unity";
    public const string RootName = "Forest Route - First Expedition";
    public const string Folder = "Assets/Environment/ForestRoute";
    public const string HousePath = Folder + "/ExpeditionHouse.prefab";
    public const string HouseModel = Folder + "/Models/ApocalypseHouse.fbx";
    public const int CrawlAgentType = -1372625422;

    private static void RequireEdit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Настройка маршрута доступна только в Edit Mode после компиляции.");
    }
    private static Scene OpenScene()
    {
        RequireEdit();
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.path == ScenePath).ToArray();
        if (scenes.Length != 1) throw new InvalidOperationException("CharacterTest должна быть уже открыта. Команда не открывает и не сохраняет сцены.");
        return scenes[0];
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
    private static void Set(UnityEngine.Object target, string field, object value)
    {
        var data = new SerializedObject(target); var property = data.FindProperty(field);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + ": нет поля " + field);
        if (value is string text) property.stringValue = text;
        else if (value is int number) property.intValue = number;
        else if (value is float scalar) property.floatValue = scalar;
        else if (value is Vector3 vector) property.vector3Value = vector;
        else if (value is bool flag) property.boolValue = flag;
        else property.objectReferenceValue = value as UnityEngine.Object;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static GameObject Child(Transform parent, string name)
    {
        var existing = parent.Find(name); if (existing != null) return existing.gameObject;
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Добавить первую вылазку"); return go;
    }
    private static WorldSaveRegistry Registry(Scene scene) => UnityEngine.Object.FindObjectsByType<WorldSaveRegistry>(FindObjectsInactive.Include)
        .Single(r => r.gameObject.scene == scene);

    /// <summary>Append only. Existing registry order and IDs are preserved.</summary>
    public static void RegisterNewRoots(Scene scene, GameObject route)
    {
        var registry = Registry(scene); Undo.RecordObject(registry, "Зарегистрировать маршрут");
        var data = new SerializedObject(registry); var list = data.FindProperty("sceneObjects");
        var known = new HashSet<UnityEngine.Object>();
        for (int i = 0; i < list.arraySize; i++) known.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
        foreach (var root in route.GetComponentsInChildren<WorldSaveObject>(true))
        {
            if (root.transform.parent != null && root.transform.parent.GetComponentInParent<WorldSaveObject>(true) != null)
                throw new InvalidOperationException("Вложенный WorldSaveObject: " + root.name);
            if (known.Add(root)) { int index = list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = root; }
        }
        data.ApplyModifiedProperties();
    }
    private static void NewIds(GameObject root)
    {
        foreach (var id in root.GetComponentsInChildren<SaveIdentity>(true))
            Set(id, "persistentId", "expedition-" + Guid.NewGuid().ToString("N"));
    }
    private static void SaveRoot(GameObject go)
    {
        var root = go.GetComponent<WorldSaveObject>() ?? go.AddComponent<WorldSaveObject>();
        Set(root.GetComponent<SaveIdentity>(), "persistentId", "expedition-" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>Build once, outside the imported source folder. Existing authored house assets are never overwritten.</summary>
    [MenuItem("Tools/First Expedition/Create House Asset (No Scene Save)")]
    public static void CreateHouseAsset()
    {
        RequireEdit(); if (AssetDatabase.LoadAssetAtPath<GameObject>(HousePath) != null) return;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(HouseModel);
        if (model == null) throw new InvalidOperationException("Не импортирована модель дома: " + HouseModel);
        EnsureFolder(Folder);
        var preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("ExpeditionHouse"); SceneManager.MoveGameObjectToScene(root, preview);
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // FBX carries a 90-degree axis conversion at its root. Door pivots use Unity
            // world-up coordinates; normalize the container without moving any mesh.
            var importedChildren = visual.transform.Cast<Transform>().ToArray();
            foreach (var child in importedChildren) child.SetParent(root.transform, true);
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); visual.transform.localScale = Vector3.one;
            foreach (var child in importedChildren) child.SetParent(visual.transform, true);
            visual.name = "House model";
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                string n = renderer.name; int marker = n.LastIndexOf("__Static_", StringComparison.Ordinal);
                string materialName = marker >= 0 ? n.Substring(marker + "__Static_".Length) : n.Contains("Knob") ? "metal" : "wood";
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/ApocalypseHouse_Unity/Materials/" + materialName + ".mat");
                if (material == null)
                {
                    // The OBJ importer exported only used overrides, not all GLB materials.
                    string path = Folder + "/Materials/House/" + materialName + ".mat";
                    EnsureFolder(Folder + "/Materials/House");
                    material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        var texture = AssetDatabase.FindAssets(materialName + " t:Texture2D", new[] { "Assets/Environment/ApocalypseHouse_Unity" })
                            .Select(AssetDatabase.GUIDToAssetPath).Where(p => System.IO.Path.GetFileNameWithoutExtension(p) == materialName)
                            .Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault();
                        if (texture == null) throw new InvalidOperationException("Нет текстуры дома: " + materialName);
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name = materialName;
                        material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .2f);
                        AssetDatabase.CreateAsset(material, path);
                    }
                }
                renderer.sharedMaterials = Enumerable.Repeat(material, Math.Max(1, renderer.sharedMaterials.Length)).ToArray();
            }
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = filter.name;
                bool shell = n.StartsWith("Exterior", StringComparison.Ordinal) || n.StartsWith("Partition", StringComparison.Ordinal)
                    || n.StartsWith("Foundation", StringComparison.Ordinal) || n.StartsWith("Floors", StringComparison.Ordinal)
                    || n.StartsWith("Cellar_Shell", StringComparison.Ordinal) || n.StartsWith("Attic", StringComparison.Ordinal)
                    || n.StartsWith("Roof__", StringComparison.Ordinal) || n.StartsWith("Porch__", StringComparison.Ordinal);
                if (shell && filter.sharedMesh != null && filter.GetComponent<Collider>() == null)
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            AddDoor(visual.transform, "Front door", new Vector3(-.38f, 0, 3.02f), new Vector3(.38f, 1, 0), new Vector3(.7f, 1.97f, .035f),
                t => t.name.Contains("DOOR_FRONT") || t.name.Contains("Door_Front_"));
            AddDoor(visual.transform, "Bathroom door", new Vector3(-.99f, 0, 1.78f), new Vector3(.33f, 1, 0), new Vector3(.62f, 1.97f, .035f),
                t => t.name.Contains("DOOR_BATHROOM"));
            AddDoor(visual.transform, "Bedroom door", new Vector3(-.35f, 0, -1.35f), new Vector3(0, 1, .38f), new Vector3(.035f, 1.97f, .74f),
                t => t.name.Contains("DOOR_BEDROOM"));
            PrefabUtility.SaveAsPrefabAsset(root, HousePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void AddDoor(Transform model, string name, Vector3 pivot, Vector3 center, Vector3 size, Func<Transform, bool> match)
    {
        var parts = model.GetComponentsInChildren<MeshRenderer>(true).Where(r => match(r.transform)).ToArray();
        if (parts.Length == 0) throw new InvalidOperationException("Нет геометрии двери: " + name);
        var go = new GameObject(name); go.transform.SetParent(model, false); go.transform.localPosition = pivot;
        foreach (var part in parts) part.transform.SetParent(go.transform, true);
        var leaf = go.AddComponent<BoxCollider>(); leaf.center = center; leaf.size = size;
        if (name == "Front door") { center = new Vector3(center.x, 1.12f, center.z); size = new Vector3(size.x, 1.76f, size.z); leaf.center = center; leaf.size = size; }
        var door = go.AddComponent<DoorInteractable>(); Set(door, "hinge", go.transform); Set(door, "leaf", leaf);
        Set(door, "displayName", name == "Front door" ? "Входная дверь" : "Межкомнатная дверь");
        Set(door, "openAngle", name == "Front door" ? 90f : 85f); Set(door, "collisionMargin", .002f);
        SaveRoot(go); go.AddComponent<WorldDoorSaveAdapter>();
        go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        var obstacle = go.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = center; obstacle.size = size; obstacle.carving = true;
    }

    public static GameObject EnsureRouteRoot()
    {
        var scene = OpenScene(); var roots = scene.GetRootGameObjects().Where(r => r.name == RootName).ToArray();
        if (roots.Length > 1) throw new InvalidOperationException("Несколько корней маршрута; ничего не изменено.");
        if (roots.Length == 1) return roots[0];
        Registry(scene); // Validate before creating anything.
        var root = new GameObject(RootName); SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Добавить первую вылазку"); return root;
    }

    /// <summary>Creates a physical container and seeds it once. Repeat setup does not refill existing contents.</summary>
    public static GameObject AddContainer(Transform parent, string name, Vector3 localPosition, Material material,
        ItemDefinition[] items, int[] amounts)
    {
        if (parent.Find(name) != null) return parent.Find(name).gameObject;
        if (items == null || amounts == null || items.Length != amounts.Length || items.Any(i => i == null)
            || amounts.Any(a => a <= 0)) throw new InvalidOperationException("Некорректный начальный лут: " + name);
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition; go.transform.localScale = new Vector3(1.1f, .65f, .6f);
        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        Undo.RegisterCreatedObjectUndo(go, "Добавить контейнер вылазки");
        var storage = go.AddComponent<ItemStorage>(); go.AddComponent<StorageContainer>(); SaveRoot(go);
        foreach (var pair in items.Select((item, i) => new { item, amount = amounts[i] }))
            if (!storage.TryAddAll(pair.item, pair.amount)) throw new InvalidOperationException("Начальный лут не помещается: " + name);
        EditorUtility.SetDirty(storage); return go;
    }

    public static NavMeshSurface AddSurface(Transform route, int agentType)
    {
        var go = Child(route, agentType == 0 ? "Route navigation standing" : "Route navigation crawl");
        var surface = go.GetComponent<NavMeshSurface>(); if (surface != null) return surface;
        surface = go.AddComponent<NavMeshSurface>(); surface.agentTypeID = agentType;
        surface.collectObjects = CollectObjects.Volume; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.center = new Vector3(30, 6, -115); surface.size = new Vector3(140, 26, 160);
        surface.overrideVoxelSize = true; surface.voxelSize = .05f;
        return surface;
    }

    // Original proposal retained for reference; use UserLocationSetup for the authored district.
    public static void Run()
    {
        var scene = OpenScene(); Registry(scene);
        CreateHouseAsset();
        var root = EnsureRouteRoot();
        var concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/KursHub/Materials/Concrete.mat");
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/ApocalypseHouse_Unity/Materials/darkwood.mat");
        AddGround(root.transform, "Route ground", new Vector3(62, -.15f, -69.5f), new Vector3(44, .35f, 53), concrete);
        AddGround(root.transform, "Kurs approach connector", new Vector3(19, -.15f, -86), new Vector3(46, .35f, 6), concrete);
        AddHouse(root.transform, "Supply house", new Vector3(60, .025f, -82), false, wood);
        AddHouse(root.transform, "Medical house", new Vector3(74, .025f, -56), true, wood);
        AddZombie(root.transform, "Supply house zombie", new Vector3(55, .03f, -80), 4f);
        AddZombie(root.transform, "Medical house zombie", new Vector3(79, .03f, -57), 4f);
        AddZombie(root.transform, "Forest edge zombie", new Vector3(89, .03f, -51), 6f);
        AddZombie(root.transform, "Supply house indoor zombie", new Vector3(57.9f, .03f, -81.86f), 1.2f);
        AddZombie(root.transform, "Medical house indoor zombie", new Vector3(71.9f, .03f, -55.86f), 1.2f);
        AddGuard(root.transform, "Entrance ally left", "Assets/Survivalist/Prefab/Survivalist (1).prefab", new Vector3(-7, 0, -94));
        AddGuard(root.transform, "Entrance ally right", "Assets/Survivalist/Prefab/Survivalist (2).prefab", new Vector3(7, 0, -94));
        AddPlaque(root.transform, "Route instructions", new Vector3(37, .045f, -86),
            "ПЕРВАЯ ВЫЛАЗКА\nВосток: склад → медпункт → лес\nE — двери и контейнеры\nОпасайтесь заражённых и возвращайтесь в ТЦ");
        AddPlaque(root.transform, "Supply instructions", new Vector3(58, .045f, -89), "СКЛАД\nE — открыть дверь\nВнутри: припасы и детали");
        AddPlaque(root.transform, "Medical instructions", new Vector3(72, .045f, -63), "МЕДПУНКТ\nE — открыть дверь\nВнутри: медикаменты и вода");
        AddPlaque(root.transform, "Forest instructions", new Vector3(82, .045f, -56), "ЛЕС →\nОстерегайтесь заражённых\nОбратно — к входу в ТЦ");
        AddSurface(root.transform, 0); AddSurface(root.transform, CrawlAgentType);
        RegisterNewRoots(scene, root); EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("FOREST_ROUTE_ADDED: дома/двери/контейнеры/5 зомби/2 союзника, отдельные поверхности. Сцена НЕ сохранена. Навигация запекается отдельной командой.");
    }

    public static void AddGround(Transform parent, string name, Vector3 center, Vector3 size, Material material)
    {
        if (parent.Find(name) != null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = center; go.transform.localScale = size;
        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        Undo.RegisterCreatedObjectUndo(go, "Добавить основание маршрута");
    }
    private static ItemDefinition Item(string path) => AssetDatabase.LoadAssetAtPath<ItemDefinition>(path)
        ?? throw new InvalidOperationException("Нет существующего типа предмета: " + path);
    public static void AddHouse(Transform parent, string name, Vector3 position, bool medical, Material material)
    {
        if (parent.Find(name) != null) return;
        var house = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HousePath), parent);
        house.name = name; house.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 180, 0));
        house.transform.localScale = Vector3.one * 1.4f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(house.transform);
        Undo.RegisterCreatedObjectUndo(house, "Добавить дом вылазки"); NewIds(house);
        var items = medical ? new[] { Item("Assets/CharacterTesting/Progression/MedicalKit.asset"), Item("Assets/CharacterTesting/Survival/Water.asset") }
            : new[] { Item("Assets/CharacterTesting/Items/CannedFood.asset"), Item("Assets/CharacterTesting/Items/Repair/WeaponParts.asset") };
        AddContainer(house.transform, medical ? "Medical supplies" : "Warehouse supplies", new Vector3(1.5f, .325f, 1.35f), material, items, medical ? new[] { 2, 2 } : new[] { 3, 4 });
    }
    public static void AddZombie(Transform parent, string name, Vector3 position, float radius)
    {
        if (parent.Find(name) != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Enemies/ZombieMaleActor.prefab");
        if (prefab == null) throw new InvalidOperationException("Не найден общий префаб зомби.");
        var npc = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); npc.name = name;
        npc.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 180, 0));
        PrefabUtility.RecordPrefabInstancePropertyModifications(npc.transform);
        Undo.RegisterCreatedObjectUndo(npc, "Добавить зомби маршрута"); NewIds(npc);
        var brain = npc.GetComponent<EnemyBrain>(); if (brain == null) throw new InvalidOperationException("На префабе отсутствует EnemyBrain.");
        var body = npc.GetComponent<CapsuleCollider>();
        if (body != null) { var center = body.center; center.y = Mathf.Max(center.y, body.height * .5f + .05f); Set(body, "m_Center", center); }
        Set(brain, "patrolRadius", radius);
        if (npc.GetComponent<WorldSaveObject>() == null) SaveRoot(npc);
        if (npc.GetComponent<WorldActorSaveAdapter>() == null) npc.AddComponent<WorldActorSaveAdapter>();
        if (npc.GetComponent<WorldIdentitySaveAdapter>() == null) npc.AddComponent<WorldIdentitySaveAdapter>();
    }
    private static RuntimeAnimatorController GuardIdle()
    {
        string path = Folder + "/SurvivalistIdle.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path); if (controller != null) return controller;
        var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Survivalist/StarterAssets/ThirdPersonController/Character/Animations/Stand--Idle.anim.fbx")
            .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__", StringComparison.Ordinal));
        if (clip == null) throw new InvalidOperationException("Не найден реальный Stand--Idle клип.");
        string clipPath = Folder + "/SurvivalistIdle.anim";
        var authored = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (authored == null)
        {
            authored = UnityEngine.Object.Instantiate(clip); authored.name = "Survivalist Idle";
            var settings = AnimationUtility.GetAnimationClipSettings(authored); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(authored, settings); AssetDatabase.CreateAsset(authored, clipPath);
        }
        controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var state = controller.layers[0].stateMachine.AddState("Idle"); state.motion = authored;
        controller.layers[0].stateMachine.defaultState = state; AssetDatabase.SaveAssetIfDirty(controller); return controller;
    }
    public static void AddGuard(Transform parent, string name, string visualPath, Vector3 position)
    {
        if (parent.Find(name) != null) return;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
        if (source == null || source.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
            throw new InvalidOperationException("Союзнику требуется чистый визуальный префаб: " + visualPath);
        var npc = new GameObject(name); npc.transform.SetParent(parent, false); npc.transform.position = position;
        Undo.RegisterCreatedObjectUndo(npc, "Добавить союзника у входа");
        var identity = npc.AddComponent<ActorIdentity>(); Set(identity, "faction", (int)ActorFaction.Survivor); Set(identity, "hostileFactions", 0);
        npc.AddComponent<Health>(); var collider = npc.AddComponent<CapsuleCollider>(); collider.center = new Vector3(0, .9f, 0); collider.height = 1.8f; collider.radius = .3f;
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, npc.transform);
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
            {
                if (material == null) throw new InvalidOperationException("Пустой материал союзника: " + renderer.name);
                return AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Survivalist/" + material.name + ".mat")
                    ?? throw new InvalidOperationException("Не подготовлена URP копия материала союзника: " + material.name);
            }).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) { animator.runtimeAnimatorController = GuardIdle(); animator.applyRootMotion = false; PrefabUtility.RecordPrefabInstancePropertyModifications(animator); }
        // No CombatTarget, EnemyBrain, weapon or damage reaction: these allies are presentation only at this stage.
        npc.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        SaveRoot(npc); npc.AddComponent<WorldActorSaveAdapter>(); npc.AddComponent<WorldIdentitySaveAdapter>();
    }
    public static void AddPlaque(Transform parent, string name, Vector3 position, string caption)
    {
        if (parent.Find(name) != null) return;
        var go = Child(parent, name); go.transform.position = position;
        var text = go.AddComponent<TextMesh>(); text.text = caption; WorldSpacePresentationSetup.ConfigurePlaque(text);
        go.GetComponentInChildren<Canvas>().transform.SetPositionAndRotation(position, Quaternion.Euler(90, 0, 0));
    }

    /// <summary>Explicit one-time relocation of the existing three actors; no stock, money or IDs are recreated.</summary>
    public static void PlaceExistingServices()
    {
        var scene = OpenScene(); var traders = UnityEngine.Object.FindObjectsByType<Trader>(FindObjectsInactive.Include).Where(t => t.gameObject.scene == scene).ToArray();
        string[] profiles = { "equipment", "food_medicine", "weapons_ammo" };
        Vector3[] positions = { new Vector3(-25, 0, -120), new Vector3(0, 0, -185), new Vector3(25, 0, -120) };
        var chosen = profiles.Select(p => traders.Single(t => t.Profile != null && t.Profile.Id == p)).ToArray();
        for (int i = 0; i < chosen.Length; i++)
        {
            Undo.RecordObject(chosen[i].transform, "Разместить существующего торговца в ТЦ");
            chosen[i].transform.SetPositionAndRotation(positions[i], Quaternion.Euler(0, i == 1 ? 0 : 180, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(chosen[i].transform);
        }
        var route = EnsureRouteRoot();
        if (route.transform.Find("Kurs workbench") == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/T4WorkbenchSession.prefab");
            var bench = (GameObject)PrefabUtility.InstantiatePrefab(prefab, route.transform); bench.name = "Kurs workbench";
            bench.transform.SetPositionAndRotation(new Vector3(18, 0, -143), Quaternion.identity); NewIds(bench);
            if (bench.GetComponent<WorldSaveObject>() == null) SaveRoot(bench);
            Undo.RegisterCreatedObjectUndo(bench, "Добавить отдельный верстак ТЦ");
        }
        RegisterNewRoots(scene, route); EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/First Expedition/Bake Route Navigation Only")]
    public static void BakeRoute()
    {
        var scene = OpenScene(); var route = scene.GetRootGameObjects().Single(r => r.name == RootName);
        EnsureFolder(Folder);
        foreach (var surface in route.GetComponentsInChildren<NavMeshSurface>(true))
        {
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Пустая навигация маршрута: " + surface.name);
            string path = Folder + (surface.agentTypeID == 0 ? "/RouteStanding.asset" : "/RouteCrawl.asset");
            var stored = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (stored == null) { AssetDatabase.CreateAsset(surface.navMeshData, path); stored = surface.navMeshData; }
            else if (stored != surface.navMeshData)
            {
                EditorUtility.CopySerialized(surface.navMeshData, stored); surface.RemoveData(); surface.navMeshData = stored; surface.AddData();
            }
            EditorUtility.SetDirty(stored); AssetDatabase.SaveAssetIfDirty(stored);
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
