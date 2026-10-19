using UnityEngine;

namespace ZombieSurvival.Persistence
{
    /// <summary>Постоянный ID объекта/хранилища. Сценовый ID задаёт редактор; runtime-ID назначает реестр создания.</summary>
    [DisallowMultipleComponent]
    public sealed class SaveIdentity : MonoBehaviour
    {
        [SerializeField, InspectorName("Постоянный ID"), Tooltip("Уникальный ID объекта/хранилища сцены. Новым копиям в редакторе ID назначается автоматически; ID оригинала сохраняется. Старые конфликты: Tools/Zombie Survival/Save IDs. player зарезервирован; runtime-ID присваивает WorldSaveRegistry.")]
        private string persistentId;
        public string Id => persistentId;
        internal void AssignRuntime(string value) => persistentId = value;
    }
}
