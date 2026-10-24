#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;
using ZombieSurvival.Player;
using ZombieSurvival.Weapons;
using ZombieSurvival.Inventory;
using ZombieSurvival.Interaction;
using ZombieSurvival.Persistence;
using ZombieSurvival.Combat;

[DefaultExecutionOrder(-10000)]
public sealed class PeekCoverProbe : MonoBehaviour
{
    FirstPersonController motor; PlayerWeapons weapons; Camera eye; Keyboard keyboard; Mouse mouse;
    Key[] keys = Array.Empty<Key>(); MouseState mouseState; Health target; int passed; PeekCoverSample sample;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static string Output => Path.GetFullPath("../..//PeekCover");
    void Awake()
    {
        motor = FindAnyObjectByType<FirstPersonController>(); weapons = motor.GetComponent<PlayerWeapons>();
        eye = motor.GetComponent<PlayerInteractor>().ViewCamera;
        target = GameObject.Find("Target").GetComponent<Health>();
        sample = gameObject.AddComponent<PeekCoverSample>(); sample.motor = motor;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
        Application.runInBackground = true; Time.captureDeltaTime = .02f;
        StartCoroutine(Guard(Scenario()));
    }
    void Update()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.QueueStateEvent(mouse, mouseState);
        InputSystem.Update();
    }
    void Check(bool value, string label)
    {
        if (!value) throw new Exception("PEEK_FAIL " + label);
        passed++; Debug.Log("PEEK_PASS " + label);
    }
    IEnumerator Guard(IEnumerator iterator)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(iterator);
        while (stack.Count > 0)
        {
            bool next; object current = null;
            try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); yield break; }
            if (!next) { stack.Pop(); continue; }
            if (current is IEnumerator nested) { stack.Push(nested); continue; }
            yield return current;
        }
    }
    IEnumerator Wait(float seconds = .6f) { yield return new WaitForSeconds(seconds); }
    IEnumerator Photo(string name)
    {
        var p = eye.ViewportToScreenPoint(new Vector3(.5f + motor.AimReticleViewportOffset, .5f, 0));
        Debug.Log("PEEK_PHOTO " + name + " " + Screen.width + "x" + Screen.height + " dot=" + p.ToString("F2"));
        ScreenCapture.CaptureScreenshot(Path.Combine(Output, name + ".png"));
        yield return new WaitForSeconds(.2f);
    }
    IEnumerator Fire(bool expectedHit, string label)
    {
        float hp = target.Current; int before = weapons.Equipped.LoadedRounds;
        mouseState.buttons = (ushort)(mouseState.buttons | 1); yield return new WaitForSeconds(.04f);
        mouseState.buttons = (ushort)(mouseState.buttons & ~1); yield return new WaitForSeconds(.25f);
        Debug.Log("PEEK_CLICK " + label + " rounds=" + before + "->" + weapons.Equipped.LoadedRounds + " hp=" + hp + "->" + target.Current + " gate=" + motor.AcceptingGameplayInput);
        Check(weapons.Equipped.LoadedRounds == before - 1, label + " one real click / one round");
        Check(expectedHit ? target.Current < hp : target.Current == hp, label + " target health");
    }
    void AimTarget()
    {
        var ray = motor.GetAimRay(eye);
        target.transform.position = ray.origin + ray.direction * ((7f - ray.origin.z) / ray.direction.z);
        target.transform.localScale = new Vector3(.25f, 1f, .3f);
        Physics.SyncTransforms();
    }
    IEnumerator Scenario()
    {
        yield return Wait(); Check(motor.AcceptingGameplayInput, "fixture input gate open");
        yield return Photo("unarmed-dot");
        var bag = motor.GetComponent<ItemStorage>();
        var gun = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Weapons/AK74Gameplay/AK74Item.asset");
        var ammo = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Weapons/AK74Gameplay/Ammo545Item.asset");
        gun.Weapon.hipSpread = gun.Weapon.aimSpread = gun.Weapon.recoil = 0;
        Check(bag.TryAddAll(gun, 1) && bag.TryAddAll(ammo, 90), "test originals added");
        int index = Enumerable.Range(0, bag.SlotCount).First(i => bag.GetSlot(i)?.Definition == gun);
        Check(weapons.Equip(index), "source hand-bound AK equipped"); yield return Wait();
        keys = new[] { Key.R }; yield return new WaitForSeconds(.06f); keys = Array.Empty<Key>();
        yield return new WaitForSeconds(2.6f); Check(weapons.Equipped.LoadedRounds == 30, "real R reload");
        Check(weapons.View.transform.Find(gun.Weapon.muzzleTransformPath) != null, "authored real muzzle resolves");
        var animator = motor.GetComponent<PlayerVisual>().ModelAnimator;
        Vector3 neutralShoulders = sample.Shoulders;
        Vector3 root = motor.transform.position;
        yield return Fire(false, "neutral cover blocks");
        foreach (int side in new[] { -1, 1 })
        {
            keys = new[] { side < 0 ? Key.Q : Key.E }; mouseState.buttons = 2; yield return Wait(.9f);
            Vector3 shoulders = sample.Shoulders;
            var muzzle = weapons.View.transform.Find(gun.Weapon.muzzleTransformPath);
            Debug.Log("PEEK_POSE side=" + side + " camera=" + eye.transform.position + " shoulders=" + shoulders + " gun=" + sample.Gun + " muzzle=" + sample.Muzzle);
            Check(motor.PeekAmount * side > .95f, "full side " + side);
            Check((shoulders.x - neutralShoulders.x) * side > .1f, "shoulders follow " + side);
            Check(sample.Muzzle.x * side > .18f, "actual muzzle clears cover " + side);
            var sight = eye.WorldToViewportPoint(sample.Sight);
            Debug.Log("PEEK_SIGHTS " + side + " " + sight);
            // Механический целик исходной модели измеряется отдельно: обязательная
            // приёмка этого изменения — попадание через видимое перекрестье.
            Check(weapons.View.activeInHierarchy, "held weapon visible " + side);
            Check((motor.transform.position - root).sqrMagnitude < .001f, "root remains " + side);
            AimTarget(); yield return Photo(side < 0 ? "left-ads" : "right-ads");
            yield return Fire(true, "ADS side " + side);
            mouseState.buttons = 0; yield return Wait();
            yield return Photo(side < 0 ? "left-hip" : "right-hip");
            yield return Fire(true, "HIP side " + side);
            keys = Array.Empty<Key>(); yield return Wait();
        }
        keys = new[] { Key.C, Key.E }; mouseState.buttons = 2; yield return Wait();
        Check(motor.IsCrouching && motor.PeekAmount > .95f, "crouch and peek");
        AimTarget(); yield return Photo("crouch-right"); yield return Fire(true, "crouch ADS");
        keys = Array.Empty<Key>(); mouseState.buttons = 0; yield return Wait();
        var stopper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stopper.transform.position = eye.transform.position + Vector3.right * .19f;
        stopper.transform.localScale = new Vector3(.1f, .5f, .5f); Physics.SyncTransforms();
        keys = new[] { Key.E }; yield return Wait();
        Check(motor.PeekAmount < .3f && motor.AimReticleViewportOffset < .008f, "collision limits body / camera / marker together");
        Destroy(stopper); keys = Array.Empty<Key>(); yield return Wait();
        keys = new[] { Key.E }; yield return Wait();
        int rounds = weapons.Equipped.LoadedRounds; motor.SetGameplayInputLock(this, true); yield return Wait();
        Check(Mathf.Abs(motor.PeekAmount) < .001f && !weapons.TryAttack() && weapons.Equipped.LoadedRounds == rounds, "owner lock recenters and blocks fire");
        yield return Photo("menu-hidden-dot");
        keys = Array.Empty<Key>(); mouseState.buttons = 0; motor.SetGameplayInputLock(this, false); yield return Wait();
        Check(Mathf.Abs(motor.PeekAmount) < .001f, "release has no residual lean");
        yield return Photo("neutral-final");
        InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
        Debug.Log("PEEK_ALL_PASS " + passed); EditorApplication.Exit(0);
    }
}
[DefaultExecutionOrder(1000)]
public sealed class PeekCoverSample : MonoBehaviour
{
    public FirstPersonController motor;
    public Vector3 Shoulders, Gun, Muzzle, Sight;
    void LateUpdate()
    {
        if (motor == null) return;
        var animator = motor.GetComponent<PlayerVisual>().ModelAnimator;
        if (animator == null || !animator.isHuman) return;
        Shoulders = (animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position + animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position) * .5f;
        var weapons = motor.GetComponent<PlayerWeapons>();
        if (weapons.View == null) return;
        Gun = weapons.View.transform.position;
        var model = weapons.View.GetComponentInChildren<AK74Reference.AK74Presentation>(true);
        if (model != null) { Muzzle = model.muzzle.position; Sight = model.aim.position; }
    }
}
[InitializeOnLoad]
public static class PeekCoverEntry
{
    static PeekCoverEntry()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PeekCover", false))
            { SessionState.SetBool("PeekCover", false); new GameObject("Temporary cover probe").AddComponent<PeekCoverProbe>(); }
        };
    }
    public static void Run()
    {
        Directory.CreateDirectory(Path.GetFullPath("../../PeekCover"));
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(0, -.25f, 0); floor.transform.localScale = new Vector3(20, .5f, 20);
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 2; sun.transform.rotation = Quaternion.Euler(45, 20, 0);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab"));
        player.transform.position = new Vector3(0, .03f, 0);
        var motor = new SerializedObject(player.GetComponent<FirstPersonController>());
        foreach (var f in new[] { "requireApplicationFocus", "requireCursorCapture", "captureCursorOnEnable" }) motor.FindProperty(f).boolValue = false;
        motor.ApplyModifiedPropertiesWithoutUndo();
        player.GetComponent<PlayerSaveSlots>().enabled = false;
        var cover = GameObject.CreatePrimitive(PrimitiveType.Cube); cover.name = "Cover"; cover.transform.position = new Vector3(0, 1.2f, 1.5f); cover.transform.localScale = new Vector3(.36f, 2.4f, .3f);
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube); target.name = "Target"; target.transform.position = new Vector3(0, 1.5f, 7); target.transform.localScale = new Vector3(4, 3, .3f);
        var health = target.AddComponent<Health>(); var hs = new SerializedObject(health); hs.FindProperty("maximum").floatValue = 5000; hs.ApplyModifiedPropertiesWithoutUndo();
        var identity = target.AddComponent<ActorIdentity>(); var ids = new SerializedObject(identity); ids.FindProperty("faction").intValue = (int)ActorFaction.Infected; ids.ApplyModifiedPropertiesWithoutUndo(); target.AddComponent<CombatTarget>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/__PeekCover.unity");
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();
        SessionState.SetBool("PeekCover", true); EditorApplication.isPlaying = true;
    }
}
#endif
