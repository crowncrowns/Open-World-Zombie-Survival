using UnityEngine;
namespace ZombieSurvival.ModularBuildings
{
    public sealed class GeneratedBuilding : MonoBehaviour
    {
        public string buildingId;
        public BuildingSeriesProfile profile;
        public int year, floors, entrances;
        [HideInInspector] public float generatedSectionWidth;
        public string revision = "demo-v4";
        [HideInInspector] public int endWindowRuleVersion;
        [HideInInspector] public int exteriorEnvelopeVersion;
        [HideInInspector] public int entranceAccessRuleVersion;
        [HideInInspector] public Material baseTrimMaterialOverride;
        [HideInInspector] public Material exteriorMaterialOverride;
        [HideInInspector] public bool exteriorIncludesEntranceDoors;
        [HideInInspector] public DoorSurfaceMaterials[] originalExteriorDoorMaterials = new DoorSurfaceMaterials[0];

        [System.Serializable]
        public sealed class DoorSurfaceMaterials
        {
            public MeshRenderer renderer;
            public Material[] materials;
        }
    }
}
