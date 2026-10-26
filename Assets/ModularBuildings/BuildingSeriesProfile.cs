using UnityEngine;
namespace ZombieSurvival.ModularBuildings
{
    [CreateAssetMenu(menuName="Zombie Survival/Building Series Profile")]
    public sealed class BuildingSeriesProfile : ScriptableObject
    {
        public string profileId = "demo-u-stair-v4";
        [Tooltip("Глубина технического продолжения наружных стен ниже пола первого этажа, м.")]
        public float foundationDepth=3;
        [TextArea] public string evidence = "Демонстрационный кирпичный тип с почти плоской кровлей 2%. Не реальная серия.";
        public SectionTemplate section;
        public GameObject solidWall, windowWall, entranceWall, apartmentWall, door, floor, stair;
        public GameObject[] apartmentDoors;
        public GameObject entranceDoor, ventCap, ventDuct;
        public Material glassMaterial, roofMaterial, stairWallMaterial, ceilingMaterial;
        public Mesh[] roofMeshes;
        public Mesh chimneyCurb, ductCurb;
        public GameObject interiorWindowLining;
        public Material facadeMaterial;
        [Tooltip("Отдельный материал нижней полосы. Пусто — красно-коричневый WeatheredPlinth.")]
        public Material baseTrimMaterial;
        [Header("Входные лестницы")]
        public Material entranceStairMaterial,entranceRailMaterial;
        [Tooltip("Одинаковый запас лестницы вниз от порога. Лишнее остаётся под Terrain; форма от рельефа не меняется.")]
        [Range(1f,40f)] public float entranceStairHeight=6f;
        [Min(1.2f)] public float entranceStairWidth=1.6f;
        [Range(.1f,.19f)] public float entranceMaxRiser=.17f;
        [Min(.28f)] public float entranceTreadDepth=.30f;
        [Min(1.2f)] public float entranceLandingDepth=1.5f;
        [HideInInspector] public float entranceMaxRun=60;
        [HideInInspector]
        public bool autoFitEntranceStairs=false;
        [Range(.02f,.3f),Tooltip("Зазор от верха цоколя до нижнего края окон первого этажа, м.")]
        public float baseTrimWindowClearance=.1f;
        [Range(.005f,.05f),Tooltip("Выступ цоколя наружу от стены, м.")]
        public float baseTrimProjection=.02f;
        public Shader approvedWorldFacadeShader;
        public bool requireWorldSpaceFacade=true;
        [Header("Правила нового городского дома")]
        [Tooltip("Новые дома: до 16 этажей / 6 подъездов, доступ до 4 этажа, квартиры, балконы и водостоки. Старые профили не меняются.")]
        public bool extendedCityRules;
        [Tooltip("Seed планировки квартир и вариантов фасада. ID дома также участвует в seed.")]
        public int citySeed=25;
        [Tooltip("Готовая квартира и её точки привязки. Создаётся целиком без уменьшения комнат; может занимать соседние секции.")]
        public ApartmentPrefabLayout apartmentLayout;
        [Tooltip("Модель завала вместо примитивов. Назначается новым блокировкам лестницы, проход остаётся закрыт отдельной коллизией.")]
        public GameObject stairBlockerPrefab;
        [Range(8,30),Tooltip("Уклон кровли домов 1–4 этажа, градусов. Для 5+ используется почти плоская кровля.")]
        public float lowRiseRoofPitch=18;
    }
    [System.Serializable]
    public sealed class SectionTemplate
    {
        // MVP deliberately supports one measured layout; different layouts require a new builder.
        public string templateId = "U-9x12-3-apartments-flat";
        public float sectionWidth = 9, sectionDepth = 12, floorHeight = 3;
        public Vector3 firstFlight = new Vector3(-.9f,.75f,5.85f);
        public Vector3 secondFlight = new Vector3(.9f,2.25f,5.85f);
    }
}
