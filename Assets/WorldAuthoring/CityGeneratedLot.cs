using UnityEngine;
namespace ZombieSurvival.WorldAuthoring
{
    [DisallowMultipleComponent]
    public sealed class CityGeneratedLot : MonoBehaviour
    {
        [Tooltip("ID исходного авторского плана, не SaveIdentity.")] [InspectorName("ID плана")] public string planId;
        [Tooltip("ID участка исходного плана.")] [InspectorName("ID участка")] public string lotId;
        [Tooltip("Постоянный авторский ID созданного здания.")] [InspectorName("ID здания")] public string buildingId;
    }
}
