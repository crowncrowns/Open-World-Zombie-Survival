using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Weapons;

/// <summary>
/// Авторские Humanoid-анимации корпуса для винтовки поверх существующего UAL.
/// Меняет только ссылки на шесть клипов: слои, маски, переходы и IK исходного
/// контроллера остаются общими. Модель, ввод, урон и боезапас здесь не создаются.
/// </summary>
public static class AK74BodyAnimationSetup
{
    public const string Source = "Assets/FreeTestCharacterAsuna/FBX/Animations/011Rifle.fbx";
    public const string Folder = "Assets/Weapons/AK74Gameplay/Body";
    public const string BodySource = Folder + "/AsunaRifle_Humanoid.fbx";
    public const string ControllerPath = "Assets/Weapons/AK74Gameplay/AK74Body.overrideController";
    public const string BaseControllerPath = "Assets/Characters/PlayerUAL/SurvivalistUAL.controller";

    private static readonly Dictionary<string, string> Replacements = new Dictionary<string, string>
    {
        { "Pistol_Idle_Loop", "RifleHoldInHands" },
        // В пакете одна Humanoid-поза прицеливания. Направление вверх/вниз
        // по-прежнему задаёт общий IK; одинаковые ссылки не выдают её за три клипа.
        { "Pistol_Aim_Up", "RifleAim" },
        { "Pistol_Aim_Neutral", "RifleAim" },
        { "Pistol_Aim_Down", "RifleAim" },
        { "Pistol_Shoot", "RifleFire" },
        { "Pistol_Reload", "RifleReload1" },
    };

    public static void Ensure(WeaponDefinition weapon)
    {
        if (weapon == null) throw new ArgumentNullException(nameof(weapon));
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Авторинг анимаций АК-74 разрешён только вне Play.");

        var baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseControllerPath);
        if (baseController == null) throw new InvalidOperationException("Не найден исходный SurvivalistUAL.controller.");

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerPath);
        bool created = controller == null;
        if (created)
        {
            PrepareBodySource();
            var clips = AssetDatabase.LoadAllAssetsAtPath(BodySource).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            controller = new AnimatorOverrideController(baseController) { name = "AK74Body" };
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(controller.overridesCount);
            controller.GetOverrides(overrides);
            int replaced = 0;
            for (int index = 0; index < overrides.Count; index++)
            {
                var original = overrides[index].Key;
                if (!Replacements.TryGetValue(ShortName(original.name), out string target)) continue;
                var replacement = clips.SingleOrDefault(c => c.name == target);
                if (replacement == null || !replacement.humanMotion)
                    throw new InvalidOperationException("Нет подходящего Humanoid-клипа: " + target);
                if (AnimationUtility.GetAnimationEvents(replacement).Length != 0)
                    throw new InvalidOperationException("Клип корпуса не должен содержать AnimationEvents: " + target);
                overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
                replaced++;
            }
            if (replaced != Replacements.Count)
                throw new InvalidOperationException("Структура UAL изменилась: ожидалось шесть замен, найдено " + replaced + ".");
            controller.ApplyOverrides(overrides);
            // До успешной проверки все операции были в памяти: частичный controller
            // не останется на диске и не будет ошибочно принят за завершённый авторинг.
            AssetDatabase.CreateAsset(controller, ControllerPath);
            AssetDatabase.SaveAssetIfDirty(controller);
        }

        if (controller.runtimeAnimatorController != baseController)
            throw new InvalidOperationException("АК-74 использует другой базовый controller. Проверьте его вручную; автоматическая замена запрещена.");

        // Повторный Setup сохраняет авторские замены клипов, длительность и профиль.
        // Подключаем defaults только при первоначальном создании/пустой ссылке.
        if (weapon.bodyAnimations == null)
        {
            weapon.bodyAnimations = controller;
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(controller.overridesCount);
            controller.GetOverrides(overrides);
            var reload = overrides.Single(p => ShortName(p.Key.name) == "Pistol_Reload").Value;
            if (reload == null || !reload.humanMotion || reload.length <= 0)
                throw new InvalidOperationException("В override отсутствует пригодная Humanoid-перезарядка винтовки.");
            weapon.bodyReloadSeconds = reload.length;
            EditorUtility.SetDirty(weapon);
            Debug.Log("AK74 body: RifleReload1=" + reload.length.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
                + " с; ActionRate = bodyReloadSeconds / WeaponDefinition.reloadSeconds.");
        }
    }

    private static string ShortName(string name)
    {
        int separator = name.LastIndexOf('|');
        return separator < 0 ? name : name.Substring(separator + 1);
    }

    private static void PrepareBodySource()
    {
        PlayerUALSetup.EnsureFolder(Folder);
        bool created = AssetDatabase.LoadAssetAtPath<GameObject>(BodySource) == null;
        if (created)
        {
            if (!AssetDatabase.CopyAsset(Source, BodySource))
                throw new InvalidOperationException("Не удалось скопировать авторский источник анимаций винтовки.");
        }

        ReportBodyImport(Source);
        ReportBodyImport(BodySource);
        bool valid = ValidateBodySource(out string reason);
        if (created || !valid)
        {
            // Незавершённый первый импорт уже мог оставить FBX на диске. Само
            // наличие файла не доказывает успех: исправляем только невалидную
            // авторскую копию и затем снова проверяем фактические humanMotion.
            if (!created) Debug.LogWarning("AK74 body: исправление невалидного авторского импорта: " + reason);
            ConfigureBodySource();
            ReportBodyImport(BodySource);
        }
        if (!ValidateBodySource(out reason))
            throw new InvalidOperationException("Авторский AsunaRifle_Humanoid.fbx не прошёл Humanoid-проверку: " + reason);
        Debug.Log("AK74_BODY_IMPORT_VALIDATED: sourceAvatar/embedded Avatar + actual Humanoid clips, без требования локального root Animator.avatar.");
    }

    private static bool ValidHumanAvatar(Avatar avatar) => avatar != null && avatar.isHuman && avatar.isValid;

    private static Avatar EffectiveAvatar(string path, ModelImporter importer)
    {
        // Animation-only FBX с CopyFromOther не обязан создавать собственный
        // Avatar или назначать его root Animator. Источник импорта — его Avatar.
        if (importer != null && importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther)
            return importer.sourceAvatar;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var nested = model != null ? model.GetComponentsInChildren<Animator>(true)
            .Select(a => a.avatar).FirstOrDefault(ValidHumanAvatar) : null;
        return nested != null ? nested : AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(ValidHumanAvatar);
    }

    private static bool ValidateBodySource(out string reason)
    {
        var importer = AssetImporter.GetAtPath(BodySource) as ModelImporter;
        if (importer == null || importer.animationType != ModelImporterAnimationType.Human)
        { reason = "ModelImporter не Human"; return false; }
        if (!ValidHumanAvatar(EffectiveAvatar(BodySource, importer)))
        { reason = "Нет валидного Humanoid Avatar импорта (sourceAvatar при CopyFromOther)"; return false; }
        var clips = AssetDatabase.LoadAllAssetsAtPath(BodySource).OfType<AnimationClip>().ToArray();
        foreach (string name in Replacements.Values.Distinct())
        {
            var clip = clips.SingleOrDefault(c => c.name == name);
            if (clip == null || !clip.humanMotion || clip.length <= 0)
            { reason = "Нет фактического Humanoid motion: " + name; return false; }
            if (AnimationUtility.GetAnimationEvents(clip).Length != 0)
            { reason = "AnimationEvents запрещены: " + name; return false; }
        }
        reason = "OK";
        return true;
    }

    private static void ReportBodyImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = model != null ? model.GetComponent<Animator>() : null;
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
        var avatar = EffectiveAvatar(path, importer);
        Debug.Log("AK74_BODY_IMPORT_REPORT: " + path
            + "; animationType=" + (importer != null ? importer.animationType.ToString() : "missing")
            + "; avatarSetup=" + (importer != null ? importer.avatarSetup.ToString() : "missing")
            + "; rootAnimator=" + (root != null) + "; rootAvatar=" + (root != null && root.avatar != null)
            + "; sourceAvatar=" + (importer != null && importer.sourceAvatar != null ? AssetDatabase.GetAssetPath(importer.sourceAvatar) : "null")
            + "; effectiveAvatarValid=" + ValidHumanAvatar(avatar)
            + "; clips=" + clips.Length + ": " + string.Join(", ", clips.Select(c => c.name + " human=" + c.humanMotion
                + " events=" + AnimationUtility.GetAnimationEvents(c).Length + " seconds="
                + c.length.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))));
    }

    private static void ConfigureBodySource()
    {
        var importer = AssetImporter.GetAtPath(BodySource) as ModelImporter;
        var sourceImporter = AssetImporter.GetAtPath(Source) as ModelImporter;
        if (importer == null || sourceImporter == null)
            throw new InvalidOperationException("Не найден ModelImporter авторской винтовки или источника.");
        var sourceAvatar = EffectiveAvatar(Source, sourceImporter);
        importer.animationType = ModelImporterAnimationType.Human;
        if (ValidHumanAvatar(sourceAvatar))
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;
        }
        else
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.humanDescription = sourceImporter.humanDescription;
        }
        importer.importAnimation = true;
        importer.optimizeGameObjects = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        // При repair берём исходную нарезку, а не случайно импортированный take Scene.
        var clips = sourceImporter.clipAnimations;
        if (clips.Length == 0) clips = sourceImporter.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.events = Array.Empty<AnimationEvent>();
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootPositionXZ = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            clip.loopTime = clip.name == "RifleHoldInHands" || clip.name == "RifleAim";
            clip.loopPose = clip.loopTime;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
}
