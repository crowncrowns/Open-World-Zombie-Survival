using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombieSurvival.Interaction;

public static class PlayerInteractionSetup
{
    private const string Root = "Assets/CharacterTesting";
    [MenuItem("Tools/Character Lab/Set Up Interaction Tests")]
    public static void Run()
    {
        var player = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/CharacterTestPlayer.prefab");
        try
        {
            var interactor = player.GetComponent<PlayerInteractor>();
            if (interactor == null) interactor = player.AddComponent<PlayerInteractor>();
            var interactionSettings = new SerializedObject(interactor);
            interactionSettings.FindProperty("viewCamera").objectReferenceValue = player.GetComponentInChildren<Camera>(true);
            interactionSettings.ApplyModifiedPropertiesWithoutUndo();
            if (player.GetComponentInChildren<InteractionHUD>(true) == null) CreateHUD(player.transform, interactor);
            PrefabUtility.SaveAsPrefabAsset(player, Root + "/Prefabs/CharacterTestPlayer.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        CreateDoorPrefab();
        var scene = EditorSceneManager.OpenScene(Root + "/CharacterTest.unity");
        var target = GameObject.Find("Future interact target");
        if (target != null)
        {
            target.name = "Interaction red switch";
            ConfigureSwitch(target, "Тестовый переключатель");
        }
        foreach (var text in Object.FindObjectsByType<TextMesh>())
            if (text.text.Contains("07 / FUTURE INTERACTION")) text.text = "07 / INTERACTION\nAim at the red block + E\nRed: off | Green: on";
        if (GameObject.Find("04 - Interaction tests") == null)
        {
            var root = new GameObject("04 - Interaction tests");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/InteractionTestDoor.prefab");
            var door = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            door.transform.position = new Vector3(11.3f, 0f, -17f);
            Box("Door left post", root.transform, new Vector3(11.15f, 1.2f, -17f), new Vector3(.2f, 2.4f, .3f));
            Box("Door right post", root.transform, new Vector3(12.85f, 1.2f, -17f), new Vector3(.2f, 2.4f, .3f));
            Box("Door lintel", root.transform, new Vector3(12f, 2.3f, -17f), new Vector3(1.9f, .2f, .3f));
            Sign(root.transform, "INTERACTION / E\nDoor opens away from you\nAn occupied doorway stops closing", new Vector3(12f, 3f, -16.8f));
            var hidden = Box("Switch behind wall", root.transform, new Vector3(18f, 1.5f, -16.5f), Vector3.one * .6f);
            ConfigureSwitch(hidden, "Переключатель за стеной");
            Box("Interaction occlusion wall", root.transform, new Vector3(18f, 1.2f, -17.5f), new Vector3(3f, 2.4f, .25f));
            Sign(root.transform, "OCCLUSION TEST\nNo interaction through the wall\nWalk around to reach the switch", new Vector3(18f, 3f, -17.4f));
        }
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("INTERACTION_SETUP_OK: player, HUD, original switch, door prefab and occlusion station.");
    }
    private static void ConfigureSwitch(GameObject target, string displayName)
    {
        var toggle = target.GetComponent<ToggleInteractable>();
        if (toggle == null) toggle = target.AddComponent<ToggleInteractable>();
        var so = new SerializedObject(toggle);
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("indicators").arraySize = 1;
        so.FindProperty("indicators").GetArrayElementAtIndex(0).objectReferenceValue = target.GetComponent<Renderer>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void CreateDoorPrefab()
    {
        string path = Root + "/Prefabs/InteractionTestDoor.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = new GameObject("InteractionTestDoor");
        try
        {
            var door = root.AddComponent<DoorInteractable>();
            var leaf = Box("Door leaf", root.transform, new Vector3(.7f, 1.05f, 0f), new Vector3(1.4f, 2f, .12f));
            var so = new SerializedObject(door);
            so.FindProperty("displayName").stringValue = "Тестовая дверь";
            so.FindProperty("hinge").objectReferenceValue = root.transform;
            so.FindProperty("leaf").objectReferenceValue = leaf.GetComponent<BoxCollider>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name;
        g.transform.SetParent(parent, false); g.transform.localPosition = position; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Concrete.mat");
        return g;
    }
    private static void Sign(Transform parent, string value, Vector3 position)
    {
        var g = new GameObject(value.Split('\n')[0]); g.transform.SetParent(parent, false); g.transform.position = position;
        var text = g.AddComponent<TextMesh>();text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value; text.fontSize = 64; text.characterSize = .1f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        g.GetComponent<MeshRenderer>().sharedMaterial = text.font.material; WorldSpacePresentationSetup.ConfigurePlaque(text);
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 a, Vector2 b)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin = min; r.anchorMax = max; r.offsetMin = a; r.offsetMax = b; return r;
    }
    private static Text Label(string name, Transform parent, int size, Vector2 a, Vector2 b)
    {
        var text = Rect(name, parent, Vector2.zero, Vector2.one, a, b).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; return text;
    }
    private static void CreateHUD(Transform player, PlayerInteractor interactor)
    {
        var canvas = player.GetComponentInChildren<Canvas>();
        if (canvas == null) throw new System.InvalidOperationException("Set up the character HUD first.");
        var root = Rect("Interaction HUD", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var center = new Vector2(.5f, .5f);
        var dot = Rect("Interaction reticle", root, center, center, new Vector2(-2f, -2f), new Vector2(2f, 2f)).gameObject.AddComponent<Image>();
        dot.raycastTarget = false;
        var panel = Rect("Interaction prompt", root, center, center, new Vector2(-230f, -150f), new Vector2(230f, -82f));
        var background = panel.gameObject.AddComponent<Image>(); background.color = new Color(.025f, .04f, .055f, .88f); background.raycastTarget = false;
        var name = Label("Interaction name", panel, 20, new Vector2(12f, 34f), new Vector2(-12f, -5f));
        var action = Label("Interaction action", panel, 22, new Vector2(12f, 3f), new Vector2(-12f, -33f));
        var feedbackArea = Rect("Interaction feedback area", root, center, center, new Vector2(-300f, -210f), new Vector2(300f, -170f));
        var feedback = Label("Interaction feedback", feedbackArea, 20, Vector2.zero, Vector2.zero);
        var hud = root.gameObject.AddComponent<InteractionHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("interactor").objectReferenceValue = interactor; so.FindProperty("promptPanel").objectReferenceValue = panel.gameObject;
        so.FindProperty("nameText").objectReferenceValue = name; so.FindProperty("actionText").objectReferenceValue = action;
        so.FindProperty("feedbackText").objectReferenceValue = feedback; so.FindProperty("reticle").objectReferenceValue = dot;
        so.ApplyModifiedPropertiesWithoutUndo(); panel.gameObject.SetActive(false);
    }
}

