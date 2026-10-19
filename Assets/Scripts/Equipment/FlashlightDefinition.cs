using UnityEngine;
namespace ZombieSurvival.Equipment
{
    [CreateAssetMenu(menuName="Zombie Survival/Фонарик",fileName="Flashlight")]
    public sealed class FlashlightDefinition : ScriptableObject
    {
        [InspectorName("Модель в руках"),Tooltip("Префаб внешнего вида. MonoBehaviour запрещены: используйте визуальную обёртку. Коллайдеры, AudioSource и исходные Lights отключаются только на runtime-копии. Отдельный луч направлен по локальной +Z.")] public GameObject viewPrefab;
        [InspectorName("Позиция в руках"),Tooltip("Позиция держателя относительно камеры, м.")] public Vector3 holdPosition=new Vector3(.26f,-.24f,.45f);
        [InspectorName("Поворот в руках"),Tooltip("Поворот держателя модели и луча относительно камеры, градусы.")] public Vector3 holdEuler;
        [InspectorName("Масштаб в руках"),Tooltip("Масштаб держателя модели; исходный префаб не меняется.")] public Vector3 viewScale=Vector3.one;
        [InspectorName("Позиция света"),Tooltip("Точка источника относительно держателя, м. Разместите перед стеклом фонаря.")] public Vector3 lightLocalPosition=new Vector3(0,0,.2f);
        [InspectorName("Поворот света"),Tooltip("Дополнительный поворот луча, градусы. При нуле направлен по +Z держателя.")] public Vector3 lightLocalEuler;
        [InspectorName("Дальность"),Tooltip("Дальность реального SpotLight, м."),Min(.1f)] public float range=22;
        [InspectorName("Яркость"),Tooltip("Яркость SpotLight в единицах текущего render pipeline."),Min(0)] public float intensity=5;
        [InspectorName("Угол луча"),Tooltip("Внешний угол конуса, градусы."),Range(1,179)] public float spotAngle=48;
        [InspectorName("Внутренний угол"),Tooltip("Внутренний угол конуса, градусы; ограничивается внешним."),Range(0,179)] public float innerSpotAngle=28;
        [InspectorName("Цвет"),Tooltip("Цвет луча. По умолчанию тёплый белый.")] public Color color=new Color(1,.86f,.68f,1);
        [InspectorName("Слои света"),Tooltip("Какие слои освещаются лучом.")] public LayerMask cullingMask=~0;
        [InspectorName("Cookie луча"),Tooltip("Необязательная текстура формы луча. Требует поддержки cookie в текущем URP.")] public Texture cookie;
        [InspectorName("Тени"),Tooltip("Реальные тени луча. Требуют дополнительных теней в URP.")] public LightShadows shadows=LightShadows.Soft;
        [InspectorName("Смещение теней"),Tooltip("Shadow bias луча для уменьшения артефактов самозатенения."),Range(0,2)] public float shadowBias=.02f;
        [InspectorName("Нормальное смещение теней"),Tooltip("Normal bias теней луча; слишком большое значение отрывает тень от поверхности."),Range(0,3)] public float shadowNormalBias=.04f;
    }
}
