using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Player;
using ZombieSurvival.Inventory;

namespace ZombieSurvival.Persistence
{
    /// <summary>Выбор файлов, не второй загрузчик. Мир не ставится на паузу; ввод игрока блокируется на время окна.</summary>
    [DefaultExecutionOrder(45)]
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerSaveSlots),typeof(FirstPersonController))]
    public sealed class PlayerSaveMenu : MonoBehaviour
    {
        [SerializeField, InspectorName("Меню сохранений"), Tooltip("Клавиша открытия/закрытия меню файлов. Отдельное Input Action работает также после смерти и при выключенной карте движения.")]
        private InputAction toggleInput=new InputAction("SaveMenu",InputActionType.Button,"<Keyboard>/f6");
        [SerializeField, InspectorName("Закрыть меню"), Tooltip("Клавиша закрытия меню сохранений.")]
        private InputAction closeInput=new InputAction("CloseSaveMenu",InputActionType.Button,"<Keyboard>/escape");
        [SerializeField, InspectorName("Требовать фокус окна"), Tooltip("Закрывать окно при потере фокуса приложения. Отключать только при автоматических проверках.")]
        private bool requireApplicationFocus=true;
        private InputAction toggle,close;
        private FirstPersonController motor;
        private PlayerSaveSystem saves;
        private PlayerSaveSlots slots;
        private bool restoreInput;
        private List<SaveSlotInfo> entries=new List<SaveSlotInfo>();
        private readonly Dictionary<string,SaveSlotInfo> backups=new Dictionary<string,SaveSlotInfo>();
        private Vector2 scroll;
        private string feedback="";
        public bool IsOpen { get; private set; }
        private void Awake(){motor=GetComponent<FirstPersonController>();saves=GetComponent<PlayerSaveSystem>();slots=GetComponent<PlayerSaveSlots>();toggle=toggleInput.Clone();close=closeInput.Clone();}
        private void OnEnable(){toggle?.Enable();close?.Enable();}
        private void OnDisable(){Close();toggle?.Disable();close?.Disable();}
        private void OnDestroy(){toggle?.Dispose();close?.Dispose();}
        private void Update()
        {
            if(IsOpen && requireApplicationFocus && !Application.isFocused){Close();return;}
            if(IsOpen && close.WasPressedThisFrame()){Close();return;}
            if(toggle.WasPressedThisFrame()){if(IsOpen)Close();else Open();}
        }
        public bool Open()
        {
            if(IsOpen || GetComponent<PlayerInventory>()?.IsOpen==true || GetComponent<PlayerProgressionMenu>()?.IsOpen==true || motor.Parkour!=null&&motor.Parkour.OwnsMovement)return false;
            restoreInput=motor.GameplayInputEnabled;motor.SetGameplayInputEnabled(false);IsOpen=true;feedback="";Refresh();return true;
        }
        public void Close(){if(!IsOpen)return;IsOpen=false;if(motor!=null && restoreInput)motor.SetGameplayInputEnabled(true);}
        public void Refresh()
        {
            entries=slots.InspectSlots();backups.Clear();
            foreach(var entry in entries)backups[entry.Title]=saves.InspectSlot(entry.Kind,entry.Index,true);
        }
        private void OnGUI()
        {
            if(!IsOpen)return;
            float scale=Mathf.Max(.25f,Mathf.Min(Screen.width/1000f,Screen.height/700f));var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;GUI.depth=-100;
            GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);float width=Screen.width/scale,height=Screen.height/scale;
            GUI.color=new Color(.025f,.035f,.045f,.97f);GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture);GUI.color=color;
            GUILayout.BeginArea(new Rect((width-920)/2,20,920,height-40),GUI.skin.box);
            GUILayout.BeginHorizontal();GUILayout.Label("СОХРАНЕНИЯ МИРА");if(GUILayout.Button("Закрыть [F6 / Esc]",GUILayout.Width(180)))Close();GUILayout.EndHorizontal();
            GUILayout.Label("Мир продолжает жить. Сохраняйтесь на земле вне атаки, перезарядки и паркура.");
            GUILayout.BeginHorizontal();GUI.enabled=entries.Any(s=>s.Readable);
            if(GUILayout.Button("Продолжить последнее")){bool ok=slots.TryContinue();feedback=ok?saves.Status:"Не удалось продолжить: "+saves.Status;if(!ok)Refresh();}
            GUI.enabled=true;if(GUILayout.Button("Обновить список"))Refresh();GUILayout.EndHorizontal();
            scroll=GUILayout.BeginScrollView(scroll);
            foreach(var entry in entries)
            {
                GUILayout.BeginVertical(GUI.skin.box);GUILayout.Label(entry.Title);
                GUILayout.Label(!entry.Exists?"Пусто":entry.Readable?entry.SavedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss")+" · "+System.IO.Path.GetFileNameWithoutExtension(entry.Scene):entry.Error);
                GUILayout.BeginHorizontal();
                GUI.enabled=entry.Kind!=SaveSlotKind.Auto && saves.CanSaveNow(out _);
                if(GUILayout.Button(entry.Exists?"Перезаписать этот слот":"Сохранить")){saves.TrySaveSlot(entry.Kind,entry.Index);feedback=saves.Status;Refresh();}
                GUI.enabled=entry.Readable;
                if(GUILayout.Button("Загрузить")){saves.TryLoadSlot(entry.Kind,entry.Index);feedback=saves.Status;Refresh();}
                var backup=backups[entry.Title];GUI.enabled=backup.Readable;
                if(GUILayout.Button("Загрузить резерв .bak")){saves.TryLoadSlot(entry.Kind,entry.Index,true);feedback=saves.Status;Refresh();}
                GUI.enabled=true;GUILayout.EndHorizontal();
                if(backup.Readable)GUILayout.Label("Резерв: "+backup.SavedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"));
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();GUILayout.Label(slots.AutoStatus);GUILayout.Label(feedback);GUILayout.EndArea();GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;
        }
    }
}
