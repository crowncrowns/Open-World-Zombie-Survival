using UnityEngine;
using ZombieSurvival.Interaction;
using ZombieSurvival.Weapons;

namespace ZombieSurvival.Player
{
    /// <summary>Presentation-only UAL hand poses and bounded directional steps; never locks feet to world space.</summary>
    [DefaultExecutionOrder(300), DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class PlayerUALAnimatorIK : MonoBehaviour
    {
        [SerializeField, Range(0,1), Tooltip("Сила перенаправления траектории ног при движении вбок/назад. Не заменяет отдельные клипы strafe/backward.")]
        private float directionWeight = .85f;
        [SerializeField, Range(.05f,.5f), Tooltip("Максимальная дополнительная коррекция позиции каждой стопы, м.")]
        private float maximumFootCorrection = .3f;
        [SerializeField, Range(0,45), Tooltip("Максимальная дополнительная ориентация носка по направлению шага, градусов; задний ход не выворачивает стопы на 180°.")]
        private float maximumFootYaw = 35;
        [SerializeField, HideInInspector, Tooltip("Положение центра модели пистолета относительно камеры при стрельбе от бедра, м.")]
        private Vector3 firearmHipPosition = new Vector3(.11f,-.16f,.4f);
        [SerializeField, HideInInspector, Tooltip("Положение центра модели пистолета относительно камеры при прицеливании, м.")]
        private Vector3 firearmAimPosition = new Vector3(0,-.04f,.44f);
        [SerializeField, HideInInspector, Tooltip("Левая кисть относительно модели пистолета; общий хват используется и без ПКМ.")]
        private Vector3 leftFirearmGrip = new Vector3(-.12f,-.055f,.035f);
        [SerializeField, HideInInspector, Range(0,1), Tooltip("Сила прицельного IK рук. Кисти следуют взгляду с учётом осей Avatar; вытягивание ограничено длиной руки.")]
        private float aimWeight = 1f;
        [SerializeField, HideInInspector, Tooltip("Подсказка сгиба локтей относительно плеча и корпуса, м: X наружу, Y вверх, Z вперёд.")]
        private Vector3 firearmElbowHint = new Vector3(.6f,.2f,.2f);
        [SerializeField, HideInInspector, Tooltip("Положение правой кисти с оружием ближнего боя в готовности относительно камеры, м. Только AxeIdle, не удар.")]
        private Vector3 meleeReadyPosition = new Vector3(.18f,-.2f,.25f);
        [SerializeField, HideInInspector, Range(0,1), Tooltip("Сила IK готовности ближнего оружия. При атаке коррекция полностью освобождается.")]
        private float meleeReadyWeight = 1f;
        private Animator animator;
        private PlayerUALAnimationDriver owner;
        private float feetWeight, handsWeight, meleeWeight;
        private float firearmAimBlend;
        private Quaternion rightBoneFromGoal;
        private bool handFrameKnown;
        internal void SetHandSettings(Vector3 hip, Vector3 aim, Vector3 leftGrip, float weight,
            Vector3 elbow, Vector3 melee, float meleeWeight)
        {
            firearmHipPosition=hip; firearmAimPosition=aim; leftFirearmGrip=leftGrip;
            aimWeight=Mathf.Clamp01(weight); firearmElbowHint=elbow;
            meleeReadyPosition=melee; meleeReadyWeight=Mathf.Clamp01(meleeWeight);
        }
        public void Configure(PlayerUALAnimationDriver value)
        {
            owner=value; animator=GetComponent<Animator>(); feetWeight=handsWeight=meleeWeight=0;
            handFrameKnown=false;
            firearmAimBlend=0;
        }
        private void OnDisable() { feetWeight=handsWeight=meleeWeight=0; }
        private void OnAnimatorIK(int layerIndex)
        {
            if(owner==null || !owner.isActiveAndEnabled || animator==null || !animator.isHuman || animator.avatar==null || !animator.avatar.isValid)return;
            owner.ApplyHandSettings(this);
            int handLayer=owner.UpperBodyLayerIndex>=0?owner.UpperBodyLayerIndex:0;
            if(layerIndex!=0 && layerIndex!=handLayer)return;
            var motor=owner.Motor; if(motor==null)return;
            if(layerIndex==0 && !handFrameKnown)
            {
                var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                if(hand!=null)
                {
                    rightBoneFromGoal=Quaternion.Inverse(animator.GetIKRotation(AvatarIKGoal.RightHand))*hand.rotation;
                    handFrameKnown=true;
                }
            }
            bool safe=motor.IsAlive && motor.isActiveAndEnabled && motor.GameplayInputEnabled && !motor.IsProne && (motor.Parkour==null || !motor.Parkour.OwnsMovement);
            Vector3 velocity=owner.LocalVelocity; float speed=velocity.magnitude;
            bool stepping=safe && motor.IsGrounded && speed>.15f && owner.DirectionalFootIK;
            if(layerIndex==0)feetWeight=Mathf.MoveTowards(feetWeight,stepping?Mathf.Clamp01(directionWeight):0,Time.deltaTime*8);
            // Do not write any goal outside locomotion: legacy crawl/ledge IK owns those poses.
            if(layerIndex==0 && safe && motor.IsGrounded && feetWeight>0 && speed>.001f)
            {
                Vector3 local=velocity/speed;
                float yaw=Mathf.Atan2(local.x,Mathf.Abs(local.z))*Mathf.Rad2Deg;
                CorrectFoot(AvatarIKGoal.LeftFoot,local,yaw);
                CorrectFoot(AvatarIKGoal.RightFoot,local,yaw);
            }
            if(layerIndex!=handLayer)return;
            if(safe)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand,0);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,0);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,0);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,0);
                animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow,0);
                animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow,0);
            }
            bool handSafe=safe && !motor.IsSprinting;
            var camera=owner.GetComponent<PlayerInteractor>()?.ViewCamera;
            var weapons=owner.Weapons;
            bool firearm=handSafe && owner.FirearmAimIK && weapons!=null && weapons.UsesHandBoundVisual
                && weapons.Equipped?.Definition.Weapon?.kind==WeaponKind.Firearm && !weapons.IsReloading
                && weapons.View!=null && weapons.View.transform.parent==animator.GetBoneTransform(HumanBodyBones.RightHand)
                && animator.GetCurrentAnimatorStateInfo(handLayer).shortNameHash!=Animator.StringToHash("Pickup");
            handsWeight=handSafe?Mathf.MoveTowards(handsWeight,firearm?Mathf.Clamp01(aimWeight):0,Time.deltaTime*12):0;
            firearmAimBlend=Mathf.MoveTowards(firearmAimBlend,weapons!=null&&weapons.IsAiming?1:0,Time.deltaTime*8);
            bool ready=handSafe && camera!=null && owner.Weapons!=null && !owner.Weapons.IsAttacking && !owner.Weapons.IsRecovering
                && !owner.Weapons.IsReloading && owner.Weapons.Equipped?.Definition.Weapon?.kind==WeaponKind.Melee
                && animator.GetCurrentAnimatorStateInfo(handLayer).shortNameHash==Animator.StringToHash("AxeIdle") && !animator.IsInTransition(handLayer);
            // Releasing immediately is deliberate: no residual IK may fight an accepted Sword_Attack.
            meleeWeight=ready?Mathf.MoveTowards(meleeWeight,Mathf.Clamp01(meleeReadyWeight),Time.deltaTime*8):0;
            if(ready && meleeWeight>0)
            {
                var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                if(hand!=null)
                {
                    // Humanoid IK goal axes differ from this rig's hand bone axes.
                    Quaternion desired=Quaternion.LookRotation(camera.transform.up,camera.transform.right);
                    PlaceHand(AvatarIKGoal.RightHand,camera.transform.TransformPoint(meleeReadyPosition),desired*Quaternion.Inverse(rightBoneFromGoal),meleeWeight);
                }
            }
            if(!handSafe || camera==null || !firearm || handsWeight<=0)return;
            PoseFirearm(camera,weapons);
        }
        private void PoseFirearm(Camera camera,PlayerWeapons weapons)
        {
            // Weapon +X is its barrel axis. Solve its desired pose first, then invert the exact
            // same hand mount that PlayerWeapons uses after IK. The weapon never floats separately.
            Vector3 local=Vector3.Lerp(firearmHipPosition,firearmAimPosition,Mathf.SmoothStep(0,1,firearmAimBlend));
            // A side-held pistol forces the supporting forearm across the eye when pointing
            // almost vertically up. Centre the shared grip smoothly; gameplay ADS is unchanged.
            float upward=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,.95f,camera.transform.forward.y));
            local.x=Mathf.Lerp(local.x,0,upward);
            local.z-=weapons.VisualRecoil*.004f;
            Quaternion gunRotation=camera.transform.rotation*Quaternion.Euler(-weapons.VisualRecoil*1.5f,0,0)*Quaternion.Euler(0,-90,0);
            Vector3 gunPosition=camera.transform.TransformPoint(local);
            Quaternion handRotation=gunRotation*Quaternion.Inverse(weapons.FirearmHandRotation);
            Vector3 mountOffset=handRotation*weapons.FirearmHandPosition;
            Vector3 leftOffset=gunRotation*leftFirearmGrip;
            // Project the shared weapon pose into BOTH reachable arm volumes. Moving the shared
            // pose keeps two-hand contact; independent clamps used to separate the hands and grip.
            for(int i=0;i<5;i++)
            {
                Vector3 right=gunPosition-mountOffset;
                gunPosition+=Reachable(AvatarIKGoal.RightHand,right)-right;
                Vector3 left=gunPosition+leftOffset;
                gunPosition+=Reachable(AvatarIKGoal.LeftHand,left)-left;
            }
            Quaternion sourceRight=animator.GetIKRotation(AvatarIKGoal.RightHand);
            Quaternion sourceLeft=animator.GetIKRotation(AvatarIKGoal.LeftHand);
            Quaternion rightGoal=handRotation*Quaternion.Inverse(rightBoneFromGoal);
            Quaternion leftGoal=rightGoal*Quaternion.Inverse(sourceRight)*sourceLeft;
            AimHand(AvatarIKGoal.RightHand,gunPosition-mountOffset,rightGoal);
            AimHand(AvatarIKGoal.LeftHand,gunPosition+leftOffset,leftGoal);
            PoseElbow(AvatarIKHint.LeftElbow,HumanBodyBones.LeftUpperArm,-1);
            PoseElbow(AvatarIKHint.RightElbow,HumanBodyBones.RightUpperArm,1);
        }
        private void PoseElbow(AvatarIKHint hint,HumanBodyBones bone,float side)
        {
            var shoulder=animator.GetBoneTransform(bone);
            if(shoulder==null)return;
            // Keep the bending plane outside the torso, including near-vertical aim.
            // This is a hint only: the hand solver still respects the physical arm length.
            animator.SetIKHintPositionWeight(hint,handsWeight);
            animator.SetIKHintPosition(hint,shoulder.position+owner.transform.right*(side*firearmElbowHint.x)
                +owner.transform.up*firearmElbowHint.y+owner.transform.forward*firearmElbowHint.z);
        }
        private void CorrectFoot(AvatarIKGoal goal,Vector3 direction,float yaw)
        {
            Vector3 current=animator.GetIKPosition(goal);
            var hips=animator.GetBoneTransform(HumanBodyBones.Hips); if(hips==null)return;
            var leg=animator.GetBoneTransform(goal==AvatarIKGoal.LeftFoot?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
            // An imported avatar can claim to be valid while mapping Hips to the ground-level scene root.
            if(leg==null || Vector3.Dot(leg.position-hips.position,owner.transform.up)>.15f)return;
            float travel=Vector3.Dot(current-hips.position,owner.transform.forward);
            Vector3 correction=owner.transform.TransformDirection((direction-Vector3.forward)*travel);
            Vector3 target=current+Vector3.ClampMagnitude(correction,Mathf.Clamp(maximumFootCorrection,.05f,.5f));
            animator.SetIKPositionWeight(goal,feetWeight); animator.SetIKRotationWeight(goal,feetWeight);
            animator.SetIKPosition(goal,target);
            animator.SetIKRotation(goal,Quaternion.AngleAxis(Mathf.Clamp(yaw,-Mathf.Clamp(maximumFootYaw,0,45),Mathf.Clamp(maximumFootYaw,0,45)),Vector3.up)*animator.GetIKRotation(goal));
        }
        private void AimHand(AvatarIKGoal goal,Vector3 point,Quaternion rotation)
        {
            PlaceHand(goal,point,rotation,handsWeight);
        }
        private void PlaceHand(AvatarIKGoal goal,Vector3 point,Quaternion rotation,float weight)
        {
            point=Reachable(goal,point);
            animator.SetIKPositionWeight(goal,weight); animator.SetIKRotationWeight(goal,weight);
            animator.SetIKPosition(goal,point); animator.SetIKRotation(goal,rotation);
        }
        private Vector3 Reachable(AvatarIKGoal goal,Vector3 point)
        {
            var shoulder=animator.GetBoneTransform(goal==AvatarIKGoal.LeftHand?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            if(shoulder==null)return point;
            // A view pose is never allowed to stretch the upper limb indefinitely.
            var lower=animator.GetBoneTransform(goal==AvatarIKGoal.LeftHand?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=animator.GetBoneTransform(goal==AvatarIKGoal.LeftHand?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            if(lower==null || hand==null)return point;
            float reach=(Vector3.Distance(shoulder.position,lower.position)+Vector3.Distance(lower.position,hand.position))*.95f;
            return shoulder.position+Vector3.ClampMagnitude(point-shoulder.position,reach);
        }
    }
}
