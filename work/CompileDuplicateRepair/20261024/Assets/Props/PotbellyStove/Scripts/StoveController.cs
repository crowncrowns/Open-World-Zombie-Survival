using UnityEngine;

namespace ZombieSurvival.EnvironmentProps
{
    /// <summary>Presentation only: fuel, heat, damage and world saving belong to game systems.</summary>
    [DisallowMultipleComponent]
    public sealed class StoveController : MonoBehaviour
    {
        [SerializeField, Tooltip("Горение при первом включении экземпляра печи.")] bool burningOnStart = true;
        [SerializeField, Range(0, 1), Tooltip("Мощность огня: 0–1, без расхода топлива.")] float fireIntensity = 1;
        [SerializeField, Min(.1f), Tooltip("Время плавного розжига и гашения пламени, с.")] float transitionSeconds = 2.8f;
        [SerializeField, Min(.1f), Tooltip("Время остывания углей после тушения, с.")] float coolingSeconds = 12;
        [SerializeField, Min(0), Tooltip("Максимальная яркость света топки, условные единицы интенсивности Unity.")] float lightIntensity = .5f;
        [SerializeField, Tooltip("Системы языков пламени внутри топки.")] ParticleSystem[] flames;
        [SerializeField, Tooltip("Лёгкий дым из верхнего отвода трубы.")] ParticleSystem smoke;
        [SerializeField, Tooltip("Мелкие искры внутри топки.")] ParticleSystem sparks;
        [SerializeField, Tooltip("Тёплый динамический источник света топки.")] Light fireLight;
        [SerializeField, Tooltip("Меши углей и обугленных поленьев с Emission.")] Renderer[] emberRenderers;
        [SerializeField, Tooltip("Необязательная камера для снижения количества дальних частиц.")] Camera distanceCamera;
        [SerializeField, Min(1), Tooltip("Расстояние начала снижения количества частиц, м.")] float farDistance = 18;
        float target, current, heat;
        bool initialized;
        MaterialPropertyBlock block;
        public bool IsBurning => target > 0;
        public float CurrentIntensity => current;
        public float EmberHeat => heat;
        public void Configure(ParticleSystem[] f, ParticleSystem s, ParticleSystem k, Light l, Renderer[] e)
        { flames=f; smoke=s; sparks=k; fireLight=l; emberRenderers=e; }
        void OnEnable()
        {
            if (!initialized) { target=burningOnStart?fireIntensity:0;current=heat=target;initialized=true; }
            block ??= new MaterialPropertyBlock();
            foreach(var ps in GetComponentsInChildren<ParticleSystem>(true)) ps.Play(true);
            Apply();
        }
        public void Ignite() { if(fireIntensity<=0)fireIntensity=1;target=fireIntensity; }
        public void Extinguish() { target=0; }
        public void SetFireIntensity(float value)
        {
            if (float.IsNaN(value)||float.IsInfinity(value)) return;
            fireIntensity=Mathf.Clamp01(value);target=fireIntensity;
        }
        void Update()
        {
            current=Mathf.MoveTowards(current,target,Time.deltaTime/Mathf.Max(.1f,transitionSeconds));
            heat=Mathf.MoveTowards(heat,current,Time.deltaTime/Mathf.Max(.1f,current>heat?transitionSeconds:coolingSeconds));
            Apply();
        }
        void Rate(ParticleSystem p,float amount)
        { if(!p)return; var emission=p.emission;emission.rateOverTime=amount; }
        void Apply()
        {
            var cam=distanceCamera?distanceCamera:Camera.main;
            float distance=cam?Vector3.Distance(cam.transform.position,transform.position):0;
            float quality=Mathf.Lerp(1,.25f,Mathf.InverseLerp(farDistance,farDistance*2,distance));
            if(flames!=null)foreach(var p in flames)Rate(p,23*current*quality);
            Rate(smoke,5.5f*current*quality);Rate(sparks,2.2f*current*quality);
            float flicker=.91f+.09f*Mathf.PerlinNoise(Time.time*.83f,2.7f);
            if(fireLight){fireLight.enabled=current>.001f;fireLight.intensity=lightIntensity*current*flicker;}
            if(emberRenderers==null)return;
            block ??= new MaterialPropertyBlock();
            foreach(var r in emberRenderers)
            {
                if(!r)continue;
                for(int i=0;i<r.sharedMaterials.Length;i++)
                {
                    var m=r.sharedMaterials[i];if(!m||!m.name.StartsWith("M_Embers"))continue;
                    r.GetPropertyBlock(block,i);
                    block.SetColor("_EmissionColor",new Color(4,4,4)*heat*(.85f+.15f*Mathf.PerlinNoise(Time.time*.45f,7.2f)));
                    r.SetPropertyBlock(block,i);
                }
            }
        }
        void OnDisable()
        {
            foreach(var p in GetComponentsInChildren<ParticleSystem>(true))p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(fireLight)fireLight.enabled=false;
        }
    }
}
