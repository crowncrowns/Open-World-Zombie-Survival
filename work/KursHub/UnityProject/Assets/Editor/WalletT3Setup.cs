using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.Economy;
using ZombieSurvival.Inventory;

public static class WalletT3Setup
{
    private const string PlayerPrefab="Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
    private static void Set(UnityEngine.Object target,string field,long value)
    { var so=new SerializedObject(target);so.FindProperty(field).longValue=value;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target); }
    [MenuItem("Tools/Character Lab/Set Up T3 Wallet Tests")]
    public static void Run()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/CharacterTesting/CharacterTest.unity") throw new InvalidOperationException("Open CharacterTest in Edit Mode; setup never replaces the scene.");
        var prefab=PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try { if(prefab.GetComponent<Wallet>()==null)prefab.AddComponent<Wallet>();PrefabUtility.SaveAsPrefabAsset(prefab,PlayerPrefab); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        if(GameObject.Find("T3 - Кошелёк и тест бюджета")==null)
        {
            var root=new GameObject("T3 - Кошелёк и тест бюджета");
            var budgetObject=new GameObject("Тестовый бюджет (не торговец)");budgetObject.transform.SetParent(root.transform,false);
            var funds=budgetObject.AddComponent<Wallet>();Set(funds,"startingBalance",500);
            var budget=budgetObject.AddComponent<TraderBudget>();
            string[] labels={"Начислить 250 руб.","Списать 100 руб.","Отказ: списать 10 000 руб.","Игрок → бюджет 100 руб.","Бюджет → игрок 100 руб.","Режим бюджета finite / infinite"};
            var actions=new[]{WalletTestAction.Credit,WalletTestAction.Debit,WalletTestAction.Debit,WalletTestAction.PlayerPaysBudget,WalletTestAction.BudgetPaysPlayer,WalletTestAction.ToggleBudgetMode};
            long[] amounts={250,100,10000,100,100,100};
            for(int i=0;i<actions.Length;i++)
            {
                var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=labels[i];obj.transform.SetParent(root.transform,false);
                obj.transform.position=new Vector3(-3+i*3,.45f,-29);obj.transform.localScale=new Vector3(.55f,.9f,.55f);
                obj.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/CharacterTesting/Materials/Concrete.mat");
                var console=obj.AddComponent<WalletTestTerminal>();var so=new SerializedObject(console);
                so.FindProperty("action").intValue=(int)actions[i];so.FindProperty("amount").longValue=amounts[i];so.FindProperty("budget").objectReferenceValue=budget;so.ApplyModifiedPropertiesWithoutUndo();
                var sign=new GameObject("Т3 / "+labels[i]);sign.transform.SetParent(root.transform,false);sign.transform.position=new Vector3(-3+i*3,2,-28.4f);
                var source=sign.AddComponent<TextMesh>();source.text="Т3 / РУБЛИ\nE: "+labels[i]+"\nТест, не торговля | Старт игрока: 0 руб.";
                WorldSpacePresentationSetup.ConfigurePlaque(source);
                var plaque=sign.GetComponentInChildren<Canvas>().transform;
                plaque.SetPositionAndRotation(new Vector3(-3+i*3,.035f,-27.4f),Quaternion.Euler(90,180,0));
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
}
