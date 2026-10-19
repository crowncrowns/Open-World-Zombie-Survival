using UnityEditor;
using UnityEngine;
using ZombieSurvival.Services;

/// <summary>Persists new serialized defaults without resetting authored stock, money, prices or scenes.</summary>
public static class TraderT12Setup
{
    [MenuItem("Tools/Character Lab/Set Up T12 Trader Economy (No Scene Save)")]
    public static void Run()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Настройка Т12 выполняется в Edit.");
        const string path="Assets/CharacterTesting/Trading/T5Trader.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var trader=root.GetComponent<Trader>();
            if(trader==null)throw new System.InvalidOperationException("В исходном prefab нет Trader.");
            trader.ValidateConfiguration();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        Debug.Log("T12_TRADER_SETUP_OK: настройки исходного prefab сохранены; товары/деньги/сцены не менялись.");
    }
}
