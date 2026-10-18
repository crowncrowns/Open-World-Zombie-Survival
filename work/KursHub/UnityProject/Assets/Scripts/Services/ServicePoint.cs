using UnityEngine;
using ZombieSurvival.Combat;
using ZombieSurvival.Interaction;
using ZombieSurvival.Player;

namespace ZombieSurvival.Services
{
    /// <summary>Physical access shared by E, an open session and future service commands.</summary>
    public abstract class ServicePoint : Interactable
    {
        [SerializeField, InspectorName("Дистанция доступа, м"), Min(.1f), Tooltip("Максимальная дистанция от камеры игрока до поверхности коллайдера сервиса, м. Повторно проверяется во время сессии.")]
        private float accessDistance = 2.5f;
        [SerializeField, InspectorName("Коллайдер доступа"), Tooltip("Твёрдый коллайдер этого сервиса. Если не назначен, используется первый твёрдый дочерний Collider. Для concave Mesh/Terrain нужны реальные попадания лучей к центру/ближней границе bounds; при обоих промахах доступ отклоняется. Можно назначить простой Collider на корне обёртки.")]
        private Collider accessCollider;
        [SerializeField, InspectorName("Слои препятствий"), Tooltip("Слои стен и предметов, перекрывающих доступ. Собственные коллайдеры игрока исключаются; триггеры не перекрывают доступ.")]
        private LayerMask obstructionMask = ~0;
        private readonly RaycastHit[] hits = new RaycastHit[48];
        public abstract string SessionTitle { get; }
        public abstract string SessionDescription { get; }

        public override string GetPrompt(PlayerInteractor actor) => "Открыть: " + SessionTitle;
        public override bool CanInteract(PlayerInteractor actor)
        {
            if (actor == null || !actor.IsAvailable || !CanAccess(actor, out _)) return false;
            var session = actor.GetComponent<PlayerServiceSession>();
            return session != null && session.isActiveAndEnabled && !session.IsOpen;
        }
        protected override bool PerformInteraction(PlayerInteractor actor)
        { return actor != null && actor.GetComponent<PlayerServiceSession>() is PlayerServiceSession session && session.TryOpen(this); }

        public bool CanAccess(PlayerInteractor actor, out string reason)
        {
            if (!base.CanInteract(actor)) return Fail("Сервис недоступен.", out reason);
            if (actor == null || !actor.isActiveAndEnabled || actor.ViewCamera == null || !actor.ViewCamera.isActiveAndEnabled)
                return Fail("Игрок или камера недоступны.", out reason);
            var motor = actor.GetComponent<FirstPersonController>();
            if (motor == null || !motor.isActiveAndEnabled || !motor.IsAlive || motor.Parkour != null && motor.Parkour.OwnsMovement)
                return Fail("Игрок не может пользоваться сервисом.", out reason);
            var health = GetComponentInParent<Health>();
            if (health != null && (health.IsDead || !health.isActiveAndEnabled)) return Fail("Владелец сервиса недоступен.", out reason);
            if (Time.timeScale <= 0) return Fail("Сервис недоступен на паузе.", out reason);
            if (accessCollider == null)
                foreach (var c in GetComponentsInChildren<Collider>())
                    if (!c.isTrigger) { accessCollider = c; break; }
            if (accessCollider == null || !accessCollider.enabled || accessCollider.isTrigger || !accessCollider.gameObject.activeInHierarchy
                || !accessCollider.transform.IsChildOf(transform)) return Fail("Нет доступного коллайдера сервиса.", out reason);
            Vector3 origin = actor.ViewCamera.transform.position;
            Vector3 destination;
            if (accessCollider is BoxCollider || accessCollider is SphereCollider || accessCollider is CapsuleCollider
                || accessCollider is MeshCollider mesh && mesh.convex)
                destination = accessCollider.ClosestPoint(origin);
            else
            {
                // ClosestPoint is unsupported on concave meshes/terrain. Bounds are ray hints only:
                // only an actual surface hit grants access, otherwise fail closed.
                Vector3 direction = accessCollider.bounds.center - origin;
                RaycastHit surface = default;
                bool hitSurface = direction.sqrMagnitude > .000001f
                    && accessCollider.Raycast(new Ray(origin, direction.normalized), out surface, accessDistance + .015f);
                if (!hitSurface)
                {
                    direction = accessCollider.bounds.ClosestPoint(origin) - origin;
                    if (direction.sqrMagnitude < .000001f
                        || !accessCollider.Raycast(new Ray(origin, direction.normalized), out surface, accessDistance + .015f))
                        return Fail("Не видна доступная поверхность сервиса.", out reason);
                }
                destination = surface.point;
            }
            Vector3 delta = destination - origin;
            float distance = delta.magnitude;
            if (distance > accessDistance) return Fail("Вы отошли от сервиса.", out reason);
            if (distance > .001f)
            {
                int count = Physics.RaycastNonAlloc(origin, delta / distance, hits, distance + .015f, obstructionMask, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) return Fail("Путь к сервису перекрыт.", out reason);
                for (int i = 0; i < count; i++)
                    if (!hits[i].collider.transform.IsChildOf(actor.transform) && !hits[i].collider.transform.IsChildOf(transform)
                        && hits[i].distance < distance - .005f) return Fail("Путь к сервису перекрыт.", out reason);
            }
            reason = null; return true;
        }
        private static bool Fail(string message, out string reason) { reason = message; return false; }
    }
}
