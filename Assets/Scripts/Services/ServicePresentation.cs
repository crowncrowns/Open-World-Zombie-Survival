using System;
using UnityEngine;

namespace ZombieSurvival.Services
{
    /// <summary>Optional, local presentation. Never changes service economics, root pose or save identity.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(150)]
    public sealed class ServicePresentation : MonoBehaviour
    {
        [SerializeField,InspectorName("Оформление"),Tooltip("Профиль idle и звуков. None использует Resources/ServicePresentation/Default; отсутствие профиля отключает эффекты, но не сервис.")]
        private ServicePresentationProfile profileOverride;
        [SerializeField,InspectorName("Ожидание NPC"),Tooltip("Подключать готовую Humanoid idle-анимацию торговцу без собственного контроллера. Корень сервиса не перемещается; root motion выключен.")]
        private bool idleEnabled=true;
        [SerializeField,InspectorName("Смотреть на собеседника"),Tooltip("Плавно поворачивать голову, либо грудь при отсутствии головы, к живому игроку в сессии и рядом с торговцем. Только Humanoid; физический корень и посадка модели не меняются.")]
        private bool lookEnabled=true;
        [SerializeField,InspectorName("Замечать игрока рядом"),Tooltip("Торговец смотрит на ближайшего живого игрока перед собой и вне меню. Не применяется к предметному верстаку.")]
        private bool ambientLookEnabled=true;
        [SerializeField,Min(.1f),InspectorName("Радиус взгляда вне меню, м"),Tooltip("Расстояние от головы NPC до камеры игрока для обычного взгляда. Сессия имеет приоритет.")]
        private float ambientLookRadius=4f;
        [SerializeField,Range(1,160),InspectorName("Сектор взгляда вне меню, °"),Tooltip("Полный сектор перед моделью: 120° означает по 60° влево и вправо. За спиной NPC возвращается к idle.")]
        private float ambientFieldOfView=120f;
        [SerializeField,Range(.15f,2f),InspectorName("Интервал поиска игрока, с"),Tooltip("Редкий поиск ближайшего игрока. Жизнь, расстояние и прямая видимость выбранного игрока проверяются между поисками.")]
        private float ambientSearchInterval=.3f;
        [SerializeField,InspectorName("Препятствия для взгляда"),Tooltip("Слои стен и объектов, перекрывающих взгляд. Собственные коллайдеры и коллайдеры выбранного игрока исключаются; триггеры игнорируются.")]
        private LayerMask lookObstructionMask=~0;
        [SerializeField,Range(0,60),InspectorName("Поворот в стороны, °"),Tooltip("Максимальный дополнительный угол головы от направления модели, градусы.")]
        private float maximumYaw=35;
        [SerializeField,Range(0,30),InspectorName("Наклон головы, °"),Tooltip("Максимальный дополнительный наклон головы к собеседнику, градусы.")]
        private float maximumPitch=15;
        [SerializeField,Min(1),InspectorName("Скорость поворота, °/с"),Tooltip("Скорость плавного наведения и возвращения головы, градусы в секунду.")]
        private float lookSpeed=80;
        [SerializeField,InspectorName("Звуки операций"),Tooltip("Звуки только результата явных действий интерфейса; не добавляют GameplayNoise и не влияют на экономику.")]
        private bool soundsEnabled=true;
        private ServiceVisual visual;
        private Animator animator;
        private Avatar boundAvatar;
        private ZombieSurvival.Combat.Health health;
        private ServicePresentationProfile defaultProfile;
        private Transform model,bone,lookTarget;
        private ZombieSurvival.Player.FirstPersonController ambientPlayer;
        private Camera ambientCamera;
        private float nextAmbientSearch;
        private readonly RaycastHit[] lookHits=new RaycastHit[48];
        private RuntimeAnimatorController assignedController;
        private bool originalRootMotion;
        private Quaternion originalBone,lastBone;
        private bool boneModified;
        private float yaw,pitch;
        private AudioSource feedbackSource;
        private float lastSoundTime=float.NegativeInfinity;
        public event Action<bool,AudioClip> FeedbackPlayed;
        public ServicePresentationProfile Profile
        {
            get
            {
                if(profileOverride!=null)return profileOverride;
                if(defaultProfile==null)defaultProfile=Resources.Load<ServicePresentationProfile>("ServicePresentation/Default");
                return defaultProfile;
            }
        }

        private void OnEnable()
        {
            health=GetComponentInParent<ZombieSurvival.Combat.Health>();
            if(health!=null){health.Died+=StopPresentation;health.WorldStateRestored+=StopPresentation;}
        }

        public void BeginSession(Transform target) { if(isActiveAndEnabled)lookTarget=target; }
        public void EndSession() { lookTarget=null; }
        private void Update()
        {
            RestoreBone();
            ResolveModel();
            UpdateAmbientTarget();
        }
        private void ResolveModel()
        {
            if(health!=null&&(health.IsDead||!health.isActiveAndEnabled)){StopPresentation();return;}
            if(visual==null)visual=GetComponent<ServiceVisual>();
            var nextModel=visual!=null?(visual.Instance!=null?visual.Instance.transform:null):transform;
            var nextAnimator=nextModel!=null?nextModel.GetComponentInChildren<Animator>():null;
            if(nextAnimator!=animator||nextModel!=model||(nextAnimator!=null&&nextAnimator.avatar!=boundAvatar))
            {
                ReleaseAnimator();animator=nextAnimator;model=nextModel;boundAvatar=animator!=null?animator.avatar:null;
                if(animator!=null&&animator.isHuman&&animator.avatar!=null&&animator.avatar.isValid)
                    bone=animator.GetBoneTransform(HumanBodyBones.Head)??animator.GetBoneTransform(HumanBodyBones.Chest);
            }
            // An external controller replacement transfers ownership, including its current root-motion choice.
            // Never clear that controller or restore our older root-motion value onto it.
            if(assignedController!=null&&animator!=null&&animator.runtimeAnimatorController!=assignedController)
                assignedController=null;
            var controller=Profile!=null?Profile.idleController:null;
            if(assignedController!=null&&(!idleEnabled||controller!=assignedController||GetComponent<Trader>()==null))ReleaseOwnedController();
            if(animator==null||!animator.isActiveAndEnabled||!animator.isHuman||!idleEnabled||GetComponent<Trader>()==null)return;
            if(animator.runtimeAnimatorController==null&&controller!=null)
            {
                originalRootMotion=animator.applyRootMotion;animator.applyRootMotion=false;
                assignedController=controller;animator.runtimeAnimatorController=controller;
            }
        }
        private void LateUpdate()
        {
            if(bone==null||model==null||animator==null||!animator.isActiveAndEnabled)return;
            float nextYaw=0,nextPitch=0;
            Transform target=null;
            if(lookEnabled&&lookTarget!=null)
            {
                var player=lookTarget.GetComponentInParent<ZombieSurvival.Player.FirstPersonController>();
                if(ValidPlayer(player,out var camera)&&ClearLook(player,camera))target=camera.transform;
            }
            else if(lookEnabled&&AmbientAllowed&&ValidAmbient())target=ambientCamera.transform;
            if(target!=null)
            {
                float sector=lookTarget!=null?90f:Mathf.Clamp(Finite(ambientFieldOfView,120),1,160)*.5f;
                TryLookAngles(target.position,sector,out nextYaw,out nextPitch);
            }
            float yawLimit=Mathf.Clamp(Finite(maximumYaw,35),0,60),pitchLimit=Mathf.Clamp(Finite(maximumPitch,15),0,30);
            float step=Mathf.Clamp(Finite(lookSpeed,80),1,180)*Mathf.Max(0,Time.deltaTime);
            yaw=Mathf.Clamp(Mathf.MoveTowards(yaw,nextYaw,step),-yawLimit,yawLimit);pitch=Mathf.Clamp(Mathf.MoveTowards(pitch,nextPitch,step),-pitchLimit,pitchLimit);
            if(Mathf.Abs(yaw)+Mathf.Abs(pitch)<.001f)return;
            originalBone=bone.localRotation;
            Quaternion turn=Quaternion.AngleAxis(yaw,transform.up)*Quaternion.AngleAxis(pitch,model.right);
            bone.rotation=turn*bone.rotation;lastBone=bone.localRotation;boneModified=true;
        }
        private bool AmbientAllowed=>ambientLookEnabled&&lookEnabled&&GetComponent<Trader>() is Trader trader&&trader.isActiveAndEnabled
            &&bone!=null&&model!=null&&animator!=null&&animator.isActiveAndEnabled&&(health==null||health.isActiveAndEnabled&&!health.IsDead);
        private static float Finite(float value,float fallback)=>float.IsNaN(value)||float.IsInfinity(value)?fallback:value;
        private bool ValidPlayer(ZombieSurvival.Player.FirstPersonController player,out Camera camera)
        {
            camera=null;if(player==null||!player.isActiveAndEnabled||!player.IsAlive||player.gameObject.scene!=gameObject.scene)return false;
            var playerHealth=player.GetComponent<ZombieSurvival.Combat.Health>();if(playerHealth!=null&&(!playerHealth.isActiveAndEnabled||playerHealth.IsDead))return false;
            var interactor=player.GetComponent<ZombieSurvival.Interaction.PlayerInteractor>();camera=interactor!=null?interactor.ViewCamera:null;
            return camera!=null&&camera.isActiveAndEnabled&&camera.transform.IsChildOf(player.transform)&&camera.gameObject.scene==gameObject.scene;
        }
        private bool TryLookAngles(Vector3 position,float frontalLimit,out float targetYaw,out float targetPitch)
        {
            targetYaw=targetPitch=0;if(bone==null||model==null)return false;
            var delta=position-bone.position;var flat=Vector3.ProjectOnPlane(delta,transform.up);var forward=Vector3.ProjectOnPlane(model.forward,transform.up);
            if(flat.sqrMagnitude<.0001f||forward.sqrMagnitude<.0001f)return false;
            float rawYaw=Vector3.SignedAngle(forward,flat,transform.up);
            if(Mathf.Abs(rawYaw)>frontalLimit)return false;
            targetYaw=Mathf.Clamp(rawYaw,-Mathf.Clamp(Finite(maximumYaw,35),0,60),Mathf.Clamp(Finite(maximumYaw,35),0,60));
            float limit=Mathf.Clamp(Finite(maximumPitch,15),0,30);
            targetPitch=Mathf.Clamp(-Mathf.Atan2(Vector3.Dot(delta,transform.up),flat.magnitude)*Mathf.Rad2Deg,-limit,limit);return true;
        }
        private bool ClearLook(ZombieSurvival.Player.FirstPersonController player,Camera camera)
        {
            if(bone==null)return false;var delta=camera.transform.position-bone.position;float distance=delta.magnitude;if(distance<.001f)return true;
            int count=Physics.RaycastNonAlloc(bone.position,delta/distance,lookHits,distance,lookObstructionMask,QueryTriggerInteraction.Ignore);
            if(count==lookHits.Length)return false;
            for(int i=0;i<count;i++)if(!lookHits[i].collider.transform.IsChildOf(transform)&&!lookHits[i].collider.transform.IsChildOf(player.transform))return false;
            return true;
        }
        private bool ValidAmbient()
        {
            if(!ValidPlayer(ambientPlayer,out var camera)||camera!=ambientCamera)return false;
            float radius=Mathf.Clamp(Finite(ambientLookRadius,4),.1f,50);
            return (camera.transform.position-bone.position).sqrMagnitude<=radius*radius
                &&TryLookAngles(camera.transform.position,Mathf.Clamp(Finite(ambientFieldOfView,120),1,160)*.5f,out _,out _)&&ClearLook(ambientPlayer,camera);
        }
        private void UpdateAmbientTarget()
        {
            if(!AmbientAllowed||lookTarget!=null){ambientPlayer=null;ambientCamera=null;nextAmbientSearch=0;return;}
            if(!ValidAmbient()){ambientPlayer=null;ambientCamera=null;}
            if(Time.unscaledTime<nextAmbientSearch)return;
            nextAmbientSearch=Time.unscaledTime+Mathf.Clamp(Finite(ambientSearchInterval,.3f),.15f,2f);
            ambientPlayer=null;ambientCamera=null;float best=Mathf.Pow(Mathf.Clamp(Finite(ambientLookRadius,4),.1f,50),2);
            foreach(var player in FindObjectsByType<ZombieSurvival.Player.FirstPersonController>(FindObjectsInactive.Exclude))
            {
                if(!ValidPlayer(player,out var camera))continue;float distance=(camera.transform.position-bone.position).sqrMagnitude;
                if(distance>best||!TryLookAngles(camera.transform.position,Mathf.Clamp(Finite(ambientFieldOfView,120),1,160)*.5f,out _,out _)||!ClearLook(player,camera))continue;
                best=distance;ambientPlayer=player;ambientCamera=camera;
            }
        }
        private void RestoreBone()
        {
            if(boneModified&&bone!=null&&Quaternion.Angle(bone.localRotation,lastBone)<.01f)bone.localRotation=originalBone;
            boneModified=false;
        }
        private void ReleaseOwnedController()
        {
            if(animator!=null&&assignedController!=null&&animator.runtimeAnimatorController==assignedController)
            {animator.runtimeAnimatorController=null;animator.applyRootMotion=originalRootMotion;}
            assignedController=null;
        }
        private void ReleaseAnimator()
        {RestoreBone();ReleaseOwnedController();animator=null;boundAvatar=null;model=bone=null;ambientPlayer=null;ambientCamera=null;nextAmbientSearch=0;yaw=pitch=0;}
        public void PlayResult(bool success,bool repair)
        {
            if(!isActiveAndEnabled||!soundsEnabled||health!=null&&health.IsDead||Time.unscaledTime-lastSoundTime<.12f)return;
            var profile=Profile;if(profile==null)return;
            var clip=success?(repair?profile.repairSuccess:profile.tradeSuccess):profile.rejected;
            if(clip==null||profile.volume<=0)return;
            if(feedbackSource==null)
            {
                var child=new GameObject("Service feedback audio");child.transform.SetParent(transform,false);
                feedbackSource=child.AddComponent<AudioSource>();feedbackSource.playOnAwake=false;feedbackSource.loop=false;
                feedbackSource.spatialBlend=0;feedbackSource.dopplerLevel=0;
            }
            feedbackSource.outputAudioMixerGroup=profile.mixerGroup;
            feedbackSource.PlayOneShot(clip,Mathf.Clamp01(profile.volume));lastSoundTime=Time.unscaledTime;
            var listeners=FeedbackPlayed;
            if(listeners!=null)foreach(Action<bool,AudioClip> listener in listeners.GetInvocationList())
                try{listener(success,clip);}catch(Exception error){Debug.LogException(error,this);}
        }
        private void StopPresentation()
        {lookTarget=null;ambientPlayer=null;ambientCamera=null;nextAmbientSearch=0;ReleaseAnimator();if(feedbackSource!=null)feedbackSource.Stop();lastSoundTime=float.NegativeInfinity;}
        private void OnDisable()
        {
            if(health!=null){health.Died-=StopPresentation;health.WorldStateRestored-=StopPresentation;}
            health=null;StopPresentation();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetForPlay()
        {
            // No Domain/Scene Reload: reconnect health once and discard transient look/audio state.
            foreach(var presentation in FindObjectsByType<ServicePresentation>(FindObjectsInactive.Include))
                if(presentation.isActiveAndEnabled){presentation.OnDisable();presentation.OnEnable();}
        }
    }
}
