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
