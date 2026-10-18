using System;
using UnityEngine;
using UnityEngine.AI;
using ZombieSurvival.AI;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Persistence
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class WorldActorSaveAdapter : WorldSaveAdapter
    {
        [Serializable] private sealed class State
        {
            public float health;
            public bool dead, crawling, agentEnabled, brainEnabled, healthEnabled;
            public Vector3 patrolOrigin;
        }
        private State applied;
        internal bool WantsNavigation => applied != null && applied.agentEnabled && !applied.dead;
        public override string Key => "actor";
        public override string Capture()
        {
            var hp = GetComponent<Health>(); var brain = GetComponent<EnemyBrain>(); var agent = GetComponent<NavMeshAgent>();
            return JsonUtility.ToJson(new State { health = hp.Current, dead = hp.IsDead, crawling = GetComponent<EnemyCrawlPursuit>()?.IsCrawling == true,
                agentEnabled = agent != null && agent.enabled, brainEnabled = brain != null && brain.enabled, healthEnabled=hp.enabled,
                patrolOrigin = brain != null ? brain.SpawnPosition : transform.position });
        }
        public override void Validate(string json, SavedWorldObject owner)
        {
            var state = Read<State>(json); var hp = GetComponent<Health>(); var agent = GetComponent<NavMeshAgent>(); var crawl = GetComponent<EnemyCrawlPursuit>();
            if (GetComponent<ZombieSurvival.Player.FirstPersonController>() != null || !Finite(state.health) || state.health < 0 || state.health > hp.Maximum
                || state.dead != (state.health <= 0) || !ValidPosition(state.patrolOrigin) || state.crawling && crawl == null
                || state.agentEnabled && agent == null || state.brainEnabled && GetComponent<EnemyBrain>() == null)
                throw new InvalidOperationException("Некорректное состояние актёра: " + owner.id);
            // При недоступном NavMesh отказываем ДО переноса денег/предметов, не телепортируем на другой этаж.
            if (owner.exists && owner.active && !state.dead && state.agentEnabled)
            {
                int type = crawl != null ? crawl.SavedAgentType(state.crawling) : agent.agentTypeID;
                var filter = new NavMeshQueryFilter { agentTypeID = type, areaMask = agent.areaMask };
                if (!NavMesh.SamplePosition(owner.position, out var hit, .15f, filter) || Vector3.Distance(hit.position, owner.position) > .1f)
                    throw new InvalidOperationException("Для сохранённой позы актёра нет подходящего NavMesh: " + owner.id);
            }
        }
        public override void ApplySilent(string json)
        {
            applied = Read<State>(json); var hp = GetComponent<Health>();
            var agent = GetComponent<NavMeshAgent>(); if (agent != null) agent.enabled = false;
            foreach(var attack in GetComponents<EnemyAttack>())attack.ResetAfterWorldLoad();
            GetComponent<EnemyCrawlPursuit>()?.RestoreSavedPosture(applied.crawling);
            hp.RestoreWorldStateSilently(applied.health, applied.dead);
            hp.enabled=applied.healthEnabled;
            GetComponent<EnemyBrain>()?.RestoreAfterWorldLoad(applied.patrolOrigin, applied.brainEnabled);
        }
        public override void Publish()
        {
            if (applied == null) return;
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = applied.agentEnabled;
                if (agent.enabled && agent.isOnNavMesh) { agent.Warp(transform.position); agent.ResetPath(); agent.isStopped = applied.dead; }
            }
            GetComponent<EnemyBrain>()?.RestoreAfterWorldLoad(applied.patrolOrigin, applied.brainEnabled);
            GetComponent<EnemyAnimationBridge>()?.RestoreWorldPresentation();
            GetComponent<ZombieSurvival.Audio.NavMeshActorAudio>()?.ResetAfterWorldLoad();
            GetComponent<Health>().NotifyWorldRestore();
        }
    }
}
