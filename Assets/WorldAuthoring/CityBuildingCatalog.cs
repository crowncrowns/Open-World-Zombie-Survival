using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.ModularBuildings;

namespace ZombieSurvival.WorldAuthoring
{
    public enum CityBuildingKind { Profile=0, Prefab=1 }
    [Serializable]
    public sealed class CityBuildingEntry
    {
        [Tooltip("Постоянный уникальный ключ типа в каталоге.")] [InspectorName("Ключ типа")] public string key;
        [Tooltip("Понятное название для автора квартала.")] [InspectorName("Название")] public string title;
        [Tooltip("Способ создания: профиль существующего генератора или готовый prefab.")] [InspectorName("Источник геометрии")] public CityBuildingKind kind;
        [Tooltip("Проверенный профиль модульного дома.")] [InspectorName("Профиль дома")] public BuildingSeriesProfile profile;
        [Tooltip("Готовый prefab из Project, не объект сцены.")] [InspectorName("Готовый префаб")] public GameObject prefab;
        [Tooltip("Размер корпуса prefab по его локальным X/Z, м. Не включает автоматически запас участка.")] [InspectorName("Размер корпуса X/Z")] public Vector2 prefabFootprint=new Vector2(12,12);
        [Tooltip("Центр корпуса относительно корня prefab по локальным X/Z, м.")] [InspectorName("Центр корпуса от корня X/Z")] public Vector2 prefabPivotOffset;
    }
    [CreateAssetMenu(menuName="Zombie Survival/World Authoring/City Building Catalog")]
    public sealed class CityBuildingCatalog : ScriptableObject
    {
        [Tooltip("Общие типы зданий; ключи должны быть уникальны.")] [InspectorName("Типы зданий")] public List<CityBuildingEntry> entries=new List<CityBuildingEntry>();
    }
}
