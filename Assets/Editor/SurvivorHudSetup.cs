using UnityEditor;
using UnityEngine;
using ZombieSurvival.Presentation;

public static class SurvivorHudSetup
{
    private const string PlayerPath = "Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
    [MenuItem("Tools/Character Lab/Set Up Contextual Survivor HUD")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Выйдите из Play перед обновлением префаба.");
        var root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            if (root.GetComponent<SurvivorHUD>() == null) root.AddComponent<SurvivorHUD>();
            var inventory = root.GetComponent<ZombieSurvival.Inventory.PlayerInventory>();
            var visual = root.GetComponent<ZombieSurvival.Player.PlayerVisual>();
            if (inventory != null && inventory.InventoryIdle == null && visual != null && visual.ModelPrefab != null)
            {
                var definition = visual.ModelPrefab.GetComponentInChildren<ZombieSurvival.Player.PlayerModelDefinition>(true);
                var animator = definition != null && definition.ModelAnimator != null ? definition.ModelAnimator : visual.ModelPrefab.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                    Motion motion = controller != null && controller.layers.Length > 0 ? controller.layers[0].stateMachine.defaultState?.motion : null;
                    if (motion is UnityEditor.Animations.BlendTree tree)
                        foreach (var child in tree.children) if (child.position.sqrMagnitude < .001f) { motion = child.motion; break; }
                    var clip = motion as AnimationClip;
                    if (clip == null) clip = System.Array.Find(animator.runtimeAnimatorController.animationClips, c => c.name.EndsWith("Idle_Loop", System.StringComparison.Ordinal));
                    if (clip != null)
                    {
                        var settings = new SerializedObject(inventory);
                        settings.FindProperty("inventoryIdle").objectReferenceValue = clip; settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Validate();
    }
    [MenuItem("Tools/Character Lab/Check Contextual Survivor HUD")]
    public static void Validate()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        if (player == null || player.GetComponents<SurvivorHUD>().Length != 1) throw new System.InvalidOperationException("Нужен один SurvivorHUD на корне игрока.");
        if (player.GetComponent<ZombieSurvival.Combat.Health>() == null || player.GetComponent<ZombieSurvival.Player.PlayerStamina>() == null
            || player.GetComponent<ZombieSurvival.Inventory.PlayerHotbar>() == null || player.GetComponent<ZombieSurvival.Interaction.PlayerInteractor>() == null)
            throw new System.InvalidOperationException("Отсутствует источник значений HUD.");
        var so = new SerializedObject(player.GetComponent<SurvivorHUD>());
        var field = so.GetIterator();
        while (field.NextVisible(true))
            if (field.name != "m_Script" && string.IsNullOrWhiteSpace(field.tooltip)) throw new System.InvalidOperationException("Нет Tooltip: " + field.name);
        Debug.Log("SURVIVOR_HUD_PREFAB_OK: one presenter; existing gameplay sources; Russian tooltips. Play scenarios: Docs/ContextualHUD.md");
    }
}
