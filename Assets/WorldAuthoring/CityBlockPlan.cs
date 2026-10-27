using System;
using System.Collections.Generic;
using UnityEngine;
namespace ZombieSurvival.WorldAuthoring
{
    [Serializable]
    public sealed class CityLot
    {
        [Tooltip("Постоянный ID участка. Не меняйте после генерации здания.")] [InspectorName("ID участка")] public string id=Guid.NewGuid().ToString("N");
        [Tooltip("Название участка.")] [InspectorName("Название")] public string title="Участок";
        [Tooltip("Ключ типа из каталога.")] [InspectorName("Ключ типа")] public string entryKey;
        [Tooltip("Центр зарезервированного прямоугольника в мировых координатах. Y задаётся явно; Terrain не меняется.")] [InspectorName("Мировой центр участка")] public Vector3 position;
        [Tooltip("Поворот участка вокруг мировой Y, градусов.")] [InspectorName("Поворот Y")] public float yaw;
        [Tooltip("Полный размер участка X/Z, м, включая лестницы, свесы и прочую геометрию.")] [InspectorName("Размер участка X/Z")] public Vector2 size=new Vector2(24,40);
        [Tooltip("Год как метаданные профиля; не выбирает реальную серию автоматически.")] [InspectorName("Год")] public int year=1980;
        [Tooltip("Этажи модульного профиля.")] [InspectorName("Этажи")] public int floors=5;
        [Tooltip("Подъезды модульного профиля.")] [InspectorName("Подъезды")] public int entrances=1;
        [Tooltip("Включить участок в проверку и генерацию.")] [InspectorName("Включён")] public bool enabled=true;
    }
    [CreateAssetMenu(menuName="Zombie Survival/World Authoring/City Block Plan")]
    public sealed class CityBlockPlan : ScriptableObject
    {
        [Tooltip("Постоянный ID плана. Создаётся один раз; копия asset сохраняет ID и требует явного нового ID для другого плана.")] [InspectorName("ID плана")] public string planId=Guid.NewGuid().ToString("N");
        [Tooltip("Общий каталог доступных зданий.")] [InspectorName("Каталог")] public CityBuildingCatalog catalog;
        [Tooltip("Участки с явными позами и запасом под полную геометрию.")] [InspectorName("Участки")] public List<CityLot> lots=new List<CityLot>();
    }
}
