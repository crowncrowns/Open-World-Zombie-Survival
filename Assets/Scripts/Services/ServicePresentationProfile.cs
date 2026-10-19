using UnityEngine;
using UnityEngine.Audio;

namespace ZombieSurvival.Services
{
    [CreateAssetMenu(menuName="Zombie Survival/Оформление сервисов",fileName="ServicePresentation")]
    public sealed class ServicePresentationProfile : ScriptableObject
    {
        [InspectorName("Ожидание торговца"),Tooltip("Необязательный Humanoid Animator Controller. Назначается только совместимому Animator без собственного контроллера; пользовательский контроллер не заменяется.")]
        public RuntimeAnimatorController idleController;
        [InspectorName("Успешная сделка"),Tooltip("Короткий звук после успешной покупки/продажи. Не выполняет операцию и не вызывает игровой шум.")]
        public AudioClip tradeSuccess;
        [InspectorName("Отказ операции"),Tooltip("Звук отказа явного запроса/подтверждения. Пассивные проверки и выключенные кнопки не звучат.")]
        public AudioClip rejected;
        [InspectorName("Успешный ремонт"),Tooltip("Короткий звук уже завершённого ремонта. Время клипа не задерживает ремонт и не создаёт очередь.")]
        public AudioClip repairSuccess;
        [Range(0,1),InspectorName("Громкость"),Tooltip("Громкость обратной связи 0…1. На слух зомби/GameplayNoise не влияет.")]
        public float volume=.45f;
        [InspectorName("Группа микшера"),Tooltip("Необязательная существующая группа Audio Mixer для сервисных звуков.")]
        public AudioMixerGroup mixerGroup;
    }
}
