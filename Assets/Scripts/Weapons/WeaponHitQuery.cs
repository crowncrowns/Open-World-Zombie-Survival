using System;
using UnityEngine;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Weapons
{
    /// <summary>Общий запрос для игрока и будущего вооружённого NPC. Ближайшее твёрдое тело
    /// останавливает атаку, включая союзника. Урон разрешают общие отношения ActorIdentity.</summary>
    public static class WeaponHitQuery
    {
        public static bool Cast(Vector3 origin, Vector3 direction, float distance, float radius,
            Transform owner, LayerMask mask, out RaycastHit result)
        {
            var hits = radius > 0 ? Physics.SphereCastAll(origin,radius,direction,distance,mask,QueryTriggerInteraction.Ignore)
                : Physics.RaycastAll(origin,direction,distance,mask,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
                if (ActorIdentity.Root(hit.transform) != ActorIdentity.Root(owner)) { result=hit; return true; }
            result=default; return false;
        }
        public static bool Damage(RaycastHit hit, Transform owner, float amount)
        {
            var target=hit.collider.GetComponentInParent<CombatTarget>();
            if(target == null || !target.CanBeTargetedBy(owner)) return false;
            target.ReceiveDamage(new DamageInfo(amount,owner.gameObject,hit.point)); return true;
        }
    }
}
