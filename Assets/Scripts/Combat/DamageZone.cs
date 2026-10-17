using System.Collections.Generic;
using UnityEngine;

namespace ZombieSurvival.Combat
{
    [DisallowMultipleComponent]
    public sealed class DamageZone : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float damage = 25f;
        [SerializeField, Min(.02f)] private float interval = 1f;
        [SerializeField] private LayerMask affectedLayers = ~0;
        private readonly Dictionary<Health, float> nextHits = new Dictionary<Health, float>();
        private void OnTriggerEnter(Collider other) => Hit(other);
        private void OnTriggerStay(Collider other) => Hit(other);
        private void OnDisable() => nextHits.Clear();
        private void Hit(Collider other)
        {
            if (Time.deltaTime <= 0f || (affectedLayers.value & (1 << other.gameObject.layer)) == 0) return;
            var health = other.GetComponentInParent<Health>();
            if (health == null || health.IsDead || nextHits.TryGetValue(health, out float next) && Time.time < next) return;
            // One interval per receiver, not per collider; failed hits do not burn spawn protection.
            if (health.ApplyDamage(new DamageInfo(damage, gameObject, other.ClosestPoint(transform.position), DamageKind.Environment)))
                nextHits[health] = Time.time + Mathf.Max(.02f, interval);
        }
    }
}
