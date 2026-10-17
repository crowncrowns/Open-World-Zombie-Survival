using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieSurvival.AI;

namespace ZombieSurvival.EditorTools
{
    /// <summary>Own basic in-place muscle animations; no clips are supplied by Zombie Male AAB.</summary>
    public static class ZombieAnimationSetup
    {
        private const string Folder = "Assets/Animations/ZombieMaleBasic";

        [MenuItem("Tools/Zombie Survival/Create Basic Zombie Animations")]
        public static void CreateAndConnect()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            GameObject zombie = GameObject.Find("ZombieMale");
            if (zombie == null) throw new InvalidOperationException("ZombieMale is not in the active scene.");
            Animator animator = zombie.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("ZombieMale needs a valid Humanoid Avatar.");
            if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Animations", "ZombieMaleBasic");

            Vector3 bodyPosition;
            Quaternion bodyRotation;
            // Read the model's standing body height in origin space; never animate scene/world translation.
            var clone = UnityEngine.Object.Instantiate(zombie);
            try
            {
                clone.hideFlags = HideFlags.HideAndDontSave;
                clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                clone.transform.localScale = Vector3.one;
                using (var handler = new HumanPoseHandler(animator.avatar, clone.transform))
                {
                    var pose = new HumanPose();
                    handler.GetHumanPose(ref pose);
                    bodyPosition = pose.bodyPosition;
                    bodyRotation = pose.bodyRotation;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }

            AnimationClip idle = BuildClip("BasicIdle", 2f, true, 0, bodyPosition, bodyRotation);
            AnimationClip walk = BuildClip("BasicWalk", 1f, true, 1, bodyPosition, bodyRotation);
            AnimationClip run = BuildClip("BasicRun", 0.65f, true, 2, bodyPosition, bodyRotation);
            AnimationClip attack = BuildClip("BasicAttack", 1f, false, 3, bodyPosition, bodyRotation);
            string controllerPath = Folder + "/ZombieMaleBasic.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            // Rebuilding this specifically named generated controller preserves clip GUIDs and scene references.
            foreach (var parameter in controller.parameters) controller.RemoveParameter(parameter);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach (var oldTree in AssetDatabase.LoadAllAssetsAtPath(controllerPath))
                if (oldTree is BlendTree) UnityEngine.Object.DestroyImmediate(oldTree, true);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attacking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("AttackPlayback", AnimatorControllerParameterType.Float);
            var locomotion = machine.AddState("Locomotion");
            var tree = new BlendTree { name = "Velocity Locomotion", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0f); tree.AddChild(walk, 1.8f); tree.AddChild(run, 5.5f);
            locomotion.motion = tree;
            machine.defaultState = locomotion;
            var attacking = machine.AddState("Attack");
            attacking.motion = attack;
            attacking.speedParameter = "AttackPlayback";
            attacking.speedParameterActive = true;
            var begin = machine.AddAnyStateTransition(attacking);
            begin.hasExitTime = false; begin.duration = 0.035f; begin.hasFixedDuration = true;
            begin.canTransitionToSelf = false;
            begin.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            var end = attacking.AddTransition(locomotion);
            end.hasExitTime = false; end.duration = 0.12f; end.hasFixedDuration = true;
            end.AddCondition(AnimatorConditionMode.IfNot, 0f, "Attacking");
            GroundClips(animator, controller, new[] { idle, walk, run, attack }, bodyPosition.y);
            Undo.RecordObject(animator, "Connect zombie animations");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            var bridge = zombie.GetComponent<EnemyAnimationBridge>();
            if (bridge == null) bridge = Undo.AddComponent<EnemyAnimationBridge>(zombie);
            var bridgeData = new SerializedObject(bridge);
            bridgeData.FindProperty("attackClipLength").floatValue = attack.length;
            bridgeData.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(zombie.scene);
            EditorSceneManager.SaveScene(zombie.scene);
        }

        private static void GroundClips(Animator source, AnimatorController controller, AnimationClip[] clips, float bodyY)
        {
            // Compensate muscle-space leg flexion in body translation, rather than moving the NPC root.
            string modelPath = AssetDatabase.GetAssetPath(source.avatar);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var sample = UnityEngine.Object.Instantiate(model);
            try
            {
                sample.hideFlags = HideFlags.HideAndDontSave;
                sample.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = sample.GetComponent<Animator>();
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                float plantedHeight = Mathf.Min(left.position.y, right.position.y);
                // Account for the local terrain NavMesh rasterization height, keeping soles near the surface.
                var navAgent = source.GetComponent<UnityEngine.AI.NavMeshAgent>();
                var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = navAgent.agentTypeID, areaMask = navAgent.areaMask };
                if (Terrain.activeTerrain != null && UnityEngine.AI.NavMesh.SamplePosition(source.transform.position, out var hit, 3f, filter))
                    plantedHeight -= Mathf.Clamp(hit.position.y - Terrain.activeTerrain.SampleHeight(source.transform.position)
                        - Terrain.activeTerrain.transform.position.y, 0f, 0.15f);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0.00001f);
                animator.SetBool("Attacking", true);
                animator.SetFloat("AttackPlayback", 1f);
                float[] speeds = { 0f, 1.8f, 5.5f, 0f };
                for (int index = 0; index < clips.Length; index++)
                {
                    var clip = clips[index];
                    var keys = new Keyframe[61];
                    animator.SetFloat("Speed", speeds[index]);
                    for (int frame = 0; frame < keys.Length; frame++)
                    {
                        float normalized = frame / 60f;
                        animator.Play(index == 3 ? "Attack" : "Locomotion", 0, normalized);
                        animator.Update(0.00001f);
                        float lowestFoot = Mathf.Min(left.position.y, right.position.y);
                        keys[frame] = new Keyframe(normalized * clip.length,
                            bodyY + (plantedHeight - lowestFoot) / animator.humanScale);
                    }
                    var curve = new AnimationCurve(keys);
                    for (int i = 0; i < keys.Length; i++) curve.SmoothTangents(i, 0f);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y"), curve);
                    EditorUtility.SetDirty(clip);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(sample); }
        }

        private static AnimationClip BuildClip(string name, float length, bool loop, int kind,
            Vector3 bodyPosition, Quaternion bodyRotation)
        {
            string path = Folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip { name = name, frameRate = 30f }; AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves();
            foreach (string muscle in HumanTrait.MuscleName)
            {
                var keys = new Keyframe[61];
                for (int i = 0; i < keys.Length; i++)
                {
                    float t = i / 60f;
                    keys[i] = new Keyframe(t * length, Muscle(muscle, t, kind));
                }
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++) curve.SmoothTangents(i, 0f);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), curve);
            }
            string[] properties = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            float[] values = { bodyPosition.x, bodyPosition.y, bodyPosition.z, bodyRotation.x, bodyRotation.y, bodyRotation.z, bodyRotation.w };
            for (int i = 0; i < properties.Length; i++)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), properties[i]),
                    AnimationCurve.Constant(0f, length, values[i]));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop; settings.loopBlend = loop;
            settings.keepOriginalPositionXZ = true; settings.keepOriginalPositionY = true;
            settings.keepOriginalOrientation = true;
            settings.heightFromFeet = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static float Muscle(string muscle, float phase, int kind)
        {
            float cycle = Mathf.Sin(phase * Mathf.PI * 2f);
            bool left = muscle.StartsWith("Left ");
            float side = left ? 1f : -1f;
            if (muscle.EndsWith("Arm Down-Up")) return -0.5f;
            if (muscle.EndsWith("Arm Front-Back"))
            {
                if (kind == 3) return Mathf.Lerp(0.3f, -0.7f, Mathf.Sin(Mathf.PI * phase));
                return 0.3f - (kind == 0 ? 0.025f : kind == 1 ? 0.18f : 0.32f) * cycle * side;
            }
            if (muscle.EndsWith("Forearm Stretch")) return kind == 3 ? -0.25f + 0.2f * Mathf.Sin(Mathf.PI * phase) : -0.15f;
            if (muscle.EndsWith("Upper Leg Front-Back")) return kind == 1 ? 0.28f * cycle * side : kind == 2 ? 0.5f * cycle * side : 0f;
            if (muscle.EndsWith("Lower Leg Stretch")) return kind == 1 || kind == 2 ? 1f - Mathf.Max(0f, -cycle * side) * (kind == 1 ? 0.55f : 1f) : 1f;
            if (muscle.EndsWith("Foot Up-Down")) return kind == 1 || kind == 2 ? -0.15f * cycle * side : 0f;
            if (muscle == "Spine Front-Back") return kind == 3 ? 0.08f + 0.18f * Mathf.Sin(Mathf.PI * phase) : 0.08f + 0.015f * cycle;
            if (muscle == "Head Nod Down-Up") return -0.04f + (kind == 0 ? 0.025f * cycle : 0f);
            return 0f;
        }
    }
}
