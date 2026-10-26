using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZombieSurvival.ModularBuildings.Editor
{
    // Operates on instance-owned renderers only; shared materials and source prefabs are never edited.
    public static class BuildingExteriorMaterials
    {
        public static Material EffectiveMaterial(GeneratedBuilding b) => b.exteriorMaterialOverride ? b.exteriorMaterialOverride : b.profile.facadeMaterial;
        public static bool IsDoorSurface(MeshRenderer r) => r.name == "Door_Leaf" || r.name.StartsWith("Door_Frame_", StringComparison.Ordinal);
        // Balcony modules use three independent wall pieces; frames/glass/handles keep their materials.
        public static bool IsBalconyWall(MeshRenderer r) => r.name == "Wall_Left" || r.name == "Wall_Right" || r.name == "Wall_Header";
        public static bool IsTripleWall(MeshRenderer r) => r.name == "Wall_Left" || r.name == "Wall_Right" || r.name == "Wall_Below" || r.name == "Wall_Above";

        public static IEnumerable<Transform> Floors(GeneratedBuilding b) => b.transform.Cast<Transform>()
            .Where(t => t.name.StartsWith("Entrance_", StringComparison.Ordinal))
            .SelectMany(t => t.Cast<Transform>()).Where(t => t.name.StartsWith("Floor_", StringComparison.Ordinal));

        static bool IsOuterModule(Transform t) => t.name.StartsWith("Front_", StringComparison.Ordinal)
            || t.name.StartsWith("Rear_", StringComparison.Ordinal) || t.name == "LeftEnd" || t.name == "RightEnd";

        public static MeshRenderer[] Walls(GeneratedBuilding b)
        {
            var renderers = new List<MeshRenderer>();
            foreach (var floor in Floors(b)) foreach (Transform module in floor) {
                if (!IsOuterModule(module)) continue;
                foreach (var r in module.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name == "Wall_Window" || r.name == "Wall_Solid_Mesh" || r.name.StartsWith("Wall_Door_", StringComparison.Ordinal) || IsBalconyWall(r) || IsTripleWall(r)) renderers.Add(r);
            }
            foreach (var name in new[] { "FacadeCornerClosures", "TerrainFoundation" }) {
                var group = b.transform.Find(name);
                if (group) renderers.AddRange(group.GetComponentsInChildren<MeshRenderer>(true));
            }
            return renderers.Distinct().ToArray();
        }

        public static MeshRenderer[] Doors(GeneratedBuilding b) => Floors(b).SelectMany(f => f.Cast<Transform>())
            .Where(t => t.name.StartsWith("Front_", StringComparison.Ordinal))
            .SelectMany(t => t.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "EntranceDoor")
            .SelectMany(t => t.GetComponentsInChildren<MeshRenderer>(true)).Where(IsDoorSurface).Distinct().ToArray();

        public static int Apply(GeneratedBuilding b, Material material, bool includeDoors, bool recordUndo = true)
        {
            if (!b || !b.profile || !material) throw new InvalidOperationException("Выберите дом и материал наружных стен.");
            if (EditorUtility.IsPersistent(b)) throw new InvalidOperationException("Выберите экземпляр дома в сцене; исходный prefab не изменяется.");
            var walls = Walls(b); var doors = Doors(b);
            var originals = b.originalExteriorDoorMaterials ?? new GeneratedBuilding.DoorSurfaceMaterials[0];
            var affected = walls.Concat(includeDoors ? doors : originals.Where(x => x.renderer).Select(x => x.renderer)).Distinct().ToArray();
            if (recordUndo) Undo.RecordObjects(affected.Cast<UnityEngine.Object>().Concat(new UnityEngine.Object[] { b }).ToArray(), "Материал наружных стен и входных дверей");
            if (includeDoors) {
                var saved = originals.Where(x => x.renderer).ToList();
                foreach (var door in doors) if (!saved.Any(x => x.renderer == door))
                    saved.Add(new GeneratedBuilding.DoorSurfaceMaterials { renderer = door, materials = door.sharedMaterials });
                b.originalExteriorDoorMaterials = saved.ToArray();
            } else {
                foreach (var saved in originals) if (saved.renderer) saved.renderer.sharedMaterials = saved.materials;
                b.originalExteriorDoorMaterials = new GeneratedBuilding.DoorSurfaceMaterials[0];
            }
            foreach (var renderer in walls.Concat(includeDoors ? doors : new MeshRenderer[0]))
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            b.exteriorMaterialOverride = material;
            b.exteriorIncludesEntranceDoors = includeDoors;
            if (recordUndo) {
                foreach (var renderer in affected) { EditorUtility.SetDirty(renderer); PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); }
                EditorUtility.SetDirty(b); PrefabUtility.RecordPrefabInstancePropertyModifications(b);
                if (b.gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(b.gameObject.scene)) EditorSceneManager.MarkSceneDirty(b.gameObject.scene);
            }
            return affected.Length;
        }
    }
}
