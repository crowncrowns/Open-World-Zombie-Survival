using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombieSurvival.Combat;
using ZombieSurvival.Economy;
using ZombieSurvival.Interaction;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;

namespace ZombieSurvival.Persistence
{
    /// <summary>Одна область сохранения текущей сцены. Сценовые ссылки сохраняются при Load;
    /// runtime-объекты создаются только из явного каталога и имеют собственный постоянный ID.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldSaveRegistry : MonoBehaviour
    {
        [SerializeField, InspectorName("ID области мира"), Tooltip("Постоянный ID сохраняемой области. Пока область совпадает с одной сценой; другой ID требует миграции, не частичной загрузки.")]
        private string scopeId = "character-test";
        [SerializeField, InspectorName("Сценовые объекты"), Tooltip("Полный список корней WorldSaveObject этой сцены, включая неактивные. Сценовые объекты удалять через RemoveFromWorld, чтобы сохранились внешние ссылки.")]
        private WorldSaveObject[] sceneObjects = Array.Empty<WorldSaveObject>();
        [SerializeField, InspectorName("Каталог создаваемых объектов"), Tooltip("Разрешённые WorldPrefabDefinition для runtime-создания. В файле хранится ID типа, не ссылка на Unity-объект.")]
        private WorldPrefabDefinition[] prefabCatalog = Array.Empty<WorldPrefabDefinition>();
        private readonly Dictionary<string, WorldSaveObject> spawned = new Dictionary<string, WorldSaveObject>(StringComparer.Ordinal);
        public string ScopeId => scopeId;
        internal IReadOnlyDictionary<string, WorldSaveObject> Spawned => spawned;
        internal WorldSaveObject[] Authored => sceneObjects;

        public WorldSaveObject Spawn(WorldPrefabDefinition type, Vector3 position, Quaternion rotation)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Создание сохраняемых объектов работает в Play.");
            if (!Catalog().TryGetValue(type != null ? type.Id : "", out var allowed) || allowed != type)
                throw new InvalidOperationException("Тип не зарегистрирован в каталоге мира.");
            var staging = new GameObject("World spawn staging"); staging.SetActive(false);
            WorldSaveObject creating=null;
            try
            {
                var go = Instantiate(type.Prefab, staging.transform); go.transform.SetPositionAndRotation(position, rotation);
                var root = go.GetComponent<WorldSaveObject>(); root.BindSpawned(type, "runtime-" + Guid.NewGuid().ToString("N"));
                creating=root;
                BindChildIdentities(root); WorldRestorePlan.ValidateCaptured(root, CaptureObject(root)); spawned.Add(root.Id, root);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(staging, gameObject.scene);
                go.transform.SetParent(null, true);
                var brain=root.GetComponent<ZombieSurvival.AI.EnemyBrain>();if(brain!=null)brain.RestoreAfterWorldLoad(position,brain.enabled);
                return root;
            }
            catch { if(creating!=null){spawned.Remove(creating.Id);creating.Retire();Destroy(creating.gameObject);}throw; }
            finally { Destroy(staging); }
        }
        internal static void BindChildIdentities(WorldSaveObject root)
        {
            foreach(var storage in root.GetComponentsInChildren<ItemStorage>(true))
                if(storage.GetComponent<SaveIdentity>()==null)storage.gameObject.AddComponent<SaveIdentity>();
            var ids = root.GetComponentsInChildren<SaveIdentity>(true); int index = 0;
            foreach (var id in ids) if (id.gameObject != root.gameObject) id.AssignRuntime(root.Id + "/child-" + index++);
        }
        internal Dictionary<string, WorldPrefabDefinition> Catalog()
        {
            var result = new Dictionary<string, WorldPrefabDefinition>(StringComparer.Ordinal);
            foreach (var type in prefabCatalog ?? Array.Empty<WorldPrefabDefinition>())
            {
                if (type == null || string.IsNullOrWhiteSpace(type.Id) || type.Id.Length > 128 || type.Revision<1 || type.Prefab == null || !result.TryAdd(type.Id, type))
                    throw new InvalidOperationException("В каталоге мира пустой/повторный ID или префаб.");
                var root = type.Prefab.GetComponent<WorldSaveObject>();
                if (root == null || type.Prefab.GetComponentInChildren<FirstPersonController>(true) != null || type.Prefab.GetComponentsInChildren<WorldSaveObject>(true).Length != 1)
                    throw new InvalidOperationException("Префабу нужен один WorldSaveObject на корне без игрока: " + type.Id);
            }
            return result;
        }
        internal Dictionary<string, WorldSaveObject> Current()
        {
            if (string.IsNullOrWhiteSpace(scopeId)) throw new InvalidOperationException("Не задан ID области мира.");
            var result = new Dictionary<string, WorldSaveObject>(StringComparer.Ordinal);
            foreach (var root in sceneObjects ?? Array.Empty<WorldSaveObject>())
            {
                if (root == null) throw new InvalidOperationException("Сценовый объект уничтожен через Destroy. Используйте RemoveFromWorld или миграцию сцены.");
                Add(root, result);
            }
            foreach (var root in spawned.Values) if (root != null && !root.IsRetired) Add(root, result);
            foreach (var root in FindObjectsByType<WorldSaveObject>(FindObjectsInactive.Include).Where(r => r.gameObject.scene == gameObject.scene && !r.IsRetired))
                if (!result.TryGetValue(root.Id ?? "", out var known) || known != root) throw new InvalidOperationException("Объект не зарегистрирован в области мира: " + root.name);
            return result;
        }
        private void Add(WorldSaveObject root, Dictionary<string, WorldSaveObject> result)
        {
            if (root.gameObject.scene != gameObject.scene || string.IsNullOrWhiteSpace(root.Id) || root.Id.Length > 256 || !result.TryAdd(root.Id, root)
                || root.transform.parent != null && root.transform.parent.GetComponentInParent<WorldSaveObject>(true) != null)
                throw new InvalidOperationException("Неверный/повторный ID, другая сцена или вложенный WorldSaveObject: " + root.name);
            var keys = new HashSet<string>();
            foreach (var adapter in root.Adapters)
                if (string.IsNullOrWhiteSpace(adapter.Key) || !keys.Add(adapter.Key)) throw new InvalidOperationException("Повторный ключ адаптера: " + root.name);
        }
        public void ValidateCoverage()
        {
            Catalog(); var current = Current();
            // Пока формат обслуживает одну область. Несогласованный откат после межсценовой передачи создаёт дюп.
            foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (component != null && (component is Wallet || component is ItemStorage) && component.gameObject.scene != gameObject.scene
                    && component.gameObject.scene.isLoaded && !component.gameObject.scene.name.StartsWith("Save staging ", StringComparison.Ordinal)
                    && !component.gameObject.scene.name.StartsWith("Save collision ", StringComparison.Ordinal))
                    throw new InvalidOperationException("Загружена другая область с деньгами/хранилищем. Для межсценовых операций нужен общий координатор областей.");
            foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(c => c != null && c.gameObject.scene == gameObject.scene))
            {
                var retired = component.GetComponentInParent<WorldSaveObject>(true); if (retired != null && retired.IsRetired) continue;
                if(component is WorldItem&&retired!=null)throw new InvalidOperationException("WorldItem уже сохраняется набором добычи; нельзя одновременно помещать его под WorldSaveObject.");
                bool actor = component is Health && component.GetComponent<FirstPersonController>() == null;
                string key = component is Wallet ? "wallet" : component is ActorIdentity ? "identity" : component is DoorInteractable ? "door" : component is ToggleInteractable ? "toggle" : actor ? "actor" : null;
                if (key == null && !(component is ItemStorage)) continue;
                var root = key == null ? component.GetComponent<WorldSaveObject>() ?? component.GetComponentInParent<WorldSaveObject>(true) : component.GetComponent<WorldSaveObject>();
                if (root == null || !current.ContainsKey(root.Id) || key != null && !root.Adapters.Any(a => a.Key == key))
                    throw new InvalidOperationException(component.name + ": добавьте корневой WorldSaveObject и адаптер " + (key ?? "хранилища") + " в реестр.");
            }
            foreach(var body in FindObjectsByType<Rigidbody>(FindObjectsInactive.Include).Where(b=>b.gameObject.scene==gameObject.scene))
            {
                var root=body.GetComponent<WorldSaveObject>();var parent=body.GetComponentInParent<WorldSaveObject>(true);
                if(parent!=null&&parent.IsRetired)continue;
                if(body.GetComponentInParent<WorldItem>(true)!=null)continue;
                // Неподвижный служебный Rigidbody триггера зоны не является динамическим предметом.
                if(root==null&&body.isKinematic&&body.GetComponentsInChildren<Collider>(true).All(c=>c.isTrigger))continue;
                if(root==null||!root.Adapters.Any(a=>a.Key=="rigidbody"))throw new InvalidOperationException(body.name+": физическому корню нужен WorldRigidbodySaveAdapter. Составной ragdoll требует отдельного адаптера.");
            }
        }
        internal static SavedWorldObject CaptureObject(WorldSaveObject root)
        {
            return new SavedWorldObject { id = root.Id, spawned = root.IsSpawned, prefabId = root.Definition != null ? root.Definition.Id : "", exists = !root.IsRemoved,
                prefabRevision = root.Definition != null ? root.Definition.Revision : 0,
                active = root.gameObject.activeSelf, position = root.transform.position, rotation = root.transform.rotation, scale = root.transform.lossyScale,
                colliders = root.GetComponent<FirstPersonController>() != null ? Array.Empty<bool>() : root.GetComponentsInChildren<Collider>(true).Select(c => c.enabled).ToArray(),
                blocks = root.Adapters.Select(a => new WorldStateBlock { key = a.Key, version = a.Version, json = a.Capture() }).ToArray() };
        }
        public WorldSceneState CaptureState()
        {
            ValidateCoverage();
            var objects = Current().Values.Where(r => !r.IsSpawned || !r.IsRemoved).OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
            if(objects.Length>4096)throw new InvalidOperationException("Слишком много объектов для текущей версии снимка области.");
            var rows = objects.Select(CaptureObject).ToArray();
            for (int i = 0; i < objects.Length; i++) WorldRestorePlan.ValidateCaptured(objects[i], rows[i]);
            return new WorldSceneState { scope = scopeId, objects = rows };
        }
        internal void ReplaceSpawned(IEnumerable<WorldSaveObject> roots)
        { spawned.Clear(); foreach (var root in roots) spawned.Add(root.Id, root); }
        public WorldRestorePlan PrepareRestore(WorldSceneState state) => new WorldRestorePlan(this, state);
    }
}
