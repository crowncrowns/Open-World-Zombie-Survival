using UnityEngine;
using UnityEngine.AI;
using ZombieSurvival.Combat;

namespace ZombieSurvival.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        public enum BehaviourState { Patrol, Investigate, Search, Chase, Attack, Dead }

        [Header("Навигация")]
        [Tooltip("Радиус патрулирования вокруг начальной точки зомби, м.")]
        [SerializeField, Min(0.1f)] private float patrolRadius = 30f;
        [Tooltip("Скорость спокойного патрулирования, м/с. Для реакции на звук используется отдельная Investigation Speed.")]
        [SerializeField, Min(0.1f)] private float patrolSpeed = 1.8f;
        [Tooltip("Максимальная скорость преследования стоя, м/с.")]
        [SerializeField, Min(0.1f)] private float maximumSpeed = 5.5f;
        [Tooltip("Пауза после достижения точки патрулирования, с.")]
        [SerializeField, Min(0f)] private float patrolPause = 2f;
        [Tooltip("На каком расстоянии считать точку маршрута достигнутой, м.")]
        [SerializeField, Min(0.1f)] private float arrivalDistance = 0.7f;
        [Tooltip("Период пересчёта пути к видимой цели, с.")]
        [SerializeField, Min(0.05f)] private float repathInterval = 0.25f;

        [Tooltip("Скорость проверки услышанного звука и поиска, м/с. Отдельна от медленного патруля.")]
        [SerializeField, Min(.1f)] private float investigationSpeed = 3f;

        [Header("Зрение")]
        [Tooltip("Локальная позиция глаз стоящего зомби относительно корня, м.")]
        [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.6f, 0f);
        [Tooltip("Предельная дальность зрения, м. Слух настраивается отдельно и работает за спиной.")]
        [SerializeField, Min(0f)] private float sightRange = 30f;
        [Tooltip("Полный угол зрения в градусах. На слух угол не влияет: звуки слышны и за спиной.")]
        [SerializeField, Range(1f, 360f)] private float fieldOfView = 110f;
        [Tooltip("Слои препятствий для зрения, низкого луча преследования и ударов зомби. Собственные коллайдеры и цель не перекрывают луч.")]
        [SerializeField] private LayerMask obstructionMask = ~0;
        [Tooltip("Интервал поиска видимых целей, с. Меньше — быстрее реакция, больше нагрузка.")]
        [SerializeField, Min(0.02f)] private float sightInterval = 0.15f;

        [Tooltip("Память о последнем зрительном контакте для замеченного входа под укрытие, с. Нужен открытый низкий луч; скрытая цель не отслеживается.")]
        [SerializeField, Range(.1f, 2f)] private float observedEntryMemory = .8f;

        [Header("Слух (игровой шум, независимо от аудио)")]
        [Tooltip("Предельная дальность слуха зомби, м. Реальная слышимость ограничена также шумом игрока и порогом слуха; работает на 360 градусов.")]
        [SerializeField, Min(0f)] private float hearingRange = 45f;
        [Tooltip("Минимальная воспринимаемая громкость для реакции. Уменьшите, чтобы зомби лучше слышал тихие шаги. Слух действует на 360 градусов.")]
        [SerializeField, Min(0f)] private float hearingThreshold = 0.08f;
        [Tooltip("Расстояние ослабления звука, м. На этой дистанции громкость падает примерно вдвое.")]
        [SerializeField, Min(0.1f)] private float hearingReferenceDistance = 5f;
        [Tooltip("Минимальная погрешность определения источника шума, м. Звук не раскрывает точную позицию игрока.")]
        [SerializeField, Min(0.1f)] private float minimumUncertainty = 2f;
        [Tooltip("Рост погрешности положения на каждый метр расстояния до звука.")]
        [SerializeField, Min(0f)] private float uncertaintyPerMetre = 0.25f;
        [Tooltip("Доля громкости, проходящая через препятствие: 0 — полностью глушит, 1 — не глушит. Это игровая модель слуха.")]
        [SerializeField, Range(0f, 1f)] private float occludedNoiseMultiplier = 0.6f;
        [Tooltip("Реагировать на шум актёров, чьи фракции не входят во враждебные. Обычно выключено.")]
        [SerializeField] private bool reactToFriendlyNoise;

        [Header("Поиск")]
        [Tooltip("Сколько секунд искать после потери цели или последнего услышанного звука.")]
        [SerializeField, Min(0.1f)] private float searchDuration = 12f;
        [Tooltip("Радиус поиска вокруг последней видимой позиции цели, м.")]
        [SerializeField, Min(0.1f)] private float visualMemoryRadius = 4f;
        [Tooltip("Пауза между проверками точек поиска, с.")]
        [SerializeField, Min(0f)] private float searchPointPause = 0.6f;

        [Header("Диагностика в Play")]
        [Tooltip("Текущее состояние поведения в Play. Диагностика, а не стартовая настройка.")]
        [SerializeField] private BehaviourState state;
        [Tooltip("Стартовая точка патруля в Play. Обновляется при сбросе поведения.")]
        [SerializeField] private Vector3 spawnPosition;
        [Tooltip("Центр текущей области поиска. По звуку содержит погрешность.")]
        [SerializeField] private Vector3 searchCenter;
        [Tooltip("Текущий радиус поиска в Play, м. Диагностика поведения.")]
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
        private ActorIdentity identity;
        private IDamageable health;
        private EnemyCrawlPursuit crawl;
        private float lastVisibleAt = float.NegativeInfinity;

        public BehaviourState State => state;
        public Vector3 SpawnPosition => started ? spawnPosition : transform.position;
        public Vector3 SearchCenter => searchCenter;
        public float SearchRadius => searchRadius;
        public bool IsAlive => DamageReceiver.IsAlive(health);
        private bool CanNavigate => agent != null && agent.enabled && agent.isOnNavMesh;
        private Vector3 CurrentEyeOffset => crawl != null && crawl.IsCrawling ? crawl.EyeOffset : eyeOffset;
        private Vector3 Eyes => transform.TransformPoint(CurrentEyeOffset);

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
            crawl = GetComponent<EnemyCrawlPursuit>();
            attacks = GetComponents<EnemyAttack>();
            identity = GetComponent<ActorIdentity>(); health = DamageReceiver.Find(this);
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
                brain.crawl = brain.GetComponent<EnemyCrawlPursuit>();
                brain.lastVisibleAt = float.NegativeInfinity;
                if (brain.crawl != null) brain.crawl.ResetPosture();
                brain.attacks = brain.GetComponents<EnemyAttack>();
                brain.identity = brain.GetComponent<ActorIdentity>(); brain.health = DamageReceiver.Find(brain);
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
            if (crawl != null) crawl.CancelObservedEntry();
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

        public void ResetBehaviour()
        {
            visibleTarget = null; activeAttack = null; hasDestination = false;
            lastVisibleAt = float.NegativeInfinity;
            if (crawl != null) crawl.ResetPosture();
            nextSightTime = nextRepathTime = waitUntil = 0f;
            foreach (var attack in attacks) if (attack != null) attack.Cancel();
            InitializeSpawn();
        }
        // Сохраняем только опору патруля, а не живую ссылку на скрытого игрока/корутину атаки.
        internal void RestoreAfterWorldLoad(Vector3 patrolOrigin, bool brainEnabled)
        {
            if (agent == null) Awake();
            foreach (var attack in attacks) if (attack != null) attack.Cancel();
            visibleTarget = null; activeAttack = null; hasDestination = false;
            lastSeenPosition = searchCenter = Vector3.zero; searchRadius = 0;
            lastVisibleAt = float.NegativeInfinity;
            nextSightTime = nextRepathTime = waitUntil = searchEndsAt = destinationExpires = 0;
            crawl?.CancelObservedEntry(); spawnPosition = patrolOrigin; started = true;
            state = IsAlive ? BehaviourState.Patrol : BehaviourState.Dead;
            enabled = brainEnabled;
            if (CanNavigate) { agent.ResetPath(); agent.isStopped = !IsAlive; }
        }

        private void Update()
        {
            if (!IsAlive)
            {
                if (crawl != null) crawl.CancelObservedEntry();
                state = BehaviourState.Dead; visibleTarget = null; activeAttack = null;
                foreach (var attack in attacks) if (attack != null && attack.IsBusy) attack.Cancel();
                if (CanNavigate) { agent.isStopped = true; if (agent.hasPath) agent.ResetPath(); }
                return;
            }
            if (state == BehaviourState.Dead) { state = BehaviourState.Patrol; hasDestination = false; }
            if (!CanNavigate) return;
            if (Time.time >= nextSightTime)
            {
                nextSightTime = Time.time + sightInterval;
                UpdateVision();
            }

            if (crawl != null && crawl.HasObservedEntry)
            {
                lastSeenPosition = crawl.ObservedEntryPosition;
                crawl.AdvanceObservedEntry(obstructionMask);
                if (crawl.HasObservedEntry)
                {
                    state = BehaviourState.Chase; agent.isStopped = false; agent.speed = maximumSpeed;
                    agent.stoppingDistance = .05f; agent.SetDestination(crawl.ApproachPosition); return;
                }
            }
            if (crawl != null && crawl.IsTransitioning) { agent.isStopped = true; return; }

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
                if (!CanSee(visibleTarget) && crawl != null && Time.time - lastVisibleAt <= observedEntryMemory
                    && visibleTarget.CanBeTargetedBy(transform)) crawl.TryFollowObservedTarget(visibleTarget, obstructionMask);
                if (!CanSee(visibleTarget)) LoseVisualTarget();
                else
                {
                    lastSeenPosition = visibleTarget.transform.position;
                    lastVisibleAt = Time.time;
                    if (crawl != null && !crawl.IsLowTarget(visibleTarget)) crawl.TryStand(obstructionMask);
                    if (crawl != null && crawl.IsTransitioning) { agent.isStopped = true; return; }
                    foreach (EnemyAttack attack in attacks)
                    {
                        if (attack == null || !attack.CanStart(visibleTarget, Eyes, obstructionMask)) continue;
                        agent.isStopped = true;
                        agent.ResetPath();
                        hasDestination = false;
                        Face(visibleTarget.AimPosition);
                        if (attack.TryStart(visibleTarget, CurrentEyeOffset, obstructionMask))
                        {
                            activeAttack = attack;
                            state = BehaviourState.Attack;
                            return;
                        }
                    }
                    state = BehaviourState.Chase;
                    agent.isStopped = false;
                    agent.speed = crawl != null && crawl.IsCrawling ? crawl.Speed : maximumSpeed;
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
            if (crawl != null) crawl.TryStand(obstructionMask);
            agent.speed = crawl != null && crawl.IsCrawling ? crawl.Speed : Mathf.Min(state == BehaviourState.Patrol ? patrolSpeed : investigationSpeed, maximumSpeed);
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
            if ((state == BehaviourState.Investigate && TryInvestigationPoint(center, out Vector3 destination))
                || TryChooseDestination(center, radius, out destination))
            {
                hasDestination = agent.SetDestination(destination);
                destinationExpires = Time.time + Mathf.Max(5f, Vector3.Distance(transform.position, destination) / agent.speed * 3f);
            }
            else waitUntil = Time.time + 1f;
        }

        private void UpdateVision()
        {
            // Память о ВИДИМОМ входе: проверяем низкий луч только для уже преследуемой цели.
            // Цель, которая изначально спряталась, не раскрывается этим механизмом.
            if (visibleTarget != null && visibleTarget.CanBeTargetedBy(transform) && crawl != null
                && Time.time - lastVisibleAt <= observedEntryMemory)
                crawl.TryFollowObservedTarget(visibleTarget, obstructionMask);
            CombatTarget nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (CombatTarget candidate in CombatTarget.ActiveTargets)
            {
                if (candidate == null || !CanSee(candidate)) continue;
                float distance = (candidate.AimPosition - Eyes).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = candidate;
                nearestDistance = distance;
            }
            if (nearest != null)
            {
                if (crawl != null && !crawl.IsLowTarget(nearest)) crawl.CancelObservedEntry();
                visibleTarget = nearest;
                lastSeenPosition = nearest.transform.position;
                lastVisibleAt = Time.time;
                hasDestination = false;
            }
            else if (visibleTarget != null) LoseVisualTarget();
        }

        public bool CanSee(CombatTarget target)
        {
            if (!IsAlive || target == null || !target.CanBeTargetedBy(transform)) return false;
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
            Face(center);
        }

        private void HearNoise(GameplayNoise.Signal noise)
        {
            if (!started || !isActiveAndEnabled || !IsAlive || !CanNavigate
                || noise.EmitterId == ActorIdentity.Entity(transform)) return;
            if (identity != null && noise.SourceFaction != FactionMask.None && !reactToFriendlyNoise
                && !identity.IsHostileTo(noise.SourceFaction)) return;
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

        private bool TryInvestigationPoint(Vector3 center, out Vector3 destination)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            destination = center;
            if (!NavMesh.SamplePosition(center, out var hit, 2f, filter)) return false;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            destination = hit.position; return true;
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
