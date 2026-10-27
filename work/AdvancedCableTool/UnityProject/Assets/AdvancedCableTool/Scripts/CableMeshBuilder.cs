using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace AdvancedCableTool
{
    public sealed class CableMeshBuilder
    {
        Vector3[] vertices, normals;
        Vector2[] uv;
        Vector4[] tangents;
        float[] cosines, sines;
        int[] triangles;
        readonly List<int> shellTriangles = new List<int>(30000), copperTriangles = new List<int>(30000);
        public Mesh mesh;
        int oldRings, oldSides, oldStrands;
        bool lastDamaged;
        public Mesh Build(Vector3[] points, Transform space, float diameter, int step, int sides,
            float tiling, bool twisted, bool damaged, bool industrial = false)
        {
            int rings = (points.Length - 2) / step + 2, strands = twisted ? 7 : 1;
            int count = strands * (rings * (sides + 1) + 2);
            bool topologyChanged = !mesh || vertices == null || rings != oldRings || sides != oldSides || strands != oldStrands;
            if (!mesh) { mesh = new Mesh { name = "Cable generated mesh", hideFlags = HideFlags.DontSave }; mesh.MarkDynamic(); }
            if (topologyChanged)
            {
                oldRings = rings; oldSides = sides; oldStrands = strands;
                vertices = new Vector3[count]; normals = new Vector3[count]; uv = new Vector2[count];
                tangents = new Vector4[count]; cosines = new float[sides + 1]; sines = new float[sides + 1];
                for (int j = 0; j <= sides; ++j) { float a = j * Mathf.PI * 2 / sides; cosines[j] = Mathf.Cos(a); sines[j] = Mathf.Sin(a); }
                triangles = new int[strands * ((rings - 1) * sides * 6 + sides * 6)];
                int k = 0, stride = sides + 1;
                for (int s = 0; s < strands; ++s)
                {
                    int b = s * (rings * stride + 2);
                    for (int r = 0; r < rings - 1; ++r)
                        for (int j = 0; j < sides; ++j)
                        {
                            int n = b + r * stride + j;
                            triangles[k++] = n; triangles[k++] = n + stride; triangles[k++] = n + 1;
                            triangles[k++] = n + 1; triangles[k++] = n + stride; triangles[k++] = n + stride + 1;
                        }
                    for (int j = 0; j < sides; ++j)
                    {
                        triangles[k++] = b + rings * stride; triangles[k++] = b + j; triangles[k++] = b + j + 1;
                        triangles[k++] = b + rings * stride + 1; triangles[k++] = b + (rings - 1) * stride + j + 1;
                        triangles[k++] = b + (rings - 1) * stride + j;
                    }
                }
                for (int ti = 0; ti < triangles.Length; ti += 3)
                { int swap = triangles[ti + 1]; triangles[ti + 1] = triangles[ti + 2]; triangles[ti + 2] = swap; }
                mesh.Clear(); mesh.indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            }
            Vector3 previousT = Vector3.forward, normal = Vector3.up;
            Matrix4x4 toLocal = space.worldToLocalMatrix, normalMatrix = space.localToWorldMatrix.transpose;
            float handedness = toLocal.determinant < 0 ? -1 : 1;
            float distance = 0; Vector3 previousPosition = points[0];
            shellTriangles.Clear(); copperTriangles.Clear();
            for (int r = 0; r < rings; ++r)
            {
                int index = Mathf.Min(r * step, points.Length - 1);
                Vector3 p = points[index];
                Vector3 tangent = points[Mathf.Min(index + step, points.Length - 1)] - points[Mathf.Max(0, index - step)];
                if (tangent.sqrMagnitude < 1e-10f) tangent = previousT;
                tangent.Normalize();
                if (r == 0) normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < .9f ? Vector3.up : Vector3.right).normalized;
                else { normal = Quaternion.FromToRotation(previousT, tangent) * normal; distance += Vector3.Distance(p, previousPosition); }
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                for (int s = 0; s < strands; ++s)
                {
                    float angle = s * Mathf.PI / 3 + distance / Mathf.Max(.02f, diameter * 5) * Mathf.PI * 2;
                    Vector3 offset = s == 6 || !twisted ? Vector3.zero : (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * diameter / 3;
                    float radius = diameter * (twisted ? 1f / 6 : .5f);
                    if (industrial) radius *= 1 + .07f * Mathf.Cos(distance / Mathf.Max(.02f, diameter * .8f) * Mathf.PI * 2);
                    // Recesses expose a real, smaller core profile rather than a flat decal.
                    if (damaged && Mathf.Repeat(distance, .75f) > .62f) radius *= .58f;
                    int b = s * (rings * (sides + 1) + 2);
                    for (int j = 0; j <= sides; ++j)
                    {
                        Vector3 n = normal * cosines[j] + binormal * sines[j];
                        int vi = b + r * (sides + 1) + j;
                        vertices[vi] = toLocal.MultiplyPoint3x4(p + offset + n * radius);
                        normals[vi] = normalMatrix.MultiplyVector(n).normalized;
                        Vector3 u = toLocal.MultiplyVector(-normal * sines[j] + binormal * cosines[j]).normalized;
                        tangents[vi] = new Vector4(u.x, u.y, u.z, handedness);
                        uv[vi] = new Vector2((float)j / sides * Mathf.PI * diameter * tiling, distance * tiling);
                    }
                    if (r == 0 || r == rings - 1)
                    {
                        int cap = b + rings * (sides + 1) + (r == 0 ? 0 : 1);
                        vertices[cap] = toLocal.MultiplyPoint3x4(p + offset);
                        normals[cap] = normalMatrix.MultiplyVector(r == 0 ? -tangent : tangent).normalized;
                        Vector3 u = toLocal.MultiplyVector(normal).normalized;
                        tangents[cap] = new Vector4(u.x, u.y, u.z, handedness);
                        uv[cap] = new Vector2(0, distance * tiling);
                    }
                }
                previousT = tangent;
                previousPosition = p;
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.tangents = tangents;
            if (damaged)
            {
                int sideIndices = (rings - 1) * sides * 6;
                for (int ti = 0; ti < triangles.Length; ti += 3)
                {
                    int ring = ti / (sides * 6);
                    float physical = uv[Mathf.Min(ring, rings - 1) * (sides + 1)].y / tiling;
                    var target = ti >= sideIndices || Mathf.Repeat(physical, .75f) > .62f ? copperTriangles : shellTriangles;
                    target.Add(triangles[ti]); target.Add(triangles[ti+1]); target.Add(triangles[ti+2]);
                }
                mesh.subMeshCount = 2; mesh.SetTriangles(shellTriangles,0); mesh.SetTriangles(copperTriangles,1);
            }
            else if (topologyChanged || lastDamaged) { mesh.subMeshCount = 1; mesh.triangles = triangles; }
            lastDamaged = damaged;
            mesh.RecalculateBounds();
            return mesh;
        }
        public void Dispose() { if (mesh) { if (Application.isPlaying) Object.Destroy(mesh); else Object.DestroyImmediate(mesh); } }
    }
}
