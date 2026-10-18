using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieSurvival.Progression
{
    [Serializable]
    public sealed class SkillTabDefinition
    {
        [Tooltip("Постоянный ID вкладки. Узлы ссылаются на него; название можно менять отдельно."), InspectorName("ID вкладки")]
        public string id = "stats";
        [Tooltip("Название вкладки в окне прокачки."), InspectorName("Название")]
        public string title = "Характеристики";
        [Tooltip("Цвет узлов и связей этой вкладки."), InspectorName("Цвет")]
        public Color color = new Color(.95f, .65f, .16f);
    }

    [Serializable]
    public sealed class SkillEffect
    {
        [Tooltip("Характеристика, которую действительно читает игровой компонент. Для одного флага оставьте прибавки равными нулю."), InspectorName("Характеристика")]
        public StatId stat;
        [Tooltip("Прибавка за ранг в единицах характеристики: например +5 HP или +0,5 м/с. Сначала суммируются все прибавки."), InspectorName("Прибавка значения")]
        public float flat;
        [Tooltip("Процент за ранг: 1 означает +1%, 10 означает +10%. Формула: (база + сумма значений) × (1 + сумма процентов / 100)."), InspectorName("Прибавка, %")]
        public float percent;
        [Tooltip("Необязательный ID открываемой способности для потребителя HasAbility. Само имя не создаёт поведение; силу лечения задаёт числовой эффект Лечение предметами."), InspectorName("ID способности")]
        public string abilityId = "";
    }

    [Serializable]
    public sealed class SkillNodeDefinition
    {
        [Tooltip("Постоянный уникальный ID узла для зависимостей и сохранения. После публикации не переименовывать без миграции."), InspectorName("ID узла")]
        public string id = Guid.NewGuid().ToString("N");
        [Tooltip("Название улучшения в дереве."), InspectorName("Название")]
        public string title = "Новое улучшение";
        [Tooltip("Описание реального эффекта и условий покупки для игрока."), InspectorName("Описание"), TextArea(2, 5)]
        public string description = "";
        [Tooltip("ID вкладки, в которой показан узел. Зависимости могут вести между вкладками."), InspectorName("ID вкладки")]
        public string tabId = "stats";
        [Tooltip("Координаты верхнего левого угла узла на редактируемой схеме, пиксели. Положение не влияет на правила покупки."), InspectorName("Положение")]
        public Vector2 position;
        [Tooltip("Постоянные ID предшественников. Достаточно приобрести хотя бы один ранг нужного предшественника."), InspectorName("Предшественники")]
        public List<string> prerequisiteIds = new List<string>();
        [Tooltip("Включено — нужны все предшественники; выключено — достаточно любого одного. Пустой список означает корневой узел."), InspectorName("Нужны все предшественники")]
        public bool requireAllPrerequisites = true;
        [Tooltip("Цена каждого ранга, очки развития. Не рубли и не предметы."), InspectorName("Цена ранга, очки"), Min(1)]
        public int pointCost = 1;
        [Tooltip("Максимальное число покупок этого узла. Каждый ранг повторно добавляет перечисленные эффекты."), InspectorName("Максимум рангов"), Min(1)]
        public int maxRank = 1;
        [Tooltip("Список эффектов за один приобретённый ранг. Можно одновременно улучшить несколько характеристик и открыть флаг способности."), InspectorName("Эффекты ранга")]
        public List<SkillEffect> effects = new List<SkillEffect>();
        public string Id => id;
        public string Title => title;
        public string Description => description;
        public string TabId => tabId;
        public Vector2 Position => position;
        public IReadOnlyList<string> PrerequisiteIds => prerequisiteIds;
        public bool RequireAllPrerequisites => requireAllPrerequisites;
        public int PointCost => pointCost;
        public int MaxRank => maxRank;
        public IReadOnlyList<SkillEffect> Effects => effects;
    }

    [CreateAssetMenu(menuName = "Zombie Survival/Дерево развития", fileName = "CharacterSkillTree")]
    public sealed class SkillTreeDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Постоянный ID дерева в сохранении. Другое дерево требует явной миграции."), InspectorName("ID дерева")]
        private string treeId = "character-main";
        [SerializeField, Tooltip("Редактируемые вкладки дерева. Их ID уникальны и не зависят от отображаемого названия."), InspectorName("Вкладки")]
        private List<SkillTabDefinition> tabs = new List<SkillTabDefinition> { new SkillTabDefinition() };
        [SerializeField, Tooltip("Все узлы дерева: расположение, цена, зависимости и эффекты. Никаких фиксированных цепочек в контроллере движения."), InspectorName("Узлы")]
        private List<SkillNodeDefinition> nodes = new List<SkillNodeDefinition>();
        public string TreeId => treeId;
        public IReadOnlyList<SkillTabDefinition> Tabs => tabs;
        public IReadOnlyList<SkillNodeDefinition> Nodes => nodes;

        public bool TryGetNode(string id, out SkillNodeDefinition node)
        {
            if (nodes != null) foreach (var candidate in nodes)
                if (candidate != null && candidate.id == id) { node = candidate; return true; }
            node = null; return false;
        }

        /// <summary>Ошибочное дерево нельзя частично купить или молча исправить при загрузке.</summary>
        public bool Validate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(treeId)) return Fail("Не задан ID дерева.", out reason);
            if (tabs == null || tabs.Count == 0 || tabs.Count > 32) return Fail("Нужно от 1 до 32 вкладок.", out reason);
            var tabIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tab in tabs)
                if (tab == null || string.IsNullOrWhiteSpace(tab.id) || string.IsNullOrWhiteSpace(tab.title) || !tabIds.Add(tab.id))
                    return Fail("У каждой вкладки нужны уникальный ID и название.", out reason);
            if (nodes == null || nodes.Count == 0 || nodes.Count > 512) return Fail("Нужно от 1 до 512 узлов.", out reason);
            var lookup = new Dictionary<string, SkillNodeDefinition>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.id) || !lookup.TryAdd(node.id, node))
                    return Fail("Пустой или повторяющийся ID узла.", out reason);
                if (string.IsNullOrWhiteSpace(node.title) || !tabIds.Contains(node.tabId ?? "")) return Fail($"Узел {node.id}: нужны название и существующая вкладка.", out reason);
                if (!Finite(node.position.x) || !Finite(node.position.y) || node.pointCost < 1 || node.pointCost > 1000000 || node.maxRank < 1 || node.maxRank > 1000)
                    return Fail($"Узел {node.title}: неверные координаты, цена или число рангов.", out reason);
                if (node.effects == null || node.effects.Count == 0 || node.effects.Count > 64) return Fail($"Узел {node.title}: задайте 1–64 эффекта.", out reason);
                foreach (var effect in node.effects)
                    if (effect == null || !Enum.IsDefined(typeof(StatId), effect.stat) || !Finite(effect.flat) || !Finite(effect.percent) || Mathf.Abs(effect.flat) > 1000000 || Mathf.Abs(effect.percent) > 1000000
                        || effect.flat == 0 && effect.percent == 0 && string.IsNullOrWhiteSpace(effect.abilityId))
                        return Fail($"Узел {node.title}: эффект пуст, число неконечно или характеристика неизвестна.", out reason);
            }
            foreach (var node in nodes)
            {
                var unique = new HashSet<string>(StringComparer.Ordinal);
                if (node.prerequisiteIds == null) return Fail($"Узел {node.title}: список зависимостей отсутствует.", out reason);
                foreach (var id in node.prerequisiteIds)
                    if (string.IsNullOrWhiteSpace(id) || id == node.id || !lookup.ContainsKey(id) || !unique.Add(id))
                        return Fail($"Узел {node.title}: неизвестная, повторная или собственная зависимость.", out reason);
            }
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes)
                if (HasCycle(node, lookup, visited, visiting)) return Fail("Зависимости содержат цикл. Разорвите замкнутую цепочку.", out reason);
            reason = ""; return true;
        }

        private static bool HasCycle(SkillNodeDefinition node, Dictionary<string, SkillNodeDefinition> lookup, HashSet<string> visited, HashSet<string> visiting)
        {
            if (visited.Contains(node.id)) return false;
            if (!visiting.Add(node.id)) return true;
            foreach (string id in node.prerequisiteIds) if (HasCycle(lookup[id], lookup, visited, visiting)) return true;
            visiting.Remove(node.id); visited.Add(node.id); return false;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Fail(string message, out string reason) { reason = message; return false; }
    }
}
