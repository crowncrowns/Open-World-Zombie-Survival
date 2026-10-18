using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieSurvival.Inventory;

namespace ZombieSurvival.Persistence
{
    /// <summary>Подготовленные цели и геометрические копии находятся в отдельной сцене без игровых кадров.
    /// Сценовые корни не заменяются: внешние UnityEvent/ссылки на них остаются действительными.</summary>
    public sealed class WorldRestorePlan : IDisposable
    {
        private sealed class Entry
        {
            public SavedWorldObject State, Before;
            public WorldSaveObject Target, Geometry;
            public bool Created;
        }
        private readonly WorldSaveRegistry registry;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<WorldSaveObject> extras = new List<WorldSaveObject>();
        private readonly Scene stagingScene;
        private readonly GameObject staging;
        private bool committed, published;
        public IEnumerable<Transform> ExistingRoots => registry.Current().Values.Select(r => r.transform);
        public Transform GeometryRoot => staging.transform;
        internal IEnumerable<WorldSaveObject> NavigationGeometry => entries.Where(e => e.Geometry != null && e.State.exists && e.State.active
            && e.Geometry.GetComponent<WorldActorSaveAdapter>()?.WantsNavigation == true).Select(e => e.Geometry);
        public IEnumerable<ItemStorage> Storages => entries.SelectMany(e => e.Target.GetComponentsInChildren<ItemStorage>(true));
        public WorldRestorePlan(WorldSaveRegistry registry, WorldSceneState state)
        {
            this.registry = registry;
            stagingScene = SceneManager.CreateScene("Save staging " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            staging = new GameObject("Inactive validation objects"); staging.SetActive(false); SceneManager.MoveGameObjectToScene(staging, stagingScene);
            try
            {
                registry.ValidateCoverage(); var current = registry.Current(); var catalog = registry.Catalog();
                if (state == null || state.version != 1 || state.scope != registry.ScopeId || state.objects == null || state.objects.Length > 4096)
                    throw new InvalidOperationException("Неизвестная область/версия или неполный снимок мира.");
                var ids = new HashSet<string>(StringComparer.Ordinal); var authored = registry.Authored.ToDictionary(r => r.Id, StringComparer.Ordinal);
                foreach (var row in state.objects)
                {
                    ValidatePose(row);
                    if (!ids.Add(row.id)) throw new InvalidOperationException("Повтор ID объекта мира.");
                    WorldSaveObject target; bool created = false;
                    if (!row.spawned)
                    {
                        if (!string.IsNullOrEmpty(row.prefabId) || row.prefabRevision!=0 || !authored.TryGetValue(row.id, out target)) throw new InvalidOperationException("Состав сценовых объектов изменился. Нужна миграция.");
                    }
                    else
                    {
                        if (!row.exists || !catalog.TryGetValue(row.prefabId ?? "", out var type) || type.Revision!=row.prefabRevision || authored.ContainsKey(row.id)) throw new InvalidOperationException("Неизвестный тип/версия создаваемого объекта мира.");
                        if (current.TryGetValue(row.id, out target))
                        { if (!target.IsSpawned || target.Definition != type) throw new InvalidOperationException("Тип runtime-объекта не совпадает."); }
                        else
                        {
                            var go = UnityEngine.Object.Instantiate(type.Prefab, staging.transform); go.SetActive(false); target = go.GetComponent<WorldSaveObject>();
                            target.BindSpawned(type, row.id); WorldSaveRegistry.BindChildIdentities(target); created = true;
                        }
                    }
                    ValidateBlocks(target, row);
                    var entry = new Entry { State = row, Target = target, Created = created, Before = created ? null : WorldSaveRegistry.CaptureObject(target) };
                    entries.Add(entry);
                    if (target.GetComponent<ZombieSurvival.Player.FirstPersonController>() == null)
                    {
                        var geometry = UnityEngine.Object.Instantiate(target.gameObject, staging.transform).GetComponent<WorldSaveObject>();
                        var crawl = geometry.GetComponent<ZombieSurvival.AI.EnemyCrawlPursuit>();
                        if(crawl!=null)crawl.CopySavedConfigurationFrom(target.GetComponent<ZombieSurvival.AI.EnemyCrawlPursuit>());
                        entry.Geometry = geometry; Apply(geometry, row); geometry.gameObject.SetActive(row.exists && row.active);
                    }
                }
                if (authored.Keys.Any(id => !ids.Contains(id))) throw new InvalidOperationException("В файле отсутствует сценовый объект; нужна миграция.");
                extras.AddRange(current.Values.Where(r => r.IsSpawned && !ids.Contains(r.Id)));
            }
            catch { Dispose(); throw; }
        }
        private static void ValidatePose(SavedWorldObject row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.id) || row.id.Length > 256 || !WorldSaveAdapter.ValidPosition(row.position) || !WorldSaveAdapter.ValidPosition(row.scale)
                || row.scale.x <= 0 || row.scale.y <= 0 || row.scale.z <= 0 || row.scale.x > 100 || row.scale.y > 100 || row.scale.z > 100 || row.blocks == null || row.blocks.Length > 64)
                throw new InvalidOperationException("Некорректный Transform или блоки объекта мира.");
            var q = row.rotation; float norm = q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w;
            if (!WorldSaveAdapter.Finite(norm) || Mathf.Abs(norm-1)>.001f || !row.exists && row.active) throw new InvalidOperationException("Некорректная ориентация/активность объекта мира.");
        }
        private static void ValidateBlocks(WorldSaveObject root, SavedWorldObject row)
        {
            bool player = root.GetComponent<ZombieSurvival.Player.FirstPersonController>() != null;
            if (row.colliders == null || row.colliders.Length != (player ? 0 : root.GetComponentsInChildren<Collider>(true).Length) || player && (!row.exists || !row.active))
                throw new InvalidOperationException("Изменился состав коллайдеров или отключён игрок: " + row.id);
            var adapters = root.Adapters.ToDictionary(a => a.Key, StringComparer.Ordinal); var keys = new HashSet<string>();
            foreach (var block in row.blocks)
            {
                if (block == null || block.key == null || !keys.Add(block.key) || !adapters.TryGetValue(block.key, out var adapter) || block.version != adapter.Version)
                    throw new InvalidOperationException("Неизвестный/повторный адаптер или версия: " + row.id);
                adapter.Validate(block.json, row);
            }
            if (keys.Count != adapters.Count) throw new InvalidOperationException("Состав адаптеров изменился: " + row.id);
        }
        internal static void ValidateCaptured(WorldSaveObject root, SavedWorldObject row) { ValidatePose(row); ValidateBlocks(root, row); }
        private static void Apply(WorldSaveObject root, SavedWorldObject row)
        {
            // Сначала выключается агент через адаптер, затем задаётся Transform. Навигация включится после commit.
            var adapters = root.Adapters.ToDictionary(a => a.Key, StringComparer.Ordinal);
            foreach (var block in row.blocks) adapters[block.key].ApplySilent(block.json);
            if (root.SavesTransform)
            {
                root.transform.SetPositionAndRotation(row.position, row.rotation);
                var parentScale = root.transform.parent != null ? root.transform.parent.lossyScale : Vector3.one;
                root.transform.localScale = new Vector3(row.scale.x/parentScale.x, row.scale.y/parentScale.y, row.scale.z/parentScale.z);
            }
            root.SetRemovedSilently(!row.exists);
            if (root.GetComponent<ZombieSurvival.Player.FirstPersonController>() == null)
            {
                var colliders = root.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = row.colliders[i];
            }
        }
        public void CommitSilent()
        {
            try { foreach (var entry in entries) Apply(entry.Target, entry.State); committed = true; }
            catch
            {
                foreach (var entry in entries) if (entry.Before != null) { Apply(entry.Target, entry.Before); foreach (var adapter in entry.Target.Adapters) adapter.Publish(); }
                throw;
            }
        }
        public void Publish()
        {
            if (!committed || published) throw new InvalidOperationException("Снимок мира не подготовлен к публикации.");
            published = true;
            foreach (var extra in extras)
            {
                // Устаревшие ссылки на удалённые контейнеры тоже не должны возвращать старые предметы.
                foreach (var storage in extra.GetComponentsInChildren<ItemStorage>(true)) storage.ReplaceSavedSlots(new List<ItemStack>());
                extra.Retire(); UnityEngine.Object.Destroy(extra.gameObject);
            }
            foreach (var entry in entries)
            {
                var root = entry.Target;
                if (entry.Created) { root.transform.SetParent(null, true); SceneManager.MoveGameObjectToScene(root.gameObject, registry.gameObject.scene); }
                root.gameObject.SetActive(entry.State.exists && entry.State.active);
            }
            registry.ReplaceSpawned(entries.Where(e => e.State.spawned).Select(e => e.Target));
            foreach (var entry in entries) foreach (var adapter in entry.Target.Adapters)
            {
                try { adapter.Publish(); }
                catch (Exception error) { Debug.LogException(error, adapter); }
            }
        }
        public void Dispose()
        {
            if (staging != null) UnityEngine.Object.Destroy(staging);
            if (stagingScene.IsValid() && stagingScene.isLoaded) SceneManager.UnloadSceneAsync(stagingScene);
        }
    }
}
