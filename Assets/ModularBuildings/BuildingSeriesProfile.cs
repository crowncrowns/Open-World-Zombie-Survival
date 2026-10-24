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
        public Shader approvedWorldFacadeShader;
        public bool requireWorldSpaceFacade=true;
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
