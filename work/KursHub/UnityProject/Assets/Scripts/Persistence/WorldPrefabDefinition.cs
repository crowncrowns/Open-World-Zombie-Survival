using UnityEngine;

namespace ZombieSurvival.Persistence
{
    /// <summary>Каталог runtime-объектов. Стабильный ID типа записывается в файл вместо ссылки на префаб.</summary>
    [CreateAssetMenu(menuName = "Zombie Survival/Сохраняемый префаб мира", fileName = "WorldPrefab")]
    public sealed class WorldPrefabDefinition : ScriptableObject
    {
        [SerializeField, InspectorName("Постоянный ID типа"), Tooltip("Уникальный ID типа создаваемого объекта. После публикации файла не менять без миграции.")]
        private string prefabId;
        [SerializeField, InspectorName("Авторский префаб"), Tooltip("Префаб с WorldSaveObject и нужными адаптерами на корне. Без игрока и вложенных WorldSaveObject.")]
        private GameObject prefab;
        [SerializeField, Min(1), InspectorName("Версия структуры префаба"), Tooltip("Увеличьте при несовместимой смене компонентов/иерархии или схемы дочерних хранилищ. Старый файл требует миграции, а не молчаливой замены типа.")]
        private int revision = 1;
        public string Id => prefabId;
        public GameObject Prefab => prefab;
        public int Revision => revision;
    }
}
