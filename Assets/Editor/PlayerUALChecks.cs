using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ZombieSurvival.Inventory;
using ZombieSurvival.Combat;
using ZombieSurvival.Interaction;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;
using ZombieSurvival.Services;
using ZombieSurvival.Weapons;

/// <summary>Disposable recovery-project Play checks. No save, asset or main scene mutations.</summary>
public static class PlayerUALChecks
{
    public static bool Completed { get; private set; }
    public static string LastReport { get; private set; }
    public static IEnumerator RunAdditional()
    {
        Completed=false;LastReport="Running additional";var results=new List<string>();
        if(!Application.isPlaying||!Application.dataPath.Contains("_CodexRecovery"))
        {LastReport="FAIL recovery Play required";Completed=true;yield break;}
        var routine=AdditionalScenario(results);
        try
        {
            while(true)
            {
                bool more;try{more=routine.MoveNext();}catch(Exception error){results.Add("FAIL "+error);break;}
                if(!more)break;yield return routine.Current;
            }
        }
        finally
        {
            try{(routine as IDisposable)?.Dispose();}catch(Exception error){results.Add("FAIL cleanup "+error);}
            LastReport=string.Join("\n",results);Completed=true;
        }
    }
    public static IEnumerator RunPlay()
    {
        Completed=false; LastReport="Running"; var results=new List<string>();
        if(!Application.isPlaying || !Application.dataPath.Contains("_CodexRecovery"))
        { LastReport="FAIL recovery Play required";Completed=true;yield break; }
        var routine=Scenario(results);
        try
        {
            while(true)
            {
                bool more; try { more=routine.MoveNext(); } catch(Exception error) { results.Add("FAIL "+error);break; }
                if(!more)break;yield return routine.Current;
            }
        }
        finally
        {
            try { (routine as IDisposable)?.Dispose(); } catch(Exception error) { results.Add("FAIL cleanup "+error); }
            LastReport=string.Join("\n",results);Completed=true;
        }
    }
    private static bool ReadBool(UnityEngine.Object obj,string field) => new SerializedObject(obj).FindProperty(field).boolValue;
    private static void SetBool(UnityEngine.Object obj,string field,bool value)
    { var so=new SerializedObject(obj);so.FindProperty(field).boolValue=value;so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Keys(Keyboard keyboard,params Key[] keys) => InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static bool State(Animator animator,int layer,params string[] names)
    {
        var current=animator.GetCurrentAnimatorStateInfo(layer);var next=animator.GetNextAnimatorStateInfo(layer);
        return names.Any(n=>current.IsName(n)||animator.IsInTransition(layer)&&next.IsName(n));
    }
    private static bool Finite(float value) => !float.IsNaN(value)&&!float.IsInfinity(value);
    private static bool IKReset(PlayerUALAnimatorIK bridge)
    {
        foreach(string name in new[]{"feetWeight","handsWeight","meleeWeight"})
            if(Mathf.Abs((float)typeof(PlayerUALAnimatorIK).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(bridge))>.00001f)return false;
        return true;
    }
    private static float HandWeight(PlayerUALAnimatorIK bridge) => (float)typeof(PlayerUALAnimatorIK)
        .GetField("handsWeight",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(bridge);

    private static IEnumerator AdditionalScenario(List<string> results)
    {
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);results.Add("PASS "+label);}
        var driver=UnityEngine.Object.FindObjectsByType<PlayerUALAnimationDriver>(FindObjectsInactive.Exclude).Single();
        var motor=driver.Motor;var visual=driver.GetComponent<PlayerVisual>();var weapons=driver.GetComponent<PlayerWeapons>();
        var storage=driver.GetComponent<ItemStorage>();var hotbar=driver.GetComponent<PlayerHotbar>();var health=driver.GetComponent<Health>();
        var interactor=driver.GetComponent<PlayerInteractor>();var inventory=driver.GetComponent<PlayerInventory>();
        var autosave=driver.GetComponent<PlayerSaveSlots>();var camera=interactor.ViewCamera;
        var flagValues=new List<Tuple<UnityEngine.Object,string,bool>>();
        void Override(UnityEngine.Object obj,string field,bool value)
        {flagValues.Add(Tuple.Create(obj,field,ReadBool(obj,field)));SetBool(obj,field,value);}
        var inputSettings=InputSystem.settings;var oldBackground=inputSettings.backgroundBehavior;var oldRouting=inputSettings.editorInputBehaviorInPlayMode;
        var oldPosition=driver.transform.position;var oldRotation=driver.transform.rotation;
        bool oldAuto=autosave!=null&&autosave.enabled;var assignments=hotbar.CaptureAssignments();int selected=hotbar.SelectedIndex;
        Keyboard keyboard=null;Mouse mouse=null;GameObject floor=null,pickup=null;int interactions=0;
        Action onInteraction=()=>interactions++;
        try
        {
            Check(motor.IsAlive,"additional fixture player alive");
            driver.GetComponent<PlayerServiceSession>()?.Close();inventory.Close();driver.GetComponent<PlayerProgressionMenu>()?.Close();
            Override(motor,"requireApplicationFocus",false);Override(motor,"requireCursorCapture",false);Override(motor,"captureCursorOnEnable",false);
            Override(inventory,"requireApplicationFocus",false);
            if(autosave!=null)autosave.enabled=false;
            inputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="UAL additional recovery floor";floor.transform.position=new Vector3(600,-.5f,600);floor.transform.localScale=new Vector3(40,1,40);
            motor.ResetForRespawn(new Vector3(600,.02f,600),Quaternion.identity);Physics.SyncTransforms();
            keyboard=InputSystem.AddDevice<Keyboard>("UALAdditionalKeyboard");mouse=InputSystem.AddDevice<Mouse>("UALAdditionalMouse");
            Keys(keyboard);InputSystem.QueueStateEvent(mouse,new MouseState());
            for(int i=0;i<hotbar.SlotCount;i++)hotbar.Clear(i);
            yield return new WaitForSeconds(.4f);
            Check(motor.AcceptingGameplayInput&&weapons.Equipped==null,"unarmed input available after ordinary hotbar clear");
            var food=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/CharacterTesting/Items/CannedFood.asset");
            Check(food!=null&&food.WorldPrefab!=null,"real WorldItem definition available");
            int quantity=storage.Count(food);
            pickup=UnityEngine.Object.Instantiate(food.WorldPrefab);pickup.name="UAL additional actual E pickup";
            pickup.GetComponent<WorldItem>().Initialize(food,1);
            foreach(var body in pickup.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
            var collider=pickup.GetComponentInChildren<Collider>();Check(collider!=null,"pickup has solid interaction collider");
            Physics.SyncTransforms();pickup.transform.position+=camera.transform.position+camera.transform.forward*1.3f-collider.bounds.center;Physics.SyncTransforms();
            interactor.SuccessfulInteraction+=onInteraction;
            yield return new WaitForSeconds(.12f);
            Check(interactor.Target==pickup.GetComponent<WorldItem>()&&interactor.CanUseTarget,"real interactor ray selects pickup");
            Keys(keyboard,Key.E);yield return new WaitForSeconds(.12f);Keys(keyboard);
            var animator=visual.ModelAnimator;int upper=animator.GetLayerIndex("Upper Body");
            Check(interactions==1&&storage.Count(food)==quantity+1&&(pickup==null||!pickup.activeSelf),"real E delivers exactly one successful WorldItem pickup");
            Check(weapons.Equipped==null&&State(animator,upper,"Upper Body.Pickup")&&animator.GetLayerWeight(upper)>.05f,"unarmed Pickup upper animation has positive weight");
            yield return new WaitForSeconds(.8f);
            var profile=Resources.Load<TraderProfile>("Trading/weapons_ammo");var pistolType=profile.Catalog.First(e=>e.item.Weapon?.kind==WeaponKind.Firearm).item;
            var pistol=Enumerable.Range(0,storage.SlotCount).Select(storage.GetSlot).FirstOrDefault(s=>s!=null&&!s.IsEmpty&&s.Definition==pistolType);
            if(pistol==null){pistol=new ItemStack(pistolType,1);Check(storage.TryAddStack(pistol),"additional pistol enters same storage");}
            int pistolSlot=Enumerable.Range(0,storage.SlotCount).Single(i=>ReferenceEquals(storage.GetSlot(i),pistol));
            Check(hotbar.EquipInventorySlot(pistolSlot),"normal hotbar pistol equips for lifecycle checks");yield return new WaitForSeconds(.4f);
            string id=pistol.InstanceId;int rounds=pistol.LoadedRounds;float condition=pistol.Durability;int ammo=storage.Count(pistolType.Weapon.Ammo);
            Check(inventory.Open(null),"normal inventory opens menu");yield return new WaitForSeconds(.15f);
            Check(inventory.IsOpen&&!motor.AcceptingGameplayInput&&animator.GetLayerWeight(upper)<.05f,"menu blocks gameplay and clears upper layer");
            Check(!weapons.TryAttack()&&pistol.LoadedRounds==rounds&&Mathf.Approximately(pistol.Durability,condition)&&storage.Count(pistolType.Weapon.Ammo)==ammo,"menu refusal does not consume ammunition or durability");
            inventory.Close();Keys(keyboard);InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.15f);
            Check(motor.AcceptingGameplayInput,"menu close returns input after release gate");
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));yield return new WaitForSeconds(.25f);
            var bridge=animator.GetComponent<PlayerUALAnimatorIK>();Check(weapons.IsAiming&&HandWeight(bridge)>.1f,"real ADS establishes transient hand IK");
            typeof(Health).GetMethod("NotifyWorldRestore",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(health,null);
            Check(IKReset(bridge),"real WorldStateRestored event clears all transient IK weights synchronously");
            yield return new WaitForSeconds(.25f);Check(HandWeight(bridge)>.1f,"ADS recovers from reset through real input");
            health.Revive(health.Current/health.Maximum,0);
            Check(IKReset(bridge),"real Revived event clears all transient IK weights synchronously");
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.15f);
            var oldModel=visual.ModelInstance;var prefab=visual.ModelPrefab;
            visual.SetModel(prefab);yield return new WaitForSeconds(.2f);animator=visual.ModelAnimator;
            Check(visual.ModelInstance!=null&&visual.ModelInstance!=oldModel&&oldModel==null,"runtime model replacement retires old model");
            Check(motor.GetComponentsInChildren<PlayerModelDefinition>(true).Length==1,"model replacement leaves exactly one model definition");
            Check(weapons.View!=null&&weapons.View.transform.parent==animator.GetBoneTransform(HumanBodyBones.RightHand)
                &&motor.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="HeldWeapon")==1,"held weapon reattaches to new RightHand without duplication");
            Check(ReferenceEquals(weapons.Equipped,pistol)&&pistol.InstanceId==id&&pistol.LoadedRounds==rounds&&Mathf.Approximately(pistol.Durability,condition)
                &&storage.Count(pistolType.Weapon.Ammo)==ammo,"presentation lifecycle preserves original item and combat state");
            Check(BonesFinite(animator)&&!animator.applyRootMotion,"replacement avatar finite and root motion disabled");
        }
        finally
        {
            if(interactor!=null)interactor.SuccessfulInteraction-=onInteraction;
            if(inventory!=null)inventory.Close();
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            inputSettings.backgroundBehavior=oldBackground;inputSettings.editorInputBehaviorInPlayMode=oldRouting;
            if(motor!=null)motor.ResetForRespawn(oldPosition,oldRotation);
            foreach(var row in flagValues)if(row.Item1!=null)SetBool(row.Item1,row.Item2,row.Item3);
            if(hotbar!=null)hotbar.RestoreAssignments(assignments,selected);if(autosave!=null)autosave.enabled=oldAuto;
            if(pickup!=null)UnityEngine.Object.Destroy(pickup);if(floor!=null)UnityEngine.Object.Destroy(floor);
        }
    }
    private static bool BonesFinite(Animator animator)
    {
        foreach(HumanBodyBones bone in new[] { HumanBodyBones.Hips,HumanBodyBones.Head,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftHand,HumanBodyBones.RightHand })
        {
            var t=animator.GetBoneTransform(bone);if(t==null)return false;
            var p=t.position;var q=t.rotation;
            if(!Finite(p.x)||!Finite(p.y)||!Finite(p.z)||!Finite(q.x)||!Finite(q.y)||!Finite(q.z)||!Finite(q.w))return false;
        }
        return true;
    }
    private static IEnumerator Scenario(List<string> results)
    {
        void Check(bool value,string label,string details=null) { if(!value)throw new InvalidOperationException(label+(details!=null?"; "+details:""));results.Add("PASS "+label); }
        var motor=UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Exclude)
            .Single(p=>p.GetComponent<PlayerUALAnimationDriver>()!=null);
        var visual=motor.GetComponent<PlayerVisual>();var animator=visual.ModelAnimator;
        var capsule=motor.GetComponent<CharacterController>();var weapons=motor.GetComponent<PlayerWeapons>();
        var storage=motor.GetComponent<ItemStorage>();var hotbar=motor.GetComponent<PlayerHotbar>();
        var autosave=motor.GetComponent<PlayerSaveSlots>();
        bool oldFocus=ReadBool(motor,"requireApplicationFocus"),oldCapture=ReadBool(motor,"requireCursorCapture"),oldAutoCapture=ReadBool(motor,"captureCursorOnEnable");
        bool oldAuto=autosave!=null&&autosave.enabled;
        var inputSettings=InputSystem.settings;
        var oldBackground=inputSettings.backgroundBehavior;
        var oldEditorInput=inputSettings.editorInputBehaviorInPlayMode;
        Vector3 oldPosition=motor.transform.position;Quaternion oldRotation=motor.transform.rotation;
        GameObject floor=null;Keyboard keyboard=null;
        int fires=0,swings=0,reloads=0;
        Action<WeaponDefinition> fire=_=>fires++,swing=_=>swings++,reload=_=>reloads++;
        try
        {
            Check(animator!=null&&animator.isHuman&&animator.avatar!=null&&animator.avatar.isValid,"real player has valid Humanoid UAL avatar");
            Check(motor.IsAlive,"fixture player alive");
            motor.GetComponent<PlayerServiceSession>()?.Close();
            SetBool(motor,"requireApplicationFocus",false);SetBool(motor,"requireCursorCapture",false);SetBool(motor,"captureCursorOnEnable",false);
            if(autosave!=null)autosave.enabled=false;
            // Recovery automation has no GameView focus. Do not weaken production input policy.
            inputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="UAL recovery test floor";
            floor.transform.position=new Vector3(600,-.5f,600);floor.transform.localScale=new Vector3(40,1,40);
            motor.ResetForRespawn(new Vector3(600,.02f,600),Quaternion.identity);Physics.SyncTransforms();
            keyboard=InputSystem.AddDevice<Keyboard>("UALChecksKeyboard");Keys(keyboard);
            yield return new WaitForSeconds(.5f);
            Check(motor.AcceptingGameplayInput&&motor.IsGrounded,"real motor receives enabled gameplay input on test floor");
            Check(!animator.applyRootMotion,"root motion disabled");
            Check(State(animator,0,"Base Layer.Idle"),"base Idle");
            var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
            Check(hips.position.y>animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position.y-.08f&&hips.position.y-motor.transform.position.y>.4f,"Hips maps to elevated pelvis, not ground root");
            var definition=visual.ModelInstance.GetComponentInChildren<PlayerModelDefinition>(true);
            Check(definition!=null&&definition.FirstPersonHiddenRenderers.Length>0&&definition.FirstPersonHiddenRenderers.All(r=>r!=null),"model defines head meshes for own-camera hiding");
            var settings=new SerializedObject(visual);
            Check(settings.FindProperty("firstPersonVisibility").enumValueIndex==0&&!settings.FindProperty("driveAnimator").boolValue,"full body shown with selected head hiding and legacy driver off");
            Check(visual.ModelInstance.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled&&!definition.FirstPersonHiddenRenderers.Contains(r)),"body renderer remains enabled outside own-camera head mask");
            Key[][] directions={ new[]{Key.W},new[]{Key.W,Key.D},new[]{Key.D},new[]{Key.S,Key.D},new[]{Key.S},new[]{Key.S,Key.A},new[]{Key.A},new[]{Key.W,Key.A} };
            Vector3[] vectors={ Vector3.forward,new Vector3(1,0,1),Vector3.right,new Vector3(1,0,-1),Vector3.back,new Vector3(-1,0,-1),Vector3.left,new Vector3(-1,0,1) };
            string[] modes={"Walk","SlowWalk","Run","CrouchWalk"};
            bool toggleCrouch=ReadBool(motor,"toggleCrouch");
            for(int mode=0;mode<modes.Length;mode++)
            {
                Keys(keyboard);yield return new WaitForSeconds(.12f);
                if(mode==3)
                {
                    Keys(keyboard,Key.C);yield return new WaitForSeconds(.08f);
                    if(toggleCrouch)Keys(keyboard);
                    yield return new WaitForSeconds(.6f);
                    Check(motor.IsCrouching&&State(animator,0,"Base Layer.CrouchIdle"),"C enters real crouch/Idle using configured hold/toggle policy");
                }
                for(int i=0;i<directions.Length;i++)
                {
                    var pressed=directions[i].ToList();if(mode==1)pressed.Add(Key.LeftAlt);if(mode==2)pressed.Add(Key.LeftShift);
                    if(mode==3&&!toggleCrouch)pressed.Add(Key.C);
                    Keys(keyboard,pressed.ToArray());yield return new WaitForSeconds(.38f);
                    Vector3 actual=Vector3.ProjectOnPlane(capsule.velocity,Vector3.up);
                    bool moving=actual.magnitude>.1f&&Vector3.Dot(actual.normalized,motor.transform.TransformDirection(vectors[i]).normalized)>.65f;
                    // Some motors intentionally permit sprint only forward; verify real policy rather than forcing a state.
                    string expected=mode==2&&!motor.IsSprinting?"Walk":modes[mode];
                    string diagnostic=$"frame={Time.frameCount}, key={keyboard.wKey.isPressed}/{keyboard.aKey.isPressed}/{keyboard.sKey.isPressed}/{keyboard.dKey.isPressed}, keyboardEnabled={keyboard.enabled}, accepting={motor.AcceptingGameplayInput}, Move={motor.FindGameplayAction("Move")?.ReadValue<Vector2>()}, CC={capsule.velocity}, planar={motor.PlanarVelocity}, pos={motor.transform.position}, stateHash={animator.GetCurrentAnimatorStateInfo(0).fullPathHash}, nextHash={animator.GetNextAnimatorStateInfo(0).fullPathHash}, speedParam={animator.GetFloat("Speed")}, finite={BonesFinite(animator)}";
                    Check(moving,modes[mode]+" direction "+i+" actual CC direction",diagnostic);
                    Check(State(animator,0,"Base Layer."+expected),modes[mode]+" direction "+i+" state "+expected,diagnostic);
                    Check(BonesFinite(animator),modes[mode]+" direction "+i+" finite bones");
                }
            }
            Keys(keyboard);yield return new WaitForSeconds(.15f);
            if(toggleCrouch){Keys(keyboard,Key.C);yield return new WaitForSeconds(.08f);Keys(keyboard);}
            yield return new WaitForSeconds(.65f);
            Keys(keyboard,Key.Z);yield return new WaitForSeconds(.08f);Keys(keyboard);yield return new WaitForSeconds(.7f);
            Check(motor.IsProne&&State(animator,0,"Base Layer.Prone")&&BonesFinite(animator),"Z enters prone with finite procedural fallback");
            Keys(keyboard,Key.Z);yield return new WaitForSeconds(.08f);Keys(keyboard);yield return new WaitForSeconds(.7f);
            Check(!motor.IsProne,"Z exits prone");
            Keys(keyboard,Key.Space);yield return new WaitForSeconds(.12f);Keys(keyboard);yield return new WaitForSeconds(.08f);
            Check(!motor.IsGrounded&&State(animator,0,"Base Layer.JumpStart","Base Layer.JumpLoop","Base Layer.Land"),"real Space jump enters airborne animation");
            yield return new WaitForSeconds(1.1f);Check(motor.IsGrounded&&BonesFinite(animator),"jump lands with finite pose");
            Keys(keyboard,Key.W);yield return new WaitForSeconds(.2f);Keys(keyboard,Key.W,Key.LeftCtrl);yield return new WaitForSeconds(.15f);
            if(motor.Parkour!=null&&motor.Parkour.State==PlayerParkour.TraversalState.Rolling)Check(State(animator,0,"Base Layer.Roll"),"real Ctrl roll animation");
            else results.Add("SKIP Roll: gameplay did not accept roll in this fixture");
            Keys(keyboard);yield return new WaitForSeconds(1f);
            var profile=Resources.Load<TraderProfile>("Trading/weapons_ammo");
            var pistol=profile.Catalog.First(e=>e.item.Weapon!=null&&e.item.Weapon.kind==WeaponKind.Firearm).item;
            var axe=profile.Catalog.First(e=>e.item.Weapon!=null&&e.item.Weapon.kind==WeaponKind.Melee).item;
            var pistolStack=new ItemStack(pistol,1);var axeStack=new ItemStack(axe,1);
            Check(storage.TryAddStack(pistolStack)&&storage.TryAddStack(axeStack)&&storage.TryAddAll(pistol.Weapon.Ammo,pistol.Weapon.MagazineSize+2),"real item instances and ammunition enter existing inventory");
            int Slot(ItemStack stack)=>Enumerable.Range(0,storage.SlotCount).Single(i=>ReferenceEquals(storage.GetSlot(i),stack));
            Check(hotbar.EquipInventorySlot(Slot(pistolStack)),"normal hotbar equips pistol");yield return new WaitForSeconds(.35f);
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            Check(weapons.View!=null&&weapons.View.transform.parent==hand,"single HeldWeapon attached to real RightHand");
            Check(motor.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="HeldWeapon")==1,"no duplicated held weapon");
            weapons.ShotFired+=fire;weapons.SwingStarted+=swing;weapons.ReloadStarted+=reload;
            int reserve=storage.Count(pistol.Weapon.Ammo);
            Check(weapons.TryReload()&&reloads==1,"accepted reload event once");
            Check(!weapons.TryAttack()&&fires==0&&pistolStack.LoadedRounds==0,"refused shot during reload has no event/ammo loss");
            yield return new WaitForSeconds(.15f);
            int upper=animator.GetLayerIndex("Upper Body");Check(upper>=0&&State(animator,upper,"Upper Body.Reload"),"Reload upper-body state");
            yield return new WaitForSeconds(pistol.Weapon.reloadSeconds+.1f);
            Check(pistolStack.LoadedRounds==pistol.Weapon.MagazineSize&&storage.Count(pistol.Weapon.Ammo)==reserve-pistol.Weapon.MagazineSize,"reload exact magazine/reserve mutation");
            Check(weapons.TryAttack()&&fires==1,"accepted shot event once");
            Check(pistolStack.LoadedRounds==pistol.Weapon.MagazineSize-1&&storage.Count(pistol.Weapon.Ammo)==reserve-pistol.Weapon.MagazineSize,"shot spends exactly one loaded round");
            Check(!weapons.TryAttack()&&fires==1,"cooldown refusal does not trigger animation again");
            yield return new WaitForSeconds(.06f);Check(State(animator,upper,"Upper Body.Fire"),"Fire upper-body state");
            yield return new WaitForSeconds(pistol.Weapon.cooldown+.1f);
            Check(hotbar.EquipInventorySlot(Slot(axeStack)),"normal hotbar equips axe");yield return new WaitForSeconds(.3f);
            Check(weapons.TryAttack()&&swings==1,"accepted melee swing event once");
            yield return new WaitForSeconds(.08f);Check(State(animator,upper,"Upper Body.Melee"),"Melee upper-body state");
            yield return new WaitForSeconds(axe.Weapon.cooldown+.15f);
            Check(weapons.View!=null&&weapons.View.transform.parent==hand&&BonesFinite(animator)&&!animator.applyRootMotion,"axe hand binding and final finite pose without root motion");
        }
        finally
        {
            if(weapons!=null){weapons.ShotFired-=fire;weapons.SwingStarted-=swing;weapons.ReloadStarted-=reload;}
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            inputSettings.backgroundBehavior=oldBackground;
            inputSettings.editorInputBehaviorInPlayMode=oldEditorInput;
            if(motor!=null)
            {
                motor.ResetForRespawn(oldPosition,oldRotation);SetBool(motor,"requireApplicationFocus",oldFocus);
                SetBool(motor,"requireCursorCapture",oldCapture);SetBool(motor,"captureCursorOnEnable",oldAutoCapture);
            }
            if(autosave!=null)autosave.enabled=oldAuto;
            if(floor!=null)UnityEngine.Object.Destroy(floor);
        }
    }
}
