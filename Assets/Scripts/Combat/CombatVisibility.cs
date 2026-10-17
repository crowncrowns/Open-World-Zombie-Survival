using System;
using UnityEngine;

namespace ZombieSurvival.Combat
{
    public static class CombatVisibility
    {
        public static bool ClearLine(Vector3 origin, Vector3 point, Transform owner,
            Transform target, LayerMask mask, EntityId ignoredRoot = default)
        {
            Vector3 delta = point - origin;
            if (delta.sqrMagnitude < 0.0001f) return true;
            RaycastHit[] hits = Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                mask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == owner || hitTransform.IsChildOf(owner)) continue;
                if (hitTransform.root.gameObject.GetEntityId() == ignoredRoot) continue;
                if (target != null && (hitTransform == target || hitTransform.IsChildOf(target))) return true;
                return false;
            }
            return true;
        }
    }
}
