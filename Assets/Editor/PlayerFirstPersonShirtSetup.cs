using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Player;

/// <summary>Model-specific own-camera neckline; the original shirt remains intact for other cameras and shadows.</summary>
public static class PlayerFirstPersonShirtSetup
{
    public const string MeshPath = "Assets/Characters/PlayerUAL/SurvivalistShirtFirstPerson.asset";
    public const string RendererName = "Survivalist shirt (own camera)";

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit only");
        var root = PrefabUtility.LoadPrefabContents(PlayerUALSetup.VisualPath);
        try
        {
            var shirt = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(r => r.name == "SK_Military_Shirt3");
            var source = shirt.sharedMesh;
            var vertices = source.vertices;
            var weights = source.boneWeights;
            int neck = Array.FindIndex(shirt.bones, b => b != null && b.name == "neck_01");
            if (neck < 0 || vertices.Length != weights.Length) throw new InvalidOperationException("Unexpected Survivalist shirt rig");
            bool IsEyeSideCloth(int i)
            {
                var w = weights[i];
                float Influence(Func<string, bool> match)
                {
                    float Part(int index, float value) => match(shirt.bones[index].name) ? value : 0;
                    return Part(w.boneIndex0,w.weight0)+Part(w.boneIndex1,w.weight1)+Part(w.boneIndex2,w.weight2)+Part(w.boneIndex3,w.weight3);
                }
                float neckWeight = Influence(n => n == "neck_01");
                float shoulderWeight = Influence(n => n.StartsWith("clavicle_"));
                // Remove the neck opening and its shoulder seam beside the independent eye.
                // Bind-pose metres of this asset: the torso and upper/lower arm sleeves remain.
                return vertices[i].y > 1.52f && Mathf.Abs(vertices[i].x) < .18f && neckWeight > .12f
                    || vertices[i].y > 1.45f && Mathf.Abs(vertices[i].x) < .32f && shoulderWeight > .15f;
            }
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = "Survivalist shirt - own camera neckline";
            int removed = 0, retained = 0;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var triangles = source.GetTriangles(sub);
                var kept = new List<int>(triangles.Length);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    if (IsEyeSideCloth(triangles[i]) || IsEyeSideCloth(triangles[i + 1]) || IsEyeSideCloth(triangles[i + 2])) { removed++; continue; }
                    kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]); retained++;
                }
                mesh.SetTriangles(kept, sub, false);
            }
            if (removed == 0 || retained < removed) { UnityEngine.Object.DestroyImmediate(mesh); throw new InvalidOperationException("Unexpected neckline selection"); }
            var authored = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (authored == null) { AssetDatabase.CreateAsset(mesh, MeshPath); authored = mesh; }
            else { EditorUtility.CopySerialized(mesh, authored); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(authored); AssetDatabase.SaveAssetIfDirty(authored); }

            var own = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).SingleOrDefault(r => r.name == RendererName);
            if (own == null)
            {
                own = new GameObject(RendererName).AddComponent<SkinnedMeshRenderer>();
                own.transform.SetParent(shirt.transform.parent, false);
            }
            own.transform.localPosition = shirt.transform.localPosition;
            own.transform.localRotation = shirt.transform.localRotation;
            own.transform.localScale = shirt.transform.localScale;
            own.sharedMesh = authored; own.sharedMaterials = shirt.sharedMaterials;
            own.bones = shirt.bones; own.rootBone = shirt.rootBone; own.localBounds = shirt.localBounds;
            own.updateWhenOffscreen = true; own.quality = shirt.quality;
            own.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            own.receiveShadows = shirt.receiveShadows;
            // forceRenderingOff is runtime-only. Disabled is the persistent safe default for
            // raw prefab/Scene views; PlayerVisual enables it and gates it to its own camera.
            own.forceRenderingOff = true; own.enabled = false;
            var definition = root.GetComponent<PlayerModelDefinition>();
            var data = new SerializedObject(definition);
            void Include(string name, UnityEngine.Object value)
            {
                var list = data.FindProperty(name);
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
                int index = list.arraySize; list.arraySize++;
                list.GetArrayElementAtIndex(index).objectReferenceValue = value;
            }
            Include("firstPersonHiddenRenderers", shirt);
            Include("firstPersonOnlyRenderers", own);
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerUALSetup.VisualPath);
            return $"Own-camera neckline: removed {removed}, retained {retained} triangles; original mesh unchanged";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
