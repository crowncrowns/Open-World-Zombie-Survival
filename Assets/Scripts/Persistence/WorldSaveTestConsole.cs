using System.Linq;
using UnityEngine;
using ZombieSurvival.Interaction;
using ZombieSurvival.Inventory;

namespace ZombieSurvival.Persistence
{
    public enum WorldTestAction
    {
        [InspectorName("Создать контейнер с припасами")] SpawnContainer,
        [InspectorName("Создать зомби в восточной зоне")] SpawnActor,
        [InspectorName("Удалить созданные контейнеры")] RemoveContainers
    }
    /// <summary>Авторская тестовая станция CM-04; не система выдачи добычи обычного уровня.</summary>
    public sealed class WorldSaveTestConsole : Interactable
    {
        [SerializeField, InspectorName("Действие теста"), Tooltip("Операция одной команды E: создать объект через реестр либо удалить созданные контейнеры.")]
        private WorldTestAction action;
        [SerializeField, InspectorName("Реестр мира"), Tooltip("Единый реестр текущей тестовой сцены.")]
        private WorldSaveRegistry registry;
        [SerializeField, InspectorName("Тип создаваемого объекта"), Tooltip("Зарегистрированный тип контейнера/актёра. При удалении определяет, какие runtime-контейнеры убрать.")]
        private WorldPrefabDefinition definition;
        [SerializeField, InspectorName("Точка создания, м"), Tooltip("Мировая позиция создания. Для зомби нужна точка на запечённом NavMesh.")]
        private Vector3 spawnPoint;
        [SerializeField, InspectorName("Тестовые припасы"), Tooltip("Тип вещей в новом контейнере. Не создаются при загрузке или повторном включении компонента.")]
        private ItemDefinition supplies;
        [SerializeField, Min(1), InspectorName("Количество припасов"), Tooltip("Количество новых вещей за успешную команду тестового создания, шт.")]
        private int quantity = 3;
        public override string GetPrompt(PlayerInteractor actor) => action == WorldTestAction.RemoveContainers ? "Тест: удалить созданные контейнеры" : action == WorldTestAction.SpawnActor ? "Тест: создать зомби в восточной зоне" : "Тест: создать контейнер с припасами";
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            if (registry == null || definition == null) return false;
            if (action == WorldTestAction.RemoveContainers)
            {
                var roots = registry.Current().Values.Where(r => r.IsSpawned && r.Definition == definition).ToArray();
                foreach (var root in roots) root.RemoveFromWorld(); actor.ShowFeedback("Созданные контейнеры удалены. F9 возвращает сохранённый мир.");
            }
            else
            {
                if (registry.Current().Values.Any(r => r.IsSpawned && !r.IsRemoved && r.Definition == definition))
                { actor.ShowFeedback("Объект этого теста уже существует; сначала удалите его или загрузите снимок."); return false; }
                var root = registry.Spawn(definition, spawnPoint, Quaternion.identity);
                if (action == WorldTestAction.SpawnContainer && supplies != null) root.GetComponent<ItemStorage>().TryAddAll(supplies, quantity);
                actor.ShowFeedback(action == WorldTestAction.SpawnActor ? "Зомби создан: восточная зона, X56 / Z−24." : "Контейнер создан рядом: X16 / Z−27. E — открыть; F5/F9 — проверить мир.");
            }
            return true;
        }
    }
}
