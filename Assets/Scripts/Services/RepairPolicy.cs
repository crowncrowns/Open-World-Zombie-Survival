using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Inventory;
namespace ZombieSurvival.Services
{
    [CreateAssetMenu(menuName="Zombie Survival/Правила ремонта",fileName="RepairPolicy")]
    public sealed class RepairPolicy:ScriptableObject
    {
        [Serializable] public sealed class TypeOverride
        {
            [InspectorName("Тип предмета"),Tooltip("Исключение для конкретного ItemDefinition; исходный ремонтный профиль не меняется.")] public ItemDefinition item;
            [InspectorName("Рецепт"),Tooltip("Рецепт для этого типа вместо общего профиля.")] public RepairRecipe recipe;
        }
        [InspectorName("Общие рецепты"),Tooltip("Рецепты с уникальными ID профилей. Стандартно weapon/textile/armor.")] public RepairRecipe[] recipes=Array.Empty<RepairRecipe>();
        [InspectorName("Категории ремонта"),Tooltip("Явный допуск категорий. По умолчанию оружие и снаряжение; наличие прочности само по себе не разрешает ремонт.")] public ItemCategory[] acceptedCategories={ItemCategory.Weapon,ItemCategory.Equipment};
        [InspectorName("Исключения типов"),Tooltip("Необязательные рецепты конкретных типов; один тип не может встречаться дважды.")] public TypeOverride[] typeOverrides=Array.Empty<TypeOverride>();
        internal bool TryResolve(ItemDefinition item,out RepairRecipe result,out string reason)
        {
            result=null;reason="";var ids=new HashSet<string>(StringComparer.Ordinal);
            if(acceptedCategories==null||Array.IndexOf(acceptedCategories,item.Category)<0){reason="Верстак не ремонтирует эту категорию";return false;}
            foreach(var row in recipes??Array.Empty<RepairRecipe>())
                if(row==null||string.IsNullOrWhiteSpace(row.profileId)||!ids.Add(row.profileId)){reason="Некорректные или повторяющиеся профили ремонта";return false;}
            var types=new HashSet<ItemDefinition>();
            foreach(var row in typeOverrides??Array.Empty<TypeOverride>())
            {
                if(row==null||row.item==null||row.recipe==null||!types.Add(row.item)){reason="Некорректное исключение ремонтного профиля";return false;}
                if(row.item==item)result=row.recipe;
            }
            if(result!=null)return true;
            foreach(var recipe in recipes??Array.Empty<RepairRecipe>())if(recipe.profileId==item.RepairProfileId){result=recipe;return true;}
            reason="Для этого типа не назначен рецепт ремонта";return false;
        }
        // Exact serialized values, not a hash/version approximation; quotes are transient within the same process.
        internal string ConfigurationStamp()
        {
            var text=JsonUtility.ToJson(this);
            foreach(var recipe in recipes??Array.Empty<RepairRecipe>())text+="\n"+(recipe!=null?JsonUtility.ToJson(recipe):"null");
            foreach(var row in typeOverrides??Array.Empty<TypeOverride>())text+="\n"+(row?.recipe!=null?JsonUtility.ToJson(row.recipe):"null");
            return text;
        }
    }
}
