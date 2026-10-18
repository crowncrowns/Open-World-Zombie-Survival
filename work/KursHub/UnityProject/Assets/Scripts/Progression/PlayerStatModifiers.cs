using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Combat;
using ZombieSurvival.Player;

namespace ZombieSurvival.Progression
{
    // Производный кэш, а не второй источник базовых характеристик/инвентаря/прокачки.
    [DisallowMultipleComponent]
    public sealed class PlayerStatModifiers : MonoBehaviour
    {
        private readonly Dictionary<StatId, double> flat = new Dictionary<StatId, double>();
        private readonly Dictionary<StatId, double> percent = new Dictionary<StatId, double>();
        public event Action Changed;

        public float Evaluate(StatId stat, float baseline)
        {
            if (!isActiveAndEnabled) return baseline;
            flat.TryGetValue(stat, out double added); percent.TryGetValue(stat, out double percentage);
            return Calculate(baseline, added, percentage);
        }

        internal static float Calculate(float baseline, double added, double percentage)
        {
            double value = (baseline + added) * (1d + percentage / 100d);
            // Предел защищает физику от неконечных значений неправильно настроенного ассета.
            return (float)Math.Max(0d, Math.Min(1000000d, value));
        }

        internal void Replace(SkillTreeDefinition tree, IReadOnlyDictionary<string, int> ranks, bool notify = true)
        {
            flat.Clear(); percent.Clear();
            if (tree != null && tree.Nodes != null) foreach (var node in tree.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.id) || !ranks.TryGetValue(node.id, out int rank) || rank <= 0 || node.effects == null) continue;
                foreach (var effect in node.effects)
                {
                    if (effect == null) continue;
                    flat.TryGetValue(effect.stat, out double added); flat[effect.stat] = added + (double)effect.flat * rank;
                    percent.TryGetValue(effect.stat, out double percentage); percent[effect.stat] = percentage + (double)effect.percent * rank;
                }
            }
            RefreshConsumers(notify); if(notify)Changed?.Invoke();
        }

        private void OnEnable() => RefreshConsumers();
        private void OnDisable() => RefreshConsumers();
        private void RefreshConsumers(bool notify = true)
        {
            GetComponent<Health>()?.RefreshDerivedMaximum(notify);
            GetComponent<PlayerStamina>()?.RefreshDerivedCapacity();
        }
        internal void NotifySavedRestore() => Changed?.Invoke();
    }
}
