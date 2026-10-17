using UnityEngine;

namespace ZombieSurvival.Interaction
{
    /// <summary>A hinged test door. Its solid collider remains enabled and stops before obstructions.</summary>
    [DisallowMultipleComponent]
    public sealed class DoorInteractable : Interactable
    {
        [Header("Door")]
        [Tooltip("Ось вращения двери.")]
        [SerializeField] private Transform hinge;
        [Tooltip("Твёрдый коллайдер движущегося полотна двери.")]
        [SerializeField] private BoxCollider leaf;
        [Tooltip("Угол открытия двери, градусов.")]
        [SerializeField, Range(10f, 160f)] private float openAngle = 100f;
        [Tooltip("Скорость поворота двери, градусов/с.")]
        [SerializeField, Min(1f)] private float angularSpeed = 110f;
        [Tooltip("Шаг проверки столкновений при повороте двери, градусов. Меньше — точнее, больше нагрузка.")]
        [SerializeField, Range(.25f, 5f)] private float collisionStepDegrees = 2f;
        [Tooltip("Слои препятствий, перекрывающих луч или действие. Для двери — препятствия её движению.")]
        [SerializeField] private LayerMask obstructionMask = ~0;
        [Tooltip("Дополнительный зазор двери от препятствий, м.")]
        [SerializeField, Min(0f)] private float collisionMargin = .005f;
        [Tooltip("Открывать дверь от игрока, выбирая сторону по его позиции.")]
        [SerializeField] private bool openAwayFromActor = true;
        [Tooltip("Текст действия открытия двери.")]
        [SerializeField] private string openPrompt = "Открыть дверь";
        [Tooltip("Текст действия закрытия двери.")]
        [SerializeField] private string closePrompt = "Закрыть дверь";
        private Quaternion closedRotation;
        private float angle, targetAngle, openSign = 1f;
        private bool open;
        private readonly Collider[] overlaps = new Collider[32];
        public bool IsOpen => open;
        public bool IsBlocked { get; private set; }
        public bool IsMoving => Mathf.Abs(angle - targetAngle) > .01f;

        private void Awake()
        {
            if (hinge == null) hinge = transform;
            closedRotation = hinge.localRotation;
        }
        public override bool CanInteract(PlayerInteractor actor) => base.CanInteract(actor) && leaf != null && hinge != null;
        public override string GetPrompt(PlayerInteractor actor) => IsBlocked ? "Проход занят — изменить направление" : open ? closePrompt : openPrompt;
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            if (!open && Mathf.Abs(angle) < .01f && openAwayFromActor)
                openSign = Vector3.Dot(actor.transform.position - hinge.position, hinge.forward) < 0f ? -1f : 1f;
            open = !open;
            targetAngle = open ? Mathf.Abs(openAngle) * openSign : 0f;
            IsBlocked = false;
            actor.ShowFeedback(open ? "Открываем дверь" : "Закрываем дверь");
            return true;
        }
        private void Update()
        {
            if (!IsMoving || hinge == null || leaf == null || Time.deltaTime <= 0f) return;
            float destination = Mathf.MoveTowards(angle, targetAngle, angularSpeed * Time.deltaTime);
            float step = Mathf.Max(.25f, collisionStepDegrees);
            IsBlocked = false;
            while (Mathf.Abs(angle - destination) > .001f)
            {
                float candidate = Mathf.MoveTowards(angle, destination, step);
                hinge.localRotation = closedRotation * Quaternion.Euler(0f, candidate, 0f);
                Physics.SyncTransforms();
                if (IsObstructed())
                {
                    hinge.localRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);
                    Physics.SyncTransforms(); IsBlocked = true; return;
                }
                angle = candidate;
            }
        }
        private bool IsObstructed()
        {
            Vector3 scale = leaf.transform.lossyScale;
            Vector3 extents = Vector3.Scale(leaf.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            extents = Vector3.Max(Vector3.one * .001f, extents - Vector3.one * collisionMargin);
            int count = Physics.OverlapBoxNonAlloc(leaf.transform.TransformPoint(leaf.center), extents, overlaps,
                leaf.transform.rotation, obstructionMask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return true;
            for (int i = 0; i < count; i++) if (!overlaps[i].transform.IsChildOf(transform)) return true;
            return false;
        }
    }
}
