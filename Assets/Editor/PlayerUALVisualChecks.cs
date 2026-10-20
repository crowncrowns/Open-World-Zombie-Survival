using System;
using System.IO;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Player;
using ZombieSurvival.Interaction;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class PlayerUALVisualChecks
{
    public const string Evidence = "S:/Games/Unity Projects/_CodexRecovery/PlayerUAL-20261020";
    public static string LastReport { get; private set; }
    public static IEnumerator RunVisuals()
    {
        if(!Application.isPlaying||!Application.dataPath.Contains("_CodexRecovery"))throw new InvalidOperationException("Recovery Play only");
        var p=UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        var autosave=p.GetComponent<ZombieSurvival.Persistence.PlayerSaveSlots>();bool oldAuto=autosave.enabled;
        var settings=InputSystem.settings;var oldBackground=settings.backgroundBehavior;var oldEditor=settings.editorInputBehaviorInPlayMode;
        var so=new SerializedObject(p);var names=new[]{"requireApplicationFocus","requireCursorCapture","captureCursorOnEnable"};var flags=names.Select(n=>so.FindProperty(n).boolValue).ToArray();
        var position=p.transform.position;var rotation=p.transform.rotation;
        Keyboard keyboard=null;
        Mouse mouse=null;
        GameObject floor=null;LastReport="running";
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        try
        {
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("UALVisualKeyboard");
            mouse=InputSystem.AddDevice<Mouse>("UALVisualMouse");
            foreach(var n in names)so.FindProperty(n).boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();autosave.enabled=false;
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Temporary visual checks floor";floor.transform.position=new Vector3(600,-.5f,600);floor.transform.localScale=new Vector3(50,1,50);
            p.ResetForRespawn(new Vector3(600,.02f,600),Quaternion.identity);Physics.SyncTransforms();Keys();yield return new WaitForSeconds(.4f);
            var weapons=p.GetComponent<ZombieSurvival.Weapons.PlayerWeapons>();weapons.Unequip();yield return new WaitForSeconds(.3f);
            Capture("final-body-down",85);Capture("final-idle-external",0,true);
            Keys(Key.W);yield return new WaitForSeconds(.45f);if(p.PlanarVelocity.magnitude<.1f)throw new InvalidOperationException("Visual keyboard did not move real motor");Capture("final-walk-external",0,true);yield return new WaitForSeconds(.15f);Capture("final-walk-next",0,true);
            Keys(Key.A);yield return new WaitForSeconds(.45f);Capture("final-left-external",0,true);
            Keys(Key.S);yield return new WaitForSeconds(.45f);Capture("final-back-external",0,true);
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.45f);Capture("final-run-external",0,true);
            Keys(Key.W,Key.LeftAlt);yield return new WaitForSeconds(.45f);Capture("final-slow-external",0,true);
            Keys(Key.C,Key.W);yield return new WaitForSeconds(.7f);if(!p.IsCrouching)throw new InvalidOperationException("Visual crouch not accepted");Capture("final-crouch-external",0,true);
            Keys();yield return new WaitForSeconds(.7f);Keys(Key.Z);yield return new WaitForSeconds(.1f);Keys(Key.W);yield return new WaitForSeconds(.7f);if(!p.IsProne)throw new InvalidOperationException("Visual prone not accepted");Capture("final-prone-external",0,true);Capture("final-prone-first");
            Keys();yield return new WaitForSeconds(.1f);Keys(Key.Z);yield return new WaitForSeconds(.1f);Keys();yield return new WaitForSeconds(.7f);
            var bag=p.GetComponent<ZombieSurvival.Inventory.ItemStorage>();var hotbar=p.GetComponent<ZombieSurvival.Inventory.PlayerHotbar>();
            var profile=Resources.Load<ZombieSurvival.Services.TraderProfile>("Trading/weapons_ammo");
            foreach(var kind in new[]{ZombieSurvival.Weapons.WeaponKind.Melee,ZombieSurvival.Weapons.WeaponKind.Firearm})
            {
                var definition=profile.Catalog.First(e=>e.item.Weapon!=null&&e.item.Weapon.kind==kind).item;
                int slot=Enumerable.Range(0,bag.SlotCount).Where(i=>bag.GetSlot(i)?.Definition==definition).DefaultIfEmpty(-1).First();
                if(slot<0){var item=new ZombieSurvival.Inventory.ItemStack(definition,1);if(!bag.TryAddStack(item))throw new InvalidOperationException("Visual fixture inventory full");slot=Enumerable.Range(0,bag.SlotCount).First(i=>ReferenceEquals(item,bag.GetSlot(i)));}
                if(!hotbar.EquipInventorySlot(slot))throw new InvalidOperationException("Visual fixture equip failed");yield return new WaitForSeconds(.5f);
                string name=kind==ZombieSurvival.Weapons.WeaponKind.Melee?"axe":"pistol";
                Capture("final-"+name);Capture("final-"+name+"-external",0,true);
                if(kind==ZombieSurvival.Weapons.WeaponKind.Melee){weapons.TryAttack();yield return new WaitForSeconds(.2f);Capture("final-axe-attack",0,true);yield return new WaitForSeconds(1.6f);}
                else
                {
                    InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));yield return new WaitForSeconds(.5f);
                    if(!weapons.IsAiming)throw new InvalidOperationException("Visual right mouse ADS not accepted");
                    Capture("final-pistol-ads");Capture("final-pistol-ads-external",0,true);
                    var pitch=typeof(FirstPersonController).GetField("pitch",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                    pitch.SetValue(p,45f);yield return new WaitForSeconds(.25f);Capture("final-pistol-ads-down");
                    pitch.SetValue(p,-45f);yield return new WaitForSeconds(.25f);Capture("final-pistol-ads-up");
                    pitch.SetValue(p,0f);InputSystem.QueueStateEvent(mouse,new MouseState());
                }
            }
            LastReport="Captured real locomotion, crawl, hand-bound weapons and accepted axe swing; frame="+Time.frameCount;
        }
        finally
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(floor!=null)UnityEngine.Object.Destroy(floor);
            if(mouse!=null)InputSystem.RemoveDevice(mouse);
            settings.backgroundBehavior=oldBackground;settings.editorInputBehaviorInPlayMode=oldEditor;
            so.Update();for(int i=0;i<names.Length;i++)so.FindProperty(names[i]).boolValue=flags[i];so.ApplyModifiedPropertiesWithoutUndo();autosave.enabled=oldAuto;
            p.ResetForRespawn(position,rotation);
        }
    }
    public static string Capture(string name, float pitch = float.NaN, bool external = false)
    {
        if(!Application.isPlaying||!Application.dataPath.Contains("_CodexRecovery"))throw new InvalidOperationException("Recovery Play only");
        var player=UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        var camera=player.GetComponent<PlayerInteractor>().ViewCamera;
        GameObject temporary=null;
        if(external)
        {
            temporary=new GameObject("Temporary UAL portrait");camera=temporary.AddComponent<Camera>();
            camera.transform.position=player.transform.TransformPoint(new Vector3(2.5f,1.5f,3.5f));
            camera.transform.LookAt(player.transform.position+Vector3.up*.95f);camera.fieldOfView=40;
        }
        var rotation=camera.transform.localRotation;var target=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
        var rt=new RenderTexture(960,720,24);var image=new Texture2D(960,720,TextureFormat.RGB24,false);
        try
        {
            if(!external&&!float.IsNaN(pitch))camera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            camera.targetTexture=rt;camera.aspect=960f/720;camera.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();string path=Evidence+"/"+name+".png";
            File.WriteAllBytes(path,image.EncodeToPNG());return path;
        }
        finally
        {
            camera.transform.localRotation=rotation;camera.targetTexture=target;camera.aspect=aspect;RenderTexture.active=active;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);if(temporary!=null)UnityEngine.Object.DestroyImmediate(temporary);
        }
    }
    public static string RigReport()
    {
        var p=UnityEngine.Object.FindAnyObjectByType<PlayerVisual>();var a=p.ModelAnimator;
        return string.Join("\n",new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand}
            .Select(b=>b+" "+p.transform.InverseTransformPoint(a.GetBoneTransform(b).position)))+"\nbodyForward="+(a.bodyRotation*Vector3.forward);
    }
}
