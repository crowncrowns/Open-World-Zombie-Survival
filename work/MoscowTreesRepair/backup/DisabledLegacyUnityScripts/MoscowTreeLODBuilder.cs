// Save in Assets/Editor/MoscowTreeLODBuilder.cs. Unity Editor only.
// Build a wind-enabled three-level LOD prefab from a selected imported *LOD0* model.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MoscowTreeLODBuilder
{
    [MenuItem("Tools/Moscow Trees/Create LOD Prefabs From Selected LOD0 Models")]
    private static void CreateLODGroups()
    {
        const string output = "Assets/MoscowTreeLODPrefabs";
        if (!AssetDatabase.IsValidFolder(output))
            AssetDatabase.CreateFolder("Assets", "MoscowTreeLODPrefabs");
        int ok = 0; int missing = 0;
        foreach (Object selected in Selection.objects)
        {
            if (!(selected is GameObject source)) continue;
            var path0 = AssetDatabase.GetAssetPath(source);
            if (!path0.Contains("/LOD0/") || !path0.Contains("_LOD0.")) continue;
            var path1 = path0.Replace("/LOD0/", "/LOD1/").Replace("_LOD0.", "_LOD1.");
            var path2 = path0.Replace("/LOD0/", "/LOD2/").Replace("_LOD0.", "_LOD2.");
            var model1 = AssetDatabase.LoadAssetAtPath<GameObject>(path1);
            var model2 = AssetDatabase.LoadAssetAtPath<GameObject>(path2);
            if (model1 == null || model2 == null) { missing++; Debug.LogWarning("Missing LOD: " + path1 + " / " + path2); continue; }
            string name = source.name.Replace("_LOD0", "");
            var root = new GameObject(name);
            try
            {
                GameObject[] modelObjects = { source, model1, model2 };
                LOD[] lods = new LOD[3];
                float[] thresholds = { 0.55f, 0.20f, 0.035f };
                for (int i = 0; i < 3; i++)
                {
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(modelObjects[i]);
                    if (child == null) child = Object.Instantiate(modelObjects[i]);
                    child.transform.SetParent(root.transform, false);
                    child.name = "Level_" + i;
                    child.transform.localPosition = Vector3.zero;
                    child.transform.localRotation = Quaternion.identity;
                    child.transform.localScale = Vector3.one;
                    var renderers = child.GetComponentsInChildren<Renderer>(true);
                    lods[i] = new LOD(thresholds[i], renderers);
                }
                var lodGroup = root.AddComponent<LODGroup>();
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
                var wind = root.AddComponent<MoscowTreeWind>();
                wind.CacheRig();
                var collider = root.AddComponent<CapsuleCollider>();
                Bounds b = new Bounds(Vector3.zero, Vector3.one);
                bool first = true;
                foreach (var renderer in lods[0].renderers)
                {
                    if (first) { b = renderer.bounds; first = false; }
                    else b.Encapsulate(renderer.bounds);
                }
                collider.direction = 1;
                collider.height = Mathf.Max(1.5f, b.size.y * .32f);
                collider.radius = Mathf.Clamp(b.size.y * .024f, .12f, .7f);
                collider.center = new Vector3(0, collider.height*.5f, 0);
                string filename = AssetDatabase.GenerateUniqueAssetPath(output + "/" + name + ".prefab");
                PrefabUtility.SaveAsPrefabAsset(root, filename);
                ok++;
            }
            finally { Object.DestroyImmediate(root); }
        }
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Moscow Trees LOD", ok + " LOD prefabs created. Missing LOD pairs: " + missing, "OK");
    }
}
#endif
