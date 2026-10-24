using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Combat;
using ZombieSurvival.Interaction;
using ZombieSurvival.Inventory;
using ZombieSurvival.Weapons;

namespace ZombieSurvival.Player
{
    /// <summary>Presentation only. UAL controller parameters never drive gameplay or consume items.</summary>
    [DefaultExecutionOrder(250), DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController), typeof(PlayerVisual))]
    public sealed class PlayerUALAnimationDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Время сглаживания фактического движения, с.")] private float motionDamping = .1f;
        [SerializeField, Tooltip("Скорость исходного обычного шага UAL, м/с.")] private float walkClipSpeed = 1.4f;
        [SerializeField, Tooltip("Скорость Jog в смешивании обычного шага Walk→Jog по Speed, м/с. Должна совпадать с верхним порогом BlendTree.")] private float jogClipSpeed = 3f;
        [SerializeField, Tooltip("Скорость исходного осторожного шага UAL, м/с.")] private float slowClipSpeed = 1f;
        [SerializeField, Tooltip("Скорость исходного бега UAL, м/с.")] private float runClipSpeed = 4f;
        [SerializeField, Tooltip("Скорость исходного шага сидя UAL, м/с.")] private float crouchClipSpeed = 1f;
        [SerializeField, Tooltip("Длительность клипа удара до события попадания игры, с. ActionRate согласует её с WeaponDefinition.windup.")] private float meleeClipImpactTime = .25f;
        [SerializeField, Tooltip("Длительность клипа перезарядки до масштабирования, с.")] private float reloadClipSeconds = 1.7f;
        [SerializeField, Tooltip("Имя необязательного masked слоя рук контроллера UAL.")] private string upperBodyLayer = "Upper Body";
        [SerializeField, Tooltip("Приближённо перенаправлять шаги вперёд для бокового/обратного движения, без разворота тела.")] private bool directionalFootIK = true;
        [SerializeField, Tooltip("Прицеливание руками относительно камеры; действует только с огнестрельным оружием.")] private bool firearmAimIK = true;
        [SerializeField, InspectorName("Наклон корпуса при выглядывании"), Range(0, 40), Tooltip("Наклон верхней части тела при полном Q/E, градусы. Переносит плечи, обе руки и оружие; стопы и капсула остаются на месте. Стена уменьшает наклон вместе с фактическим смещением камеры.")]
        private float peekTorsoAngle = 32f;
        [Header("Руки и пистолет — положение относительно камеры")]
        [SerializeField, Tooltip("Без ПКМ, метры: X вправо, Y вверх, Z вперёд от камеры. Цель ограничена длиной рук.")]
        private Vector3 firearmHipPosition = new Vector3(.11f,-.16f,.4f);
        [SerializeField, Tooltip("Прицеливание с ПКМ, метры: X вправо, Y вверх, Z вперёд от камеры.")]
        private Vector3 firearmAimPosition = new Vector3(0,-.04f,.44f);
        [SerializeField, Tooltip("Точка левой кисти в локальных осях модели пистолета, метры. У текущего пистолета +X вдоль ствола.")]
        private Vector3 leftFirearmGrip = new Vector3(-.12f,-.055f,.035f);
        [SerializeField, Range(0,1), Tooltip("Сила коррекции рук: 1 — полный хват, 0 — исходная анимация.")]
        private float aimWeight = 1f;
        [SerializeField, Tooltip("Сгиб локтей относительно плеча и корпуса, метры: X наружу, Y вверх, Z вперёд.")]
        private Vector3 firearmElbowHint = new Vector3(.6f,.2f,.2f);
        [Header("Рука с оружием ближнего боя — готовность")]
        [SerializeField, Tooltip("Правая кисть относительно камеры, метры. Коррекция освобождается на время удара.")]
        private Vector3 meleeReadyPosition = new Vector3(.18f,-.2f,.25f);
        [SerializeField, Range(0,1)] private float meleeReadyWeight = 1f;
        private FirstPersonController motor;
        private CharacterController capsule;
        private PlayerVisual visual;
        private PlayerWeapons weapons;
        private PlayerInteractor interactor;
        private Health health;
        private Animator animator;
        private RuntimeAnimatorController controller;
        private Animator bodyAnimator;
        private RuntimeAnimatorController originalBodyController, appliedBodyController;
        private PlayerUALAnimatorIK bridge;
        private readonly Dictionary<string, AnimatorControllerParameterType> parameters = new Dictionary<string, AnimatorControllerParameterType>();
        private bool wasBlocked;
        private bool upperSuspended;
        private int upperLayer = -1;
        private float actionRate = 1;
        public FirstPersonController Motor => motor;
        public PlayerWeapons Weapons => weapons;
        public Vector3 LocalVelocity { get; private set; }
        public bool DirectionalFootIK => directionalFootIK;
        public bool FirearmAimIK => firearmAimIK;
        internal float PeekTorsoAngle => Mathf.Clamp(peekTorsoAngle, 0f, 40f);
        public int UpperBodyLayerIndex => upperLayer;

        // Read the root's serialized values on every IK pass, including edits made during Play.
        // The Animator bridge is transient and is never the authoring source for hand poses.
        internal void ApplyHandSettings(PlayerUALAnimatorIK target)
        {
            target.SetHandSettings(firearmHipPosition, firearmAimPosition, leftFirearmGrip,
                aimWeight, firearmElbowHint, meleeReadyPosition, meleeReadyWeight);
        }

        private void OnEnable() => Initialize();
        private void Initialize()
        {
            Unsubscribe(); motor=GetComponent<FirstPersonController>(); capsule=GetComponent<CharacterController>();
            visual=GetComponent<PlayerVisual>(); weapons=GetComponent<PlayerWeapons>(); interactor=GetComponent<PlayerInteractor>(); health=GetComponent<Health>();
            motor.Jumped+=Jump; motor.Landed+=Land;
            if(weapons!=null) { weapons.ShotFired+=Fire; weapons.SwingStarted+=Melee; weapons.ReloadStarted+=Reload; weapons.WeaponEquipped+=Equip; }
            if(interactor!=null)interactor.SuccessfulInteraction+=Pickup;
            if(health!=null) { health.WorldStateRestored+=ResetPresentation; health.Revived+=ResetPresentation; health.Died+=ResetPresentation; }
            controller=null; ResolveAnimator(); ResetPresentation();
        }
        private void Unsubscribe()
        {
            if(motor!=null) { motor.Jumped-=Jump; motor.Landed-=Land; }
            if(weapons!=null) { weapons.ShotFired-=Fire; weapons.SwingStarted-=Melee; weapons.ReloadStarted-=Reload; weapons.WeaponEquipped-=Equip; }
            if(interactor!=null)interactor.SuccessfulInteraction-=Pickup;
            if(health!=null) { health.WorldStateRestored-=ResetPresentation; health.Revived-=ResetPresentation; health.Died-=ResetPresentation; }
        }
        private void OnDisable() { Unsubscribe(); ClearTriggers(); if(bridge!=null)bridge.Configure(null); if(animator!=null && upperLayer>=0)animator.SetLayerWeight(upperLayer,0); RestoreBodyController(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetForPlay()
        {
            foreach(var driver in Object.FindObjectsByType<PlayerUALAnimationDriver>(FindObjectsInactive.Include))
                if(driver.isActiveAndEnabled)driver.Initialize();
        }
        private void ResolveAnimator()
        {
            var current=visual!=null?visual.ModelAnimator:null;
            ResolveBodyController(current);
            if(current==animator && (current==null || controller==current.runtimeAnimatorController))return;
            if(bridge!=null)bridge.Configure(null);
            animator=current; controller=animator!=null?animator.runtimeAnimatorController:null; parameters.Clear(); upperLayer=-1;
            if(animator==null || controller==null)return;
            animator.applyRootMotion=false;
            foreach(var p in animator.parameters)parameters[p.name]=p.type;
            upperLayer=animator.GetLayerIndex(upperBodyLayer);
            bridge=animator.GetComponent<PlayerUALAnimatorIK>();
            if(bridge==null)bridge=animator.gameObject.AddComponent<PlayerUALAnimatorIK>();
            bridge.Configure(this); ClearTriggers();
        }
        private void RestoreBodyController()
        {
            if(bodyAnimator!=null && appliedBodyController!=null && bodyAnimator.runtimeAnimatorController==appliedBodyController)
                bodyAnimator.runtimeAnimatorController=originalBodyController;
            bodyAnimator=null; originalBodyController=appliedBodyController=null;
        }
        private void ResolveBodyController(Animator current)
        {
            if(current!=bodyAnimator)
            {
                RestoreBodyController(); bodyAnimator=current;
                originalBodyController=current!=null?current.runtimeAnimatorController:null;
            }
            if(current==null)return;
            // Не перетираем внешний SetModel/controller и возвращаем прежний controller при смене оружия.
            if(current.runtimeAnimatorController!=(appliedBodyController??originalBodyController))
            { originalBodyController=current.runtimeAnimatorController; appliedBodyController=null; }
            var overrides=weapons!=null?weapons.Equipped?.Definition.Weapon?.bodyAnimations:null;
            var desired=overrides!=null && overrides.runtimeAnimatorController==originalBodyController ? overrides : originalBodyController;
            if(current.runtimeAnimatorController!=desired)current.runtimeAnimatorController=desired;
            appliedBodyController=desired!=originalBodyController?desired:null;
        }
        private bool Has(string name, AnimatorControllerParameterType type) => animator!=null && parameters.TryGetValue(name,out var actual) && actual==type;
        private void Float(string name,float value,bool damp=false) { if(Has(name,AnimatorControllerParameterType.Float)) { if(damp)animator.SetFloat(name,value,Mathf.Max(0,motionDamping),Time.deltaTime); else animator.SetFloat(name,value); } }
        private void Bool(string name,bool value) { if(Has(name,AnimatorControllerParameterType.Bool))animator.SetBool(name,value); }
        private void Int(string name,int value) { if(Has(name,AnimatorControllerParameterType.Int))animator.SetInteger(name,value); }
        private void Trigger(string name) { ResolveAnimator(); if(isActiveAndEnabled && motor!=null && motor.IsAlive && Has(name,AnimatorControllerParameterType.Trigger)) { animator.ResetTrigger(name); animator.SetTrigger(name); } }
        private void ClearTriggers() { foreach(var p in parameters)if(p.Value==AnimatorControllerParameterType.Trigger && animator!=null)animator.ResetTrigger(p.Key); }
        private void ResetUpperBody()
        {
            foreach(string name in new[] { "Fire", "Melee", "Reload", "Equip", "Pickup" })
                if(Has(name,AnimatorControllerParameterType.Trigger))animator.ResetTrigger(name);
            if(animator!=null && upperLayer>=0)
            {
                int empty=Animator.StringToHash(upperBodyLayer+".Empty");
                if(animator.HasState(upperLayer,empty))animator.Play(empty,upperLayer,0);
                animator.SetLayerWeight(upperLayer,0);
            }
            actionRate=1;
        }
        private void ResetPresentation()
        {
            ResolveAnimator(); ClearTriggers(); LocalVelocity=Vector3.zero; actionRate=1; wasBlocked=true; upperSuspended=false;
            if(animator!=null && controller!=null) { animator.Rebind(); animator.applyRootMotion=false; }
            if(bridge!=null)bridge.Configure(this);
        }
        private void Jump() => Trigger("Jump");
        private void Land(float speed) => Trigger("Land");
        private void Fire(WeaponDefinition data) { actionRate=1; Trigger("Fire"); }
        private void Melee(WeaponDefinition data) { actionRate=Mathf.Clamp(meleeClipImpactTime/Mathf.Max(.01f,data.windup),.1f,8); Trigger("Melee"); }
        private void Reload(WeaponDefinition data) { ResolveAnimator(); actionRate=Mathf.Clamp((data.bodyAnimations!=null && animator!=null && animator.runtimeAnimatorController==data.bodyAnimations?data.bodyReloadSeconds:reloadClipSeconds)/Mathf.Max(.01f,data.reloadSeconds),.1f,8); Trigger("Reload"); }
        private void Equip(WeaponDefinition data) { ResolveAnimator(); ResetUpperBody(); Trigger("Equip"); }
        private void Pickup()
        {
            if(weapons!=null && (weapons.IsAttacking || weapons.IsReloading || weapons.IsRecovering))return;
            if(interactor!=null && interactor.Target is WorldItem) { actionRate=1; Trigger("Pickup"); }
        }
        private void Update()
        {
            bridge?.RestorePeekPose();
            ResolveAnimator(); if(animator==null || controller==null || motor==null)return;
            var parkour=motor.Parkour; bool traversal=parkour!=null && parkour.OwnsMovement;
            // Cursor/focus only stops gameplay; owner/base locks also suspend the presentation.
            bool blocked=!motor.GameplayInputEnabled || !motor.isActiveAndEnabled;
            if(blocked && !wasBlocked)ClearTriggers(); wasBlocked=blocked;
            bool suspended=blocked || traversal || motor.IsProne || motor.IsSprinting || !motor.IsAlive;
            if(suspended && !upperSuspended)ResetUpperBody();
            upperSuspended=suspended;
            Vector3 velocity=capsule!=null && capsule.enabled?capsule.velocity:Vector3.zero;
            LocalVelocity=transform.InverseTransformDirection(Vector3.ProjectOnPlane(velocity,Vector3.up));
            float speed=LocalVelocity.magnitude;
            Float("MoveX",LocalVelocity.x,true); Float("MoveZ",LocalVelocity.z,true); Float("Speed",speed,true);
            Float("VerticalSpeed",motor.VerticalSpeed); Float("ParkourProgress",parkour!=null?parkour.ActionProgress:0);
            Bool("SlowWalking",motor.IsSlowWalking); Bool("Sprinting",motor.IsSprinting); Bool("Crouched",motor.IsCrouching);
            Bool("Prone",motor.IsProne); Bool("Grounded",motor.IsGrounded); Bool("Dead",!motor.IsAlive); Bool("InputBlocked",blocked);
            Bool("Aiming",weapons!=null && weapons.IsAiming); Bool("Reloading",weapons!=null && weapons.IsReloading);
            var item=weapons!=null?weapons.Equipped:null; var weapon=item!=null?item.Definition.Weapon:null;
            Int("WeaponKind",weapon==null?0:weapon.kind==WeaponKind.Firearm?1:2);
            Int("ParkourState",parkour!=null?(int)parkour.State:0);
            float walkingReference=Mathf.Lerp(walkClipSpeed,jogClipSpeed,Mathf.InverseLerp(walkClipSpeed,Mathf.Max(walkClipSpeed+.01f,jogClipSpeed),speed));
            float reference=motor.IsCrouching?crouchClipSpeed:motor.IsSprinting?runClipSpeed:motor.IsSlowWalking?slowClipSpeed:walkingReference;
            Float("LocomotionRate",speed>.05f?Mathf.Clamp(speed/Mathf.Max(.1f,reference),.2f,3):1); Float("ActionRate",actionRate);
            float pitch=0; if(interactor!=null && interactor.ViewCamera!=null)pitch=-Mathf.Asin(Mathf.Clamp(interactor.ViewCamera.transform.forward.y,-1,1))*Mathf.Rad2Deg/60f;
            Float("Pitch",Mathf.Clamp(pitch,-1,1),true);
            if(upperLayer>=0)
            {
                bool pickup=animator.GetCurrentAnimatorStateInfo(upperLayer).shortNameHash==Animator.StringToHash("Pickup")
                    || animator.IsInTransition(upperLayer) && animator.GetNextAnimatorStateInfo(upperLayer).shortNameHash==Animator.StringToHash("Pickup");
                bool allow=!suspended && (weapon!=null || pickup);
                animator.SetLayerWeight(upperLayer,Mathf.MoveTowards(animator.GetLayerWeight(upperLayer),allow?1:0,Time.deltaTime*12));
            }
        }
    }
}
