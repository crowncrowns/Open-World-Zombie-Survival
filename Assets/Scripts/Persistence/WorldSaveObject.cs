using System;
using UnityEngine;

namespace ZombieSurvival.Persistence
{
    [DisallowMultipleComponent, RequireComponent(typeof(SaveIdentity))]
    public sealed class WorldSaveObject : MonoBehaviour
    {
        [SerializeField, InspectorName("Сохранять положение"), Tooltip("Восстанавливать мировой Transform корня. Для игрока позу всегда восстанавливает FirstPersonController из его блока.")]
        private bool saveTransform = true;
        private bool removed, spawned, retired;
        private WorldPrefabDefinition definition;
        public string Id => GetComponent<SaveIdentity>().Id;
        public bool IsRemoved => removed;
        public bool IsSpawned => spawned;
        internal bool IsRetired => retired;
        public WorldPrefabDefinition Definition => definition;
        internal bool SavesTransform => saveTransform && GetComponent<ZombieSurvival.Player.FirstPersonController>() == null;
        public WorldSaveAdapter[] Adapters => GetComponents<WorldSaveAdapter>();

        /// <summary>Сценовый объект оставляем как tombstone, чтобы старое сохранение могло вернуть его
        /// без потери внешних ссылок и overrides. Для runtime-объектов допустим также обычный Destroy.</summary>
        public void RemoveFromWorld()
        {
            if (GetComponent<ZombieSurvival.Player.FirstPersonController>() != null) throw new InvalidOperationException("Игрок не удаляется через WorldSaveObject.");
            removed = true; gameObject.SetActive(false);
        }
        internal void BindSpawned(WorldPrefabDefinition type, string id)
        { definition = type; spawned = true; GetComponent<SaveIdentity>().AssignRuntime(id); }
        internal void SetRemovedSilently(bool value) => removed = value;
        internal void Retire() { retired = true; gameObject.SetActive(false); }
    }
}
