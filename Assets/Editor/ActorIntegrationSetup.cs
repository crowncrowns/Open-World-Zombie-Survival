using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using ZombieSurvival.AI;
using ZombieSurvival.Combat;
using ZombieSurvival.Interaction;
using ZombieSurvival.Player;

public static class ActorIntegrationSetup
{
    private const string Lab = "Assets/CharacterTesting";
    private const string EnemyFolder = "Assets/Characters/Enemies";
    private const string ZombiePrefab = EnemyFolder + "/ZombieMaleActor.prefab";

    [MenuItem("Tools/Character Lab/Set Up Actor and Stealth Tests")]
    public static void Run()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before setup.");
        if (!AssetDatabase.IsValidFolder(EnemyFolder)) AssetDatabase.CreateFolder("Assets/Characters", "Enemies");
        ConfigurePlayer(); ExportZombie();
        var scene = EditorSceneManager.OpenScene(Lab + "/CharacterTest.unity");
        if (GameObject.Find("06 - Actors and stealth") == null) CreateArena();
        foreach (var door in Object.FindObjectsByType<DoorInteractable>())
        {
            if (door.GetComponent<NavMeshModifier>() == null) door.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var leaf = door.GetComponentInChildren<BoxCollider>();
            if (leaf != null && leaf.GetComponent<NavMeshObstacle>() == null)
            {
                var obstacle = leaf.gameObject.AddComponent<NavMeshObstacle>();obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = leaf.size;obstacle.center = leaf.center;obstacle.carving = true;
            }
        }
        var navigation = GameObject.Find("Character lab navigation");
        if (navigation == null) navigation = new GameObject("Character lab navigation");
        var surface = navigation.GetComponent<NavMeshSurface>();if (surface == null) surface = navigation.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Volume;surface.center = new Vector3(20f, 5f, 20f);surface.size = new Vector3(130f, 30f, 125f);
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize = true;surface.voxelSize = .1f;
        surface.BuildNavMesh();
        const string navPath = Lab + "/CharacterTestNavMesh.asset";
        var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
        if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData, navPath);
        else if (existing != surface.navMeshData)
        {
            EditorUtility.CopySerialized(surface.navMeshData, existing);surface.RemoveData();surface.navMeshData = existing;surface.AddData();
        }
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("ACTOR_SETUP_OK: source-scene zombie prefab, player contracts, stealth arena, controls and baked navigation.");
    }
    private static void ConfigurePlayer()
    {
        string path = Lab + "/Prefabs/CharacterTestPlayer.prefab";var player = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Identity(player, ActorFaction.Survivor, FactionMask.Infected | FactionMask.Wildlife);
            var target = player.GetComponent<CombatTarget>();if (target == null) target = player.AddComponent<CombatTarget>();
            Set(target, "useColliderCenter", true);
            if (player.GetComponent<PlayerNoiseEmitter>() == null) player.AddComponent<PlayerNoiseEmitter>();
            var modifier = player.GetComponent<NavMeshModifier>();if (modifier == null) modifier = player.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild = true;
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            foreach (string action in new[] { "Move", "Jump", "Roll", "Prone", "Interact" })
                if (actions.FindAction("Player/" + action) == null) throw new System.InvalidOperationException("Missing Player/" + action);
            PrefabUtility.SaveAsPrefabAsset(player, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
    }
    private static void ExportZombie()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefab) != null) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var source = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include).Single(b => b.gameObject.name == "ZombieMale");
        var actor = Object.Instantiate(source.gameObject);actor.name = "ZombieMaleActor";
        try
        {
            if (PrefabUtility.IsPartOfPrefabInstance(actor)) PrefabUtility.UnpackPrefabInstance(actor, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);actor.transform.localScale = Vector3.one;
            Identity(actor, ActorFaction.Infected, FactionMask.Survivor | FactionMask.Neutral | FactionMask.Wildlife);
            var legacy = actor.GetComponent<DamageableHealth>();if (legacy != null) Object.DestroyImmediate(legacy);
            if (actor.GetComponent<Health>() == null) actor.AddComponent<Health>();
            var target = actor.GetComponent<CombatTarget>();if (target == null) target = actor.AddComponent<CombatTarget>();Set(target, "useColliderCenter", true);
            var modifier = actor.GetComponent<NavMeshModifier>();if (modifier == null) modifier = actor.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild = true;
            string controllerPath = EnemyFolder + "/ZombieMaleActor.controller";
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(actor.GetComponent<Animator>().runtimeAnimatorController), controllerPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            var sm = controller.layers[0].stateMachine;
            foreach (var transition in sm.anyStateTransitions) transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            var death = sm.AddState("Dead");death.motion = AssetDatabase.LoadAllAssetsAtPath("Assets/Kevin Iglesias/Human Animations/Animations/Female/Combat/HumanF@Death01.fbx").OfType<AnimationClip>().Single(c => c.name == "HumanF@Death01");
            var enter = sm.AddAnyStateTransition(death);enter.hasExitTime = false;enter.duration = .1f;enter.canTransitionToSelf = false;enter.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            var exit = death.AddTransition(sm.defaultState);exit.hasExitTime = false;exit.duration = 0f;exit.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            actor.GetComponent<Animator>().runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(actor, ZombiePrefab);
        }
        finally { Object.DestroyImmediate(actor); }
    }
    private static void Identity(GameObject obj, ActorFaction faction, FactionMask enemies)
    {
        var id = obj.GetComponent<ActorIdentity>();if (id == null) id = obj.AddComponent<ActorIdentity>();
        Set(id, "faction", (int)faction);Set(id, "hostileFactions", (int)enemies);
    }
    private static void CreateArena()
    {
        var root = new GameObject("06 - Actors and stealth");
        var oldBoundary = GameObject.Find("Boundary east");if (oldBoundary != null) oldBoundary.SetActive(false);
        Box(root.transform, "East wall south", new Vector3(32, 1.5f, -27), new Vector3(1, 3, 10));
        Box(root.transform, "East wall north", new Vector3(32, 1.5f, 9), new Vector3(1, 3, 46));
        Box(root.transform, "Stealth arena floor", new Vector3(56, -.5f, -8), new Vector3(48, 1, 44));
        Box(root.transform, "Stealth arena east wall", new Vector3(80, 1.5f, -8), new Vector3(1, 3, 44));
        Box(root.transform, "Stealth arena north wall", new Vector3(56, 1.5f, 14), new Vector3(48, 3, 1));
        Box(root.transform, "Stealth arena south wall", new Vector3(56, 1.5f, -30), new Vector3(48, 3, 1));
        Box(root.transform, "Stealth cover wall", new Vector3(53, 1.5f, -7), new Vector3(.5f, 3, 18));
        Box(root.transform, "Crouch cover 1.15 m", new Vector3(43, .575f, 3), new Vector3(10, 1.15f, .5f));
        Box(root.transform, "Crawl cover 0.85 m", new Vector3(66, .425f, 5), new Vector3(10, .85f, .5f));
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefab), root.transform);
        actor.transform.SetPositionAndRotation(new Vector3(66, .05f, -8), Quaternion.Euler(0, 270, 0));
        var brain = actor.GetComponent<EnemyBrain>();Set(brain, "patrolRadius", 6f);Set(brain, "sightRange", 24f);Set(brain, "hearingRange", 35f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
        var noise = Box(root.transform, "Noise console", new Vector3(39, 1, -20), new Vector3(1, 2, 1));
        noise.AddComponent<NoiseTestInteractable>();Set(noise.GetComponent<NoiseTestInteractable>(), "displayName", "Отвлекающий шум");
        var reset = Box(root.transform, "Zombie reset console", new Vector3(39, 1, -17), new Vector3(1, 2, 1));
        var resetControl = reset.AddComponent<ActorTestInteractable>();Set(resetControl, "targetActor", actor);Set(resetControl, "resetActor", true);Set(resetControl, "displayName", "Сброс зомби");
        var damage = Box(root.transform, "Zombie damage console", new Vector3(39, 1, -14), new Vector3(1, 2, 1));
        var damageControl = damage.AddComponent<ActorTestInteractable>();Set(damageControl, "targetActor", actor);Set(damageControl, "displayName", "Тест урона зомби");
        Sign(root.transform, "ACTORS / STEALTH\nEast entrance | Zombie from SampleScene\nE consoles: noise / reset / damage", new Vector3(36, 4, -21));
        Sign(root.transform, "BREAK LINE OF SIGHT\nRun is loud | Crouch / crawl are quieter\nZombie searches the last known area", new Vector3(53, 4.5f, -7));
    }
    private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name = name;obj.transform.SetParent(parent, false);obj.transform.localPosition = position;obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Lab + "/Materials/Concrete.mat");return obj;
    }
    private static void Sign(Transform parent, string value, Vector3 position)
    {
        var obj = new GameObject(value.Split('\n')[0]);obj.transform.SetParent(parent, false);obj.transform.position = position;
        var text = obj.AddComponent<TextMesh>();text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text = value;text.fontSize = 64;text.characterSize = .14f;text.anchor = TextAnchor.MiddleCenter;text.alignment = TextAlignment.Center;
        obj.GetComponent<MeshRenderer>().sharedMaterial = text.font.material; WorldSpacePresentationSetup.ConfigurePlaque(text);
    }
    private static void Set(Object target, string name, object value)
    {
        var so = new SerializedObject(target);var property = so.FindProperty(name);
        if (value is bool b) property.boolValue = b;
        else if (value is int i) property.intValue = i;
        else if (value is float f) property.floatValue = f;
        else if (value is string s) property.stringValue = s;
        else property.objectReferenceValue = value as Object;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

