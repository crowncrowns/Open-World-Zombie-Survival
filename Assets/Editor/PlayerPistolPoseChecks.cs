using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ZombieSurvival.Player;
using ZombieSurvival.Inventory;
using ZombieSurvival.Weapons;

public static class PlayerPistolPoseChecks
{
    public const string Evidence="S:/Games/Unity Projects/_CodexRecovery/PlayerPistolFix-20261020";
    public static bool Completed {get;private set;}
    public static string Report {get;private set;}
    public static IEnumerator Run(bool crouch=false)
    {
        if(!Application.isPlaying||!Application.dataPath.Contains("_CodexRecovery"))throw new InvalidOperationException("Recovery Play only");
        Completed=false;Report="running";var report=new StringBuilder();
        var p=UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();var camera=p.GetComponentInChildren<Camera>();
        var visual=p.GetComponent<PlayerVisual>();var weapons=p.GetComponent<PlayerWeapons>();var bag=p.GetComponent<ItemStorage>();
        var autosave=p.GetComponent<ZombieSurvival.Persistence.PlayerSaveSlots>();bool oldAuto=autosave.enabled;
        var so=new SerializedObject(p);var flags=new[]{"requireApplicationFocus","requireCursorCapture","captureCursorOnEnable"};var oldFlags=flags.Select(n=>so.FindProperty(n).boolValue).ToArray();
        var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
        var position=p.transform.position;var rotation=p.transform.rotation;Mouse mouse=null;Keyboard keyboard=null;GameObject floor=null;
        var pitch=typeof(FirstPersonController).GetField("pitch",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        try
        {
            Directory.CreateDirectory(Evidence);autosave.enabled=false;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse=InputSystem.AddDevice<Mouse>("PistolPoseMouse");InputSystem.QueueStateEvent(mouse,new MouseState());
            keyboard=InputSystem.AddDevice<Keyboard>("PistolPoseKeyboard");InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            foreach(var n in flags)so.FindProperty(n).boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Pistol pose check floor";floor.transform.position=new Vector3(600,-.5f,600);floor.transform.localScale=new Vector3(40,1,40);
            p.ResetForRespawn(new Vector3(600,.02f,600),Quaternion.identity);Physics.SyncTransforms();
            var type=Resources.Load<ZombieSurvival.Services.TraderProfile>("Trading/weapons_ammo").Catalog.First(e=>e.item.Weapon?.kind==WeaponKind.Firearm).item;
            int slot=Enumerable.Range(0,bag.SlotCount).Where(i=>bag.GetSlot(i)?.Definition==type).DefaultIfEmpty(-1).First();
            if(slot<0){var stack=new ItemStack(type,1);if(!bag.TryAddStack(stack))throw new InvalidOperationException("Fixture inventory full");slot=Enumerable.Range(0,bag.SlotCount).Single(i=>ReferenceEquals(stack,bag.GetSlot(i)));}
            if(!p.GetComponent<PlayerHotbar>().EquipInventorySlot(slot))throw new InvalidOperationException("Equip refused");
            yield return new WaitForSeconds(.5f);
            if(crouch)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.C));yield return new WaitForSeconds(.12f);
                if(new SerializedObject(p).FindProperty("toggleCrouch").boolValue)InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                yield return new WaitForSeconds(.55f);
                if(!p.IsCrouching||p.IsProne)throw new InvalidOperationException("Crouch fixture did not enter real crouch");
            }
            foreach(float angle in new[]{0f,-85f,70f})
            foreach(bool aim in new[]{false,true})
            {
                InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.12f);
                pitch.SetValue(p,angle);
                if(aim)InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));
                yield return new WaitForSeconds(.45f);
                if(!p.AcceptingGameplayInput||weapons.IsAiming!=aim)throw new InvalidOperationException("Pose input refused");
                string name=(crouch?"crouch-":"")+(angle<0?"up":angle>0?"down":"level")+(aim?"-ads":"-hip");
                Capture(camera,name);Capture(camera,name+"-external",true);
                var a=visual.ModelAnimator;var hand=a.GetBoneTransform(HumanBodyBones.RightHand);
                var middle=a.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate);
                Vector3 grip=weapons.View.transform.TransformPoint(new Vector3(-.08f,-.035f,0));
                Vector3 local=camera.transform.InverseTransformPoint(grip);
                float middleDistance=Vector3.Distance(middle.position,grip);
                float barrelAngle=Vector3.Angle(weapons.View.transform.right,camera.transform.forward);
                var bridge=a.GetComponent<PlayerUALAnimatorIK>();var bridgeData=new SerializedObject(bridge);
                Vector3 leftGrip=weapons.View.transform.TransformPoint(bridgeData.FindProperty("leftFirearmGrip").vector3Value);
                float leftResidual=Vector3.Distance(leftGrip,a.GetBoneTransform(HumanBodyBones.LeftHand).position);
                float nearDepth=MinimumMeshDepth(weapons.View,camera);
                report.AppendLine(name+" frame="+Time.frameCount+" pitch="+camera.transform.localEulerAngles.x+" gripCamera="+local.ToString("F3")+" wrist="+hand.InverseTransformPoint(grip).ToString("F3")+" middleDistance="+middleDistance.ToString("F3")+" barrelAngle="+barrelAngle.ToString("F2")+" leftResidual="+leftResidual.ToString("F3")+" conservativeMeshNearDepth="+nearDepth.ToString("F3")+" view="+weapons.View.activeInHierarchy);
                if(!weapons.View.activeInHierarchy||weapons.View.transform.parent!=hand||local.z<.08f)throw new InvalidOperationException(name+" weapon hidden/near camera");
                if(barrelAngle>=3f||middleDistance>=.04f||leftResidual>=.04f||nearDepth<Mathf.Max(.12f,camera.nearClipPlane))
                    throw new InvalidOperationException(name+" barrel/grip/near-depth assertion failed; see metrics");
            }
            var restore=typeof(FirstPersonController).GetMethod("RestoreSavedPose",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            restore.Invoke(p,new object[]{p.transform.position,p.transform.rotation,85f,false,crouch});
            float restoredPitch=(float)pitch.GetValue(p);
            var capsule=p.GetComponent<CharacterController>();Vector3 restoredCamera=p.transform.InverseTransformPoint(camera.transform.position);
            float capHalf=Mathf.Max(0,capsule.height*.5f-capsule.radius);
            float dy=restoredCamera.y-Mathf.Clamp(restoredCamera.y,capsule.center.y-capHalf,capsule.center.y+capHalf);
            float capRadius=Mathf.Sqrt(Mathf.Max(0,capsule.radius*capsule.radius-dy*dy))-.02f;
            float actualXZ=new Vector2(restoredCamera.x-capsule.center.x,restoredCamera.z-capsule.center.z).magnitude;
            var baseCamera=(Vector3)typeof(FirstPersonController).GetField("standingCameraPosition",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p);
            Vector2 expectedXZ=Vector2.ClampMagnitude(new Vector2(baseCamera.x-capsule.center.x,baseCamera.z+p.DownwardCameraShift-capsule.center.z),Mathf.Max(0,capRadius));
            Vector2 actualOffset=new Vector2(restoredCamera.x-capsule.center.x,restoredCamera.z-capsule.center.z);
            if(Mathf.Abs(p.MaximumDownwardLook-70f)>.01f||Mathf.Abs(restoredPitch-70f)>.01f||actualXZ>capRadius+.0001f||(actualOffset-expectedXZ).magnitude>.0001f)
                throw new InvalidOperationException("RestoreSavedPose same-frame clamp/capsule failed: pitch="+restoredPitch+" camera="+restoredCamera+" XZ="+actualXZ+" allowed="+capRadius);
            report.AppendLine("PASS RestoreSavedPose85→70 and same-frame camera inside capsule; camera="+restoredCamera.ToString("F3"));
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.1f);
            InputSystem.QueueDeltaStateEvent(mouse.delta,new Vector2(0,-3000));yield return new WaitForSeconds(.15f);
            float actual=(float)pitch.GetValue(p);
            if(Mathf.Abs(actual-p.MaximumDownwardLook)>.1f)throw new InvalidOperationException("Actual mouse down clamp="+actual);
            report.AppendLine("PASS real mouse downward limit="+actual+"; upward limit unchanged");
            Report=report.ToString();
        }
        finally
        {
            if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(floor!=null)UnityEngine.Object.Destroy(floor);
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;
            so.Update();for(int i=0;i<flags.Length;i++)so.FindProperty(flags[i]).boolValue=oldFlags[i];so.ApplyModifiedPropertiesWithoutUndo();autosave.enabled=oldAuto;
            p.ResetForRespawn(position,rotation);Completed=true;
            if(Report=="running")Report="FAILED\n"+report;
            File.WriteAllText(Evidence+"/"+(crouch?"crouch-":"")+"matrix.txt",Report);
        }
    }
    private static float MinimumMeshDepth(GameObject view,Camera camera)
    {
        float minimum=float.PositiveInfinity;
        foreach(var filter in view.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=filter.sharedMesh;if(mesh==null)continue;
            // Local mesh bounds are conservative even for non-readable imported meshes.
            var bounds=mesh.bounds;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z));
                minimum=Mathf.Min(minimum,camera.transform.InverseTransformPoint(filter.transform.TransformPoint(corner)).z);
            }
        }
        if(float.IsInfinity(minimum))throw new InvalidOperationException("Weapon has no mesh for near-depth check");
        return minimum;
    }
    public static void Capture(Camera camera,string name,bool external=false)
    {
        GameObject temp=null;
        if(external)
        {
            var p=UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();temp=new GameObject("Pistol pose external camera");camera=temp.AddComponent<Camera>();
            camera.transform.position=p.transform.TransformPoint(new Vector3(1.8f,1.65f,2.2f));camera.transform.LookAt(p.transform.position+Vector3.up*1.3f);camera.fieldOfView=42;
        }
        var target=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
        var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.aspect=1280f/720;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Evidence+"/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=target;camera.aspect=aspect;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);if(temp!=null)UnityEngine.Object.DestroyImmediate(temp);}
    }
}
