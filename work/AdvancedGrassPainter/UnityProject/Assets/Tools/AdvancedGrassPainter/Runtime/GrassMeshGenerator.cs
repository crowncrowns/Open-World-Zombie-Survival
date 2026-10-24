using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdvancedGrassPainter
{
    public static class GrassMeshGenerator
    {
        public const int Segments = 6;
        public struct Blade
        {
            public Vector3 root, bend, side, normal;
            public float height, width, ripple;
            public Vector3 Center(float t) => root + normal * (height * t) + bend * (t * t) + side * (Mathf.Sin(t * 5.4f) * ripple * t);
            public float HalfWidth(float t) => width * .5f * Mathf.Pow(1 - t, .72f) * (.72f + .28f * Mathf.Sin(t * Mathf.PI));
        }
        public static int BladeCount(GrassPlant p) => p.kind == GrassKind.Short ? 7 : p.kind == GrassKind.NeglectedUrban ? 11 : 9;
        public static Blade GetBlade(GrassPlant p, int i)
        {
            var r = new GrassRandomizationSystem(GrassRandomizationSystem.Hash(p.seed, i));
            Quaternion orientation = Quaternion.FromToRotation(Vector3.up, p.normal) * Quaternion.Euler(0, p.yaw, 0);
            float a = r.Range(0, Mathf.PI * 2), spread = r.Range(.005f, .075f);
            Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            float h = p.height * r.Range(.48f, 1.12f);
            bool weed = p.kind == GrassKind.NeglectedUrban && i % 4 == 0;
            return new Blade { root = p.position + orientation * radial * spread,
                normal = p.normal, side = orientation * new Vector3(-radial.z, 0, radial.x),
                bend = orientation * radial * h * r.Range(weed ? .28f : .10f, .48f),
                height = h, width = p.width * r.Range(.55f, weed ? 2.8f : 1.3f), ripple = h * r.Range(.015f, .055f) };
        }
        public static Mesh Build(List<GrassPlant> plants, int lod, Vector3 origin)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var colors = new List<Color>();
            var tangents = new List<Vector4>();
            var uv = new List<Vector2>(); var wind = new List<Vector2>(); var triangles = new List<int>();
            int segments = lod == 0 ? Segments : lod == 1 ? 3 : 2;
            for (int n = 0; n < plants.Count; n++)
            {
                var p = plants[n];
                // A stable nested subset reduces far density without moving the remaining plants.
                uint keep = (uint)GrassRandomizationSystem.Hash(p.seed, 517);
                if (lod == 1 && keep % 4 == 0 || lod == 2 && keep % 4 != 1) continue;
                int count = BladeCount(p);
                for (int b = 0; b < count; b++)
                {
                    if (lod > 0 && b % (lod + 1) != 0) continue;
                    Blade blade = GetBlade(p, b); int start = vertices.Count;
                    for (int j = 0; j <= segments; j++)
                    {
                        float t = j / (float)segments; Vector3 center = blade.Center(t) - origin;
                        float w = blade.HalfWidth(t);
                        Vector3 face = Vector3.Cross(blade.side, (blade.normal + blade.bend / blade.height * (2 * t))).normalized;
                        for (int k = 0; k < 3; k++)
                        {
                            float side = k - 1;
                            vertices.Add(center + blade.side * (side * w) + face * (k == 1 ? w * .22f : 0));
                            Vector3 normal=(face+blade.side*(side*.22f)).normalized;
                            normals.Add(normal);Vector3 tangent=(blade.side-normal*Vector3.Dot(blade.side,normal)).normalized;
                            tangents.Add(new Vector4(tangent.x,tangent.y,tangent.z,1));
                            colors.Add(Color.Lerp(p.root, p.tip, Mathf.Pow(t, .7f)));
                            uv.Add(new Vector2(k * .5f, t)); wind.Add(new Vector2(p.wind, (p.seed & 4095) * .017f));
                        }
                        if (j == segments) continue;
                        for (int k = 0; k < 2; k++)
                        {
                            int x = start + j * 3 + k;
                            triangles.Add(x); triangles.Add(x + 3); triangles.Add(x + 1);
                            triangles.Add(x + 1); triangles.Add(x + 3); triangles.Add(x + 4);
                        }
                    }
                }
            }
            var mesh = new Mesh { name = "Grass chunk LOD" + lod, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTangents(tangents); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetUVs(1, wind); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); var bounds = mesh.bounds; bounds.Expand(1.2f); mesh.bounds = bounds;
            return mesh;
        }
    }
}
