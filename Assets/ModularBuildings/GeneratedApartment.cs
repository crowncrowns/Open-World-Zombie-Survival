using UnityEngine;
using ZombieSurvival.Interaction;
namespace ZombieSurvival.ModularBuildings
{
    public sealed class GeneratedApartment : MonoBehaviour
    {
        [Tooltip("Авторский ID квартиры; не SaveIdentity.")] public string apartmentId;
        [Tooltip("Воспроизводимый seed размещения.")] public int layoutSeed;
        [Tooltip("Авторский источник планировки.")] public ApartmentPrefabLayout layout;
        [Tooltip("Исходный prefab квартиры, не копия состояния.")] public GameObject sourcePrefab;
        [Tooltip("Единственный nested prefab экземпляр внутри квартиры.")] public GameObject prefabInstance;
        [Tooltip("Номер этажа от нуля.")] public int floor;
        [Tooltip("Занимаемые области в локальных XZ дома.")] public Rect[] occupiedRegions;
        [Tooltip("Зарезервированная площадь приватного тамбура.")] public Rect[] connectorRegions;
        [Tooltip("Путь от фиксированной двери подъезда к входу prefab.")] public Vector3[] connectorPath;
        [Tooltip("Вход на полу в локальных координатах дома.")] public Vector3 entryLocal;
        [Tooltip("Внешняя точка подхода в локальных координатах дома.")] public Vector3 approachLocal;
        [Tooltip("Входная дверь созданного квартирного слота.")] public DoorInteractable entranceDoor;
        [Tooltip("Угол исходно открытого входа.")] public float entranceAngle;
        [Tooltip("Обобщённый bounds, не замена точным occupiedRegions.")] public Bounds buildingLocalBounds;
    }
}
