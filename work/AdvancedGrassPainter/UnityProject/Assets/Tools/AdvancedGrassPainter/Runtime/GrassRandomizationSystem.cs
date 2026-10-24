using UnityEngine;

namespace AdvancedGrassPainter
{
    public struct GrassRandomizationSystem
    {
        uint state;
        public GrassRandomizationSystem(int seed) { state = (uint)seed ^ 0x9e3779b9u; if (state == 0) state = 1; }
        public uint Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        public float Value() => (Next() & 0xffffff) / 16777216f;
        public float Range(float a, float b) => Mathf.Lerp(a, b, Value());
        public static int Hash(int a, int b, int c = 0)
        {
            unchecked { uint h = (uint)a * 747796405u + (uint)b * 2891336453u + (uint)c * 277803737u; h = (h ^ (h >> 16)) * 2246822519u; return (int)(h ^ (h >> 13)); }
        }
    }
    public static class GrassDensityMask
    {
        public static float Fractal(Vector3 p, GrassSettings s)
        {
            float offset = (s.globalSeed & 65535) * .0173f;
            float x = p.x / Mathf.Max(.1f, s.noiseScale) + offset, y = p.z / Mathf.Max(.1f, s.noiseScale) - offset;
            return .57f * Mathf.PerlinNoise(x, y) + .29f * Mathf.PerlinNoise(x * 2.13f, y * 2.13f) + .14f * Mathf.PerlinNoise(x * 4.31f, y * 4.31f);
        }
        public static float Sample(Vector3 p, Vector3 center, GrassSettings s)
        {
            float t = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(center.x, center.z)) / s.radius;
            float edge = s.edgeFalloff <= 0 ? 1 : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1 - s.edgeFalloff, 1, t));
            return Mathf.Clamp01(s.density * s.growth * edge * Mathf.Lerp(1, Mathf.SmoothStep(.12f, .86f, Fractal(p, s)), s.clumping));
        }
    }
}
