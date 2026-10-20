using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Services;

/// <summary>Copy the functional owner even when its visual or bone is selected.</summary>
public static class TraderCopyTools
{
    public const string CopyMenu = "GameObject/Zombie Survival/Дублировать торговца целиком";

    [MenuItem(CopyMenu, false, 20)]
    private static void CopySelected()
    {
        var trader = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Trader>(true) : null;
        if (trader == null) return;
        Selection.activeGameObject = Duplicate(trader);
    }

    [MenuItem(CopyMenu, true)]
    private static bool CanCopy() => !EditorApplication.isPlayingOrWillChangePlaymode
        && Eligible(Selection.activeGameObject)
        && Selection.activeGameObject.GetComponentInParent<Trader>(true) != null;

    private static bool Eligible(GameObject go) => go != null && !EditorUtility.IsPersistent(go)
        && go.scene.IsValid() && go.scene.isLoaded && !EditorSceneManager.IsPreviewScene(go.scene)
        && PrefabStageUtility.GetPrefabStage(go) == null;

    public static GameObject Duplicate(Trader original)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || original == null || !Eligible(original.gameObject))
            throw new InvalidOperationException("Выберите сценового торговца вне Play.");
        original.ValidateConfiguration();
        if (original.Stocks.Any(s => Enumerable.Range(0, s.storage.SlotCount).Any(i => s.storage.GetSlot(i) != null && !s.storage.GetSlot(i).IsEmpty)))
            throw new InvalidOperationException("В Edit-складе есть экземпляры товаров. Копируйте пустого авторского торговца: ассортимент создаётся при запуске из профиля. Копирование остановлено, чтобы не повторить ID предметов.");
        SaveIdentityAuthoring.CaptureBaseline();
        var copy = UnityEngine.Object.Instantiate(original.gameObject, original.transform.parent);
        copy.name = GameObjectUtility.GetUniqueNameForSibling(original.transform.parent, original.name);
        Undo.RegisterCreatedObjectUndo(copy, "Дублировать торговца целиком");
        copy.GetComponent<Trader>().ValidateConfiguration();
        SaveIdentityAuthoring.ProcessNewObjects();
        return copy;
    }
}
