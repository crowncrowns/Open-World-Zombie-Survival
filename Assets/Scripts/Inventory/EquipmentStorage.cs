using UnityEngine;
namespace ZombieSurvival.Inventory
{
    /// <summary>Закрытые wearable-слоты. Обычный auto-add/trade не кладёт вещи сюда; только PlayerEquipment и проверенное восстановление.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ItemStorage))]
    public sealed class EquipmentStorage : MonoBehaviour { }
}
