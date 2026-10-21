using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieSurvival.Player;

public static class PlayerUALSetup
{
    public const string Folder = "Assets/Characters/PlayerUAL";
    public const string ControllerPath = Folder + "/SurvivalistUAL.controller";
    public const string VisualPath = Folder + "/SurvivalistPlayer.prefab";
    public const string PlayerPath = "Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab";
    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
    public static string PrepareSources()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit only");
        EnsureFolder(Folder + "/Sources");
        for (int pack = 1; pack <= 2; pack++)
        {
            string path = Folder + "/Sources/UAL" + pack + "_Humanoid.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null
                && !AssetDatabase.CopyAsset("Assets/Animations/Universal Animation Library/UAL " + pack + "/UAL" + pack + "_Standard.fbx", path))
                throw new InvalidOperationException("Не скопирован UAL" + pack);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false; importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = clip.name.EndsWith("_Loop", StringComparison.Ordinal) || clip.name.EndsWith("Sword_Idle", StringComparison.Ordinal);
                clip.loopPose = clip.loopTime; clip.lockRootRotation = true; clip.keepOriginalOrientation = true;
                clip.lockRootPositionXZ = true; clip.lockRootHeightY = true; clip.heightFromFeet = true;
                clip.events = Array.Empty<AnimationEvent>();
            }
            importer.clipAnimations = clips; importer.SaveAndReimport();
        }
        return string.Join("\n", Enumerable.Range(1, 2).Select(i =>
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Sources/UAL" + i + "_Humanoid.fbx");
            var a = model.GetComponent<Animator>();
            return model.name + ": valid=" + (a != null && a.avatar != null && a.avatar.isValid) + ", human=" + (a != null && a.avatar != null && a.avatar.isHuman);
        }));
    }
    public static AnimationClip Clip(string name)
    {
        foreach (int pack in new[] { 1, 2 })
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(Folder + "/Sources/UAL" + pack + "_Humanoid.fbx").OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == "Armature|" + name);
            if (clip != null) return clip;
        }
        throw new InvalidOperationException("Не найден клип " + name);
    }
    public static Avatar PrepareSurvivalistAvatar()
    {
        string path=Folder+"/Sources/SurvivalistRig.fbx";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null
            && !AssetDatabase.CopyAsset("Assets/Survivalist/Basemesh/SK_Military_Survivalist.fbx",path))throw new InvalidOperationException("Нет Survivalist rig");
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        var description=importer.humanDescription;var bones=description.human;
        int index=Array.FindIndex(bones,b=>b.humanName=="Hips");
        if(index<0)throw new InvalidOperationException("Нет Hips mapping");
        bones[index].boneName="pelvis";description.human=bones;importer.humanDescription=description;
        importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var avatar=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Animator>().avatar;
        if(avatar==null||!avatar.isValid||!avatar.isHuman)throw new InvalidOperationException("Неверный исправленный Avatar");
        return avatar;
    }

    private static void Set(UnityEngine.Object owner, string name, object value)
    {
        var so = new SerializedObject(owner); var p = so.FindProperty(name);
        if (p == null) throw new InvalidOperationException(owner.name + ": " + name);
        if (value is bool flag) p.boolValue = flag;
        else if (value is Vector3 vector) p.vector3Value = vector;
        else if (value is Vector2 vector2) p.vector2Value = vector2;
        else if (value is float scalar) p.floatValue = scalar;
        else p.objectReferenceValue = value as UnityEngine.Object;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static AnimatorState State(AnimatorStateMachine machine, string name, string clip, string speed = null)
    {
        var state = machine.AddState(name); state.motion = Clip(clip); state.writeDefaultValues = false;
        if (speed != null) { state.speedParameter = speed; state.speedParameterActive = true; }
        return state;
    }
    private static void Condition(AnimatorStateTransition t, string name, AnimatorConditionMode mode, float value = 0)
        => t.AddCondition(mode, value, name);
    private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration = .12f, bool exit = false, float exitTime = .9f)
    {
        var t = from.AddTransition(to); t.hasExitTime = exit; t.exitTime = exitTime; t.hasFixedDuration = true;
        t.duration = duration; t.canTransitionToSelf = false; return t;
    }
    private static AnimatorStateTransition Any(AnimatorStateMachine sm, AnimatorState to, string parameter, AnimatorConditionMode mode, float value = 0)
    {
        var t = sm.AddAnyStateTransition(to); t.hasExitTime = false; t.hasFixedDuration = true; t.duration = .08f;
        t.canTransitionToSelf = false; Condition(t, parameter, mode, value); return t;
    }
    public static void CreateController()
    {
        EnsureFolder(Folder);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null) throw new InvalidOperationException("Контроллер уже создан; не перезаписывать авторские правки автоматически.");
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (var n in new[] { "MoveX", "MoveZ", "Speed", "VerticalSpeed", "ParkourProgress", "LocomotionRate", "ActionRate", "Pitch" }) controller.AddParameter(n, AnimatorControllerParameterType.Float);
        foreach (var n in new[] { "SlowWalking", "Sprinting", "Crouched", "Prone", "Grounded", "Dead", "InputBlocked", "Aiming", "Reloading" }) controller.AddParameter(n, AnimatorControllerParameterType.Bool);
        foreach (var n in new[] { "ParkourState", "WeaponKind" }) controller.AddParameter(n, AnimatorControllerParameterType.Int);
        foreach (var n in new[] { "Jump", "Land", "Fire", "Melee", "Reload", "Equip", "Pickup" }) controller.AddParameter(n, AnimatorControllerParameterType.Trigger);
        var baseLayer = controller.layers[0]; baseLayer.iKPass = true; var sm = baseLayer.stateMachine;
        var idle = State(sm, "Idle", "Idle_Loop"); sm.defaultState = idle;
        var slow = State(sm, "SlowWalk", "Walk_Formal_Loop", "LocomotionRate");
        var walk = State(sm, "Walk", "Walk_Loop", "LocomotionRate");
        var walkTree=new BlendTree { name="Walk to jog",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false };
        AssetDatabase.AddObjectToAsset(walkTree,controller);walkTree.AddChild(Clip("Walk_Loop"),1.4f);walkTree.AddChild(Clip("Jog_Fwd_Loop"),3);walk.motion=walkTree;
        var run = State(sm, "Run", "Sprint_Loop", "LocomotionRate");
        var crouchIdle = State(sm, "CrouchIdle", "Crouch_Idle_Loop");
        var crouch = State(sm, "CrouchWalk", "Crouch_Fwd_Loop", "LocomotionRate");
        var prone = State(sm, "Prone", "Idle_Loop");
        var jump = State(sm, "JumpStart", "Jump_Start"); jump.speed = 2.5f;
        var air = State(sm, "JumpLoop", "Jump_Loop");
        var land = State(sm, "Land", "Jump_Land"); land.speed = 2.5f;
        var roll = State(sm, "Roll", "Roll"); roll.timeParameter = "ParkourProgress"; roll.timeParameterActive = true;
        var climb = State(sm, "Climb", "ClimbUp_1m"); climb.timeParameter = "ParkourProgress"; climb.timeParameterActive = true;
        var vault = State(sm, "Vault", "ClimbUp_1m"); vault.timeParameter = "ParkourProgress"; vault.timeParameterActive = true;
        var hang = State(sm, "Hang", "ClimbUp_1m"); hang.speed = 0; hang.cycleOffset = .1f;
        var death = State(sm, "Death", "Death01");
        Any(sm, death, "Dead", AnimatorConditionMode.If);
        var p = Any(sm, prone, "Prone", AnimatorConditionMode.If); Condition(p, "Dead", AnimatorConditionMode.IfNot);
        foreach (var pair in new[] { (roll,4), (climb,2), (vault,3), (hang,1) })
        { var t = Any(sm, pair.Item1, "ParkourState", AnimatorConditionMode.Equals, pair.Item2); Condition(t,"Dead",AnimatorConditionMode.IfNot); }
        var normal = new[] { idle, slow, walk, run, crouchIdle, crouch };
        foreach (var from in normal)
        {
            var t = Transition(from, jump, .06f); Condition(t,"Jump",AnimatorConditionMode.If);
            t = Transition(from, air, .1f); Condition(t,"Grounded",AnimatorConditionMode.IfNot); Condition(t,"ParkourState",AnimatorConditionMode.Equals,0);
        }
        Transition(jump, air, .08f, true, .32f);
        foreach (var from in new[] { jump, air }) { var t = Transition(from,land,.08f); Condition(t,"Grounded",AnimatorConditionMode.If); }
        foreach (var from in normal.Concat(new[] { prone, land, roll, climb, vault, hang, death }))
        foreach (var to in normal)
        {
            if (from == to) continue;
            var t = Transition(from,to,.12f,from==land,.32f);
            Condition(t,"Dead",AnimatorConditionMode.IfNot); Condition(t,"Prone",AnimatorConditionMode.IfNot);
            Condition(t,"Grounded",AnimatorConditionMode.If); Condition(t,"ParkourState",AnimatorConditionMode.Equals,0);
            bool isCrouch = to == crouch || to == crouchIdle;
            Condition(t,"Crouched",isCrouch?AnimatorConditionMode.If:AnimatorConditionMode.IfNot);
            Condition(t,"Speed",to==idle||to==crouchIdle?AnimatorConditionMode.Less:AnimatorConditionMode.Greater,.08f);
            if (!isCrouch && to != idle)
            {
                Condition(t,"Sprinting",to==run?AnimatorConditionMode.If:AnimatorConditionMode.IfNot);
                if(to!=run)Condition(t,"SlowWalking",to==slow?AnimatorConditionMode.If:AnimatorConditionMode.IfNot);
            }
        }
        controller.layers = new[] { baseLayer };
        var mask = new AvatarMask(); mask.name = "UpperBody";
        for (int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
        foreach (var part in new[] { AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers }) mask.SetHumanoidBodyPartActive(part,true);
        AssetDatabase.CreateAsset(mask,Folder+"/UpperBody.mask");
        controller.AddLayer("Upper Body"); var layers=controller.layers; layers[1].avatarMask=mask;layers[1].defaultWeight=0; layers[1].iKPass=true; controller.layers=layers;
        var upper=layers[1].stateMachine;
        var empty=State(upper,"Empty","Idle_Loop"); upper.defaultState=empty;
        var pistol=State(upper,"PistolIdle","Pistol_Idle_Loop");
        var aim=upper.AddState("PistolAim"); aim.writeDefaultValues=false;
        var aimTree=new BlendTree { name="Pistol pitch", blendType=BlendTreeType.Simple1D,blendParameter="Pitch",useAutomaticThresholds=false };
        AssetDatabase.AddObjectToAsset(aimTree,controller); aimTree.AddChild(Clip("Pistol_Aim_Up"),-1);aimTree.AddChild(Clip("Pistol_Aim_Neutral"),0);aimTree.AddChild(Clip("Pistol_Aim_Down"),1);aim.motion=aimTree;
        var axe=State(upper,"AxeIdle","Sword_Idle");
        var fire=State(upper,"Fire","Pistol_Shoot");
        var reload=State(upper,"Reload","Pistol_Reload","ActionRate");
        var melee=State(upper,"Melee","Sword_Attack","ActionRate");
        var pickup=State(upper,"Pickup","PickUp_Table");
        foreach(var pair in new[]{(fire,"Fire"),(reload,"Reload"),(melee,"Melee"),(pickup,"Pickup")})
        {var t=Any(upper,pair.Item1,pair.Item2,AnimatorConditionMode.If);Condition(t,"Dead",AnimatorConditionMode.IfNot);Condition(t,"InputBlocked",AnimatorConditionMode.IfNot);t.canTransitionToSelf=true;t.duration=.035f;}
        foreach(var from in new[]{empty,pistol,aim,axe,fire,reload,melee,pickup})
        foreach(var to in new[]{empty,pistol,aim,axe})
        {
            if(from==to)continue; bool action=from==fire||from==reload||from==melee||from==pickup;
            var t=Transition(from,to,.08f,action,.96f);
            Condition(t,"WeaponKind",AnimatorConditionMode.Equals,to==empty?0:to==axe?2:1);
            if(to==pistol||to==aim)Condition(t,"Aiming",to==aim?AnimatorConditionMode.If:AnimatorConditionMode.IfNot);
        }
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller);
    }

    public static void CreateVisual()
    {
        if(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath)!=null)return;
        var scene=EditorSceneManager.NewPreviewScene(); var root=new GameObject("SurvivalistPlayer"); SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var body=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Survivalist/Prefab/Survivalist (1).prefab"),root.transform);
            body.transform.localRotation=Quaternion.Euler(0,180,0);PrefabUtility.RecordPrefabInstancePropertyModifications(body.transform);
            var animator=body.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.avatar=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Sources/SurvivalistRig.fbx").GetComponent<Animator>().avatar;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            foreach(var renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.name.Contains("FPS")){renderer.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);}
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/ForestRoute/Materials/Survivalist/"+m.name+".mat")??throw new InvalidOperationException("Нет URP материала "+m.name)).ToArray();
                if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            var definition=root.AddComponent<PlayerModelDefinition>();Set(definition,"modelAnimator",animator);
            var so=new SerializedObject(definition);var hidden=so.FindProperty("firstPersonHiddenRenderers");
            var heads=body.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("Head")||r.name.Contains("Cap")).ToArray();hidden.arraySize=heads.Length;
            for(int i=0;i<heads.Length;i++)hidden.GetArrayElementAtIndex(i).objectReferenceValue=heads[i];so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,VisualPath);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
    public static void ConfigurePlayer(GameObject player)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit only");
        var visual=player.GetComponent<PlayerVisual>();if(visual==null)throw new InvalidOperationException("Нет PlayerVisual");
        Set(visual,"driveAnimator",false);Set(visual,"localPosition",Vector3.zero);Set(visual,"localScale",Vector3.one);Set(visual,"localEulerAngles",Vector3.zero);
        visual.SetModel(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath));
        // Updating the source player prefab can add an inherited visual beside an old scene override.
        // Remove only redundant instances of OUR visual, never arbitrary user attachments.
        var anchor=visual.ModelInstance.transform.parent;
        for(int i=anchor.childCount-1;i>=0;i--)
        {
            var child=anchor.GetChild(i).gameObject;
            if(child!=visual.ModelInstance && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child)==VisualPath)
                Undo.DestroyObjectImmediate(child);
        }
        // The UAL packs do not include prone locomotion: retain the existing procedural crawl.
        if(player.GetComponent<PlayerUALAnimationDriver>()==null)player.AddComponent<PlayerUALAnimationDriver>();
        var weapons=player.GetComponent<ZombieSurvival.Weapons.PlayerWeapons>(); if(weapons!=null)
        {
            Set(weapons,"handBoundVisual",true);Set(weapons,"firearmHandEuler",new Vector3(90,0,0));
            Set(weapons,"firearmHandPosition",new Vector3(.19f,-.045f,.03f));
            Set(weapons,"meleeHandEuler",new Vector3(90,35,0));
            Set(weapons,"meleeHandPosition",new Vector3(.16f,0,.13f));
        }
        var motor=player.GetComponent<FirstPersonController>();if(motor!=null){Set(motor,"downwardLookLimit",70f);Set(motor,"downwardCameraShift",.13f);Set(motor,"crouchCameraOffset",new Vector2(.08f,.02f));}
        var camera=player.GetComponentInChildren<Camera>();if(camera!=null)
        {
            camera.transform.localPosition=new Vector3(0,1.65f,.1f);
            camera.nearClipPlane=.035f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        }
        EditorUtility.SetDirty(visual);PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
    }
    public static void ConfigurePlayerPrefab()
    {
        var root=PrefabUtility.LoadPrefabContents(PlayerPath);
        try{ConfigurePlayer(root);PrefabUtility.SaveAsPrefabAsset(root,PlayerPath);}finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static void CreateFingerGrips()
    {
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if(controller.layers.Any(l=>l.name=="Hand Grip"))return;
        EnsureFolder(Folder+"/Grips");
        AnimationClip Grip(string name,bool closed,bool support)
        {
            var clip=new AnimationClip{name=name};
            foreach(string side in new[]{"Left","Right"})
            foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Little"})
            for(int joint=1;joint<=3;joint++)
            {
                bool close=closed&&(side=="Right"||support);
                float value=close?(finger=="Thumb"?-.35f:joint==1?-.65f:-.8f):.6f;
                if(close&&side=="Left")value=Mathf.Lerp(value,.3f,.4f);
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),side+"Hand."+finger+"."+joint+" Stretched"),AnimationCurve.Constant(0,1,value));
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            AssetDatabase.CreateAsset(clip,Folder+"/Grips/"+name+".anim");return clip;
        }
        var open=Grip("Open",false,false);var pistol=Grip("PistolGrip",true,true);var axe=Grip("AxeGrip",true,false);
        var mask=new AvatarMask{name="Fingers only"};
        for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers,true);mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers,true);
        AssetDatabase.CreateAsset(mask,Folder+"/Grips/Fingers.mask");controller.AddLayer("Hand Grip");var layers=controller.layers;layers[2].avatarMask=mask;layers[2].defaultWeight=1;controller.layers=layers;
        var sm=layers[2].stateMachine;var a=sm.AddState("Open");a.motion=open;var b=sm.AddState("PistolGrip");b.motion=pistol;var c=sm.AddState("AxeGrip");c.motion=axe;
        a.writeDefaultValues=b.writeDefaultValues=c.writeDefaultValues=false;sm.defaultState=a;
        foreach(var pair in new[]{(b,1),(c,2)})
        {
            var t=Any(sm,pair.Item1,"WeaponKind",AnimatorConditionMode.Equals,pair.Item2);
            Condition(t,"Prone",AnimatorConditionMode.IfNot);Condition(t,"Dead",AnimatorConditionMode.IfNot);Condition(t,"InputBlocked",AnimatorConditionMode.IfNot);Condition(t,"ParkourState",AnimatorConditionMode.Equals,0);
        }
        Any(sm,a,"WeaponKind",AnimatorConditionMode.Equals,0);Any(sm,a,"Prone",AnimatorConditionMode.If);Any(sm,a,"InputBlocked",AnimatorConditionMode.If);Any(sm,a,"Dead",AnimatorConditionMode.If);Any(sm,a,"ParkourState",AnimatorConditionMode.Greater,0);
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssetIfDirty(controller);
    }
}
