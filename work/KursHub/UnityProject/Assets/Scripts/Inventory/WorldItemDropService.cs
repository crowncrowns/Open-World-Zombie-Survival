using UnityEngine;

namespace ZombieSurvival.Inventory
{
    /// <summary>Общая безопасная операция выброса для рюкзака и хотбара. Владение меняется после проверки места.</summary>
    public static class WorldItemDropService
    {
        public static bool TryDrop(Transform actor, Vector3 feetPosition, ItemStorage storage, int index, int amount,
            float distance, LayerMask mask)
        {
            if (actor == null || storage == null || distance <= 0) return false;
            var stack = storage.GetSlot(index);
            if (stack == null || stack.IsEmpty || amount <= 0 || amount > stack.Quantity || stack.Definition.WorldPrefab == null) return false;
            Vector3 origin = feetPosition + Vector3.up * .35f;
            Vector3 destination = origin + actor.forward * distance;
            foreach (var hit in Physics.SphereCastAll(origin, .19f, actor.forward, distance, mask, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(actor)) return false;
            if (!Physics.Raycast(destination + Vector3.up, Vector3.down, out var ground, 3f, mask, QueryTriggerInteraction.Ignore)
                || ground.normal.y < .6f || ground.transform.IsChildOf(actor)) return false;
            destination = ground.point + Vector3.up * .22f;
            foreach (var hit in Physics.OverlapSphere(destination, .18f, mask, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(actor)) return false;
            var obj = Object.Instantiate(stack.Definition.WorldPrefab, destination, Quaternion.identity);
            obj.SetActive(false);
            var world = obj.GetComponent<WorldItem>();
            if (world == null) { Object.Destroy(obj); return false; }
            if (!storage.TryExtract(index, amount, out var extracted)) { Object.Destroy(obj); return false; }
            try { world.Initialize(extracted); }
            catch (System.Exception exception)
            {
                storage.RestoreExtracted(index, extracted); Object.Destroy(obj); Debug.LogException(exception, actor); return false;
            }
            obj.SetActive(true); return true;
        }
    }
}
