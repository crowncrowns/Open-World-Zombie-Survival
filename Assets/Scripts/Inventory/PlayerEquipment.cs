using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Player;
namespace ZombieSurvival.Inventory
{
    /// <summary>Экипировка владеет оригиналами через отдельный зарегистрированный ItemStorage. Броня пока без расчёта защиты/новых визуальных мешей.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ItemStorage))]
    public sealed class PlayerEquipment : MonoBehaviour
    {
        [SerializeField, Range(1,64), InspectorName("Базовые карманы, слотов"), Tooltip("Слоты без надетого рюкзака, шт. По умолчанию 5. Смена в Edit не удаляет прежние занятые вещи; при несовместимости нужен перенос/новый снимок.")]
        private int baseSlots=5;
        [SerializeField, InspectorName("Хранилище экипировки"), Tooltip("Дочерний ItemStorage из пяти слотов: туловище, голова, ноги, плечи, рюкзак. Имеет собственный SaveIdentity и EquipmentStorage, входит в общий снимок мира.")]
        private ItemStorage equipment;
        public ItemStorage Storage=>equipment;
        public int BaseSlots=>Mathf.Clamp(baseSlots,1,64);
        public int Capacity=>BaseSlots+(Get(WearSlot.Backpack)?.Definition.BackpackSlots??0);
        public static bool IsBusy { get; private set; }
        public string Status { get; private set; }="";
        public ItemStack Get(WearSlot slot)
        {
            var item=equipment!=null && slot!=WearSlot.None ? equipment.GetSlot((int)slot-1) : null;
            return item!=null&&!item.IsEmpty?item:null;
        }
        private bool Available=>isActiveAndEnabled && equipment!=null && equipment.isActiveAndEnabled
            && GetComponent<FirstPersonController>()?.IsAlive==true && !IsBusy;
        public bool TryEquip(int inventoryIndex)
        {
            var bag=GetComponent<ItemStorage>();var item=bag.GetSlot(inventoryIndex);
            if(!Available || item==null || item.IsEmpty || item.Quantity!=1 || item.Definition.WearSlot==WearSlot.None){Status="Этот предмет нельзя надеть";return false;}
            var next=bag.EquipmentLayout();var gear=equipment.EquipmentLayout();int index=(int)item.Definition.WearSlot-1;
            next[inventoryIndex]=gear[index];gear[index]=item;return Commit(bag,next,gear);
        }
        public bool TryUnequip(WearSlot slot,int targetIndex=-1)
        {
            if(!Available || !Enum.IsDefined(typeof(WearSlot),slot) || slot==WearSlot.None)return false;
            var bag=GetComponent<ItemStorage>();var next=bag.EquipmentLayout();var gear=equipment.EquipmentLayout();var item=gear[(int)slot-1];
            if(item==null || item.IsEmpty)return false;
            int index=targetIndex;
            if(index<0)index=next.FindIndex(s=>s==null||s.IsEmpty);
            if(index<0 || index>=next.Count || next[index]!=null&&!next[index].IsEmpty){Status="Освободите место для снятого предмета";return false;}
            next[index]=item;gear[(int)slot-1]=null;return Commit(bag,next,gear);
        }
        private bool Commit(ItemStorage bag,List<ItemStack> next,List<ItemStack> gear)
        {
            int capacity=BaseSlots+(gear[4]!=null&&!gear[4].IsEmpty?gear[4].Definition.BackpackSlots:0);
            if(next.FindAll(i=>i!=null&&!i.IsEmpty).Count>capacity){Status="Недостаточно места: освободите карманы перед снятием/заменой рюкзака";return false;}
            // Сжимаем только выходящие за новую границу оригиналы в свободные карманы, не создавая копии/новые ID.
            while(next.Count<capacity)next.Add(null);
            for(int i=capacity;i<next.Count;i++)if(next[i]!=null&&!next[i].IsEmpty){int empty=next.FindIndex(0,capacity,s=>s==null||s.IsEmpty);next[empty]=next[i];next[i]=null;}
            if(next.Count>capacity)next.RemoveRange(capacity,next.Count-capacity);
            if(!bag.TryLockTrade()){Status="Инвентарь занят";return false;}
            if(!equipment.TryLockTrade()){bag.UnlockTrade();Status="Экипировка занята";return false;}
            IsBusy=true;
            try{bag.CommitEquipmentLayout(next);equipment.CommitEquipmentLayout(gear);Status="Экипировка изменена";bag.PublishTrade();equipment.PublishTrade();return true;}
            finally{IsBusy=false;equipment.UnlockTrade();bag.UnlockTrade();}
        }
        internal bool ValidateSaved(IReadOnlyList<Persistence.SavedStorage> rows,string bagId,out string reason)
        {
            reason="";var id=equipment!=null?equipment.GetComponent<Persistence.SaveIdentity>()?.Id:null;
            Persistence.SavedStorage gear=null,bag=null;
            foreach(var row in rows){if(row.id==id)gear=row;if(row.id==bagId)bag=row;}
            if(gear?.slots==null || gear.slots.Count!=5 || bag?.slots==null){reason="В снимке нет пяти слотов экипировки. Для нового состава мира сделайте новый F5.";return false;}
            var catalog=GetComponent<Persistence.PlayerSaveSystem>();int capacity=BaseSlots;
            for(int i=0;i<5;i++)if(gear.slots[i]!=null&&gear.slots[i].occupied)
            {
                var type=catalog.FindItemType(gear.slots[i].typeId);
                if(type==null || type.WearSlot!=(WearSlot)(i+1) || gear.slots[i].quantity!=1){reason="Несовместимый тип в слоте экипировки";return false;}
                if(i==4)capacity+=type.BackpackSlots;
            }
            for(int i=capacity;i<bag.slots.Count;i++)if(bag.slots[i]!=null&&bag.slots[i].occupied){reason="Содержимое не помещается в сохранённый рюкзак";return false;}
            return true;
        }
    }
}
