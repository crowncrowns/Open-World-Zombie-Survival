using UnityEngine;
namespace ZombieSurvival.ModularBuildings
{
    public enum GeneratedRoomKind { Kitchen,Living,Bathroom,Toilet }
    public sealed class GeneratedApartmentRoom : MonoBehaviour
    {
        [Tooltip("Назначение помещения.")] public GeneratedRoomKind kind;
        [Tooltip("Доступно для будущего наполнения; настоящий лут пока не создаётся.")] public bool availableForLoot;
        [Tooltip("Площадь помещения, квадратные метры.")] public float area;
        [Tooltip("Размер свободного пола X/Z, м.")] public Vector2 floorSize;
        [Tooltip("Точка будущей мебели/основного назначения комнаты.")] public Transform furnishingAnchor;
        [Tooltip("Точка прохода в комнату.")] public Transform doorwayAnchor;
        [Tooltip("Явное закрытое полотно у недоступной комнаты.")] public BoxCollider closedDoor;
    }
}
