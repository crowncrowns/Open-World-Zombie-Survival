using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombieSurvival.Player;

/// <summary>Additive setup for the character lab; does not touch SampleScene or vendor assets.</summary>
public static class PlayerStageFiveSetup
{
    [MenuItem("Tools/Character Lab/Set Up Stamina and Crawl")]
    public static void Run()
    {
        const string path = "Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
        var player = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var stamina = player.GetComponent<PlayerStamina>();
            if (stamina == null) stamina = player.AddComponent<PlayerStamina>();
            if (player.GetComponent<PlayerCrawlAnimation>() == null) player.AddComponent<PlayerCrawlAnimation>();
            if (player.GetComponentInChildren<PlayerStaminaHUD>(true) == null) CreateHUD(player.transform, stamina);
            var bar = player.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == "Fill");
            if (bar != null) { bar.sprite = null; bar.type = Image.Type.Simple; }
            PrefabUtility.SaveAsPrefabAsset(player, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        SetupAnimator();
        var scene = EditorSceneManager.OpenScene("Assets/CharacterTesting/CharacterTest.unity");
        foreach (var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
        {
            if (text.text.Contains("05 / ROLL TUNNEL"))
                text.text = "05 / ROLL TO CRAWL\nCtrl: roll in | Z: prone / stand\nLow roof keeps you crawling";
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("STAGE_FIVE_SETUP_OK: crawl, stamina, HUD, animator and tunnel sign.");
    }

    private static void SetupAnimator()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Characters/Animation/PlayerLocomotion.controller");
        if (!controller.parameters.Any(p => p.name == "Prone")) controller.AddParameter("Prone", AnimatorControllerParameterType.Bool);
        var layers = controller.layers; layers[0].iKPass = true; controller.layers = layers;
        var sm = controller.layers[0].stateMachine;
        if (sm.states.Any(s => s.state.name == "Crawling (Humanoid IK)")) return;
        var prone = sm.AddState("Crawling (Humanoid IK)");
        prone.motion = AssetDatabase.LoadAllAssetsAtPath("Assets/FreeTestCharacterAsuna/FBX/Animations/001Idle.fbx")
            .OfType<AnimationClip>().First(c => c.name == "Idle1");
        var enter = sm.AddAnyStateTransition(prone);
        enter.hasExitTime = false; enter.canTransitionToSelf = false; enter.duration = .1f;
        enter.AddCondition(AnimatorConditionMode.If, 0, "Prone");
        enter.AddCondition(AnimatorConditionMode.Equals, 0, "ParkourState");
        foreach (var child in sm.states)
        {
            if (child.state == prone) continue;
            // Keep standing/crouched/landing transitions from stealing the crawl state.
            foreach (var transition in child.state.transitions)
                if (transition.destinationState != null && (transition.destinationState.name == "Standing" || transition.destinationState.name == "Crouching"))
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Prone");
        }
        foreach (var name in new[] { "Standing", "Crouching" })
        {
            var exit = prone.AddTransition(sm.states.Single(s => s.state.name == name).state);
            exit.hasExitTime = false; exit.duration = .15f;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0, "Prone");
            exit.AddCondition(name == "Crouching" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Crouched");
        }
        EditorUtility.SetDirty(controller);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        return rect;
    }

    private static Text Label(string name, Transform parent, string value, int size, TextAnchor align, Vector2 min, Vector2 max)
    {
        var text = Rect(name, parent, Vector2.zero, Vector2.one, min, max).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value; text.fontSize = size; text.alignment = align; text.color = Color.white; text.raycastTarget = false;
        return text;
    }

    private static void CreateHUD(Transform player, PlayerStamina stamina)
    {
        var canvas = new GameObject("Character HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.transform.SetParent(player, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 10;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var panel = Rect("Stamina", canvas.transform, Vector2.zero, Vector2.zero, new Vector2(36, 36), new Vector2(396, 132));
        panel.gameObject.AddComponent<Image>().color = new Color(.025f, .04f, .055f, .88f);
        panel.GetComponent<Image>().raycastTarget = false;
        Label("Title", panel, "ВЫНОСЛИВОСТЬ", 18, TextAnchor.UpperLeft, new Vector2(16, 57), new Vector2(-120, -12));
        var number = Label("Reserve", panel, "100 / 100", 18, TextAnchor.UpperRight, new Vector2(220, 57), new Vector2(-16, -12));
        var track = Rect("Bar track", panel, Vector2.zero, Vector2.one, new Vector2(16, 34), new Vector2(-16, -42));
        track.gameObject.AddComponent<Image>().color = new Color(.14f, .18f, .2f); track.GetComponent<Image>().raycastTarget = false;
        var fill = Rect("Fill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        fill.type = Image.Type.Simple;
        fill.color = new Color(.25f, .85f, .65f); fill.raycastTarget = false;
        var status = Label("Status", panel, "", 14, TextAnchor.LowerLeft, new Vector2(16, 9), new Vector2(-16, -67));
        var hud = panel.gameObject.AddComponent<PlayerStaminaHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("stamina").objectReferenceValue = stamina; so.FindProperty("fill").objectReferenceValue = fill;
        so.FindProperty("reserveText").objectReferenceValue = number; so.FindProperty("stateText").objectReferenceValue = status;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
