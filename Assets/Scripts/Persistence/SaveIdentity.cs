using UnityEngine;

namespace ZombieSurvival.Persistence
{
    /// <summary>Стабильный ID контейнера сцены. Не генерировать заново при загрузке или открытии UI.</summary>
    [DisallowMultipleComponent]
    public sealed class SaveIdentity : MonoBehaviour
    {
        [SerializeField, Tooltip("Уникальный постоянный ID хранилища этой сцены. При копировании ящика назначьте новый ID; player зарезервирован для игрока.")]
        private string persistentId;
        public string Id => persistentId;
    }
}
