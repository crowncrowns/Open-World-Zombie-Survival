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
    public const string Evidence="S:/Games/Unity Projects/_CodexRecovery/PlayerPistolFix-20261021";
    public static bool Completed {get;private set;}
    public static string Report {get;private set;}
    public static IEnumerator Run(bool crouch=false,bool moving=false)
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
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Pistol pose check floor";floor.transform.position=new Vector3(600,-.5f,600);floor.transform.localScale=new Vector3(100,1,100);
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
            bool holdC=crouch&&!new SerializedObject(p).FindProperty("toggleCrouch").boolValue;
            if(moving)InputSystem.QueueStateEvent(keyboard,holdC?new KeyboardState(Key.C,Key.W):new KeyboardState(Key.W));
            foreach(float angle in new[]{0f,-45f,-85f,35f,70f})
            foreach(bool aim in new[]{false,true})
            {
                InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.12f);
                pitch.SetValue(p,angle);
                if(aim)InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));
                yield return new WaitForSeconds(.45f);
                if(!p.AcceptingGameplayInput||weapons.IsAiming!=aim)throw new InvalidOperationException("Pose input refused");
                string direction=angle==-45?"up45":angle==35?"down35":angle<0?"up":angle>0?"down":"level";
                string name=(crouch?"crouch-":"")+(moving?"moving-":"")+direction+(aim?"-ads":"-hip");
                var ownShirt=visual.ModelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name=="Survivalist shirt (own camera)").ToArray();
                if(ownShirt.Length!=1)throw new InvalidOperationException(name+" expected exactly one own-camera shirt");
                var shirt=ownShirt[0];
                var original=visual.ModelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name=="SK_Military_Shirt3"&&r.gameObject.activeInHierarchy&&r.rootBone==shirt.rootBone&&r.bones.SequenceEqual(shirt.bones)&&r.sharedMaterials.SequenceEqual(shirt.sharedMaterials)).ToArray();
                if(original.Length!=1)throw new InvalidOperationException(name+" original/own shirt bones/root/materials mismatch");
                var originalShadow=original[0].shadowCastingMode;
                Capture(camera,name);AssertShirtRestored(original[0],shirt,originalShadow,name+" own");
                Capture(camera,name+"-external",true);AssertShirtRestored(original[0],shirt,originalShadow,name+" external");
                AssertClothClear(shirt,camera,name);
                if(moving&&(p.PlanarVelocity.magnitude<.1f||p.IsCrouching!=crouch))throw new InvalidOperationException(name+" moving input fixture stopped/wrong posture");
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
            report.AppendLine("PASS all shirt poses clear the camera near plane inside the captured frustum; own/external renderer states restored");
            var restore=typeof(FirstPersonController).GetMethod("RestoreSavedPose",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            restore.Invoke(p,new object[]{p.transform.position,p.transform.rotation,85f,false,crouch});
            float restoredPitch=(float)pitch.GetValue(p);
            var capsule=p.GetComponent<CharacterController>();Vector3 restoredCamera=p.transform.InverseTransformPoint(camera.transform.position);
            float capHalf=Mathf.Max(0,capsule.height*.5f-capsule.radius);
            float dy=restoredCamera.y-Mathf.Clamp(restoredCamera.y,capsule.center.y-capHalf,capsule.center.y+capHalf);
            float capRadius=Mathf.Sqrt(Mathf.Max(0,capsule.radius*capsule.radius-dy*dy))-.02f;
            float actualXZ=new Vector2(restoredCamera.x-capsule.center.x,restoredCamera.z-capsule.center.z).magnitude;
            var baseCamera=(Vector3)typeof(FirstPersonController).GetField("standingCameraPosition",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p);
            Vector2 crouchOffset=crouch?p.CrouchCameraOffset:Vector2.zero;
            Vector2 expectedXZ=Vector2.ClampMagnitude(new Vector2(baseCamera.x+crouchOffset.x-capsule.center.x,baseCamera.z+crouchOffset.y+p.DownwardCameraShift-capsule.center.z),Mathf.Max(0,capRadius));
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
            File.WriteAllText(Evidence+"/"+(crouch?"crouch-":"")+(moving?"moving-":"")+"matrix.txt",Report);
        }
    }
    private static void AssertShirtRestored(SkinnedMeshRenderer original,SkinnedMeshRenderer own,UnityEngine.Rendering.ShadowCastingMode saved,string name)
    {
        if(original.shadowCastingMode!=saved||saved==UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly||!own.forceRenderingOff)
            throw new InvalidOperationException(name+" camera renderer state not restored / own shirt visible externally");
        if(original.rootBone!=own.rootBone||!original.bones.SequenceEqual(own.bones)||!original.sharedMaterials.SequenceEqual(own.sharedMaterials))
            throw new InvalidOperationException(name+" shirt binding/materials changed during capture");
    }
    private static void AssertClothClear(SkinnedMeshRenderer shirt,Camera camera,string name)
    {
        var baked=new Mesh();
        try
        {
            shirt.BakeMesh(baked);
            var points=baked.vertices.Select(v=>camera.transform.InverseTransformPoint(shirt.transform.TransformPoint(v))).ToArray();
            var triangles=baked.triangles;float plane=camera.nearClipPlane;
            float halfY=plane*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad),halfX=halfY*1280f/720;
            bool CrossesRect(Vector2 a,Vector2 b)
            {
                float enter=0,exit=1;
                for(int axis=0;axis<2;axis++)
                {
                    float delta=b[axis]-a[axis],half=axis==0?halfX:halfY;
                    if(Mathf.Abs(delta)<.000001f){if(Mathf.Abs(a[axis])>half)return false;continue;}
                    float t0=(-half-a[axis])/delta,t1=(half-a[axis])/delta;
                    enter=Mathf.Max(enter,Mathf.Min(t0,t1));exit=Mathf.Min(exit,Mathf.Max(t0,t1));
                    if(enter>exit)return false;
                }
                return true;
            }
            for(int i=0;i<triangles.Length;i+=3)
            {
                var p0=points[triangles[i]];var p1=points[triangles[i+1]];var p2=points[triangles[i+2]];
                if(Mathf.Min(p0.z,Mathf.Min(p1.z,p2.z))>plane||Mathf.Max(p0.z,Mathf.Max(p1.z,p2.z))<plane)continue;
                var crossings=new System.Collections.Generic.List<Vector2>(3);
                void Edge(Vector3 a,Vector3 b)
                {
                    if((a.z-plane)*(b.z-plane)>0||Mathf.Abs(b.z-a.z)<.000001f)return;
                    Vector3 point=Vector3.Lerp(a,b,(plane-a.z)/(b.z-a.z));crossings.Add(new Vector2(point.x,point.y));
                }
                Edge(p0,p1);Edge(p1,p2);Edge(p2,p0);
                if(crossings.Count>=2&&CrossesRect(crossings[0],crossings[1]))
                {
                    var animator=shirt.GetComponentInParent<Animator>();
                    string rig=string.Join("\n",new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand}
                        .Select(b=>b+"="+camera.transform.InverseTransformPoint(animator.GetBoneTransform(b).position).ToString("F4")));
                    File.WriteAllText(Evidence+"/"+name+"-collision.txt",rig+"\ntriangle="+p0+","+p1+","+p2);
                    throw new InvalidOperationException(name+" shirt triangle "+(i/3)+" intersects the camera near plane in the visible frustum");
                }
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(baked);}
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
        int renderingCallbacks=0;bool visibilityApplied=false;
        void Observe(UnityEngine.Rendering.ScriptableRenderContext context,Camera current)
        {
            if(current!=camera)return;renderingCallbacks++;
            var v=UnityEngine.Object.FindAnyObjectByType<PlayerVisual>();
            var pair=v.ModelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var original=pair.Single(r=>r.name=="SK_Military_Shirt3");
            var own=pair.Single(r=>r.name=="Survivalist shirt (own camera)");
            visibilityApplied=external?own.forceRenderingOff&&original.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                :!own.forceRenderingOff&&original.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering+=Observe;
        try
        {
            camera.aspect=1280f/720;
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            if(renderingCallbacks!=1||!visibilityApplied)throw new InvalidOperationException(name+" capture bypassed own-camera visibility callbacks");
            RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Evidence+"/"+name+".png",image.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering-=Observe;
            camera.targetTexture=target;camera.aspect=aspect;RenderTexture.active=active;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);if(temp!=null)UnityEngine.Object.DestroyImmediate(temp);
        }
    }
}
