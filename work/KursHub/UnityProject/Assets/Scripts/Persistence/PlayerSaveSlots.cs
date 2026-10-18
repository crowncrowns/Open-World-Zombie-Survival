using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Persistence
{
    /// <summary>Планировщик и перечень слотов поверх единственного PlayerSaveSystem. Unity API и запись синхронны на главном потоке.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerSaveSystem))]
    public sealed class PlayerSaveSlots : MonoBehaviour
    {
        [SerializeField, InspectorName("Автосохранение включено"), Tooltip("Периодически сохранять общий снимок мира в отдельный автослот. Не выполняет загрузку при запуске.")]
        private bool autoSaveEnabled=true;
        [SerializeField, Min(1), InspectorName("Интервал автосохранения, с"), Tooltip("Интервал активного игрового времени, с. По умолчанию 300 с (5 минут). Пауза Time.timeScale=0 и смерть останавливают отсчёт; рюкзак сам по себе не останавливает.")]
        private float intervalSeconds=300;
        [SerializeField, Range(1,10), InspectorName("Количество автослотов"), Tooltip("Количество циклических автослотов, шт. Сначала заполняются пустые, затем заменяется самый старый. Ручные/быстрый файлы не затрагиваются; уменьшение не удаляет старые файлы.")]
        private int autoSlotCount=3;
        [SerializeField, Range(1,10), InspectorName("Количество ручных слотов"), Tooltip("Количество ручных слотов в меню F6, шт. Игрок сам выбирает файл для сохранения; они не входят в ротацию автосохранений.")]
        private int manualSlotCount=3;
        [SerializeField, Min(1), InspectorName("Повтор после ошибки, с"), Tooltip("Минимальная пауза перед повтором неудачной записи/проверки мира, с активной игры. Ошибки не вызывают файловую запись каждый кадр.")]
        private float retrySeconds=30;
        private PlayerSaveSystem saves;
        private Health health;
        private float elapsed,retryLeft;
        public int AutoSlotCount=>Mathf.Clamp(autoSlotCount,1,10);
        public int ManualSlotCount=>Mathf.Clamp(manualSlotCount,1,10);
        public bool Pending { get; private set; }
        public string AutoStatus { get; private set; }="";
        public float ElapsedSeconds=>elapsed;
        public float IntervalSeconds=>Mathf.Max(1,intervalSeconds);
        private void OnEnable()
        {
            saves=GetComponent<PlayerSaveSystem>();health=GetComponent<Health>();
            saves.SaveCompleted+=ResetTimer;saves.LoadCompleted+=ResetTimer;ResetTimer();
        }
        private void OnDisable() { if(saves!=null){saves.SaveCompleted-=ResetTimer;saves.LoadCompleted-=ResetTimer;} }
        private void ResetTimer() { elapsed=retryLeft=0;Pending=false;AutoStatus=""; }
        private void Update()
        {
            if(!autoSaveEnabled){ResetTimer();AutoStatus="Автосохранение выключено";return;}
            if(Time.timeScale<=0)return;
            if(health==null || health.IsDead){AutoStatus="Автосохранение: ожидание живого персонажа";return;}
            elapsed+=Time.deltaTime;retryLeft=Mathf.Max(0,retryLeft-Time.deltaTime);
            if(!Pending && elapsed<IntervalSeconds){AutoStatus=$"Автосохранение через {Mathf.CeilToInt(IntervalSeconds-elapsed)} с";return;}
            Pending=true;
            if(retryLeft>0)return;
            if(GetComponent<PlayerSaveMenu>()?.IsOpen==true){AutoStatus="Автосохранение ожидает закрытия меню F6";return;}
            if(!saves.CanSaveNow(out var reason)){AutoStatus="Автосохранение: "+reason;return;}
            // Выбор файла и полный снимок выполняются один раз на попытку, а не при каждом ожидании в воздухе.
            int slot=OldestAutoSlot();
            if(saves.TrySaveSlot(SaveSlotKind.Auto,slot)){AutoStatus="Автосохранение готово · "+SaveSlotInfo.Label(SaveSlotKind.Auto,slot);}
            else {retryLeft=Mathf.Max(1,retrySeconds);AutoStatus=saves.Status+" · повтор через "+Mathf.CeilToInt(retryLeft)+" с";}
        }
        private int OldestAutoSlot()
        {
            var infos=Enumerable.Range(0,AutoSlotCount).Select(i=>saves.InspectSlot(SaveSlotKind.Auto,i)).ToArray();
            var empty=infos.FirstOrDefault(s=>!s.Exists);if(empty!=null)return empty.Index;
            return infos.OrderBy(s=>s.SavedUtc).ThenBy(s=>s.Index).First().Index;
        }
        public List<SaveSlotInfo> InspectSlots()
        {
            var list=new List<SaveSlotInfo>{saves.InspectSlot(SaveSlotKind.Quick)};
            for(int i=0;i<ManualSlotCount;i++)list.Add(saves.InspectSlot(SaveSlotKind.Manual,i));
            for(int i=0;i<AutoSlotCount;i++)list.Add(saves.InspectSlot(SaveSlotKind.Auto,i));
            return list;
        }
        /// <summary>Явная команда «Продолжить». Ошибка полной валидации не запускает скрытый откат к другому файлу.</summary>
        public bool TryContinue()
        {
            var last=InspectSlots().Where(s=>s.Readable).OrderByDescending(s=>s.SavedUtc).FirstOrDefault();
            return last!=null && saves.TryLoadSlot(last.Kind,last.Index);
        }
    }
}
