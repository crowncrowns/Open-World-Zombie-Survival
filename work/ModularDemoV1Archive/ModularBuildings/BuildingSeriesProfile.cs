using UnityEngine;
namespace ZombieSurvival.ModularBuildings
{
    [CreateAssetMenu(menuName="Zombie Survival/Building Series Profile")]
    public sealed class BuildingSeriesProfile : ScriptableObject
    {
        public string profileId = "demo-u-stair-v1";
        [TextArea] public string evidence = "Демонстрационный профиль, не реальная серия. Два марша 1,5 м, этаж 3 м.";
        public SectionTemplate section;
        public GameObject solidWall, windowWall, entranceWall, apartmentWall, door, floor, stair;
    }
    [System.Serializable]
    public sealed class SectionTemplate
    {
        // MVP deliberately supports one measured layout; different layouts require a new builder.
        public string templateId = "U-9x9-2-apartments";
        public float sectionWidth = 9, sectionDepth = 9, floorHeight = 3;
        public Vector3 firstFlight = new Vector3(-.75f,.75f,2.85f);
        public Vector3 secondFlight = new Vector3(.75f,2.25f,2.85f);
    }
}
