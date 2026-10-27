using System;
using System.Collections.Generic;
using UnityEngine;
namespace ZombieSurvival.ModularBuildings
{
    [Serializable]
    public sealed class ApartmentPrefabRoomMarker
    {
        [Tooltip("Название помещения.")] public string title;
        [Tooltip("Назначение помещения.")] public GeneratedRoomKind kind;
        [Tooltip("Точка помещения в локальных координатах исходного prefab.")] public Vector3 position;
        [Tooltip("Доступность для будущего наполнения, без создания лута.")] public bool availableForLoot=true;
    }
    [CreateAssetMenu(menuName="Zombie Survival/Apartment Prefab Layout")]
    public sealed class ApartmentPrefabLayout : ScriptableObject
    {
        [Tooltip("Исходный prefab квартиры; масштаб при планировании не меняется.")] public GameObject prefab;
        [Tooltip("Точка входа на уровне пола в локальных координатах prefab. Назначается после подтверждения автором.")] public Vector3 entryFloorPosition;
        [Tooltip("Горизонтальное направление от входа внутрь квартиры.")] public Vector3 inwardDirection=Vector3.forward;
        [Tooltip("Реальная высота чистого пола в локальных координатах prefab.")] public float floorY=-3;
        [Tooltip("Высота помещений, м; справочные метаданные.")] public float roomHeight=3;
        [Tooltip("Объединение непересекающихся прямоугольников XZ реальной занимаемой площади; без изменения масштаба.")] public Rect[] footprintRegions=Array.Empty<Rect>();
        [Tooltip("Необязательные авторские точки помещений исходной планировки.")] public List<ApartmentPrefabRoomMarker> rooms=new List<ApartmentPrefabRoomMarker>();
    }
}
