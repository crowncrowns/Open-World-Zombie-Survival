using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Combat;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;
using ZombieSurvival.AI;

namespace ZombieSurvival.Weapons
{
    /// <summary>Только управление боем. Хранилище владеет предметами и патронами;
    /// WeaponHitQuery применяет общие правила попаданий. Замена модели игрока независима.</summary>
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent, RequireComponent(typeof(FirstPersonController),typeof(ItemStorage))]
    public sealed class PlayerWeapons : MonoBehaviour
    {
        [Tooltip("Камера взгляда. Если ссылка потеряна, берётся камера дочернего объекта.")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("Слои попадания и стен. Включайте геометрию мира и коллайдеры актёров.")]
        [SerializeField] private LayerMask hitMask = ~0;
        [Tooltip("Включить прицел, название оружия и боезапас справа снизу.")]
        [SerializeField] private bool showHud = true;
        [Tooltip("Показывать подсказку клавиш оружия справа сверху.")]
        [SerializeField] private bool showControls = true;
        [Tooltip("Скорость перехода оружия в положение прицеливания, 1/с.")]
        [SerializeField, Min(1)] private float aimSpeed = 12;
        [Tooltip("Продолжительность отметки попадания на HUD, с.")]
        [SerializeField, Min(.05f)] private float hitMarkerSeconds = .2f;
        private FirstPersonController motor;
        private ItemStorage storage;
        private PlayerNoiseEmitter noise;
        private InputAction attack, reload, aim, previous, next;
        private ItemStack equipped, pending;
        private GameObject view;
        private AudioSource audioSource;
        private float swingStarted = -100, attackAt, reloadAt, readyAt, recoilAngle, hitUntil, aimWeight;
        private bool armed;
        public ItemStack Equipped => equipped;
        public bool IsReloading => reloadAt > 0;
        public bool IsAttacking => pending != null;
        public bool IsAiming { get; private set; }
        public string Status { get; private set; } = "";
        private bool CanAct => motor != null && motor.AcceptingGameplayInput && viewCamera != null
            && (motor.Parkour == null || !motor.Parkour.OwnsMovement) && !motor.IsSprinting;
        private void OnEnable() => Initialize();
        private void Initialize()
        {
            motor=GetComponent<FirstPersonController>(); storage=GetComponent<ItemStorage>(); noise=GetComponent<PlayerNoiseEmitter>();
            if(viewCamera==null) viewCamera=GetComponentInChildren<Camera>(true);
            if(storage!=null) { storage.Changed-=OnStorageChanged; storage.Changed+=OnStorageChanged; }
            if(audioSource==null)
            {
                var child=transform.Find("WeaponAudio");
                if(child==null) { var go=new GameObject("WeaponAudio"); go.transform.SetParent(transform,false); child=go.transform; }
                audioSource=child.GetComponent<AudioSource>(); if(audioSource==null) audioSource=child.gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake=false; audioSource.spatialBlend=1; audioSource.minDistance=2; audioSource.maxDistance=55;
            }
            attack=reload=aim=previous=next=null; equipped=null; Cancel(); armed=false; readyAt=recoilAngle=aimWeight=hitUntil=0;
            if(view!=null) Destroy(view); view=null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreForPlay() { foreach(var w in Object.FindObjectsByType<PlayerWeapons>(FindObjectsInactive.Include)) if(w.isActiveAndEnabled) w.Initialize(); }
        private void OnDisable() { if(storage!=null) storage.Changed-=OnStorageChanged; Cancel(); if(view!=null) Destroy(view); view=null; equipped=null; }
        private void OnStorageChanged() { if(equipped!=null && !storage.Contains(equipped)) Unequip(); }
        private void Cancel() { pending=null; swingStarted=-100; attackAt=reloadAt=0; IsAiming=false; if(audioSource!=null) audioSource.Stop(); }
        public void Unequip() { Cancel(); equipped=null; armed=false; if(view!=null) Destroy(view); view=null; }
        public bool Equip(int slot)
        {
            var item=storage.GetSlot(slot);
            if(!motor.IsAlive || item==null || item.IsEmpty || item.Definition.Weapon==null) return false;
            Cancel(); if(view!=null) Destroy(view); view=null; equipped=item; readyAt=Time.time+.2f; armed=false; recoilAngle=0;
            var data=item.Definition.Weapon;
            if(data.viewPrefab!=null && viewCamera!=null)
            {
                view=Instantiate(data.viewPrefab,viewCamera.transform,false); view.name="HeldWeapon";
                foreach(var c in view.GetComponentsInChildren<Collider>(true)) c.enabled=false;
                foreach(var body in view.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic=true; body.detectCollisions=false; }
                foreach(var source in view.GetComponentsInChildren<AudioSource>(true)) source.enabled=false;
                foreach(var renderer in view.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                view.transform.localScale=data.viewScale;
            }
            Status=""; return true;
        }
        private void Cycle(int direction)
        {
            int current=-1; for(int i=0;i<storage.SlotCount;i++) if(storage.GetSlot(i)==equipped) current=i;
            for(int offset=1;offset<=storage.SlotCount;offset++)
            { int i=(current+direction*offset+storage.SlotCount*2)%storage.SlotCount; var s=storage.GetSlot(i); if(s!=null && s.Definition!=null && s.Definition.Weapon!=null) { Equip(i); return; } }
        }
        private void Update()
        {
            var current=motor.FindGameplayAction("Attack");
            if(current!=attack) { Cancel(); armed=false; attack=current; reload=motor.FindGameplayAction("Reload"); aim=motor.FindGameplayAction("Aim"); previous=motor.FindGameplayAction("Previous"); next=motor.FindGameplayAction("Next"); }
            if(!CanAct) { Cancel(); armed=false; if(view!=null) view.SetActive(false); return; }
            if(previous!=null && previous.WasPressedThisFrame()) Cycle(-1);
            if(next!=null && next.WasPressedThisFrame()) Cycle(1);
            if(equipped==null) { if(attack==null || !attack.IsPressed()) armed=true; return; }
            var data=equipped.Definition.Weapon;
            recoilAngle=Mathf.MoveTowards(recoilAngle,0,data.recoilRecovery*Time.deltaTime);
            IsAiming=data.kind==WeaponKind.Firearm && aim!=null && aim.IsPressed() && !IsReloading;
            if(!armed) { if(attack==null || !attack.IsPressed()) armed=true; }
            if(reload!=null && reload.WasPressedThisFrame()) TryReload();
            if(IsReloading && Time.time>=reloadAt) { reloadAt=0; storage.Reload(equipped); Status=""; }
            if(pending!=null && Time.time>=attackAt)
            {
                var strike=pending; pending=null;
                if(strike==equipped && storage.Contains(strike)) MeleeImpact(data);
            }
            if(armed && attack!=null && (data.automatic && data.kind==WeaponKind.Firearm ? attack.IsPressed() : attack.WasPressedThisFrame())) TryAttack();
        }
        public bool TryReload()
        {
            if(!CanAct || equipped==null || IsReloading || pending!=null || Time.time<readyAt) return false;
            var data=equipped.Definition.Weapon;
            if(data.kind!=WeaponKind.Firearm || data.Ammo==null || equipped.LoadedRounds>=data.MagazineSize || storage.Count(data.Ammo)==0) return false;
            reloadAt=Time.time+data.reloadSeconds; IsAiming=false; Play(data.reloadSound,data); Status="Перезарядка…"; return true;
        }
        public bool TryAttack()
        {
            if(!CanAct || equipped==null || !storage.Contains(equipped) || IsReloading || pending!=null || Time.time<readyAt) return false;
            var data=equipped.Definition.Weapon;
            if(data.kind==WeaponKind.Melee)
            {
                if(motor.Stamina!=null && !motor.Stamina.TrySpend(data.staminaCost)) { Status="Мало выносливости"; return false; }
                pending=equipped; swingStarted=Time.time; attackAt=Time.time+Mathf.Clamp(data.windup,0,data.cooldown);
            }
            else if(!storage.SpendRound(equipped)) { readyAt=Time.time+.2f; Status="Магазин пуст — R"; return false; }
            readyAt=Time.time+data.cooldown; Status="";
            if(data.attacks!=null && data.attacks.Length>0) Play(data.attacks[Random.Range(0,data.attacks.Length)],data);
            if(noise!=null) noise.Emit(data.noise,data.kind==WeaponKind.Firearm ? NoiseKind.Gunshot : NoiseKind.Melee);
            else GameplayNoise.Emit(transform.position,data.noise.x,data.noise.y,gameObject,data.kind==WeaponKind.Firearm ? NoiseKind.Gunshot : NoiseKind.Melee);
            if(data.kind==WeaponKind.Firearm) { Fire(data); recoilAngle=Mathf.Min(15,recoilAngle+data.recoil); }
            return true;
        }
        private bool OriginBlocked(Vector3 point)
        {
            foreach(var c in Physics.OverlapSphere(point,.025f,hitMask,QueryTriggerInteraction.Ignore))
                if(ActorIdentity.Root(c.transform)!=ActorIdentity.Root(transform)) return true;
            return false;
        }
        private void Fire(WeaponDefinition data)
        {
            Vector3 eye=viewCamera.transform.position;
            var offset=Random.insideUnitCircle*Mathf.Tan((IsAiming?data.aimSpread:data.hipSpread)*Mathf.Deg2Rad);
            Vector3 direction=(viewCamera.transform.forward+viewCamera.transform.right*offset.x+viewCamera.transform.up*(offset.y+Mathf.Tan(recoilAngle*Mathf.Deg2Rad))).normalized;
            Vector3 muzzle=viewCamera.transform.TransformPoint(Vector3.Lerp(data.muzzleOffset,data.aimMuzzleOffset,aimWeight));
            if(OriginBlocked(eye)) return;
            // Проверяем путь от центра тела через камеру до ствола: камера может выглянуть из-за стены,
            // но дульная точка справа всё ещё может быть перекрыта.
            Vector3 body=new Vector3(transform.position.x,eye.y,transform.position.z);
            if(WeaponHitQuery.Cast(body, (eye-body).normalized,Vector3.Distance(body,eye),0,transform,hitMask,out var blocked)) { Impact(blocked,data); return; }
            if(WeaponHitQuery.Cast(eye,(muzzle-eye).normalized,Vector3.Distance(eye,muzzle),0,transform,hitMask,out blocked)) { Impact(blocked,data); return; }
            bool found=WeaponHitQuery.Cast(eye,direction,data.range,0,transform,hitMask,out var target);
            Vector3 point=found?target.point:eye+direction*data.range;
            if(OriginBlocked(muzzle)) return;
            if(WeaponHitQuery.Cast(muzzle,(point-muzzle).normalized,Vector3.Distance(muzzle,point)+.02f,0,transform,hitMask,out var hit)) Impact(hit,data);
        }
        private void MeleeImpact(WeaponDefinition data)
        {
            Vector3 origin=viewCamera.transform.position;
            if(OriginBlocked(origin)) return;
            if(!WeaponHitQuery.Cast(origin,viewCamera.transform.forward,data.range,data.meleeRadius,transform,hitMask,out var hit)) return;
            if(!CombatVisibility.ClearLine(origin,hit.point,transform,ActorIdentity.Root(hit.transform),hitMask)) return;
            Impact(hit,data);
        }
        private void Impact(RaycastHit hit,WeaponDefinition data)
        {
            if(WeaponHitQuery.Damage(hit,transform,data.damage)) hitUntil=Time.time+hitMarkerSeconds;
            if(data.impactSound!=null) audioSource.PlayOneShot(data.impactSound,data.volume);
        }
        private void Play(AudioClip clip,WeaponDefinition data) { if(clip!=null) { audioSource.transform.position=viewCamera.transform.position; audioSource.PlayOneShot(clip,data.volume); } }
        private void LateUpdate()
        {
            if(view==null || equipped==null) return;
            view.SetActive(CanAct); if(!CanAct) return;
            var data=equipped.Definition.Weapon;
            aimWeight=Mathf.MoveTowards(aimWeight,IsAiming?1:0,Time.deltaTime*aimSpeed);
            Vector3 position=Vector3.Lerp(data.holdPosition,data.aimPosition,aimWeight);
            Vector3 rotation=data.holdEuler;
            if(data.kind==WeaponKind.Melee && Time.time<swingStarted+data.cooldown)
            {
                float age=Time.time-swingStarted;
                float t=age<data.windup ? age/Mathf.Max(.01f,data.windup) : 1-(age-data.windup)/Mathf.Max(.01f,data.cooldown-data.windup);
                t=Mathf.SmoothStep(0,1,Mathf.Clamp01(t)); position+=new Vector3(-.35f*t,.1f*Mathf.Sin(t*Mathf.PI),-.05f*t); rotation+=new Vector3(65*t,-35*t,25*t);
            }
            if(IsReloading) { position+=new Vector3(0,-.15f,0); rotation+=new Vector3(20,0,-25); }
            position.z-=recoilAngle*.018f; rotation.x-=recoilAngle*2;
            // У стен модель убирается, чтобы не торчать сквозь геометрию.
            if(WeaponHitQuery.Cast(viewCamera.transform.position,viewCamera.transform.forward,.55f,.05f,transform,hitMask,out _)) position.y-=.35f;
            view.transform.localPosition=position; view.transform.localRotation=Quaternion.Euler(rotation);
        }
        private void OnGUI()
        {
            if(!showHud || motor==null || !motor.IsAlive || !motor.GameplayInputEnabled) return;
            float scale=Mathf.Min(Screen.width/1000f,Screen.height/700f); var old=GUI.matrix;
            GUI.matrix=Matrix4x4.Scale(Vector3.one*scale); float w=Screen.width/scale,h=Screen.height/scale;
            if(showControls)
            {
                var oldColor=GUI.color; GUI.color=new Color(.055f,.075f,.095f,1);
                GUI.DrawTexture(new Rect(w-330,15,315,100),Texture2D.whiteTexture); GUI.color=oldColor;
                GUI.Label(new Rect(w-318,23,295,85),"ОРУЖИЕ\nE — взять · Tab — экипировать\n1/2 — смена · ЛКМ — атака\nПКМ — прицел · R — перезарядка",new GUIStyle(GUI.skin.label){fontSize=15});
            }
            var style=new GUIStyle(GUI.skin.label){fontSize=17,alignment=TextAnchor.MiddleRight};
            string text="Оружие не выбрано\nTab — рюкзак / экипировка";
            if(equipped!=null)
            {
                var data=equipped.Definition.Weapon;
                text=equipped.Definition.DisplayName+"\n"+(data.kind==WeaponKind.Firearm?$"{equipped.LoadedRounds} / {storage.Count(data.Ammo)}   [R] перезарядка":"ЛКМ — удар")+"\n"+Status;
            }
            GUI.Box(new Rect(w-330,h-115,315,100),GUIContent.none); GUI.Label(new Rect(w-320,h-110,295,90),text,style);
            if(equipped!=null && CanAct)
            {
                GUI.color=Time.time<hitUntil?Color.red:Color.white;
                GUI.Label(new Rect(w/2-8,h/2-12,25,25),Time.time<hitUntil?"×":"+",new GUIStyle(GUI.skin.label){fontSize=20}); GUI.color=Color.white;
            }
            GUI.matrix=old;
        }
    }
}
