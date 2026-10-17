using UnityEngine;
using UnityEngine.AI;
using ZombieSurvival.Combat;

namespace ZombieSurvival.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        public enum BehaviourState { Patrol, Investigate, Search, Chase, Attack }

        [Header("Navigation")]
        [SerializeField, Min(0.1f)] private float patrolRadius = 30f;
        [SerializeField, Min(0.1f)] private float patrolSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float maximumSpeed = 5.5f;
        [SerializeField, Min(0f)] private float patrolPause = 2f;
        [SerializeField, Min(0.1f)] private float arrivalDistance = 0.7f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.25f;

        [Header("Vision")]
        [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField, Min(0f)] private float sightRange = 30f;
        [SerializeField, Range(1f, 360f)] private float fieldOfView = 110f;
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField, Min(0.02f)] private float sightInterval = 0.15f;

        [Header("Hearing (gameplay noise, independent from audio)")]
        [SerializeField, Min(0f)] private float hearingRange = 45f;
        [SerializeField, Min(0f)] private float hearingThreshold = 0.08f;
        [SerializeField, Min(0.1f)] private float hearingReferenceDistance = 5f;
        [SerializeField, Min(0.1f)] private float minimumUncertainty = 2f;
        [SerializeField, Min(0f)] private float uncertaintyPerMetre = 0.25f;
        [Tooltip("Optional attenuation through obstacles. Not an acoustic simulation.")]
        [SerializeField, Range(0f, 1f)] private float occludedNoiseMultiplier = 0.6f;

        [Header("Searching")]
        [SerializeField, Min(0.1f)] private float searchDuration = 12f;
        [SerializeField, Min(0.1f)] private float visualMemoryRadius = 4f;
        [SerializeField, Min(0f)] private float searchPointPause = 0.6f;

        [Header("Runtime diagnostics")]
        [SerializeField] private BehaviourState state;
        [SerializeField] private Vector3 spawnPosition;
        [SerializeField] private Vector3 searchCenter;
        [SerializeField] private float searchRadius;

        private NavMeshAgent agent;
        private EnemyAttack[] attacks;
        private CombatTarget visibleTarget;
        private EnemyAttack activeAttack;
        private Vector3 lastSeenPosition;
        private float nextSightTime;
        private float nextRepathTime;
        private float searchEndsAt;
        private float waitUntil;
        private float destinationExpires;
        private bool hasDestination;
        private bool started;

        public BehaviourState State => state;
        public Vector3 SpawnPosition => spawnPosition;
        public Vector3 SearchCenter => searchCenter;
        public float SearchRadius => searchRadius;
        private bool CanNavigate => agent != null && agent.enabled && agent.isOnNavMesh;
        private Vector3 Eyes => transform.TransformPoint(eyeOffset);

        private void Reset()
        {
            // Adding just this component supplies a configurable default melee attack and agent.
            if (GetComponent<EnemyAttack>() == null) gameObject.AddComponent<MeleeAttack>();
            var capsule = GetComponent<CapsuleCollider>();
            var navigation = GetComponent<NavMeshAgent>();
            if (capsule != null && capsule.direction == 1 && navigation != null)
            {
                navigation.radius = capsule.radius;
                navigation.height = capsule.height;
                navigation.baseOffset = capsule.height * 0.5f - capsule.center.y;
                eyeOffset = capsule.center + Vector3.up * (capsule.height * 0.32f);
            }
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            attacks = GetComponents<EnemyAttack>();
        }

        private void OnEnable()
        {
            GameplayNoise.Emitted -= HearNoise;
            GameplayNoise.Emitted += HearNoise;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreBrainsForPlay()
        {
            foreach (var brain in Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include))
            {
                brain.agent = brain.GetComponent<NavMeshAgent>();
                brain.attacks = brain.GetComponents<EnemyAttack>();
                brain.visibleTarget = null;
                brain.activeAttack = null;
                brain.hasDestination = false;
                brain.nextSightTime = brain.nextRepathTime = brain.waitUntil = 0f;
                brain.InitializeSpawn();
                GameplayNoise.Emitted -= brain.HearNoise;
                if (brain.isActiveAndEnabled) GameplayNoise.Emitted += brain.HearNoise;
            }
        }
        private void OnDisable()
        {
            GameplayNoise.Emitted -= HearNoise;
            if (CanNavigate) agent.isStopped = true;
        }

        private void Start()
        {
            if (started) return;
            InitializeSpawn();
        }

        private void InitializeSpawn()
        {
            spawnPosition = transform.position;
            started = true;
            state = BehaviourState.Patrol;
            if (CanNavigate) { agent.ResetPath(); agent.isStopped = false; }
            if (!CanNavigate)
                Debug.LogWarning("EnemyBrain needs a baked NavMesh matching its NavMeshAgent type.", this);
        }

        private void Update()
        {
            if (!CanNavigate) return;
            if (Time.time >= nextSightTime)
            {
                nextSightTime = Time.time + sightInterval;
                UpdateVision();
            }

            if (activeAttack != null && activeAttack.IsBusy)
            {
                state = BehaviourState.Attack;
                agent.isStopped = true;
                return;
            }
            activeAttack = null;

            if (visibleTarget != null)
            {
                // Never use a stale visible reference to update the destination or start an attack.
                if (!CanSee(visibleTarget)) LoseVisualTarget();
                else
                {
                    lastSeenPosition = visibleTarget.transform.position;
                    foreach (EnemyAttack attack in attacks)
                    {
                        if (attack == null || !attack.CanStart(visibleTarget, Eyes, obstructionMask)) continue;
                        agent.isStopped = true;
                        agent.ResetPath();
                        hasDestination = false;
                        Face(visibleTarget.AimPosition);
                        if (attack.TryStart(visibleTarget, eyeOffset, obstructionMask))
                        {
                            activeAttack = attack;
                            state = BehaviourState.Attack;
                            return;
                        }
                    }
                    state = BehaviourState.Chase;
                    agent.isStopped = false;
                    agent.speed = maximumSpeed;
                    agent.stoppingDistance = arrivalDistance;
                    if (Time.time >= nextRepathTime)
                    {
                        nextRepathTime = Time.time + repathInterval;
                        agent.SetDestination(lastSeenPosition);
                    }
                    return;
                }
            }

            if (state == BehaviourState.Attack || state == BehaviourState.Chase)
                BeginSearch(lastSeenPosition, visualMemoryRadius);
            if ((state == BehaviourState.Search || state == BehaviourState.Investigate) && Time.time >= searchEndsAt)
            {
                state = BehaviourState.Patrol;
                hasDestination = false;
                waitUntil = 0f;
            }

            agent.isStopped = false;
            agent.speed = Mathf.Min(patrolSpeed, maximumSpeed);
            agent.stoppingDistance = arrivalDistance;
            if (hasDestination)
            {
                bool reached = !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= arrivalDistance + 0.15f);
                bool failed = Time.time >= destinationExpires || (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete);
                if (!reached && !failed) return;
                hasDestination = false;
                if (state == BehaviourState.Investigate) state = BehaviourState.Search;
                waitUntil = Time.time + (state == BehaviourState.Patrol ? patrolPause : searchPointPause);
            }
            if (Time.time < waitUntil) return;
            Vector3 center = state == BehaviourState.Patrol ? spawnPosition : searchCenter;
            float radius = state == BehaviourState.Patrol ? patrolRadius : searchRadius;
            if (TryChooseDestination(center, radius, out Vector3 destination))
            {
                hasDestination = agent.SetDestination(destination);
                destinationExpires = Time.time + Mathf.Max(5f, Vector3.Distance(transform.position, destination) / agent.speed * 3f);
            }
            else waitUntil = Time.time + 1f;
        }

        private void UpdateVision()
        {
            CombatTarget nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (CombatTarget candidate in CombatTarget.ActiveTargets)
            {
                if (candidate == null || candidate.transform.root == transform.root || !CanSee(candidate)) continue;
                float distance = (candidate.AimPosition - Eyes).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = candidate;
                nearestDistance = distance;
            }
            if (nearest != null)
            {
                visibleTarget = nearest;
                lastSeenPosition = nearest.transform.position;
                hasDestination = false;
            }
            else if (visibleTarget != null) LoseVisualTarget();
        }

        public bool CanSee(CombatTarget target)
        {
            if (target == null || !target.IsAlive) return false;
            Vector3 delta = target.AimPosition - Eyes;
            if (delta.sqrMagnitude > sightRange * sightRange) return false;
            if (Vector3.Angle(transform.forward, delta) > fieldOfView * 0.5f) return false;
            return CombatVisibility.ClearLine(Eyes, target.AimPosition, transform, target.transform, obstructionMask);
        }

        private void LoseVisualTarget()
        {
            visibleTarget = null;
            BeginSearch(lastSeenPosition, visualMemoryRadius);
        }

        private void BeginSearch(Vector3 center, float radius)
        {
            searchCenter = center;
            searchRadius = Mathf.Max(0.1f, radius);
            searchEndsAt = Time.time + searchDuration;
            state = BehaviourState.Investigate;
            hasDestination = false;
            waitUntil = 0f;
            if (CanNavigate) agent.ResetPath();
        }

        private void HearNoise(GameplayNoise.Signal noise)
        {
            if (!started || !isActiveAndEnabled || !CanNavigate
                || noise.EmitterId == transform.root.gameObject.GetEntityId()) return;
            // Sound cannot override a visible target, and never gives us a target Transform.
            if (visibleTarget != null && CanSee(visibleTarget)) return;
            if (activeAttack != null && activeAttack.IsBusy) return;
            float distance = Vector3.Distance(Eyes, noise.Position);
            if (distance > hearingRange || distance > noise.Range) return;
            float perceived = noise.Loudness / (1f + Mathf.Pow(distance / hearingReferenceDistance, 2f));
            if (!CombatVisibility.ClearLine(Eyes, noise.Position + Vector3.up * 0.2f,
                transform, null, obstructionMask, noise.EmitterId)) perceived *= occludedNoiseMultiplier;
            if (perceived < hearingThreshold) return;
            float uncertainty = minimumUncertainty + distance * uncertaintyPerMetre;
            Vector2 error = Random.insideUnitCircle * (uncertainty * 0.8f);
            Vector3 estimate = noise.Position + new Vector3(error.x, 0f, error.y);
            // Only the noisy estimate and radius survive this event. No emitter reference or exact position is kept.
            visibleTarget = null;
            BeginSearch(estimate, uncertainty);
        }

        private bool TryChooseDestination(Vector3 center, float radius, out Vector3 destination)
        {
            var path = new NavMeshPath();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 point = center + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, filter)) continue;
                Vector2 displacement = new Vector2(hit.position.x - center.x, hit.position.z - center.z);
                if (displacement.sqrMagnitude > radius * radius) continue;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                destination = hit.position;
                return true;
            }
            destination = transform.position;
            return false;
        }

        private void Face(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        private void OnValidate()
        {
            patrolSpeed = Mathf.Min(patrolSpeed, maximumSpeed);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            DrawCircle(Application.isPlaying ? spawnPosition : transform.position, patrolRadius);
            Gizmos.color = Color.yellow;
            DrawCircle(transform.position, hearingRange);
            Gizmos.color = Color.cyan;
            Vector3 eyes = transform.TransformPoint(eyeOffset);
            Vector3 left = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, fieldOfView * 0.5f, 0f) * transform.forward;
            Gizmos.DrawRay(eyes, left * sightRange);
            Gizmos.DrawRay(eyes, right * sightRange);
            Gizmos.DrawWireSphere(eyes, sightRange);
            if (Application.isPlaying && (state == BehaviourState.Search || state == BehaviourState.Investigate))
            {
                Gizmos.color = new Color(1f, 0.4f, 0f);
                DrawCircle(searchCenter, searchRadius);
            }
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            Vector3 previous = center + Vector3.right * radius;
            for (int i = 1; i <= 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
