// Put this file in Assets/Editor/MoscowTreePrefabBuilder.cs (Editor folder required).
// Works on imported FBX and glTF model prefabs that Unity recognizes as GameObjects.
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MoscowTreePrefabBuilder
{
    [MenuItem("Tools/Moscow Trees/Create Wind Prefabs From Selection")]
    private static void MakePrefabs()
    {
        const string output = "Assets/MoscowTreePrefabs";
        if (!AssetDatabase.IsValidFolder(output))
            AssetDatabase.CreateFolder("Assets", "MoscowTreePrefabs");
        int count = 0;
        foreach (UnityEngine.Object selected in Selection.objects)
        {
            if (!(selected is GameObject source)) continue;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (instance == null) instance = UnityEngine.Object.Instantiate(source);
            instance.name = source.name;
            try
            {
                var wind = instance.GetComponent<MoscowTreeWind>();
                if (wind == null) wind = instance.AddComponent<MoscowTreeWind>();
                wind.CacheRig();
                // Simple static trunk collision. Branch/leaf meshes have no collision.
                Renderer[] all = instance.GetComponentsInChildren<Renderer>(true);
                Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
                bool first = true;
                foreach (var renderer in all)
                {
                    if (first) { bounds = renderer.bounds; first = false; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (!first)
                {
                    var capsule = instance.GetComponent<CapsuleCollider>();
                    if (capsule == null) capsule = instance.AddComponent<CapsuleCollider>();
                    capsule.direction = 1;
                    // Collider fits only the trunk, not canopy.
                    capsule.height = Mathf.Max(1.5f, bounds.size.y * 0.35f);
                    capsule.radius = Mathf.Clamp(bounds.size.y * 0.025f, 0.12f, 0.7f);
                    capsule.center = new Vector3(0f, capsule.height * .5f, 0f);
                }
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(output + "/" + source.name + ".prefab");
                PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
                count++;
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Moscow Trees", "Saved " + count + " wind-enabled tree prefabs in " + output, "OK");
    }
    [MenuItem("Tools/Moscow Trees/Create Wind Prefabs From Selection", true)]
    private static bool CanMakePrefabs() => Selection.objects != null && Selection.objects.Length > 0;
}
#endif
