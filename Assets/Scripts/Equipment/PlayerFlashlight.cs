using UnityEngine;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;
namespace ZombieSurvival.Equipment
{
    [DefaultExecutionOrder(180)]
    [DisallowMultipleComponent,RequireComponent(typeof(FirstPersonController),typeof(PlayerHotbar),typeof(ItemStorage))]
    public sealed class PlayerFlashlight : MonoBehaviour,IPlayerItemUseHandler
    {
        [SerializeField,InspectorName("Камера взгляда"),Tooltip("Камера модели в руках. При потере ссылки ищется дочерняя камера игрока.")] private Camera viewCamera;
        private FirstPersonController motor;
        private PlayerHotbar hotbar;
        private ItemStorage storage;
        private ItemStack held;
        private FlashlightDefinition definition;
        private GameObject holder;
        private Light beam;
        private bool switchedOn;
        public bool IsOn=>beam!=null&&beam.enabled&&holder!=null&&holder.activeInHierarchy;
        public ItemStack HeldItem=>held;
        public Light Beam=>beam;
        private bool Allowed=>isActiveAndEnabled&&motor!=null&&motor.AcceptingGameplayInput&&!motor.IsSprinting
            &&(motor.Parkour==null||!motor.Parkour.OwnsMovement)&&hotbar!=null&&hotbar.isActiveAndEnabled&&viewCamera!=null&&viewCamera.isActiveAndEnabled;
        public bool CanHandle(ItemStack item)=>item!=null&&!item.IsEmpty&&item.Definition.Weapon==null&&item.Definition.Flashlight!=null;
        private void OnEnable()=>Initialize();
        private void Initialize()
        {
            Release();motor=GetComponent<FirstPersonController>();hotbar=GetComponent<PlayerHotbar>();storage=GetComponent<ItemStorage>();
            if(viewCamera==null)viewCamera=GetComponentInChildren<Camera>(true);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreForPlay()
        {foreach(var component in Object.FindObjectsByType<PlayerFlashlight>(FindObjectsInactive.Include))if(component.isActiveAndEnabled)component.Initialize();}
        private void OnDisable()=>Release();
        private void OnDestroy()=>Release();
        private void OnApplicationFocus(bool focused){if(!focused){switchedOn=false;if(beam!=null)beam.enabled=false;}}
        private void Release()
        {
            switchedOn=false;if(beam!=null)beam.enabled=false;
            if(holder!=null){holder.SetActive(false);Destroy(holder);}holder=null;beam=null;held=null;definition=null;
        }
        private void Update()=>Refresh();
        private void LateUpdate()=>Refresh();
        private void Refresh()
        {
            var item=hotbar!=null?hotbar.ActiveStack:null;
            if(!CanHandle(item)||storage==null||!storage.Contains(item)){Release();return;}
            if(held!=item||definition!=item.Definition.Flashlight)
            {
                Release();held=item;definition=item.Definition.Flashlight;
                if(viewCamera!=null&&definition.viewPrefab!=null)CreateView();
            }
            if(!Allowed)switchedOn=false;
            if(holder!=null)holder.SetActive(Allowed);
            if(beam!=null)beam.enabled=Allowed&&switchedOn;
        }
        private void CreateView()
        {
            // Disabled MonoBehaviours can still Awake when activated: reject script-rich visuals before instantiation.
            if(definition.viewPrefab.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)
            { Debug.LogWarning("Модель фонарика должна содержать только визуальные компоненты, без MonoBehaviour. Создайте отдельную визуальную обёртку.",definition);return; }
            holder=new GameObject("HeldFlashlight");holder.SetActive(false);holder.transform.SetParent(viewCamera.transform,false);
            holder.transform.localPosition=definition.holdPosition;holder.transform.localRotation=Quaternion.Euler(definition.holdEuler);holder.transform.localScale=definition.viewScale;
            var visual=Instantiate(definition.viewPrefab,holder.transform,false);
            foreach(var camera in visual.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            foreach(var listener in visual.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            foreach(var collider in visual.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var body in visual.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;}
            foreach(var source in visual.GetComponentsInChildren<AudioSource>(true))source.enabled=false;
            foreach(var light in visual.GetComponentsInChildren<Light>(true))light.enabled=false;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var lightObject=new GameObject("FlashlightBeam");lightObject.transform.SetParent(holder.transform,false);
            lightObject.transform.localPosition=definition.lightLocalPosition;lightObject.transform.localRotation=Quaternion.Euler(definition.lightLocalEuler);
            beam=lightObject.AddComponent<Light>();beam.type=LightType.Spot;beam.enabled=false;
            beam.range=Mathf.Max(.1f,Finite(definition.range,22));beam.intensity=Mathf.Max(0,Finite(definition.intensity,5));
            beam.spotAngle=Mathf.Clamp(Finite(definition.spotAngle,48),1,179);beam.innerSpotAngle=Mathf.Clamp(Finite(definition.innerSpotAngle,28),0,beam.spotAngle);
            beam.color=definition.color;beam.cookie=definition.cookie;beam.cullingMask=definition.cullingMask;beam.shadows=definition.shadows;
            beam.shadowBias=Mathf.Clamp(Finite(definition.shadowBias,.02f),0,2);beam.shadowNormalBias=Mathf.Clamp(Finite(definition.shadowNormalBias,.04f),0,3);
        }
        private static float Finite(float value,float fallback)=>float.IsNaN(value)||float.IsInfinity(value)?fallback:value;
        public bool TryUsePrimary(PlayerHotbar source,ItemStack item)
        {
            if(source!=hotbar||!Allowed||source.ActiveStack!=item||!CanHandle(item)||!storage.Contains(item))return false;
            Refresh();if(beam==null)return false;switchedOn=!switchedOn;beam.enabled=switchedOn;return true;
        }
        public bool TryUseSecondary(PlayerHotbar source,ItemStack item)=>false;
    }
}
