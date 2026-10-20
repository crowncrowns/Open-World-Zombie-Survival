using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Static interior presentation only; no actor or service state.</summary>
public static class KursHubInteriorFinishSetup
{
    [Serializable] public sealed class LightPlacement
    {
        public string id;
        public float[] position;
        public float[] color;
        public float intensity;
        public float range;
    }
    private static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

    public static void Apply(GameObject root, Dictionary<string, Material> materials, LightPlacement[] lights)
    {
        foreach (var r in root.transform.Find("Shell").GetComponentsInChildren<MeshRenderer>(true))
            if (r.name.StartsWith("Floors_", StringComparison.Ordinal))
                TileTopOnly(r, materials);
        var interior = root.transform.Find("Interior architecture");
        foreach (Transform unit in interior)
        {
            if (unit.name.EndsWith("Directions", StringComparison.Ordinal)) unit.gameObject.SetActive(false);
            if (!unit.name.StartsWith("Ground ", StringComparison.Ordinal)) continue;
            foreach (var r in unit.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name == "KH_Shopfront6_Lettering_NC") r.enabled = false;
        }
        // Keep the original lights and IDs for existing scene overrides. New,
        // smaller pools follow lamps and shops instead of filling the hall flat.
        root.transform.Find("Stage 1 working lights").gameObject.SetActive(false);
        var group = root.transform.Find("Settlement working lights");
        if (group == null)
        {
            group = new GameObject("Settlement working lights").transform;
            group.SetParent(root.transform, false);
        }
        var expected = lights.ToDictionary(l => l.id, StringComparer.Ordinal);
        foreach (Transform child in group)
            if (!expected.ContainsKey(child.name) || child.GetComponent<Light>() == null)
                throw new InvalidOperationException("Авторский объект в группе света оставлен без замены: " + child.name);
        foreach (var item in lights)
        {
            var child = group.Find(item.id);
            if (child == null)
            {
                child = new GameObject(item.id).transform;
                child.SetParent(group, false);
                var light = child.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.color = new Color(item.color[0], item.color[1], item.color[2]);
                light.intensity = item.intensity;
                light.range = item.range;
            }
            child.localPosition = V(item.position);
            child.gameObject.isStatic = true;
        }
    }

    private static void TileTopOnly(MeshRenderer renderer, Dictionary<string, Material> materials)
    {
        var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(KursHubSetup.Folder + "/Models/KursHubShell.fbx");
        var originalFilter = sourceModel.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == renderer.name);
        var original = originalFilter.sharedMesh;
        var vertices = original.vertices;
        var top = new List<int>(); var other = new List<int>();
        for (var s = 0; s < original.subMeshCount; s++)
        {
            var indices = original.GetTriangles(s);
            for (var i = 0; i < indices.Length; i += 3)
            {
                // FBX axes and normal handedness are importer details. The
                // real walking planes are the confirmed world Y=0 / 5.5 m.
                var heights = new[] { 0, 1, 2 }.Select(j => renderer.transform.TransformPoint(vertices[indices[i+j]]).y).ToArray();
                var isTop = heights.All(y => Mathf.Abs(y) < .001f) || heights.All(y => Mathf.Abs(y - 5.5f) < .001f);
                var target = isTop ? top : other;
                target.Add(indices[i]); target.Add(indices[i+1]); target.Add(indices[i+2]);
            }
        }
        if (top.Count == 0 || other.Count == 0) throw new InvalidOperationException("Не разделены пол и потолок: " + renderer.name);
        var folder = KursHubSetup.Folder + "/InteriorMeshes";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(KursHubSetup.Folder, "InteriorMeshes");
        var path = folder + "/" + renderer.name + ".asset";
        var built = UnityEngine.Object.Instantiate(original);
        built.name = renderer.name + "_TileTop";
        built.subMeshCount = 2; built.SetTriangles(top, 0); built.SetTriangles(other, 1);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { AssetDatabase.CreateAsset(built, path); mesh = built; }
        else { EditorUtility.CopySerialized(built, mesh); UnityEngine.Object.DestroyImmediate(built); EditorUtility.SetDirty(mesh); }
        renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
        renderer.GetComponent<MeshCollider>().sharedMesh = mesh;
        renderer.sharedMaterials = new[] { materials["CeramicTile"], materials["Floor"] };
    }
}
