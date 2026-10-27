using System;
using UnityEngine;

namespace AdvancedCableTool
{
    [Serializable]
    public sealed class CableAnchor
    {
        public bool attached = true;
        public Transform target;
        public Vector3 localPoint;
        public Vector3 worldPoint;
        [SerializeField] bool bound;
        public Vector3 Resolve()
        {
            if (target) { worldPoint = target.TransformPoint(localPoint); bound = true; }
            else if (bound) { attached = false; bound = false; }
            return worldPoint;
        }
        public void Attach(Vector3 point, Transform support = null)
        {
            target = support; bound = support; attached = true; worldPoint = point;
            localPoint = support ? support.InverseTransformPoint(point) : point;
        }
        public void Detach() { Resolve(); attached = false; target = null; bound = false; }
    }

    public enum CableType { Electric, Power, Soviet, Steel, Industrial, Damaged }
    public enum CableWear { New, Used, Damaged }

    public static class CableCatenary
    {
        // Solve 2a sinh(h/2a) = sqrt(L²-v²), then sample uniformly by arc length.
        public static Vector3 Point(Vector3 start, Vector3 end, float length, Vector3 gravity, float t)
        {
            Vector3 up = gravity.sqrMagnitude > 0.0001f ? -gravity.normalized : Vector3.up;
            Vector3 delta = end - start;
            double v = Vector3.Dot(delta, up);
            Vector3 horizontal = delta - up * (float)v;
            double h = horizontal.magnitude, chord = delta.magnitude;
            if (length <= chord + 0.0001) return Vector3.Lerp(start, end, t);
            if (h < 0.0001)
            {
                // Degenerate vertical span: folded hanging chain, length-preserving limit.
                float down = (length - (float)v) * 0.5f, s = t * length;
                return start + up * (s <= down ? -s : s - 2 * down);
            }
            double q = Math.Sqrt(Math.Max(h * h, length * length - v * v));
            double lo = 0, hi = 40;
            for (int i = 0; i < 64; ++i)
            {
                double u = (lo + hi) * 0.5;
                if (Math.Sinh(u) / u > q / h) hi = u; else lo = u;
            }
            double a = h / (lo + hi);
            double x0 = h * 0.5 - a * Math.Log((length + v) / (length - v)) * 0.5;
            double baseS = Math.Sinh(-x0 / a);
            double sValue = baseS + t * length / a;
            double x = x0 + a * Math.Log(sValue + Math.Sqrt(sValue * sValue + 1));
            double y = a * (Math.Sqrt(1 + sValue * sValue) - Math.Cosh(-x0 / a));
            return start + horizontal.normalized * (float)x + up * (float)y;
        }
    }
}
