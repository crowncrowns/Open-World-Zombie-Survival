using UnityEngine;
using UnityEngine.AI;
using ZombieSurvival.Combat;

namespace ZombieSurvival.AI
{
    /// <summary>Низкая навигация NPC. Решение о преследовании принимает EnemyBrain;
    /// этот компонент не ищет игрока и не хранит ссылку на скрытую цель.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed class EnemyCrawlPursuit : MonoBehaviour
    {
        [Tooltip("Может ли этот монстр преследовать цель ползком.")]
        [SerializeField] private bool crawlEnabled = true;
        [Tooltip("Тип агента низкого NavMesh. Создаётся настройкой полигона; для новой сцены нужен отдельный запечённый NavMesh этого типа.")]
        [SerializeField] private int crawlAgentType;
        [Tooltip("Скорость ползания, м/с.")]
        [SerializeField, Min(.1f)] private float crawlSpeed = 1.25f;
        [Tooltip("Высота капсулы ползущего монстра, м. Должна соответствовать низкому NavMesh.")]
        [SerializeField, Min(.4f)] private float crawlHeight = .65f;
        [Tooltip("Высота глаз ползущего монстра над землёй, м.")]
        [SerializeField, Min(.1f)] private float eyeHeight = .45f;
        [Tooltip("Продолжительность падения на четвереньки и подъёма, с. Во время падения монстр не двигается и не атакует.")]
        [SerializeField, Min(.1f)] private float transitionDuration = .65f;
        [Tooltip("Максимальная высота цели, при которой зомби пробует опуститься за ней, м.")]
        [SerializeField, Min(.4f)] private float lowTargetHeight = 1.1f;
        [Tooltip("Дальность замеченного входа под укрытие, м. Сквозь стены вход не обнаруживается.")]
        [SerializeField, Min(1f)] private float entryDistance = 10f;
        [Tooltip("На каком расстоянии от препятствия начинать опускаться, м. До входа монстр подходит стоя.")]
        [SerializeField, Range(.2f, 1.2f)] private float crawlStartDistance = .65f;
        [Tooltip("Сколько секунд помнить замеченный вход под укрытие. Хранится точка входа, а не ссылка на скрытого игрока.")]
        [SerializeField, Min(.5f)] private float entryMemorySeconds = 6f;
        private NavMeshAgent agent;
        private CapsuleCollider body;
        private int standingType;
        private float standingHeight, standingOffset;
        private Vector3 standingCenter;
        private float transitionEnds;
        public bool IsCrawling { get; private set; }
        public bool IsTransitioning => Time.time < transitionEnds;
        public float Speed => crawlSpeed;
        public Vector3 EyeOffset => Vector3.up * eyeHeight;
        public float PoseWeight { get; private set; }
        private readonly Collider[] overlaps = new Collider[48];
        private readonly RaycastHit[] passageHits = new RaycastHit[64];
        private bool entryPending;
        private float entryExpires;
        public bool HasObservedEntry => entryPending && Time.time < entryExpires;
        public Vector3 ObservedEntryPosition { get; private set; }
        public Vector3 ApproachPosition { get; private set; }

        private void Awake()
        {
            if (agent != null) return;
            agent = GetComponent<NavMeshAgent>(); body = GetComponent<CapsuleCollider>();
            standingType = agent.agentTypeID; standingHeight = body.height;
            standingCenter = body.center; standingOffset = agent.baseOffset;
        }
        internal int SavedAgentType(bool crawling) { if (agent == null) Awake(); return crawling ? crawlAgentType : standingType; }
        // У Instantiate живого ползущего актёра native-капсула уже низкая, а несериализованный кэш отсутствует.
        // Для проверочной копии переносим исходную стоячую конфигурацию, иначе она неверно проверит старую позу.
        internal void CopySavedConfigurationFrom(EnemyCrawlPursuit source)
        {
            if (source.agent == null) source.Awake();
            agent = GetComponent<NavMeshAgent>(); body = GetComponent<CapsuleCollider>();
            standingType = source.standingType; standingHeight = source.standingHeight;
            standingCenter = source.standingCenter; standingOffset = source.standingOffset;
        }
        // Вызывается загрузчиком при выключенном агенте. Не ищет цель и не меняет позицию по SamplePosition.
        internal void RestoreSavedPosture(bool crawling)
        {
            if (agent == null) Awake(); CancelObservedEntry();
            agent.agentTypeID = crawling ? crawlAgentType : standingType;
            body.height = crawling ? Mathf.Max(crawlHeight, body.radius * 2) : standingHeight;
            body.center = crawling ? new Vector3(standingCenter.x, body.height * .5f, standingCenter.z) : standingCenter;
            agent.height = body.height; agent.baseOffset = crawling ? 0 : standingOffset;
            IsCrawling = crawling; PoseWeight = crawling ? 1 : 0; transitionEnds = 0;
        }
        private void Update()
        {
            PoseWeight = Mathf.MoveTowards(PoseWeight, IsCrawling ? 1f : 0f, Time.deltaTime / transitionDuration);
        }
        public bool IsLowTarget(CombatTarget target)
        {
            var collider = target != null ? target.GetComponent<Collider>() : null;
            return collider != null && collider.bounds.size.y <= lowTargetHeight;
        }
        public bool TryFollowObservedTarget(CombatTarget target, LayerMask mask)
        {
            if (IsCrawling) return true;
            if (HasObservedEntry) return false;
            if (!isActiveAndEnabled || !crawlEnabled || !IsLowTarget(target) || !agent.enabled || !agent.isOnNavMesh
                || Vector3.Distance(transform.position, target.transform.position) > entryDistance) return false;
            // Только ранее увиденная цель и открытый НИЗКИЙ луч. Через сплошную стену не «угадываем» вход.
            if (!CombatVisibility.ClearLine(transform.TransformPoint(EyeOffset), target.AimPosition,
                transform, target.transform, mask)
                || !FindLowPassage(target.transform.position, mask, out float distance)) return false;
            if (distance <= crawlStartDistance) return EnterCrawl();
            // Подходим стоя к ЗАМЕЧЕННОМУ входу. Поза игрока на открытом месте ничего не переключает.
            ObservedEntryPosition = target.transform.position;
            if (!FindApproach(distance, ObservedEntryPosition)) return false;
            entryPending = true; entryExpires = Time.time + entryMemorySeconds;
            foreach (var attack in GetComponents<EnemyAttack>()) attack.Cancel();
            return false;
        }
        public bool AdvanceObservedEntry(LayerMask mask)
        {
            if (!HasObservedEntry || !FindLowPassage(ObservedEntryPosition, mask, out float distance))
            { CancelObservedEntry(); return false; }
            if (distance <= crawlStartDistance)
            {
                bool entered = EnterCrawl(); CancelObservedEntry(); return entered;
            }
            if (!FindApproach(distance, ObservedEntryPosition)) CancelObservedEntry();
            return false;
        }
        public void CancelObservedEntry() { entryPending = false; entryExpires = 0f; }
        private bool FindApproach(float distance, Vector3 destination)
        {
            Vector3 direction = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).normalized;
            Vector3 point = transform.position + direction * Mathf.Max(0f, distance - crawlStartDistance * .6f);
            var filter = new NavMeshQueryFilter { agentTypeID = standingType, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(point, out var hit, .8f, filter) || Mathf.Abs(hit.position.y - transform.position.y) > .3f) return false;
            ApproachPosition = hit.position; return true;
        }
        private bool FindLowPassage(Vector3 destination, LayerMask mask, out float distance)
        {
            Vector3 delta = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
            float length = delta.magnitude; distance = float.PositiveInfinity;
            if (length < .05f) return false;
            Vector3 direction = delta / length;
            // Проверяем полный объём стоящего тела, затем такой же коридор низкой капсулой.
            // Тонкая стенка, которую нельзя проползти, не является поводом ложиться.
            if (!CastPassage(standingHeight, direction, length, mask, out distance)) return false;
            return !CastPassage(Mathf.Max(crawlHeight, body.radius * 2f), direction, length, mask, out _);
        }
        private bool CastPassage(float height, Vector3 direction, float length, LayerMask mask, out float nearest)
        {
            float radius = body.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) * .95f;
            float worldHeight = Mathf.Max(radius * 2f, height * transform.lossyScale.y);
            Vector3 feet = transform.position + Vector3.up * ((standingCenter.y - standingHeight * .5f) * transform.lossyScale.y + .04f);
            int count = Physics.CapsuleCastNonAlloc(feet + Vector3.up * radius, feet + Vector3.up * (worldHeight - radius),
                radius, direction, passageHits, length, mask, QueryTriggerInteraction.Ignore);
            nearest = float.PositiveInfinity;
            if (count == passageHits.Length) { nearest = 0f; return true; }
            for (int i = 0; i < count; i++)
            {
                var hit = passageHits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<ActorIdentity>() != null
                    || hit.collider.bounds.max.y <= feet.y + .08f) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            return !float.IsPositiveInfinity(nearest);
        }
        private bool EnterCrawl()
        {
            if (!SwitchNavigation(crawlAgentType)) return false;
            foreach (var attack in GetComponents<EnemyAttack>()) attack.Cancel();
            body.height = Mathf.Max(crawlHeight, body.radius * 2f);
            body.center = new Vector3(standingCenter.x, body.height * .5f, standingCenter.z);
            agent.height = body.height; agent.baseOffset = 0f;
            IsCrawling = true; transitionEnds = Time.time + transitionDuration;
            return true;
        }
        public bool TryStand(LayerMask mask)
        {
            if (!IsCrawling) return true;
            Vector3 center = transform.TransformPoint(standingCenter);
            float radius = body.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            float half = Mathf.Max(0f, standingHeight * transform.lossyScale.y * .5f - radius);
            int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * half, center - Vector3.up * half,
                radius + .015f, overlaps, mask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (!overlaps[i].transform.IsChildOf(transform) && overlaps[i].bounds.max.y > transform.position.y + .1f) return false;
            if (!SwitchNavigation(standingType)) return false;
            body.height = standingHeight; body.center = standingCenter;
            agent.height = standingHeight; agent.baseOffset = standingOffset;
            IsCrawling = false; transitionEnds = Time.time + transitionDuration;
            return true;
        }
        public void ResetPosture()
        {
            CancelObservedEntry();
            if (agent == null) Awake();
            if (IsCrawling && agent.agentTypeID != standingType && !SwitchNavigation(standingType)) return;
            body.height = standingHeight; body.center = standingCenter;
            agent.height = standingHeight; agent.baseOffset = standingOffset;
            IsCrawling = false; PoseWeight = 0f; transitionEnds = 0f;
        }
        private bool SwitchNavigation(int type)
        {
            if (type == agent.agentTypeID) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = type, areaMask = agent.areaMask };
            // Не телепортируемся через стену или на другой этаж при смене типа агента.
            if (!NavMesh.SamplePosition(transform.position, out var hit, .4f, filter)) return false;
            int oldType = agent.agentTypeID;
            Vector3 origin = transform.position;
            agent.ResetPath(); agent.enabled = false; agent.agentTypeID = type;
            transform.position = hit.position; agent.enabled = true;
            if (agent.isOnNavMesh) return true;
            agent.enabled = false; agent.agentTypeID = oldType; transform.position = origin; agent.enabled = true;
            return false;
        }
    }
}
