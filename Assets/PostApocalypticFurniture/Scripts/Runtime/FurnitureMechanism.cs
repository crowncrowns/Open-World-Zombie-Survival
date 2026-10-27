using System;
using UnityEngine;
using ZombieSurvival.Interaction;

namespace ZombieSurvival.Furniture
{
    [Serializable]
    public sealed class FurnitureMechanismState
    {
        public int marker;
        public string id;
        public float position, velocity;
        public bool targetOpen, blocked;
    }

    [Serializable]
    public sealed class FurnitureLinkedMotion
    {
        [Tooltip("Дополнительная деталь того же механизма, например спинка дивана.")]
        public Transform target;
        [Tooltip("Локальная ось поворота или выдвижения.")]
        public Vector3 axis = Vector3.right;
        [Tooltip("Ход в метрах либо угол в градусах.")]
        public float amount;
        [Tooltip("Вращение; иначе поступательное перемещение.")]
        public bool rotate = true;
        [HideInInspector] public Vector3 closedPosition;
        [HideInInspector] public Quaternion closedRotation = Quaternion.identity;
    }

    [Serializable]
    public sealed class FurnitureInterlock
    {
        [Tooltip("Другой механизм этого же предмета, положение которого освобождает путь.")]
        public FurnitureMechanism other;
        [Tooltip("Проверять перед открытием или раскладыванием этого механизма.")]
        public bool whenOpening = true;
        [Tooltip("Проверять перед закрытием или складыванием этого механизма.")]
        public bool whenClosing;
        [Range(0f, 1f), Tooltip("Минимальная допустимая доля открытия другого механизма, 0–1.")]
        public float minimum;
        [Range(0f, 1f), Tooltip("Максимальная допустимая доля открытия другого механизма, 0–1.")]
        public float maximum = 1f;
        [Tooltip("Требовать, чтобы цель движения другого механизма также оставалась в допустимом диапазоне.")]
        public bool requireDestination = true;
        [Tooltip("Русское объяснение правильного порядка действий.")]
        public string message;
        public bool Applies(bool opening) => opening ? whenOpening : whenClosing;
        public bool Allows(float fraction, bool destination) => fraction >= minimum - .0001f && fraction <= maximum + .0001f
            && (!requireDestination || (destination ? 1f : 0f) >= minimum - .0001f && (destination ? 1f : 0f) <= maximum + .0001f);
    }

    /// <summary>Input is exclusively the project's PlayerInteractor/Interact action (F).</summary>
    [DisallowMultipleComponent]
    public sealed class FurnitureMechanism : Interactable
    {
        [SerializeField, Tooltip("Уникальный код механизма внутри предмета; входит в сохранение.")]
        private string stableId;
        [SerializeField, Tooltip("Отдельный Transform оси. Корень шкафа сюда не назначать.")]
        private Transform pivot;
        [SerializeField, Tooltip("Локальная ось поворота или выдвижения.")]
        private Vector3 axis = Vector3.up;
        [SerializeField, Tooltip("Предельный угол в градусах либо ход в метрах.")]
        private float amount = 105f;
        [SerializeField, Tooltip("Поворот на угол; иначе выдвижение на расстояние.")]
        private bool rotate = true;
        [SerializeField, Min(.12f), Tooltip("Время плавного движения, с.")]
        private float motionTime = .7f;
        [SerializeField, Tooltip("Название детали для обратной связи.")]
        private string label = "Дверца";
        [SerializeField, Tooltip("Подсказка открытия без названия кнопки: кнопку показывает PlayerInteractor.")]
        private string openPrompt = "Открыть";
        [SerializeField, Tooltip("Подсказка закрытия без названия кнопки.")]
        private string closePrompt = "Закрыть";
        [SerializeField, Tooltip("Звук начала движения. AudioSource остаётся пространственным.")]
        private AudioClip movementClip;
        [SerializeField, Tooltip("Дополнительные синхронно движущиеся части раскладного механизма.")]
        private FurnitureLinkedMotion[] linked = Array.Empty<FurnitureLinkedMotion>();
        [SerializeField, Tooltip("Внутренние лампы отделения, включаемые открытием дверцы. Это обычный свет, не UV.")]
        private Light[] compartmentLights = Array.Empty<Light>();
        [SerializeField, Tooltip("Порядок безопасного раскладывания составной мебели. Условия проверяются до изменения цели и на каждом шаге.")]
        private FurnitureInterlock[] interlocks = Array.Empty<FurnitureInterlock>();
        [SerializeField, Tooltip("Останавливать деталь перед внешними коллайдерами. Собственная мебель исключается.")]
        private bool stopAtObstructions = true;
        [SerializeField, Tooltip("Слои препятствий для движущейся детали.")]
        private LayerMask obstructionMask = ~0;
        [SerializeField, HideInInspector] private Vector3 closedPosition;
        [SerializeField, HideInInspector] private Quaternion closedRotation = Quaternion.identity;
        [SerializeField, HideInInspector] private bool authoredPose;
        private float position, velocity;
        private bool targetOpen, blocked;
        private string lastObstruction;
        private AudioSource audioSource;
        private FurnitureAssembly owner;
        private BoxCollider[] movingBoxes;
        private CapsuleCollider[] movingCapsules;
        private readonly Collider[] contacts = new Collider[64];
        public string StableId => stableId;
        public Transform Pivot => pivot;
        public float OpenFraction => position;
        public bool TargetOpen => targetOpen;
        public bool IsBlocked => blocked;
        public string LastObstruction => lastObstruction;
        public bool IsMoving => Mathf.Abs(position - (targetOpen ? 1f : 0f)) > .0001f;
        public FurnitureLinkedMotion[] Linked => linked;
        public FurnitureInterlock[] Interlocks => interlocks;

        public void Configure(string id, Transform target, Vector3 localAxis, float extent, bool rotation,
            string displayLabel, string promptOpen = "Открыть", string promptClose = "Закрыть")
        {
            stableId = id; pivot = target; axis = localAxis.normalized; amount = extent; rotate = rotation;
            label = displayLabel; openPrompt = promptOpen; closePrompt = promptClose;
            CaptureAuthoredPose(); Cache();
        }
        public void ConfigureSound(AudioClip clip) { movementClip = clip; }
        public void ConfigureCompartmentLights(Light[] values)
        { compartmentLights = values ?? Array.Empty<Light>(); ApplyPose(position); }
        public void ConfigureInterlocks(FurnitureInterlock[] values)
        {
            foreach (var value in values ?? Array.Empty<FurnitureInterlock>())
                if (value == null || value.other == null || value.other == this || !Finite(value.minimum) || !Finite(value.maximum)
                    || value.minimum < 0f || value.maximum > 1f || value.minimum > value.maximum
                    || value.other.GetComponentInParent<FurnitureAssembly>(true) != GetComponentInParent<FurnitureAssembly>(true))
                    throw new ArgumentException("Условие порядка движения требует другого механизма той же мебели и диапазон 0–1.");
            interlocks = values ?? Array.Empty<FurnitureInterlock>();
        }
        public void ConfigureLinked(FurnitureLinkedMotion[] values)
        {
            linked = values ?? Array.Empty<FurnitureLinkedMotion>();
            foreach (var value in linked) if (value != null && value.target != null)
            { value.closedPosition = value.target.localPosition; value.closedRotation = value.target.localRotation; }
            Cache();
        }
        private void Awake() { EnsurePose(); Cache(); }
        private void OnEnable() { EnsurePose(); ApplyPose(position); }
        private void Cache()
        {
            owner = GetComponentInParent<FurnitureAssembly>(true);
            movingBoxes = pivot != null ? pivot.GetComponentsInChildren<BoxCollider>(true) : Array.Empty<BoxCollider>();
            movingCapsules = pivot != null ? pivot.GetComponentsInChildren<CapsuleCollider>(true) : Array.Empty<CapsuleCollider>();
        }
        private void EnsurePose() { if (pivot == null) pivot = transform; if (!authoredPose) CaptureAuthoredPose(); }
        private void CaptureAuthoredPose()
        {
            if (pivot == null) return;
            closedPosition = pivot.localPosition; closedRotation = pivot.localRotation; authoredPose = true;
        }
        public override bool CanInteract(PlayerInteractor actor) => base.CanInteract(actor) && pivot != null && axis.sqrMagnitude > .9f;
        public override string GetPrompt(PlayerInteractor actor)
        {
            if (!CanSetOpen(!targetOpen, out string reason)) return reason;
            return blocked ? "Проход занят — " + (targetOpen ? closePrompt.ToLowerInvariant() : openPrompt.ToLowerInvariant())
                : targetOpen ? closePrompt : openPrompt;
        }
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            if (!CanSetOpen(!targetOpen, out string reason)) { if (actor != null) actor.ShowFeedback(reason); return false; }
            SetOpen(!targetOpen);
            if (actor != null) actor.ShowFeedback(label + ": " + (targetOpen ? openPrompt : closePrompt));
            return true;
        }
        public bool CanSetOpen(bool value, out string reason)
        {
            foreach (var requirement in interlocks)
            {
                if (requirement == null || !requirement.Applies(value)) continue;
                if (requirement.other != null && requirement.Allows(requirement.other.position, requirement.other.targetOpen)) continue;
                reason = !string.IsNullOrWhiteSpace(requirement.message) ? requirement.message
                    : "Сначала установите деталь «" + (requirement.other != null ? requirement.other.label : "опора") + "» в безопасное положение";
                return false;
            }
            reason = null; return true;
        }
        public bool SetOpen(bool value, bool immediate = false)
        {
            if (!CanSetOpen(value, out _)) return false;
            EnsurePose();
            // Reversing an accepted F command must stop the old direction immediately, while
            // retaining the current pose and accelerating smoothly toward the new endpoint.
            if (targetOpen != value) velocity = 0f;
            targetOpen = value; blocked = false; lastObstruction = null;
            if (immediate) { position = value ? 1f : 0f; velocity = 0f; ApplyPose(position); }
            else PlayMovement();
            return true;
        }
        private void PlayMovement()
        {
            if (!Application.isPlaying || movementClip == null || !isActiveAndEnabled) return;
            if (audioSource == null) { audioSource = GetComponent<AudioSource>(); if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>(); }
            audioSource.playOnAwake = false; audioSource.spatialBlend = 1f; audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = .45f; audioSource.maxDistance = 8f; audioSource.volume = .45f; audioSource.dopplerLevel = 0f;
            audioSource.PlayOneShot(movementClip);
        }
        private void Update() => Step(Time.deltaTime);
        public void Step(float deltaTime)
        {
            if (deltaTime <= 0f || !IsMoving || pivot == null) return;
            if (!CanSetOpen(targetOpen, out _)) { blocked = true; velocity = 0f; return; }
            float goal = targetOpen ? 1f : 0f;
            float proposedVelocity = velocity;
            float proposed = Mathf.SmoothDamp(position, goal, ref proposedVelocity, Mathf.Max(.06f, motionTime * .3f), Mathf.Infinity, deltaTime);
            if (Mathf.Abs(proposed - goal) < .0001f) { proposed = goal; proposedVelocity = 0f; }
            // Sample long steps. This is a conservative positional obstruction guard, not a rigidbody simulation.
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(proposed - position) / .015f), 1, 128);
            float start = position;
            for (int i = 1; i <= steps; i++)
            {
                float candidate = Mathf.Lerp(start, proposed, i / (float)steps);
                ApplyPose(candidate);
                if (Application.isPlaying && stopAtObstructions && IsObstructed())
                {
                    ApplyPose(position); velocity = 0f; blocked = true; Physics.SyncTransforms(); return;
                }
                position = candidate;
            }
            velocity = proposedVelocity; blocked = false;
        }
        private bool IsObstructed()
        {
            if (movingBoxes == null || movingCapsules == null) Cache();
            lastObstruction = null;
            Physics.SyncTransforms();
            foreach (var box in movingBoxes)
            {
                if (box == null || !box.enabled || box.isTrigger || !box.gameObject.activeInHierarchy) continue;
                Vector3 scale = box.transform.lossyScale;
                Vector3 half = Vector3.Scale(box.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                half = Vector3.Max(Vector3.one * .0005f, half - Vector3.one * .002f);
                Vector3 centre = box.transform.TransformPoint(box.center);
                int count = Physics.OverlapBoxNonAlloc(centre, half, contacts, box.transform.rotation,
                    obstructionMask.value, QueryTriggerInteraction.Ignore);
                // A full non-alloc buffer may contain only our own compound colliders. Inspect
                // the complete query in this rare case rather than inventing an external block.
                if (count == contacts.Length)
                {
                    var all=Physics.OverlapBox(centre,half,box.transform.rotation,obstructionMask.value,QueryTriggerInteraction.Ignore);
                    if(HasExternalContact(box,all,all.Length))return true;
                }
                else if (HasExternalContact(box,contacts,count)) return true;
            }
            foreach (var capsule in movingCapsules)
            {
                if (capsule == null || !capsule.enabled || capsule.isTrigger || !capsule.gameObject.activeInHierarchy) continue;
                // Unity scales capsule cylinder length and cap radius separately. Use its real
                // collider bounds only for candidate discovery; native penetration below tests
                // the actual rotated/scaled capsule, without an approximate reconstructed shape.
                Bounds bounds = capsule.bounds;
                int count = Physics.OverlapBoxNonAlloc(bounds.center,bounds.extents+Vector3.one*.0001f,contacts,
                    Quaternion.identity,obstructionMask.value,QueryTriggerInteraction.Ignore);
                if (count == contacts.Length)
                {
                    var all=Physics.OverlapBox(bounds.center,bounds.extents+Vector3.one*.0001f,Quaternion.identity,obstructionMask.value,QueryTriggerInteraction.Ignore);
                    if(HasCapsuleExternalContact(capsule,all,all.Length))return true;
                }
                else if (HasCapsuleExternalContact(capsule,contacts,count)) return true;
            }
            return false;
        }
        private bool HasCapsuleExternalContact(CapsuleCollider capsule,Collider[] candidates,int count)
        {
            for(int i=0;i<count;i++)
            {
                var hit=candidates[i];
                if(hit==null||hit==capsule||hit.transform.IsChildOf(pivot))continue;
                if(owner!=null&&hit.transform.IsChildOf(owner.transform))continue;
                if(hit.transform.IsChildOf(transform))continue;
                if(!Physics.ComputePenetration(capsule,capsule.transform.position,capsule.transform.rotation,
                    hit,hit.transform.position,hit.transform.rotation,out _,out float penetration))continue;
                // No positional penetration tolerance: restore the preceding safe sample.
                lastObstruction=hit.name+" ("+hit.transform.root.name+") penetration="+penetration.ToString("G6");
                return true;
            }
            return false;
        }
        private bool HasExternalContact(Collider self,Collider[] candidates,int count)
        {
            for (int i = 0; i < count; i++)
            {
                var hit = candidates[i];
                if (hit == null || hit == self || hit.transform.IsChildOf(pivot)) continue;
                if (owner != null && hit.transform.IsChildOf(owner.transform)) continue;
                if (hit.transform.IsChildOf(transform)) continue;
                lastObstruction = hit.name + " (" + hit.transform.root.name + ")";
                return true;
            }
            return false;
        }
        private void ApplyPose(float fraction)
        {
            if (pivot == null) return;
            if (rotate) pivot.localRotation = closedRotation * Quaternion.AngleAxis(amount * fraction, axis);
            else pivot.localPosition = closedPosition + axis * (amount * fraction);
            foreach (var value in linked)
            {
                if (value == null || value.target == null) continue;
                if (value.rotate) value.target.localRotation = value.closedRotation * Quaternion.AngleAxis(value.amount * fraction, value.axis);
                else value.target.localPosition = value.closedPosition + value.axis.normalized * (value.amount * fraction);
            }
            foreach (var lamp in compartmentLights) if (lamp != null) lamp.enabled = fraction > .03f;
        }
        public FurnitureMechanismState CaptureState() => new FurnitureMechanismState
        { marker = 489, id = stableId, position = position, velocity = velocity, targetOpen = targetOpen, blocked = blocked };
        public bool ValidateState(FurnitureMechanismState value) => value != null && value.marker == 489 && value.id == stableId
            && Finite(value.position) && value.position >= 0f && value.position <= 1f && Finite(value.velocity) && Mathf.Abs(value.velocity) <= 100f;
        public bool ValidateInterlockState(FurnitureMechanismState value, FurnitureMechanismState[] future)
        {
            foreach (var requirement in interlocks)
            {
                if (requirement == null || !requirement.Applies(value.targetOpen)) continue;
                if (requirement.other == null || requirement.other.GetComponentInParent<FurnitureAssembly>(true) != GetComponentInParent<FurnitureAssembly>(true)) return false;
                FurnitureMechanismState otherState = null;
                foreach (var row in future) if (row != null && row.id == requirement.other.StableId) { if (otherState != null) return false; otherState = row; }
                if (otherState == null || !requirement.Allows(otherState.position, otherState.targetOpen)) return false;
            }
            return true;
        }
        public void RestoreStateSilently(FurnitureMechanismState value)
        { EnsurePose(); position = value.position; velocity = value.velocity; targetOpen = value.targetOpen; blocked = value.blocked; ApplyPose(position); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
