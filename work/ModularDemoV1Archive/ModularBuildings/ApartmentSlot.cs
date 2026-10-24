using UnityEngine;
namespace ZombieSurvival.ModularBuildings
{
    public sealed class ApartmentSlot : MonoBehaviour
    {
        public string slotId;
        public Transform doorHinge, doorLeaf;
        [Tooltip("Вставляйте квартиру сюда. Генератор не удаляет существующие здания.")]
        public Transform manualContent;
    }
}
