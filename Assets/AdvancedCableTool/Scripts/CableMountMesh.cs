using UnityEngine;

namespace AdvancedCableTool
{
    // Shared metal band geometry: local Y follows the cable tangent.
    public static class CableMountMesh
    {
        static Mesh shared;
        static CableMountMesh()
        {
            #if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Clear;
            #endif
        }
        public static Mesh Get()
        {
            if (shared) return shared;
            const int rings = 32, sides = 8, stride = sides + 1;
            var vertices = new Vector3[(rings + 1) * stride];
            var normals = new Vector3[vertices.Length]; var uv = new Vector2[vertices.Length];
            var triangles = new int[rings * sides * 6]; int k = 0;
            for (int r = 0; r <= rings; ++r)
            {
                float a = r * Mathf.PI * 2 / rings;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int s = 0; s <= sides; ++s)
                {
                    float b = s * Mathf.PI * 2 / sides; int index = r * stride + s;
                    normals[index] = radial * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    vertices[index] = radial * .61f + normals[index] * .105f;
                    uv[index] = new Vector2((float)r / rings, (float)s / sides);
                    if (r == rings || s == sides) continue;
                    triangles[k++] = index; triangles[k++] = index + 1; triangles[k++] = index + stride;
                    triangles[k++] = index + 1; triangles[k++] = index + stride + 1; triangles[k++] = index + stride;
                }
            }
            shared = new Mesh { name = "Cable metal mounting band", hideFlags = HideFlags.DontSave };
            shared.vertices = vertices; shared.normals = normals; shared.uv = uv; shared.triangles = triangles;
            shared.RecalculateBounds(); shared.RecalculateTangents(); return shared;
        }
        static void Clear() { if (shared) Object.DestroyImmediate(shared); shared = null; }
    }
}
