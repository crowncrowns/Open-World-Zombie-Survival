using UnityEngine;
using ZombieSurvival.Inventory;

namespace ZombieSurvival.Weapons
{
    public enum WeaponKind { [InspectorName("Ближний бой")] Melee, [InspectorName("Огнестрельное")] Firearm }
    /// <summary>Общие настройки типа оружия. Патроны экземпляра находятся только в ItemStack.</summary>
    [CreateAssetMenu(menuName = "Zombie Survival/Оружие", fileName = "Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Tooltip("Тип атаки: удар или мгновенный луч выстрела.")]
        public WeaponKind kind;
        [InspectorName("Износ за применение"), Tooltip("Расход прочности экземпляра, ед./применение: за состоявшийся выстрел или завершённый замах, в том числе промах. Отменённый замах не расходует прочность. 0 отключает расход, но не разрешает атаку сломанным оружием. Отрицательное/NaN/Infinity запрещает атаку."), Min(0)]
        public float durabilityCostPerUse = .5f;
        [Tooltip("Урон одного попадания, единицы здоровья.")][Min(0)] public float damage = 35;
        [Tooltip("Дальность попадания от камеры, м.")][Min(.1f)] public float range = 1.8f;
        [Tooltip("Пауза между началами атак, с.")][Min(.05f)] public float cooldown = .7f;
        [Tooltip("Задержка попадания после начала замаха, с.")][Min(0)] public float windup = .25f;
        [Tooltip("Радиус проверки удара, м. Стены дополнительно проверяются тонким лучом.")][Range(0,.5f)] public float meleeRadius = .15f;
        [Tooltip("Расход выносливости за замах. При нехватке удар не начинается.")][Min(0)] public float staminaCost = 14;
        [Tooltip("Тип патронов в рюкзаке. Не назначать сам предмет оружия.")][SerializeField] private ItemDefinition ammo;
        [Tooltip("Ёмкость магазина, патроны. Новые пистолеты подбираются пустыми.")][SerializeField, Min(1)] private int magazineSize = 12;
        [Tooltip("Длительность перезарядки, с. Патроны расходуются только при её завершении.")][Min(.1f)] public float reloadSeconds = 1.7f;
        [Tooltip("Автоматический огонь при удержании. Выключено — один выстрел на нажатие.")] public bool automatic;
        [Tooltip("Разброс от бедра, градусы конуса.")][Range(0,15)] public float hipSpread = 1.5f;
        [Tooltip("Разброс при удержании прицела, градусы конуса.")][Range(0,15)] public float aimSpread = .15f;
        [Tooltip("Подброс направления следующих выстрелов после выстрела, градусы.")][Range(0,15)] public float recoil = 2;
        [Tooltip("Скорость возврата отдачи к нулю, градусов/с.")][Min(.1f)] public float recoilRecovery = 7;
        [Tooltip("Шум для слуха AI: X — громкость, Y — радиус в метрах. Независим от AudioSource.")] public Vector2 noise = new Vector2(2,12);
        [Tooltip("Префаб только внешнего вида. Физика и коллайдеры в руках отключаются.")] public GameObject viewPrefab;
        [InspectorName("Профиль хвата модели"), Tooltip("Необязательная посадка этой модели на кисть и двухручный IK. Пусто — прежние настройки на корне игрока (пистолет). Не меняет урон или сохранения.")]
        public WeaponHandPose handPose;
        [InspectorName("Анимации тела"), Tooltip("Необязательные замены клипов рук совместимого UAL-контроллера. Пусто — обычные анимации игрока. Не содержит боевых AnimationEvents.")]
        public AnimatorOverrideController bodyAnimations;
        [InspectorName("Исходная перезарядка тела"), Min(.1f), Tooltip("Длительность исходного клипа рук в профиле, с. Масштабируется до reloadSeconds; боезапас меняет только PlayerWeapons.")]
        public float bodyReloadSeconds = 1.7f;
        [Header("Визуальный след выстрела")]
        [InspectorName("Показывать трассер"), Tooltip("Рисует короткий светящийся след от дула к цели. Это только визуальный эффект: попадание и урон по-прежнему рассчитывает PlayerWeapons.")]
        public bool showShotTracer;
        [InspectorName("Цвет трассера"), Tooltip("Цвет следа пули, включая яркость в начале и плавное затухание к концу.")]
        public Color shotTracerColor = new Color(1f, .63f, .24f, 1f);
        [InspectorName("Ширина трассера"), Min(.001f), Tooltip("Ширина светового следа, м.")]
        public float shotTracerWidth = .016f;
        [InspectorName("Время трассера"), Min(.01f), Tooltip("Сколько секунд виден след выстрела.")]
        public float shotTracerSeconds = .085f;
        [InspectorName("Максимальная длина трассера"), Min(.1f), Tooltip("Максимальная длина светового следа, м. Реальная точка попадания остаётся прежней.")]
        public float shotTracerMaxLength = 40f;
        [InspectorName("Вспышка у дула"), Tooltip("Показывает короткую вспышку частиц у дула при каждом выстреле.")]
        public bool showMuzzleFlash;
        [InspectorName("Искры попадания"), Tooltip("Показывает короткие искры на поверхности или актёре, куда попал луч.")]
        public bool showImpactSparks;
        [InspectorName("Цвет искр"), Tooltip("Цвет визуальных искр при попадании. Не меняет тип цели или урон.")]
        public Color impactSparkColor = new Color(1f, .56f, .2f, 1f);
        [InspectorName("Количество искр"), Range(1, 16), Tooltip("Количество частиц на попадание.")]
        public int impactSparkCount = 7;
        [Tooltip("Позиция модели относительно камеры от бедра, м.")] public Vector3 holdPosition = new Vector3(.3f,-.3f,.55f);
        [Tooltip("Позиция модели при прицеливании, м.")] public Vector3 aimPosition = new Vector3(0,-.18f,.55f);
        [Tooltip("Поворот модели относительно камеры, градусы.")] public Vector3 holdEuler;
        [Tooltip("Масштаб модели в руках.")] public Vector3 viewScale = Vector3.one;
        [Tooltip("Путь к дульной точке внутри viewPrefab, например Model/Rig/Muzzle. Пусто — прежние смещения от камеры. При Q/E заданная точка следует реальному оружию и рукам.")]
        public string muzzleTransformPath;
        [Tooltip("Дульная точка от бедра относительно камеры, м. Путь до неё проверяется на стены.")] public Vector3 muzzleOffset = new Vector3(.28f,-.22f,.64f);
        [Tooltip("Дульная точка при прицеливании относительно камеры, м. Плавно следует за позой модели.")] public Vector3 aimMuzzleOffset = new Vector3(0,-.015f,.64f);
        [Tooltip("Звуки удара или выстрела. Случайный вариант на атаку.")] public AudioClip[] attacks;
        [Tooltip("Звук перезарядки; отдельный канал, не прерывает шаги.")] public AudioClip reloadSound;
        [Tooltip("Звук попадания в преграду или актёра.")] public AudioClip impactSound;
        [Tooltip("Громкость оружия, 0–1. Слух AI настраивается отдельно выше.")][Range(0,1)] public float volume = .7f;
        public ItemDefinition Ammo => ammo;
        public int MagazineSize => Mathf.Max(1,magazineSize);
    }
}
