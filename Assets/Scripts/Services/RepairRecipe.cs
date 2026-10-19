using UnityEngine;
using ZombieSurvival.Inventory;
namespace ZombieSurvival.Services
{
    [CreateAssetMenu(menuName="Zombie Survival/Рецепт ремонта",fileName="RepairRecipe")]
    public sealed class RepairRecipe:ScriptableObject
    {
        [System.Serializable] public sealed class MaterialRow
        {
            [InspectorName("Материал"),Tooltip("Тип материала из рюкзака игрока. Не предмет с прочностью и не деньги.")] public ItemDefinition material;
            [InspectorName("Расход полного ремонта"),Tooltip("Количество при ремонте с нуля до максимума. При частичном повреждении ceil(B × повреждение / максимум)."),Min(1)] public int fullRepairAmount=4;
        }
        [InspectorName("ID профиля"),Tooltip("Стабильный ключ ItemDefinition.RepairProfileId, например weapon/textile/armor.")] public string profileId;
        [InspectorName("Материалы"),Tooltip("Полный непустой рецепт. Каждая строка имеет отдельный тип и положительный расход.")] public MaterialRow[] materials=System.Array.Empty<MaterialRow>();
    }
}
