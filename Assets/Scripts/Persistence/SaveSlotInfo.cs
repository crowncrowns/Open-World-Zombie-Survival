using System;
using UnityEngine;

namespace ZombieSurvival.Persistence
{
    public enum SaveSlotKind
    {
        [InspectorName("Быстрое")] Quick,
        [InspectorName("Ручное")] Manual,
        [InspectorName("Автоматическое")] Auto
    }
    /// <summary>Метаданные для списка. Readable — файл читается и относится к сцене, НЕ доказательство полной совместимости мира.</summary>
    public sealed class SaveSlotInfo
    {
        public SaveSlotKind Kind { get; }
        public int Index { get; }
        public bool IsBackup { get; }
        public bool Exists { get; internal set; }
        public bool Readable { get; internal set; }
        public DateTime SavedUtc { get; internal set; }
        public string Scene { get; internal set; } = "";
        public string Error { get; internal set; } = "";
        public string Title => Label(Kind, Index) + (IsBackup ? " · резерв .bak" : "");
        public SaveSlotInfo(SaveSlotKind kind,int index,bool backup) { Kind=kind;Index=index;IsBackup=backup; }
        public static string Label(SaveSlotKind kind,int index) => kind==SaveSlotKind.Quick ? "Быстрое F5/F9" : (kind==SaveSlotKind.Auto ? "Авто " : "Ручное ")+(index+1);
    }
}
