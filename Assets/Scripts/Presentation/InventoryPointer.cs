using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Inventory;

namespace ZombieSurvival.Presentation
{
    internal sealed partial class InventoryPresentation
    {
        // Один владелец ПК-курсора для этого окна. Координаты Input System — экран снизу вверх;
        // layout переводится в реальные пиксели сверху вниз, включая масштаб, viewport и scroll.
        // Не опрашивать мышь в предметах и не исполнять ту же команду повторно через IMGUI Button.
        private struct CellHit { internal Rect pixels;internal ItemStorage storage;internal int index,gear; }
        private struct ButtonHit { internal Rect pixels,key;internal bool enabled; }
        private readonly List<CellHit> pointerCells=new List<CellHit>();
        private readonly List<ButtonHit> pointerButtons=new List<ButtonHit>();
        private Matrix4x4 pointerMatrix;
        private Rect ownViewport,otherViewport,sliderPixels;
        private float ownMaxScroll,otherMaxScroll;
        private int sliderMaximum;
        private Rect? pressedButton,clickedButton;
        private bool sliding;
        private Vector2 pointerPosition;
        private void CancelPointerClick(){clickedButton=null;pressedButton=null;sliding=false;}
        private void BeginPointerLayout(Matrix4x4 matrix){pointerMatrix=matrix;pointerCells.Clear();pointerButtons.Clear();sliderPixels=default;}
        private Rect Pixels(Rect rect)
        {
            var min=pointerMatrix.MultiplyPoint3x4(rect.min);var max=pointerMatrix.MultiplyPoint3x4(rect.max);
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        private void RecordCell(Rect rect,Rect clip,ItemStorage storage,int index,int gear)
        {
            var visible=Rect.MinMaxRect(Mathf.Max(rect.xMin,clip.xMin),Mathf.Max(rect.yMin,clip.yMin),Mathf.Min(rect.xMax,clip.xMax),Mathf.Min(rect.yMax,clip.yMax));
            if(visible.width>0&&visible.height>0)pointerCells.Add(new CellHit{pixels=Pixels(visible),storage=storage,index=index,gear=gear});
        }
        private void RecordButton(Rect rect,bool enabled)=>pointerButtons.Add(new ButtonHit{pixels=Pixels(rect),key=rect,enabled=enabled});
        private void RecordScroll(Rect viewport,bool other,float maximum)
        {if(other){otherViewport=Pixels(viewport);otherMaxScroll=maximum;}else{ownViewport=Pixels(viewport);ownMaxScroll=maximum;}}
        private void RecordSlider(Rect rect,int quantity){sliderPixels=Pixels(rect);sliderMaximum=Mathf.Max(1,quantity-1);}
        private bool TakeClick(Rect key)
        {if(clickedButton.HasValue&&clickedButton.Value==key){clickedButton=null;return GUI.enabled;}return false;}
        internal void ProcessPointer()
        {
            var mouse=Mouse.current;if(mouse==null)return;
            Vector2 raw=mouse.position.ReadValue(),p=new Vector2(raw.x,Screen.height-raw.y);
            pointerPosition=p;
            float wheel=mouse.scroll.ReadValue().y;
            if(!contextOpen && Mathf.Abs(wheel)>.001f)
            {if(ownViewport.Contains(p))ownScroll.y=Mathf.Clamp(ownScroll.y-Mathf.Sign(wheel)*60,0,ownMaxScroll);else if(owner.Container!=null&&otherViewport.Contains(p))containerScroll.y=Mathf.Clamp(containerScroll.y-Mathf.Sign(wheel)*60,0,otherMaxScroll);}
            CellHit? cell=null;foreach(var hit in pointerCells)if(hit.pixels.Contains(p)){cell=hit;break;}
            if(mouse.rightButton.wasPressedThisFrame && cell.HasValue)
            {
                var hit=cell.Value;var item=hit.storage.GetSlot(hit.index);
                if(item!=null&&!item.IsEmpty)
                {
                    if(hit.gear<0)owner.SelectSlot(hit.index,hit.storage==owner.Container);
                    contextGear=hit.gear;contextId=item.InstanceId;contextScreen=p;contextOpen=true;splitAmount=Mathf.Max(1,item.Quantity/2);
                    dragId=null;dragging=false;pressedButton=null;return;
                }
            }
            if(mouse.leftButton.wasPressedThisFrame)
            {
                if(contextOpen && !Pixels(contextBounds).Contains(p)){contextOpen=false;pressedButton=null;return;}
                sliding=contextOpen&&sliderPixels.Contains(p);
                pressedButton=null;
                foreach(var hit in pointerButtons)if(hit.enabled&&hit.pixels.Contains(p)){pressedButton=hit.key;return;}
                if(!contextOpen&&cell.HasValue)
                {
                    var hit=cell.Value;var item=hit.storage.GetSlot(hit.index);
                    if(hit.gear<0)owner.SelectSlot(hit.index,hit.storage==owner.Container);
                    dragStorage=hit.storage;dragIndex=hit.index;dragGear=hit.gear;dragId=item!=null&&!item.IsEmpty?item.InstanceId:null;pressedAt=p;dragging=false;
                }
            }
            if(sliding && mouse.leftButton.isPressed)splitAmount=Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1,sliderMaximum,Mathf.InverseLerp(sliderPixels.xMin,sliderPixels.xMax,p.x))),1,sliderMaximum);
            if(dragId!=null && mouse.leftButton.isPressed && Vector2.Distance(pressedAt,p)>5)dragging=true;
            if(!mouse.leftButton.wasReleasedThisFrame)return;
            sliding=false;
            if(pressedButton.HasValue)
            {
                foreach(var hit in pointerButtons)if(hit.enabled&&hit.key==pressedButton.Value&&hit.pixels.Contains(p)){clickedButton=hit.key;break;}
                pressedButton=null;
            }
            else if(dragging&&cell.HasValue)
            {
                var hit=cell.Value;var item=dragStorage!=null?dragStorage.GetSlot(dragIndex):null;
                if(item!=null&&!item.IsEmpty&&item.InstanceId==dragId)
                {
                    var equipment=owner.GetComponent<PlayerEquipment>();
                    if(hit.gear>=0 && dragGear<0 && dragStorage==owner.Storage)
                    {
                        if(item.Definition.WearSlot==(WearSlot)(hit.gear+1)){equipment.TryEquip(dragIndex);owner.Report(equipment.Status);}else owner.Report("Этот предмет не подходит для выбранного слота");
                    }
                    else if(dragGear>=0 && hit.gear<0 && hit.storage==owner.Storage){equipment.TryUnequip((WearSlot)(dragGear+1),hit.index);owner.Report(equipment.Status);}
                    else if(dragGear<0 && hit.gear<0)owner.MoveItem(dragStorage,dragIndex,dragId,hit.storage,hit.index);
                }
            }
            dragId=null;dragStorage=null;dragGear=-1;dragging=false;
        }
    }
}
