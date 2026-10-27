using UnityEngine;

namespace AdvancedCableTool
{
    [CreateAssetMenu(menuName = "Environment/Cable material library")]
    public sealed class CableMaterialLibrary : ScriptableObject
    {
        public Material[] variants = new Material[18];
        public Material copper;
        public Material Get(CableType type, CableWear wear)
        {
            int index = (int)type * 3 + (int)wear;
            return variants != null && index >= 0 && index < variants.Length ? variants[index] : null;
        }
    }
}
