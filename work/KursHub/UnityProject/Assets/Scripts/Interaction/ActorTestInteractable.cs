using UnityEngine;
using UnityEngine.AI;
using ZombieSurvival.AI;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Interaction
{
    /// <summary>Lab controls, not a player attack/inventory implementation.</summary>
    public sealed class ActorTestInteractable : Interactable
    {
        [Tooltip("Актёр для тестовой консоли урона или сброса. Не ссылка на скрытую цель AI.")]
        [SerializeField] private GameObject targetActor;
        [Tooltip("Включено — вернуть актёра на старт и оживить; выключено — нанести тестовый урон.")]
        [SerializeField] private bool resetActor;
        [Tooltip("Количество урона от тестовой консоли.")]
        [SerializeField, Min(0f)] private float testDamage = 25f;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private void Awake()
        {
            if (targetActor != null) { startPosition = targetActor.transform.position; startRotation = targetActor.transform.rotation; }
        }
        public override bool CanInteract(PlayerInteractor actor) => base.CanInteract(actor) && targetActor != null;
        public override string GetPrompt(PlayerInteractor actor) => resetActor ? "Вернуть зомби на старт" : "Нанести зомби " + testDamage.ToString("0.#") + " урона";
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            if (!resetActor)
            {
                var receiver = DamageReceiver.Find(targetActor.transform);
                bool hit = receiver != null && receiver.ApplyDamage(new DamageInfo(testDamage, actor.gameObject, targetActor.transform.position));
                actor.ShowFeedback(hit ? "Зомби получил урон" : "Зомби уже мёртв или защищён");return hit;
            }
            foreach (var attack in targetActor.GetComponents<EnemyAttack>()) attack.Cancel();
            var health = targetActor.GetComponent<Health>();
            var agent = targetActor.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.Warp(startPosition); }
            else targetActor.transform.position = startPosition;
            targetActor.transform.rotation = startRotation;
            if (health != null) health.Revive();
            var brain = targetActor.GetComponent<EnemyBrain>();if (brain != null) brain.ResetBehaviour();
            actor.ShowFeedback("Зомби возвращён на старт");return true;
        }
    }
}
