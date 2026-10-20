using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ZombieSurvival.Services;

/// <summary>Targeted, idempotent authoring; never saves user scenes or edits vendor imports.</summary>
public static class ServicePresentationT11Setup
{
    private const string Folder="Assets/Resources/ServicePresentation";
    [MenuItem("Tools/Character Lab/Set Up T11 Service Presentation (No Scene Save)")]
    public static void Run()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Настройка Т11 выполняется в Edit.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Resources","ServicePresentation");
        const string controllerPath=Folder+"/TraderIdle.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(controller==null)
        {
            var clip=AssetDatabase.LoadAllAssetsAtPath("Assets/Bubba_The_Handyman/URP/Animations/Handyman_idle_1_animation.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__")&&c.humanMotion);
            if(clip==null)throw new System.InvalidOperationException("Не найден Humanoid idle Handyman. Исходные ассеты не менялись.");
            controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var state=controller.layers[0].stateMachine.AddState("Idle");state.motion=clip;state.writeDefaultValues=false;
            controller.layers[0].stateMachine.defaultState=state;
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssetIfDirty(controller);
        }
        const string profilePath=Folder+"/Default.asset";
        if(AssetDatabase.LoadAssetAtPath<ServicePresentationProfile>(profilePath)==null)
        {
            var profile=ScriptableObject.CreateInstance<ServicePresentationProfile>();profile.idleController=controller;
            profile.tradeSuccess=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Services/TradeAccepted.wav");
            profile.rejected=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Services/OperationRejected.wav");
            profile.repairSuccess=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Services/RepairCompleted.wav");
            if(profile.tradeSuccess==null||profile.rejected==null||profile.repairSuccess==null)
            {Object.DestroyImmediate(profile);throw new System.InvalidOperationException("Сначала создайте/импортируйте звуки Tools/Audio/generate_service_sounds.py.");}
            AssetDatabase.CreateAsset(profile,profilePath);
        }
        foreach(var path in new[]{"Assets/CharacterTesting/Trading/T5Trader.prefab","Assets/CharacterTesting/Prefabs/T4WorkbenchSession.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                if(root.GetComponent<ServicePresentation>()==null)root.AddComponent<ServicePresentation>();
                // Unity may synthesize a newly required component while loading an older prefab.
                // Persist that native repair too; merely testing GetComponent would leave it memory-only.
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        Debug.Log("T11_SERVICE_PRESENTATION_OK: общий idle/звуки и исходные prefab подключены; сцены не сохранялись.");
    }
}
