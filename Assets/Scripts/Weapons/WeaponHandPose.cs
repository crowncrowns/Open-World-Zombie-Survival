using UnityEngine;

namespace ZombieSurvival.Weapons
{
    /// <summary>Опциональная посадка конкретной модели. Без профиля прежние настройки игрока остаются источником хвата.</summary>
    [CreateAssetMenu(menuName = "Zombie Survival/Хват оружия", fileName = "WeaponHandPose")]
    public sealed class WeaponHandPose : ScriptableObject
    {
        [InspectorName("Крепление к правой кисти"), Tooltip("Позиция корня модели относительно правой кисти Humanoid, м.")]
        public Vector3 handPosition;
        [InspectorName("Поворот на кисти"), Tooltip("Локальный поворот корня модели относительно правой кисти, градусы.")]
        public Vector3 handEuler;
        [InspectorName("От бедра"), Tooltip("Позиция корня модели относительно камеры без ПКМ, м: X вправо, Y вверх, Z вперёд.")]
        public Vector3 hipPosition = new Vector3(.16f,-.22f,.42f);
        [InspectorName("Прицеливание"), Tooltip("Позиция корня модели относительно камеры с ПКМ, м. Настройте по механическому прицелу.")]
        public Vector3 aimPosition = new Vector3(0,-.084f,.36f);
        [InspectorName("Оси модели относительно камеры"), Tooltip("Поворот модели относительно направления взгляда, градусы. Для ствола +Z оставьте 0; для +X задайте Y=-90.")]
        public Vector3 cameraEuler;
        [InspectorName("Левый хват"), Tooltip("Позиция левой кисти в локальных осях корня viewPrefab, м. Учитывает viewScale и масштаб на кисти.")]
        public Vector3 leftGrip;
        [InspectorName("Вес хвата"), Range(0,1), Tooltip("Сила коррекции обеих рук, 0–1. Не разрешает действия при блокировке ввода.")]
        public float weight = 1;
        [InspectorName("Подсказка локтей"), Tooltip("Сгиб локтей относительно плеча, м: X наружу, Y вверх, Z вперёд.")]
        public Vector3 elbowHint = new Vector3(.45f,-.15f,-.1f);
    }
}
