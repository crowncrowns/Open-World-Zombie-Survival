using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Combat;
using ZombieSurvival.Presentation;
using ZombieSurvival.Weapons;

public static class WorldSpacePresentationSetup
{
    public const string Folder = "Assets/Characters/Presentation";
    private const string ZombiePrefab = "Assets/Characters/Enemies/ZombieMaleActor.prefab";
    private static TMP_FontAsset Font()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "/WorldText.asset");
        if (font != null) return font;
        System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        if (source == null) throw new InvalidOperationException("Import TMP Essentials non-interactively before configuring world text.");
        font = TMP_FontAsset.CreateFontAsset(source, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        font.name = "WorldText"; font.material.name = "WorldText depth-tested";
        font.material.SetFloat("_ZTestMode", 4f);
        AssetDatabase.CreateAsset(font, Folder + "/WorldText.asset");
        AssetDatabase.AddObjectToAsset(font.material, font); AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
        font.TryAddCharacters("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz /|:+.,-()ЭТАПОРУЖИЕмишеньсоюзникуронзапрещён");
        EditorUtility.SetDirty(font); return font;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.sizeDelta = size;
        return rect;
    }
    private static TextMeshProUGUI Caption(Transform parent, Vector2 size)
    {
        var rect = Rect("Caption", parent, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Font(); text.fontSharedMaterial = text.font.material;
        text.alignment = TextAlignmentOptions.Center; text.color = new Color(.98f, .98f, .9f);
        text.raycastTarget = false; text.richText = false;
        text.enableAutoSizing = true; text.fontSizeMin = 14; text.fontSizeMax = 24;
        return text;
    }
    public static void ConfigurePlaque(TextMesh source)
    {
        if (source == null || source.GetComponentInParent<WeaponPracticeTarget>() != null) return;
        var marker = source.GetComponent<FloorInformationPlaque>();
        if (marker != null) return;
        var original = source.transform.position;
        bool parkour = source.transform.root.name == "03 - Parkour course";
        var point = new Vector3(original.x, .035f, original.z - (parkour ? 4.5f : 2.8f));
        // Keep clearance labels outside the narrow corridor rather than inside its walls.
        if (source.text.StartsWith("Width ")) point.z = -16f;
        if (source.text.StartsWith("05 / CROUCH")) point.z = 4.5f;
        if (source.text.StartsWith("BREAK LINE")) point = new Vector3(50, .035f, -18);
        if (source.text.StartsWith("CRAWL PURSUIT")) point = new Vector3(63, .035f, -24);
        if (source.text.StartsWith("ЭТАП 10")) point = new Vector3(18, .035f, -27);
        if (source.text.StartsWith("05 / ROLL")) point.z = 53.5f;
        if (source.text.StartsWith("07 / HEAD")) point.z = 54.5f;
        if (source.text.StartsWith("08 / FALL")) point.z = 57f;
        if (source.text.StartsWith("06 / RUNNING")) point = new Vector3(-8, .035f, 51);
        bool small = !source.text.Contains("\n");
        var size = small ? new Vector2(260, 100) : new Vector2(500, 240);
        var rect = Rect("Floor plaque", source.transform, size);
        rect.SetPositionAndRotation(point, Quaternion.Euler(90f, 0f, 0f)); rect.localScale = Vector3.one * .004f;
        var canvas = rect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var panel = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); panel.color = new Color(.025f, .04f, .055f, 1f); panel.raycastTarget = false;
        var caption = Caption(rect, size - new Vector2(28, 22)); caption.rectTransform.localPosition = new Vector3(0, 0, -.3f);
        caption.text = source.text;
        marker = source.gameObject.AddComponent<FloorInformationPlaque>(); marker.Bind(source, caption);
        EditorUtility.SetDirty(source.GetComponent<MeshRenderer>()); EditorUtility.SetDirty(marker);
    }
    [MenuItem("Tools/Character Lab/Apply Compact Plaques and Zombie Health Ring")]
    public static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/CharacterTesting/CharacterTest.unity")
            throw new InvalidOperationException("Run in the open CharacterTest scene outside Play Mode. Other scenes are deliberately not opened or replaced.");
        foreach (var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (text.gameObject.scene == scene) ConfigurePlaque(text);
        ConfigureZombie();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("WORLD_PRESENTATION_READY: full-text floor plaques and shared zombie prefab HP ring saved.");
    }
    public static void ConfigureZombie()
    {
        var root = PrefabUtility.LoadPrefabContents(ZombiePrefab);
        try
        {
            var health = root.GetComponent<Health>();
            if (health == null) throw new InvalidOperationException("Shared actor must have root Health.");
            var indicator = root.GetComponent<ActorHealthIndicator>();
            var canvas = indicator != null ? indicator.GetComponentInChildren<Canvas>(true) : null;
            var rect = canvas != null ? (RectTransform)canvas.transform : Rect("Health indicator", root.transform, new Vector2(128,128));
            rect.sizeDelta = new Vector2(128,128); rect.localScale = Vector3.one * .001875f;
            if (canvas == null) canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Upgrade existing indicators too: remove the old number object, not merely its text.
            foreach (var text in rect.GetComponentsInChildren<TMP_Text>(true)) UnityEngine.Object.DestroyImmediate(text.gameObject);
            var graphic = rect.GetComponentInChildren<HollowHealthRing>(true);
            if (graphic == null) graphic = Rect("Radial health ring", rect, new Vector2(128,128)).gameObject.AddComponent<HollowHealthRing>();
            graphic.name = "Radial health ring"; graphic.rectTransform.sizeDelta = new Vector2(128,128);
            if (graphic.GetComponent<CanvasRenderer>() == null) graphic.gameObject.AddComponent<CanvasRenderer>();
            graphic.color = new Color(1f,.035f,.035f,1f); graphic.raycastTarget = false;
            var animator = root.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.isHuman);
            var head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            rect.position = (head != null ? head.position : root.transform.position + Vector3.up*1.65f) + Vector3.up*.28f;
            if (indicator == null) indicator = root.AddComponent<ActorHealthIndicator>();
            indicator.Bind(health, canvas, graphic, head);
            graphic.SetHealth(1f);
            PrefabUtility.SaveAsPrefabAsset(root, ZombiePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}





