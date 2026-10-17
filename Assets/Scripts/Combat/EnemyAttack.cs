using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ZombieSurvival.Combat
{
    /// <summary>Derive to implement shooting, spells, or other effects; the brain selects any ready attack.</summary>
    public abstract class EnemyAttack : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float range = 1.8f;
        [SerializeField, Min(0.05f)] private float cooldown = 1.5f;
        [SerializeField, Min(0f)] private float stopDelay = 0.15f;
        [SerializeField, Min(0f)] private float windup = 0.35f;
        [SerializeField, Min(0f)] private float recovery = 0.35f;
        [SerializeField] private UnityEvent onStarted = new UnityEvent();
        [SerializeField] private UnityEvent onImpact = new UnityEvent();
        private float nextAllowedTime;
        private Coroutine routine;
        public bool IsBusy { get; private set; }
        public float Windup => windup;
        public float Recovery => recovery;
        public event System.Action<EnemyAttack> AnimationStarted;
        public event System.Action<EnemyAttack> AttackFinished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetAttacksForPlay()
        {
            foreach (var attack in Object.FindObjectsByType<EnemyAttack>(FindObjectsInactive.Include))
                attack.ResetForPlay();
        }

        private void ResetForPlay()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            IsBusy = false;
            nextAllowedTime = 0f;
        }

        public virtual bool CanStart(CombatTarget target, Vector3 origin, LayerMask mask)
            => isActiveAndEnabled && !IsBusy && Time.time >= nextAllowedTime && CanReach(target, origin, mask);

        protected virtual bool CanReach(CombatTarget target, Vector3 origin, LayerMask mask)
            => target != null && target.IsAlive && Vector3.Distance(origin, target.AimPosition) <= range
                && CombatVisibility.ClearLine(origin, target.AimPosition, transform, target.transform, mask);

        public bool TryStart(CombatTarget target, Vector3 localOrigin, LayerMask mask)
        {
            if (!CanStart(target, transform.TransformPoint(localOrigin), mask)) return false;
            nextAllowedTime = Time.time + cooldown;
            IsBusy = true;
            routine = StartCoroutine(Execute(target, localOrigin, mask));
            return true;
        }

        private IEnumerator Execute(CombatTarget target, Vector3 localOrigin, LayerMask mask)
        {
            if (stopDelay > 0f) yield return new WaitForSeconds(stopDelay);
            AnimationStarted?.Invoke(this);
            onStarted.Invoke();
            if (windup > 0f) yield return new WaitForSeconds(windup);
            // Recheck at impact: targets can leave range or move behind a wall during windup.
            if (CanReach(target, transform.TransformPoint(localOrigin), mask))
            {
                ApplyEffect(target);
                onImpact.Invoke();
            }
            if (recovery > 0f) yield return new WaitForSeconds(recovery);
            IsBusy = false;
            routine = null;
            AttackFinished?.Invoke(this);
        }

        protected abstract void ApplyEffect(CombatTarget target);

        protected virtual void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            bool wasBusy = IsBusy;
            IsBusy = false;
            if (wasBusy) AttackFinished?.Invoke(this);
        }
    }
}
