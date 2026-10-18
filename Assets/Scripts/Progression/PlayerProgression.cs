using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Progression
{
    [Serializable]
    public sealed class SkillRankState
    {
        public string nodeId;
        public int rank;
    }

    // DTO содержит только полученные ресурсы и ранги. Бонусы характеристик всегда пересчитываются из дерева.
    [Serializable]
    public sealed class ProgressionState
    {
        public string treeId;
        public long experience;
        public int bonusPoints;
        public List<SkillRankState> ranks = new List<SkillRankState>();
    }

    public sealed class PreparedProgressionState
    {
        internal readonly PlayerProgression Owner;
        internal readonly SkillTreeDefinition Tree;
        internal readonly string TreeJson;
        internal readonly Dictionary<string, int> Ranks;
        internal readonly long Experience;
        internal readonly int BonusPoints;
        private readonly Dictionary<StatId, double> flat = new Dictionary<StatId, double>();
        private readonly Dictionary<StatId, double> percent = new Dictionary<StatId, double>();

        internal PreparedProgressionState(PlayerProgression owner, SkillTreeDefinition tree, ProgressionState state, Dictionary<string, int> ranks)
        {
            Owner = owner; Tree = tree; TreeJson = JsonUtility.ToJson(tree); Ranks = ranks;
            Experience = state.experience; BonusPoints = state.bonusPoints;
            foreach (var node in tree.Nodes)
            {
                if (!ranks.TryGetValue(node.id, out int rank)) continue;
                foreach (var effect in node.effects)
                {
                    flat.TryGetValue(effect.stat, out double added); flat[effect.stat] = added + (double)effect.flat * rank;
                    percent.TryGetValue(effect.stat, out double percentage); percent[effect.stat] = percentage + (double)effect.percent * rank;
                }
            }
        }

        /// <summary>Пределы HP/выносливости сохранённого дерева проверяются до изменения живого игрока.</summary>
        public float Evaluate(StatId stat, float baseline)
        {
            flat.TryGetValue(stat, out double added); percent.TryGetValue(stat, out double percentage);
            return PlayerStatModifiers.Calculate(baseline, added, percentage);
        }
    }

    [DisallowMultipleComponent, RequireComponent(typeof(PlayerStatModifiers))]
    public sealed class PlayerProgression : MonoBehaviour
    {
        [SerializeField, Tooltip("Редактируемый ассет вкладок, узлов, зависимостей и эффектов. Один источник правил развития."), InspectorName("Дерево развития")]
        private SkillTreeDefinition tree;
        [SerializeField, Min(0), Tooltip("Очки развития в новой игре. Не начисляются заново при включении компонента или загрузке."), InspectorName("Стартовые очки")]
        private int startingPoints = 3;
        [SerializeField, Min(1), Tooltip("Опыт для перехода с 1 на 2 уровень, XP. Последующие цены умножаются на рост."), InspectorName("Опыт первого уровня, XP")]
        private int experiencePerLevel = 100;
        [SerializeField, Min(1), Tooltip("Множитель цены каждого следующего уровня. 1 — одинаковая цена, 1,25 — рост на 25%."), InspectorName("Рост цены уровня")]
        private float levelCostGrowth = 1.25f;
        [SerializeField, Min(0), Tooltip("Очки развития за каждый полученный уровень. Уже потраченные очки определяются рангами узлов."), InspectorName("Очки за уровень")]
        private int pointsPerLevel = 1;
        [SerializeField, Range(1, 1000), Tooltip("Предельный уровень персонажа. Опыт сверх предела не даёт дополнительных очков."), InspectorName("Максимальный уровень")]
        private int maxLevel = 100;
        [SerializeField, Min(0), Tooltip("XP за убийство враждебного актёра атакой этого игрока. Урон падения/мира, союзники и чужие убийства не дают опыт."), InspectorName("Опыт за враждебное убийство, XP")]
        private int hostileKillExperience = 25;

        private readonly Dictionary<string, int> ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> abilities = new HashSet<string>(StringComparer.Ordinal);
        private long experience;
        private int bonusPoints;
        private bool initialized;
        private PlayerStatModifiers modifiers;
        private Health health;
        public SkillTreeDefinition Definition => tree;
        public long Experience => experience;
        public int Level => LevelFor(experience);
        public int AvailablePoints => (int)Math.Max(0L, Math.Min(int.MaxValue, EarnedPoints(experience, bonusPoints) - SpentPoints(ranks)));
        public long ExperienceToNextLevel => Level >= Mathf.Clamp(maxLevel, 1, 1000) ? 0L : Math.Max(0L, ThresholdForLevel(Level + 1) - experience);
        public event Action Changed;

        private void OnEnable() => Initialize(false);
        private void OnDisable() => Health.ActorKilled -= OnActorKilled;
        private void Initialize(bool fresh)
        {
            Health.ActorKilled -= OnActorKilled;
            modifiers = GetComponent<PlayerStatModifiers>(); health = GetComponent<Health>();
            if (fresh || !initialized) { experience = 0; bonusPoints = 0; ranks.Clear(); }
            initialized = true;
            if (isActiveAndEnabled) Health.ActorKilled += OnActorKilled;
            Rebuild();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreForPlay()
        {
            // Новая Play-сессия при отключённом Domain/Scene Reload не наследует старые очки/подписки.
            foreach (var progression in FindObjectsByType<PlayerProgression>(FindObjectsInactive.Include))
                if (progression.isActiveAndEnabled) progression.Initialize(true);
        }

        private void OnActorKilled(Health victim, DamageInfo damage)
        {
            if (hostileKillExperience <= 0 || damage.Kind != DamageKind.Attack || damage.Source == null || victim == null || health != null && health.IsDead) return;
            if (ActorIdentity.Root(damage.Source.transform) != ActorIdentity.Root(transform) || ActorIdentity.Root(victim.transform) == ActorIdentity.Root(transform)) return;
            var identity = GetComponent<ActorIdentity>();
            var targetIdentity = victim.GetComponentInParent<ActorIdentity>();
            if (identity == null || targetIdentity == null || !identity.IsHostileTo(targetIdentity)) return;
            AddExperience(hostileKillExperience);
        }

        public int RankOf(string nodeId) => nodeId != null && ranks.TryGetValue(nodeId, out int value) ? value : 0;
        public bool HasAbility(string id) => isActiveAndEnabled && !string.IsNullOrEmpty(id) && abilities.Contains(id);

        public void AddExperience(long amount)
        {
            if (!isActiveAndEnabled || amount <= 0) return;
            experience = amount > long.MaxValue - experience ? long.MaxValue : experience + amount;
            Changed?.Invoke();
        }

        /// <summary>Выдача награды внешней системой/тестовой консолью. Рубли и инвентарь не затрагиваются.</summary>
        public void AddBonusPoints(int amount)
        {
            if (!isActiveAndEnabled || amount <= 0) return;
            bonusPoints = (int)Math.Min(1000000L, (long)bonusPoints + amount); Changed?.Invoke();
        }

        public bool CanPurchase(string nodeId, out string reason)
        {
            if (!isActiveAndEnabled) return Fail("Прокачка отключена.", out reason);
            if (health != null && health.IsDead) return Fail("Нельзя учиться после смерти.", out reason);
            if (tree == null) return Fail("Дерево развития не назначено.", out reason);
            if (!tree.Validate(out reason)) return false;
            if (!tree.TryGetNode(nodeId, out var node)) return Fail("Узел не найден.", out reason);
            if (RankOf(nodeId) >= node.maxRank) return Fail("Достигнут максимальный ранг.", out reason);
            if (!PrerequisitesMet(node, ranks)) return Fail(node.requireAllPrerequisites ? "Сначала изучите все предыдущие узлы." : "Сначала изучите любой предыдущий узел.", out reason);
            if (AvailablePoints < node.pointCost) return Fail("Недостаточно очков развития.", out reason);
            reason = ""; return true;
        }

        public bool TryPurchase(string nodeId, out string reason)
        {
            if (!CanPurchase(nodeId, out reason)) return false;
            ranks[nodeId] = RankOf(nodeId) + 1;
            // Пересборка из рангов делает повторный Play/load идемпотентным; никогда не прибавлять бонус поверх бонуса.
            Rebuild(); return true;
        }

        public float PreviewStatAfterPurchase(string nodeId, StatId stat, float baseline)
        {
            double added = 0d, percentage = 0d;
            if (tree != null && tree.Nodes != null) foreach (var node in tree.Nodes)
            {
                if (node == null || node.effects == null) continue;
                int rank = RankOf(node.id) + (node.id == nodeId && RankOf(node.id) < node.maxRank ? 1 : 0);
                foreach (var effect in node.effects) if (effect != null && effect.stat == stat)
                { added += (double)effect.flat * rank; percentage += (double)effect.percent * rank; }
            }
            return PlayerStatModifiers.Calculate(baseline, added, percentage);
        }

        public ProgressionState CreateFreshState() => new ProgressionState { treeId = tree != null ? tree.TreeId : "", experience = 0, bonusPoints = 0 };
        public ProgressionState CaptureState()
        {
            var state = new ProgressionState { treeId = tree != null ? tree.TreeId : "", experience = experience, bonusPoints = bonusPoints };
            foreach (var pair in ranks) state.ranks.Add(new SkillRankState { nodeId = pair.Key, rank = pair.Value });
            state.ranks.Sort((a, b) => StringComparer.Ordinal.Compare(a.nodeId, b.nodeId)); return state;
        }

        public bool TryPrepareRestore(ProgressionState state, out PreparedProgressionState prepared, out string reason)
        {
            prepared = null;
            if (tree == null) return Fail("Не назначено дерево развития для загрузки.", out reason);
            if (!tree.Validate(out reason)) return false;
            if (state == null || state.treeId != tree.TreeId) return Fail("Сохранение использует другое дерево развития; нужна миграция.", out reason);
            if (state.experience < 0 || state.bonusPoints < 0 || state.bonusPoints > 1000000 || state.ranks == null || state.ranks.Count > tree.Nodes.Count)
                return Fail("Неверный опыт, наградные очки или список рангов.", out reason);
            var restored = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in state.ranks)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.nodeId) || !tree.TryGetNode(entry.nodeId, out var node)
                    || entry.rank <= 0 || entry.rank > node.maxRank || !restored.TryAdd(entry.nodeId, entry.rank))
                    return Fail("Сохранён неизвестный/удалённый узел или неверный ранг. Верните ID либо добавьте миграцию.", out reason);
            }
            foreach (var entry in restored)
                if (!tree.TryGetNode(entry.Key, out var node) || !PrerequisitesMet(node, restored)) return Fail("Сохранённые ранги нарушают зависимости дерева.", out reason);
            if (SpentPoints(restored) > EarnedPoints(state.experience, state.bonusPoints)) return Fail("На изученные узлы потрачено больше заработанных очков. Проверьте миграцию цен/уровней.", out reason);
            prepared = new PreparedProgressionState(this, tree, state, restored); reason = ""; return true;
        }

        public void ApplyPreparedRestore(PreparedProgressionState prepared)
            => ApplyPreparedRestore(prepared,true);
        internal void ApplyPreparedRestore(PreparedProgressionState prepared,bool notify)
        {
            if (prepared == null || prepared.Owner != this || prepared.Tree != tree || prepared.TreeJson != JsonUtility.ToJson(tree))
                throw new ArgumentException("Подготовленное развитие принадлежит другому игроку или дерево изменилось после проверки.");
            experience = prepared.Experience; bonusPoints = prepared.BonusPoints; ranks.Clear();
            foreach (var pair in prepared.Ranks) ranks.Add(pair.Key, pair.Value);
            initialized = true; Rebuild(notify);
        }

        private void Rebuild(bool notify = true)
        {
            abilities.Clear();
            bool valid = tree != null && tree.Validate(out _);
            if (valid) foreach (var node in tree.Nodes)
                if (node != null && RankOf(node.id) > 0 && node.effects != null) foreach (var effect in node.effects)
                    if (effect != null && !string.IsNullOrWhiteSpace(effect.abilityId)) abilities.Add(effect.abilityId);
            if (modifiers == null) modifiers = GetComponent<PlayerStatModifiers>();
            // Ошибка авторинга не должна превращаться в NaN физики или исключение Awake.
            // Сохраняем ранги для исправления/миграции, но временно убираем производные эффекты неверного графа.
            modifiers?.Replace(valid ? tree : null, ranks,notify); if(notify)Changed?.Invoke();
        }
        internal void NotifySavedRestore() => Changed?.Invoke();

        private long SpentPoints(IReadOnlyDictionary<string, int> purchased)
        {
            long spent = 0;
            foreach (var pair in purchased) if (tree != null && tree.TryGetNode(pair.Key, out var node)) spent += (long)node.pointCost * pair.Value;
            return spent;
        }
        private long EarnedPoints(long xp, int bonus) => Math.Max(0L, startingPoints) + (long)Math.Max(0, pointsPerLevel) * (LevelFor(xp) - 1) + bonus;
        private int LevelFor(long xp)
        {
            int level = 1, limit = Mathf.Clamp(maxLevel, 1, 1000);
            double total = 0d, cost = Math.Max(1, experiencePerLevel);
            double growth = float.IsNaN(levelCostGrowth) || float.IsInfinity(levelCostGrowth) ? 1d : Math.Max(1d, levelCostGrowth);
            // Один проход даже для большого дерева/предела уровней; UI вызывает свойство несколько раз за кадр.
            while (level < limit)
            {
                total += Math.Ceiling(cost);
                long threshold = double.IsInfinity(total) || total >= long.MaxValue ? long.MaxValue : (long)total;
                if (xp < threshold) break;
                level++; cost *= growth;
            }
            return level;
        }
        private long ThresholdForLevel(int level)
        {
            double total = 0d;
            double growth = float.IsNaN(levelCostGrowth) || float.IsInfinity(levelCostGrowth) ? 1d : Math.Max(1d, levelCostGrowth);
            double cost = Math.Max(1, experiencePerLevel);
            for (int index = 0; index < level - 1; index++)
            {
                total += Math.Ceiling(cost); cost *= growth;
                if (double.IsInfinity(total) || total >= long.MaxValue) return long.MaxValue;
            }
            return (long)total;
        }
        private static bool PrerequisitesMet(SkillNodeDefinition node, IReadOnlyDictionary<string, int> purchased)
        {
            if (node.prerequisiteIds.Count == 0) return true;
            foreach (var id in node.prerequisiteIds)
            {
                bool has = purchased.TryGetValue(id, out int rank) && rank > 0;
                if (node.requireAllPrerequisites && !has) return false;
                if (!node.requireAllPrerequisites && has) return true;
            }
            return node.requireAllPrerequisites;
        }
        private static bool Fail(string message, out string reason) { reason = message; return false; }
    }
}
