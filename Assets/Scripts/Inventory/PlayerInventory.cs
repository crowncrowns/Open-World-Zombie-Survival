using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Player;

namespace ZombieSurvival.Inventory
{
    /// <summary>Прототип окна добычи. Время мира не останавливается; UI закрывается при смерти,
    /// удалении ящика или удалении от него. Данные остаются в независимом ItemStorage.</summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent, RequireComponent(typeof(ItemStorage), typeof(FirstPersonController))]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [Tooltip("Открыть/закрыть рюкзак. Отдельное действие работает и при выключенной карте движения.")]
        [SerializeField] private InputAction toggleInput = new InputAction("Inventory", InputActionType.Button, "<Keyboard>/tab");
        [Tooltip("Закрыть окно инвентаря.")]
        [SerializeField] private InputAction closeInput = new InputAction("CloseInventory", InputActionType.Button, "<Keyboard>/escape");
        [Tooltip("Максимальное расстояние до открытого ящика, м. За пределами окно закрывается.")]
        [SerializeField, Min(1f)] private float containerDistance = 3.5f;
        [Tooltip("Дистанция выбрасывания перед игроком, м. Проверяется свободное место и земля.")]
        [SerializeField, Range(.6f, 2f)] private float dropDistance = 1.1f;
        [Tooltip("Слои земли и препятствий при выбрасывании предмета.")]
        [SerializeField] private LayerMask dropMask = ~0;
        [Tooltip("Закрывать окно при потере фокуса приложения. Отключается только для автоматических тестов.")]
        [SerializeField] private bool requireApplicationFocus = true;
        private InputAction toggle, close;
        private FirstPersonController motor;
        private ItemStorage storage, container;
        private bool hadContainer;
        private int selected = -1;
        private bool selectedContainer;
        [SerializeField, InspectorName("Ожидание в рюкзаке"), Tooltip("Клип idle для независимого предпросмотра Humanoid-модели, без root motion и игровых событий.")]
        private AnimationClip inventoryIdle;
        private ZombieSurvival.Presentation.InventoryPresentation presentation;
        public AnimationClip InventoryIdle => inventoryIdle;
        internal ItemStorage Container => hadContainer ? container : null;
        internal int SelectedIndex => selected;
        internal bool SelectedContainer => selectedContainer;
        internal ItemStack SelectedStack => (selectedContainer ? Container : Storage)?.GetSlot(selected);
        internal string Feedback => feedback;
        private string feedback = "";
        public bool IsOpen { get; private set; }
        public ItemStorage Storage => storage != null ? storage : GetComponent<ItemStorage>();
        private void Awake() { motor = GetComponent<FirstPersonController>(); storage = GetComponent<ItemStorage>(); toggle = toggleInput.Clone(); close = closeInput.Clone(); }
        private void OnEnable() { toggle?.Enable(); close?.Enable(); }
        private void OnDisable() { Close(); toggle?.Disable(); close?.Disable(); }
        private void OnDestroy() { presentation?.Dispose(); toggle?.Dispose(); close?.Dispose(); }
        private void LateUpdate() { if(IsOpen) { if(presentation==null) presentation=new ZombieSurvival.Presentation.InventoryPresentation(this); presentation.Tick(); } }
        private void Update()
        {
            if (IsOpen && (!motor.IsAlive || requireApplicationFocus && !Application.isFocused || hadContainer && (container == null || !container.isActiveAndEnabled
                || Vector3.Distance(transform.position, container.transform.position) > containerDistance))) { Close(); return; }
            if (IsOpen && close.WasPressedThisFrame()) { Close(); return; }
            if(IsOpen)presentation?.ProcessPointer();
            if (!toggle.WasPressedThisFrame()) return;
            if (IsOpen) Close(); else if (motor.AcceptingGameplayInput) Open(null);
        }
        public bool Open(ItemStorage other)
        {
            if (IsOpen || !motor.AcceptingGameplayInput || motor.Parkour != null && motor.Parkour.OwnsMovement
                || other == storage || other != null && Vector3.Distance(transform.position, other.transform.position) > containerDistance) return false;
            container = other; hadContainer = other != null; selected = -1; feedback = "";
            motor.SetGameplayInputLock(this, true); IsOpen = true; return true;
        }
        public void Close()
        {
            if (!IsOpen) return;
            presentation?.Dispose(); presentation=null;
            IsOpen = false; container = null; hadContainer = false;
            if (motor != null) motor.SetGameplayInputLock(this, false);
        }
        public bool Drop(int index, int amount)
        {
            if (motor == null || !motor.IsAlive) return false;
            var hotbar = GetComponent<PlayerHotbar>();
            string[] before = hotbar != null ? hotbar.CaptureAssignments() : null;
            bool dropped = WorldItemDropService.TryDrop(transform, motor.FeetPosition, Storage, index, amount, dropDistance, dropMask);
            // Changed может временно очистить назначение целой стопки до отката неудачного Initialize.
            if (!dropped && hotbar != null) hotbar.RestoreAssignments(before, hotbar.SelectedIndex);
            return dropped;
        }
        private void OnGUI()
        {
            if (!IsOpen) return;
            if(presentation==null) presentation=new ZombieSurvival.Presentation.InventoryPresentation(this);
            presentation.Draw();
        }
        internal void SelectSlot(int index, bool fromContainer)
        {
            var target=fromContainer?Container:Storage;
            if(target==null || index<0 || index>=target.SlotCount)return;
            selected=index; selectedContainer=fromContainer; feedback="";
        }
        internal void TransferSelected(int amount)
        {
            var source=selectedContainer?Container:Storage; var destination=selectedContainer?Storage:Container;
            if(!IsOpen || source==null || destination==null)return;
            int moved=source.TransferTo(destination,selected,amount);
            feedback=moved>0?$"Перенесено: {moved}":"Нет места — предметы остались в исходном слоте";
        }
        internal void ConsumeSelected()
        {
            if(!IsOpen || selectedContainer)return;
            var needs=GetComponent<PlayerNeeds>();
            feedback=needs!=null && needs.TryConsume(selected)?"Предмет употреблён":"Показатели полные или предмет недоступен";
        }
        internal void EquipSelected()
        {
            if(!IsOpen || selectedContainer)return;
            if(SelectedStack!=null&&!SelectedStack.IsEmpty && SelectedStack.Definition.WearSlot!=WearSlot.None)
            {var equipment=GetComponent<PlayerEquipment>();if(equipment!=null){equipment.TryEquip(selected);feedback=equipment.Status;}return;}
            var weapons=GetComponent<ZombieSurvival.Weapons.PlayerWeapons>();
            feedback=weapons!=null && weapons.Equip(selected)?"Оружие выбрано":"Нельзя экипировать этот предмет";
        }
        internal void UseSelected()
        {
            var item=SelectedStack;if(!IsOpen || selectedContainer || item==null || item.IsEmpty)return;
            if(item.Definition.WearSlot!=WearSlot.None || item.Definition.Weapon!=null){EquipSelected();return;}
            var needs=GetComponent<PlayerNeeds>();if(needs!=null&&needs.CanConsume(item.Definition)){ConsumeSelected();return;}
            if(item.Definition.Flashlight!=null)
            {
                var hotbar=GetComponent<PlayerHotbar>();if(hotbar==null)return;
                int target=-1;for(int i=0;i<hotbar.SlotCount;i++)if(hotbar.GetAssignedItem(i)?.InstanceId==item.InstanceId){target=i;break;}
                if(target<0)for(int i=0;i<hotbar.SlotCount;i++)if(hotbar.GetAssignedItem(i)==null){target=i;break;}
                if(target<0 || !hotbar.Assign(target,selected)){feedback="Освободите быстрый слот для фонарика";return;}
                hotbar.Select(target);Close();hotbar.TryUsePrimary();return;
            }
            feedback="У предмета пока нет действия";
        }
        internal void SplitSelected(int amount)
        {
            var source=selectedContainer?Container:Storage;
            if(!IsOpen || source==null)return;
            if(source.TrySplit(selected,amount,out int index)){selected=index;feedback="Стопка разделена";}else feedback="Нужен свободный слот и делимая стопка";
        }
        internal bool MoveItem(ItemStorage source,int sourceIndex,string instanceId,ItemStorage destination,int targetIndex)
        {
            if(!IsOpen || source==null || destination==null || source!=Storage&&source!=Container || destination!=Storage&&destination!=Container)return false;
            var item=source.GetSlot(sourceIndex);if(item==null || item.IsEmpty || item.InstanceId!=instanceId)return false;
            bool moved=source.MoveToSlot(destination,sourceIndex,targetIndex);feedback=moved?"Предмет перемещён":"Нельзя переместить предмет в этот слот";
            if(moved){selected=targetIndex;selectedContainer=destination==Container;}return moved;
        }
        internal void Report(string message)=>feedback=message;
        internal void DropSelected(bool all)
        {
            if(!IsOpen || selectedContainer)return;
            var stack=SelectedStack; if(stack==null || stack.IsEmpty)return;
            feedback=Drop(selected,all?stack.Quantity:1)?"Предмет на земле":"Нет свободного места перед игроком";
        }
        internal void AssignSelected(int slot)
        {
            if(!IsOpen || selectedContainer)return;
            var hotbar=GetComponent<PlayerHotbar>();
            feedback=hotbar!=null && hotbar.Assign(slot,selected)?"Назначен быстрый слот "+(slot+1):"Предмет нельзя назначить";
        }
    }
}
