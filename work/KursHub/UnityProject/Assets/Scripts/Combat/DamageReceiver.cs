using UnityEngine;

namespace ZombieSurvival.Combat
{
    /// <summary>Resolve child colliders to one reserve. Full Health has priority over legacy receivers.</summary>
    public static class DamageReceiver
    {
        public static IDamageable Find(Component source)
        {
            for (Transform node = source != null ? source.transform : null; node != null; node = node.parent)
            {
                var health = node.GetComponent<Health>();
                if (health != null) return health;
                foreach (var behaviour in node.GetComponents<MonoBehaviour>())
                    if (behaviour is IDamageable receiver) return receiver;
                // Do not accidentally use a container/parent actor's health.
                if (node.GetComponent<ActorIdentity>() != null) break;
            }
            return null;
        }
        public static bool IsAlive(IDamageable receiver) => receiver == null
            || (!(receiver is Object obj) || obj != null) && !receiver.IsDead;
    }
}
