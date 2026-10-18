using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wind for the rigged Moscow Summer Trees. Attach to the imported model root.
/// Works with Built-in, URP and HDRP; no custom shader required.
/// Local branches/pivots are discovered by their Wind_Trunk and Wind_Branch_ prefixes.
/// </summary>
[DisallowMultipleComponent]
public sealed class MoscowTreeWind : MonoBehaviour
{
    public enum WindFromDirection
    {
        North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest, Custom
    }

    [Header("Wind / Ветер")]
    [SerializeField] private bool enableWind = true;
    [Tooltip("Откуда ДУЕТ ветер. Север = +Z в вашей сцене.")]
    [SerializeField] private WindFromDirection windFrom = WindFromDirection.West;
    [Tooltip("Для режима Custom укажи вектор В СТОРОНУ движения ветра в мировых координатах X,Z.")]
    [SerializeField] private Vector3 customTravelDirection = Vector3.right;
    [SerializeField, Range(0f, 3f)] private float windStrength = 0.8f;
    [SerializeField, Range(0.05f, 5f)] private float windSpeed = 0.65f;
    [SerializeField, Range(0f, 1f)] private float gustiness = 0.35f;
    [SerializeField, Range(0.02f, 3f)] private float gustFrequency = 0.32f;

    [Header("Motion / Анимация")]
    [SerializeField, Range(0f, 6f)] private float trunkBendDegrees = 1.0f;
    [SerializeField, Range(0f, 16f)] private float branchBendDegrees = 4.0f;
    [Tooltip("Фазовый сдвиг, чтобы одинаковые деревья не качались синхронно")]
    [SerializeField] private int variationSeed = 17;
    [SerializeField] private bool animateInEditMode = false;

    private struct Pivot
    {
        public Transform transform;
        public Quaternion rest;
        public float phase;
        public float weight;
        public bool trunk;
    }
    private readonly List<Pivot> pivots = new List<Pivot>();
    private bool cached;

    private void Awake() => CacheRig();
    private void OnEnable() => CacheRig();
    private void OnDisable() => RestoreRestPose();

    [ContextMenu("Rebuild wind rig / Обновить ветки")]
    public void CacheRig()
    {
        RestoreRestPose();
        pivots.Clear();
        Transform[] all = GetComponentsInChildren<Transform>(true);
        foreach (Transform part in all)
        {
            bool trunk = part.name.StartsWith("Wind_Trunk", StringComparison.OrdinalIgnoreCase);
            bool branch = part.name.StartsWith("Wind_Branch_", StringComparison.OrdinalIgnoreCase);
            if (!trunk && !branch) continue;
            int hash = variationSeed;
            unchecked
            {
                for (int i = 0; i < part.name.Length; i++) hash = hash * 31 + part.name[i];
                hash ^= (int)(part.position.x * 100f) * 17;
                hash ^= (int)(part.position.z * 100f) * 23;
            }
            float phase = Mathf.Abs((float)(hash % 10007)) / 10007f * Mathf.PI * 2f;
            pivots.Add(new Pivot
            {
                transform = part,
                rest = part.localRotation,
                phase = phase,
                weight = trunk ? 1f : Mathf.Lerp(0.55f, 1.2f, Mathf.Abs(Mathf.Sin(phase))),
                trunk = trunk
            });
        }
        cached = true;
    }

    private Vector3 WindTravelDirection()
    {
        switch (windFrom)
        {
            case WindFromDirection.North:     return Vector3.back;
            case WindFromDirection.NorthEast: return new Vector3(-1, 0, -1).normalized;
            case WindFromDirection.East:      return Vector3.left;
            case WindFromDirection.SouthEast: return new Vector3(-1, 0, 1).normalized;
            case WindFromDirection.South:     return Vector3.forward;
            case WindFromDirection.SouthWest: return new Vector3(1, 0, 1).normalized;
            case WindFromDirection.West:      return Vector3.right;
            case WindFromDirection.NorthWest: return new Vector3(1, 0, -1).normalized;
            default:
                var d = new Vector3(customTravelDirection.x, 0f, customTravelDirection.z);
                return d.sqrMagnitude < 0.00001f ? Vector3.right : d.normalized;
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying && !animateInEditMode) return;
        if (!cached) CacheRig();
        if (!enableWind || windStrength < 0.001f)
        {
            RestoreRestPose();
            return;
        }
        // Convert scene/world wind vector into this tree's local space.
        var worldDir = WindTravelDirection();
        var localDir = transform.InverseTransformDirection(worldDir);
        localDir.y = 0f;
        if (localDir.sqrMagnitude < 0.00001f) localDir = Vector3.right;
        localDir.Normalize();
        var axis = Vector3.Cross(Vector3.up, localDir).normalized;
        float t = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        float envelope = Mathf.Lerp(1f, 0.45f + Mathf.PerlinNoise(variationSeed * 0.021f, t * gustFrequency), gustiness);
        for (int i = 0; i < pivots.Count; i++)
        {
            var p = pivots[i];
            if (p.transform == null) continue;
            float wave = Mathf.Sin(t * windSpeed * 1.9f + p.phase * 0.28f)
                + Mathf.Sin(t * windSpeed * 3.1f + p.phase * 1.27f) * 0.23f;
            float amplitude = p.trunk ? trunkBendDegrees : branchBendDegrees;
            // Apply rotation from saved rest pose so it never drifts.
            p.transform.localRotation = p.rest * Quaternion.AngleAxis(
                amplitude * p.weight * windStrength * envelope * wave, axis);
        }
    }

    [ContextMenu("Reset rest pose / Сбросить анимацию")]
    public void RestoreRestPose()
    {
        foreach (Pivot p in pivots)
            if (p.transform != null) p.transform.localRotation = p.rest;
    }
}
