using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;
using ZombieSurvival.Progression;

public static class CharacterStage12Validation
{
    [MenuItem("Tools/Character Lab/Check Stage 12 and Hotbar")]
    public static void Run()
    {
        var players = UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include);
        if (players.Length == 0) throw new InvalidOperationException("В открытой сцене нет игрока.");
        foreach (var player in players)
        {
            var progression = player.GetComponent<PlayerProgression>(); var hotbar = player.GetComponent<PlayerHotbar>();
            if (progression == null || hotbar == null || player.GetComponent<PlayerProgressionMenu>() == null || player.GetComponent<PlayerStatModifiers>() == null)
                throw new InvalidOperationException(player.name + ": не настроены компоненты этапа 12/хотбара.");
            if (progression.Definition == null || !progression.Definition.Validate(out _)) throw new InvalidOperationException("Дерево не назначено или содержит ошибки.");
            var actions = new SerializedObject(player).FindProperty("inputActions").objectReferenceValue as InputActionAsset;
            foreach (var name in new[] { "Slot1", "Slot2", "Slot3", "Slot4", "Slot5", "HotbarScroll", "Drop", "Attack", "Aim", "Reload", "Interact" })
                if (actions == null || actions.FindAction("Player/" + name) == null) throw new InvalidOperationException("Нет действия " + name);
            foreach (var name in new[] { "Previous", "Next" })
                if (actions.FindAction("Player/" + name).bindings.Any(b => b.path == "<Keyboard>/1" || b.path == "<Keyboard>/2"))
                    throw new InvalidOperationException("Старый перебор оружия конфликтует с прямым выбором хотбара.");
            var catalog = new SerializedObject(player.GetComponent<ZombieSurvival.Persistence.PlayerSaveSystem>()).FindProperty("itemCatalog");
            var needs = new SerializedObject(player.GetComponent<PlayerNeeds>()).FindProperty("consumables");
            for (int i = 0; i < needs.arraySize; i++)
            {
                var effect = needs.GetArrayElementAtIndex(i).objectReferenceValue as ConsumableDefinition;
                if (effect == null || effect.Item == null || !Enumerable.Range(0, catalog.arraySize).Any(j => catalog.GetArrayElementAtIndex(j).objectReferenceValue == effect.Item))
                    throw new InvalidOperationException("Расходуемый предмет отсутствует в каталоге сохранения.");
            }
        }
        Debug.Log("STAGE12_VALIDATION_OK: дерево, предметы, компоненты и действия ввода.");
    }
}
