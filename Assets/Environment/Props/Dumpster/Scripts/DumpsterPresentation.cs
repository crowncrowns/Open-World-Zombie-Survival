using UnityEngine;

namespace ZombieSurvival.EnvironmentProps
{
    // Both prop prefab locations share this single component implementation.
    [DisallowMultipleComponent]
    public sealed class DumpsterPresentation : MonoBehaviour
    {
        [SerializeField, Tooltip("Animator крышки. Параметр Open задаёт желаемое положение; действия игрока подключаются отдельно.")]
        private Animator lidAnimator;
        [SerializeField, Range(0f,1f), Tooltip("Количество грязи от 0 до 1. Меняется через маски, общие материалы не копируются.")]
        private float dirtAmount = .68f;
        [SerializeField, Range(0f,1f), Tooltip("Выцветание пластика от 0 до 1.")]
        private float fadeAmount = .25f;
        [SerializeField, Tooltip("Начальное число варианта. Одинаковое число даёт одинаковое размещение объявлений и выбор мешков.")]
        private int variationSeed = 17;
        [SerializeField, Range(0,8), Tooltip("Количество видимых объявлений от 0 до 8; каждое можно перемещать вручную после отключения автоматической раскладки.")]
        private int stickerCount = 6;
        [SerializeField, Tooltip("Применить детерминированную раскладку объявлений при включении. Выключите для ручной расстановки дочерних decals.")]
        private bool arrangeStickersOnEnable = true;
        [SerializeField, Tooltip("Группа переставляемых сеточных decals с рваными и загнутыми краями.")]
        private Transform stickerRoot;
        [SerializeField, Tooltip("Группа независимых мешков. Отсутствует у пустого варианта.")]
        private Transform bagRoot;
        [SerializeField, Range(0,3), Tooltip("Количество скрытых верхних мешков от 0 до 3. Не двигает мусор через корпус или крышку.")]
        private int missingTopBags;
        static readonly int OpenId=Animator.StringToHash("Open");
        MaterialPropertyBlock block;
        public bool RequestedOpen { get; private set; }
        public void SetOpen(bool value) { RequestedOpen=value; if(lidAnimator) lidAnimator.SetBool(OpenId,value); }
        public void ToggleLid() => SetOpen(!RequestedOpen);
        public void SetVariation(int seed,float dirt,float fading,int announcements,int missingBags=0)
        {
            variationSeed=seed;dirtAmount=Mathf.Clamp01(dirt);fadeAmount=Mathf.Clamp01(fading);stickerCount=Mathf.Clamp(announcements,0,8);missingTopBags=Mathf.Clamp(missingBags,0,3);ApplyVariation();
        }
        void OnEnable() { ApplyVariation(); SetOpen(false); }
        void OnDisable() { RequestedOpen=false; }
        [ContextMenu("Применить вариант оформления")]
        public void ApplyVariation()
        {
            block ??= new MaterialPropertyBlock();
            foreach(var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats=r.sharedMaterials;
                for(int j=0;j<mats.Length;j++)
                {
                    if(!mats[j] || mats[j].shader.name!="ZombieSurvival/Dumpster Surface") continue;
                    r.GetPropertyBlock(block,j);block.SetFloat("_DirtAmount",dirtAmount);block.SetFloat("_FadeAmount",fadeAmount);block.SetFloat("_VariantSeed",variationSeed);block.SetFloat("_HeightOffset",r.name.StartsWith("Dumpster_Lid_LOD")?1.23f:0f);r.SetPropertyBlock(block,j);
                }
            }
            var random=new System.Random(variationSeed);
            if(stickerRoot && arrangeStickersOnEnable)
            {
                for(int i=0;i<stickerRoot.childCount;i++)
                {
                    var t=stickerRoot.GetChild(i);t.gameObject.SetActive(i<stickerCount);
                    bool front=i<5;float y=.61f+(float)random.NextDouble()*.36f;
                    float taper=.465f+(y-.32f)*.066f;
                    if(front) { t.localPosition=new Vector3(-.40f+i*.18f+(float)random.NextDouble()*.04f,y,taper+.006f);t.localRotation=Quaternion.Euler(3.77f,0,((float)random.NextDouble()-.5f)*16); }
                    else { t.localPosition=new Vector3(.60f+(y-.32f)*.06f,y,-.28f+(i-5)*.22f);t.localRotation=Quaternion.Euler(0,90,((float)random.NextDouble()-.5f)*12); }
                    t.localScale=Vector3.one*(.64f+(float)random.NextDouble()*.45f);
                }
            }
            if(bagRoot)
            {
                int lower=bagRoot.childCount/2,upper=bagRoot.childCount-lower;
                for(int i=0;i<bagRoot.childCount;i++)
                {
                    int choice=upper>0?(i-lower+(variationSeed%upper+upper)%upper)%upper:0;
                    bagRoot.GetChild(i).gameObject.SetActive(i<lower||choice>=missingTopBags);
                }
            }
        }
    }
}
