using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieSurvival.Persistence
{
    /// <summary>Коллайдеры будущего состояния в отдельной PhysicsScene. Проверка закрытой/открытой двери
    /// и появляющейся опоры не двигает настоящую дверь и не вызывает триггеры текущего мира.</summary>
    internal sealed class ProspectiveWorldPhysics : IDisposable
    {
        private readonly Scene scene;
        private readonly PhysicsScene physics;
        private readonly Collider[] hits = new Collider[512];
        private readonly Dictionary<Collider, string> owners = new Dictionary<Collider, string>();
        public ProspectiveWorldPhysics()
        {
            scene = SceneManager.CreateScene("Save collision " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics = scene.GetPhysicsScene();
        }
        public void Add(Transform root)
        {
            foreach (var source in root.GetComponentsInChildren<Collider>(true))
            {
                if (!source.enabled || source.isTrigger || !ActiveUnder(source.transform, root)) continue;
                var go = new GameObject("Prospective " + source.name); SceneManager.MoveGameObjectToScene(go, scene);
                go.layer = source.gameObject.layer;
                go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation); go.transform.localScale = source.transform.lossyScale;
                if (source is BoxCollider box) { var c = go.AddComponent<BoxCollider>(); c.size = box.size; c.center = box.center; }
                else if (source is CapsuleCollider capsule) { var c = go.AddComponent<CapsuleCollider>(); c.radius = capsule.radius; c.height = capsule.height; c.center = capsule.center; c.direction = capsule.direction; }
                else if (source is SphereCollider sphere) { var c = go.AddComponent<SphereCollider>(); c.radius = sphere.radius; c.center = sphere.center; }
                else if (source is MeshCollider mesh) { var c = go.AddComponent<MeshCollider>(); c.sharedMesh = mesh.sharedMesh; c.convex = mesh.convex; }
                else if (source is TerrainCollider terrain) go.AddComponent<TerrainCollider>().terrainData = terrain.terrainData;
                else if (source is CharacterController cc) { var c = go.AddComponent<CapsuleCollider>(); c.radius = cc.radius; c.height = cc.height; c.center = cc.center; }
                else throw new InvalidOperationException("Нет адаптера геометрии для " + source.GetType().Name);
                owners[go.GetComponent<Collider>()] = source.GetComponentInParent<WorldSaveObject>(true)?.Id;
            }
            Physics.SyncTransforms();
        }
        private static bool ActiveUnder(Transform child, Transform root)
        { for (var p = child; p != null && p != root; p = p.parent) if (!p.gameObject.activeSelf) return false; return true; }
        public bool Blocks(Vector3 bottom, Vector3 top, float radius, int mask, string ignoreId = null)
        {
            int count = physics.OverlapCapsule(bottom, top, radius, hits, mask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return true;
            for (int i = 0; i < count; i++) if (ignoreId == null || !owners.TryGetValue(hits[i], out var id) || id != ignoreId) return true;
            return false;
        }
        public bool Supports(Vector3 origin, float distance, int mask) => physics.Raycast(origin, Vector3.down, out var hit, distance, mask, QueryTriggerInteraction.Ignore) && hit.normal.y > .6f;
        public void Dispose() { if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene); }
    }
}
