using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombieSurvival.Combat;
using ZombieSurvival.Interaction;
using ZombieSurvival.Player;

public static class PlayerHealthSetup
{
    private const string Root = "Assets/CharacterTesting";
    [MenuItem("Tools/Character Lab/Set Up Health Tests")]
    public static void Run()
    {
        var player = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/CharacterTestPlayer.prefab");
        try
        {
            var health = player.GetComponent<Health>(); if (health == null) health = player.AddComponent<Health>();
            var life = player.GetComponent<PlayerLifeCycle>(); if (life == null) life = player.AddComponent<PlayerLifeCycle>();
            if (player.GetComponent<PlayerFallDamage>() == null) player.AddComponent<PlayerFallDamage>();
            if (player.GetComponentInChildren<PlayerHealthHUD>(true) == null) CreateHUD(player.transform, health, life);
            PrefabUtility.SaveAsPrefabAsset(player, Root + "/Prefabs/CharacterTestPlayer.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        SetupAnimator();
        var scene = EditorSceneManager.OpenScene(Root + "/CharacterTest.unity");
        if (GameObject.Find("05 - Health tests") == null)
        {
            var root = new GameObject("05 - Health tests");
            var spawn = new GameObject("Player respawn point"); spawn.transform.SetParent(root.transform); spawn.transform.position = new Vector3(0f, .05f, -24f);
            Console(root.transform, "Damage console", new Vector3(-9f, 1f, -24f), new Color(.85f, .18f, .13f), false, 25f, "Получить 25 урона");
            Console(root.transform, "Healing console", new Vector3(-12f, 1f, -24f), new Color(.15f, .85f, .4f), true, 100f, "Восстановить здоровье");
            Zone(root.transform, "Damage zone 25 per second", new Vector3(-18f, .35f, -21f), new Vector3(4f, .7f, 4f), 25f);
            Zone(root.transform, "Lethal zone", new Vector3(-25f, .35f, -21f), new Vector3(4f, .7f, 4f), 10000f);
            Sign(root.transform, "HEALTH / E\nRed console: -25 HP | Green: heal\nR after death: respawn", new Vector3(-10.5f, 3f, -24f));
            Sign(root.transform, "DAMAGE ZONE\n25 HP per second", new Vector3(-18f, 2.5f, -21f));
            Sign(root.transform, "LETHAL ZONE\nDeath / respawn check", new Vector3(-25f, 2.5f, -21f));
        }
        var instance = Object.FindAnyObjectByType<PlayerLifeCycle>();
        var so = new SerializedObject(instance);so.FindProperty("respawnPoint").objectReferenceValue = GameObject.Find("Player respawn point").transform;so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        foreach (var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            if (text.text.Contains("08 / LANDING ROLL")) text.text = "08 / FALL DAMAGE\n3 m drop | Ctrl before landing reduces damage\nRoll needs stamina";
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("HEALTH_SETUP_OK: health, death/respawn, HUD, damage/heal consoles, zones and Kevin Iglesias death animation.");
    }
    private static void SetupAnimator()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Characters/Animation/PlayerLocomotion.controller");
        if (!controller.parameters.Any(p => p.name == "Dead")) controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        var sm = controller.layers[0].stateMachine;
        if (sm.states.Any(s => s.state.name == "Dead")) return;
        var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Kevin Iglesias/Human Animations/Animations/Female/Combat/HumanF@Death01.fbx")
            .OfType<AnimationClip>().Single(c => c.name == "HumanF@Death01");
        if (!clip.humanMotion) throw new System.InvalidOperationException("Death clip must be Humanoid.");
        foreach (var transition in sm.anyStateTransitions) transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        var death = sm.AddState("Dead");death.motion = clip;
        var enter = sm.AddAnyStateTransition(death);enter.hasExitTime = false;enter.duration = .08f;enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        var exit = death.AddTransition(sm.states.Single(s => s.state.name == "Standing").state);
        exit.hasExitTime = false;exit.duration = 0f;exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        EditorUtility.SetDirty(controller);
    }
    private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 size, Color color)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);g.name = name;g.transform.SetParent(parent, false);g.transform.localPosition = position;g.transform.localScale = size;
        string path = Root + "/Materials/Health" + (color.g > color.r ? "Green" : "Red") + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor", color);AssetDatabase.CreateAsset(material, path); }
        g.GetComponent<Renderer>().sharedMaterial = material;return g;
    }
    private static void Console(Transform parent, string name, Vector3 position, Color color, bool healing, float amount, string prompt)
    {
        var g = Cube(name, parent, position, new Vector3(1f, 2f, 1f), color);
        var component = g.AddComponent<HealthTestInteractable>();var so = new SerializedObject(component);
        so.FindProperty("healing").boolValue = healing;so.FindProperty("amount").floatValue = amount;
        so.FindProperty("prompt").stringValue = prompt;so.FindProperty("displayName").stringValue = healing ? "Тестовое лечение" : "Тестовый урон";so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Zone(Transform parent, string name, Vector3 position, Vector3 size, float damage)
    {
        Cube(name + " floor marker", parent, new Vector3(position.x, .015f, position.z), new Vector3(size.x, .03f, size.z), new Color(.85f, .18f, .13f));
        var g = new GameObject(name, typeof(BoxCollider), typeof(Rigidbody), typeof(DamageZone));g.transform.SetParent(parent, false);g.transform.localPosition = position;
        g.GetComponent<BoxCollider>().size = size;g.GetComponent<BoxCollider>().isTrigger = true;
        var body = g.GetComponent<Rigidbody>();body.isKinematic = true;body.useGravity = false;
        var so = new SerializedObject(g.GetComponent<DamageZone>());so.FindProperty("damage").floatValue = damage;so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Sign(Transform parent, string value, Vector3 position)
    {
        var g = new GameObject(value.Split('\n')[0]);g.transform.SetParent(parent, false);g.transform.position = position;
        var text = g.AddComponent<TextMesh>();text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text = value;text.fontSize = 64;text.characterSize = .1f;text.anchor = TextAnchor.MiddleCenter;text.alignment = TextAlignment.Center;
        g.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 a, Vector2 b)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent, false);r.anchorMin = min;r.anchorMax = max;r.offsetMin = a;r.offsetMax = b;return r;
    }
    private static Text Text(string name, Transform parent, string value, int size, Vector2 a, Vector2 b)
    {
        var text = Rect(name, parent, Vector2.zero, Vector2.one, a, b).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text = value;text.fontSize = size;text.color = Color.white;text.alignment = TextAnchor.MiddleCenter;text.raycastTarget = false;return text;
    }
    private static void CreateHUD(Transform player, Health health, PlayerLifeCycle life)
    {
        var canvas = player.GetComponentInChildren<Canvas>();
        var root = Rect("Health HUD", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var panel = Rect("Health reserve", root, Vector2.zero, Vector2.zero, new Vector2(36f, 144f), new Vector2(396f, 218f));
        var bg = panel.gameObject.AddComponent<Image>();bg.color = new Color(.025f, .04f, .055f, .88f);bg.raycastTarget = false;
        Text("Health title", panel, "ЗДОРОВЬЕ", 18, new Vector2(10f, 39f), new Vector2(-170f, -7f));
        var number = Text("Health value", panel, "100 / 100", 18, new Vector2(220f, 39f), new Vector2(-10f, -7f));
        var track = Rect("Health track", panel, Vector2.zero, Vector2.one, new Vector2(16f, 14f), new Vector2(-16f, -36f));
        var trackImage = track.gameObject.AddComponent<Image>();trackImage.color = new Color(.14f, .18f, .2f);trackImage.raycastTarget = false;
        var fill = Rect("Health fill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();fill.color = new Color(.92f, .3f, .32f);fill.raycastTarget = false;
        var flash = Rect("Damage flash", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();flash.color = Color.clear;flash.raycastTarget = false;
        var death = Rect("Death screen", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var shade = death.gameObject.AddComponent<Image>();shade.color = new Color(.02f, .025f, .035f, .75f);shade.raycastTarget = false;
        var center = Rect("Death message", death, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-370f, -110f), new Vector2(370f, 110f));
        Text("Death title", center, "ВЫ ПОГИБЛИ", 42, new Vector2(0f, 100f), Vector2.zero);
        var hint = Text("Respawn hint", center, "[R] Возродиться", 24, Vector2.zero, new Vector2(0f, -105f));
        var hud = root.gameObject.AddComponent<PlayerHealthHUD>();var so = new SerializedObject(hud);
        so.FindProperty("health").objectReferenceValue = health;so.FindProperty("lifeCycle").objectReferenceValue = life;
        so.FindProperty("fill").objectReferenceValue = fill;so.FindProperty("valueText").objectReferenceValue = number;
        so.FindProperty("deathPanel").objectReferenceValue = death.gameObject;so.FindProperty("respawnText").objectReferenceValue = hint;so.FindProperty("damageFlash").objectReferenceValue = flash;
        so.ApplyModifiedPropertiesWithoutUndo();death.gameObject.SetActive(false);
    }
}
