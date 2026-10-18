// Copy this script into Assets/Editor in your Unity project.
// Select imported Apocalypse_House_Unity.obj (or a prefab from a glTF importer),
// then Tools > Apocalypse House > Create Prefab With Colliders.
using System;
using UnityEditor;
using UnityEngine;

public class ApocalypseHousePrefabBuilder
{
    [MenuItem("Tools/Apocalypse House/Create Prefab With Colliders")]
    public static void CreatePrefab()
    {
        var model = Selection.activeObject as GameObject;
        if (model == null)
        {
            EditorUtility.DisplayDialog("Apocalypse House", "Select the imported OBJ or GLB model in Project first.", "OK");
            return;
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (instance == null) instance = UnityEngine.Object.Instantiate(model);
        instance.name = "Apocalypse_House_GameReady";
        int added = 0;
        foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            GameObject go = filter.gameObject;
            string n = go.name;
            bool dynamicDoor = n.Contains("DOOR_") || n.Contains("Doors__");
            bool geometry = n.Contains("Exterior") || n.Contains("Partition") ||
                n.Contains("Foundation") || n.Contains("Floors") ||
                n.Contains("Cellar_Shell") || n.Contains("Attic__") ||
                n.Contains("Roof__") || n.Contains("Porch__") ||
                n.Contains("Cellar_Details");
            if (dynamicDoor)
            {
                if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
                continue;
            }
            if (geometry)
            {
                if (go.GetComponent<Collider>() == null)
                {
                    go.AddComponent<MeshCollider>();
                    added++;
                }
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }
        }
        const string folder = "Assets/ApocalypseHouse";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "ApocalypseHouse");
        string path = folder + "/Apocalypse_House_GameReady.prefab";
        PrefabUtility.SaveAsPrefabAsset(instance,path);
        UnityEngine.Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Apocalypse House", "Prefab created at " + path +
            ". Added " + added + " MeshColliders. Check door pivots before animating.", "OK");
    }
}
