using UnityEngine;
using ZombieSurvival.Interaction;
namespace ZombieSurvival.ModularBuildings
{
    public sealed class GeneratedApartment : MonoBehaviour
    {
        [Tooltip("Авторский ID квартиры; не SaveIdentity.")] public string apartmentId;
        [Tooltip("Воспроизводимый seed планировки.")] public int layoutSeed;
        [Tooltip("Входная дверь существующего квартирного слота.")] public DoorInteractable entranceDoor;
        [Tooltip("Угол исходно открытого входа, наружу квартиры.")] public float entranceAngle;
        [Tooltip("Локальная область квартиры относительно корня дома.")] public Bounds buildingLocalBounds;
    }
}
