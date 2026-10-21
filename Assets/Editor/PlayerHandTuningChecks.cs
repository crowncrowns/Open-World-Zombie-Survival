using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ZombieSurvival.Player;
using ZombieSurvival.Inventory;
using ZombieSurvival.Weapons;

/// <summary>Recovery-only transient hand tuning and cursor-lock integration checks; no saves.</summary>
public static class PlayerHandTuningChecks
{
    public static bool Completed { get; private set; }
    public static string LastReport { get; private set; }
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly string[] Settings={"firearmHipPosition","firearmAimPosition","leftFirearmGrip","firearmElbowHint","aimWeight","meleeReadyPosition","meleeReadyWeight"};
    public static IEnumerator RunPlay()
    {
        Completed=false;LastReport="running";var lines=new List<string>();
        if(!Application.isPlaying||!Application.dataPath.Contains("_CodexRecovery"))
        {LastReport="FAIL Recovery Play only";Completed=true;yield break;}
        var routine=Scenario(lines);
        try
        {
            while(true)
            {
                bool more;try{more=routine.MoveNext();}catch(Exception e){lines.Add("FAIL "+e);break;}
                if(!more)break;yield return routine.Current;
            }
        }
        finally
        {
            try{(routine as IDisposable)?.Dispose();}catch(Exception e){lines.Add("FAIL cleanup "+e);}
            LastReport=string.Join("\n",lines);Completed=true;
        }
    }
    private static void Check(bool ok,string text,List<string> lines)
    {if(!ok)throw new InvalidOperationException(text);lines.Add("PASS "+text);}
    private static void Flag(UnityEngine.Object obj,string name,bool value)
    {var so=new SerializedObject(obj);so.FindProperty(name).boolValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static float Weight(PlayerUALAnimatorIK ik)=> (float)typeof(PlayerUALAnimatorIK).GetField("handsWeight",Hidden).GetValue(ik);
    private static IEnumerator Scenario(List<string> lines)
    {
        var p=UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        var driver=p.GetComponent<PlayerUALAnimationDriver>();var weapons=p.GetComponent<PlayerWeapons>();
        var bag=p.GetComponent<ItemStorage>();var hotbar=p.GetComponent<PlayerHotbar>();
        var animator=p.GetComponent<PlayerVisual>().ModelAnimator;var ik=animator.GetComponent<PlayerUALAnimatorIK>();
        var autosave=p.GetComponent<ZombieSurvival.Persistence.PlayerSaveSlots>();bool oldAuto=autosave.enabled;
        var flags=new[]{"requireApplicationFocus","requireCursorCapture","captureCursorOnEnable"};
        var motorData=new SerializedObject(p);var oldFlags=flags.Select(n=>motorData.FindProperty(n).boolValue).ToArray();
        var settings=InputSystem.settings;var oldBackground=settings.backgroundBehavior;var oldEditor=settings.editorInputBehaviorInPlayMode;
        var oldCursor=Cursor.lockState;bool oldCursorVisible=Cursor.visible;
        var oldPosition=p.transform.position;var oldRotation=p.transform.rotation;
        var saved=new Dictionary<string,object>();var data=new SerializedObject(driver);
        foreach(string name in Settings){var prop=data.FindProperty(name);if(prop==null)throw new InvalidOperationException("Missing root setting "+name);saved[name]=prop.propertyType==SerializedPropertyType.Vector3?(object)prop.vector3Value:prop.floatValue;}
        Mouse mouse=null;Keyboard keyboard=null;GameObject floor=null,lockOwner=null;
        ItemStack created=null;int previousSlot=hotbar.SelectedIndex;var previousAssignments=hotbar.CaptureAssignments();
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        void CaptureCursor()=>typeof(FirstPersonController).GetMethod("SetCursorCaptured",Hidden).Invoke(p,new object[]{true});
        void CurrentRig()
        {
            animator=p.GetComponent<PlayerVisual>().ModelAnimator;
            if(animator==null)throw new InvalidOperationException("Actual PlayerVisual.ModelAnimator missing frame="+Time.frameCount);
            ik=animator.GetComponent<PlayerUALAnimatorIK>();
            if(ik==null)throw new InvalidOperationException("Actual bridge missing animator="+animator.GetEntityId()+" frame="+Time.frameCount);
        }
        try
        {
            autosave.enabled=false;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach(string n in flags)Flag(p,n,false);
            mouse=InputSystem.AddDevice<Mouse>("HandTuningMouse");keyboard=InputSystem.AddDevice<Keyboard>("HandTuningKeyboard");
            Keys();InputSystem.QueueStateEvent(mouse,new MouseState());
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Hand tuning check floor";floor.transform.position=new Vector3(600,-.5f,600);floor.transform.localScale=new Vector3(40,1,40);
            p.ResetForRespawn(new Vector3(600,.02f,600),Quaternion.identity);Physics.SyncTransforms();
            yield return new WaitForSeconds(.35f);yield return null;CurrentRig();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab");
            var sourceDriver=source.GetComponent<PlayerUALAnimationDriver>();var sourceData=new SerializedObject(sourceDriver);
            Check(Settings.All(n=>sourceData.FindProperty(n)!=null),"root serialized hand settings exist on authored player before runtime",lines);
            data.Update();
            foreach(string n in Settings)
            {var prop=data.FindProperty(n);if(prop.propertyType==SerializedPropertyType.Vector3)prop.vector3Value=(Vector3)saved[n]+new Vector3(.001f,.002f,.001f);else prop.floatValue=.87f;}
            int editFrame=Time.frameCount;data.ApplyModifiedPropertiesWithoutUndo();yield return new WaitForSeconds(.35f);yield return null;CurrentRig();
            Check(Time.frameCount>=editFrame+2,"real frames elapsed after root edits",lines);
            var bridgeData=new SerializedObject(ik);
            foreach(string n in Settings)
            {
                CurrentRig();bridgeData=new SerializedObject(ik);data.Update();bridgeData.Update();
                var root=data.FindProperty(n);var bridge=bridgeData.FindProperty(n);
                string expected=root.propertyType==SerializedPropertyType.Vector3?root.vector3Value.ToString("F6"):root.floatValue.ToString("F6");
                string actual=bridge==null?"MISSING":bridge.propertyType==SerializedPropertyType.Vector3?bridge.vector3Value.ToString("F6"):bridge.floatValue.ToString("F6");
                Check(bridge!=null&&(root.propertyType==SerializedPropertyType.Vector3?(root.vector3Value-bridge.vector3Value).sqrMagnitude<1e-10f:Mathf.Abs(root.floatValue-bridge.floatValue)<.0001f),
                    "live root→bridge "+n+" expected="+expected+" actual="+actual+" root="+driver.GetEntityId()+" animator="+animator.GetEntityId()+" bridge="+ik.GetEntityId()+" frame="+Time.frameCount+" editFrame="+editFrame,lines);
            }
            // Restore calibrated pose before testing actual ready/ADS holds.
            data.Update();foreach(var row in saved){var prop=data.FindProperty(row.Key);if(row.Value is Vector3 v)prop.vector3Value=v;else prop.floatValue=(float)row.Value;}data.ApplyModifiedPropertiesWithoutUndo();
            var type=Resources.Load<ZombieSurvival.Services.TraderProfile>("Trading/weapons_ammo").Catalog.First(e=>e.item.Weapon?.kind==WeaponKind.Firearm).item;
            int slot=Enumerable.Range(0,bag.SlotCount).Where(i=>bag.GetSlot(i)?.Definition==type).DefaultIfEmpty(-1).First();
            if(slot<0){created=new ItemStack(type,1);Check(bag.TryAddStack(created),"temporary pistol fixture admitted",lines);slot=Enumerable.Range(0,bag.SlotCount).Single(i=>ReferenceEquals(bag.GetSlot(i),created));}
            Check(hotbar.EquipInventorySlot(slot),"actual hotbar pistol equip",lines);yield return new WaitForSeconds(.5f);CurrentRig();
            Check(weapons.View!=null&&weapons.View.transform.parent==animator.GetBoneTransform(HumanBodyBones.RightHand),"held view on actual RightHand",lines);
            Check((weapons.View.transform.localPosition-weapons.FirearmHandPosition).sqrMagnitude<1e-8f&&Quaternion.Angle(weapons.View.transform.localRotation,weapons.FirearmHandRotation)<.01f,"root PlayerWeapons mount applied",lines);
            Flag(p,"requireCursorCapture",true);CaptureCursor();yield return new WaitForSeconds(.2f);
            foreach(bool ads in new[]{false,true})
            {
                InputSystem.QueueStateEvent(mouse,ads?new MouseState().WithButton(MouseButton.Right):new MouseState());yield return new WaitForSeconds(.4f);
                Check(p.AcceptingGameplayInput&&weapons.IsAiming==ads,"ready input before Esc "+ads,lines);
                int rounds=weapons.Equipped.LoadedRounds;float durability=weapons.Equipped.Durability;
                Keys(Key.Escape);yield return new WaitForSeconds(.12f);Keys();yield return new WaitForSeconds(.25f);
                Check(!p.AcceptingGameplayInput&&p.GameplayInputEnabled,"real Esc releases cursor without owner lock",lines);
                Check(weapons.View.activeInHierarchy&&weapons.IsAiming==ads&&Weight(ik)>.5f&&animator.GetLayerWeight(driver.UpperBodyLayerIndex)>.5f,"Esc keeps ready visual/IK/ADS "+ads,lines);
                Check(!weapons.TryAttack()&&!weapons.TryReload()&&weapons.Equipped.LoadedRounds==rounds&&weapons.Equipped.Durability==durability,"Esc denies gameplay without spending "+ads,lines);
                data.Update();var edited=data.FindProperty(ads?"firearmAimPosition":"firearmHipPosition");var before=edited.vector3Value;
                edited.vector3Value=before+Vector3.right*.001f;data.ApplyModifiedPropertiesWithoutUndo();yield return new WaitForSeconds(.12f);
                CurrentRig();bridgeData=new SerializedObject(ik);bridgeData.Update();Check((bridgeData.FindProperty(ads?"firearmAimPosition":"firearmHipPosition").vector3Value-(before+Vector3.right*.001f)).sqrMagnitude<1e-10f&&weapons.View.activeInHierarchy,"root edit while cursor free reaches held pose "+ads,lines);
                data.Update();data.FindProperty(ads?"firearmAimPosition":"firearmHipPosition").vector3Value=before;data.ApplyModifiedPropertiesWithoutUndo();
                CaptureCursor();yield return new WaitForSeconds(.2f);
            }
            // Re-enabled button actions need a fresh release/press; do not assume held RMB survives recapture.
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));yield return new WaitForSeconds(.35f);CurrentRig();
            Check(p.AcceptingGameplayInput&&weapons.IsAiming,"fresh ADS established before focus callback",lines);
            // Exercise the exact application-focus-loss callback. This is not an OS focus-delivery test.
            typeof(FirstPersonController).GetMethod("OnApplicationFocus",Hidden).Invoke(p,new object[]{false});yield return new WaitForSeconds(.25f);
            CurrentRig();
            Check(!p.AcceptingGameplayInput&&weapons.View.activeInHierarchy&&weapons.IsAiming&&Weight(ik)>.5f,
                "focus-loss callback retains ADS presentation but rejects gameplay; accepting="+p.AcceptingGameplayInput+" gameplay="+p.GameplayInputEnabled+" view="+weapons.View.activeInHierarchy+" aim="+weapons.IsAiming+" handsWeight="+Weight(ik)+" cursor="+Cursor.lockState+" frame="+Time.frameCount,lines);
            Check(!weapons.TryAttack()&&!weapons.TryReload(),"focus-loss callback cannot fire/reload",lines);
            lockOwner=new GameObject("Hand tuning owner lock");p.SetGameplayInputLock(lockOwner,true);yield return new WaitForSeconds(.35f);
            Check(!p.GameplayInputEnabled&&!weapons.View.activeInHierarchy&&Weight(ik)<.01f&&animator.GetLayerWeight(driver.UpperBodyLayerIndex)<.01f,"owner lock suspends visual and gameplay",lines);
            InputSystem.QueueStateEvent(mouse,new MouseState());Keys();yield return new WaitForSeconds(.15f);
            Check(!mouse.rightButton.isPressed&&!mouse.leftButton.isPressed&&!keyboard.escapeKey.isPressed,"button releases processed before owner unlock",lines);
            p.SetGameplayInputLock(lockOwner,false);yield return null;yield return new WaitForSeconds(.15f);
            Check(p.GameplayInputEnabled,"release gate cleared before explicit cursor recapture; gameplay="+p.GameplayInputEnabled+" accepting="+p.AcceptingGameplayInput+" cursor="+Cursor.lockState+" frame="+Time.frameCount,lines);
            CaptureCursor();yield return new WaitForSeconds(.4f);CurrentRig();
            Check(p.AcceptingGameplayInput&&weapons.View.activeInHierarchy&&Weight(ik)>.5f,
                "owner unlock resumes ready only after released controls; accepting="+p.AcceptingGameplayInput+" gameplay="+p.GameplayInputEnabled+" view="+weapons.View.activeInHierarchy+" handsWeight="+Weight(ik)+" cursor="+Cursor.lockState+" frame="+Time.frameCount,lines);
            lines.Add("INFO Focus callback simulated explicitly; native OS focus delivery requires separate manual verification.");
        }
        finally
        {
            if(lockOwner!=null){p.SetGameplayInputLock(lockOwner,false);UnityEngine.Object.Destroy(lockOwner);}
            if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            data.Update();foreach(var row in saved){var prop=data.FindProperty(row.Key);if(row.Value is Vector3 v)prop.vector3Value=v;else prop.floatValue=(float)row.Value;}data.ApplyModifiedPropertiesWithoutUndo();
            for(int i=0;i<flags.Length;i++)Flag(p,flags[i],oldFlags[i]);
            settings.backgroundBehavior=oldBackground;settings.editorInputBehaviorInPlayMode=oldEditor;autosave.enabled=oldAuto;
            if(created!=null){int index=Enumerable.Range(0,bag.SlotCount).Where(i=>ReferenceEquals(bag.GetSlot(i),created)).DefaultIfEmpty(-1).First();if(index>=0)bag.TryExtract(index,1,out _);}
            hotbar.RestoreAssignments(previousAssignments,previousSlot);
            p.ResetForRespawn(oldPosition,oldRotation);Cursor.lockState=oldCursor;Cursor.visible=oldCursorVisible;
            if(floor!=null)UnityEngine.Object.Destroy(floor);
        }
    }
}
